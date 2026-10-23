using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Remote automation server (development/QA builds only; started by
/// AutomationGate). Owns transports, the single client session, the event
/// log, log capture, and the input driver, and dispatches JSON-RPC requests
/// to the engine adapter after authentication, capability, rate-limit, and
/// schema checks. See docs/AUTOMATION.md.
/// </summary>
public partial class AutomationServer : Node, IAutomationServer
{
    [Signal] public delegate void SessionClosedEventHandler(string sessionId, string reason);

    public const string IsolatedSaveDir = "user://automation/saves";
    public const string IsolatedSettings = "user://automation/settings.cfg";
    public const string SessionFile = "user://automation/session.json";
    public const string AuditLog = "user://automation/audit.log";
    public const string FixtureDir = "res://automation/fixtures";
    public const int MaxCheckpoints = 16;

    private delegate Task<JsonObject> Handler(JsonObject p, DispatchContext ctx);

    public string Profile { get; private set; } = AutomationProfiles.Qa;
    public List<string> Capabilities { get; private set; } = new();
    public AutomationAuth Auth { get; private set; }
    public GodotAutomationAdapter Adapter { get; private set; }
    public AutomationEventLog Events { get; } = new();
    public AutomationLogCapture Logs { get; } = new();
    public AutomationInputDriver InputDriver { get; private set; }
    public AutomationHttpTransport Http { get; private set; }
    public AutomationWebSocketTransport WebSocket { get; private set; }
    public AutomationSession Session { get; private set; }
    public bool Active { get; private set; }
    /// <summary>Replaceable for tests.</summary>
    public AutomationRateLimiter RateLimiter { get; set; } = new();

    private JsonObject _config = new();
    private readonly Dictionary<string, Handler> _handlers;
    private SaveBackend _originalBackend;
    private string _originalSettingsPath;
    private bool _wroteSessionFile;
    private CanvasLayer _badge;

    public AutomationServer()
    {
        _handlers = new Dictionary<string, Handler>
        {
            ["session.hello"] = Sync(SessionHello),
            ["session.close"] = Sync(SessionClose),
            ["session.reset"] = SessionReset,
            ["protocol.capabilities"] = Sync(ProtocolCapabilities),
            ["protocol.schema"] = Sync((_, _) => AutomationProtocol.Ok(AutomationProtocol.SchemaDocument())),
            ["scene.current"] = Sync((_, _) => AutomationProtocol.Ok(Adapter.SceneInfo())),
            ["tree.query"] = Sync((p, _) => AutomationProtocol.Ok(new JsonObject { ["entities"] = GodotAutomationAdapter.ToArray(Adapter.Query(p)) })),
            ["entity.get"] = Sync(EntityGet),
            ["state.get"] = Sync((_, _) => AutomationProtocol.Ok(Adapter.State())),
            ["actions.available"] = Sync((_, _) => AutomationProtocol.Ok(new JsonObject { ["actions"] = GodotAutomationAdapter.ToArray(Adapter.AvailableActions()) })),
            ["action.perform"] = Sync((p, _) => Adapter.Perform(JsonNodes.Str(p["action"]), JsonNodes.Str(p["target"]), p["args"] as JsonObject ?? new JsonObject())),
            ["input.key"] = Sync((p, _) => InputResult(InputDriver.Key(JsonNodes.Str(p["key"]), JsonNodes.Str(p["mode"], "tap"), JsonNodes.Int(p["duration_ms"])))),
            ["input.action"] = Sync((p, _) => InputResult(InputDriver.Action(JsonNodes.Str(p["action"]), JsonNodes.Str(p["mode"], "tap"), JsonNodes.Int(p["duration_ms"])))),
            ["input.mouse"] = Sync((p, _) => InputResult(InputDriver.Mouse(JsonNodes.Str(p["mode"]), PointOf(p), JsonNodes.Str(p["button"], "left")))),
            ["input.gamepad"] = Sync(InputGamepad),
            ["input.touch"] = Sync((p, _) => InputResult(InputDriver.Touch(JsonNodes.Int(p["index"]), JsonNodes.Str(p["mode"], "tap"), PointOf(p)))),
            ["input.release_all"] = Sync((_, _) => AutomationProtocol.Ok(new JsonObject { ["released"] = InputDriver.ReleaseAll() })),
            ["wait.frames"] = WaitFrames,
            ["wait.until"] = WaitUntil,
            ["wait.event"] = WaitEvent,
            ["wait.settled"] = WaitSettled,
            ["assert.check"] = Sync(AssertCheck),
            ["checkpoint.create"] = Sync(CheckpointCreate),
            ["checkpoint.restore"] = CheckpointRestore,
            ["checkpoint.list"] = Sync((_, _) => AutomationProtocol.Ok(new JsonObject { ["names"] = GodotAutomationAdapter.ToArray(Session.Checkpoints.Keys) })),
            ["fixture.list"] = Sync((_, _) => AutomationProtocol.Ok(new JsonObject { ["fixtures"] = GodotAutomationAdapter.ToArray(FixtureIds()) })),
            ["fixture.load"] = FixtureLoad,
            ["recording.start"] = Sync(RecordingStart),
            ["recording.stop"] = Sync(RecordingStop),
            ["observe.screenshot"] = (p, _) => Adapter.Screenshot(JsonNodes.Int(p["max_width"])),
            ["observe.logs"] = Sync((p, _) => AutomationProtocol.Ok(Logs.LinesSince(JsonNodes.Long(p["since_seq"]), JsonNodes.Str(p["min_level"], "info"), JsonNodes.Int(p["limit"], 200)))),
            ["observe.events"] = Sync(ObserveEvents),
            ["events.subscribe"] = Sync(EventsSubscribe),
            ["observe.perf"] = Sync((_, _) => AutomationProtocol.Ok(Adapter.Perf())),
            ["app.quit"] = Sync(AppQuit),
            ["dev.teleport"] = Sync((p, _) => Adapter.Dev("dev.teleport", p)),
            ["dev.spawn"] = Sync((p, _) => Adapter.Dev("dev.spawn", p)),
            ["dev.command"] = Sync((p, _) => Adapter.Dev("dev.command", p)),
        };
    }

    /// <summary>True when every method in methods.json has a handler (checked by tests).</summary>
    public bool HasHandler(string method) => _handlers.ContainsKey(method);

    /// <summary>
    /// config keys: profile, port, bind, token, token_ttl_s, allow_remote,
    /// write_session_file (bool), badge (bool), idle_timeout_ms (int).
    /// Player-file isolation is unconditional: reset/fixtures delete and write
    /// saves, which must never reach the player's real save folder.
    /// </summary>
    public void Configure(JsonObject config)
    {
        _config = (JsonObject)config.DeepClone();
    }

    /// <summary>Returns "" on success, else a developer-facing reason (server not started).</summary>
    public string Start()
    {
        Profile = JsonNodes.Str(_config["profile"], "qa");
        if (!AutomationProfiles.IsKnown(Profile) || Profile == AutomationProfiles.Production)
            return $"profile '{Profile}' cannot start the automation server";
        string bind = JsonNodes.Str(_config["bind"], "127.0.0.1");
        int ttl = JsonNodes.Int(_config["token_ttl_s"], AutomationProtocol.DefaultTokenTtlS);
        if (!AutomationAuth.IsLoopback(bind))
        {
            if (!JsonNodes.Bool(_config["allow_remote"]))
                return "non-local bind address requires --automation-allow-remote";
            ttl = Math.Min(ttl, AutomationProtocol.RemoteMaxTokenTtlS);
        }
        string token = JsonNodes.Str(_config["token"]);
        if (token.Length == 0)
        {
            token = AutomationAuth.GenerateToken();
            _wroteSessionFile = JsonNodes.Bool(_config["write_session_file"], true);
        }
        else if (token.Length < AutomationProtocol.MinTokenLength)
        {
            return $"automation token must be at least {AutomationProtocol.MinTokenLength} characters";
        }
        Capabilities = AutomationProfiles.Capabilities(Profile);
        int port = JsonNodes.Int(_config["port"], 47801);

        InputDriver = new AutomationInputDriver(GetTree().Root);
        Adapter = new GodotAutomationAdapter(GetTree(), InputDriver);
        Http = new AutomationHttpTransport { Name = "Http", Server = this };
        AddChild(Http);
        Error err = Http.Listen(port, bind);
        if (err != Error.Ok)
        {
            Http.QueueFree();
            return $"could not listen on {bind}:{port} ({err})";
        }
        WebSocket = new AutomationWebSocketTransport { Name = "WebSocket", Server = this };
        AddChild(WebSocket);
        err = WebSocket.Listen(Http.Port() + 1, bind);
        if (err != Error.Ok)
        {
            Stop();
            return $"could not listen for WebSocket on {bind}:{Http.Port() + 1} ({err})";
        }
        Auth = new AutomationAuth(token, ttl, bind, Http.Port());

        OS.AddLogger(Logs);
        IsolatePlayerFiles();
        Adapter.ConnectEvents(Events);
        if (_wroteSessionFile)
            WriteSessionFile();
        if (JsonNodes.Bool(_config["badge"], true))
            ShowBadge();
        Active = true;
        ProcessMode = ProcessModeEnum.Always;
        GD.Print($"[automation] listening on http://{bind}:{Http.Port()} and ws://{bind}:{Http.Port() + 1} (profile {Profile})");
        Audit("server_started", new JsonObject { ["profile"] = Profile, ["bind"] = bind, ["port"] = Http.Port() });
        return "";
    }

    public override void _Process(double delta)
    {
        if (!Active)
            return;
        InputDriver.Tick();
        if (Session != null && Session.IdleMs() > JsonNodes.Int(_config["idle_timeout_ms"], AutomationProtocol.SessionIdleTimeoutMs))
            CloseSession("idle_timeout");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationExitTree)
            Stop();
    }

    /// <summary>
    /// Full cleanup: session state, held input, sockets, logger, event hooks,
    /// temporary files, and player-file isolation. Safe to call repeatedly.
    /// </summary>
    public void Stop()
    {
        if (Session != null)
            CloseSession("shutdown");
        InputDriver?.ReleaseAll();
        Http?.Stop();
        WebSocket?.Stop();
        if (!Active)
            return;
        Active = false;
        OS.RemoveLogger(Logs);
        Adapter?.Shutdown();
        if (_badge != null && IsInstanceValid(_badge))
            _badge.QueueFree();
        if (_wroteSessionFile)
            DirAccess.RemoveAbsolute(SessionFile);
        RestorePlayerFiles();
        Audit("server_stopped", new JsonObject());
    }

    // --- auth & dispatch ------------------------------------------------------------------

    public int Authenticate(string token) => Auth?.CheckToken(token) ?? AutomationProtocol.Unauthorized;

    public bool Granted(string capability) => Capabilities.Contains(capability);

    /// <summary>Validates and executes one JSON-RPC request. Always returns a response.</summary>
    public async Task<JsonObject> Dispatch(JsonNode request, DispatchContext ctx)
    {
        JsonNode id = null;
        if (request is JsonObject withId && withId.ContainsKey("id"))
        {
            id = withId["id"];
            if (JsonNodes.TryNumber(id, out double number) && Math.Abs(number - Math.Round(number)) < 1e-9 && Math.Abs(number) < 9.0e15)
                id = (long)Math.Round(number); // JSON numbers may arrive as 7.0; echo integer ids as integers
            if (!(id == null || JsonNodes.IsString(id) || JsonNodes.IsNumber(id)))
                return AutomationProtocol.ErrorResponse(null, AutomationProtocol.Error(AutomationProtocol.InvalidRequest, "id must be a string, number, or null"));
        }
        if (request is not JsonObject call || JsonNodes.Str(call["jsonrpc"]) != "2.0" || !JsonNodes.IsString(call["method"])
            || (call.ContainsKey("params") && call["params"] is not JsonObject))
            return AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(AutomationProtocol.InvalidRequest, "expected {jsonrpc: \"2.0\", id, method, params?: object}"));
        if (!Active)
            return AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(AutomationProtocol.Busy, "server is shutting down"));
        if (!RateLimiter.Allow())
            return AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(AutomationProtocol.RateLimited, "too many requests"));
        string method = call["method"].GetValue<string>();
        if (AutomationProtocol.Methods()[method] is not JsonObject spec || !_handlers.TryGetValue(method, out Handler handler))
            return AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(AutomationProtocol.MethodNotFound, $"unknown method '{(method.Length > 64 ? method[..64] : method)}'"));
        string capability = JsonNodes.Str(spec["capability"]);
        if (!Granted(capability))
        {
            Audit("denied", new JsonObject { ["method"] = method, ["capability"] = capability });
            return AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(AutomationProtocol.Forbidden,
                $"capability '{capability}' is not granted to profile '{Profile}'", new JsonObject { ["capability"] = capability }));
        }
        var parameters = (JsonObject)(call["params"]?.DeepClone() ?? new JsonObject());
        List<string> problems = AutomationSchemaValidator.Validate(parameters, (JsonObject)spec["params"]);
        if (problems.Count == 0 && parameters.ContainsKey("condition"))
            problems = AutomationPredicate.Validate(parameters["condition"]);
        if (problems.Count > 0)
            return AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(AutomationProtocol.InvalidParams, "invalid params",
                new JsonObject { ["problems"] = GodotAutomationAdapter.ToArray(problems.Take(10)) }));
        AutomationSession current = EnsureSession();
        current.Touch();
        if (!method.StartsWith("recording."))
            current.Record("request", new JsonObject { ["method"] = method, ["params"] = parameters.DeepClone() });
        JsonObject outcome;
        try
        {
            outcome = await handler(parameters, ctx);
        }
        catch (Exception e)
        {
            GD.PushError($"[automation] {method} failed: {e}");
            outcome = AutomationProtocol.Fail(AutomationProtocol.InternalError, $"{method} failed inside the game");
        }
        JsonObject error = outcome["error"] as JsonObject;
        Audit("call", new JsonObject { ["method"] = method, ["ok"] = outcome.ContainsKey("result"), ["code"] = JsonNodes.Int(error?["code"]) });
        if (error != null)
            return AutomationProtocol.ErrorResponse(id, error);
        return AutomationProtocol.Response(id, outcome["result"]);
    }

    public AutomationSession EnsureSession()
    {
        if (Session == null)
        {
            Session = new AutomationSession();
            Events.Emit("automation.session_opened", new JsonObject { ["session"] = Session.Id });
            Audit("session_opened", new JsonObject { ["session"] = Session.Id });
        }
        return Session;
    }

    public void CloseSession(string reason)
    {
        if (Session == null)
            return;
        AutomationSession closed = Session;
        Session = null;
        int released = InputDriver.ReleaseAll();
        Adapter.CleanupSession();
        foreach (string slot in closed.FixtureSlots)
            SaveService.Instance.DeleteSave(slot);
        closed.Checkpoints.Clear();
        closed.Recording = null;
        Events.Emit("automation.session_closed", new JsonObject { ["session"] = closed.Id, ["reason"] = reason, ["released_inputs"] = released });
        Audit("session_closed", new JsonObject
        {
            ["session"] = closed.Id, ["reason"] = reason, ["requests"] = closed.RequestCount,
            ["assertion_failures"] = closed.AssertionFailures.Count,
        });
        EmitSignalSessionClosed(closed.Id, reason);
    }

    // --- handlers (one per methods.json entry, registered in the constructor) -------------

    private static Handler Sync(Func<JsonObject, DispatchContext, JsonObject> handler) =>
        (p, ctx) => Task.FromResult(handler(p, ctx));

    private JsonObject SessionHello(JsonObject p, DispatchContext ctx) => AutomationProtocol.Ok(new JsonObject
    {
        ["protocol"] = AutomationProtocol.Name,
        ["version"] = AutomationProtocol.Version,
        ["session_id"] = Session.Id,
        ["profile"] = Profile,
        ["capabilities"] = GodotAutomationAdapter.ToArray(Capabilities),
        ["expires_at_unix"] = Auth.ExpiresAtUnix,
    });

    private JsonObject SessionClose(JsonObject p, DispatchContext ctx)
    {
        CloseSession("client_closed");
        return AutomationProtocol.Ok(new JsonObject { ["closed"] = true });
    }

    private Task<JsonObject> SessionReset(JsonObject p, DispatchContext ctx)
    {
        Session.Checkpoints.Clear();
        return Adapter.Reset(JsonNodes.Str(p["route"], "main_menu"));
    }

    private JsonObject ProtocolCapabilities(JsonObject p, DispatchContext ctx)
    {
        JsonObject methods = AutomationProtocol.Methods();
        List<string> available = methods
            .Where(pair => Granted(JsonNodes.Str(pair.Value?["capability"])))
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        JsonObject info = Adapter.Describe();
        List<string> caps = Capabilities.ToList();
        if (!JsonNodes.Bool(info["screenshots"]))
            caps.Remove("observe.screenshot");
        return AutomationProtocol.Ok(new JsonObject
        {
            ["protocol"] = AutomationProtocol.Name,
            ["version"] = AutomationProtocol.Version,
            ["engine"] = info,
            ["profile"] = Profile,
            ["capabilities"] = GodotAutomationAdapter.ToArray(caps),
            ["methods"] = GodotAutomationAdapter.ToArray(available),
            ["events"] = GodotAutomationAdapter.ToArray(new[]
            {
                "route.changing", "route.changed", "route.failed", "screen.opened", "screen.closed",
                "unit.moved", "unit.blocked", "unit.damaged", "unit.defeated", "mission.objective_reached",
                "mission.finished", "save.finished", "load.finished", "input.method_changed", "input.released",
                "locale.changed", "campaign.started", "campaign.loaded", "automation.session_opened", "automation.session_closed",
            }),
            ["transports"] = new JsonObject
            {
                ["http"] = new JsonObject { ["port"] = Http.Port() },
                ["websocket"] = new JsonObject { ["port"] = WebSocket.Port() },
            },
            ["limits"] = new JsonObject
            {
                ["max_body_bytes"] = AutomationProtocol.MaxBodyBytes,
                ["rate_per_second"] = AutomationProtocol.RatePerSecond,
                ["rate_burst"] = AutomationProtocol.RateBurst,
                ["idle_timeout_ms"] = JsonNodes.Int(_config["idle_timeout_ms"], AutomationProtocol.SessionIdleTimeoutMs),
                ["max_wait_ms"] = 120000,
            },
            ["fixtures"] = GodotAutomationAdapter.ToArray(FixtureIds()),
        });
    }

    private JsonObject EntityGet(JsonObject p, DispatchContext ctx)
    {
        bool deep = JsonNodes.Bool(p["deep"]);
        if (deep && !Granted("inspect.deep"))
            return AutomationProtocol.Fail(AutomationProtocol.Forbidden, "deep inspection needs capability 'inspect.deep'", new JsonObject { ["capability"] = "inspect.deep" });
        string id = JsonNodes.Str(p["id"]);
        JsonObject found = Adapter.Entity(id, deep);
        return found != null ? AutomationProtocol.Ok(found) : AutomationProtocol.Fail(AutomationProtocol.NotFound, $"no entity '{id}'");
    }

    private JsonObject InputResult(string problem)
    {
        if (problem.Length > 0)
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, problem);
        return AutomationProtocol.Ok(new JsonObject { ["held"] = GodotAutomationAdapter.ToArray(InputDriver.Held()) });
    }

    private static Vector2 PointOf(JsonObject p) => new((float)JsonNodes.Double(p["x"]), (float)JsonNodes.Double(p["y"]));

    private JsonObject InputGamepad(JsonObject p, DispatchContext ctx)
    {
        if (p.ContainsKey("button") == p.ContainsKey("axis"))
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, "pass exactly one of button or axis");
        if (p.ContainsKey("axis"))
        {
            if (!p.ContainsKey("value"))
                return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, "axis needs value");
            return InputResult(InputDriver.GamepadAxis(JsonNodes.Str(p["axis"]), (float)JsonNodes.Double(p["value"])));
        }
        return InputResult(InputDriver.GamepadButton(JsonNodes.Str(p["button"]), JsonNodes.Str(p["mode"], "tap"), JsonNodes.Int(p["duration_ms"])));
    }

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task<JsonObject> WaitFrames(JsonObject p, DispatchContext ctx)
    {
        int count = JsonNodes.Int(p["count"]);
        for (int i = 0; i < count; i++)
        {
            await NextFrame();
            if (!Active)
                return AutomationProtocol.Fail(AutomationProtocol.Busy, "server stopped");
        }
        return AutomationProtocol.Ok(new JsonObject { ["frames"] = count });
    }

    private async Task<JsonObject> WaitUntil(JsonObject p, DispatchContext ctx)
    {
        int timeout = JsonNodes.Int(p["timeout_ms"], 10000);
        ulong started = Time.GetTicksMsec();
        ulong deadline = started + (ulong)timeout;
        int frames = 0;
        var condition = (JsonObject)p["condition"];
        while (true)
        {
            AutomationPredicate.Outcome outcome = AutomationPredicate.Evaluate(condition, Resolve);
            if (outcome.Passed)
                return AutomationProtocol.Ok(new JsonObject
                {
                    ["met"] = true, ["frames"] = frames, ["elapsed_ms"] = (long)(Time.GetTicksMsec() - started), ["actual"] = outcome.Actual,
                });
            if (Time.GetTicksMsec() >= deadline)
                return AutomationProtocol.Fail(AutomationProtocol.Timeout, $"condition not met within {timeout} ms",
                    new JsonObject { ["actual"] = outcome.Actual, ["frames"] = frames });
            await NextFrame();
            frames++;
            if (!Active)
                return AutomationProtocol.Fail(AutomationProtocol.Busy, "server stopped");
        }
    }

    private async Task<JsonObject> WaitEvent(JsonObject p, DispatchContext ctx)
    {
        long after = JsonNodes.Long(p["after_seq"], Events.LastSeq);
        int timeout = JsonNodes.Int(p["timeout_ms"], 10000);
        ulong deadline = Time.GetTicksMsec() + (ulong)timeout;
        string type = JsonNodes.Str(p["type"]);
        var match = p["match"] as JsonObject ?? new JsonObject();
        while (true)
        {
            foreach (JsonObject entry in Events.Since(after, new[] { type }, AutomationProtocol.EventBuffer))
                if (AutomationEventLog.Matches(entry, type, match))
                    return AutomationProtocol.Ok(new JsonObject { ["met"] = true, ["event"] = entry });
            if (Time.GetTicksMsec() >= deadline)
                return AutomationProtocol.Fail(AutomationProtocol.Timeout, $"no '{type}' event within {timeout} ms", new JsonObject { ["last_seq"] = Events.LastSeq });
            await NextFrame();
            if (!Active)
                return AutomationProtocol.Fail(AutomationProtocol.Busy, "server stopped");
        }
    }

    private async Task<JsonObject> WaitSettled(JsonObject p, DispatchContext ctx)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)JsonNodes.Int(p["timeout_ms"], 10000);
        int frames = 0;
        int stable = 0;
        while (true) // settled on two consecutive frames
        {
            stable = Adapter.IsSettled() ? stable + 1 : 0;
            if (stable >= 2)
                break;
            if (Time.GetTicksMsec() >= deadline)
                return AutomationProtocol.Fail(AutomationProtocol.Timeout, "game did not settle", new JsonObject { ["state"] = Adapter.SceneInfo() });
            await NextFrame();
            frames++;
            if (!Active)
                return AutomationProtocol.Fail(AutomationProtocol.Busy, "server stopped");
        }
        return AutomationProtocol.Ok(new JsonObject { ["met"] = true, ["frames"] = frames });
    }

    private JsonObject AssertCheck(JsonObject p, DispatchContext ctx)
    {
        var condition = (JsonObject)p["condition"];
        AutomationPredicate.Outcome outcome = AutomationPredicate.Evaluate(condition, Resolve);
        string message = JsonNodes.Str(p["message"]);
        if (!outcome.Passed)
        {
            Session.AssertionFailures.Add(new JsonObject { ["condition"] = condition.DeepClone(), ["actual"] = JsonNodes.Copy(outcome.Actual), ["message"] = message });
            Audit("assertion_failed", new JsonObject { ["message"] = message });
        }
        return AutomationProtocol.Ok(new JsonObject { ["passed"] = outcome.Passed, ["actual"] = outcome.Actual, ["message"] = message });
    }

    private JsonObject CheckpointCreate(JsonObject p, DispatchContext ctx)
    {
        string name = JsonNodes.Str(p["name"]);
        if (Session.Checkpoints.Count >= MaxCheckpoints && !Session.Checkpoints.ContainsKey(name))
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"at most {MaxCheckpoints} checkpoints per session");
        Session.Checkpoints[name] = Adapter.CaptureCheckpoint();
        return AutomationProtocol.Ok(new JsonObject { ["name"] = name });
    }

    private async Task<JsonObject> CheckpointRestore(JsonObject p, DispatchContext ctx)
    {
        string name = JsonNodes.Str(p["name"]);
        if (!Session.Checkpoints.TryGetValue(name, out JsonObject snapshot))
            return AutomationProtocol.Fail(AutomationProtocol.NotFound, $"no checkpoint '{name}'");
        JsonObject outcome = await Adapter.RestoreCheckpoint((JsonObject)snapshot.DeepClone());
        if (outcome["result"] is JsonObject result)
            result["name"] = name;
        return outcome;
    }

    private async Task<JsonObject> FixtureLoad(JsonObject p, DispatchContext ctx)
    {
        string fixture = JsonNodes.Str(p["fixture"]);
        if (!FixtureIds().Contains(fixture))
            return AutomationProtocol.Fail(AutomationProtocol.NotFound, $"no approved fixture '{fixture}'");
        if (JsonNodes.Parse(FileAccess.GetFileAsString($"{FixtureDir}/{fixture}.json")) is not JsonObject data)
            return AutomationProtocol.Fail(AutomationProtocol.InternalError, "fixture file is malformed");
        bool writeSave = JsonNodes.Bool(p["write_save"]);
        JsonObject outcome = await Adapter.ApplyFixture(data, writeSave);
        if (writeSave && outcome.ContainsKey("result"))
            Session?.FixtureSlots.Add(SaveService.DefaultSlot);
        return outcome;
    }

    private JsonObject RecordingStart(JsonObject p, DispatchContext ctx)
    {
        Session.Recording = new JsonArray();
        Events.EventAdded -= RecordEvent;
        Events.EventAdded += RecordEvent;
        return AutomationProtocol.Ok(new JsonObject { ["recording"] = true });
    }

    private JsonObject RecordingStop(JsonObject p, DispatchContext ctx)
    {
        JsonArray entries = Session.Recording;
        Session.Recording = null;
        Events.EventAdded -= RecordEvent;
        if (entries == null)
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, "no recording in progress");
        return AutomationProtocol.Ok(new JsonObject { ["entries"] = entries });
    }

    private JsonObject ObserveEvents(JsonObject p, DispatchContext ctx)
    {
        List<string> types = (p["types"] as JsonArray ?? new JsonArray()).Select(t => JsonNodes.Str(t)).ToList();
        List<JsonObject> found = Events.Since(JsonNodes.Long(p["since_seq"]), types, JsonNodes.Int(p["limit"], 200));
        return AutomationProtocol.Ok(new JsonObject { ["events"] = AutomationEventLog.ToArray(found), ["last_seq"] = Events.LastSeq });
    }

    private JsonObject EventsSubscribe(JsonObject p, DispatchContext ctx)
    {
        if (ctx?.Transport != "ws" || ctx.Peer == null)
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, "events.subscribe needs the WebSocket transport; use observe.events over HTTP");
        List<string> types = (p["types"] as JsonArray ?? new JsonArray()).Select(t => JsonNodes.Str(t)).ToList();
        ctx.Peer.Subscriptions = types;
        return AutomationProtocol.Ok(new JsonObject { ["subscribed"] = GodotAutomationAdapter.ToArray(types) });
    }

    private JsonObject AppQuit(JsonObject p, DispatchContext ctx)
    {
        int code = JsonNodes.Int(p["exit_code"]);
        Audit("quit_requested", new JsonObject { ["exit_code"] = code });
        // Let the transport deliver this response first, then clean up and quit.
        GetTree().CreateTimer(0.25, true, false, true).Timeout += () =>
        {
            Stop();
            GetTree().Quit(code);
        };
        return AutomationProtocol.Ok(new JsonObject { ["quitting"] = true });
    }

    // --- helpers ---------------------------------------------------------------------------

    private JsonNode Resolve(string source, string id) => source switch
    {
        "state" => Adapter.State(),
        "scene" => Adapter.SceneInfo(),
        "entity" => Adapter.Entity(id, false),
        _ => null,
    };

    private void RecordEvent(JsonObject entry) => Session?.Record("event", entry);

    private static List<string> FixtureIds() =>
        DirAccess.GetFilesAt(FixtureDir).Where(file => file.EndsWith(".json")).Select(file => file.GetBaseName())
            .OrderBy(id => id, StringComparer.Ordinal).ToList();

    /// <summary>Automation must never touch the player's real saves or preferences.</summary>
    private void IsolatePlayerFiles()
    {
        DirAccess.MakeDirRecursiveAbsolute(IsolatedSaveDir);
        _originalBackend = SaveService.Instance.Backend;
        _originalSettingsPath = SettingsService.Instance.SettingsPath;
        SaveService.Instance.Backend = new JsonFileSaveBackend(IsolatedSaveDir);
        SettingsService.Instance.SettingsPath = IsolatedSettings;
    }

    private void RestorePlayerFiles()
    {
        if (_originalBackend == null)
            return;
        foreach (string file in DirAccess.GetFilesAt(IsolatedSaveDir))
            DirAccess.RemoveAbsolute(IsolatedSaveDir.PathJoin(file));
        DirAccess.RemoveAbsolute(IsolatedSettings);
        SaveService.Instance.Backend = _originalBackend;
        SettingsService.Instance.SettingsPath = _originalSettingsPath;
        _originalBackend = null;
    }

    private void WriteSessionFile()
    {
        DirAccess.MakeDirRecursiveAbsolute(SessionFile.GetBaseDir());
        using (FileAccess file = FileAccess.Open(SessionFile, FileAccess.ModeFlags.Write))
        {
            if (file == null)
                return;
            file.StoreString(JsonNodes.Stringify(new JsonObject
            {
                ["http_port"] = Http.Port(), ["ws_port"] = WebSocket.Port(), ["token"] = Auth.Token,
                ["expires_at_unix"] = Auth.ExpiresAtUnix, ["profile"] = Profile,
            }));
        }
        FileAccess.SetUnixPermissions(SessionFile, FileAccess.UnixPermissionFlags.ReadOwner | FileAccess.UnixPermissionFlags.WriteOwner);
    }

    private void ShowBadge()
    {
        _badge = new CanvasLayer { Layer = 120, ProcessMode = ProcessModeEnum.Always };
        var label = new Label
        {
            Text = "AUTOMATION_BADGE",
            ThemeTypeVariation = "KeycapLabel",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.Minsize, 6);
        label.GrowHorizontal = Control.GrowDirection.Both;
        _badge.AddChild(label);
        AddChild(_badge);
    }

    private void Audit(string kind, JsonObject data)
    {
        string line = JsonNodes.Stringify(new JsonObject
        {
            ["t"] = Time.GetDatetimeStringFromSystem(true),
            ["kind"] = kind,
            ["session"] = Session != null ? Session.Id : "",
            ["data"] = data,
        });
        if (kind != "call")
            DevLog.Info("automation", line);
        DirAccess.MakeDirRecursiveAbsolute(AuditLog.GetBaseDir());
        if (FileAccess.FileExists(AuditLog) && FileAccess.GetSize(AuditLog) > 1048576)
            DirAccess.RenameAbsolute(AuditLog, AuditLog + ".1"); // simple rotation
        using FileAccess file = FileAccess.Open(AuditLog, FileAccess.FileExists(AuditLog) ? FileAccess.ModeFlags.ReadWrite : FileAccess.ModeFlags.Write);
        if (file == null)
            return;
        file.SeekEnd();
        file.StoreLine(line);
    }
}

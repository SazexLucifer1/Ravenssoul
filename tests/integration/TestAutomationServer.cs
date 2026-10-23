using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Utopia.Automation;

namespace Utopia.Tests.Integration;

/// <summary>Server behaviour through Dispatch() and through real HTTP/WebSocket sockets.</summary>
public class TestAutomationServer : AutomationTestCase
{
    private static SceneRouter Router => SceneRouter.Instance;

    private async Task GotoRoute(StringName route)
    {
        await Router.Goto(route);
        await WaitUntil(() => !Router.IsTransitioning);
    }

    private static JsonObject Obj(string json) => (JsonObject)JsonNode.Parse(json);

    private static List<string> Ids(JsonNode result) =>
        ((JsonArray)result["entities"]).Select(e => JsonNodes.Str(e["id"])).ToList();

    // --- protocol & validation ----------------------------------------------------------

    public void TestEveryMethodHasAHandler()
    {
        StartServer();
        foreach ((string method, _) in AutomationProtocol.Methods())
            AssertTrue(Server.HasHandler(method), method);
    }

    public async Task TestMalformedRequests()
    {
        StartServer();
        foreach (string bad in new[]
        {
            "[]", "\"x\"", "{\"method\": \"state.get\"}", "{\"jsonrpc\": \"2.0\"}", "{\"jsonrpc\": \"2.0\", \"method\": 5}",
            "{\"jsonrpc\": \"2.0\", \"method\": \"state.get\", \"params\": []}", "{\"jsonrpc\": \"2.0\", \"id\": {}, \"method\": \"state.get\"}",
        })
        {
            JsonObject response = await Server.Dispatch(JsonNode.Parse(bad), new DispatchContext("test"));
            AssertEq(ErrorKind(response), "invalid_request", bad);
        }
        AssertEq(ErrorKind(await Rpc("no.such_method")), "method_not_found");
        AssertEq(ErrorKind(await Rpc("entity.get", new JsonObject())), "invalid_params", "missing id");
        AssertEq(ErrorKind(await Rpc("entity.get", Obj("{\"id\": \"x\", \"evil\": 1}"))), "invalid_params", "unknown field");
        AssertEq(ErrorKind(await Rpc("wait.until", Obj("{\"condition\": {\"source\": \"state\", \"op\": \"eval\", \"path\": \"x\", \"value\": 1}}"))), "invalid_params", "bad predicate");
        AssertEq(ErrorKind(await Rpc("input.key", Obj("{\"key\": \"NotAKey\"}"))), "invalid_params");
    }

    public async Task TestIntegerIdsAreEchoed()
    {
        StartServer();
        JsonObject response = await Server.Dispatch(JsonNode.Parse("{\"jsonrpc\": \"2.0\", \"id\": 7.0, \"method\": \"scene.current\"}"), new DispatchContext("test"));
        AssertEq(response["id"].ToJsonString(), "7");
    }

    public async Task TestQaProfileRejectsDevCapabilities()
    {
        StartServer("qa");
        JsonObject parameters = Obj("{\"target\": \"unit.x\", \"cell\": [1, 1], \"hero_id\": \"a\", \"battalion_id\": \"b\", \"command\": \"heal_all\"}");
        foreach (string method in new[] { "dev.teleport", "dev.spawn", "dev.command" })
        {
            var allowed = new JsonObject();
            foreach ((string key, _) in (JsonObject)AutomationProtocol.Methods()[method]["params"]["properties"])
                if (parameters.ContainsKey(key))
                    allowed[key] = parameters[key].DeepClone();
            AssertEq(ErrorKind(await Rpc(method, allowed)), "forbidden", method);
        }
        AssertEq(ErrorKind(await Rpc("entity.get", Obj("{\"id\": \"scene.main_menu\", \"deep\": true}"))), "forbidden", "deep inspection is dev-only");
    }

    public async Task TestRateLimiting()
    {
        StartServer();
        Server.RateLimiter = new AutomationRateLimiter(2, 0);
        AssertTrue((await Rpc("scene.current")).ContainsKey("result"));
        AssertTrue((await Rpc("scene.current")).ContainsKey("result"));
        AssertEq(ErrorKind(await Rpc("scene.current")), "rate_limited");
    }

    public async Task TestCapabilitiesAdaptToBuild()
    {
        StartServer();
        JsonNode caps = ResultOf(await Rpc("protocol.capabilities"));
        AssertEq(caps["protocol"], AutomationProtocol.Name);
        AssertFalse(((JsonArray)caps["capabilities"]).Any(c => JsonNodes.Str(c) == "observe.screenshot"), "headless runs cannot take screenshots");
        AssertFalse(((JsonArray)caps["methods"]).Any(c => JsonNodes.Str(c) == "dev.teleport"), "qa does not list dev methods");
        AssertEq(caps["engine"]["semantic_actions"]["equip"], false, "unsupported actions are declared");
        AssertEq(ErrorKind(await Rpc("observe.screenshot")), "unsupported");
        AssertEq(ErrorKind(await Rpc("action.perform", Obj("{\"action\": \"equip\"}"))), "unsupported");
    }

    // --- discovery & stable ids -------------------------------------------------------------

    public async Task TestStableIdsOnTheTitleScreen()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        List<string> first = Ids(ResultOf(await Rpc("tree.query")));
        await GotoRoute(Routes.MainMenu);
        List<string> second = Ids(ResultOf(await Rpc("tree.query")));
        AssertEq(first, second, "ids are identical across scene reloads");
        foreach (string id in new[] { "scene.main_menu", "screen.main_menu", "main_menu.new_game", "main_menu.settings", "main_menu.quit", "resource.gold" })
            AssertContains(first, id);
        JsonNode button = ResultOf(await Rpc("entity.get", Obj("{\"id\": \"main_menu.new_game\"}")));
        AssertEq(button["role"], "button");
        AssertEq(button["text_key"], "MAIN_MENU_NEW_GAME", "language-independent key");
        AssertEq(button["text"], "New Campaign");
        AssertContains(button["tags"], "primary");
        AssertEq(button["actions"], new[] { "activate" });
        AssertEq(ErrorKind(await Rpc("entity.get", Obj("{\"id\": \"no.such_entity\"}"))), "not_found");
    }

    public void TestEveryButtonInShippedScreensHasAnAutomationId()
    {
        foreach (string path in TestUiLayout.Screens.Append("res://scenes/bootstrap/bootstrap.tscn"))
        {
            Node root = GD.Load<PackedScene>(path).Instantiate();
            var ids = new HashSet<string>();
            foreach (Node node in GodotAutomationAdapter.FindNodes(root, n => n is BaseButton or UiScreen))
            {
                AssertTrue(node.HasMeta(GodotAutomationAdapter.MetaId), $"{path.GetFile()}: {node.Name} has no automation_id");
                string id = node.GetMeta(GodotAutomationAdapter.MetaId, "").AsString();
                AssertTrue(ids.Add(id), $"{path.GetFile()}: duplicate id {id}");
            }
            root.Free();
        }
    }

    public async Task TestGameplayEntitiesAndState()
    {
        StartServer();
        GameSession.Instance.StartNewCampaign();
        await GotoRoute(Routes.Gameplay);
        List<string> ids = Ids(ResultOf(await Rpc("tree.query")));
        foreach (string id in new[] { "unit.hero_deserter", "unit.hero_deserter/health", "grid.map_training_ground", "mission.mission_first_spark", "hud.objective" })
            AssertContains(ids, id);
        JsonNode unit = ResultOf(await Rpc("entity.get", Obj("{\"id\": \"unit.hero_deserter\"}")));
        AssertEq(unit["cell"], new[] { 1, 10 });
        AssertEq(unit["role"], "unit");
        JsonNode state = ResultOf(await Rpc("state.get"));
        AssertEq(state["route"], "gameplay");
        AssertEq(state["unit"]["cell"], new[] { 1, 10 });
        AssertEq(state["mission"]["finished"], false);
        var actions = (JsonArray)ResultOf(await Rpc("actions.available"))["actions"];
        AssertTrue(actions.Any(a => JsonNodes.Str(a["action"]) == "move" && JsonNodes.Str(a["args"]?["direction"]) == "left"));
        AssertTrue(actions.Any(a => JsonNodes.Str(a["action"]) == "pause"));
    }

    // --- semantic actions -------------------------------------------------------------------

    public async Task TestSemanticActionsRespectModals()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        AssertEq(ResultOf(await Rpc("action.perform", Obj("{\"action\": \"activate\", \"target\": \"main_menu.settings\"}")))["performed"], true);
        JsonObject blocked = await Rpc("action.perform", Obj("{\"action\": \"activate\", \"target\": \"main_menu.new_game\"}"));
        AssertEq(ErrorKind(blocked), "not_interactable", "cannot click through the settings modal");
        JsonNode select = ResultOf(await Rpc("action.perform", Obj("{\"action\": \"select\", \"target\": \"settings.language\", \"args\": {\"value\": \"de\"}}")));
        AssertEq(select["selected"], 1);
        AssertEq(TranslationServer.GetLocale(), "de");
        JsonNode entity = ResultOf(await Rpc("entity.get", Obj("{\"id\": \"settings.back\"}")));
        AssertEq(entity["text_key"], "UI_BACK", "ids and keys stay stable when the language changes");
        AssertEq(entity["text"], "Zurück");
        ResultOf(await Rpc("action.perform", Obj("{\"action\": \"cancel\"}")));
        await WaitFrames(2);
        AssertEq(ResultOf(await Rpc("scene.current"))["screens"], new string[0]);
        SettingsService.Instance.SetLocale("en");
    }

    public async Task TestMoveFollowsGameRules()
    {
        StartServer();
        GameSession.Instance.StartNewCampaign();
        await GotoRoute(Routes.Gameplay);
        JsonNode moved = ResultOf(await Rpc("action.perform", Obj("{\"action\": \"move\", \"args\": {\"direction\": \"left\"}}")));
        AssertEq(moved["cell"], new[] { 0, 10 });
        JsonNode blocked = ResultOf(await Rpc("action.perform", Obj("{\"action\": \"move\", \"args\": {\"direction\": \"left\"}}")));
        AssertEq(blocked["performed"], false);
        AssertEq(blocked["stopped"], "blocked");
        AssertEq(ErrorKind(await Rpc("action.perform", Obj("{\"action\": \"move\", \"args\": {\"direction\": \"sideways\"}}"))), "invalid_params");
        AssertEq(ErrorKind(await Rpc("action.perform", Obj("{\"action\": \"move\", \"target\": \"unit.enemy\", \"args\": {\"direction\": \"up\"}}"))), "not_found");
    }

    public async Task TestActionsDuringTransitionAreBusy()
    {
        StartServer();
        _ = Router.Goto(Routes.MainMenu);
        AssertEq(ErrorKind(await Rpc("action.perform", Obj("{\"action\": \"confirm\"}"))), "busy");
        await WaitUntil(() => !Router.IsTransitioning);
    }

    // --- waits, events, assertions -----------------------------------------------------------

    public async Task TestWaitFramesIsExact()
    {
        StartServer();
        ulong before = Engine.GetProcessFrames();
        ResultOf(await Rpc("wait.frames", Obj("{\"count\": 5}")));
        AssertEq(Engine.GetProcessFrames() - before, 5);
    }

    public async Task TestWaitUntilAndTimeout()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        if (!GameSession.Instance.HasCampaign)
            GameSession.Instance.StartNewCampaign();
        _ = Router.Goto(Routes.Gameplay);
        JsonNode met = ResultOf(await Rpc("wait.until", Obj("{\"condition\": {\"source\": \"scene\", \"path\": \"route\", \"op\": \"eq\", \"value\": \"gameplay\"}, \"timeout_ms\": 5000}")));
        AssertEq(met["met"], true);
        JsonObject timeout = await Rpc("wait.until", Obj("{\"condition\": {\"source\": \"state\", \"path\": \"unit.cell\", \"op\": \"eq\", \"value\": [5, 5]}, \"timeout_ms\": 50}"));
        AssertEq(ErrorKind(timeout), "timeout");
        AssertEq(timeout["error"]["data"]["actual"], new[] { 1, 10 }, "timeouts report the actual value");
    }

    public async Task TestWaitEventAndSceneTransitionEvents()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        long seq = Server.Events.LastSeq;
        _ = Router.Goto(Routes.MainMenu);
        JsonNode waited = ResultOf(await Rpc("wait.event", Obj($"{{\"type\": \"route.changed\", \"match\": {{\"route\": \"main_menu\"}}, \"after_seq\": {seq}, \"timeout_ms\": 5000}}")));
        AssertEq(waited["event"]["data"]["route"], "main_menu");
        List<string> types = ((JsonArray)ResultOf(await Rpc("observe.events", Obj($"{{\"since_seq\": {seq}}}")))["events"])
            .Select(e => JsonNodes.Str(e["type"])).ToList();
        AssertTrue(types.IndexOf("route.changing") < types.IndexOf("route.changed"));
    }

    public async Task TestAssertReportsWithoutRaising()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        JsonNode passed = ResultOf(await Rpc("assert.check", Obj("{\"condition\": {\"source\": \"scene\", \"path\": \"route\", \"op\": \"eq\", \"value\": \"main_menu\"}}")));
        AssertEq(passed["passed"], true);
        JsonNode failed = ResultOf(await Rpc("assert.check", Obj("{\"condition\": {\"source\": \"entity\", \"id\": \"main_menu.new_game\", \"path\": \"enabled\", \"op\": \"eq\", \"value\": false}, \"message\": \"m\"}")));
        AssertEq(failed["passed"], false);
        AssertEq(failed["actual"], true);
        AssertEq(Server.Session.AssertionFailures.Count, 1);
    }

    // --- lifecycle --------------------------------------------------------------------------

    public async Task TestHeldInputReleasedOnSessionClose()
    {
        StartServer();
        ResultOf(await Rpc("input.key", Obj("{\"key\": \"Right\", \"mode\": \"down\"}")));
        AssertTrue(Input.IsKeyPressed(Key.Right));
        ResultOf(await Rpc("session.close"));
        AssertFalse(Input.IsKeyPressed(Key.Right));
        AssertNull(Server.Session);
    }

    public async Task TestHeldInputReleasedOnSceneChange()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        ResultOf(await Rpc("input.gamepad", Obj("{\"button\": \"a\", \"mode\": \"down\"}")));
        AssertTrue(Input.IsJoyButtonPressed(0, JoyButton.A));
        await GotoRoute(Routes.MainMenu);
        AssertFalse(Input.IsJoyButtonPressed(0, JoyButton.A));
    }

    public async Task TestIdleTimeoutCleansUp()
    {
        StartServer("qa", Obj("{\"idle_timeout_ms\": 100}"));
        ResultOf(await Rpc("input.key", Obj("{\"key\": \"Up\", \"mode\": \"down\"}")));
        string firstSession = Server.Session.Id;
        await WaitSeconds(0.2);
        AssertNull(Server.Session, "idle session closed");
        AssertFalse(Input.IsKeyPressed(Key.Up));
        ResultOf(await Rpc("session.hello"));
        AssertNe(Server.Session.Id, firstSession, "a reconnecting client gets a fresh session");
    }

    public async Task TestShutdownReleasesEverythingAndRestoresPlayerFiles()
    {
        SaveBackend original = SaveService.Instance.Backend;
        StartServer();
        AssertTrue(SaveService.Instance.Backend is JsonFileSaveBackend, "automation uses an isolated save folder");
        AssertNe(SettingsService.Instance.SettingsPath, "user://test_settings.cfg");
        ResultOf(await Rpc("input.key", Obj("{\"key\": \"Down\", \"mode\": \"down\"}")));
        int port = Server.Http.Port();
        Server.Stop();
        AssertFalse(Input.IsKeyPressed(Key.Down));
        AssertTrue(ReferenceEquals(SaveService.Instance.Backend, original), "original save backend restored");
        AssertEq(SettingsService.Instance.SettingsPath, "user://test_settings.cfg");
        AssertEq(ErrorKind(await Rpc("scene.current")), "busy", "no requests after stop");
        var probe = new TcpServer();
        AssertEq(probe.Listen((ushort)port, "127.0.0.1"), Error.Ok, "socket released");
        probe.Stop();
    }

    public async Task TestResetCheckpointFixtureAndRecording()
    {
        StartServer();
        ResultOf(await Rpc("recording.start"));
        ResultOf(await Rpc("fixture.load", Obj("{\"fixture\": \"campaign_after_first_mission\", \"write_save\": true}")));
        AssertEq(GameSession.Instance.Wallet.Amount(ResourceWallet.Gold), 50);
        AssertTrue(SaveService.Instance.HasSave());
        ResultOf(await Rpc("checkpoint.create", Obj("{\"name\": \"rich\"}")));
        ResultOf(await Rpc("session.reset"));
        AssertFalse(GameSession.Instance.HasCampaign);
        AssertFalse(SaveService.Instance.HasSave(), "reset removes automation saves");
        ResultOf(await Rpc("checkpoint.create", Obj("{\"name\": \"empty\"}")));
        ResultOf(await Rpc("checkpoint.restore", Obj("{\"name\": \"empty\"}")));
        AssertEq(ErrorKind(await Rpc("checkpoint.restore", Obj("{\"name\": \"rich\"}"))), "not_found", "reset clears checkpoints");
        AssertEq(ErrorKind(await Rpc("fixture.load", Obj("{\"fixture\": \"../../etc/passwd\"}"))), "invalid_params", "fixture ids only");
        AssertEq(ErrorKind(await Rpc("fixture.load", Obj("{\"fixture\": \"not_approved\"}"))), "not_found");
        var entries = (JsonArray)ResultOf(await Rpc("recording.stop"))["entries"];
        AssertTrue(entries.Any(e => JsonNodes.Str(e["kind"]) == "request" && JsonNodes.Str(e["data"]?["method"]) == "fixture.load"));
        AssertTrue(entries.Any(e => JsonNodes.Str(e["kind"]) == "event"));
    }

    public async Task TestDevProfileToolsAndCleanup()
    {
        StartServer("dev");
        GameSession.Instance.StartNewCampaign();
        await GotoRoute(Routes.Gameplay);
        AssertEq(ResultOf(await Rpc("dev.teleport", Obj("{\"target\": \"unit.hero_deserter\", \"cell\": [9, 1]}")))["cell"], new[] { 9, 1 });
        AssertEq(ErrorKind(await Rpc("dev.teleport", Obj("{\"target\": \"unit.hero_deserter\", \"cell\": [4, 3]}"))), "invalid_params", "walls stay walls");
        JsonNode spawned = ResultOf(await Rpc("dev.spawn", Obj("{\"hero_id\": \"hero_deserter\", \"battalion_id\": \"battalion_militia\", \"cell\": [2, 2]}")));
        AssertEq(spawned["id"], "unit.spawned.1");
        AssertNotNull(ResultOf(await Rpc("entity.get", Obj("{\"id\": \"unit.spawned.1\", \"deep\": true}")))["properties"]["stats"]);
        ResultOf(await Rpc("dev.command", Obj("{\"command\": \"grant_resources\", \"args\": {\"resource\": \"gold\", \"amount\": 5}}")));
        AssertEq(GameSession.Instance.Wallet.Amount(ResourceWallet.Gold), 5);
        AssertEq(ErrorKind(await Rpc("dev.command", Obj("{\"command\": \"grant_resources\", \"args\": {\"resource\": \"gold\", \"amount\": 999999}}"))), "invalid_params");
        AssertEq(ErrorKind(await Rpc("dev.command", Obj("{\"command\": \"rm -rf\"}"))), "invalid_params", "allow-listed commands only");
        ResultOf(await Rpc("session.close"));
        AssertNull(((Gameplay)Tree.CurrentScene).Grid.OccupantAt(new Vector2I(2, 2)), "spawned units removed with the session");
        ResultOf(await Rpc("dev.command", Obj("{\"command\": \"win_mission\"}")));
        AssertEq(ResultOf(await Rpc("state.get"))["mission"]["finished"], true);
    }

    // --- transports -------------------------------------------------------------------------

    public async Task TestHttpTransportSecurity()
    {
        StartServer();
        int port = Server.Http.Port();
        HttpReply health = await Http($"GET /v1/health HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");
        AssertEq(health.Status, 200);
        AssertEq(health.Body?["ok"], true);
        string body = "{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"scene.current\"}";
        AssertEq((await HttpPost(body)).Status, 200);
        AssertEq((await HttpPost(body, new() { ["Authorization"] = null })).Status, 401, "missing token");
        AssertEq((await HttpPost(body, new() { ["Authorization"] = "Bearer wrong" })).Status, 401, "wrong token");
        AssertEq((await HttpPost(body, new() { ["Host"] = "attacker.example" })).Status, 403, "DNS rebinding");
        AssertEq((await HttpPost(body, new() { ["Origin"] = "https://attacker.example" })).Status, 403, "browser origin");
        AssertEq((await HttpPost(body, new() { ["Content-Type"] = "text/plain" })).Status, 415);
        AssertEq((await HttpPost("{nope")).Status, 400);
        AssertEq((await HttpPost(new string('x', 70000))).Status, 413);
        AssertEq((await Http($"GET /v1/rpc HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n")).Status, 405);
        AssertEq((await Http($"GET /etc/passwd HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n")).Status, 404);
        AssertEq(Server.Http.ConnectionCount(), 0, "connections are closed after each response");
    }

    public async Task TestHttpReadTimeoutDropsSlowClients()
    {
        StartServer();
        var peer = new StreamPeerTcp();
        peer.ConnectToHost("127.0.0.1", Server.Http.Port());
        await WaitFrames(5);
        peer.Poll();
        peer.PutData(System.Text.Encoding.UTF8.GetBytes("POST /v1/rpc HTTP/1.1\r\n"));
        await WaitFrames(5);
        AssertEq(Server.Http.ConnectionCount(), 1);
        await WaitSeconds(AutomationProtocol.ReadTimeoutMs / 1000.0 + 0.3);
        AssertEq(Server.Http.ConnectionCount(), 0);
    }

    public async Task TestWebsocketAuthEventsAndReconnect()
    {
        StartServer();
        await GotoRoute(Routes.MainMenu);
        WebSocketPeer intruder = await WsConnect();
        WsSend(intruder, Obj("{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"scene.current\"}"));
        JsonObject rejected = await WsReceive(intruder);
        AssertEq(JsonNodes.Int(rejected?["error"]?["code"]), AutomationProtocol.Unauthorized, "first message must authenticate");
        await WsReceive(intruder, 500);
        AssertEq(intruder.GetReadyState(), WebSocketPeer.State.Closed);

        WebSocketPeer ws = await WsConnect();
        WsSend(ws, Obj($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"session.authenticate\", \"params\": {{\"token\": \"{Token}\"}}}}"));
        AssertEq((await WsReceive(ws))?["result"]?["authenticated"], true);
        WsSend(ws, Obj("{\"jsonrpc\": \"2.0\", \"id\": 2, \"method\": \"events.subscribe\", \"params\": {\"types\": [\"route.changed\"]}}"));
        AssertEq((await WsReceive(ws))?["result"]?["subscribed"], new[] { "route.changed" });
        _ = Router.Goto(Routes.MainMenu);
        JsonObject pushed = await WsReceive(ws, 5000);
        AssertEq(pushed?["method"], "event");
        AssertEq(pushed?["params"]?["type"], "route.changed");
        long seq = JsonNodes.Long(pushed?["params"]?["seq"]);
        ws.Close();
        await WaitFrames(5);

        // Reconnect: a new socket authenticates again and catches up via observe.events.
        _ = Router.Goto(Routes.MainMenu);
        await WaitUntil(() => !Router.IsTransitioning);
        WebSocketPeer again = await WsConnect();
        WsSend(again, Obj($"{{\"jsonrpc\": \"2.0\", \"id\": 1, \"method\": \"session.authenticate\", \"params\": {{\"token\": \"{Token}\"}}}}"));
        await WsReceive(again);
        WsSend(again, Obj($"{{\"jsonrpc\": \"2.0\", \"id\": 2, \"method\": \"observe.events\", \"params\": {{\"since_seq\": {seq}, \"types\": [\"route.changed\"]}}}}"));
        var missed = (await WsReceive(again))?["result"]?["events"] as JsonArray;
        AssertEq(missed?.Count ?? -1, 1, "missed events are recoverable after reconnect");
        again.Close();
        await WaitFrames(5);
    }

    public async Task TestEventsSubscribeNeedsWebsocket()
    {
        StartServer();
        AssertEq(ErrorKind(await Rpc("events.subscribe", Obj("{\"types\": [\"route.changed\"]}"))), "unsupported");
    }

    public void TestRemoteBindRequiresExplicitOptIn()
    {
        AutomationServer refused = AddNode(new AutomationServer());
        refused.Configure(Obj($"{{\"profile\": \"qa\", \"port\": 0, \"token\": \"{Token}\", \"bind\": \"0.0.0.0\", \"badge\": false, \"write_session_file\": false}}"));
        AssertTrue(refused.Start().Contains("allow-remote"));
        AutomationServer shortToken = AddNode(new AutomationServer());
        shortToken.Configure(Obj("{\"profile\": \"qa\", \"port\": 0, \"token\": \"short\", \"badge\": false}"));
        AssertTrue(shortToken.Start().Contains("at least"));
        AutomationServer production = AddNode(new AutomationServer());
        production.Configure(Obj($"{{\"profile\": \"production\", \"port\": 0, \"token\": \"{Token}\"}}"));
        AssertNe(production.Start(), "");
    }
}

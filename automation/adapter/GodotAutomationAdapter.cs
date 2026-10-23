using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Godot implementation of the automation contract.
///
/// Generic layer: scenes (route ids), Controls tagged with the
/// <c>automation_id</c> metadata, focus, modality, screenshots, performance, and
/// input. Game semantics come from AutomationProvider instances.
/// Reads existing game services (SceneRouter, GameSession, SaveService,
/// Settings, InputMethod); it never keeps a parallel copy of game state.
/// </summary>
public class GodotAutomationAdapter : AutomationAdapter
{
    public static readonly StringName MetaId = "automation_id";
    public static readonly StringName MetaTags = "automation_tags";

    public static readonly IReadOnlyDictionary<string, bool> SemanticActions = new Dictionary<string, bool>
    {
        ["activate"] = true, ["select"] = true, ["toggle"] = true, ["confirm"] = true, ["cancel"] = true,
        ["move"] = true, ["pause"] = true, ["resume"] = true,
        // Defined by the protocol; this game has no such systems yet.
        ["interact"] = false, ["equip"] = false, ["use"] = false, ["choose_dialogue"] = false,
    };

    public List<AutomationProvider> Providers { get; } = new();
    public AutomationInputDriver InputDriver { get; }

    private readonly SceneTree _tree;
    private AutomationEventLog _events;
    private readonly List<Action> _disconnects = new();

    public GodotAutomationAdapter(SceneTree tree, AutomationInputDriver inputDriver)
    {
        _tree = tree;
        InputDriver = inputDriver;
        Providers.Add(new UtopiaAutomationProvider());
        foreach (AutomationProvider provider in Providers)
            provider.Input = inputDriver;
    }

    public override JsonObject Describe()
    {
        Godot.Collections.Dictionary info = Engine.GetVersionInfo();
        var actions = new JsonObject();
        foreach ((string name, bool supported) in SemanticActions)
            actions[name] = supported;
        return new JsonObject
        {
            ["engine"] = "godot",
            ["engine_version"] = $"{info["major"]}.{info["minor"]}.{info["patch"]}",
            ["adapter"] = "godot-utopia",
            ["game"] = ProjectSettings.GetSetting("application/config/name").AsString(),
            ["game_version"] = ProjectSettings.GetSetting("application/config/version").AsString(),
            ["build"] = OS.IsDebugBuild() ? "debug" : "release",
            ["semantic_actions"] = actions,
            ["input_devices"] = new JsonArray("keyboard", "mouse", "gamepad", "touch", "action"),
            ["screenshots"] = ScreenshotsAvailable(),
            ["logical_resolution"] = new JsonArray(1280, 720),
        };
    }

    public static bool ScreenshotsAvailable() => DisplayServer.GetName() != "headless";

    public Node Scene() => _tree.CurrentScene;

    private static SceneRouter Router => SceneRouter.Instance;
    private static GameSession Session => GameSession.Instance;

    public override JsonObject SceneInfo()
    {
        string route = Router.CurrentRoute;
        return new JsonObject
        {
            ["route"] = route,
            ["scene_id"] = route.Length > 0 ? $"scene.{route}" : "scene.unknown",
            ["transitioning"] = Router.IsTransitioning,
            ["paused"] = _tree.Paused,
            ["screens"] = ToArray(ScreenIds()),
            ["focused"] = FocusedId(),
        };
    }

    // --- index ------------------------------------------------------------------

    public Dictionary<string, IndexEntry> BuildIndex()
    {
        var index = new Dictionary<string, IndexEntry>();
        Node current = Scene();
        if (current == null)
            return index;
        string route = Router.CurrentRoute;
        index[$"scene.{(route.Length > 0 ? route : "unknown")}"] = new IndexEntry("scene", current);
        foreach (Node node in FindNodes(current, n => n is Control && n.HasMeta(MetaId)))
        {
            if (!node.IsInsideTree() || node.IsQueuedForDeletion())
                continue;
            string id = node.GetMeta(MetaId).AsString();
            string unique = id;
            int n = 2;
            while (index.ContainsKey(unique))
                unique = $"{id}#{n++}";
            index[unique] = new IndexEntry(node is UiScreen ? "screen" : "control", node);
        }
        foreach (AutomationProvider provider in Providers)
            provider.Collect(index, current);
        return index;
    }

    public override List<JsonObject> Query(JsonObject filters)
    {
        var output = new List<JsonObject>();
        Dictionary<string, IndexEntry> index = BuildIndex();
        int limit = JsonNodes.Int(filters["limit"], 200);
        foreach (string id in index.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            JsonObject desc = DescribeEntry(id, index[id], false);
            if (filters.ContainsKey("role") && JsonNodes.Str(desc["role"]) != JsonNodes.Str(filters["role"]))
                continue;
            if (filters.ContainsKey("type") && JsonNodes.Str(desc["type"]) != JsonNodes.Str(filters["type"]))
                continue;
            if (filters.ContainsKey("tag") && !((desc["tags"] as JsonArray) ?? new JsonArray()).Any(t => JsonNodes.Str(t) == JsonNodes.Str(filters["tag"])))
                continue;
            if (filters.ContainsKey("id_prefix") && !id.StartsWith(JsonNodes.Str(filters["id_prefix"])))
                continue;
            if (JsonNodes.Bool(filters["visible_only"]) && !JsonNodes.Bool(desc["visible"]))
                continue;
            if (JsonNodes.Bool(filters["interactable_only"]) && !JsonNodes.Bool(desc["interactable"]))
                continue;
            output.Add(desc);
            if (output.Count >= limit)
                break;
        }
        return output;
    }

    public override JsonObject Entity(string id, bool deep)
    {
        Dictionary<string, IndexEntry> index = BuildIndex();
        return index.TryGetValue(id, out IndexEntry entry) ? DescribeEntry(id, entry, deep) : null;
    }

    private JsonObject DescribeEntry(string id, IndexEntry entry, bool deep)
    {
        if (entry.Provider != null)
        {
            JsonObject desc = entry.Provider.Describe(id, entry, deep);
            desc["id"] = id;
            return desc;
        }
        if (entry.Kind == "scene")
        {
            return new JsonObject
            {
                ["id"] = id, ["type"] = "Scene", ["name"] = (string)Router.CurrentRoute, ["role"] = "scene",
                ["tags"] = new JsonArray(), ["visible"] = true, ["enabled"] = true, ["interactable"] = false,
                ["components"] = new JsonArray(), ["actions"] = new JsonArray(),
                ["properties"] = new JsonObject { ["screens"] = ToArray(ScreenIds()) },
            };
        }
        return DescribeControl(id, (Control)entry.Node, deep);
    }

    private JsonObject DescribeControl(string id, Control control, bool deep)
    {
        string role = "group";
        var actions = new List<string>();
        var props = new JsonObject();
        switch (control)
        {
            case UiScreen screen:
                role = "screen";
                props["cancel_closes"] = screen.CancelCloses;
                List<string> screens = ScreenIds();
                props["top"] = screens.Count > 0 && screens[^1] == id;
                break;
            case OptionButton option:
                role = "combobox";
                actions.Add("select");
                props["selected"] = option.Selected;
                props["item_count"] = option.ItemCount;
                var items = new JsonArray();
                for (int i = 0; i < option.ItemCount; i++)
                {
                    Variant meta = option.GetItemMetadata(i);
                    string itemText = option.GetItemText(i);
                    items.Add(new JsonObject
                    {
                        ["index"] = i,
                        ["value"] = meta.VariantType != Variant.Type.Nil ? meta.AsString() : i.ToString(),
                        ["text_key"] = itemText,
                        ["text"] = control.CanAutoTranslate() ? control.Tr(itemText) : itemText,
                    });
                }
                props["items"] = items;
                break;
            case CheckButton or CheckBox:
                role = "switch";
                actions.AddRange(new[] { "toggle", "activate" });
                props["checked"] = ((BaseButton)control).ButtonPressed;
                break;
            case BaseButton:
                role = "button";
                actions.Add("activate");
                if (control is GameButton gameButton)
                    props["state"] = Snake(gameButton.GetState().ToString());
                break;
            case ProgressBar bar:
                role = "progressbar";
                props["value"] = bar.Value;
                props["max"] = bar.MaxValue;
                break;
            case Label:
                role = "label";
                break;
        }
        string textKey = "";
        string shown = "";
        Variant text = control.Get("text");
        if (text.VariantType == Variant.Type.String)
        {
            string raw = text.AsString();
            shown = control.CanAutoTranslate() ? control.Tr(raw) : raw;
            textKey = control.CanAutoTranslate() ? raw : "";
        }
        var tags = new JsonArray();
        if (control.HasMeta(MetaTags))
            foreach (string tag in control.GetMeta(MetaTags).AsStringArray())
                tags.Add(tag);
        if (control.ThemeTypeVariation == "PrimaryButton")
            tags.Add("primary");
        else if (control.ThemeTypeVariation == "DangerButton")
            tags.Add("danger");
        Rect2 rect = control.GetGlobalRect();
        bool interactable = IsInteractable(control);
        var desc = new JsonObject
        {
            ["id"] = id,
            ["type"] = control.GetClass(),
            ["name"] = (string)control.Name,
            ["role"] = role,
            ["tags"] = tags,
            ["visible"] = control.IsVisibleInTree(),
            ["enabled"] = !(control is BaseButton button && button.Disabled),
            ["interactable"] = interactable,
            ["focused"] = control.HasFocus(),
            ["position"] = new JsonObject { ["x"] = Round(rect.GetCenter().X), ["y"] = Round(rect.GetCenter().Y) },
            ["rect"] = new JsonObject { ["x"] = Round(rect.Position.X), ["y"] = Round(rect.Position.Y), ["w"] = Round(rect.Size.X), ["h"] = Round(rect.Size.Y) },
            ["components"] = new JsonArray(),
            ["actions"] = interactable ? ToArray(actions) : new JsonArray(),
            ["text_key"] = textKey,
            ["text"] = shown,
            ["properties"] = props,
        };
        if (deep)
        {
            props["theme_type_variation"] = (string)control.ThemeTypeVariation;
            props["focus_mode"] = (int)control.GetFocusModeWithOverride();
            props["layout_rtl"] = control.IsLayoutRtl();
        }
        return desc;
    }

    /// <summary>
    /// A control a player could use right now: visible, enabled, and not blocked
    /// by a modal screen (ScreenStack disables focus beneath the top screen).
    /// </summary>
    public static bool IsInteractable(Control control)
    {
        if (!control.IsVisibleInTree())
            return false;
        if (control is BaseButton button && button.Disabled)
            return false;
        if (control is BaseButton or Godot.Range)
            return control.GetFocusModeWithOverride() != Control.FocusModeEnum.None;
        return false;
    }

    // --- state & actions -----------------------------------------------------------

    public override JsonObject State()
    {
        JsonObject output = SceneInfo();
        output["locale"] = TranslationServer.GetLocale();
        output["input_method"] = InputMethodService.Instance.Current == InputMethodService.Method.Gamepad ? "gamepad" : "keyboard_mouse";
        output["held_inputs"] = ToArray(InputDriver.Held());
        output["save"] = new JsonObject { ["has_save"] = SaveService.Instance.HasSave() };
        foreach (AutomationProvider provider in Providers)
            provider.AddState(output, Scene());
        return output;
    }

    public override List<JsonObject> AvailableActions()
    {
        var output = new List<JsonObject>();
        Dictionary<string, IndexEntry> index = BuildIndex();
        foreach (string id in index.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            IndexEntry entry = index[id];
            if (entry.Kind != "control")
                continue;
            var control = (Control)entry.Node;
            if (!IsInteractable(control))
                continue;
            if (control is OptionButton)
                output.Add(new JsonObject { ["action"] = "select", ["target"] = id });
            else if (control is CheckButton or CheckBox)
                output.Add(new JsonObject { ["action"] = "toggle", ["target"] = id });
            else if (control is BaseButton)
                output.Add(new JsonObject { ["action"] = "activate", ["target"] = id });
        }
        UiScreen top = TopScreen();
        if (top != null && top.CancelCloses)
            output.Add(new JsonObject { ["action"] = "cancel", ["target"] = IdOf(top) });
        string focused = FocusedId();
        if (focused.Length > 0)
            output.Add(new JsonObject { ["action"] = "confirm", ["target"] = focused });
        foreach (AutomationProvider provider in Providers)
            provider.AddActions(output, Scene());
        return output;
    }

    public override JsonObject Perform(string action, string target, JsonObject args)
    {
        if (!SemanticActions.TryGetValue(action, out bool supported) || !supported)
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, $"action '{action}' is not supported by this game");
        if (Router.IsTransitioning)
            return AutomationProtocol.Fail(AutomationProtocol.Busy, "scene transition in progress");
        foreach (AutomationProvider provider in Providers)
        {
            JsonObject handled = provider.Perform(action, target, args, Scene());
            if (handled != null)
                return handled;
        }
        switch (action)
        {
            case "confirm":
                InputDriver.Action("ui_accept", "tap", 0);
                return AutomationProtocol.Ok(new JsonObject { ["performed"] = true, ["via"] = "ui_accept" });
            case "cancel":
                InputDriver.Action("ui_cancel", "tap", 0);
                return AutomationProtocol.Ok(new JsonObject { ["performed"] = true, ["via"] = "ui_cancel" });
            case "activate" or "select" or "toggle":
                return PerformControl(action, target, args);
        }
        return AutomationProtocol.Fail(AutomationProtocol.Unsupported, $"action '{action}' is not available here");
    }

    private JsonObject PerformControl(string action, string target, JsonObject args)
    {
        if (target.Length == 0)
            return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, "target required");
        Dictionary<string, IndexEntry> index = BuildIndex();
        if (!index.TryGetValue(target, out IndexEntry entry) || entry.Kind != "control")
            return AutomationProtocol.Fail(AutomationProtocol.NotFound, $"no control '{target}'");
        var control = (Control)entry.Node;
        if (!IsInteractable(control))
            return AutomationProtocol.Fail(AutomationProtocol.NotInteractable, $"'{target}' is hidden, disabled, or behind a modal screen");
        switch (action)
        {
            case "activate":
                if (control is OptionButton)
                    return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"use select for '{target}'");
                if (control is CheckButton or CheckBox)
                {
                    var toggle = (BaseButton)control;
                    toggle.ButtonPressed = !toggle.ButtonPressed;
                }
                else
                {
                    control.GrabFocus();
                    control.EmitSignal(BaseButton.SignalName.Pressed);
                }
                return AutomationProtocol.Ok(new JsonObject { ["performed"] = true });
            case "toggle":
            {
                if (control is not (CheckButton or CheckBox))
                    return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"'{target}' is not a toggle");
                var button = (BaseButton)control;
                button.ButtonPressed = JsonNodes.Bool(args["on"], !button.ButtonPressed);
                return AutomationProtocol.Ok(new JsonObject { ["performed"] = true, ["checked"] = button.ButtonPressed });
            }
            case "select":
            {
                if (control is not OptionButton option)
                    return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, $"'{target}' is not a combobox");
                int indexToSelect = -1;
                if (args.ContainsKey("value"))
                {
                    string wanted = JsonNodes.Str(args["value"], JsonNodes.Stringify(args["value"]));
                    for (int i = 0; i < option.ItemCount; i++)
                        if (option.GetItemMetadata(i).AsString() == wanted)
                            indexToSelect = i;
                }
                else if (JsonNodes.IsNumber(args["index"]))
                {
                    indexToSelect = JsonNodes.Int(args["index"], -1);
                }
                if (indexToSelect < 0 || indexToSelect >= option.ItemCount)
                    return AutomationProtocol.Fail(AutomationProtocol.InvalidParams, "args.index or args.value must name an existing item");
                option.Select(indexToSelect);
                option.EmitSignal(OptionButton.SignalName.ItemSelected, indexToSelect);
                return AutomationProtocol.Ok(new JsonObject { ["performed"] = true, ["selected"] = indexToSelect });
            }
        }
        return AutomationProtocol.Fail(AutomationProtocol.Unsupported, action);
    }

    // --- observation ------------------------------------------------------------------

    public override async Task<JsonObject> Screenshot(int maxWidth)
    {
        if (!ScreenshotsAvailable())
            return AutomationProtocol.Fail(AutomationProtocol.Unsupported, "screenshots need a renderer (run without --headless, e.g. under Xvfb)");
        await _tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image image = _tree.Root.GetTexture().GetImage();
        if (image == null || image.IsEmpty())
            return AutomationProtocol.Fail(AutomationProtocol.InternalError, "frame capture failed");
        if (maxWidth > 0 && image.GetWidth() > maxWidth)
            image.Resize(maxWidth, Mathf.RoundToInt(image.GetHeight() * (float)maxWidth / image.GetWidth()), Image.Interpolation.Bilinear);
        byte[] png = image.SavePngToBuffer();
        if (png.Length > AutomationProtocol.MaxScreenshotBytes)
            return AutomationProtocol.Fail(AutomationProtocol.PayloadTooLarge, "screenshot too large; pass max_width");
        return AutomationProtocol.Ok(new JsonObject
        {
            ["width"] = image.GetWidth(),
            ["height"] = image.GetHeight(),
            ["png_base64"] = Convert.ToBase64String(png),
        });
    }

    public override JsonObject Perf() => new()
    {
        ["fps"] = Performance.GetMonitor(Performance.Monitor.TimeFps),
        ["process_ms"] = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0,
        ["physics_ms"] = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0,
        ["nodes"] = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
        ["orphan_nodes"] = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount),
        ["objects"] = Performance.GetMonitor(Performance.Monitor.ObjectCount),
        ["static_memory_mb"] = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0,
        ["frame"] = (long)Engine.GetProcessFrames(),
    };

    public override bool IsSettled()
    {
        if (Router.IsTransitioning || InputDriver.HasHolds())
            return false;
        return Providers.All(provider => provider.IsSettled(Scene()));
    }

    // --- lifecycle helpers ----------------------------------------------------------

    public override async Task<JsonObject> Reset(string route)
    {
        InputDriver.ReleaseAll();
        CleanupSession();
        _tree.Paused = false;
        Session.HasCampaign = false;
        Session.Wallet = new ResourceWallet();
        foreach (string slot in SaveService.Instance.ListSaves())
            SaveService.Instance.DeleteSave(slot);
        StringName target = route == "gameplay" ? Routes.Gameplay : Routes.MainMenu;
        if (target == Routes.Gameplay)
            Session.StartNewCampaign();
        if (!await Goto(target))
            return AutomationProtocol.Fail(AutomationProtocol.Busy, "could not change scene");
        return AutomationProtocol.Ok(new JsonObject { ["route"] = (string)target });
    }

    public override JsonObject CaptureCheckpoint() => new()
    {
        ["route"] = (string)Router.CurrentRoute,
        ["campaign"] = Session.HasCampaign ? Session.ToSaveData() : null,
    };

    public override async Task<JsonObject> RestoreCheckpoint(JsonObject snapshot)
    {
        InputDriver.ReleaseAll();
        _tree.Paused = false;
        if (snapshot["campaign"] is JsonObject campaign)
        {
            if (!Session.LoadSaveData((JsonObject)campaign.DeepClone()))
                return AutomationProtocol.Fail(AutomationProtocol.InternalError, "checkpoint data no longer matches game content");
        }
        else
        {
            Session.HasCampaign = false;
        }
        StringName route = JsonNodes.Str(snapshot["route"]);
        if (!Router.HasRoute(route) || route == Routes.Bootstrap)
            route = Routes.MainMenu;
        if (!await Goto(route))
            return AutomationProtocol.Fail(AutomationProtocol.Busy, "could not change scene");
        return AutomationProtocol.Ok(new JsonObject { ["route"] = (string)route });
    }

    public override async Task<JsonObject> ApplyFixture(JsonObject fixture, bool writeSave)
    {
        InputDriver.ReleaseAll();
        _tree.Paused = false;
        if (fixture["campaign"] is JsonObject campaign)
        {
            if (!Session.LoadSaveData((JsonObject)campaign.DeepClone()))
                return AutomationProtocol.Fail(AutomationProtocol.InternalError, "fixture references unknown content");
            if (writeSave && !SaveService.Instance.SaveGame((JsonObject)campaign.DeepClone()).IsOk())
                return AutomationProtocol.Fail(AutomationProtocol.InternalError, "fixture save failed");
        }
        else
        {
            Session.HasCampaign = false;
        }
        StringName route = JsonNodes.Str(fixture["route"], "main_menu");
        if (route == Routes.Gameplay && !Session.HasCampaign)
            Session.StartNewCampaign();
        if (!await Goto(Router.HasRoute(route) ? route : Routes.MainMenu))
            return AutomationProtocol.Fail(AutomationProtocol.Busy, "could not change scene");
        return AutomationProtocol.Ok(new JsonObject { ["fixture"] = JsonNodes.Str(fixture["id"]), ["route"] = (string)Router.CurrentRoute });
    }

    public override JsonObject Dev(string method, JsonObject parameters)
    {
        foreach (AutomationProvider provider in Providers)
        {
            JsonObject handled = provider.Dev(method, parameters, Scene());
            if (handled != null)
                return handled;
        }
        return AutomationProtocol.Fail(AutomationProtocol.Unsupported, $"{method} is not available in this scene");
    }

    public override void ConnectEvents(AutomationEventLog log)
    {
        _events = log;
        foreach (AutomationProvider provider in Providers)
            provider.Events = log;

        SceneRouter router = Router;
        SceneRouter.RouteChangingEventHandler onChanging = (from, to) =>
        {
            InputDriver.ReleaseAll(); // held input never leaks into the next scene
            log.Emit("route.changing", new JsonObject { ["from"] = (string)from, ["to"] = (string)to });
        };
        SceneRouter.RouteChangedEventHandler onChanged = route =>
        {
            log.Emit("route.changed", new JsonObject { ["route"] = (string)route });
            HookScene();
        };
        SceneRouter.RouteFailedEventHandler onFailed = route => log.Emit("route.failed", new JsonObject { ["route"] = (string)route });
        router.RouteChanging += onChanging;
        router.RouteChanged += onChanged;
        router.RouteFailed += onFailed;
        _disconnects.Add(() => router.RouteChanging -= onChanging);
        _disconnects.Add(() => router.RouteChanged -= onChanged);
        _disconnects.Add(() => router.RouteFailed -= onFailed);

        SaveService saves = SaveService.Instance;
        SaveService.SaveFinishedEventHandler onSaved = (slot, result) => log.Emit("save.finished", SaveEvent(slot, result));
        SaveService.LoadFinishedEventHandler onLoaded = (slot, result) => log.Emit("load.finished", SaveEvent(slot, result));
        saves.SaveFinished += onSaved;
        saves.LoadFinished += onLoaded;
        _disconnects.Add(() => saves.SaveFinished -= onSaved);
        _disconnects.Add(() => saves.LoadFinished -= onLoaded);

        InputMethodService inputMethod = InputMethodService.Instance;
        InputMethodService.MethodChangedEventHandler onMethod = method => log.Emit("input.method_changed",
            new JsonObject { ["method"] = method == InputMethodService.Method.Gamepad ? "gamepad" : "keyboard_mouse" });
        inputMethod.MethodChanged += onMethod;
        _disconnects.Add(() => inputMethod.MethodChanged -= onMethod);

        SettingsService settings = SettingsService.Instance;
        SettingsService.LocaleChangedEventHandler onLocale = locale => log.Emit("locale.changed", new JsonObject { ["locale"] = locale });
        settings.LocaleChanged += onLocale;
        _disconnects.Add(() => settings.LocaleChanged -= onLocale);

        GameSession session = Session;
        GameSession.CampaignStartedEventHandler onStarted = () => log.Emit("campaign.started");
        GameSession.CampaignLoadedEventHandler onCampaignLoaded = () => log.Emit("campaign.loaded");
        session.CampaignStarted += onStarted;
        session.CampaignLoaded += onCampaignLoaded;
        _disconnects.Add(() => session.CampaignStarted -= onStarted);
        _disconnects.Add(() => session.CampaignLoaded -= onCampaignLoaded);

        Action<string> onReleased = holdId => log.Emit("input.released", new JsonObject { ["hold"] = holdId });
        InputDriver.HoldReleased += onReleased;
        _disconnects.Add(() => InputDriver.HoldReleased -= onReleased);
        HookScene();
    }

    private static JsonObject SaveEvent(string slot, SaveResult result) => new()
    {
        ["slot"] = slot,
        ["ok"] = result.IsOk(),
        ["status"] = Snake(result.State.ToString()),
    };

    public override void CleanupSession()
    {
        foreach (AutomationProvider provider in Providers)
            provider.CleanupSession(Scene());
    }

    public override void Shutdown()
    {
        foreach (Action disconnect in _disconnects)
            disconnect();
        _disconnects.Clear();
    }

    private void HookScene()
    {
        Node current = Scene();
        if (current == null || _events == null)
            return;
        AutomationEventLog log = _events;
        foreach (ScreenStack stack in Stacks())
        {
            stack.ScreenOpened += screen => log.Emit("screen.opened", new JsonObject { ["screen"] = IdOf(screen) });
            stack.ScreenClosed += screen => log.Emit("screen.closed", new JsonObject { ["screen"] = IdOf(screen) });
        }
        foreach (AutomationProvider provider in Providers)
            provider.HookScene(current);
    }

    private async Task<bool> Goto(StringName route)
    {
        while (Router.IsTransitioning)
            await _tree.ToSignal(_tree, SceneTree.SignalName.ProcessFrame);
        return await Router.Goto(route);
    }

    private List<ScreenStack> Stacks() => FindNodes(Scene(), n => n is ScreenStack).Cast<ScreenStack>().ToList();

    /// <summary>Depth-first search in tree order, including script classes.</summary>
    public static List<Node> FindNodes(Node root, Func<Node, bool> predicate)
    {
        var output = new List<Node>();
        if (root == null)
            return output;
        var pending = new LinkedList<Node>();
        pending.AddFirst(root);
        while (pending.Count > 0)
        {
            Node node = pending.First.Value;
            pending.RemoveFirst();
            if (predicate(node))
                output.Add(node);
            Godot.Collections.Array<Node> children = node.GetChildren();
            for (int i = children.Count - 1; i >= 0; i--)
                pending.AddFirst(children[i]);
        }
        return output;
    }

    private UiScreen TopScreen()
    {
        foreach (ScreenStack stack in Stacks())
            if (!stack.IsEmpty())
                return stack.Top();
        return null;
    }

    private List<string> ScreenIds() =>
        FindNodes(Scene(), n => n is UiScreen)
            .Where(node => node.IsInsideTree() && !node.IsQueuedForDeletion())
            .Select(IdOf)
            .ToList();

    private string FocusedId()
    {
        Control focused = _tree.Root.GuiGetFocusOwner();
        return focused != null ? IdOf(focused) : "";
    }

    private static string IdOf(Node node) => node != null && node.HasMeta(MetaId) ? node.GetMeta(MetaId).AsString() : "";

    private static int Round(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);

    public static JsonArray ToArray(IEnumerable<string> values) => new(values.Select(v => (JsonNode)v).ToArray());

    public static JsonArray ToArray(IEnumerable<JsonObject> values) => new(values.Select(v => (JsonNode)v).ToArray());

    /// <summary>"VersionTooNew" -> "version_too_new" (protocol values are snake_case).</summary>
    public static string Snake(string pascal)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            if (char.IsUpper(pascal[i]) && i > 0)
                builder.Append('_');
            builder.Append(char.ToLowerInvariant(pascal[i]));
        }
        return builder.ToString();
    }
}

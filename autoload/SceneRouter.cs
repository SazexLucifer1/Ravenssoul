using System.Threading.Tasks;
using Godot;

namespace Utopia;

/// <summary>
/// SceneRouter (Autoload) — owns top-level scene changes.
///
/// Why global: a scene cannot replace itself safely, and the fade overlay must
/// outlive both the old and the new scene. Nothing else may call
/// SceneTree.ChangeSceneTo*.
/// </summary>
public partial class SceneRouter : Node
{
    [Signal] public delegate void RouteChangingEventHandler(StringName fromRoute, StringName toRoute);
    [Signal] public delegate void RouteChangedEventHandler(StringName route);
    [Signal] public delegate void RouteFailedEventHandler(StringName route);

    public const string RouteTablePath = "res://core/routing/route_table.tres";
    public const float FadeSeconds = 0.2f;

    /// <summary>The autoload instance (registered as "SceneRouter" in project.godot).</summary>
    public static SceneRouter Instance { get; private set; }

    public StringName CurrentRoute { get; private set; } = "";
    public bool IsTransitioning { get; private set; }

    private RouteTable _routeTable;
    private readonly CanvasLayer _overlayLayer = new();
    private readonly ColorRect _fade = new();

    public override void _EnterTree()
    {
        Instance ??= this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _routeTable = GD.Load<RouteTable>(RouteTablePath);
        _overlayLayer.Layer = 100;
        _fade.Color = Colors.Black;
        _fade.Modulate = _fade.Modulate with { A = 0.0f };
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlayLayer.AddChild(_fade);
        AddChild(_overlayLayer);
    }

    public bool HasRoute(StringName route) => _routeTable.HasRoute(route);

    /// <summary>
    /// Changes the top-level scene. Returns false if the request was rejected
    /// (unknown route, already transitioning, or the scene failed to load).
    /// Await it when the caller needs the new scene to be ready.
    /// </summary>
    public async Task<bool> Goto(StringName route)
    {
        if (IsTransitioning)
        {
            DevLog.Info("router", $"Ignored '{route}': transition already running");
            return false;
        }
        string path = _routeTable.PathFor(route);
        if (path.Length == 0 || !ResourceLoader.Exists(path))
        {
            DevLog.Error("router", $"Unknown route or missing scene: '{route}' -> '{path}'");
            EmitSignalRouteFailed(route);
            return false;
        }

        IsTransitioning = true;
        _fade.MouseFilter = Control.MouseFilterEnum.Stop; // swallow input during the change
        EmitSignalRouteChanging(CurrentRoute, route);
        await TweenFade(1.0f);

        var packed = ResourceLoader.Load(path) as PackedScene;
        if (packed == null)
        {
            DevLog.Error("router", $"Failed to load scene for route '{route}'");
            await FinishTransition();
            EmitSignalRouteFailed(route);
            return false;
        }

        GetTree().Paused = false;
        Error err = GetTree().ChangeSceneToPacked(packed);
        if (err != Error.Ok)
        {
            DevLog.Error("router", $"ChangeSceneToPacked failed ({err}) for '{route}'");
            await FinishTransition();
            EmitSignalRouteFailed(route);
            return false;
        }
        await ToSignal(GetTree(), SceneTree.SignalName.SceneChanged);

        CurrentRoute = route;
        DevLog.Info("router", $"Entered route '{route}'");
        await FinishTransition();
        EmitSignalRouteChanged(route);
        return true;
    }

    private async Task FinishTransition()
    {
        await TweenFade(0.0f);
        _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        IsTransitioning = false;
    }

    private async Task TweenFade(float targetAlpha)
    {
        float duration = SettingsService.Instance.ReducedMotion ? 0.0f : FadeSeconds;
        if (Mathf.IsZeroApprox(duration))
        {
            _fade.Modulate = _fade.Modulate with { A = targetAlpha };
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            return;
        }
        Tween tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", targetAlpha, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}

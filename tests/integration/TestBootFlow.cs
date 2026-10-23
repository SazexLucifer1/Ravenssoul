using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Integration;

/// <summary>Definition of done: bootstrap -> main menu -> gameplay without errors.</summary>
public class TestBootFlow : TestCase
{
    private static SceneRouter Router => SceneRouter.Instance;

    private Task<bool> WaitForRoute(StringName route) =>
        WaitUntil(() => Router.CurrentRoute == route && !Router.IsTransitioning);

    public async Task TestBootstrapRoutesToMainMenu()
    {
        AssertTrue(await Router.Goto(Routes.Bootstrap));
        AssertTrue(await WaitForRoute(Routes.MainMenu));
        AssertEq(Tree.CurrentScene.SceneFilePath, "res://scenes/main_menu/main_menu.tscn");
    }

    public async Task TestMainMenuNewGameEntersGameplay()
    {
        await Router.Goto(Routes.MainMenu);
        var menu = (MainMenu)Tree.CurrentScene;
        menu.NewGameButton.EmitSignal(BaseButton.SignalName.Pressed);
        AssertTrue(await WaitForRoute(Routes.Gameplay));
        var gameplay = (Gameplay)Tree.CurrentScene;
        AssertEq(gameplay.SceneFilePath, "res://scenes/gameplay/gameplay.tscn");
        AssertNotNull(gameplay.PlayerUnit);
        AssertEq(gameplay.PlayerUnit.Cell, GameSession.Instance.ActiveMission.Map.PlayerSpawn);
        AssertEq(gameplay.PlayerUnit.Health.MaxHealth, GameSession.Instance.ActiveLoadout.CombinedStats().MaxHealth);
    }

    public async Task TestRouterRejectsRequestsDuringATransition()
    {
        var changes = new List<StringName>();
        Router.Connect(SceneRouter.SignalName.RouteChanging,
            Callable.From((StringName from, StringName to) => changes.Add(to)), (uint)GodotObject.ConnectFlags.OneShot);
        _ = Router.Goto(Routes.MainMenu); // deliberately not awaited
        bool second = await Router.Goto(Routes.Gameplay);
        AssertFalse(second, "second request is rejected while the first runs");
        await WaitUntil(() => !Router.IsTransitioning);
        AssertEq(Router.CurrentRoute, Routes.MainMenu);
        AssertEq(changes, new[] { Routes.MainMenu });
    }

    public async Task TestUnknownRouteFailsSafely()
    {
        DevLog.Muted = true;
        var failures = new List<StringName>();
        Router.Connect(SceneRouter.SignalName.RouteFailed,
            Callable.From((StringName route) => failures.Add(route)), (uint)GodotObject.ConnectFlags.OneShot);
        AssertFalse(await Router.Goto("does_not_exist"));
        AssertEq(failures, new[] { "does_not_exist" });
        AssertFalse(Router.IsTransitioning);
    }

    public async Task TestTransitionWithMotionEnabledCompletes()
    {
        SettingsService.Instance.ReducedMotion = false;
        AssertTrue(await Router.Goto(Routes.MainMenu));
        AssertFalse(Router.IsTransitioning);
    }

    public async Task TestContinueWithDamagedSaveShowsPlayerMessage()
    {
        DevLog.Muted = true;
        ((MemorySaveBackend)SaveService.Instance.Backend).Slots[SaveService.DefaultSlot] = "{broken";
        await Router.Goto(Routes.MainMenu);
        var menu = (MainMenu)Tree.CurrentScene;
        AssertTrue(menu.ContinueButton.Visible, "continue shown when a save file exists");
        AssertTrue(menu.ContinueButton.HasFocus(), "continue is the primary action");
        menu.ContinueButton.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitFrames(2);
        var dialog = menu.ScreenStack.Top() as ConfirmDialog;
        AssertNotNull(dialog);
        AssertEq(dialog?.BodyLabel.Text, "SAVE_ERROR_CORRUPTED");
        AssertFalse(dialog?.CancelButton.Visible ?? true, "single-action message");
        AssertEq(Router.CurrentRoute, Routes.MainMenu);
    }

    public async Task TestFailedRouteResetsBusyButtonAndExplains()
    {
        DevLog.Muted = true;
        await Router.Goto(Routes.MainMenu);
        var menu = (MainMenu)Tree.CurrentScene;
        menu.NewGameButton.SetBusy(true);
        await Router.Goto("missing_route");
        AssertFalse(menu.NewGameButton.IsBusy());
        var dialog = menu.ScreenStack.Top() as ConfirmDialog;
        AssertNotNull(dialog);
        AssertEq(dialog?.BodyLabel.Text, "ROUTE_FAILED_BODY");
    }
}

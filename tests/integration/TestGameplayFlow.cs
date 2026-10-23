using System.Threading.Tasks;
using Godot;

namespace Utopia.Tests.Integration;

/// <summary>Minimal mission loop: move, hazards, pause, victory + autosave, defeat.</summary>
public class TestGameplayFlow : TestCase
{
    private async Task<Gameplay> EnterGameplay()
    {
        GameSession.Instance.StartNewCampaign();
        await SceneRouter.Instance.Goto(Routes.Gameplay);
        return (Gameplay)Tree.CurrentScene;
    }

    private static void Press(BaseButton button) => button.EmitSignal(BaseButton.SignalName.Pressed);

    public async Task TestReachingObjectiveWinsAndAutosaves()
    {
        Gameplay gameplay = await EnterGameplay();
        PlayerUnitController controller = gameplay.Controller;
        // Safe path on map_training_ground: east along row 10, then north along column 10.
        for (int i = 0; i < 9; i++)
            AssertTrue(controller.TryStep(Vector2I.Right), $"east step {i}");
        for (int i = 0; i < 9; i++)
            AssertTrue(controller.TryStep(Vector2I.Up), $"north step {i}");
        var result = gameplay.ScreenStack.Top() as MissionResultScreen;
        AssertNotNull(result);
        if (result == null)
            return;
        AssertEq(result.TitleLabel.Text, "RESULT_VICTORY_TITLE");
        AssertTrue(await WaitUntil(() => result.CurrentSaveState == MissionResultScreen.SaveState.Saved, 2.0));
        AssertTrue(SaveService.Instance.HasSave());
        AssertEq(GameSession.Instance.Wallet.Amount(ResourceWallet.Gold), 50);
        AssertTrue(result.RewardsLabel.Text.Contains("50"), result.RewardsLabel.Text);
        AssertFalse(controller.Enabled, "no movement after the mission ends");
    }

    public async Task TestFailedAutosaveExplainsAndOffersRetry()
    {
        var backend = (MemorySaveBackend)SaveService.Instance.Backend;
        backend.FailWrites = true;
        DevLog.Muted = true;
        Gameplay gameplay = await EnterGameplay();
        gameplay.Finish(true);
        var result = (MissionResultScreen)gameplay.ScreenStack.Top();
        AssertTrue(await WaitUntil(() => result.CurrentSaveState == MissionResultScreen.SaveState.Failed, 2.0));
        AssertEq(result.SaveStatusLabel.Text, "SAVE_ERROR_WRITE_FAILED");
        AssertTrue(result.SaveRetryButton.Visible);
        AssertFalse(result.ContinueButton.Disabled, "player is never trapped");
        backend.FailWrites = false;
        Press(result.SaveRetryButton);
        AssertTrue(await WaitUntil(() => result.CurrentSaveState == MissionResultScreen.SaveState.Saved, 2.0));
    }

    public async Task TestDefeatShowsRetryAndDoesNotSave()
    {
        Gameplay gameplay = await EnterGameplay();
        gameplay.PlayerUnit.Health.ApplyDamage(999);
        var result = gameplay.ScreenStack.Top() as MissionResultScreen;
        AssertNotNull(result);
        if (result == null)
            return;
        AssertEq(result.TitleLabel.Text, "RESULT_DEFEAT_TITLE");
        AssertTrue(result.RetryButton.Visible);
        AssertTrue(result.RetryButton.HasFocus());
        await WaitFrames(2);
        AssertFalse(SaveService.Instance.HasSave());
    }

    public async Task TestMoveRangeIsHighlightedAndHiddenWhenUnitCannotAct()
    {
        Gameplay gameplay = await EnterGameplay();
        BattleGrid grid = gameplay.Grid;
        AssertEq(grid.MoveHighlights.Count, 4);
        foreach (Vector2I cell in new[] { new Vector2I(1, 9), new Vector2I(1, 11), new Vector2I(0, 10), new Vector2I(2, 10) })
            AssertContains(grid.MoveHighlights, cell);
        gameplay.OpenPauseMenu();
        AssertEmpty(grid.MoveHighlights, "no move hints while paused");
        gameplay.ScreenStack.Pop();
        AssertEq(grid.MoveHighlights.Count, 4);
    }

    public async Task TestPauseMenuPausesAndResumes()
    {
        Gameplay gameplay = await EnterGameplay();
        ScreenStack stack = gameplay.ScreenStack;
        PressAction("pause");
        await WaitFrames(1);
        AssertTrue(stack.Top() is PauseMenu);
        AssertTrue(Tree.Paused);
        AssertTrue((stack.Top() as PauseMenu)?.ResumeButton.HasFocus() ?? false);
        PressAction("ui_cancel");
        await WaitFrames(1);
        AssertTrue(stack.IsEmpty());
        AssertFalse(Tree.Paused);
        AssertTrue(gameplay.Controller.Enabled);
    }

    public async Task TestPauseSettingsAndBackRestoreFocus()
    {
        Gameplay gameplay = await EnterGameplay();
        ScreenStack stack = gameplay.ScreenStack;
        PauseMenu pause = gameplay.OpenPauseMenu();
        pause.SettingsButton.GrabFocus();
        Press(pause.SettingsButton);
        AssertTrue(stack.Top() is SettingsScreen);
        PressAction("ui_cancel");
        await WaitFrames(1);
        AssertEq(stack.Top(), pause);
        AssertTrue(pause.SettingsButton.HasFocus(), "focus restored to the button that opened settings");
    }

    public async Task TestAbandonRequiresConfirmation()
    {
        Gameplay gameplay = await EnterGameplay();
        PauseMenu pause = gameplay.OpenPauseMenu();
        Press(pause.AbandonButton);
        var dialog = gameplay.ScreenStack.Top() as ConfirmDialog;
        AssertNotNull(dialog);
        if (dialog == null)
            return;
        AssertTrue(dialog.CancelButton.HasFocus(), "destructive dialogs focus the safe choice");
        Press(dialog.ConfirmButton);
        AssertTrue(await WaitUntil(() => SceneRouter.Instance.CurrentRoute == Routes.MainMenu && !SceneRouter.Instance.IsTransitioning));
        AssertFalse(Tree.Paused);
    }

    public async Task TestBlockedMoveShowsPlayerHint()
    {
        Gameplay gameplay = await EnterGameplay();
        AssertTrue(gameplay.Controller.TryStep(Vector2I.Left)); // (1,10) -> (0,10)
        AssertFalse(gameplay.Controller.TryStep(Vector2I.Left), "map edge");
        AssertEq(gameplay.Hud.HintLabel.Text, "You can't move there.");
    }

    public async Task TestHazardHintUsesPluralRules()
    {
        Gameplay gameplay = await EnterGameplay();
        // (1,10) -> (5,10) -> (5,9) -> hazard (5,8)
        for (int i = 0; i < 4; i++)
            gameplay.Controller.TryStep(Vector2I.Right);
        gameplay.Controller.TryStep(Vector2I.Up);
        gameplay.Controller.TryStep(Vector2I.Up);
        AssertEq(gameplay.Hud.HintLabel.Text, "Hazard! You lost 4 health points.");
        AssertEq(gameplay.PlayerUnit.Health.CurrentHealth, gameplay.PlayerUnit.Health.MaxHealth - 4);
    }
}

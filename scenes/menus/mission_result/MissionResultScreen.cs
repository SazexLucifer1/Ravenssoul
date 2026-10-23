using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Utopia;

/// <summary>
/// Victory/defeat summary. Shows rewards and the autosave state in plain
/// language, and offers a clear next action. Emits intent only.
/// </summary>
[GlobalClass]
public partial class MissionResultScreen : UiScreen
{
    [Signal] public delegate void ContinueRequestedEventHandler();
    [Signal] public delegate void RetryRequestedEventHandler();
    [Signal] public delegate void SaveRetryRequestedEventHandler();

    public enum SaveState { Hidden, Saving, Saved, Failed }

    [Export] public Label TitleLabel { get; set; }
    [Export] public Label BodyLabel { get; set; }
    [Export] public Label RewardsLabel { get; set; }
    [Export] public Label SaveStatusLabel { get; set; }
    [Export] public GameButton ContinueButton { get; set; }
    [Export] public GameButton RetryButton { get; set; }
    [Export] public GameButton SaveRetryButton { get; set; }

    public SaveState CurrentSaveState { get; private set; } = SaveState.Hidden;

    public override void _Ready()
    {
        CancelCloses = false;
        ContinueButton.Activated += EmitSignalContinueRequested;
        RetryButton.Activated += EmitSignalRetryRequested;
        SaveRetryButton.Activated += EmitSignalSaveRetryRequested;
    }

    /// <summary>context: victory (bool), rewards (IDictionary&lt;StringName, int&gt;)</summary>
    public override void OnOpened(ScreenContext context)
    {
        bool victory = context.Get("victory", false);
        TitleLabel.Text = victory ? "RESULT_VICTORY_TITLE" : "RESULT_DEFEAT_TITLE";
        TitleLabel.ThemeTypeVariation = victory ? "SuccessHeadingLabel" : "DangerHeadingLabel";
        BodyLabel.Text = victory ? "RESULT_VICTORY_BODY" : "RESULT_DEFEAT_BODY";
        RewardsLabel.Text = FormatRewards(context.Get<IDictionary<StringName, int>>("rewards", null));
        RewardsLabel.Visible = RewardsLabel.Text.Length > 0;
        RetryButton.Visible = !victory;
        ContinueButton.Text = victory ? "RESULT_CONTINUE" : "RESULT_RETURN_TO_MENU";
        ContinueButton.ThemeTypeVariation = victory ? "PrimaryButton" : "Button";
        DefaultFocus = victory ? ContinueButton : RetryButton;
        RelinkFocus();
        SetSaveState(SaveState.Hidden);
    }

    public void SetSaveState(SaveState state, string messageKey = "")
    {
        CurrentSaveState = state;
        SaveStatusLabel.Visible = state != SaveState.Hidden;
        SaveRetryButton.Visible = state == SaveState.Failed;
        ContinueButton.SetBusy(state == SaveState.Saving);
        switch (state)
        {
            case SaveState.Saving:
                SaveStatusLabel.ThemeTypeVariation = "MutedLabel";
                SaveStatusLabel.Text = "SAVE_STATUS_SAVING";
                break;
            case SaveState.Saved:
                SaveStatusLabel.ThemeTypeVariation = "SuccessLabel";
                SaveStatusLabel.Text = "SAVE_STATUS_SAVED";
                break;
            case SaveState.Failed:
                SaveStatusLabel.ThemeTypeVariation = "DangerLabel";
                SaveStatusLabel.Text = messageKey;
                break;
        }
        RelinkFocus();
    }

    public static string FormatRewards(IDictionary<StringName, int> rewards)
    {
        if (rewards == null || rewards.Count == 0)
            return "";
        List<string> parts = rewards.Select(pair => Loc.Fill(Loc.Template("REWARD_LINE"), new Dictionary<string, string>
        {
            ["amount"] = LocaleFormat.Integer(pair.Value),
            ["resource"] = Loc.Template("RESOURCE_" + pair.Key.ToString().ToUpperInvariant()),
        })).ToList();
        return Loc.Format("RESULT_REWARDS", new Dictionary<string, string>
        {
            ["list"] = string.Join(Loc.Template("LIST_SEPARATOR"), parts),
        });
    }

    private void RelinkFocus() =>
        FocusChain.LinkHorizontal(FocusChain.Focusable(new Control[] { RetryButton, ContinueButton, SaveRetryButton }));
}

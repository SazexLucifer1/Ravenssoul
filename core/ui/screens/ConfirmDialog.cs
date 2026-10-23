using Godot;

namespace Utopia;

/// <summary>
/// Generic yes/no confirmation. Destructive confirmations focus "cancel" first
/// so a stray press of the accept button never destroys progress.
/// </summary>
[GlobalClass]
public partial class ConfirmDialog : UiScreen
{
    [Signal] public delegate void ConfirmedEventHandler();
    [Signal] public delegate void CancelledEventHandler();

    [Export] public Label TitleLabel { get; set; }
    [Export] public Label BodyLabel { get; set; }
    [Export] public GameButton ConfirmButton { get; set; }
    [Export] public GameButton CancelButton { get; set; }

    public override void _Ready()
    {
        ConfirmButton.Activated += OnConfirm;
        CancelButton.Activated += OnCancel;
    }

    /// <summary>
    /// context keys: title_key, body_key, confirm_key, cancel_key,
    /// destructive (bool), single_action (bool: message box with one button).
    /// </summary>
    public override void OnOpened(ScreenContext context)
    {
        TitleLabel.Text = context.Get("title_key", "CONFIRM_TITLE_DEFAULT");
        BodyLabel.Text = context.Get("body_key", "");
        BodyLabel.Visible = BodyLabel.Text.Length > 0;
        ConfirmButton.Text = context.Get("confirm_key", "UI_CONFIRM");
        CancelButton.Text = context.Get("cancel_key", "UI_CANCEL");
        bool destructive = context.Get("destructive", false);
        ConfirmButton.ThemeTypeVariation = destructive ? "DangerButton" : "PrimaryButton";
        bool single = context.Get("single_action", false);
        CancelButton.Visible = !single;
        DefaultFocus = destructive && !single ? CancelButton : ConfirmButton;
        FocusChain.LinkHorizontal(FocusChain.Focusable(new Control[] { CancelButton, ConfirmButton }));
    }

    public override void OnCancel()
    {
        EmitSignalCancelled();
        EmitSignalCloseRequested();
    }

    private void OnConfirm()
    {
        EmitSignalConfirmed();
        EmitSignalCloseRequested();
    }
}

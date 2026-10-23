using Godot;

namespace Utopia;

/// <summary>Pause menu. Emits intent; the gameplay scene decides what happens.</summary>
[GlobalClass]
public partial class PauseMenu : UiScreen
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void SettingsRequestedEventHandler();
    [Signal] public delegate void AbandonRequestedEventHandler();

    [Export] public GameButton ResumeButton { get; set; }
    [Export] public GameButton SettingsButton { get; set; }
    [Export] public GameButton AbandonButton { get; set; }

    public override void _Ready()
    {
        ResumeButton.Activated += EmitSignalResumeRequested;
        SettingsButton.Activated += EmitSignalSettingsRequested;
        AbandonButton.Activated += EmitSignalAbandonRequested;
        FocusChain.LinkVertical(new Control[] { ResumeButton, SettingsButton, AbandonButton });
        DefaultFocus = ResumeButton;
    }

    public override void OnCancel() => EmitSignalResumeRequested();

    /// <summary>Same as pressing "Abandon mission" (tests and screenshot tools).</summary>
    public void RequestAbandon() => EmitSignalAbandonRequested();
}

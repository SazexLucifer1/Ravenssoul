using Godot;

namespace Utopia;

/// <summary>Battle HUD. Displays state it is bound to; it never changes game state.</summary>
[GlobalClass]
public partial class Hud : CanvasLayer
{
    [Export] public Label UnitNameLabel { get; set; }
    [Export] public HealthBar HealthBar { get; set; }
    [Export] public Label MissionTitleLabel { get; set; }
    [Export] public Label ObjectiveLabel { get; set; }
    [Export] public Label HintLabel { get; set; }

    private Tween _hintTween;

    public void Bind(Character unit, MissionData mission)
    {
        UnitNameLabel.Text = unit.DisplayNameKey;
        HealthBar.Bind(unit.Health);
        MissionTitleLabel.Text = mission.TitleKey;
        ObjectiveLabel.Text = mission.ObjectiveKey;
        HintLabel.Text = "";
    }

    /// <summary>Shows a short, already-translated hint, e.g. why a move was refused.</summary>
    public void ShowHint(string text)
    {
        HintLabel.Text = text;
        HintLabel.Modulate = HintLabel.Modulate with { A = 1.0f };
        _hintTween?.Kill();
        _hintTween = CreateTween();
        _hintTween.TweenInterval(1.6);
        _hintTween.TweenProperty(HintLabel, "modulate:a", 0.0, UiMotion.TransitionSeconds());
    }
}

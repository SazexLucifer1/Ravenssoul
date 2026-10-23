using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Displays a HealthComponent. Always shows numbers as text so health is never
/// communicated by color alone.
/// </summary>
[GlobalClass]
public partial class HealthBar : ProgressBar
{
    private Label _valueLabel;
    private HealthComponent _health;

    public override void _Ready()
    {
        _valueLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };
        ShowPercentage = false;
        ThemeTypeVariation = "HealthBar";
        CustomMinimumSize = CustomMinimumSize with { Y = Mathf.Max(CustomMinimumSize.Y, 28.0f) };
        _valueLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_valueLabel);
    }

    // Autoload signals outlive this node: subscribe while in the tree only (docs/AI_INSTRUCTIONS.md).
    public override void _EnterTree() => SettingsService.Instance.LocaleChanged += OnLocaleChanged;

    public override void _ExitTree() => SettingsService.Instance.LocaleChanged -= OnLocaleChanged;

    public void Bind(HealthComponent health)
    {
        if (_health != null)
            _health.HealthChanged -= OnHealthChanged;
        _health = health;
        _health.HealthChanged += OnHealthChanged;
        Refresh();
    }

    public string ValueText() => _valueLabel.Text;

    private void OnLocaleChanged(string locale) => Refresh();

    private void OnHealthChanged(int current, int maximum, int delta) => Refresh();

    private void Refresh()
    {
        if (_health == null)
            return;
        MaxValue = _health.MaxHealth;
        Value = _health.CurrentHealth;
        _valueLabel.Text = Loc.Format("HUD_HEALTH_VALUE", new Dictionary<string, string>
        {
            ["current"] = LocaleFormat.Integer(_health.CurrentHealth),
            ["max"] = LocaleFormat.Integer(_health.MaxHealth),
        });
    }
}

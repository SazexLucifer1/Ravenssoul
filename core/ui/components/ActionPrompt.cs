using Godot;

namespace Utopia;

/// <summary>
/// "[Esc] Pause" style hint. Shows the glyph for the active input method and
/// updates live when the player switches between keyboard and gamepad.
/// </summary>
[GlobalClass]
public partial class ActionPrompt : HBoxContainer
{
    [Export] public StringName Action { get; set; } = "ui_cancel";
    [Export] public string TextKey { get; set; } = "";

    private Label _glyph;
    private Label _label;

    // Autoload signals outlive this node: subscribe while in the tree only (docs/AI_INSTRUCTIONS.md).
    public override void _EnterTree() => InputMethodService.Instance.MethodChanged += OnMethodChanged;

    public override void _ExitTree() => InputMethodService.Instance.MethodChanged -= OnMethodChanged;

    public override void _Ready()
    {
        _glyph = new Label { ThemeTypeVariation = "KeycapLabel", AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        _label = new Label { ThemeTypeVariation = "MutedLabel", Text = TextKey };
        AddChild(_glyph);
        AddChild(_label);
        OnMethodChanged(InputMethodService.Instance.Current);
    }

    public string GlyphText() => _glyph.Text;

    private void OnMethodChanged(InputMethodService.Method method)
    {
        if (_glyph == null)
            return;
        _glyph.Text = InputGlyphs.GlyphFor(Action, method);
        _glyph.Visible = _glyph.Text.Length > 0;
    }
}

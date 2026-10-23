using Godot;

namespace Utopia;

/// <summary>
/// Design tokens for the approved art direction: warm wood, brass, and
/// parchment with darker undertones (docs/ART_DIRECTION.md, docs/UI_STYLE_GUIDE.md).
/// The Theme is generated from these values by ThemeBuilder — change tokens,
/// then rebuild the theme; never hand-edit it.
/// </summary>
[GlobalClass]
public partial class UiTokens : Resource
{
    public const string DefaultPath = "res://core/ui/theme/ui_tokens.tres";

    private static UiTokens _default;

    /// <summary>The project's tokens (loaded once).</summary>
    public static UiTokens Default => _default ??= GD.Load<UiTokens>(DefaultPath);

    [ExportGroup("Surfaces")]
    [Export] public Color Background { get; set; } = new("16100c");
    [Export] public Color Surface { get; set; } = new("2b1e16");
    [Export] public Color SurfaceRaised { get; set; } = new("3c2a1e");
    [Export] public Color Border { get; set; } = new("9a7a44");
    [Export] public Color Scrim { get; set; } = new(0.05f, 0.03f, 0.02f, 0.72f);

    [ExportGroup("Text")]
    [Export] public Color TextPrimary { get; set; } = new("f1e4c8");
    [Export] public Color TextMuted { get; set; } = new("c9b391");
    [Export] public Color TextDisabled { get; set; } = new("8a7a64");
    [Export] public Color TextOnAccent { get; set; } = new("fff8ec");

    [ExportGroup("Semantic")]
    /// <summary>Revolution ember: primary actions.</summary>
    [Export] public Color Accent { get; set; } = new("b8452f");
    [Export] public Color AccentHover { get; set; } = new("d05a42");
    [Export] public Color AccentPressed { get; set; } = new("8f3423");
    /// <summary>Spectral teal: soul energy only.</summary>
    [Export] public Color Soul { get; set; } = new("5fc9c0");
    [Export] public Color Danger { get; set; } = new("ee7366");
    [Export] public Color Success { get; set; } = new("8fbf74");
    [Export] public Color Warning { get; set; } = new("e0b04f");
    /// <summary>Focus ring must stay distinct from hover and from every semantic color.</summary>
    [Export] public Color Focus { get; set; } = new("f2c14e");

    [ExportGroup("Type")]
    [Export] public int FontSizeSmall { get; set; } = 16;
    [Export] public int FontSizeBody { get; set; } = 20;
    [Export] public int FontSizeHeading { get; set; } = 30;
    [Export] public int FontSizeTitle { get; set; } = 56;

    [ExportGroup("Shape and spacing")]
    [Export] public int CornerRadius { get; set; } = 1;
    [Export] public int BorderWidth { get; set; } = 2;
    [Export] public int FocusWidth { get; set; } = 3;
    [Export] public int SpacingXs { get; set; } = 4;
    [Export] public int SpacingS { get; set; } = 8;
    [Export] public int SpacingM { get; set; } = 16;
    [Export] public int SpacingL { get; set; } = 24;
    [Export] public int SpacingXl { get; set; } = 40;
    /// <summary>Minimum interactive target, also used for touch-safe sizing.</summary>
    [Export] public int ButtonMinHeight { get; set; } = 52;
    [Export] public int ButtonMinWidth { get; set; } = 280;

    [ExportGroup("Motion")]
    [Export(PropertyHint.Range, "0.15,0.3")] public float TransitionSeconds { get; set; } = 0.2f;
    [Export(PropertyHint.Range, "0.05,0.2")] public float PressFeedbackSeconds { get; set; } = 0.1f;
}

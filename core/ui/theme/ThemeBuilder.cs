using Godot;

namespace Utopia;

/// <summary>
/// Generates the project Theme from UiTokens.
/// Rebuild after changing tokens:
///   godot --headless res://core/ui/theme/build_theme.tscn
/// </summary>
public static class ThemeBuilder
{
    public const string TokensPath = UiTokens.DefaultPath;
    public const string ThemePath = "res://assets/ui/theme/utopia_theme.tres";

    public static Theme Build(UiTokens t)
    {
        var theme = new Theme { DefaultFontSize = t.FontSizeBody };

        // Labels and type variations.
        theme.SetColor("font_color", "Label", t.TextPrimary);
        Variation(theme, "TitleLabel", "Label");
        theme.SetFontSize("font_size", "TitleLabel", t.FontSizeTitle);
        theme.SetColor("font_color", "TitleLabel", t.TextPrimary);
        Variation(theme, "HeadingLabel", "Label");
        theme.SetFontSize("font_size", "HeadingLabel", t.FontSizeHeading);
        Variation(theme, "MutedLabel", "Label");
        theme.SetColor("font_color", "MutedLabel", t.TextMuted);
        Variation(theme, "DangerLabel", "Label");
        theme.SetColor("font_color", "DangerLabel", t.Danger);
        Variation(theme, "SuccessLabel", "Label");
        theme.SetColor("font_color", "SuccessLabel", t.Success);
        // Result titles keep heading size while using semantic color.
        Variation(theme, "SuccessHeadingLabel", "Label");
        theme.SetFontSize("font_size", "SuccessHeadingLabel", t.FontSizeHeading);
        theme.SetColor("font_color", "SuccessHeadingLabel", t.Success);
        Variation(theme, "DangerHeadingLabel", "Label");
        theme.SetFontSize("font_size", "DangerHeadingLabel", t.FontSizeHeading);
        theme.SetColor("font_color", "DangerHeadingLabel", t.Danger);
        Variation(theme, "KeycapLabel", "Label");
        theme.SetFontSize("font_size", "KeycapLabel", t.FontSizeSmall);
        theme.SetStylebox("normal", "KeycapLabel", Box(t.SurfaceRaised, t.Border, t.BorderWidth, t.CornerRadius, t.SpacingS, t.SpacingXs));

        // Panels.
        theme.SetStylebox("panel", "PanelContainer", Box(t.Surface, t.Border, t.BorderWidth, t.CornerRadius, t.SpacingL, t.SpacingL));
        Variation(theme, "HudPanel", "PanelContainer");
        theme.SetStylebox("panel", "HudPanel", Box(t.Surface with { A = 0.9f }, t.Border, t.BorderWidth, t.CornerRadius, t.SpacingM, t.SpacingS));
        theme.SetStylebox("panel", "Panel", Box(t.Surface, t.Border, t.BorderWidth, t.CornerRadius, 0, 0));

        // Buttons: every interactive state is explicit.
        ButtonStyles(theme, "Button", t.SurfaceRaised, t.Border, t);
        theme.SetConstant("h_separation", "Button", t.SpacingS);
        Variation(theme, "PrimaryButton", "Button");
        ButtonStyles(theme, "PrimaryButton", t.Accent, t.AccentHover, t);
        theme.SetStylebox("hover", "PrimaryButton", Box(t.AccentHover, t.AccentHover, t.BorderWidth, t.CornerRadius, t.SpacingL, t.SpacingS));
        theme.SetStylebox("pressed", "PrimaryButton", Box(t.AccentPressed, t.AccentPressed, t.BorderWidth, t.CornerRadius, t.SpacingL, t.SpacingS));
        theme.SetStylebox("hover_pressed", "PrimaryButton", theme.GetStylebox("pressed", "PrimaryButton"));
        foreach (string state in new[] { "hover", "pressed", "hover_pressed" })
            theme.SetStylebox(state + "_mirrored", "PrimaryButton", theme.GetStylebox(state, "PrimaryButton"));
        theme.SetColor("font_color", "PrimaryButton", t.TextOnAccent);
        theme.SetColor("font_hover_color", "PrimaryButton", t.TextOnAccent);
        theme.SetColor("font_focus_color", "PrimaryButton", t.TextOnAccent);
        theme.SetColor("font_pressed_color", "PrimaryButton", t.TextOnAccent);
        Variation(theme, "DangerButton", "Button");
        ButtonStyles(theme, "DangerButton", t.SurfaceRaised, t.Danger, t);
        theme.SetColor("font_color", "DangerButton", t.Danger);
        theme.SetColor("font_hover_color", "DangerButton", t.TextPrimary);

        ButtonStyles(theme, "OptionButton", t.SurfaceRaised, t.Border, t);
        theme.SetColor("font_color", "CheckButton", t.TextPrimary);
        theme.SetColor("font_hover_color", "CheckButton", t.TextPrimary);
        theme.SetColor("font_focus_color", "CheckButton", t.TextPrimary);
        theme.SetColor("font_pressed_color", "CheckButton", t.TextPrimary);
        theme.SetStylebox("focus", "CheckButton", FocusBox(t));
        theme.SetStylebox("panel", "PopupMenu", Box(t.SurfaceRaised, t.Border, t.BorderWidth, t.CornerRadius, t.SpacingS, t.SpacingS));
        theme.SetStylebox("hover", "PopupMenu", Box(t.Accent, t.Accent, 0, t.CornerRadius, t.SpacingS, t.SpacingXs));
        theme.SetColor("font_color", "PopupMenu", t.TextPrimary);
        theme.SetColor("font_hover_color", "PopupMenu", t.TextOnAccent);

        // Progress bars (health uses a semantic variation; text always accompanies color).
        theme.SetStylebox("background", "ProgressBar", Box(t.Background, t.Border, t.BorderWidth, t.CornerRadius, 0, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(t.Accent, t.Accent, 0, t.CornerRadius, 0, 0));
        theme.SetColor("font_color", "ProgressBar", t.TextPrimary);
        Variation(theme, "HealthBar", "ProgressBar");
        theme.SetStylebox("fill", "HealthBar", Box(t.Success, t.Success, 0, t.CornerRadius, 0, 0));
        Variation(theme, "SoulBar", "ProgressBar");
        theme.SetStylebox("fill", "SoulBar", Box(t.Soul, t.Soul, 0, t.CornerRadius, 0, 0));

        // Containers spacing.
        theme.SetConstant("separation", "VBoxContainer", t.SpacingM);
        theme.SetConstant("separation", "HBoxContainer", t.SpacingM);
        return theme;
    }

    private static void ButtonStyles(Theme theme, StringName typeName, Color fill, Color edge, UiTokens t)
    {
        int padX = t.SpacingL;
        int padY = t.SpacingS;
        theme.SetStylebox("normal", typeName, Box(fill, edge, t.BorderWidth, t.CornerRadius, padX, padY));
        theme.SetStylebox("hover", typeName, Box(fill.Lightened(0.12f), t.TextMuted, t.BorderWidth, t.CornerRadius, padX, padY));
        theme.SetStylebox("pressed", typeName, Box(fill.Darkened(0.25f), t.TextPrimary, t.BorderWidth, t.CornerRadius, padX, padY));
        theme.SetStylebox("disabled", typeName, Box(fill with { A = 0.45f }, edge with { A = 0.35f }, t.BorderWidth, t.CornerRadius, padX, padY));
        theme.SetStylebox("hover_pressed", typeName, theme.GetStylebox("pressed", typeName));
        theme.SetStylebox("focus", typeName, FocusBox(t));
        // Right-to-left layouts use the *_mirrored variants (OptionButton arrow side).
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            theme.SetStylebox(state + "_mirrored", typeName, theme.GetStylebox(state, typeName));
        theme.SetColor("font_color", typeName, t.TextPrimary);
        theme.SetColor("font_hover_color", typeName, t.TextPrimary);
        theme.SetColor("font_focus_color", typeName, t.TextPrimary);
        theme.SetColor("font_pressed_color", typeName, t.TextPrimary);
        theme.SetColor("font_disabled_color", typeName, t.TextDisabled);
    }

    private static StyleBoxFlat FocusBox(UiTokens t)
    {
        StyleBoxFlat box = Box(Colors.Transparent, t.Focus, t.FocusWidth, t.CornerRadius + 2, 0, 0);
        box.DrawCenter = false;
        box.SetExpandMarginAll(t.FocusWidth + 1);
        return box;
    }

    private static StyleBoxFlat Box(Color fill, Color edge, int edgeWidth, int radius, int padX, int padY)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = edge };
        box.SetBorderWidthAll(edgeWidth);
        box.SetCornerRadiusAll(radius);
        box.ContentMarginLeft = padX;
        box.ContentMarginRight = padX;
        box.ContentMarginTop = padY;
        box.ContentMarginBottom = padY;
        return box;
    }

    private static void Variation(Theme theme, StringName variation, StringName baseType) =>
        theme.SetTypeVariation(variation, baseType);
}

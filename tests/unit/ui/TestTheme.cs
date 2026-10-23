using System;
using Godot;

namespace Utopia.Tests.Unit.Ui;

/// <summary>
/// The committed Theme must be exactly what ThemeBuilder generates from the
/// tokens, so nobody hand-edits it and the two never drift.
/// </summary>
public class TestTheme : TestCase
{
    private static UiTokens Tokens => GD.Load<UiTokens>(ThemeBuilder.TokensPath);

    public void TestCommittedThemeMatchesTokens()
    {
        Theme built = ThemeBuilder.Build(Tokens);
        var saved = ResourceLoader.Load(ThemeBuilder.ThemePath, "", ResourceLoader.CacheMode.Ignore) as Theme;
        AssertNotNull(saved);
        if (saved == null)
            return;
        foreach (string typeName in built.GetTypeList())
        {
            foreach (string colorName in built.GetColorList(typeName))
                AssertEq(saved.GetColor(colorName, typeName), built.GetColor(colorName, typeName), $"{typeName}/{colorName}");
            foreach (string sizeName in built.GetFontSizeList(typeName))
                AssertEq(saved.GetFontSize(sizeName, typeName), built.GetFontSize(sizeName, typeName), $"{typeName}/{sizeName}");
            foreach (string boxName in built.GetStyleboxList(typeName))
            {
                var a = (StyleBoxFlat)built.GetStylebox(boxName, typeName);
                var b = saved.GetStylebox(boxName, typeName) as StyleBoxFlat;
                AssertNotNull(b, $"{typeName}/{boxName} missing (rebuild the theme)");
                if (b == null)
                    continue;
                AssertEq(b.BgColor, a.BgColor, $"{typeName}/{boxName} bg");
                AssertEq(b.BorderColor, a.BorderColor, $"{typeName}/{boxName} border");
            }
        }
    }

    public void TestEveryButtonTypeDefinesAllInteractiveStates()
    {
        Theme theme = ThemeDB.GetProjectTheme();
        AssertEq(theme.GetFontSize("font_size", "DangerHeadingLabel"), theme.GetFontSize("font_size", "HeadingLabel"), "result titles keep heading size");
        foreach (string typeName in new[] { "Button", "PrimaryButton", "DangerButton", "OptionButton" })
            foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus",
                         "normal_mirrored", "hover_mirrored", "pressed_mirrored", "disabled_mirrored" })
                AssertTrue(theme.HasStylebox(state, typeName), $"{typeName}/{state}");
    }

    public void TestFocusRingIsDistinctFromHover()
    {
        UiTokens tokens = Tokens;
        AssertNe(tokens.Focus, tokens.AccentHover);
        AssertNe(tokens.Focus, tokens.TextMuted);
    }

    public void TestTextContrastMeetsWcagAa()
    {
        UiTokens t = Tokens;
        foreach ((Color fg, Color bg) in new[]
        {
            (t.TextPrimary, t.Surface), (t.TextMuted, t.Surface), (t.TextOnAccent, t.Accent), (t.Danger, t.Surface),
            (t.Danger, t.SurfaceRaised), (t.Success, t.Surface), (t.TextPrimary, t.SurfaceRaised),
        })
        {
            double ratio = Contrast(fg, bg);
            AssertTrue(ratio >= 4.5, $"contrast {ratio:F2} for {fg.ToHtml()} on {bg.ToHtml()}");
        }
    }

    public void TestTextScaleScalesAndRestoresWithoutCompounding()
    {
        Theme theme = ThemeBuilder.Build(Tokens);
        int baseSize = theme.GetFontSize("font_size", "TitleLabel");
        ThemeScaler.Apply(theme, 1.5f);
        ThemeScaler.Apply(theme, 1.5f);
        AssertEq(theme.GetFontSize("font_size", "TitleLabel"), (int)Math.Round(baseSize * 1.5, MidpointRounding.AwayFromZero));
        ThemeScaler.Apply(theme, 1.0f);
        AssertEq(theme.GetFontSize("font_size", "TitleLabel"), baseSize);
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a);
        double lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(Color c)
    {
        static double Channel(float v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }
}

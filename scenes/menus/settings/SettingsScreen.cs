using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Player preferences: language, text size, reduced motion.
/// Reads and writes the Settings autoload; changes apply immediately.
/// </summary>
[GlobalClass]
public partial class SettingsScreen : UiScreen
{
    public static readonly IReadOnlyList<string> TextSizeKeys = new[]
    {
        "SETTINGS_TEXT_SIZE_DEFAULT", "SETTINGS_TEXT_SIZE_LARGE", "SETTINGS_TEXT_SIZE_LARGEST",
    };

    [Export] public OptionButton LanguageOption { get; set; }
    [Export] public OptionButton TextSizeOption { get; set; }
    [Export] public CheckButton ReducedMotionToggle { get; set; }
    [Export] public GameButton BackButton { get; set; }

    public override void _Ready()
    {
        Populate();
        LanguageOption.ItemSelected += OnLanguageSelected;
        TextSizeOption.ItemSelected += OnTextSizeSelected;
        ReducedMotionToggle.Toggled += pressed => SettingsService.Instance.SetReducedMotion(pressed);
        BackButton.Activated += EmitSignalCloseRequested;
        FocusChain.LinkVertical(new Control[] { LanguageOption, TextSizeOption, ReducedMotionToggle, BackButton });
        DefaultFocus = LanguageOption;
    }

    private void Populate()
    {
        SettingsService settings = SettingsService.Instance;
        LanguageOption.Clear();
        foreach (string locale in LocaleRegistry.Supported)
        {
            LanguageOption.AddItem(LocaleRegistry.Endonym(locale));
            LanguageOption.SetItemMetadata(LanguageOption.ItemCount - 1, locale);
            if (locale == settings.Locale)
                LanguageOption.Select(LanguageOption.ItemCount - 1);
        }
        // Endonyms must not be translated into the current UI language.
        LanguageOption.AutoTranslateMode = AutoTranslateModeEnum.Disabled;

        TextSizeOption.Clear();
        for (int i = 0; i < SettingsService.TextScales.Count; i++)
        {
            TextSizeOption.AddItem(TextSizeKeys[i]);
            if (Mathf.IsEqualApprox(SettingsService.TextScales[i], settings.TextScale))
                TextSizeOption.Select(i);
        }
        ReducedMotionToggle.SetPressedNoSignal(settings.ReducedMotion);
    }

    private void OnLanguageSelected(long index) =>
        SettingsService.Instance.SetLocale(LanguageOption.GetItemMetadata((int)index).AsString());

    private void OnTextSizeSelected(long index) =>
        SettingsService.Instance.SetTextScale(SettingsService.TextScales[(int)index]);
}

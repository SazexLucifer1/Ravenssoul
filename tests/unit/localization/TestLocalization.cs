using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using Utopia.Tests.Tools;

namespace Utopia.Tests.Unit.Localization;

/// <summary>
/// Catalog validation: missing/unused keys, placeholder parity, hard-coded
/// text, fallback, pseudolocalization, plurals, and number formatting.
/// </summary>
public class TestLocalization : TestCase
{
    private const string CatalogDir = "res://assets/localization";
    private const string PlaceholderPattern = @"\{[a-z_]+\}";
    /// <summary>Keys built at runtime from stable ids (e.g. "RESOURCE_" + id). Documented in docs/LOCALIZATION.md.</summary>
    private static readonly string[] DynamicPrefixes = { "RESOURCE_" };

    private static Translation Catalog(string locale) => GD.Load<Translation>($"{CatalogDir}/{locale}.po");

    private static List<string> Keys(string locale) =>
        Catalog(locale).GetMessageList().Where(k => k.Length > 0).OrderBy(k => k, System.StringComparer.Ordinal).ToList();

    private static string CatalogText(string locale) => FileAccess.GetFileAsString($"{CatalogDir}/{locale}.po");

    private static List<string> PluralKeys(string locale) =>
        Regex.Matches(CatalogText(locale), "msgid \"(\\w+)\"\\nmsgid_plural").Select(m => m.Groups[1].Value).ToList();

    private static List<string> Placeholders(string text) =>
        Regex.Matches(text, PlaceholderPattern).Select(m => m.Value).OrderBy(v => v, System.StringComparer.Ordinal).ToList();

    public void TestEverySupportedLocaleHasACatalog()
    {
        foreach (string locale in LocaleRegistry.Supported)
        {
            AssertNotNull(Catalog(locale), locale);
            AssertContains(TranslationServer.GetLoadedLocales(), locale);
        }
    }

    public void TestLaunchLocalesHaveIdenticalKeySets()
    {
        List<string> source = Keys(LocaleRegistry.SourceLocale);
        foreach (string locale in LocaleRegistry.Supported)
        {
            List<string> keys = Keys(locale);
            foreach (string key in source)
                AssertTrue(keys.Contains(key), $"{locale} is missing {key}");
            foreach (string key in keys)
                AssertTrue(source.Contains(key), $"{locale} has key {key} that is not in the source catalog");
        }
    }

    public void TestPlaceholdersMatchSource()
    {
        Translation source = Catalog(LocaleRegistry.SourceLocale);
        List<string> plurals = PluralKeys(LocaleRegistry.SourceLocale);
        foreach (string locale in LocaleRegistry.Supported)
        {
            Translation catalog = Catalog(locale);
            foreach (string key in Keys(LocaleRegistry.SourceLocale))
            {
                if (!plurals.Contains(key))
                {
                    AssertEq(Placeholders(catalog.GetMessage(key)), Placeholders(source.GetMessage(key)), $"{locale} {key}");
                    continue;
                }
                foreach (int n in new[] { 1, 2 })
                    AssertEq(Placeholders(catalog.GetPluralMessage(key, key + "_PLURAL", n)),
                        Placeholders(source.GetPluralMessage(key, key + "_PLURAL", n)), $"{locale} {key} (n={n})");
            }
        }
    }

    public void TestPluralKeysFollowNamingRule()
    {
        foreach (string locale in LocaleRegistry.Supported)
            foreach (Match m in Regex.Matches(CatalogText(locale), "msgid \"(\\w+)\"\\nmsgid_plural \"(\\w+)\""))
                AssertEq(m.Groups[2].Value, m.Groups[1].Value + "_PLURAL", $"{locale} plural id");
    }

    public void TestTranslationsAreNotStale()
    {
        // Every non-source entry carries "#. English: <source text>". When the
        // English changes, this fails until a translator reviews the entry and
        // updates the reference line.
        Translation source = Catalog(LocaleRegistry.SourceLocale);
        List<string> plurals = PluralKeys(LocaleRegistry.SourceLocale);
        foreach (string locale in LocaleRegistry.Supported.Where(l => l != LocaleRegistry.SourceLocale))
        {
            var seen = new HashSet<string>();
            foreach (Match m in Regex.Matches(CatalogText(locale), "#\\. English: (.*)\\nmsgid \"(\\w+)\""))
            {
                string key = m.Groups[2].Value;
                seen.Add(key);
                string english = plurals.Contains(key) ? source.GetPluralMessage(key, key + "_PLURAL", 1) : source.GetMessage(key);
                AssertEq(m.Groups[1].Value, english, $"{locale} {key} needs review");
            }
            foreach (string key in Keys(locale))
                AssertTrue(seen.Contains(key), $"{locale} {key} has no '#. English:' reference");
        }
    }

    public void TestAllReferencedKeysExist()
    {
        List<string> keys = Keys(LocaleRegistry.SourceLocale);
        foreach (string path in SourceFiles.Collect())
        {
            foreach (Match m in Regex.Matches(FileAccess.GetFileAsString(path), SourceFiles.KeyPattern))
            {
                string key = m.Groups[1].Value;
                if (key.EndsWith("_PLURAL"))
                    key = key[..^"_PLURAL".Length];
                if (SourceFiles.NotTranslationKeys.Contains(key))
                    continue;
                AssertTrue(keys.Contains(key), $"{path} references missing key {key}");
            }
        }
    }

    public void TestNoUnusedKeys()
    {
        var corpus = new StringBuilder();
        foreach (string path in SourceFiles.Collect())
            corpus.Append(FileAccess.GetFileAsString(path));
        string text = corpus.ToString();
        foreach (string key in Keys(LocaleRegistry.SourceLocale))
            if (!DynamicPrefixes.Any(key.StartsWith))
                AssertTrue(text.Contains($"\"{key}\""), $"unused key {key}");
    }

    public void TestDynamicResourceKeysExist()
    {
        List<string> keys = Keys(LocaleRegistry.SourceLocale);
        foreach (StringName id in ResourceWallet.All)
            AssertTrue(keys.Contains("RESOURCE_" + id.ToString().ToUpperInvariant()), id);
    }

    public void TestNamedPlaceholdersAreFilledThroughLoc()
    {
        // Filling Tr(key) by hand breaks under pseudolocalization; see core/localization/Loc.cs.
        var forbidden = new Regex(@"\bTrN?\([^\n]*\)\s*\.\s*(Replace|Format)\(|string\.Format\(\s*TrN?\(");
        foreach (string path in SourceFiles.Collect(extensions: new[] { "cs" }))
            foreach (string line in FileAccess.GetFileAsString(path).Split('\n'))
                if (!line.TrimStart().StartsWith("//"))
                    AssertFalse(forbidden.IsMatch(line), $"{path} fills Tr() by hand; use Loc.Format(): {line.Trim()}");
    }

    public void TestScenesContainNoHardCodedDisplayText()
    {
        var lineRegex = new Regex("^(text|tooltip_text|placeholder_text) = \"(.*)\"$");
        var keyRegex = new Regex("^[A-Z][A-Z0-9_]*$");
        foreach (string path in SourceFiles.Collect(extensions: new[] { "tscn" }))
        {
            foreach (string line in FileAccess.GetFileAsString(path).Split('\n'))
            {
                Match m = lineRegex.Match(line);
                if (m.Success && m.Groups[2].Value.Length > 0)
                    AssertTrue(keyRegex.IsMatch(m.Groups[2].Value), $"{path}: hard-coded text '{m.Groups[2].Value}'");
            }
        }
    }

    public void TestPlayerTextHasNoDeveloperArtifacts()
    {
        foreach (string locale in LocaleRegistry.Supported)
        {
            Translation catalog = Catalog(locale);
            foreach (string key in Keys(locale))
            {
                string text = catalog.GetMessage(key);
                foreach (string forbidden in new[] { "TODO", "FIXME", "res://", "user://", "%s", "%d", "null", "Error", "Exception" })
                    AssertFalse(text.Contains(forbidden), $"{locale} {key} contains '{forbidden}'");
                AssertFalse(text.Trim().Length == 0 && key != "LIST_SEPARATOR", $"{locale} {key} is empty");
            }
        }
    }

    public void TestMissingTranslationFallsBackToSourceLocale()
    {
        var partial = new Translation { Locale = "fr" };
        partial.AddMessage("UI_OK", "D'accord");
        TranslationServer.AddTranslation(partial);
        TranslationServer.SetLocale("fr");
        AssertEq(TranslationServer.Translate("UI_OK"), "D'accord");
        AssertEq(TranslationServer.Translate("UI_CANCEL"), "Cancel", "missing key uses the fallback locale");
        TranslationServer.RemoveTranslation(partial);
    }

    public void TestRuntimeLanguageSwitch()
    {
        SettingsService settings = SettingsService.Instance;
        settings.SettingsPath = "user://test_settings_locale.cfg";
        settings.SetLocale("de");
        AssertEq(TranslationServer.Translate("MAIN_MENU_QUIT"), "Spiel beenden");
        settings.SetLocale("en");
        AssertEq(TranslationServer.Translate("MAIN_MENU_QUIT"), "Quit Game");
        settings.SetLocale("xx");
        AssertEq(settings.Locale, "en", "unsupported locales are ignored");
        DirAccess.RemoveAbsolute("user://test_settings_locale.cfg");
    }

    public void TestOsLocaleMatching()
    {
        AssertEq(LocaleRegistry.BestMatch("de_AT"), "de");
        AssertEq(LocaleRegistry.BestMatch("ja_JP"), "en");
    }

    public void TestEndonymsComeFromEachCatalog()
    {
        AssertEq(LocaleRegistry.Endonym("de"), "Deutsch");
        AssertEq(LocaleRegistry.Endonym("en"), "English");
    }

    public void TestPluralForms()
    {
        var one = new Dictionary<string, string> { ["amount"] = "1" };
        var four = new Dictionary<string, string> { ["amount"] = "4" };
        AssertEq(Loc.Fill(TranslationServer.TranslatePlural("HUD_HINT_HAZARD", "HUD_HINT_HAZARD_PLURAL", 1), one), "Hazard! You lost 1 health point.");
        AssertEq(Loc.Fill(TranslationServer.TranslatePlural("HUD_HINT_HAZARD", "HUD_HINT_HAZARD_PLURAL", 4), four), "Hazard! You lost 4 health points.");
        TranslationServer.SetLocale("de");
        AssertTrue(TranslationServer.TranslatePlural("HUD_HINT_HAZARD", "HUD_HINT_HAZARD_PLURAL", 4).ToString().EndsWith("Lebenspunkte verloren."));
    }

    public void TestPseudolocalizationExpandsAndMarksText()
    {
        string plain = TranslationServer.Translate("MAIN_MENU_NEW_GAME");
        TranslationServer.PseudolocalizationEnabled = true;
        TranslationServer.ReloadPseudolocalization();
        string pseudo = TranslationServer.Translate("MAIN_MENU_NEW_GAME");
        TranslationServer.PseudolocalizationEnabled = false;
        AssertTrue(pseudo.StartsWith('[') && pseudo.EndsWith(']'), pseudo);
        AssertTrue(pseudo.Length >= (int)(plain.Length * 1.3), $"expected >=30% expansion: {pseudo}");
    }

    public void TestPseudolocalizationKeepsNamedPlaceholdersWorking()
    {
        TranslationServer.PseudolocalizationEnabled = true;
        TranslationServer.ReloadPseudolocalization();
        string version = Loc.Format("MAIN_MENU_VERSION", new Dictionary<string, string> { ["version"] = "0.1.0" });
        string hazard = Loc.FormatPlural("HUD_HINT_HAZARD", 4, new Dictionary<string, string> { ["amount"] = "4" });
        TranslationServer.PseudolocalizationEnabled = false;
        AssertTrue(version.Contains("0.1.0") && !version.Contains('{'), version);
        AssertTrue(hazard.Contains('4') && !hazard.Contains('{') && hazard.StartsWith('['), hazard);
    }

    public void TestLocFormatMatchesEngineTranslation()
    {
        AssertEq(Loc.Format("MAIN_MENU_VERSION", new Dictionary<string, string> { ["version"] = "1.2" }), "Version 1.2");
        AssertEq(Loc.FormatPlural("HUD_HINT_HAZARD", 1, new Dictionary<string, string> { ["amount"] = "1" }), "Hazard! You lost 1 health point.");
        TranslationServer.SetLocale("de");
        AssertEq(Loc.FormatPlural("HUD_HINT_HAZARD", 2, new Dictionary<string, string> { ["amount"] = "2" }), "Gefahr! Du hast 2 Lebenspunkte verloren.");
    }

    public void TestRtlLocaleDetection()
    {
        AssertTrue(LocaleRegistry.IsRtl("ar"));
        AssertTrue(LocaleRegistry.IsRtl("he_IL"));
        AssertFalse(LocaleRegistry.IsRtl("de"));
    }

    public void TestNumberFormattingPerLocale()
    {
        AssertEq(LocaleFormat.Integer(1234567, "en"), "1,234,567");
        AssertEq(LocaleFormat.Integer(1234567, "de"), "1.234.567");
        AssertEq(LocaleFormat.Integer(-950, "en"), "-950");
        AssertEq(LocaleFormat.Decimal(1234.5, 1, "de"), "1.234,5");
        AssertEq(LocaleFormat.Percent(0.75, "en"), "75%");
        AssertEq(LocaleFormat.Percent(0.75, "de"), "75 %");
        AssertEq(LocaleFormat.Integer(42, "ar"), "٤٢", "native digits via TextServer");
    }

    public void TestDefaultFontCoversLaunchLocales()
    {
        Font font = ThemeDB.FallbackFont;
        foreach (string locale in LocaleRegistry.Supported)
        {
            Translation catalog = Catalog(locale);
            foreach (string key in Keys(locale))
            {
                string text = catalog.GetMessage(key);
                foreach (Rune rune in text.EnumerateRunes())
                    if (rune.Value > 32)
                        AssertTrue(font.HasChar(rune.Value), $"{locale} {key}: glyph U+{rune.Value:X4} missing");
            }
        }
    }
}

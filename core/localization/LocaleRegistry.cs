using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Utopia;

/// <summary>
/// Supported player-selectable locales and their properties.
/// Adding a language: see docs/LOCALIZATION.md ("Adding a language").
/// </summary>
public static class LocaleRegistry
{
    public const string SourceLocale = "en";
    public static readonly IReadOnlyList<string> Supported = new[] { "en", "de" };

    /// <summary>
    /// Locales whose UI must lay out right-to-left. None ship yet; the list exists
    /// so RTL support is data-driven when Arabic/Hebrew are added.
    /// </summary>
    public static readonly IReadOnlyList<string> RtlLocales = new[] { "ar", "he", "fa", "ur" };

    public static bool IsSupported(string locale) => Supported.Contains(locale);

    public static bool IsRtl(string locale) =>
        RtlLocales.Contains(TranslationServer.StandardizeLocale(locale).Split('_')[0]);

    /// <summary>Picks the best supported locale for an OS locale such as "de_AT".</summary>
    public static string BestMatch(string osLocale)
    {
        string language = TranslationServer.StandardizeLocale(osLocale).Split('_')[0];
        return IsSupported(language) ? language : SourceLocale;
    }

    /// <summary>
    /// The language's own name ("Deutsch"), read from that language's catalog so
    /// the picker always shows endonyms regardless of the current UI language.
    /// </summary>
    public static string Endonym(string locale)
    {
        Translation translation = Catalog(locale);
        if (translation != null && translation.Locale.StartsWith(locale))
        {
            string name = translation.GetMessage("LANGUAGE_ENDONYM");
            if (!string.IsNullOrEmpty(name))
                return name;
        }
        return TranslationServer.GetLocaleName(locale);
    }

    /// <summary>Best-matching loaded catalog for a locale ("de" also matches "de_AT"), or null.</summary>
    internal static Translation Catalog(string locale)
    {
        Godot.Collections.Array<Translation> found = TranslationServer.FindTranslations(locale, false);
        return found.Count > 0 ? found[0] : null;
    }
}

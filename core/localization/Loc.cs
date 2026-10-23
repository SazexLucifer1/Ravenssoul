using System.Collections.Generic;
using Godot;

namespace Utopia;

/// <summary>
/// Formatting helpers for translated text with named {placeholders}.
///
/// Always use these instead of filling <c>Tr(key)</c> by hand: Godot's
/// pseudolocalizer only protects %-style placeholders, so it would mangle
/// {names} before they are filled, and plural lookups skip pseudolocalization.
/// Here placeholders are filled first, then the finished string is
/// pseudolocalized, so pseudo builds exercise the real final text.
/// </summary>
public static class Loc
{
    /// <summary>Translates <paramref name="key"/> and fills its {placeholders} from <paramref name="args"/>.</summary>
    public static string Format(StringName key, IReadOnlyDictionary<string, string> args = null) =>
        Finish(Fill(Lookup(key, null, 1), args));

    /// <summary>
    /// Plural-aware variant. Catalog entries use msgid KEY / msgid_plural KEY_PLURAL.
    /// <paramref name="count"/> picks the plural form and is available as {count}.
    /// </summary>
    public static string FormatPlural(StringName key, int count, IReadOnlyDictionary<string, string> args = null)
    {
        var all = new Dictionary<string, string>();
        if (args != null)
            foreach ((string name, string value) in args)
                all[name] = value;
        if (!all.ContainsKey("count"))
            all["count"] = LocaleFormat.Integer(count);
        return Finish(Fill(Lookup(key, new StringName(key + "_PLURAL"), count), all));
    }

    /// <summary>
    /// The translated template without placeholders filled or pseudolocalization
    /// applied. Use only to build pieces that are passed into another Format() call.
    /// </summary>
    public static string Template(StringName key) => Lookup(key, null, 1);

    /// <summary>Replaces each {name} with its value (Godot String.format semantics).</summary>
    public static string Fill(string template, IReadOnlyDictionary<string, string> args)
    {
        if (args == null)
            return template;
        foreach ((string name, string value) in args)
            template = template.Replace("{" + name + "}", value);
        return template;
    }

    private static string Lookup(StringName key, StringName pluralKey, int count)
    {
        if (!TranslationServer.PseudolocalizationEnabled)
        {
            if (pluralKey == null)
                return TranslationServer.Translate(key);
            return TranslationServer.TranslatePlural(key, pluralKey, count);
        }
        // Pseudo mode: read the un-pseudolocalized template (locale, then fallback).
        string fallback = ProjectSettings.GetSetting("internationalization/locale/fallback", "en").AsString();
        foreach (string locale in new[] { TranslationServer.GetLocale(), fallback })
        {
            Translation catalog = LocaleRegistry.Catalog(locale);
            if (catalog == null)
                continue;
            string text = pluralKey == null ? catalog.GetMessage(key) : catalog.GetPluralMessage(key, pluralKey, count);
            if (!string.IsNullOrEmpty(text))
                return text;
        }
        return key;
    }

    private static string Finish(string text) =>
        TranslationServer.PseudolocalizationEnabled ? TranslationServer.Pseudolocalize(text) : text;
}

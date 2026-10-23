using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace Utopia;

/// <summary>
/// Locale-aware number formatting for player-facing text.
///
/// Godot does not expose CLDR number formatting, so grouping separators come
/// from a small table here and digits are shaped by TextServer (native digits
/// for scripts such as Arabic). Extend Grouping when adding a locale.
/// Never use these strings for saves, ids, or game rules.
/// </summary>
public static class LocaleFormat
{
    public static readonly IReadOnlyDictionary<string, string> Grouping = new Dictionary<string, string>
    {
        ["en"] = ",",
        ["de"] = ".",
        ["fr"] = " ",
        ["ar"] = "٬",
    };

    public static readonly IReadOnlyDictionary<string, string> DecimalSeparator = new Dictionary<string, string>
    {
        ["en"] = ".",
        ["de"] = ",",
        ["fr"] = ",",
        ["ar"] = "٫",
    };

    public static string CurrentLanguage() => TranslationServer.GetLocale().Split('_')[0];

    public static string Integer(long value, string locale = "")
    {
        string language = locale.Length > 0 ? locale : CurrentLanguage();
        string digits = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
        string separator = Grouping.TryGetValue(language, out string found) ? found : Grouping["en"];
        string grouped = "";
        int count = 0;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            if (count > 0 && count % 3 == 0)
                grouped = separator + grouped;
            grouped = digits[i] + grouped;
            count++;
        }
        if (value < 0)
            grouped = "-" + grouped;
        return TranslationServer.FormatNumber(grouped, language);
    }

    public static string Decimal(double value, int places, string locale = "")
    {
        string language = locale.Length > 0 ? locale : CurrentLanguage();
        string fixedText = Math.Abs(value).ToString("F" + places, CultureInfo.InvariantCulture);
        string[] parts = fixedText.Split('.');
        string text = Integer(long.Parse(parts[0], CultureInfo.InvariantCulture), language).TrimStart('-');
        if (places > 0)
        {
            string separator = DecimalSeparator.TryGetValue(language, out string found) ? found : DecimalSeparator["en"];
            text += separator + TranslationServer.FormatNumber(parts[1], language);
        }
        return (value < 0.0 ? "-" : "") + text;
    }

    /// <summary>"75%" (en) vs "75 %" (de). Percent spacing per CLDR.</summary>
    public static string Percent(double ratio, string locale = "")
    {
        string language = locale.Length > 0 ? locale : CurrentLanguage();
        string number = Integer((long)Math.Round(ratio * 100.0, MidpointRounding.AwayFromZero), language);
        return number + (language is "de" or "fr" ? " %" : "%");
    }
}

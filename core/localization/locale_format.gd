class_name LocaleFormat
extends RefCounted
## Locale-aware number formatting for player-facing text.
##
## Godot does not expose CLDR number formatting, so grouping separators come
## from a small table here and digits are shaped by TextServer (native digits
## for scripts such as Arabic). Extend GROUPING when adding a locale.
## Never use these strings for saves, ids, or game rules.

const GROUPING: Dictionary[String, String] = {
	"en": ",",
	"de": ".",
	"fr": " ",
	"ar": "٬",
}
const DECIMAL: Dictionary[String, String] = {
	"en": ".",
	"de": ",",
	"fr": ",",
	"ar": "٫",
}


static func current_language() -> String:
	return TranslationServer.get_locale().get_slice("_", 0)


static func integer(value: int, locale: String = "") -> String:
	var language: String = locale if not locale.is_empty() else current_language()
	var digits: String = str(absi(value))
	var separator: String = GROUPING.get(language, GROUPING["en"])
	var grouped: String = ""
	var count: int = 0
	for i: int in range(digits.length() - 1, -1, -1):
		if count > 0 and count % 3 == 0:
			grouped = separator + grouped
		grouped = digits[i] + grouped
		count += 1
	if value < 0:
		grouped = "-" + grouped
	return TextServerManager.get_primary_interface().format_number(grouped, language)


static func decimal(value: float, places: int, locale: String = "") -> String:
	var language: String = locale if not locale.is_empty() else current_language()
	var whole: int = int(absf(value))
	var fraction: String = ("%.*f" % [places, absf(value)]).get_slice(".", 1)
	var text: String = integer(whole, language).trim_prefix("-")
	if places > 0:
		text += DECIMAL.get(language, DECIMAL["en"]) + TextServerManager.get_primary_interface().format_number(fraction, language)
	return ("-" if value < 0.0 else "") + text


## "75%" (en) vs "75 %" (de). Percent spacing per CLDR.
static func percent(ratio: float, locale: String = "") -> String:
	var language: String = locale if not locale.is_empty() else current_language()
	var number: String = integer(roundi(ratio * 100.0), language)
	return number + (" %" if language == "de" or language == "fr" else "%")

class_name LocaleRegistry
extends RefCounted
## Supported player-selectable locales and their properties.
## Adding a language: see docs/LOCALIZATION.md ("Adding a language").

const SOURCE_LOCALE: String = "en"
const SUPPORTED: PackedStringArray = ["en", "de"]
## Locales whose UI must lay out right-to-left. None ship yet; the list exists
## so RTL support is data-driven when Arabic/Hebrew are added.
const RTL_LOCALES: PackedStringArray = ["ar", "he", "fa", "ur"]


static func is_supported(locale: String) -> bool:
	return SUPPORTED.has(locale)


static func is_rtl(locale: String) -> bool:
	return RTL_LOCALES.has(TranslationServer.standardize_locale(locale).get_slice("_", 0))


## Picks the best supported locale for an OS locale such as "de_AT".
static func best_match(os_locale: String) -> String:
	var language: String = TranslationServer.standardize_locale(os_locale).get_slice("_", 0)
	return language if is_supported(language) else SOURCE_LOCALE


## The language's own name ("Deutsch"), read from that language's catalog so
## the picker always shows endonyms regardless of the current UI language.
static func endonym(locale: String) -> String:
	var translation: Translation = TranslationServer.get_translation_object(locale)
	if translation != null and translation.locale.begins_with(locale):
		var name: StringName = translation.get_message(&"LANGUAGE_ENDONYM")
		if not String(name).is_empty():
			return String(name)
	return TranslationServer.get_locale_name(locale)

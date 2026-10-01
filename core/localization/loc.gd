class_name Loc
extends RefCounted
## Formatting helpers for translated text with named {placeholders}.
##
## Always use these instead of `tr(key).format(...)`: Godot's
## pseudolocalizer only protects %-style placeholders, so it would mangle
## {names} before they are filled, and tr_n() skips pseudolocalization.
## Here placeholders are filled first, then the finished string is
## pseudolocalized, so pseudo builds exercise the real final text.


## Translates [param key] and fills its {placeholders} from [param args].
static func format(key: StringName, args: Dictionary = {}) -> String:
	return _finish(_lookup(key, &"", 1).format(args))


## Plural-aware variant. Catalog entries use msgid KEY / msgid_plural KEY_PLURAL.
## [param count] picks the plural form and is available as {count}.
static func format_plural(key: StringName, count: int, args: Dictionary = {}) -> String:
	var all_args: Dictionary = args.duplicate()
	if not all_args.has("count"):
		all_args["count"] = LocaleFormat.integer(count)
	return _finish(_lookup(key, StringName(String(key) + "_PLURAL"), count).format(all_args))


## The translated template without placeholders filled or pseudolocalization
## applied. Use only to build pieces that are passed into another format() call.
static func template(key: StringName) -> String:
	return _lookup(key, &"", 1)


static func _lookup(key: StringName, plural_key: StringName, count: int) -> String:
	if not TranslationServer.pseudolocalization_enabled:
		if String(plural_key).is_empty():
			return TranslationServer.translate(key)
		return TranslationServer.translate_plural(key, plural_key, count)
	# Pseudo mode: read the un-pseudolocalized template (locale, then fallback).
	for locale: String in [TranslationServer.get_locale(), str(ProjectSettings.get_setting("internationalization/locale/fallback", "en"))]:
		var catalog: Translation = TranslationServer.get_translation_object(locale)
		if catalog == null:
			continue
		var text: String = catalog.get_message(key) if String(plural_key).is_empty() else catalog.get_plural_message(key, plural_key, count)
		if not text.is_empty():
			return text
	return String(key)


static func _finish(text: String) -> String:
	return TranslationServer.pseudolocalize(text) if TranslationServer.pseudolocalization_enabled else text

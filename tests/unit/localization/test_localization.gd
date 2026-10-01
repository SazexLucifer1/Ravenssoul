extends TestCase
## Catalog validation: missing/unused keys, placeholder parity, hard-coded
## text, fallback, pseudolocalization, plurals, and number formatting.

const CATALOG_DIR: String = "res://assets/localization"
const SOURCE_DIRS: PackedStringArray = ["res://autoload", "res://core", "res://features", "res://scenes"]
const KEY_PATTERN: String = "\"([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+)\""
const PLACEHOLDER_PATTERN: String = "\\{[a-z_]+\\}"
## Keys built at runtime from stable ids (e.g. "RESOURCE_" + id). Documented in docs/LOCALIZATION.md.
const DYNAMIC_PREFIXES: PackedStringArray = ["RESOURCE_"]


func _catalog(locale: String) -> Translation:
	return load("%s/%s.po" % [CATALOG_DIR, locale]) as Translation


func _keys(locale: String) -> PackedStringArray:
	var keys := PackedStringArray()
	for key: StringName in _catalog(locale).get_message_list():
		if not String(key).is_empty():
			keys.append(String(key))
	keys.sort()
	return keys


func _source_files() -> PackedStringArray:
	var files := PackedStringArray()
	for dir: String in SOURCE_DIRS:
		_collect(dir, files)
	return files


func _collect(dir: String, out: PackedStringArray) -> void:
	for sub: String in DirAccess.get_directories_at(dir):
		_collect(dir.path_join(sub), out)
	for file: String in DirAccess.get_files_at(dir):
		if file.get_extension() in ["gd", "tscn", "tres"]:
			out.append(dir.path_join(file))


func test_every_supported_locale_has_a_catalog() -> void:
	for locale: String in LocaleRegistry.SUPPORTED:
		assert_not_null(_catalog(locale), locale)
		assert_contains(TranslationServer.get_loaded_locales(), locale)


func test_launch_locales_have_identical_key_sets() -> void:
	var source: PackedStringArray = _keys(LocaleRegistry.SOURCE_LOCALE)
	for locale: String in LocaleRegistry.SUPPORTED:
		var keys: PackedStringArray = _keys(locale)
		for key: String in source:
			assert_true(keys.has(key), "%s is missing %s" % [locale, key])
		for key: String in keys:
			assert_true(source.has(key), "%s has key %s that is not in the source catalog" % [locale, key])


func test_placeholders_match_source() -> void:
	var regex := RegEx.create_from_string(PLACEHOLDER_PATTERN)
	var source: Translation = _catalog(LocaleRegistry.SOURCE_LOCALE)
	for locale: String in LocaleRegistry.SUPPORTED:
		var catalog: Translation = _catalog(locale)
		var plurals: PackedStringArray = _plural_keys(LocaleRegistry.SOURCE_LOCALE)
		for key: String in _keys(LocaleRegistry.SOURCE_LOCALE):
			if not plurals.has(key):
				assert_eq(_placeholders(regex, catalog.get_message(key)), _placeholders(regex, source.get_message(key)), "%s %s" % [locale, key])
				continue
			for n: int in [1, 2]:
				var expected: Array = _placeholders(regex, source.get_plural_message(key, key + "_PLURAL", n))
				var actual: Array = _placeholders(regex, catalog.get_plural_message(key, key + "_PLURAL", n))
				assert_eq(actual, expected, "%s %s (n=%d)" % [locale, key, n])


func test_plural_keys_follow_naming_rule() -> void:
	var regex := RegEx.create_from_string("msgid \"(\\w+)\"\\nmsgid_plural \"(\\w+)\"")
	for locale: String in LocaleRegistry.SUPPORTED:
		var text: String = FileAccess.get_file_as_string("%s/%s.po" % [CATALOG_DIR, locale])
		for m: RegExMatch in regex.search_all(text):
			assert_eq(m.get_string(2), m.get_string(1) + "_PLURAL", "%s plural id" % locale)


func _plural_keys(locale: String) -> PackedStringArray:
	var regex := RegEx.create_from_string("msgid \"(\\w+)\"\\nmsgid_plural")
	var text: String = FileAccess.get_file_as_string("%s/%s.po" % [CATALOG_DIR, locale])
	var keys := PackedStringArray()
	for m: RegExMatch in regex.search_all(text):
		keys.append(m.get_string(1))
	return keys


func _placeholders(regex: RegEx, text: String) -> Array:
	var found: Array = regex.search_all(text).map(func(m: RegExMatch) -> String: return m.get_string())
	found.sort()
	return found


func test_translations_are_not_stale() -> void:
	# Every non-source entry carries "#. English: <source text>". When the
	# English changes, this fails until a translator reviews the entry and
	# updates the reference line.
	var regex := RegEx.create_from_string("#\\. English: (.*)\\nmsgid \"(\\w+)\"")
	var source: Translation = _catalog(LocaleRegistry.SOURCE_LOCALE)
	for locale: String in LocaleRegistry.SUPPORTED:
		if locale == LocaleRegistry.SOURCE_LOCALE:
			continue
		var text: String = FileAccess.get_file_as_string("%s/%s.po" % [CATALOG_DIR, locale])
		var seen: Dictionary = {}
		for m: RegExMatch in regex.search_all(text):
			seen[m.get_string(2)] = true
			var english: String = source.get_plural_message(m.get_string(2), m.get_string(2) + "_PLURAL", 1) \
				if _plural_keys(LocaleRegistry.SOURCE_LOCALE).has(m.get_string(2)) else String(source.get_message(m.get_string(2)))
			assert_eq(m.get_string(1), english, "%s %s needs review" % [locale, m.get_string(2)])
		for key: String in _keys(locale):
			assert_true(seen.has(key), "%s %s has no '#. English:' reference" % [locale, key])


func test_all_referenced_keys_exist() -> void:
	var regex := RegEx.create_from_string(KEY_PATTERN)
	var keys: PackedStringArray = _keys(LocaleRegistry.SOURCE_LOCALE)
	for path: String in _source_files():
		var text: String = FileAccess.get_file_as_string(path)
		for match: RegExMatch in regex.search_all(text):
			var key: String = match.get_string(1)
			if key.ends_with("_PLURAL"):
				key = key.trim_suffix("_PLURAL")
			assert_true(keys.has(key), "%s references missing key %s" % [path, key])


func test_no_unused_keys() -> void:
	var corpus: String = ""
	for path: String in _source_files():
		corpus += FileAccess.get_file_as_string(path)
	for key: String in _keys(LocaleRegistry.SOURCE_LOCALE):
		var dynamic: bool = false
		for prefix: String in DYNAMIC_PREFIXES:
			dynamic = dynamic or key.begins_with(prefix)
		if not dynamic:
			assert_true(corpus.contains("\"%s\"" % key), "unused key %s" % key)


func test_dynamic_resource_keys_exist() -> void:
	var keys: PackedStringArray = _keys(LocaleRegistry.SOURCE_LOCALE)
	for id: StringName in ResourceWallet.ALL:
		assert_true(keys.has("RESOURCE_%s" % String(id).to_upper()), String(id))


func test_named_placeholders_are_filled_through_loc() -> void:
	# tr(key).format(...) breaks under pseudolocalization; see core/localization/loc.gd.
	var regex := RegEx.create_from_string("\\btr(_n)?\\([^\\n]*\\)\\.format\\(")
	for path: String in _source_files():
		if not path.ends_with(".gd"):
			continue
		for line: String in FileAccess.get_file_as_string(path).split("\n"):
			if not line.strip_edges().begins_with("#"):
				assert_null(regex.search(line), "%s uses tr().format(); use Loc.format(): %s" % [path, line.strip_edges()])


func test_scenes_contain_no_hard_coded_display_text() -> void:
	var line_regex := RegEx.create_from_string("^(text|tooltip_text|placeholder_text) = \"(.*)\"$")
	var key_regex := RegEx.create_from_string("^[A-Z][A-Z0-9_]*$")
	for path: String in _source_files():
		if not path.ends_with(".tscn"):
			continue
		for line: String in FileAccess.get_file_as_string(path).split("\n"):
			var m: RegExMatch = line_regex.search(line)
			if m != null and not m.get_string(2).is_empty():
				assert_true(key_regex.search(m.get_string(2)) != null, "%s: hard-coded text '%s'" % [path, m.get_string(2)])


func test_player_text_has_no_developer_artifacts() -> void:
	for locale: String in LocaleRegistry.SUPPORTED:
		var catalog: Translation = _catalog(locale)
		for key: String in _keys(locale):
			var text: String = catalog.get_message(key)
			for forbidden: String in ["TODO", "FIXME", "res://", "user://", "%s", "%d", "null", "Error", "Exception"]:
				assert_false(text.contains(forbidden), "%s %s contains '%s'" % [locale, key, forbidden])
			assert_false(text.strip_edges().is_empty() and key != "LIST_SEPARATOR", "%s %s is empty" % [locale, key])


func test_missing_translation_falls_back_to_source_locale() -> void:
	var partial := Translation.new()
	partial.locale = "fr"
	partial.add_message(&"UI_OK", "D'accord")
	TranslationServer.add_translation(partial)
	TranslationServer.set_locale("fr")
	assert_eq(tr(&"UI_OK"), "D'accord")
	assert_eq(tr(&"UI_CANCEL"), "Cancel", "missing key uses the fallback locale")
	TranslationServer.remove_translation(partial)


func test_runtime_language_switch() -> void:
	Settings.settings_path = "user://test_settings_locale.cfg"
	Settings.set_locale("de")
	assert_eq(tr(&"MAIN_MENU_QUIT"), "Spiel beenden")
	Settings.set_locale("en")
	assert_eq(tr(&"MAIN_MENU_QUIT"), "Quit Game")
	Settings.set_locale("xx")
	assert_eq(Settings.locale, "en", "unsupported locales are ignored")
	DirAccess.remove_absolute("user://test_settings_locale.cfg")


func test_os_locale_matching() -> void:
	assert_eq(LocaleRegistry.best_match("de_AT"), "de")
	assert_eq(LocaleRegistry.best_match("ja_JP"), "en")


func test_endonyms_come_from_each_catalog() -> void:
	assert_eq(LocaleRegistry.endonym("de"), "Deutsch")
	assert_eq(LocaleRegistry.endonym("en"), "English")


func test_plural_forms() -> void:
	assert_eq(tr_n(&"HUD_HINT_HAZARD", &"HUD_HINT_HAZARD_PLURAL", 1).format({"amount": 1}), "Hazard! You lost 1 health point.")
	assert_eq(tr_n(&"HUD_HINT_HAZARD", &"HUD_HINT_HAZARD_PLURAL", 4).format({"amount": 4}), "Hazard! You lost 4 health points.")
	TranslationServer.set_locale("de")
	assert_true(tr_n(&"HUD_HINT_HAZARD", &"HUD_HINT_HAZARD_PLURAL", 4).ends_with("Lebenspunkte verloren."))


func test_pseudolocalization_expands_and_marks_text() -> void:
	var plain: String = tr(&"MAIN_MENU_NEW_GAME")
	TranslationServer.pseudolocalization_enabled = true
	TranslationServer.reload_pseudolocalization()
	var pseudo: String = tr(&"MAIN_MENU_NEW_GAME")
	TranslationServer.pseudolocalization_enabled = false
	assert_true(pseudo.begins_with("[") and pseudo.ends_with("]"), pseudo)
	assert_true(pseudo.length() >= int(plain.length() * 1.3), "expected >=30%% expansion: %s" % pseudo)


func test_pseudolocalization_keeps_named_placeholders_working() -> void:
	TranslationServer.pseudolocalization_enabled = true
	TranslationServer.reload_pseudolocalization()
	var version: String = Loc.format(&"MAIN_MENU_VERSION", {"version": "0.1.0"})
	var hazard: String = Loc.format_plural(&"HUD_HINT_HAZARD", 4, {"amount": "4"})
	TranslationServer.pseudolocalization_enabled = false
	assert_true(version.contains("0.1.0") and not version.contains("{"), version)
	assert_true(hazard.contains("4") and not hazard.contains("{") and hazard.begins_with("["), hazard)


func test_loc_format_matches_engine_translation() -> void:
	assert_eq(Loc.format(&"MAIN_MENU_VERSION", {"version": "1.2"}), "Version 1.2")
	assert_eq(Loc.format_plural(&"HUD_HINT_HAZARD", 1, {"amount": "1"}), "Hazard! You lost 1 health point.")
	TranslationServer.set_locale("de")
	assert_eq(Loc.format_plural(&"HUD_HINT_HAZARD", 2, {"amount": "2"}), "Gefahr! Du hast 2 Lebenspunkte verloren.")


func test_rtl_locale_detection() -> void:
	assert_true(LocaleRegistry.is_rtl("ar"))
	assert_true(LocaleRegistry.is_rtl("he_IL"))
	assert_false(LocaleRegistry.is_rtl("de"))


func test_number_formatting_per_locale() -> void:
	assert_eq(LocaleFormat.integer(1234567, "en"), "1,234,567")
	assert_eq(LocaleFormat.integer(1234567, "de"), "1.234.567")
	assert_eq(LocaleFormat.integer(-950, "en"), "-950")
	assert_eq(LocaleFormat.decimal(1234.5, 1, "de"), "1.234,5")
	assert_eq(LocaleFormat.percent(0.75, "en"), "75%")
	assert_eq(LocaleFormat.percent(0.75, "de"), "75 %")
	assert_eq(LocaleFormat.integer(42, "ar"), "٤٢", "native digits via TextServer")


func test_default_font_covers_launch_locales() -> void:
	var font: Font = ThemeDB.fallback_font
	for locale: String in LocaleRegistry.SUPPORTED:
		var catalog: Translation = _catalog(locale)
		for key: String in _keys(locale):
			var text: String = catalog.get_message(key)
			for i: int in text.length():
				var code: int = text.unicode_at(i)
				if code > 32:
					assert_true(font.has_char(code), "%s %s: glyph U+%04X missing" % [locale, key, code])

extends Node
## Settings (Autoload) — player preferences that apply to every screen.
##
## Why global: language, reduced motion, and text scale must be applied before
## the first screen is shown and must survive scene changes. Persisted in
## user://settings.cfg (separate from save games so preferences survive a
## deleted save).

signal locale_changed(locale: String)
signal reduced_motion_changed(enabled: bool)
signal text_scale_changed(scale: float)

const SETTINGS_PATH: String = "user://settings.cfg"
const TEXT_SCALES: PackedFloat32Array = [1.0, 1.25, 1.5]

var locale: String = LocaleRegistry.SOURCE_LOCALE
var reduced_motion: bool = false
var text_scale: float = 1.0

## Tests point this at a throwaway file.
var settings_path: String = SETTINGS_PATH


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	load_settings()


func load_settings() -> void:
	var config := ConfigFile.new()
	var err: Error = config.load(settings_path)
	if err != OK and err != ERR_FILE_NOT_FOUND:
		DevLog.warn("settings", "Could not read settings (%s); using defaults" % error_string(err))
	var default_locale: String = LocaleRegistry.best_match(OS.get_locale())
	locale = str(config.get_value("general", "locale", default_locale))
	if not LocaleRegistry.is_supported(locale):
		locale = LocaleRegistry.SOURCE_LOCALE
	reduced_motion = bool(config.get_value("accessibility", "reduced_motion", false))
	text_scale = _sanitize_scale(float(config.get_value("accessibility", "text_scale", 1.0)))
	_apply_all()


func save_settings() -> Error:
	var config := ConfigFile.new()
	config.set_value("general", "locale", locale)
	config.set_value("accessibility", "reduced_motion", reduced_motion)
	config.set_value("accessibility", "text_scale", text_scale)
	var err: Error = config.save(settings_path)
	if err != OK:
		DevLog.error("settings", "Could not write settings: %s" % error_string(err))
	return err


func set_locale(value: String) -> void:
	if not LocaleRegistry.is_supported(value) or value == locale:
		return
	locale = value
	_apply_locale()
	locale_changed.emit(locale)
	save_settings()


func set_reduced_motion(value: bool) -> void:
	if value == reduced_motion:
		return
	reduced_motion = value
	reduced_motion_changed.emit(value)
	save_settings()


func set_text_scale(value: float) -> void:
	var sanitized: float = _sanitize_scale(value)
	if is_equal_approx(sanitized, text_scale):
		return
	text_scale = sanitized
	ThemeScaler.apply(ThemeDB.get_project_theme(), text_scale)
	text_scale_changed.emit(text_scale)
	save_settings()


func _apply_all() -> void:
	_apply_locale()
	ThemeScaler.apply(ThemeDB.get_project_theme(), text_scale)


func _apply_locale() -> void:
	TranslationServer.set_locale(locale)
	# Controls default to "inherited", so the whole UI mirrors automatically
	# when the application locale is right-to-left.
	get_tree().root.set_layout_direction(Window.LAYOUT_DIRECTION_APPLICATION_LOCALE)


func _sanitize_scale(value: float) -> float:
	var best: float = TEXT_SCALES[0]
	for candidate: float in TEXT_SCALES:
		if absf(candidate - value) < absf(best - value):
			best = candidate
	return best

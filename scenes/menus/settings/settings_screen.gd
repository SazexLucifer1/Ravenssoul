class_name SettingsScreen
extends UiScreen
## Player preferences: language, text size, reduced motion.
## Reads and writes the Settings autoload; changes apply immediately.

@export var language_option: OptionButton
@export var text_size_option: OptionButton
@export var reduced_motion_toggle: CheckButton
@export var back_button: GameButton

const TEXT_SIZE_KEYS: PackedStringArray = ["SETTINGS_TEXT_SIZE_DEFAULT", "SETTINGS_TEXT_SIZE_LARGE", "SETTINGS_TEXT_SIZE_LARGEST"]


func _ready() -> void:
	_populate()
	language_option.item_selected.connect(_on_language_selected)
	text_size_option.item_selected.connect(_on_text_size_selected)
	reduced_motion_toggle.toggled.connect(Settings.set_reduced_motion)
	back_button.activated.connect(close_requested.emit)
	FocusChain.link_vertical([language_option, text_size_option, reduced_motion_toggle, back_button] as Array[Control])
	default_focus = language_option


func _populate() -> void:
	language_option.clear()
	for locale: String in LocaleRegistry.SUPPORTED:
		language_option.add_item(LocaleRegistry.endonym(locale))
		language_option.set_item_metadata(language_option.item_count - 1, locale)
		if locale == Settings.locale:
			language_option.select(language_option.item_count - 1)
	# Endonyms must not be translated into the current UI language.
	language_option.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED

	text_size_option.clear()
	for i: int in Settings.TEXT_SCALES.size():
		text_size_option.add_item(TEXT_SIZE_KEYS[i])
		if is_equal_approx(Settings.TEXT_SCALES[i], Settings.text_scale):
			text_size_option.select(i)
	reduced_motion_toggle.set_pressed_no_signal(Settings.reduced_motion)


func _on_language_selected(index: int) -> void:
	Settings.set_locale(str(language_option.get_item_metadata(index)))


func _on_text_size_selected(index: int) -> void:
	Settings.set_text_scale(Settings.TEXT_SCALES[index])

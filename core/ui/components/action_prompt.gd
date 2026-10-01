class_name ActionPrompt
extends HBoxContainer
## "[Esc] Pause" style hint. Shows the glyph for the active input method and
## updates live when the player switches between keyboard and gamepad.

@export var action: StringName = &"ui_cancel"
@export var text_key: String = ""

var _glyph := Label.new()
var _label := Label.new()


func _ready() -> void:
	_glyph.theme_type_variation = &"KeycapLabel"
	_glyph.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	_label.theme_type_variation = &"MutedLabel"
	_label.text = text_key
	add_child(_glyph)
	add_child(_label)
	InputMethod.method_changed.connect(_on_method_changed)
	_on_method_changed(InputMethod.current)


func glyph_text() -> String:
	return _glyph.text


func _on_method_changed(method: InputMethodService.Method) -> void:
	_glyph.text = InputGlyphs.glyph_for(action, method)
	_glyph.visible = not _glyph.text.is_empty()

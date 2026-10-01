class_name HealthBar
extends ProgressBar
## Displays a HealthComponent. Always shows numbers as text so health is never
## communicated by color alone.

var _value_label := Label.new()
var _health: HealthComponent


func _ready() -> void:
	show_percentage = false
	theme_type_variation = &"HealthBar"
	custom_minimum_size.y = maxf(custom_minimum_size.y, 28.0)
	_value_label.set_anchors_preset(Control.PRESET_FULL_RECT)
	_value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_value_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_value_label.auto_translate_mode = Node.AUTO_TRANSLATE_MODE_DISABLED
	add_child(_value_label)
	Settings.locale_changed.connect(func(_l: String) -> void: _refresh())


func bind(health: HealthComponent) -> void:
	if _health and _health.health_changed.is_connected(_on_health_changed):
		_health.health_changed.disconnect(_on_health_changed)
	_health = health
	_health.health_changed.connect(_on_health_changed)
	_refresh()


func value_text() -> String:
	return _value_label.text


func _on_health_changed(_current: int, _maximum: int, _delta: int) -> void:
	_refresh()


func _refresh() -> void:
	if _health == null:
		return
	max_value = _health.max_health
	value = _health.current_health
	_value_label.text = Loc.format(&"HUD_HEALTH_VALUE", {
		"current": LocaleFormat.integer(_health.current_health),
		"max": LocaleFormat.integer(_health.max_health),
	})

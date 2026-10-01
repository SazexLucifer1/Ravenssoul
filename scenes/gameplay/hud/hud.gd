class_name Hud
extends CanvasLayer
## Battle HUD. Displays state it is bound to; it never changes game state.

@export var unit_name_label: Label
@export var health_bar: HealthBar
@export var mission_title_label: Label
@export var objective_label: Label
@export var hint_label: Label

var _hint_tween: Tween


func bind(unit: Character, mission: MissionData) -> void:
	unit_name_label.text = unit.display_name_key
	health_bar.bind(unit.health)
	mission_title_label.text = mission.title_key
	objective_label.text = mission.objective_key
	hint_label.text = ""


## Shows a short, already-translated hint, e.g. why a move was refused.
func show_hint(text: String) -> void:
	hint_label.text = text
	hint_label.modulate.a = 1.0
	if _hint_tween:
		_hint_tween.kill()
	_hint_tween = create_tween()
	_hint_tween.tween_interval(1.6)
	_hint_tween.tween_property(hint_label, "modulate:a", 0.0, UiMotion.transition_seconds())

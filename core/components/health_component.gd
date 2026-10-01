class_name HealthComponent
extends Node
## Reusable hit-point owner for any entity (units, destructible props).
##
## Owns numbers and signals only. It never touches visuals, audio, or UI:
## listeners (animators, health bars, controllers) subscribe to its signals.

signal health_changed(current: int, maximum: int, delta: int)
signal damaged(amount: int, source_id: StringName)
signal healed(amount: int)
signal died

@export_range(1, 9999) var max_health: int = 10:
	set(value):
		max_health = maxi(1, value)
		if _current > max_health:
			_current = max_health

## -1 means "not yet set": the component starts at full health.
var _current: int = -1

var current_health: int:
	get:
		return max_health if _current < 0 else _current


func is_dead() -> bool:
	return current_health <= 0


## Returns the damage actually dealt (clamped to remaining health).
func apply_damage(amount: int, source_id: StringName = &"") -> int:
	if amount <= 0 or is_dead():
		return 0
	var before: int = current_health
	_current = maxi(0, before - amount)
	var dealt: int = before - _current
	damaged.emit(dealt, source_id)
	health_changed.emit(_current, max_health, -dealt)
	if _current == 0:
		died.emit()
	return dealt


## Returns the amount actually healed. Defeated entities cannot be healed;
## use [method revive] for that.
func heal(amount: int) -> int:
	if amount <= 0 or is_dead():
		return 0
	var before: int = current_health
	_current = mini(max_health, before + amount)
	var restored: int = _current - before
	if restored > 0:
		healed.emit(restored)
		health_changed.emit(_current, max_health, restored)
	return restored


func revive(health: int) -> void:
	set_current_health(maxi(1, health))


## Sets health directly (save loading, setup). Emits health_changed but not
## damaged/healed, because nothing in the game world caused it.
func set_current_health(value: int) -> void:
	var before: int = current_health
	_current = clampi(value, 0, max_health)
	health_changed.emit(_current, max_health, _current - before)

class_name PlayerUnitController
extends Node
## Turns directional input into grid steps for one unit. Owns no visuals and
## no UI: it asks the grid for legality and reports outcomes through signals.
##
## First-playable stand-in for card-driven movement: one step per press.
## Card/AP movement replaces try_step's caller, not the grid rules.

signal stepped(cell: Vector2i)
signal step_blocked(cell: Vector2i)
signal hazard_entered(cell: Vector2i, damage: int)
signal objective_reached(cell: Vector2i)

const DIRECTIONS: Dictionary[StringName, Vector2i] = {
	&"ui_up": Vector2i.UP,
	&"ui_down": Vector2i.DOWN,
	&"ui_left": Vector2i.LEFT,
	&"ui_right": Vector2i.RIGHT,
}

@export var grid: BattleGrid

var unit: Character
var enabled: bool = true


func _unhandled_input(event: InputEvent) -> void:
	if not enabled or unit == null:
		return
	for action: StringName in DIRECTIONS:
		if event.is_action_pressed(action, true):
			get_viewport().set_input_as_handled()
			try_step(DIRECTIONS[action])
			return


## Attempts one orthogonal step. Returns true if the unit moved.
func try_step(direction: Vector2i) -> bool:
	if not enabled or unit == null or unit.is_defeated():
		return false
	if absi(direction.x) + absi(direction.y) != 1:
		return false
	var target: Vector2i = unit.cell + direction
	if not grid.relocate(unit.cell, target):
		step_blocked.emit(target)
		return false
	unit.step_to(target, grid.cell_to_local(target))
	stepped.emit(target)
	var map: BattleMapData = grid.map_data
	if map.is_hazard(target):
		var dealt: int = unit.health.apply_damage(map.hazard_damage, &"hazard")
		hazard_entered.emit(target, dealt)
	if target == map.objective_cell and not unit.is_defeated():
		objective_reached.emit(target)
	return true

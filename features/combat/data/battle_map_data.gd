class_name BattleMapData
extends Resource
## Authored layout of one Fire-Emblem-sized battle map (12x12 to 16x16).
## Logical cells are the source of truth for positions; visuals follow them.

const MIN_SIZE: int = 12
const MAX_SIZE: int = 16

@export var id: StringName
@export var size: Vector2i = Vector2i(12, 12)
@export var blocked_cells: Array[Vector2i] = []
@export var hazard_cells: Array[Vector2i] = []
@export_range(0, 99) var hazard_damage: int = 1
@export var player_spawn: Vector2i = Vector2i.ZERO
@export var objective_cell: Vector2i = Vector2i.ZERO


func is_inside(cell: Vector2i) -> bool:
	return cell.x >= 0 and cell.y >= 0 and cell.x < size.x and cell.y < size.y


func is_walkable(cell: Vector2i) -> bool:
	return is_inside(cell) and not blocked_cells.has(cell)


func is_hazard(cell: Vector2i) -> bool:
	return hazard_cells.has(cell)


func validate() -> PackedStringArray:
	var problems := PackedStringArray()
	if size.x < MIN_SIZE or size.y < MIN_SIZE or size.x > MAX_SIZE or size.y > MAX_SIZE:
		problems.append("%s: size %s outside %d..%d" % [id, size, MIN_SIZE, MAX_SIZE])
	if not is_walkable(player_spawn):
		problems.append("%s: player_spawn is not walkable" % id)
	if not is_walkable(objective_cell):
		problems.append("%s: objective_cell is not walkable" % id)
	return problems

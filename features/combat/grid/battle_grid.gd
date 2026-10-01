class_name BattleGrid
extends Node2D
## Logical tactical grid plus its placeholder rendering.
##
## Owns cell<->world conversion and occupancy. This is the gameplay
## "collision" layer for tactics: movement legality is decided here from
## BattleMapData, not by physics bodies (see docs/COLLISION_AND_HIT_DETECTION.md).

signal occupancy_changed(cell: Vector2i)

const COLOR_FLOOR := Color("2a2e39")
const COLOR_FLOOR_ALT := Color("262a34")
const COLOR_BLOCKED := Color("0e0f13")
const COLOR_HAZARD := Color("5e2620")
const COLOR_HAZARD_LINE := Color("a8442f")
const COLOR_OBJECTIVE := Color("e0b04f")
const COLOR_LINE := Color("3a3f4c")

@export var map_data: BattleMapData:
	set(value):
		map_data = value
		_occupants.clear()
		queue_redraw()
@export_range(16, 128) var cell_size: int = 48

var _occupants: Dictionary[Vector2i, Node] = {}


func cell_to_local(cell: Vector2i) -> Vector2:
	return Vector2(cell) * cell_size + Vector2.ONE * (cell_size * 0.5)


func local_to_cell(local_position: Vector2) -> Vector2i:
	return Vector2i((local_position / cell_size).floor())


func pixel_size() -> Vector2:
	return Vector2(map_data.size * cell_size)


func occupant_at(cell: Vector2i) -> Node:
	return _occupants.get(cell)


func can_enter(cell: Vector2i) -> bool:
	return map_data != null and map_data.is_walkable(cell) and not _occupants.has(cell)


## Registers [param occupant] at [param cell]. Returns false if not enterable.
func place(occupant: Node, cell: Vector2i) -> bool:
	if not can_enter(cell):
		return false
	_occupants[cell] = occupant
	occupancy_changed.emit(cell)
	return true


## Moves an occupant one or more cells. The destination is validated; the
## caller is responsible for path legality (adjacent steps only).
func relocate(from_cell: Vector2i, to_cell: Vector2i) -> bool:
	if not _occupants.has(from_cell) or not can_enter(to_cell):
		return false
	_occupants[to_cell] = _occupants[from_cell]
	_occupants.erase(from_cell)
	occupancy_changed.emit(from_cell)
	occupancy_changed.emit(to_cell)
	return true


func remove(cell: Vector2i) -> void:
	if _occupants.erase(cell):
		occupancy_changed.emit(cell)


func _draw() -> void:
	if map_data == null:
		return
	for y: int in map_data.size.y:
		for x: int in map_data.size.x:
			var cell := Vector2i(x, y)
			var rect := Rect2(Vector2(cell * cell_size), Vector2.ONE * cell_size)
			var color: Color = COLOR_FLOOR if (x + y) % 2 == 0 else COLOR_FLOOR_ALT
			if not map_data.is_walkable(cell):
				color = COLOR_BLOCKED
			elif map_data.is_hazard(cell):
				color = COLOR_HAZARD
			draw_rect(rect, color)
			if map_data.is_walkable(cell) and map_data.is_hazard(cell):
				_draw_hatching(rect) # hazards are never communicated by color alone
			draw_rect(rect, COLOR_LINE, false, 1.0)
			if cell == map_data.objective_cell:
				draw_rect(rect.grow(-6), COLOR_OBJECTIVE, false, 3.0)
				var c: Vector2 = rect.get_center()
				var r: float = cell_size * 0.16
				draw_colored_polygon(PackedVector2Array([c + Vector2(0, -r), c + Vector2(r, 0), c + Vector2(0, r), c + Vector2(-r, 0)]), COLOR_OBJECTIVE)


func _draw_hatching(rect: Rect2) -> void:
	var step: float = cell_size / 4.0
	for i: int in range(1, 8):
		var offset: float = step * i
		var from := rect.position + Vector2(maxf(0.0, offset - rect.size.y), minf(offset, rect.size.y))
		var to := rect.position + Vector2(minf(offset, rect.size.x), maxf(0.0, offset - rect.size.x))
		draw_line(from, to, COLOR_HAZARD_LINE, 2.0)

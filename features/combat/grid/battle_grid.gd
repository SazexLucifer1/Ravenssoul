class_name BattleGrid
extends Node2D
## Logical tactical grid plus its placeholder rendering, in isometric
## projection (ADR 0010).
##
## Owns cell<->world conversion and occupancy. This is the gameplay
## "collision" layer for tactics: movement legality is decided here from
## BattleMapData, not by physics bodies (see docs/COLLISION_AND_HIT_DETECTION.md).
## All world sizes are in *art pixels* (the 640x360 pixel-art space); the
## gameplay camera zooms by an integer factor (see docs/ART_IMPLEMENTATION_GUIDE.md).

signal occupancy_changed(cell: Vector2i)

## Isometric tile footprint in art pixels (2:1 diamond).
const TILE_SIZE := Vector2i(32, 16)
## Height of one elevation step (blocked cells are drawn as one-step blocks).
const BLOCK_HEIGHT: int = 8

# Placeholder palette (warm with darker undertones). Replaced by tile art.
const COLOR_FLOOR := Color("5b5a3a")
const COLOR_FLOOR_ALT := Color("545335")
const COLOR_EDGE := Color("3f3d27")
const COLOR_BLOCK_TOP := Color("6f6253")
const COLOR_BLOCK_LEFT := Color("4a4036")
const COLOR_BLOCK_RIGHT := Color("3a3129")
const COLOR_HAZARD := Color("6a2a1c")
const COLOR_HAZARD_LINE := Color("d8742f")
const COLOR_OBJECTIVE := Color("f2c14e")
const COLOR_MOVE_FILL := Color(0.36, 0.62, 0.95, 0.35)
const COLOR_MOVE_EDGE := Color("8cc4ff")

@export var map_data: BattleMapData:
	set(value):
		map_data = value
		_occupants.clear()
		move_highlights.clear()
		queue_redraw()

## Cells the active unit can move to (drawn as blue diamonds).
var move_highlights: Array[Vector2i] = []
var _occupants: Dictionary[Vector2i, Node] = {}


## Center of a cell's diamond in grid-local art pixels.
func cell_to_local(cell: Vector2i) -> Vector2:
	return Vector2((cell.x - cell.y) * TILE_SIZE.x * 0.5, (cell.x + cell.y) * TILE_SIZE.y * 0.5)


## Inverse projection (pointer picking). Exact at cell centers.
func local_to_cell(local_position: Vector2) -> Vector2i:
	var a: float = local_position.x / (TILE_SIZE.x * 0.5)
	var b: float = local_position.y / (TILE_SIZE.y * 0.5)
	return Vector2i(roundi((a + b) * 0.5), roundi((b - a) * 0.5))


## Bounding box of all tiles (including block height) in grid-local art pixels.
func world_bounds() -> Rect2:
	if map_data == null:
		return Rect2()
	var half := Vector2(TILE_SIZE) * 0.5
	var left: float = cell_to_local(Vector2i(0, map_data.size.y - 1)).x - half.x
	var right: float = cell_to_local(Vector2i(map_data.size.x - 1, 0)).x + half.x
	var top: float = cell_to_local(Vector2i.ZERO).y - half.y - BLOCK_HEIGHT
	var bottom: float = cell_to_local(map_data.size - Vector2i.ONE).y + half.y
	return Rect2(left, top, right - left, bottom - top)


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


func set_move_highlights(cells: Array[Vector2i]) -> void:
	move_highlights = cells
	queue_redraw()


func _diamond(cell: Vector2i, lift: float = 0.0, inset: float = 0.0) -> PackedVector2Array:
	var c: Vector2 = cell_to_local(cell) - Vector2(0, lift)
	var hx: float = TILE_SIZE.x * 0.5 - inset * 2.0
	var hy: float = TILE_SIZE.y * 0.5 - inset
	return PackedVector2Array([c + Vector2(0, -hy), c + Vector2(hx, 0), c + Vector2(0, hy), c + Vector2(-hx, 0)])


func _draw() -> void:
	if map_data == null:
		return
	# Back-to-front (painter's order) so raised blocks overlap correctly.
	for depth: int in map_data.size.x + map_data.size.y - 1:
		for x: int in map_data.size.x:
			var cell := Vector2i(x, depth - x)
			if cell.y < 0 or cell.y >= map_data.size.y:
				continue
			_draw_cell(cell)


func _draw_cell(cell: Vector2i) -> void:
	var top: PackedVector2Array = _diamond(cell)
	if not map_data.is_walkable(cell):
		_draw_block(cell)
		return
	var floor_color: Color = COLOR_FLOOR if (cell.x + cell.y) % 2 == 0 else COLOR_FLOOR_ALT
	draw_colored_polygon(top, COLOR_HAZARD if map_data.is_hazard(cell) else floor_color)
	if map_data.is_hazard(cell):
		# Hazards are never communicated by color alone: ember cross-hatch.
		var inner: PackedVector2Array = _diamond(cell, 0.0, 3.0)
		draw_line(inner[0].lerp(inner[3], 0.5), inner[1].lerp(inner[2], 0.5), COLOR_HAZARD_LINE, 1.0)
		draw_line(inner[0].lerp(inner[1], 0.5), inner[3].lerp(inner[2], 0.5), COLOR_HAZARD_LINE, 1.0)
	if move_highlights.has(cell):
		draw_colored_polygon(_diamond(cell, 0.0, 1.0), COLOR_MOVE_FILL)
		draw_polyline(_closed(_diamond(cell, 0.0, 1.0)), COLOR_MOVE_EDGE, 1.0)
	draw_polyline(_closed(top), COLOR_EDGE, 1.0)
	if cell == map_data.objective_cell:
		draw_polyline(_closed(_diamond(cell, 0.0, 2.0)), COLOR_OBJECTIVE, 1.0)
		var c: Vector2 = cell_to_local(cell) - Vector2(0, 6)
		draw_colored_polygon(PackedVector2Array([c + Vector2(0, -4), c + Vector2(3, 0), c + Vector2(0, 4), c + Vector2(-3, 0)]), COLOR_OBJECTIVE)


func _draw_block(cell: Vector2i) -> void:
	var base: PackedVector2Array = _diamond(cell)
	var top: PackedVector2Array = _diamond(cell, BLOCK_HEIGHT)
	draw_colored_polygon(PackedVector2Array([top[3], top[2], base[2], base[3]]), COLOR_BLOCK_LEFT)
	draw_colored_polygon(PackedVector2Array([top[2], top[1], base[1], base[2]]), COLOR_BLOCK_RIGHT)
	draw_colored_polygon(top, COLOR_BLOCK_TOP)
	draw_polyline(_closed(top), COLOR_BLOCK_RIGHT, 1.0)


static func _closed(points: PackedVector2Array) -> PackedVector2Array:
	var out: PackedVector2Array = points.duplicate()
	out.append(points[0])
	return out

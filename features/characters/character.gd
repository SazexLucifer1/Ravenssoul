class_name Character
extends Node2D
## Reusable battlefield unit (hero + battalion, or an enemy).
##
## Composition root only: it wires its child components and exposes a small
## API. Logic lives in components (HealthComponent, GridMover,
## CharacterAnimator); decisions live in controllers outside this scene.
## Logical grid cell is the source of truth; visuals follow it.

signal cell_changed(cell: Vector2i)
signal defeated

@export var health: HealthComponent
@export var mover: GridMover
@export var animator: CharacterAnimator
@export var visual: UnitVisual

## Translation key of the unit's display name.
var display_name_key: String = ""
var stats: UnitStats
var cell: Vector2i = Vector2i.ZERO


func _ready() -> void:
	health.died.connect(_on_died)


## Applies combined stats. Call before or after adding to the tree.
func configure(p_stats: UnitStats, p_display_name_key: String) -> void:
	stats = p_stats
	display_name_key = p_display_name_key
	health.max_health = maxi(1, p_stats.max_health)
	health.set_current_health(health.max_health)


## Instantly places the unit (spawning, loading).
func place_at(p_cell: Vector2i, world_position: Vector2) -> void:
	cell = p_cell
	mover.snap_to(world_position)
	cell_changed.emit(cell)


## Updates the logical cell immediately and animates the visual toward it.
## Gameplay can query [member cell] right away; it never waits for animation.
func step_to(p_cell: Vector2i, world_position: Vector2) -> void:
	var direction: Vector2i = p_cell - cell
	cell = p_cell
	mover.move_to(world_position, direction)
	cell_changed.emit(cell)


func is_defeated() -> bool:
	return health.is_dead()


func _on_died() -> void:
	defeated.emit()

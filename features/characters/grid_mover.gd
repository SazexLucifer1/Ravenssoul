class_name GridMover
extends Node
## Moves a Node2D between grid cell positions with a short tween.
##
## Visual-only: the logical cell is already updated by the time the tween
## starts, so frame rate never changes game outcomes. Reduced motion snaps.

signal move_started(direction: Vector2i)
signal move_finished

@export var body: Node2D
@export_range(0.05, 0.5) var step_seconds: float = 0.18

var _tween: Tween


func is_moving() -> bool:
	return _tween != null and _tween.is_running()


func snap_to(world_position: Vector2) -> void:
	if _tween:
		_tween.kill()
	body.position = world_position


func move_to(world_position: Vector2, direction: Vector2i) -> void:
	if _tween:
		_tween.kill()
	move_started.emit(direction)
	if UiMotion.reduced() or not body.is_inside_tree():
		body.position = world_position
		move_finished.emit()
		return
	_tween = body.create_tween()
	_tween.tween_property(body, "position", world_position, step_seconds) \
		.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
	_tween.finished.connect(move_finished.emit)

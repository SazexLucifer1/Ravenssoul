extends Node
## SceneRouter (Autoload) — owns top-level scene changes.
##
## Why global: a scene cannot replace itself safely, and the fade overlay must
## outlive both the old and the new scene. Nothing else may call
## SceneTree.change_scene_to_*.

signal route_changing(from_route: StringName, to_route: StringName)
signal route_changed(route: StringName)
signal route_failed(route: StringName)

const ROUTE_TABLE: RouteTable = preload("res://core/routing/route_table.tres")
const FADE_SECONDS: float = 0.2

var current_route: StringName = &""
var is_transitioning: bool = false

var _overlay_layer := CanvasLayer.new()
var _fade := ColorRect.new()


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_overlay_layer.layer = 100
	_fade.color = Color.BLACK
	_fade.modulate.a = 0.0
	_fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_fade.set_anchors_preset(Control.PRESET_FULL_RECT)
	_overlay_layer.add_child(_fade)
	add_child(_overlay_layer)


func has_route(route: StringName) -> bool:
	return ROUTE_TABLE.has_route(route)


## Changes the top-level scene. Returns false if the request was rejected
## (unknown route, already transitioning, or the scene failed to load).
## Await it when the caller needs the new scene to be ready.
func goto(route: StringName) -> bool:
	if is_transitioning:
		DevLog.info("router", "Ignored '%s': transition already running" % route)
		return false
	var path: String = ROUTE_TABLE.path_for(route)
	if path.is_empty() or not ResourceLoader.exists(path):
		DevLog.error("router", "Unknown route or missing scene: '%s' -> '%s'" % [route, path])
		route_failed.emit(route)
		return false

	is_transitioning = true
	_fade.mouse_filter = Control.MOUSE_FILTER_STOP # swallow input during the change
	route_changing.emit(current_route, route)
	await _tween_fade(1.0)

	var packed := ResourceLoader.load(path) as PackedScene
	if packed == null:
		DevLog.error("router", "Failed to load scene for route '%s'" % route)
		await _finish_transition()
		route_failed.emit(route)
		return false

	get_tree().paused = false
	var err: Error = get_tree().change_scene_to_packed(packed)
	if err != OK:
		DevLog.error("router", "change_scene_to_packed failed (%s) for '%s'" % [error_string(err), route])
		await _finish_transition()
		route_failed.emit(route)
		return false
	await get_tree().scene_changed

	current_route = route
	DevLog.info("router", "Entered route '%s'" % route)
	await _finish_transition()
	route_changed.emit(route)
	return true


func _finish_transition() -> void:
	await _tween_fade(0.0)
	_fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	is_transitioning = false


func _tween_fade(target_alpha: float) -> void:
	var duration: float = 0.0 if Settings.reduced_motion else FADE_SECONDS
	if is_zero_approx(duration):
		_fade.modulate.a = target_alpha
		await get_tree().process_frame
		return
	var tween := create_tween()
	tween.tween_property(_fade, "modulate:a", target_alpha, duration)
	await tween.finished

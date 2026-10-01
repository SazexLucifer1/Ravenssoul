class_name ScreenStack
extends Control
## Narrow, scene-owned navigation service for modal screens (menus, dialogs).
##
## Each top-level scene owns its own ScreenStack. Only the top screen is
## interactive; everything beneath it (including [member underlying_root]) has
## focus and mouse input disabled. Closing a screen restores the focus that was
## active before it opened, so controller players never lose their place.

signal screen_opened(screen: UiScreen)
signal screen_closed(screen: UiScreen)
signal emptied

const TOKENS: UiTokens = preload("res://core/ui/theme/ui_tokens.tres")

## Content behind the stack (e.g. the main menu) that must be blocked while a
## screen is open.
@export var underlying_root: Control

var _entries: Array[Dictionary] = []
var _scrim: ColorRect


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	# Created here (not at member init) so an instance that never enters the
	# tree doesn't leak an orphan node.
	_scrim = ColorRect.new()
	_scrim.color = TOKENS.scrim
	_scrim.set_anchors_preset(Control.PRESET_FULL_RECT)
	_scrim.mouse_filter = Control.MOUSE_FILTER_STOP
	_scrim.visible = false
	add_child(_scrim, false, Node.INTERNAL_MODE_FRONT)


func is_empty() -> bool:
	return _entries.is_empty()


func depth() -> int:
	return _entries.size()


func top() -> UiScreen:
	return null if _entries.is_empty() else _entries.back()["screen"]


func push(screen: UiScreen, context: Dictionary = {}) -> UiScreen:
	var viewport: Viewport = get_viewport()
	var previous_focus: Control = viewport.gui_get_focus_owner() if viewport else null
	var below: Control = top() if not is_empty() else underlying_root
	if below != null:
		_set_interactive(below, false)
	_entries.append({"screen": screen, "previous_focus": previous_focus})
	screen.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(screen)
	screen.close_requested.connect(_on_close_requested.bind(screen))
	_scrim.visible = true
	screen.on_opened(context)
	screen.focus_default()
	screen.play_open()
	screen_opened.emit(screen)
	return screen


## Closes the top screen. Returns the closed screen (already queued for free).
func pop() -> UiScreen:
	if _entries.is_empty():
		return null
	var entry: Dictionary = _entries.pop_back()
	var screen: UiScreen = entry["screen"]
	_set_interactive(screen, false)
	remove_child(screen)
	screen.queue_free()

	var revealed: Control = top() if not is_empty() else underlying_root
	if revealed != null:
		_set_interactive(revealed, true)
	_scrim.visible = not is_empty()

	var previous: Control = entry["previous_focus"]
	if is_instance_valid(previous) and previous.is_visible_in_tree():
		previous.grab_focus()
	elif top() != null:
		top().focus_default()
	screen_closed.emit(screen)
	if is_empty():
		emptied.emit()
	return screen


func clear() -> void:
	while not is_empty():
		pop()


func _unhandled_input(event: InputEvent) -> void:
	if is_empty():
		return
	if event.is_action_pressed(&"ui_cancel"):
		get_viewport().set_input_as_handled()
		top().on_cancel()


func _on_close_requested(screen: UiScreen) -> void:
	if top() == screen:
		pop()


func _set_interactive(control: Control, enabled: bool) -> void:
	control.focus_behavior_recursive = (
		Control.FOCUS_BEHAVIOR_INHERITED if enabled else Control.FOCUS_BEHAVIOR_DISABLED
	)
	control.mouse_behavior_recursive = (
		Control.MOUSE_BEHAVIOR_INHERITED if enabled else Control.MOUSE_BEHAVIOR_DISABLED
	)

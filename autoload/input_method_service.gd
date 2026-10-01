class_name InputMethodService
extends Node
## InputMethod (Autoload) — tracks whether the player last used keyboard/mouse
## or a gamepad, so prompts show matching glyphs and focus indicators appear
## for controller navigation.
##
## Why global: input device is a property of the whole session, every prompt
## needs it, and it must observe input before any screen consumes it.

signal method_changed(method: Method)

enum Method { KEYBOARD_MOUSE, GAMEPAD }

const AXIS_THRESHOLD: float = 0.5
const MOUSE_MOVE_THRESHOLD: float = 4.0

var current: Method = Method.KEYBOARD_MOUSE


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS


func _input(event: InputEvent) -> void:
	handle_event(event)


## Separate from _input so tests can feed synthetic events.
func handle_event(event: InputEvent) -> void:
	var detected: Method = current
	if event is InputEventJoypadButton:
		detected = Method.GAMEPAD
	elif event is InputEventJoypadMotion:
		if absf((event as InputEventJoypadMotion).axis_value) >= AXIS_THRESHOLD:
			detected = Method.GAMEPAD
	elif event is InputEventKey or event is InputEventMouseButton:
		detected = Method.KEYBOARD_MOUSE
	elif event is InputEventMouseMotion:
		if (event as InputEventMouseMotion).relative.length() >= MOUSE_MOVE_THRESHOLD:
			detected = Method.KEYBOARD_MOUSE
	if detected != current:
		current = detected
		method_changed.emit(current)

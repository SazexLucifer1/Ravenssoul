class_name AutomationInputDriver
extends RefCounted
## Injects input through Input.parse_input_event (the real input path used by
## players: InputMap, GUI focus, _unhandled_input, InputMethod tracking).
## Every "down" is tracked as a hold and is released on "up", on expiry, or by
## release_all() (session close, scene change, shutdown).

signal hold_released(hold_id: String)

const GAMEPAD_BUTTONS: Dictionary[String, int] = {
	"a": JOY_BUTTON_A, "b": JOY_BUTTON_B, "x": JOY_BUTTON_X, "y": JOY_BUTTON_Y,
	"start": JOY_BUTTON_START, "back": JOY_BUTTON_BACK,
	"dpad_up": JOY_BUTTON_DPAD_UP, "dpad_down": JOY_BUTTON_DPAD_DOWN,
	"dpad_left": JOY_BUTTON_DPAD_LEFT, "dpad_right": JOY_BUTTON_DPAD_RIGHT,
	"left_shoulder": JOY_BUTTON_LEFT_SHOULDER, "right_shoulder": JOY_BUTTON_RIGHT_SHOULDER,
}
const GAMEPAD_AXES: Dictionary[String, int] = {
	"left_x": JOY_AXIS_LEFT_X, "left_y": JOY_AXIS_LEFT_Y,
	"right_x": JOY_AXIS_RIGHT_X, "right_y": JOY_AXIS_RIGHT_Y,
}
const MOUSE_BUTTONS: Dictionary[String, int] = {"left": MOUSE_BUTTON_LEFT, "right": MOUSE_BUTTON_RIGHT, "middle": MOUSE_BUTTON_MIDDLE}

## hold id -> {"release": InputEvent, "release_at": msec (-1 = until released), "frame": int}
var _holds: Dictionary[String, Dictionary] = {}
var _viewport: Viewport


func _init(viewport: Viewport) -> void:
	_viewport = viewport


func held() -> PackedStringArray:
	return PackedStringArray(_holds.keys())


func has_holds() -> bool:
	return not _holds.is_empty()


## Returns "" on success or a validation message.
func key(name: String, mode: String, duration_ms: int) -> String:
	var keycode: Key = OS.find_keycode_from_string(name)
	if keycode == KEY_NONE:
		return "unknown key '%s'" % name
	var down := InputEventKey.new()
	down.keycode = keycode
	down.physical_keycode = keycode
	down.pressed = true
	var up: InputEventKey = down.duplicate()
	up.pressed = false
	_apply("key:%s" % OS.get_keycode_string(keycode), down, up, mode, duration_ms)
	return ""


func action(name: String, mode: String, duration_ms: int) -> String:
	if not InputMap.has_action(StringName(name)):
		return "unknown input action '%s'" % name
	var down := InputEventAction.new()
	down.action = StringName(name)
	down.pressed = true
	var up: InputEventAction = down.duplicate()
	up.pressed = false
	_apply("action:%s" % name, down, up, mode, duration_ms)
	return ""


func gamepad_button(name: String, mode: String, duration_ms: int) -> String:
	if not GAMEPAD_BUTTONS.has(name):
		return "unknown gamepad button '%s'" % name
	var down := InputEventJoypadButton.new()
	down.device = 0
	down.button_index = GAMEPAD_BUTTONS[name] as JoyButton
	down.pressed = true
	down.pressure = 1.0
	var up: InputEventJoypadButton = down.duplicate()
	up.pressed = false
	up.pressure = 0.0
	_apply("pad:%s" % name, down, up, mode, duration_ms)
	return ""


## Axis values persist until set back to 0 (tracked as a hold while non-zero).
func gamepad_axis(name: String, value: float) -> String:
	if not GAMEPAD_AXES.has(name):
		return "unknown gamepad axis '%s'" % name
	var motion := InputEventJoypadMotion.new()
	motion.device = 0
	motion.axis = GAMEPAD_AXES[name] as JoyAxis
	motion.axis_value = value
	var neutral: InputEventJoypadMotion = motion.duplicate()
	neutral.axis_value = 0.0
	_send(motion)
	var hold_id: String = "axis:%s" % name
	if is_zero_approx(value):
		_holds.erase(hold_id)
	else:
		_holds[hold_id] = {"release": neutral, "release_at": -1, "frame": Engine.get_process_frames()}
	return ""


func mouse(mode: String, logical: Vector2, button_name: String) -> String:
	var position: Vector2 = _to_window(logical)
	if mode == "move":
		var motion := InputEventMouseMotion.new()
		motion.position = position
		motion.global_position = position
		motion.relative = Vector2(8, 0) # large enough to register as deliberate movement
		_send(motion)
		return ""
	var down := InputEventMouseButton.new()
	down.button_index = MOUSE_BUTTONS.get(button_name, MOUSE_BUTTON_LEFT) as MouseButton
	down.position = position
	down.global_position = position
	down.pressed = true
	var up: InputEventMouseButton = down.duplicate()
	up.pressed = false
	# Hover first so GUI controls see the pointer like a real cursor.
	mouse("move", logical, button_name)
	_apply("mouse:%s" % button_name, down, up, "tap" if mode == "click" else mode, 0)
	return ""


func touch(index: int, mode: String, logical: Vector2) -> String:
	var position: Vector2 = _to_window(logical)
	if mode == "drag":
		var drag := InputEventScreenDrag.new()
		drag.index = index
		drag.position = position
		_send(drag)
		return ""
	var down := InputEventScreenTouch.new()
	down.index = index
	down.position = position
	down.pressed = true
	var up: InputEventScreenTouch = down.duplicate()
	up.pressed = false
	_apply("touch:%d" % index, down, up, mode, 0)
	return ""


## Releases holds whose time is up. Called every frame by the server.
func tick() -> void:
	var now: int = Time.get_ticks_msec()
	var frame: int = Engine.get_process_frames()
	for hold_id: String in _holds.keys():
		var hold: Dictionary = _holds[hold_id]
		if hold["release_at"] >= 0 and now >= hold["release_at"] and frame > hold["frame"]:
			_release(hold_id)


func release_all() -> int:
	var count: int = _holds.size()
	for hold_id: String in _holds.keys():
		_release(hold_id)
	return count


func _apply(hold_id: String, down: InputEvent, up: InputEvent, mode: String, duration_ms: int) -> void:
	match mode:
		"up":
			if _holds.has(hold_id):
				_release(hold_id)
			else:
				_send(up)
		"down":
			_send(down)
			_holds[hold_id] = {"release": up, "release_at": Time.get_ticks_msec() + duration_ms if duration_ms > 0 else -1, "frame": Engine.get_process_frames()}
		_: # tap: press now, release on a later frame (at least one frame held)
			_send(down)
			_holds[hold_id] = {"release": up, "release_at": Time.get_ticks_msec() + duration_ms, "frame": Engine.get_process_frames()}


func _release(hold_id: String) -> void:
	var hold: Dictionary = _holds[hold_id]
	_holds.erase(hold_id)
	_send(hold["release"])
	hold_released.emit(hold_id)


func _send(event: InputEvent) -> void:
	Input.parse_input_event(event)
	Input.flush_buffered_events()


## Logical canvas coordinates (1280x720 design space) -> window coordinates.
func _to_window(logical: Vector2) -> Vector2:
	if _viewport == null:
		return logical
	return _viewport.get_final_transform() * logical

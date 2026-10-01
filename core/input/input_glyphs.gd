class_name InputGlyphs
extends RefCounted
## Turns an input action into a short, player-readable glyph label for the
## active input method (e.g. "Esc" or "B"). Text glyphs for now; swap to icon
## textures once the art pass delivers them (see docs/UI_ARCHITECTURE.md).

const JOY_BUTTON_LABELS: Dictionary[int, String] = {
	JOY_BUTTON_A: "A",
	JOY_BUTTON_B: "B",
	JOY_BUTTON_X: "X",
	JOY_BUTTON_Y: "Y",
	JOY_BUTTON_START: "Start",
	JOY_BUTTON_BACK: "Back",
	JOY_BUTTON_DPAD_UP: "D-Pad ↑",
	JOY_BUTTON_DPAD_DOWN: "D-Pad ↓",
	JOY_BUTTON_DPAD_LEFT: "D-Pad ←",
	JOY_BUTTON_DPAD_RIGHT: "D-Pad →",
	JOY_BUTTON_LEFT_SHOULDER: "LB",
	JOY_BUTTON_RIGHT_SHOULDER: "RB",
}


static func glyph_for(action: StringName, method: InputMethodService.Method) -> String:
	if not InputMap.has_action(action):
		return ""
	for event: InputEvent in InputMap.action_get_events(action):
		if method == InputMethodService.Method.GAMEPAD and event is InputEventJoypadButton:
			var button: int = (event as InputEventJoypadButton).button_index
			return JOY_BUTTON_LABELS.get(button, "Pad %d" % button)
		if method == InputMethodService.Method.KEYBOARD_MOUSE and event is InputEventKey:
			var key := event as InputEventKey
			var keycode: Key = key.keycode
			if keycode == KEY_NONE:
				keycode = DisplayServer.keyboard_get_keycode_from_physical(key.physical_keycode)
			return OS.get_keycode_string(keycode)
	return ""

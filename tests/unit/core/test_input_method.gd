extends TestCase


func test_switches_to_gamepad_and_back() -> void:
	var service := InputMethodService.new()
	add_node(service)
	var changes: Array = []
	service.method_changed.connect(func(m: InputMethodService.Method) -> void: changes.append(m))
	var pad := InputEventJoypadButton.new()
	pad.button_index = JOY_BUTTON_A
	pad.pressed = true
	service.handle_event(pad)
	assert_eq(service.current, InputMethodService.Method.GAMEPAD)
	var key := InputEventKey.new()
	key.keycode = KEY_SPACE
	key.pressed = true
	service.handle_event(key)
	assert_eq(service.current, InputMethodService.Method.KEYBOARD_MOUSE)
	assert_eq(changes.size(), 2)


func test_stick_noise_and_tiny_mouse_jitter_do_not_switch() -> void:
	var service := InputMethodService.new()
	add_node(service)
	var drift := InputEventJoypadMotion.new()
	drift.axis_value = 0.1
	service.handle_event(drift)
	assert_eq(service.current, InputMethodService.Method.KEYBOARD_MOUSE)
	var pad := InputEventJoypadButton.new()
	pad.pressed = true
	service.handle_event(pad)
	var jitter := InputEventMouseMotion.new()
	jitter.relative = Vector2(1, 0)
	service.handle_event(jitter)
	assert_eq(service.current, InputMethodService.Method.GAMEPAD)


func test_glyphs_follow_input_method() -> void:
	assert_eq(InputGlyphs.glyph_for(&"ui_cancel", InputMethodService.Method.KEYBOARD_MOUSE), "Escape")
	assert_eq(InputGlyphs.glyph_for(&"ui_cancel", InputMethodService.Method.GAMEPAD), "B")
	assert_eq(InputGlyphs.glyph_for(&"pause", InputMethodService.Method.GAMEPAD), "Start")
	assert_eq(InputGlyphs.glyph_for(&"no_such_action", InputMethodService.Method.GAMEPAD), "")


func test_menu_actions_are_reachable_from_every_input_method() -> void:
	# Godot's defaults leave ui_accept/ui_cancel without gamepad bindings; the
	# project overrides them so controller players never hit a dead end.
	for action: StringName in [&"ui_accept", &"ui_cancel", &"pause", &"ui_up", &"ui_down", &"ui_left", &"ui_right"]:
		assert_ne(InputGlyphs.glyph_for(action, InputMethodService.Method.KEYBOARD_MOUSE), "", "%s keyboard" % action)
		var has_pad: bool = InputMap.action_get_events(action).any(func(e: InputEvent) -> bool:
			return e is InputEventJoypadButton or e is InputEventJoypadMotion)
		assert_true(has_pad, "%s gamepad" % action)

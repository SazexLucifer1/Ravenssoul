extends TestCase


func _button() -> GameButton:
	var button := GameButton.new()
	button.text = "UI_OK"
	button.custom_minimum_size = Vector2(200, 52)
	return add_node(button) as GameButton


func test_idle_focus_and_disabled_states() -> void:
	var button := _button()
	assert_eq(button.get_state(), GameButton.State.IDLE)
	button.grab_focus()
	assert_eq(button.get_state(), GameButton.State.FOCUS)
	button.disabled = true
	assert_eq(button.get_state(), GameButton.State.DISABLED)


func test_selected_state_for_toggle_buttons() -> void:
	var button := _button()
	button.toggle_mode = true
	button.button_pressed = true
	assert_eq(button.get_state(), GameButton.State.SELECTED)


func test_repeated_presses_activate_once_within_guard() -> void:
	var button := _button()
	var count: Array[int] = [0]
	button.activated.connect(func() -> void: count[0] += 1)
	for i: int in 5:
		button.pressed.emit()
	assert_eq(count[0], 1)
	button.repeat_guard_seconds = 0.0
	button.pressed.emit()
	assert_eq(count[0], 2)


func test_busy_blocks_activation_and_relabels() -> void:
	var button := _button()
	button.repeat_guard_seconds = 0.0
	var count: Array[int] = [0]
	button.activated.connect(func() -> void: count[0] += 1)
	button.set_busy(true)
	assert_eq(button.get_state(), GameButton.State.BUSY)
	assert_eq(button.text, "UI_BUSY")
	button.pressed.emit()
	assert_eq(count[0], 0)
	button.set_busy(false)
	assert_eq(button.text, "UI_OK")
	assert_false(button.disabled)


func test_cooldown_disables_then_recovers() -> void:
	var button := _button()
	button.start_cooldown(0.1)
	assert_eq(button.get_state(), GameButton.State.COOLDOWN)
	assert_true(button.disabled)
	assert_true(await wait_until(func() -> bool: return button.get_state() != GameButton.State.COOLDOWN, 2.0))
	assert_false(button.disabled)


func test_error_state_is_temporary() -> void:
	var button := _button()
	button.show_error()
	assert_eq(button.get_state(), GameButton.State.ERROR)
	assert_true(await wait_until(func() -> bool: return button.get_state() != GameButton.State.ERROR, 2.0))
	assert_eq(button.modulate, Color.WHITE)

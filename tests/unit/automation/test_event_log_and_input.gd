extends TestCase


func after_each() -> void:
	Input.action_release(&"ui_left")


func test_event_sequence_capacity_and_filters() -> void:
	var log := AutomationEventLog.new(3)
	for i: int in 5:
		log.emit("a" if i % 2 == 0 else "b", {"i": i})
	assert_eq(log.last_seq, 5)
	assert_eq(log.since(0).map(func(e: Dictionary) -> int: return e["seq"]), [3, 4, 5], "ring buffer keeps the newest")
	assert_eq(log.since(3, ["a"]).size(), 1)
	assert_true(AutomationEventLog.matches(log.since(4)[0], "a", {"i": 4}))
	assert_false(AutomationEventLog.matches(log.since(4)[0], "a", {"i": 5}))


func test_log_capture_redacts_paths() -> void:
	var capture := AutomationLogCapture.new()
	var text: String = capture.redact("failed at %s/saves/x.json" % OS.get_user_data_dir())
	assert_false(text.contains(OS.get_user_data_dir()))
	assert_true(text.contains("<user_data>"))


func test_key_hold_and_release_all() -> void:
	var driver := AutomationInputDriver.new(tree.root)
	assert_eq(driver.key("Left", "down", 0), "")
	assert_true(Input.is_key_pressed(KEY_LEFT), "real Input state reflects the hold")
	assert_true(Input.is_action_pressed(&"ui_left"), "InputMap action sees the key")
	assert_eq(driver.held(), PackedStringArray(["key:Left"]))
	assert_eq(driver.release_all(), 1)
	assert_false(Input.is_key_pressed(KEY_LEFT))
	assert_false(driver.has_holds())


func test_tap_releases_on_a_later_frame() -> void:
	var driver := AutomationInputDriver.new(tree.root)
	driver.action("ui_left", "tap", 0)
	assert_true(Input.is_action_pressed(&"ui_left"))
	driver.tick()
	assert_true(Input.is_action_pressed(&"ui_left"), "held for at least one frame")
	await wait_frames(1)
	driver.tick()
	assert_false(Input.is_action_pressed(&"ui_left"))


func test_timed_hold_expires() -> void:
	var driver := AutomationInputDriver.new(tree.root)
	driver.gamepad_button("a", "down", 50)
	await wait_seconds(0.08)
	driver.tick()
	assert_false(driver.has_holds())


func test_axis_hold_returns_to_neutral() -> void:
	var driver := AutomationInputDriver.new(tree.root)
	driver.gamepad_axis("left_x", -1.0)
	assert_eq(driver.held(), PackedStringArray(["axis:left_x"]))
	assert_true(Input.get_joy_axis(0, JOY_AXIS_LEFT_X) < -0.5)
	driver.release_all()
	assert_true(is_zero_approx(Input.get_joy_axis(0, JOY_AXIS_LEFT_X)))


func test_invalid_inputs_are_reported() -> void:
	var driver := AutomationInputDriver.new(tree.root)
	assert_ne(driver.key("NotAKey", "tap", 0), "")
	assert_ne(driver.action("no_such_action", "tap", 0), "")
	assert_ne(driver.gamepad_button("turbo", "tap", 0), "")
	assert_ne(driver.gamepad_axis("z", 1.0), "")
	assert_false(driver.has_holds())

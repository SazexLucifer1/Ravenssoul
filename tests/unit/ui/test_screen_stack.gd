extends TestCase


class FakeScreen extends UiScreen:
	var button := GameButton.new()

	func _init() -> void:
		button.text = "UI_OK"
		add_child(button)
		default_focus = button


func _stack_with_root() -> Array:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	var root_button := GameButton.new()
	root_button.text = "UI_BACK"
	root.add_child(root_button)
	add_node(root)
	var stack := ScreenStack.new()
	stack.underlying_root = root
	add_node(stack)
	root_button.grab_focus()
	return [stack, root, root_button]


func test_push_focuses_screen_and_blocks_underlying() -> void:
	var parts: Array = _stack_with_root()
	var stack: ScreenStack = parts[0]
	var root: Control = parts[1]
	var screen := FakeScreen.new()
	stack.push(screen)
	assert_true(screen.button.has_focus())
	assert_eq(root.focus_behavior_recursive, Control.FOCUS_BEHAVIOR_DISABLED)
	assert_eq(root.mouse_behavior_recursive, Control.MOUSE_BEHAVIOR_DISABLED)


func test_pop_restores_previous_focus() -> void:
	var parts: Array = _stack_with_root()
	var stack: ScreenStack = parts[0]
	var root_button: GameButton = parts[2]
	stack.push(FakeScreen.new())
	stack.pop()
	assert_true(root_button.has_focus())
	assert_true(stack.is_empty())
	assert_eq((parts[1] as Control).focus_behavior_recursive, Control.FOCUS_BEHAVIOR_INHERITED)


func test_nested_modals_only_top_is_interactive() -> void:
	var parts: Array = _stack_with_root()
	var stack: ScreenStack = parts[0]
	var first := FakeScreen.new()
	var second := FakeScreen.new()
	stack.push(first)
	stack.push(second)
	assert_eq(stack.depth(), 2)
	assert_eq(first.focus_behavior_recursive, Control.FOCUS_BEHAVIOR_DISABLED)
	assert_true(second.button.has_focus())
	stack.pop()
	assert_true(first.button.has_focus(), "focus returns to the screen below")
	assert_eq(first.focus_behavior_recursive, Control.FOCUS_BEHAVIOR_INHERITED)


func test_ui_cancel_closes_top_screen_only() -> void:
	var parts: Array = _stack_with_root()
	var stack: ScreenStack = parts[0]
	stack.push(FakeScreen.new())
	stack.push(FakeScreen.new())
	press_action(&"ui_cancel")
	await wait_frames(1)
	assert_eq(stack.depth(), 1)


func test_screen_can_refuse_cancel() -> void:
	var parts: Array = _stack_with_root()
	var stack: ScreenStack = parts[0]
	var screen := FakeScreen.new()
	screen.cancel_closes = false
	stack.push(screen)
	press_action(&"ui_cancel")
	await wait_frames(1)
	assert_eq(stack.depth(), 1)


func test_emptied_signal() -> void:
	var parts: Array = _stack_with_root()
	var stack: ScreenStack = parts[0]
	var emptied: Array[int] = [0]
	stack.emptied.connect(func() -> void: emptied[0] += 1)
	stack.push(FakeScreen.new())
	stack.pop()
	stack.pop()
	assert_eq(emptied[0], 1)

extends TestCase


func test_full_viewport_has_no_insets() -> void:
	var insets: Dictionary = SafeAreaContainer.compute_insets(Vector2(1280, 720), Rect2(0, 0, 1280, 720))
	assert_eq(insets, {"left": 0, "top": 0, "right": 0, "bottom": 0})


func test_notch_and_rounded_corner_insets() -> void:
	var insets: Dictionary = SafeAreaContainer.compute_insets(Vector2(1280, 720), Rect2(44, 0, 1280 - 44 - 30, 700))
	assert_eq(insets, {"left": 44, "top": 0, "right": 30, "bottom": 20})


func test_container_applies_insets_on_top_of_base_margin() -> void:
	var container := SafeAreaContainer.new()
	container.base_margin = 10
	add_node(container)
	container.override_safe_rect = Rect2(40, 0, container.get_viewport_rect().size.x - 40, container.get_viewport_rect().size.y)
	assert_eq(container.get_theme_constant(&"margin_left"), 50)
	assert_eq(container.get_theme_constant(&"margin_right"), 10)

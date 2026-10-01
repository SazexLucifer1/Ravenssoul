extends TestCase
## Enforces the pixel-art presentation rules in docs/ART_IMPLEMENTATION_GUIDE.md.


func test_textures_default_to_nearest_filtering() -> void:
	assert_eq(int(ProjectSettings.get_setting("rendering/textures/canvas_textures/default_texture_filter")), 0)


func test_pixel_snapping_is_enabled() -> void:
	assert_true(ProjectSettings.get_setting("rendering/2d/snap/snap_2d_transforms_to_pixel"))
	assert_true(ProjectSettings.get_setting("rendering/2d/snap/snap_2d_vertices_to_pixel"))


func test_gameplay_camera_uses_an_integer_zoom_and_units_are_y_sorted() -> void:
	var gameplay: Node = (load("res://scenes/gameplay/gameplay.tscn") as PackedScene).instantiate()
	var camera: Camera2D = gameplay.get_node(^"World/Camera2D")
	assert_eq(camera.zoom, Vector2(2, 2), "640x360 art space on the 1280x720 canvas")
	assert_true((gameplay.get_node(^"World/BattleGrid/Units") as Node2D).y_sort_enabled)
	gameplay.free()


func test_logical_resolution_is_an_integer_multiple_of_the_art_frame() -> void:
	var width: int = ProjectSettings.get_setting("display/window/size/viewport_width")
	var height: int = ProjectSettings.get_setting("display/window/size/viewport_height")
	assert_eq(Vector2i(width, height), Vector2i(640, 360) * 2)

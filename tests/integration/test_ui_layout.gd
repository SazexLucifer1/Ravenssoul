extends TestCase
## Menus at supported resolutions, RTL, long (pseudolocalized) text, large
## text, and controller navigation without dead ends. Screens are laid out in
## a SubViewport of the target size so no real window is required.

const RESOLUTIONS: Array[Vector2i] = [
	Vector2i(1280, 720), Vector2i(1366, 768), Vector2i(1920, 1080),
	Vector2i(2560, 1440), Vector2i(2560, 1080), Vector2i(1280, 1024), Vector2i(1024, 768),
]
const SCREENS: PackedStringArray = [
	"res://scenes/main_menu/main_menu.tscn",
	"res://scenes/menus/pause/pause_menu.tscn",
	"res://scenes/menus/settings/settings_screen.tscn",
	"res://core/ui/screens/confirm_dialog.tscn",
	"res://scenes/menus/mission_result/mission_result_screen.tscn",
]
const MIN_TARGET: float = 44.0


func after_each() -> void:
	TranslationServer.pseudolocalization_enabled = false
	ThemeScaler.apply(ThemeDB.get_project_theme(), 1.0)


## Mirrors the project's canvas_items/expand stretch: logical height stays
## 720, logical width grows with wider aspect ratios.
func _logical_size(resolution: Vector2i) -> Vector2i:
	var scale: float = minf(resolution.x / 1280.0, resolution.y / 720.0)
	return Vector2i(roundi(resolution.x / scale), roundi(resolution.y / scale))


func _layout(scene_path: String, size: Vector2i, context: Dictionary = {}) -> Control:
	var viewport := SubViewport.new()
	viewport.size = size
	viewport.gui_disable_input = false
	add_node(viewport)
	var screen := (load(scene_path) as PackedScene).instantiate() as Control
	viewport.add_child(screen)
	screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if screen is UiScreen:
		(screen as UiScreen).on_opened(context)
	await wait_frames(3)
	return screen


func _interactive(root: Node) -> Array[Control]:
	var found: Array[Control] = []
	for node: Node in root.find_children("*", "BaseButton", true, false):
		var control := node as Control
		if control.is_visible_in_tree():
			found.append(control)
	return found


func _assert_fits(screen: Control, size: Vector2i, label: String) -> void:
	var bounds := Rect2(Vector2.ZERO, Vector2(size))
	for control: Control in _interactive(screen):
		var rect: Rect2 = control.get_global_rect()
		assert_true(bounds.encloses(rect), "%s: %s at %s outside %s" % [label, control.name, rect, bounds])
		assert_true(rect.size.y >= MIN_TARGET, "%s: %s is %.0fpx tall" % [label, control.name, rect.size.y])
	for node: Node in screen.find_children("*", "Label", true, false):
		var text_label := node as Label
		if text_label.is_visible_in_tree() and not text_label.text.is_empty():
			assert_true(bounds.encloses(text_label.get_global_rect()), "%s: label %s clipped" % [label, text_label.name])


func test_screens_fit_every_supported_resolution() -> void:
	for resolution: Vector2i in RESOLUTIONS:
		var logical: Vector2i = _logical_size(resolution)
		for path: String in SCREENS:
			var screen: Control = await _layout(path, logical, {"victory": true, "rewards": {&"gold": 50}})
			_assert_fits(screen, logical, "%s @ %s" % [path.get_file(), resolution])
			free_owned_nodes()


func test_long_pseudolocalized_text_still_fits() -> void:
	TranslationServer.pseudolocalization_enabled = true
	TranslationServer.reload_pseudolocalization()
	for path: String in SCREENS:
		var screen: Control = await _layout(path, Vector2i(1280, 720), {"victory": false})
		_assert_fits(screen, Vector2i(1280, 720), "pseudo " + path.get_file())
		free_owned_nodes()


func test_german_text_fits() -> void:
	TranslationServer.set_locale("de")
	for path: String in SCREENS:
		var screen: Control = await _layout(path, Vector2i(1280, 720), {"victory": true, "rewards": {&"gold": 50}})
		_assert_fits(screen, Vector2i(1280, 720), "de " + path.get_file())
		free_owned_nodes()


func test_largest_text_size_fits() -> void:
	ThemeScaler.apply(ThemeDB.get_project_theme(), Settings.TEXT_SCALES[Settings.TEXT_SCALES.size() - 1])
	for path: String in SCREENS:
		var screen: Control = await _layout(path, Vector2i(1280, 720), {"victory": true})
		_assert_fits(screen, Vector2i(1280, 720), "150%% text " + path.get_file())
		free_owned_nodes()


func test_rtl_layout_mirrors_main_menu() -> void:
	var screen: Control = await _layout("res://scenes/main_menu/main_menu.tscn", Vector2i(1280, 720))
	var button: Control = screen.get("new_game_button")
	var ltr_x: float = button.get_global_rect().position.x
	screen.layout_direction = Control.LAYOUT_DIRECTION_RTL
	await wait_frames(3)
	assert_true(button.is_layout_rtl())
	assert_true(button.get_global_rect().position.x > ltr_x, "menu column moves to the right edge")
	_assert_fits(screen, Vector2i(1280, 720), "rtl main menu")


func test_horizontal_focus_follows_visual_order_in_ltr_and_rtl() -> void:
	for direction: Control.LayoutDirection in [Control.LAYOUT_DIRECTION_LTR, Control.LAYOUT_DIRECTION_RTL]:
		var screen: Control = await _layout("res://core/ui/screens/confirm_dialog.tscn", Vector2i(1280, 720))
		var dialog := screen as ConfirmDialog
		screen.layout_direction = direction
		dialog.on_opened({}) # relinks focus for the current direction
		await wait_frames(2)
		var a: Control = dialog.cancel_button
		var b: Control = dialog.confirm_button
		var left: Control = a if a.get_global_rect().position.x < b.get_global_rect().position.x else b
		var right: Control = b if left == a else a
		assert_eq(left.get_node(left.focus_neighbor_right), right, "direction %d: left -> right" % direction)
		assert_eq(right.get_node(right.focus_neighbor_left), left, "direction %d: right -> left" % direction)
		free_owned_nodes()


func test_no_focus_dead_ends() -> void:
	var cases: Array = [
		["res://scenes/main_menu/main_menu.tscn", {}],
		["res://scenes/menus/pause/pause_menu.tscn", {}],
		["res://scenes/menus/settings/settings_screen.tscn", {}],
		["res://core/ui/screens/confirm_dialog.tscn", {}],
		["res://scenes/menus/mission_result/mission_result_screen.tscn", {"victory": false}],
	]
	for case: Array in cases:
		var screen: Control = await _layout(case[0], Vector2i(1280, 720), case[1])
		var controls: Array[Control] = _interactive(screen)
		assert_true(controls.size() > 0, "%s has interactive controls" % case[0])
		for control: Control in controls:
			for side: StringName in [&"focus_next", &"focus_previous"]:
				var path: NodePath = control.get(side)
				var target: Control = control.get_node_or_null(path) as Control if not path.is_empty() else null
				assert_true(target != null and target.is_visible_in_tree(),
					"%s: %s has no visible %s" % [case[0].get_file(), control.name, side])
		free_owned_nodes()


func test_every_screen_has_one_primary_action() -> void:
	for path: String in ["res://scenes/menus/pause/pause_menu.tscn", "res://scenes/menus/settings/settings_screen.tscn"]:
		var screen: Control = await _layout(path, Vector2i(1280, 720))
		var primaries: int = 0
		for control: Control in _interactive(screen):
			if control.theme_type_variation == &"PrimaryButton":
				primaries += 1
		assert_eq(primaries, 1, path.get_file())
		free_owned_nodes()

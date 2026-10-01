extends TestCase
## Definition of done: bootstrap -> main menu -> gameplay without errors.


func test_bootstrap_routes_to_main_menu() -> void:
	assert_true(await SceneRouter.goto(Routes.BOOTSTRAP))
	assert_true(await wait_until(func() -> bool:
		return SceneRouter.current_route == Routes.MAIN_MENU and not SceneRouter.is_transitioning))
	assert_eq(tree.current_scene.scene_file_path, "res://scenes/main_menu/main_menu.tscn")


func test_main_menu_new_game_enters_gameplay() -> void:
	await SceneRouter.goto(Routes.MAIN_MENU)
	var menu: Node = tree.current_scene
	(menu.get("new_game_button") as GameButton).pressed.emit()
	assert_true(await wait_until(func() -> bool:
		return SceneRouter.current_route == Routes.GAMEPLAY and not SceneRouter.is_transitioning))
	var gameplay: Node = tree.current_scene
	assert_eq(gameplay.scene_file_path, "res://scenes/gameplay/gameplay.tscn")
	var unit: Character = gameplay.get("player_unit")
	assert_not_null(unit)
	assert_eq(unit.cell, GameSession.active_mission.map.player_spawn)
	assert_eq(unit.health.max_health, GameSession.active_loadout.combined_stats().max_health)


func test_router_rejects_requests_during_a_transition() -> void:
	var changes: Array[StringName] = []
	SceneRouter.route_changing.connect(func(_from: StringName, to: StringName) -> void: changes.append(to), CONNECT_ONE_SHOT)
	SceneRouter.goto(Routes.MAIN_MENU) # deliberately not awaited
	var second: bool = await SceneRouter.goto(Routes.GAMEPLAY)
	assert_false(second, "second request is rejected while the first runs")
	await wait_until(func() -> bool: return not SceneRouter.is_transitioning)
	assert_eq(SceneRouter.current_route, Routes.MAIN_MENU)
	assert_eq(changes, [Routes.MAIN_MENU] as Array[StringName])


func test_unknown_route_fails_safely() -> void:
	DevLog.muted = true
	var failures: Array[StringName] = []
	SceneRouter.route_failed.connect(func(r: StringName) -> void: failures.append(r), CONNECT_ONE_SHOT)
	assert_false(await SceneRouter.goto(&"does_not_exist"))
	assert_eq(failures, [&"does_not_exist"] as Array[StringName])
	assert_false(SceneRouter.is_transitioning)


func test_transition_with_motion_enabled_completes() -> void:
	Settings.reduced_motion = false
	assert_true(await SceneRouter.goto(Routes.MAIN_MENU))
	assert_false(SceneRouter.is_transitioning)


func test_continue_with_damaged_save_shows_player_message() -> void:
	DevLog.muted = true
	(SaveService.backend as MemorySaveBackend).slots[SaveService.DEFAULT_SLOT] = "{broken"
	await SceneRouter.goto(Routes.MAIN_MENU)
	var menu: Node = tree.current_scene
	var continue_button: GameButton = menu.get("continue_button")
	assert_true(continue_button.visible, "continue shown when a save file exists")
	assert_true(continue_button.has_focus(), "continue is the primary action")
	continue_button.pressed.emit()
	await wait_frames(2)
	var stack: ScreenStack = menu.get("screen_stack")
	assert_true(stack.top() is ConfirmDialog)
	var dialog := stack.top() as ConfirmDialog
	assert_eq(dialog.body_label.text, "SAVE_ERROR_CORRUPTED")
	assert_false(dialog.cancel_button.visible, "single-action message")
	assert_eq(SceneRouter.current_route, Routes.MAIN_MENU)


func test_failed_route_resets_busy_button_and_explains() -> void:
	DevLog.muted = true
	await SceneRouter.goto(Routes.MAIN_MENU)
	var menu: Node = tree.current_scene
	var button: GameButton = menu.get("new_game_button")
	button.set_busy(true)
	await SceneRouter.goto(&"missing_route")
	assert_false(button.is_busy())
	var dialog := (menu.get("screen_stack") as ScreenStack).top() as ConfirmDialog
	assert_not_null(dialog)
	assert_eq(dialog.body_label.text, "ROUTE_FAILED_BODY")

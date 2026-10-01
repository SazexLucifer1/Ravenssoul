extends TestCase
## Minimal mission loop: move, hazards, pause, victory + autosave, defeat.


func _enter_gameplay() -> Node:
	GameSession.start_new_campaign()
	await SceneRouter.goto(Routes.GAMEPLAY)
	return tree.current_scene


func test_reaching_objective_wins_and_autosaves() -> void:
	var gameplay: Node = await _enter_gameplay()
	var controller: PlayerUnitController = gameplay.get("controller")
	# Safe path on map_training_ground: east along row 10, then north along column 10.
	for i: int in 9:
		assert_true(controller.try_step(Vector2i.RIGHT), "east step %d" % i)
	for i: int in 9:
		assert_true(controller.try_step(Vector2i.UP), "north step %d" % i)
	var stack: ScreenStack = gameplay.get("screen_stack")
	assert_true(stack.top() is MissionResultScreen)
	var result := stack.top() as MissionResultScreen
	assert_eq(result.title_label.text, "RESULT_VICTORY_TITLE")
	assert_true(await wait_until(func() -> bool: return result.save_state == MissionResultScreen.SaveState.SAVED, 2.0))
	assert_true(SaveService.has_save())
	assert_eq(GameSession.wallet.amount(ResourceWallet.GOLD), 50)
	assert_true(result.rewards_label.text.contains("50"), result.rewards_label.text)
	assert_false(controller.enabled, "no movement after the mission ends")


func test_failed_autosave_explains_and_offers_retry() -> void:
	(SaveService.backend as MemorySaveBackend).fail_writes = true
	DevLog.muted = true
	var gameplay: Node = await _enter_gameplay()
	gameplay.call("_finish", true)
	var result := (gameplay.get("screen_stack") as ScreenStack).top() as MissionResultScreen
	assert_true(await wait_until(func() -> bool: return result.save_state == MissionResultScreen.SaveState.FAILED, 2.0))
	assert_eq(result.save_status_label.text, "SAVE_ERROR_WRITE_FAILED")
	assert_true(result.save_retry_button.visible)
	assert_false(result.continue_button.disabled, "player is never trapped")
	(SaveService.backend as MemorySaveBackend).fail_writes = false
	result.save_retry_button.pressed.emit()
	assert_true(await wait_until(func() -> bool: return result.save_state == MissionResultScreen.SaveState.SAVED, 2.0))


func test_defeat_shows_retry_and_does_not_save() -> void:
	var gameplay: Node = await _enter_gameplay()
	var unit: Character = gameplay.get("player_unit")
	unit.health.apply_damage(999)
	var result := (gameplay.get("screen_stack") as ScreenStack).top() as MissionResultScreen
	assert_not_null(result)
	assert_eq(result.title_label.text, "RESULT_DEFEAT_TITLE")
	assert_true(result.retry_button.visible)
	assert_true(result.retry_button.has_focus())
	await wait_frames(2)
	assert_false(SaveService.has_save())


func test_pause_menu_pauses_and_resumes() -> void:
	var gameplay: Node = await _enter_gameplay()
	var stack: ScreenStack = gameplay.get("screen_stack")
	press_action(&"pause")
	await wait_frames(1)
	assert_true(stack.top() is PauseMenu)
	assert_true(tree.paused)
	assert_true((stack.top() as PauseMenu).resume_button.has_focus())
	press_action(&"ui_cancel")
	await wait_frames(1)
	assert_true(stack.is_empty())
	assert_false(tree.paused)
	assert_true((gameplay.get("controller") as PlayerUnitController).enabled)


func test_pause_settings_and_back_restore_focus() -> void:
	var gameplay: Node = await _enter_gameplay()
	var stack: ScreenStack = gameplay.get("screen_stack")
	gameplay.call("open_pause_menu")
	var pause := stack.top() as PauseMenu
	pause.settings_button.grab_focus()
	pause.settings_button.pressed.emit()
	assert_true(stack.top() is SettingsScreen)
	press_action(&"ui_cancel")
	await wait_frames(1)
	assert_eq(stack.top(), pause)
	assert_true(pause.settings_button.has_focus(), "focus restored to the button that opened settings")


func test_abandon_requires_confirmation() -> void:
	var gameplay: Node = await _enter_gameplay()
	var stack: ScreenStack = gameplay.get("screen_stack")
	gameplay.call("open_pause_menu")
	(stack.top() as PauseMenu).abandon_button.pressed.emit()
	var dialog := stack.top() as ConfirmDialog
	assert_not_null(dialog)
	assert_true(dialog.cancel_button.has_focus(), "destructive dialogs focus the safe choice")
	dialog.confirm_button.pressed.emit()
	assert_true(await wait_until(func() -> bool:
		return SceneRouter.current_route == Routes.MAIN_MENU and not SceneRouter.is_transitioning))
	assert_false(tree.paused)


func test_blocked_move_shows_player_hint() -> void:
	var gameplay: Node = await _enter_gameplay()
	var controller: PlayerUnitController = gameplay.get("controller")
	assert_true(controller.try_step(Vector2i.LEFT)) # (1,10) -> (0,10)
	assert_false(controller.try_step(Vector2i.LEFT), "map edge")
	var hud: Hud = gameplay.get("hud")
	assert_eq(hud.hint_label.text, "You can't move there.")


func test_hazard_hint_uses_plural_rules() -> void:
	var gameplay: Node = await _enter_gameplay()
	var controller: PlayerUnitController = gameplay.get("controller")
	# (1,10) -> (5,10) -> (5,9) -> hazard (5,8)
	for i: int in 4:
		controller.try_step(Vector2i.RIGHT)
	controller.try_step(Vector2i.UP)
	controller.try_step(Vector2i.UP)
	var hud: Hud = gameplay.get("hud")
	assert_eq(hud.hint_label.text, "Hazard! You lost 4 health points.")
	var unit: Character = gameplay.get("player_unit")
	assert_eq(unit.health.current_health, unit.health.max_health - 4)

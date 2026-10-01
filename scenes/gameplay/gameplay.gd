extends Node
## Minimal battle scene: composition root for one mission.
##
## Wires grid, unit, controller, HUD, and menus together through signals.
## Rules live in features/; this script only coordinates the mission flow.

signal mission_finished(victory: bool)

@export var character_scene: PackedScene
@export var pause_menu_scene: PackedScene
@export var settings_screen_scene: PackedScene
@export var result_screen_scene: PackedScene
@export var confirm_dialog_scene: PackedScene

@export var grid: BattleGrid
@export var units_root: Node2D
@export var camera: Camera2D
@export var controller: PlayerUnitController
@export var hud: Hud
@export var screen_stack: ScreenStack

var player_unit: Character
var mission: MissionData
var _finished: bool = false
var _result_screen: MissionResultScreen


func _ready() -> void:
	if not GameSession.has_campaign:
		# Launched directly (editor "Run current scene" or --route=gameplay).
		GameSession.start_new_campaign()
	mission = GameSession.active_mission
	grid.map_data = mission.map
	camera.position = grid.position + grid.world_bounds().get_center()

	player_unit = character_scene.instantiate() as Character
	units_root.add_child(player_unit)
	var loadout: UnitLoadout = GameSession.active_loadout
	player_unit.configure(loadout.combined_stats(), loadout.hero.name_key)
	grid.place(player_unit, mission.map.player_spawn)
	player_unit.place_at(mission.map.player_spawn, grid.cell_to_local(mission.map.player_spawn))

	controller.unit = player_unit
	controller.step_blocked.connect(func(_cell: Vector2i) -> void: hud.show_hint(Loc.format(&"HUD_HINT_BLOCKED")))
	controller.hazard_entered.connect(func(_cell: Vector2i, dealt: int) -> void:
		hud.show_hint(Loc.format_plural(&"HUD_HINT_HAZARD", dealt, {"amount": LocaleFormat.integer(dealt)})))
	controller.objective_reached.connect(func(_cell: Vector2i) -> void: _finish(true))
	player_unit.defeated.connect(func() -> void: _finish(false))
	hud.bind(player_unit, mission)
	controller.stepped.connect(func(_cell: Vector2i) -> void: _refresh_move_highlights())
	_refresh_move_highlights()
	screen_stack.emptied.connect(_on_stack_emptied)


func _unhandled_input(event: InputEvent) -> void:
	if not event.is_action_pressed(&"pause") or _finished:
		return
	if screen_stack.is_empty():
		get_viewport().set_input_as_handled()
		open_pause_menu()
	elif screen_stack.top() is PauseMenu:
		# Start/Esc toggles the pause menu closed again.
		get_viewport().set_input_as_handled()
		screen_stack.pop()


func open_pause_menu() -> void:
	get_tree().paused = true
	controller.enabled = false
	grid.set_move_highlights([] as Array[Vector2i])
	var menu: PauseMenu = pause_menu_scene.instantiate()
	menu.resume_requested.connect(screen_stack.pop)
	menu.settings_requested.connect(func() -> void: screen_stack.push(settings_screen_scene.instantiate()))
	menu.abandon_requested.connect(_confirm_abandon)
	screen_stack.push(menu)


func _confirm_abandon() -> void:
	var dialog: ConfirmDialog = confirm_dialog_scene.instantiate()
	dialog.confirmed.connect(func() -> void: SceneRouter.goto(Routes.MAIN_MENU))
	screen_stack.push(dialog, {
		"title_key": "CONFIRM_ABANDON_TITLE",
		"body_key": "CONFIRM_ABANDON_BODY",
		"confirm_key": "CONFIRM_ABANDON_ACCEPT",
		"cancel_key": "CONFIRM_ABANDON_DECLINE",
		"destructive": true,
	})


func _on_stack_emptied() -> void:
	get_tree().paused = false
	controller.enabled = not _finished
	_refresh_move_highlights()


## Blue diamonds on the cells the unit can step to (readability, rubric cat. 10).
func _refresh_move_highlights() -> void:
	var cells: Array[Vector2i] = []
	if controller.enabled and not player_unit.is_defeated():
		for direction: Vector2i in PlayerUnitController.DIRECTIONS.values():
			if grid.can_enter(player_unit.cell + direction):
				cells.append(player_unit.cell + direction)
	grid.set_move_highlights(cells)


func _finish(victory: bool) -> void:
	if _finished:
		return
	_finished = true
	controller.enabled = false
	grid.set_move_highlights([] as Array[Vector2i])
	var rewards: Dictionary = GameSession.complete_mission(mission) if victory else {}
	var screen: MissionResultScreen = result_screen_scene.instantiate()
	screen.continue_requested.connect(func() -> void: SceneRouter.goto(Routes.MAIN_MENU))
	screen.retry_requested.connect(func() -> void: SceneRouter.goto(Routes.GAMEPLAY))
	screen.save_retry_requested.connect(_autosave)
	_result_screen = screen
	screen_stack.push(screen, {"victory": victory, "rewards": rewards})
	mission_finished.emit(victory)
	if victory:
		_autosave()


func _autosave() -> void:
	_result_screen.set_save_state(MissionResultScreen.SaveState.SAVING)
	# One frame so the "Saving…" state is visible before the (blocking) write.
	await get_tree().process_frame
	var result: SaveResult = SaveService.save_game(GameSession.to_save_data())
	if result.is_ok():
		_result_screen.set_save_state(MissionResultScreen.SaveState.SAVED)
	else:
		_result_screen.set_save_state(MissionResultScreen.SaveState.FAILED, result.player_message_key())

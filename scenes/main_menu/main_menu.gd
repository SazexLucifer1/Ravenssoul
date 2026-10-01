extends Control
## Title screen. One clear primary action: "Continue" when a save exists,
## otherwise "New Campaign".

@export var layout_root: Control
@export var new_game_button: GameButton
@export var continue_button: GameButton
@export var settings_button: GameButton
@export var quit_button: GameButton
@export var version_label: Label
@export var screen_stack: ScreenStack
@export var settings_screen_scene: PackedScene
@export var confirm_dialog_scene: PackedScene


func _ready() -> void:
	screen_stack.underlying_root = layout_root
	new_game_button.activated.connect(_on_new_game)
	continue_button.activated.connect(_on_continue)
	settings_button.activated.connect(open_settings)
	quit_button.activated.connect(_on_quit)
	# Never await SceneRouter.goto() here: on success this scene is freed
	# mid-coroutine. Failures come back through route_failed instead.
	SceneRouter.route_failed.connect(_on_route_failed)
	_update_version_label()
	Settings.locale_changed.connect(func(_l: String) -> void: _update_version_label())
	refresh()


func refresh() -> void:
	var has_save: bool = SaveService.has_save()
	continue_button.visible = has_save
	continue_button.theme_type_variation = &"PrimaryButton"
	new_game_button.theme_type_variation = &"Button" if has_save else &"PrimaryButton"
	var buttons: Array[Control] = [continue_button, new_game_button, settings_button, quit_button]
	FocusChain.link_vertical(FocusChain.focusable(buttons))
	primary_button().grab_focus()


func _update_version_label() -> void:
	version_label.text = Loc.format(&"MAIN_MENU_VERSION", {"version": ProjectSettings.get_setting("application/config/version")})


func primary_button() -> GameButton:
	return continue_button if continue_button.visible else new_game_button


func open_settings() -> SettingsScreen:
	return screen_stack.push(settings_screen_scene.instantiate()) as SettingsScreen


func _on_new_game() -> void:
	if not SaveService.has_save():
		_start_new_campaign()
		return
	var dialog: ConfirmDialog = confirm_dialog_scene.instantiate()
	dialog.confirmed.connect(_start_new_campaign)
	screen_stack.push(dialog, {
		"title_key": "CONFIRM_NEW_GAME_TITLE",
		"body_key": "CONFIRM_NEW_GAME_BODY",
		"confirm_key": "CONFIRM_NEW_GAME_ACCEPT",
		"cancel_key": "UI_CANCEL",
		"destructive": true,
	})


func _start_new_campaign() -> void:
	new_game_button.set_busy(true)
	GameSession.start_new_campaign()
	SceneRouter.goto(Routes.GAMEPLAY)


func _on_continue() -> void:
	continue_button.set_busy(true)
	var result: SaveResult = SaveService.load_game()
	if result.is_ok() and GameSession.load_save_data(result.data):
		SceneRouter.goto(Routes.GAMEPLAY)
		return
	continue_button.set_busy(false)
	continue_button.show_error()
	# A readable save that references missing content is reported as damaged.
	_show_message("LOAD_FAILED_TITLE", result.player_message_key() if not result.is_ok() else "SAVE_ERROR_CORRUPTED")


func _on_route_failed(_route: StringName) -> void:
	for button: GameButton in [new_game_button, continue_button]:
		if button.is_busy():
			button.set_busy(false)
			button.show_error()
	_show_message("ROUTE_FAILED_TITLE", "ROUTE_FAILED_BODY")


func _show_message(title_key: String, body_key: String) -> void:
	var dialog: ConfirmDialog = confirm_dialog_scene.instantiate()
	screen_stack.push(dialog, {
		"title_key": title_key,
		"body_key": body_key,
		"confirm_key": "UI_OK",
		"single_action": true,
	})


func _on_quit() -> void:
	get_tree().quit()

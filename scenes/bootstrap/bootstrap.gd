extends Control
## First scene. Verifies that required services and content are available,
## then hands over to the main menu (or a dev-only --route=<id> override).

@export var status_label: Label
@export var error_panel: Control
@export var quit_button: GameButton


func _ready() -> void:
	error_panel.visible = false
	quit_button.activated.connect(func() -> void: get_tree().quit(1))
	# Let autoloads finish _ready and the loading text render once.
	await get_tree().process_frame
	# Dev/QA builds only, and only when requested on the command line.
	if not get_tree().root.has_node(^"Automation"):
		AutomationGate.start_if_requested(get_tree())
	var problems: PackedStringArray = verify_startup()
	if not problems.is_empty():
		for problem: String in problems:
			DevLog.error("bootstrap", problem)
		_show_startup_error()
		return
	# If bootstrap itself was reached through the router, let that finish first.
	while SceneRouter.is_transitioning:
		await get_tree().process_frame
	SceneRouter.goto(start_route())


## Returns developer-facing problems; empty means the game can start.
func verify_startup() -> PackedStringArray:
	var problems := PackedStringArray()
	for route: StringName in [Routes.MAIN_MENU, Routes.GAMEPLAY]:
		if not SceneRouter.has_route(route):
			problems.append("Missing route '%s'" % route)
	if not TranslationServer.get_loaded_locales().has(LocaleRegistry.SOURCE_LOCALE):
		problems.append("Source locale catalog '%s' is not loaded" % LocaleRegistry.SOURCE_LOCALE)
	var roster: RosterCatalog = GameSession.ROSTER
	if roster.default_hero == null or roster.default_battalion == null:
		problems.append("Roster catalog has no default hero/battalion")
	return problems


## Dev builds accept `-- --route=gameplay` to jump straight into a scene.
func start_route() -> StringName:
	if OS.is_debug_build():
		for arg: String in OS.get_cmdline_user_args():
			if arg.begins_with("--route="):
				var route := StringName(arg.trim_prefix("--route="))
				if SceneRouter.has_route(route) and route != Routes.BOOTSTRAP:
					return route
	return Routes.MAIN_MENU


func _show_startup_error() -> void:
	status_label.visible = false
	error_panel.visible = true
	quit_button.grab_focus()

extends Node
## Captures representative screenshots per locale for visual review/CI.
## Needs a real renderer (not --headless). On Linux CI use Xvfb:
##   xvfb-run -a godot --path . --rendering-driver opengl3 \
##     res://tests/tools/screenshot_capture.tscn -- --out=/abs/path/to/dir
## Variants: en, de, pseudo (expanded accents), rtl (pseudo + fake bidi + RTL layout).

const VARIANTS: PackedStringArray = ["en", "de", "pseudo", "rtl"]

var _out_dir: String = "user://screenshots"


func _ready() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--out="):
			_out_dir = arg.trim_prefix("--out=")
	DirAccess.make_dir_recursive_absolute(_out_dir)
	Settings.settings_path = "user://screenshot_settings.cfg"
	SaveService.backend = MemorySaveBackend.new()
	Settings.reduced_motion = true
	var placeholder := Node.new()
	get_tree().root.add_child.call_deferred(placeholder)
	await get_tree().process_frame
	get_tree().current_scene = placeholder
	for variant: String in VARIANTS:
		await _capture_variant(variant)
	await _capture_extra_states()
	get_tree().quit()


## English-only states for the visual-state matrix (docs/VISUAL_QUALITY_RUBRIC.md).
func _capture_extra_states() -> void:
	TranslationServer.set_locale("en")
	TranslationServer.pseudolocalization_enabled = false
	get_tree().root.set_layout_direction(Window.LAYOUT_DIRECTION_APPLICATION_LOCALE)
	GameSession.start_new_campaign()
	await SceneRouter.goto(Routes.GAMEPLAY)
	var gameplay: Node = get_tree().current_scene
	await _shot("state", "battle_start")
	gameplay.call("open_pause_menu")
	(gameplay.get("screen_stack") as ScreenStack).top().call("emit_signal", "abandon_requested")
	await _shot("state", "confirm_abandon")
	(gameplay.get("screen_stack") as ScreenStack).clear()
	(gameplay.get("player_unit") as Character).health.apply_damage(999)
	await _shot("state", "defeat")
	(SaveService.backend as MemorySaveBackend).slots[SaveService.DEFAULT_SLOT] = "{damaged"
	await SceneRouter.goto(Routes.MAIN_MENU)
	(get_tree().current_scene.get("continue_button") as GameButton).pressed.emit()
	await _shot("state", "load_failed")


func _capture_variant(variant: String) -> void:
	TranslationServer.set_locale("de" if variant == "de" else "en")
	var pseudo: bool = variant == "pseudo" or variant == "rtl"
	ProjectSettings.set_setting("internationalization/pseudolocalization/fake_bidi", variant == "rtl")
	TranslationServer.pseudolocalization_enabled = pseudo
	TranslationServer.reload_pseudolocalization()
	get_tree().root.set_layout_direction(
		Window.LAYOUT_DIRECTION_RTL if variant == "rtl" else Window.LAYOUT_DIRECTION_APPLICATION_LOCALE)

	await SceneRouter.goto(Routes.MAIN_MENU)
	await _shot(variant, "main_menu")
	var menu: Node = get_tree().current_scene
	menu.call("open_settings")
	await _shot(variant, "settings")

	GameSession.start_new_campaign()
	await SceneRouter.goto(Routes.GAMEPLAY)
	var gameplay: Node = get_tree().current_scene
	var controller: PlayerUnitController = gameplay.get("controller")
	for i: int in 4:
		controller.try_step(Vector2i.RIGHT)
	controller.try_step(Vector2i.UP)
	controller.try_step(Vector2i.UP)
	await _shot(variant, "gameplay")
	gameplay.call("open_pause_menu")
	await _shot(variant, "pause")
	(gameplay.get("screen_stack") as ScreenStack).clear()
	gameplay.call("_finish", true)
	await _shot(variant, "victory")


func _shot(variant: String, name: String) -> void:
	for i: int in 6:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var image: Image = get_viewport().get_texture().get_image()
	var path: String = _out_dir.path_join("%s_%s.png" % [variant, name])
	image.save_png(path)
	print("saved ", path)

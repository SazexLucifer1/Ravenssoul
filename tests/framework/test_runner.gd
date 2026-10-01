extends Node
## Discovers and runs tests under res://tests/unit and res://tests/integration.
##
##   godot --headless --path . res://tests/framework/test_runner.tscn
##   godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=save
##
## Runs as the main scene so all autoloads exist exactly as in the game.
## Exits with code 0 when every test passes, 1 otherwise.

const TEST_DIRS: PackedStringArray = ["res://tests/unit", "res://tests/integration"]

var _logger := ErrorCaptureLogger.new()


func _ready() -> void:
	OS.add_logger(_logger)
	# Tests own scene changes: give SceneRouter a disposable current scene so
	# routing never frees this runner.
	var placeholder := Node.new()
	placeholder.name = "TestPlaceholderScene"
	get_tree().root.add_child.call_deferred(placeholder)
	await get_tree().process_frame
	get_tree().current_scene = placeholder
	var exit_code: int = await _run_all(_filter())
	OS.remove_logger(_logger)
	get_tree().quit(exit_code)


func _filter() -> String:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--filter="):
			return arg.trim_prefix("--filter=")
	return ""


func _run_all(filter: String) -> int:
	var files: PackedStringArray = []
	for dir: String in TEST_DIRS:
		_collect(dir, files)
	files.sort()
	var passed: int = 0
	var failed: PackedStringArray = []
	_logger.take()
	for path: String in files:
		if not filter.is_empty() and not path.contains(filter):
			continue
		var script := load(path) as GDScript
		var load_errors: PackedStringArray = _logger.take()
		if script == null or not script.can_instantiate() or not load_errors.is_empty():
			failed.append("%s: could not load test script" % path)
			for error: String in load_errors:
				print("        - ", error)
			continue
		for method: Dictionary in script.get_script_method_list():
			var name: String = method["name"]
			if not name.begins_with("test_"):
				continue
			var test: TestCase = script.new()
			test.tree = get_tree()
			_reset_global_state()
			await test.before_each()
			await test.call(name)
			await test.after_each()
			test.free_owned_nodes()
			await get_tree().process_frame
			var errors: PackedStringArray = _logger.take()
			if errors.size() > test.allowed_engine_errors:
				for error: String in errors:
					test.fail("engine error: " + error)
			var label: String = "%s::%s" % [path.get_file().get_basename(), name]
			if test.failures.is_empty():
				passed += 1
				print("  PASS  ", label)
			else:
				failed.append(label)
				print("  FAIL  ", label)
				for failure: String in test.failures:
					print("        - ", failure)
	print("\n%d passed, %d failed" % [passed, failed.size()])
	for label: String in failed:
		print("  failed: ", label)
	return 0 if failed.is_empty() and passed > 0 else 1


## Keeps tests independent from each other and from the player's real files.
func _reset_global_state() -> void:
	DevLog.muted = false
	get_tree().paused = false
	SaveService.backend = MemorySaveBackend.new()
	Settings.settings_path = "user://test_settings.cfg"
	Settings.reduced_motion = true
	Settings.text_scale = 1.0
	Settings.locale = "en"
	TranslationServer.set_locale("en")
	TranslationServer.pseudolocalization_enabled = false
	GameSession.has_campaign = false


func _collect(dir: String, out: PackedStringArray) -> void:
	for sub: String in DirAccess.get_directories_at(dir):
		_collect(dir.path_join(sub), out)
	for file: String in DirAccess.get_files_at(dir):
		if file.begins_with("test_") and file.ends_with(".gd"):
			out.append(dir.path_join(file))

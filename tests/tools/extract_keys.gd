extends SceneTree
## Lists translation keys referenced in source/scenes/resources that are
## missing from the source catalog, printed as PO stubs ready to paste.
##   godot --headless --script res://tests/tools/extract_keys.gd
## Exit code 1 when keys are missing.

const SOURCE_DIRS: PackedStringArray = ["res://autoload", "res://core", "res://features", "res://scenes"]
const KEY_PATTERN: String = "\"([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+)\""


func _initialize() -> void:
	var catalog := load("res://assets/localization/en.po") as Translation
	var known: Dictionary = {}
	for key: StringName in catalog.get_message_list():
		known[String(key)] = true
	var regex := RegEx.create_from_string(KEY_PATTERN)
	var missing: Dictionary = {}
	var files := PackedStringArray()
	for dir: String in SOURCE_DIRS:
		_collect(dir, files)
	for path: String in files:
		for m: RegExMatch in regex.search_all(FileAccess.get_file_as_string(path)):
			var key: String = m.get_string(1).trim_suffix("_PLURAL")
			if not known.has(key):
				missing[key] = path
	for key: String in missing:
		print("#. TODO translator note (referenced in %s)\nmsgid \"%s\"\nmsgstr \"\"\n" % [missing[key], key])
	print("%d missing key(s)" % missing.size())
	quit(1 if missing.size() > 0 else 0)


func _collect(dir: String, out: PackedStringArray) -> void:
	for sub: String in DirAccess.get_directories_at(dir):
		_collect(dir.path_join(sub), out)
	for file: String in DirAccess.get_files_at(dir):
		if file.get_extension() in ["gd", "tscn", "tres"]:
			out.append(dir.path_join(file))

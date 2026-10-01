class_name JsonFileSaveBackend
extends SaveBackend
## Stores each slot as <directory>/<slot_id>.json.
##
## Writes are atomic: the new text goes to <slot>.json.tmp, the previous file
## is kept as <slot>.json.bak, then the temp file replaces the slot. A crash
## mid-write therefore never destroys the last good save.

const EXTENSION: String = ".json"

var directory: String


func _init(p_directory: String = "user://saves") -> void:
	directory = p_directory


func exists(slot_id: String) -> bool:
	return FileAccess.file_exists(_path(slot_id))


func read_text(slot_id: String) -> String:
	var file := FileAccess.open(_path(slot_id), FileAccess.READ)
	if file == null:
		last_error = FileAccess.get_open_error()
		return ""
	last_error = OK
	return file.get_as_text()


func write_text(slot_id: String, text: String) -> Error:
	var err: Error = DirAccess.make_dir_recursive_absolute(directory)
	if err != OK:
		return err
	var final_path: String = _path(slot_id)
	var temp_path: String = final_path + ".tmp"
	var file := FileAccess.open(temp_path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string(text)
	file.flush()
	err = file.get_error()
	file.close()
	if err != OK:
		DirAccess.remove_absolute(temp_path)
		return err
	if FileAccess.file_exists(final_path):
		DirAccess.copy_absolute(final_path, final_path + ".bak")
		DirAccess.remove_absolute(final_path)
	return DirAccess.rename_absolute(temp_path, final_path)


func delete(slot_id: String) -> Error:
	var path: String = _path(slot_id)
	if not FileAccess.file_exists(path):
		return ERR_FILE_NOT_FOUND
	DirAccess.remove_absolute(path + ".bak")
	return DirAccess.remove_absolute(path)


func list_slots() -> PackedStringArray:
	var slots := PackedStringArray()
	for file_name: String in DirAccess.get_files_at(directory):
		if file_name.ends_with(EXTENSION):
			slots.append(file_name.trim_suffix(EXTENSION))
	slots.sort()
	return slots


func _path(slot_id: String) -> String:
	return directory.path_join(slot_id + EXTENSION)

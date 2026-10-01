extends Node
## SaveService (Autoload) — the only code that reads or writes save games.
##
## Why global: saving is triggered from many scenes (mission results, base,
## menus), and the backend/serialization rules must be identical everywhere.
## It knows nothing about game rules: callers hand it a Dictionary payload of
## plain, language-independent data. See docs/SAVE_SYSTEM.md.

signal save_finished(slot_id: String, result: SaveResult)
signal load_finished(slot_id: String, result: SaveResult)

const SCHEMA_VERSION: int = 1
const DEFAULT_SLOT: String = "campaign_1"
const SLOT_PATTERN: String = "^[a-z0-9_]{1,32}$"

var backend: SaveBackend = JsonFileSaveBackend.new()
var _slot_regex := RegEx.create_from_string(SLOT_PATTERN)


func is_valid_slot(slot_id: String) -> bool:
	return _slot_regex.search(slot_id) != null


func has_save(slot_id: String = DEFAULT_SLOT) -> bool:
	return is_valid_slot(slot_id) and backend.exists(slot_id)


func list_saves() -> PackedStringArray:
	return backend.list_slots()


func save_game(payload: Dictionary, slot_id: String = DEFAULT_SLOT) -> SaveResult:
	var result: SaveResult
	if not is_valid_slot(slot_id):
		result = SaveResult.failure(SaveResult.Status.INVALID_SLOT, "Invalid slot id '%s'" % slot_id)
	else:
		var envelope: Dictionary = {
			"schema_version": SCHEMA_VERSION,
			"game_version": str(ProjectSettings.get_setting("application/config/version", "")),
			"saved_at_unix": int(Time.get_unix_time_from_system()),
			"data": payload,
		}
		var err: Error = backend.write_text(slot_id, JSON.stringify(envelope, "\t"))
		if err == OK:
			result = SaveResult.success(payload)
		else:
			result = SaveResult.failure(SaveResult.Status.WRITE_FAILED, error_string(err))
	_log_failure("save", slot_id, result)
	save_finished.emit(slot_id, result)
	return result


func load_game(slot_id: String = DEFAULT_SLOT) -> SaveResult:
	var result: SaveResult = _load(slot_id)
	_log_failure("load", slot_id, result)
	load_finished.emit(slot_id, result)
	return result


func delete_save(slot_id: String = DEFAULT_SLOT) -> SaveResult:
	if not is_valid_slot(slot_id):
		return SaveResult.failure(SaveResult.Status.INVALID_SLOT, "Invalid slot id '%s'" % slot_id)
	var err: Error = backend.delete(slot_id)
	if err == ERR_FILE_NOT_FOUND:
		return SaveResult.failure(SaveResult.Status.NOT_FOUND, "No save in '%s'" % slot_id)
	if err != OK:
		return SaveResult.failure(SaveResult.Status.WRITE_FAILED, error_string(err))
	return SaveResult.success()


func _load(slot_id: String) -> SaveResult:
	if not is_valid_slot(slot_id):
		return SaveResult.failure(SaveResult.Status.INVALID_SLOT, "Invalid slot id '%s'" % slot_id)
	if not backend.exists(slot_id):
		return SaveResult.failure(SaveResult.Status.NOT_FOUND, "No save in '%s'" % slot_id)
	var text: String = backend.read_text(slot_id)
	if backend.last_error != OK:
		return SaveResult.failure(SaveResult.Status.READ_FAILED, error_string(backend.last_error))
	var json := JSON.new()
	if json.parse(text) != OK:
		return SaveResult.failure(SaveResult.Status.CORRUPTED,
			"JSON error at line %d: %s" % [json.get_error_line(), json.get_error_message()])
	if not json.data is Dictionary:
		return SaveResult.failure(SaveResult.Status.CORRUPTED, "Save is not a JSON object")
	var envelope: Dictionary = json.data
	if not envelope.has("schema_version") or not envelope.get("data") is Dictionary:
		return SaveResult.failure(SaveResult.Status.CORRUPTED, "Save envelope is missing fields")
	var version: int = int(envelope["schema_version"])
	if version > SCHEMA_VERSION:
		return SaveResult.failure(SaveResult.Status.VERSION_TOO_NEW,
			"Save schema %d is newer than supported %d" % [version, SCHEMA_VERSION])
	var data: Dictionary = envelope["data"]
	if version < SCHEMA_VERSION:
		var migrated: Variant = SaveMigrations.migrate(data, version, SCHEMA_VERSION)
		if migrated == null:
			return SaveResult.failure(SaveResult.Status.CORRUPTED, "No migration from schema %d" % version)
		data = migrated
	return SaveResult.success(data)


func _log_failure(operation: String, slot_id: String, result: SaveResult) -> void:
	if not result.is_ok() and result.status != SaveResult.Status.NOT_FOUND:
		DevLog.warn("save", "%s '%s' failed: %s (%s)" % [operation, slot_id, SaveResult.Status.keys()[result.status], result.detail])

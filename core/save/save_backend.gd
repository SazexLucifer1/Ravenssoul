class_name SaveBackend
extends RefCounted
## Storage interface used by SaveService. Implementations move opaque text
## between a slot id and a storage medium. They know nothing about the game.
##
## Implementations: JsonFileSaveBackend (disk), MemorySaveBackend (tests).
## A future platform/cloud backend implements the same five methods.

## Result of the last read_text() call.
var last_error: Error = OK


func exists(_slot_id: String) -> bool:
	push_error("SaveBackend.exists() not implemented")
	return false


## Returns the stored text. Sets [member last_error] to OK or the failure.
func read_text(_slot_id: String) -> String:
	push_error("SaveBackend.read_text() not implemented")
	last_error = ERR_UNAVAILABLE
	return ""


func write_text(_slot_id: String, _text: String) -> Error:
	push_error("SaveBackend.write_text() not implemented")
	return ERR_UNAVAILABLE


func delete(_slot_id: String) -> Error:
	push_error("SaveBackend.delete() not implemented")
	return ERR_UNAVAILABLE


func list_slots() -> PackedStringArray:
	push_error("SaveBackend.list_slots() not implemented")
	return PackedStringArray()


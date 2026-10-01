class_name MemorySaveBackend
extends SaveBackend
## In-memory backend for tests. [member fail_writes] simulates a full disk.

var slots: Dictionary[String, String] = {}
var fail_writes: bool = false


func exists(slot_id: String) -> bool:
	return slots.has(slot_id)


func read_text(slot_id: String) -> String:
	if not slots.has(slot_id):
		last_error = ERR_FILE_NOT_FOUND
		return ""
	last_error = OK
	return slots[slot_id]


func write_text(slot_id: String, text: String) -> Error:
	if fail_writes:
		return ERR_FILE_CANT_WRITE
	slots[slot_id] = text
	return OK


func delete(slot_id: String) -> Error:
	return OK if slots.erase(slot_id) else ERR_FILE_NOT_FOUND


func list_slots() -> PackedStringArray:
	var ids := PackedStringArray(slots.keys())
	ids.sort()
	return ids

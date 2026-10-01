class_name SaveResult
extends RefCounted
## Outcome of a save-service call. Carries a developer detail (logs only) and a
## translation key for the player-facing explanation.

enum Status { OK, NOT_FOUND, CORRUPTED, VERSION_TOO_NEW, READ_FAILED, WRITE_FAILED, INVALID_SLOT }

const PLAYER_MESSAGE_KEYS: Dictionary[Status, String] = {
	Status.OK: "SAVE_STATUS_OK",
	Status.NOT_FOUND: "SAVE_ERROR_NOT_FOUND",
	Status.CORRUPTED: "SAVE_ERROR_CORRUPTED",
	Status.VERSION_TOO_NEW: "SAVE_ERROR_VERSION_TOO_NEW",
	Status.READ_FAILED: "SAVE_ERROR_READ_FAILED",
	Status.WRITE_FAILED: "SAVE_ERROR_WRITE_FAILED",
	Status.INVALID_SLOT: "SAVE_ERROR_INVALID_SLOT",
}

var status: Status = Status.OK
## The game payload (only set on successful loads).
var data: Dictionary = {}
## Developer-only detail. Never display it.
var detail: String = ""


static func success(payload: Dictionary = {}) -> SaveResult:
	var result := SaveResult.new()
	result.data = payload
	return result


static func failure(p_status: Status, p_detail: String) -> SaveResult:
	var result := SaveResult.new()
	result.status = p_status
	result.detail = p_detail
	return result


func is_ok() -> bool:
	return status == Status.OK


## Translation key explaining the outcome to a player in plain language.
func player_message_key() -> String:
	return PLAYER_MESSAGE_KEYS[status]

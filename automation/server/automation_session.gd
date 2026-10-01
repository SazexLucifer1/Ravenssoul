class_name AutomationSession
extends RefCounted
## Per-client automation state. Everything here is temporary and is removed
## by AutomationServer.close_session() (explicit close, idle timeout,
## shutdown).

var id: String
var opened_msec: int
var last_seen_msec: int
var recording: Variant = null # Array[Dictionary] while recording
var checkpoints: Dictionary = {}
var fixture_slots: PackedStringArray = []
var assertion_failures: Array[Dictionary] = []
var request_count: int = 0


func _init() -> void:
	id = Crypto.new().generate_random_bytes(8).hex_encode()
	opened_msec = Time.get_ticks_msec()
	last_seen_msec = opened_msec


func touch() -> void:
	last_seen_msec = Time.get_ticks_msec()
	request_count += 1


func idle_ms() -> int:
	return Time.get_ticks_msec() - last_seen_msec


func record(kind: String, data: Dictionary) -> void:
	if recording is Array:
		(recording as Array).append({"t_ms": Time.get_ticks_msec() - opened_msec, "kind": kind, "data": data})

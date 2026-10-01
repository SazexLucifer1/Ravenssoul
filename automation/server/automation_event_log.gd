class_name AutomationEventLog
extends RefCounted
## Ring buffer of semantic events with monotonically increasing sequence
## numbers. Payloads contain ids and numbers only (no localized text).

signal event_added(event: Dictionary)

var capacity: int
var last_seq: int = 0
var _events: Array[Dictionary] = []


func _init(p_capacity: int = AutomationProtocol.EVENT_BUFFER) -> void:
	capacity = p_capacity


func emit(type: String, data: Dictionary = {}) -> Dictionary:
	last_seq += 1
	var event: Dictionary = {
		"seq": last_seq,
		"type": type,
		"frame": Engine.get_process_frames(),
		"t_ms": Time.get_ticks_msec(),
		"data": data,
	}
	_events.append(event)
	if _events.size() > capacity:
		_events.pop_front()
	event_added.emit(event)
	return event


func since(seq: int, types: Array = [], limit: int = 200) -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	for event: Dictionary in _events:
		if event["seq"] <= seq:
			continue
		if not types.is_empty() and not types.has(event["type"]):
			continue
		out.append(event)
		if out.size() >= limit:
			break
	return out


static func matches(event: Dictionary, type: String, match: Dictionary) -> bool:
	if event["type"] != type:
		return false
	for key: Variant in match:
		var found: Array = AutomationPredicate.lookup(event["data"], str(key))
		if not found[0] or not AutomationPredicate._equal(found[1], match[key]):
			return false
	return true

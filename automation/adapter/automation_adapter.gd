class_name AutomationAdapter
extends RefCounted
## Engine-side contract behind the protocol. The protocol (methods.json) is
## engine-independent; an adapter maps it onto one engine's scene/entity
## model. Every method returns plain JSON-compatible data (ids, numbers,
## strings, arrays, dictionaries). Errors are returned as
## {"error": AutomationProtocol.error(...)}; successes as {"result": ...}.
## To port the protocol to another engine, implement these methods there.


## {"engine", "engine_version", "adapter", "semantic_actions": {name: supported}, "input_devices": [...], "screenshots": bool}
func describe() -> Dictionary:
	return {}


func scene_info() -> Dictionary:
	return {}


func query(_filters: Dictionary) -> Array[Dictionary]:
	return []


## Returns the entity description or null when the id is unknown.
func entity(_id: String, _deep: bool) -> Variant:
	return null


func state() -> Dictionary:
	return {}


func available_actions() -> Array[Dictionary]:
	return []


func perform(_action: String, _target: String, _args: Dictionary) -> Dictionary:
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "not implemented")}


func screenshot(_max_width: int) -> Dictionary:
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "not implemented")}


func perf() -> Dictionary:
	return {}


func is_settled() -> bool:
	return true


func reset(_route: String) -> Dictionary:
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "not implemented")}


func capture_checkpoint() -> Dictionary:
	return {}


func restore_checkpoint(_snapshot: Dictionary) -> Dictionary:
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "not implemented")}


func apply_fixture(_fixture: Dictionary, _write_save: bool) -> Dictionary:
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "not implemented")}


func dev(_method: String, _params: Dictionary) -> Dictionary:
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "not implemented")}


## Start emitting semantic events into [param log]; stop on [method shutdown].
func connect_events(_log: AutomationEventLog) -> void:
	pass


## Removes temporary state created through the adapter (spawned units, ...).
func cleanup_session() -> void:
	pass


func shutdown() -> void:
	pass

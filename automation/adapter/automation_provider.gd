class_name AutomationProvider
extends RefCounted
## Game-specific extension of the Godot adapter. A provider contributes
## semantic entities, state, actions, and events for game systems the generic
## UI layer cannot understand (units, grids, missions, inventories).
## Register providers in GodotAutomationAdapter._init().

var events: AutomationEventLog
var input: AutomationInputDriver


## Add entries to [param index]: id -> {"kind": String, "node": Node (optional), "provider": self}.
func collect(_index: Dictionary, _scene: Node) -> void:
	pass


## Describe an entry this provider added (see docs/AUTOMATION.md#entities).
func describe(_id: String, _entry: Dictionary, _deep: bool) -> Dictionary:
	return {}


func add_state(_state: Dictionary, _scene: Node) -> void:
	pass


func add_actions(_actions: Array[Dictionary], _scene: Node) -> void:
	pass


## Return null if this provider does not handle the action.
func perform(_action: String, _target: String, _args: Dictionary, _scene: Node) -> Variant:
	return null


## Called after every scene change so the provider can connect scene signals.
func hook_scene(_scene: Node) -> void:
	pass


func is_settled(_scene: Node) -> bool:
	return true


func dev(_method: String, _params: Dictionary, _scene: Node) -> Variant:
	return null


func cleanup_session(_scene: Node) -> void:
	pass

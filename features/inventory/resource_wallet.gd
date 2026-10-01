class_name ResourceWallet
extends RefCounted
## Campaign resources (wood, stone, food, gold, soul energy).
## Keys are stable ids; display names come from translation keys.

const WOOD: StringName = &"wood"
const STONE: StringName = &"stone"
const FOOD: StringName = &"food"
const GOLD: StringName = &"gold"
const SOUL_ENERGY: StringName = &"soul_energy"
const ALL: Array[StringName] = [WOOD, STONE, FOOD, GOLD, SOUL_ENERGY]

signal changed(resource_id: StringName, amount: int)

var _amounts: Dictionary[StringName, int] = {}


func amount(resource_id: StringName) -> int:
	return _amounts.get(resource_id, 0)


func add(resource_id: StringName, value: int) -> void:
	assert(ALL.has(resource_id), "Unknown resource id: %s" % resource_id)
	if value <= 0:
		return
	_amounts[resource_id] = amount(resource_id) + value
	changed.emit(resource_id, _amounts[resource_id])


## Returns false (and changes nothing) if the wallet cannot afford it.
func spend(resource_id: StringName, value: int) -> bool:
	if value < 0 or amount(resource_id) < value:
		return false
	_amounts[resource_id] = amount(resource_id) - value
	changed.emit(resource_id, _amounts[resource_id])
	return true


func to_save_data() -> Dictionary:
	var out: Dictionary = {}
	for id: StringName in ALL:
		out[String(id)] = amount(id)
	return out


func load_save_data(data: Dictionary) -> void:
	_amounts.clear()
	for id: StringName in ALL:
		_amounts[id] = maxi(0, int(data.get(String(id), 0)))

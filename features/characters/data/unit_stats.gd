class_name UnitStats
extends Resource
## Additive stat block shared by heroes, battalions, and enemies.
##
## A battlefield unit is hero + battalion, so stats combine by addition
## (see [method combine]). Keep every field an int so combination and saving
## stay trivial and deterministic.

const FIELDS: Array[StringName] = [
	&"max_health",
	&"physical_damage",
	&"magical_damage",
	&"movement",
	&"card_draw",
	&"max_hand_size",
	&"max_ap",
	&"morale",
	&"speed",
]

@export_range(0, 999) var max_health: int = 0
@export_range(0, 999) var physical_damage: int = 0
@export_range(0, 999) var magical_damage: int = 0
@export_range(0, 20) var movement: int = 0
@export_range(0, 20) var card_draw: int = 0
@export_range(0, 20) var max_hand_size: int = 0
@export_range(0, 20) var max_ap: int = 0
@export_range(-100, 100) var morale: int = 0
@export_range(0, 999) var speed: int = 0


static func combine(a: UnitStats, b: UnitStats) -> UnitStats:
	var result := UnitStats.new()
	for field: StringName in FIELDS:
		result.set(field, int(a.get(field)) + int(b.get(field)))
	return result


func to_dict() -> Dictionary:
	var out: Dictionary = {}
	for field: StringName in FIELDS:
		out[String(field)] = int(get(field))
	return out

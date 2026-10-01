class_name EnemyConfig
extends Resource
## Authored enemy type. Enemies follow the same AP/draw/deck rules as player
## units. Scouting intel is tracked per enemy type id (see features/scouting).

@export var id: StringName
@export var name_key: String
@export var stats: UnitStats
@export var deck: Array[CardData] = []
## Stable id of the AI behaviour script/profile that picks cards.
@export var behavior_id: StringName = &"aggressive"

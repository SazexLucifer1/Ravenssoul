class_name MissionData
extends Resource
## One selectable mission: objective, map, and rewards.

enum Objective { DEFEAT, HOLD, ESCORT, REACH }

@export var id: StringName
@export var title_key: String
@export var objective_key: String
@export var objective: Objective = Objective.REACH
@export var map: BattleMapData
@export var rewards: Dictionary[StringName, int] = {}

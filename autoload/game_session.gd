extends Node
## GameSession (Autoload) — campaign state that must survive scene changes.
##
## Why global: the base, mission select, and battle scenes are separate routed
## scenes; the active roster, resources, and soul-energy tally live between
## them. It holds data and serialization only: rules live in features, UI
## reads it through signals.

signal campaign_started
signal campaign_loaded

const ROSTER: RosterCatalog = preload("res://features/characters/content/roster_catalog.tres")
const FIRST_MISSION: MissionData = preload("res://features/quests/content/mission_first_spark.tres")

var wallet := ResourceWallet.new()
var active_loadout: UnitLoadout
var active_mission: MissionData
var completed_missions: PackedStringArray = []
## Tracked globally; drives the corruption story threshold (design pillar).
var soul_energy_spent: int = 0
var has_campaign: bool = false


func start_new_campaign() -> void:
	wallet = ResourceWallet.new()
	active_loadout = UnitLoadout.new(ROSTER.default_hero, ROSTER.default_battalion)
	active_mission = FIRST_MISSION
	completed_missions = []
	soul_energy_spent = 0
	has_campaign = true
	campaign_started.emit()


## Applies mission rewards and records completion. Returns the rewards granted.
func complete_mission(mission: MissionData) -> Dictionary[StringName, int]:
	for resource_id: StringName in mission.rewards:
		wallet.add(resource_id, mission.rewards[resource_id])
	if not completed_missions.has(String(mission.id)):
		completed_missions.append(String(mission.id))
	return mission.rewards


func to_save_data() -> Dictionary:
	return {
		"wallet": wallet.to_save_data(),
		"loadout": active_loadout.to_save_data() if active_loadout else {},
		"active_mission_id": String(active_mission.id) if active_mission else "",
		"completed_missions": Array(completed_missions),
		"soul_energy_spent": soul_energy_spent,
	}


## Returns false (leaving the session untouched) if the data references
## content that no longer exists.
func load_save_data(data: Dictionary) -> bool:
	var loadout := UnitLoadout.from_save_data(data.get("loadout", {}), ROSTER)
	if not loadout.is_complete():
		DevLog.warn("session", "Save references unknown hero or battalion: %s" % data.get("loadout"))
		return false
	wallet = ResourceWallet.new()
	wallet.load_save_data(data.get("wallet", {}))
	active_loadout = loadout
	# Only one mission exists in the first playable; unknown ids fall back to it.
	active_mission = FIRST_MISSION
	completed_missions = PackedStringArray(data.get("completed_missions", []))
	soul_energy_spent = maxi(0, int(data.get("soul_energy_spent", 0)))
	has_campaign = true
	campaign_loaded.emit()
	return true

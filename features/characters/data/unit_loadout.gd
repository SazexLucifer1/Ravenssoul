class_name UnitLoadout
extends RefCounted
## The "Combine" verb: one hero paired with one battalion.
##
## Pure data logic. Produces the combined stats and the combined 20-card deck
## the battlefield unit uses. Saved as ids only (see to_save_data).

var hero: HeroData
var battalion: BattalionData


func _init(p_hero: HeroData = null, p_battalion: BattalionData = null) -> void:
	hero = p_hero
	battalion = p_battalion


func is_complete() -> bool:
	return hero != null and battalion != null


func combined_stats() -> UnitStats:
	assert(is_complete(), "UnitLoadout needs both a hero and a battalion")
	return UnitStats.combine(hero.stats, battalion.stats)


## Hero cards first, then battalion cards. Order is stable for deterministic tests.
func combined_deck() -> Array[CardData]:
	assert(is_complete(), "UnitLoadout needs both a hero and a battalion")
	var cards: Array[CardData] = []
	cards.append_array(hero.deck)
	cards.append_array(battalion.deck)
	return cards


func to_save_data() -> Dictionary:
	return {
		"hero_id": String(hero.id) if hero else "",
		"battalion_id": String(battalion.id) if battalion else "",
	}


static func from_save_data(data: Dictionary, catalog: RosterCatalog) -> UnitLoadout:
	var loadout := UnitLoadout.new(
		catalog.find_hero(StringName(str(data.get("hero_id", "")))),
		catalog.find_battalion(StringName(str(data.get("battalion_id", ""))))
	)
	return loadout

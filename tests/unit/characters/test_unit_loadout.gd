extends TestCase

const ROSTER: RosterCatalog = preload("res://features/characters/content/roster_catalog.tres")


func test_stats_combine_by_addition() -> void:
	var a := UnitStats.new()
	a.max_health = 8
	a.speed = 6
	a.max_ap = 1
	var b := UnitStats.new()
	b.max_health = 12
	b.speed = 2
	b.max_ap = 2
	var c := UnitStats.combine(a, b)
	assert_eq(c.max_health, 20)
	assert_eq(c.speed, 8)
	assert_eq(c.max_ap, 3)
	assert_eq(a.max_health, 8, "inputs are not mutated")


func test_combined_deck_is_twenty_cards_hero_first() -> void:
	var loadout := UnitLoadout.new(ROSTER.default_hero, ROSTER.default_battalion)
	var deck: Array[CardData] = loadout.combined_deck()
	assert_eq(deck.size(), HeroData.DECK_SIZE + BattalionData.DECK_SIZE)
	assert_eq(deck[0], ROSTER.default_hero.deck[0])


func test_all_authored_units_are_valid() -> void:
	for hero: UnitPartData in ROSTER.heroes as Array:
		assert_empty(hero.validate(), "hero %s" % hero.id)
	for battalion: UnitPartData in ROSTER.battalions as Array:
		assert_empty(battalion.validate(), "battalion %s" % battalion.id)


func test_ids_are_unique() -> void:
	var seen: Dictionary = {}
	for part: UnitPartData in (ROSTER.heroes as Array) + (ROSTER.battalions as Array):
		assert_false(seen.has(part.id), "duplicate id %s" % part.id)
		seen[part.id] = true


func test_save_data_round_trips_through_ids() -> void:
	var loadout := UnitLoadout.new(ROSTER.default_hero, ROSTER.default_battalion)
	var data: Dictionary = loadout.to_save_data()
	assert_eq(data["hero_id"], "hero_deserter")
	var restored := UnitLoadout.from_save_data(data, ROSTER)
	assert_true(restored.is_complete())
	assert_eq(restored.hero, ROSTER.default_hero)


func test_unknown_ids_produce_incomplete_loadout() -> void:
	var restored := UnitLoadout.from_save_data({"hero_id": "nobody", "battalion_id": "battalion_militia"}, ROSTER)
	assert_false(restored.is_complete())

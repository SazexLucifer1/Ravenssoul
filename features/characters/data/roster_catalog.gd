class_name RosterCatalog
extends Resource
## Lookup table of all authored heroes and battalions, by stable id.
## Saves store ids; this catalog turns them back into resources.

@export var heroes: Array[HeroData] = []
@export var battalions: Array[BattalionData] = []
@export var default_hero: HeroData
@export var default_battalion: BattalionData


func find_hero(id: StringName) -> HeroData:
	for hero: HeroData in heroes:
		if hero.id == id:
			return hero
	return null


func find_battalion(id: StringName) -> BattalionData:
	for battalion: BattalionData in battalions:
		if battalion.id == id:
			return battalion
	return null

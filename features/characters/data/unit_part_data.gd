class_name UnitPartData
extends Resource
## Shared base for the two halves of a battlefield unit: heroes and battalions.

## Stable, language-independent identifier. Used in saves; never shown to players.
@export var id: StringName
## Translation key for the display name.
@export var name_key: String
@export var stats: UnitStats
@export var deck: Array[CardData] = []


## Overridden by subclasses with the design-mandated deck size.
func expected_deck_size() -> int:
	return 0


## Returns developer-facing problems (empty when valid). Used by content tests.
func validate() -> PackedStringArray:
	var problems := PackedStringArray()
	if String(id).is_empty():
		problems.append("missing id")
	if name_key.is_empty():
		problems.append("%s: missing name_key" % id)
	if stats == null:
		problems.append("%s: missing stats" % id)
	if deck.size() != expected_deck_size():
		problems.append("%s: deck has %d cards, expected %d" % [id, deck.size(), expected_deck_size()])
	for card: CardData in deck:
		if card == null:
			problems.append("%s: deck contains an empty slot" % id)
	return problems

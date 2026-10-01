class_name BattalionData
extends UnitPartData
## A battalion type. Carries the 15-card backbone deck of a unit.

const DECK_SIZE: int = 15


func expected_deck_size() -> int:
	return DECK_SIZE

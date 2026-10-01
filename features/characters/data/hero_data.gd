class_name HeroData
extends UnitPartData
## A named hero. Carries a small 5-card deck that defines its identity.

const DECK_SIZE: int = 5


func expected_deck_size() -> int:
	return DECK_SIZE

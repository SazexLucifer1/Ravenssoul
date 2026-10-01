class_name Deck
extends RefCounted
## Runtime draw/hand/discard piles for one battlefield unit.
##
## Seeded RandomNumberGenerator keeps shuffles reproducible for tests,
## replays, and the scouting "see their hand" intel tier.

var draw_pile: Array[CardData] = []
var hand: Array[CardData] = []
var discard_pile: Array[CardData] = []
var _rng := RandomNumberGenerator.new()


func _init(cards: Array[CardData] = [], rng_seed: int = 0) -> void:
	_rng.seed = rng_seed
	draw_pile = cards.duplicate()
	shuffle_draw_pile()


func shuffle_draw_pile() -> void:
	# Fisher-Yates with our own RNG (Array.shuffle uses the global RNG).
	for i: int in range(draw_pile.size() - 1, 0, -1):
		var j: int = _rng.randi_range(0, i)
		var tmp: CardData = draw_pile[i]
		draw_pile[i] = draw_pile[j]
		draw_pile[j] = tmp


## Draws up to [param count] cards without exceeding [param max_hand_size].
## Reshuffles the discard pile when the draw pile runs out.
## Returns the cards drawn.
func draw(count: int, max_hand_size: int) -> Array[CardData]:
	var drawn: Array[CardData] = []
	while drawn.size() < count and hand.size() < max_hand_size:
		if draw_pile.is_empty():
			if discard_pile.is_empty():
				break
			draw_pile = discard_pile.duplicate()
			discard_pile.clear()
			shuffle_draw_pile()
		var card: CardData = draw_pile.pop_back()
		hand.append(card)
		drawn.append(card)
	return drawn


func discard_from_hand(card: CardData) -> bool:
	var index: int = hand.find(card)
	if index < 0:
		return false
	hand.remove_at(index)
	discard_pile.append(card)
	return true


func total_cards() -> int:
	return draw_pile.size() + hand.size() + discard_pile.size()

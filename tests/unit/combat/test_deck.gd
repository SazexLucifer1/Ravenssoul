extends TestCase


func _cards(count: int) -> Array[CardData]:
	var cards: Array[CardData] = []
	for i: int in count:
		var card := CardData.new()
		card.id = StringName("card_%d" % i)
		cards.append(card)
	return cards


func test_draw_respects_count_and_hand_size() -> void:
	var deck := Deck.new(_cards(20), 7)
	assert_eq(deck.draw(4, 6).size(), 4)
	assert_eq(deck.draw(4, 6).size(), 2, "hand size caps the draw")
	assert_eq(deck.hand.size(), 6)
	assert_eq(deck.total_cards(), 20)


func test_same_seed_same_order() -> void:
	var a := Deck.new(_cards(20), 42)
	var b := Deck.new(_cards(20), 42)
	var ids_a: Array = a.draw(5, 10).map(func(c: CardData) -> StringName: return c.id)
	var ids_b: Array = b.draw(5, 10).map(func(c: CardData) -> StringName: return c.id)
	assert_eq(ids_a, ids_b)


func test_discard_reshuffles_when_draw_pile_empty() -> void:
	var deck := Deck.new(_cards(3), 1)
	deck.draw(3, 10)
	for card: CardData in deck.hand.duplicate():
		assert_true(deck.discard_from_hand(card))
	assert_eq(deck.draw(2, 10).size(), 2)
	assert_eq(deck.total_cards(), 3)


func test_empty_deck_draws_nothing() -> void:
	var deck := Deck.new([], 1)
	assert_empty(deck.draw(3, 5))

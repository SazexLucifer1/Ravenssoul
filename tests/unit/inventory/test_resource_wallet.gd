extends TestCase


func test_add_and_spend() -> void:
	var wallet := ResourceWallet.new()
	wallet.add(ResourceWallet.GOLD, 50)
	assert_true(wallet.spend(ResourceWallet.GOLD, 20))
	assert_eq(wallet.amount(ResourceWallet.GOLD), 30)


func test_cannot_overspend() -> void:
	var wallet := ResourceWallet.new()
	wallet.add(ResourceWallet.WOOD, 5)
	assert_false(wallet.spend(ResourceWallet.WOOD, 6))
	assert_eq(wallet.amount(ResourceWallet.WOOD), 5)


func test_save_round_trip_uses_stable_ids_and_sanitizes() -> void:
	var wallet := ResourceWallet.new()
	wallet.add(ResourceWallet.SOUL_ENERGY, 3)
	var data: Dictionary = wallet.to_save_data()
	assert_eq(data["soul_energy"], 3)
	data["food"] = -10
	var restored := ResourceWallet.new()
	restored.load_save_data(data)
	assert_eq(restored.amount(ResourceWallet.SOUL_ENERGY), 3)
	assert_eq(restored.amount(ResourceWallet.FOOD), 0, "negative values are clamped")

extends TestCase


func test_sorted_by_descending_speed() -> void:
	var order: Array = TurnOrder.sort_by_speed([{"id": "a", "speed": 3}, {"id": "b", "speed": 9}, {"id": "c", "speed": 5}])
	assert_eq(order.map(func(e: Dictionary) -> String: return e["id"]), ["b", "c", "a"])


func test_ties_keep_input_order() -> void:
	var order: Array = TurnOrder.sort_by_speed([{"id": "a", "speed": 4}, {"id": "b", "speed": 4}, {"id": "c", "speed": 4}])
	assert_eq(order.map(func(e: Dictionary) -> String: return e["id"]), ["a", "b", "c"])


func test_works_with_stat_resources() -> void:
	var slow := UnitStats.new()
	slow.speed = 1
	var fast := UnitStats.new()
	fast.speed = 7
	assert_eq(TurnOrder.sort_by_speed([slow, fast])[0], fast)

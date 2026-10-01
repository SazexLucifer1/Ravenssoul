extends TestCase

const STATE: Dictionary = {"unit": {"cell": [0, 10], "health": 16}, "screens": ["screen.pause"], "route": "gameplay", "paused": true}


func _eval(condition: Dictionary) -> bool:
	return AutomationPredicate.evaluate(condition, func(_s: String, _id: String) -> Variant: return STATE)["passed"]


func _c(path: String, op: String, value: Variant = null) -> Dictionary:
	var c: Dictionary = {"source": "state", "path": path, "op": op}
	if value != null:
		c["value"] = value
	return c


func test_comparison_ops() -> void:
	assert_true(_eval(_c("unit.cell", "eq", [0.0, 10.0])), "JSON floats equal ints")
	assert_true(_eval(_c("unit.health", "gt", 10)))
	assert_false(_eval(_c("unit.health", "lt", 10)))
	assert_true(_eval(_c("unit.health", "lte", 16)))
	assert_true(_eval(_c("screens", "contains", "screen.pause")))
	assert_true(_eval(_c("route", "in", ["main_menu", "gameplay"])))
	assert_true(_eval(_c("route", "ne", "main_menu")))
	assert_true(_eval(_c("unit.cell.1", "eq", 10)), "array index path")
	assert_true(_eval(_c("missing.path", "not_exists")))
	assert_false(_eval(_c("missing.path", "eq", null if false else 0)))


func test_combinators() -> void:
	assert_true(_eval({"all": [_c("paused", "eq", true), _c("route", "eq", "gameplay")]}))
	assert_false(_eval({"all": [_c("paused", "eq", true), _c("route", "eq", "main_menu")]}))
	assert_true(_eval({"any": [_c("route", "eq", "x"), _c("route", "eq", "gameplay")]}))
	assert_true(_eval({"not": _c("route", "eq", "x")}))


func test_actual_value_is_reported() -> void:
	var outcome: Dictionary = AutomationPredicate.evaluate(_c("unit.cell", "eq", [1, 10]), func(_s: String, _i: String) -> Variant: return STATE)
	assert_false(outcome["passed"])
	assert_eq(outcome["actual"], [0, 10])


func test_validation_rejects_malformed_conditions() -> void:
	assert_empty(AutomationPredicate.validate(_c("a", "eq", 1)))
	assert_false(AutomationPredicate.validate("string").is_empty())
	assert_false(AutomationPredicate.validate({"source": "state", "path": "a", "op": "regex", "value": 1}).is_empty(), "unknown op")
	assert_false(AutomationPredicate.validate({"source": "disk", "path": "a", "op": "eq", "value": 1}).is_empty(), "unknown source")
	assert_false(AutomationPredicate.validate({"source": "entity", "path": "a", "op": "eq", "value": 1}).is_empty(), "entity needs id")
	assert_false(AutomationPredicate.validate({"source": "state", "path": "a", "op": "eq"}).is_empty(), "missing value")
	assert_false(AutomationPredicate.validate({"all": []}).is_empty(), "empty all")
	assert_false(AutomationPredicate.validate({"source": "state", "path": "a", "op": "eq", "value": 1, "code": "x"}).is_empty(), "extra field")


func test_size_and_depth_limits() -> void:
	var deep: Dictionary = _c("a", "exists")
	for i: int in 10:
		deep = {"not": deep}
	assert_false(AutomationPredicate.validate(deep).is_empty(), "too deep")
	var wide: Array = []
	for i: int in 100:
		wide.append(_c("a", "exists"))
	assert_false(AutomationPredicate.validate({"all": wide}).is_empty(), "too many nodes")

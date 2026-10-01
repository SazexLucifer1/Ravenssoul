extends TestCase

const SCHEMA: Dictionary = {
	"type": "object", "additionalProperties": false, "required": ["name"],
	"properties": {
		"name": {"type": "string", "minLength": 1, "maxLength": 8, "pattern": "^[a-z_]+$"},
		"count": {"type": "integer", "minimum": 1, "maximum": 5},
		"mode": {"type": "string", "enum": ["tap", "down"]},
		"cell": {"type": "array", "items": {"type": "integer"}, "minItems": 2, "maxItems": 2},
		"flag": {"type": "boolean"},
	},
}


func _problems(value: Variant) -> PackedStringArray:
	return AutomationSchemaValidator.validate(value, SCHEMA)


func test_valid_payload() -> void:
	assert_empty(_problems({"name": "ok_name", "count": 3.0, "mode": "tap", "cell": [1, 2], "flag": true}))


func test_rejects_wrong_types_and_missing_fields() -> void:
	assert_false(_problems([]).is_empty(), "array instead of object")
	assert_false(_problems({}).is_empty(), "missing required")
	assert_false(_problems({"name": 5}).is_empty(), "number instead of string")
	assert_false(_problems({"name": "a", "count": 1.5}).is_empty(), "non-integral integer")
	assert_false(_problems({"name": "a", "flag": "yes"}).is_empty(), "string instead of boolean")


func test_rejects_unknown_fields_and_out_of_range_values() -> void:
	assert_false(_problems({"name": "a", "evil": true}).is_empty(), "unknown field")
	assert_false(_problems({"name": "a", "count": 9}).is_empty(), "above maximum")
	assert_false(_problems({"name": "a", "mode": "hold"}).is_empty(), "not in enum")
	assert_false(_problems({"name": "../x"}).is_empty(), "pattern")
	assert_false(_problems({"name": "abcdefghijk"}).is_empty(), "too long")
	assert_false(_problems({"name": "a", "cell": [1]}).is_empty(), "too few items")
	assert_false(_problems({"name": "a", "cell": [1, "x"]}).is_empty(), "item type")


func test_every_method_schema_is_well_formed() -> void:
	var known: Array = ["object", "string", "integer", "number", "boolean", "array"]
	var methods: Dictionary = AutomationProtocol.methods()
	assert_true(methods.size() >= 30, "method table loaded")
	for name: String in methods:
		var params: Dictionary = methods[name]["params"]
		assert_eq(params["type"], "object", name)
		assert_eq(params.get("additionalProperties"), false, "%s must reject unknown params" % name)
		for prop: String in params.get("properties", {}):
			assert_true(known.has(params["properties"][prop].get("type", "")), "%s.%s has a known type" % [name, prop])
		assert_empty(AutomationSchemaValidator.validate({}, {"type": "object"}))

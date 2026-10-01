class_name AutomationSchemaValidator
extends RefCounted
## Validates JSON values against the JSON-Schema subset used in methods.json:
## type, properties, required, additionalProperties(false), enum, minimum,
## maximum, minLength, maxLength, pattern, items, minItems, maxItems.
## Returns a list of problems (empty = valid). Depth- and size-limited.

const MAX_DEPTH: int = 12


static func validate(value: Variant, schema: Dictionary, path: String = "params", depth: int = 0) -> PackedStringArray:
	var problems := PackedStringArray()
	if depth > MAX_DEPTH:
		problems.append("%s: nested too deeply" % path)
		return problems
	var expected: String = schema.get("type", "")
	if not expected.is_empty() and not _type_matches(value, expected):
		problems.append("%s: expected %s" % [path, expected])
		return problems
	if schema.has("enum") and not (schema["enum"] as Array).has(value):
		problems.append("%s: must be one of %s" % [path, ", ".join(PackedStringArray(schema["enum"]))])
	match expected:
		"string":
			var s: String = value
			if s.length() < int(schema.get("minLength", 0)):
				problems.append("%s: too short" % path)
			if schema.has("maxLength") and s.length() > int(schema["maxLength"]):
				problems.append("%s: too long" % path)
			if schema.has("pattern") and RegEx.create_from_string(schema["pattern"]).search(s) == null:
				problems.append("%s: invalid format" % path)
		"integer", "number":
			var n: float = float(value)
			if schema.has("minimum") and n < float(schema["minimum"]):
				problems.append("%s: below minimum %s" % [path, schema["minimum"]])
			if schema.has("maximum") and n > float(schema["maximum"]):
				problems.append("%s: above maximum %s" % [path, schema["maximum"]])
		"array":
			var arr: Array = value
			if arr.size() < int(schema.get("minItems", 0)):
				problems.append("%s: too few items" % path)
			if schema.has("maxItems") and arr.size() > int(schema["maxItems"]):
				problems.append("%s: too many items" % path)
			if schema.has("items"):
				for i: int in arr.size():
					problems.append_array(validate(arr[i], schema["items"], "%s[%d]" % [path, i], depth + 1))
		"object":
			var obj: Dictionary = value
			var props: Dictionary = schema.get("properties", {})
			for key: Variant in schema.get("required", []):
				if not obj.has(key):
					problems.append("%s.%s: required" % [path, key])
			for key: Variant in obj:
				if not key is String:
					problems.append("%s: keys must be strings" % path)
				elif props.has(key):
					problems.append_array(validate(obj[key], props[key], "%s.%s" % [path, key], depth + 1))
				elif schema.get("additionalProperties", true) == false:
					problems.append("%s.%s: unknown field" % [path, key])
	return problems


static func _type_matches(value: Variant, expected: String) -> bool:
	match expected:
		"object":
			return value is Dictionary
		"array":
			return value is Array
		"string":
			return value is String
		"boolean":
			return value is bool
		"number":
			return value is float or value is int
		"integer":
			# JSON numbers arrive as float; accept integral values only.
			return value is int or (value is float and is_equal_approx(value, roundf(value)) and absf(value) < 9.0e15)
	return true

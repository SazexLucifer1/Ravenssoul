class_name AutomationPredicate
extends RefCounted
## Declarative conditions for waits and assertions. No code is evaluated.
##
##   {"source": "state", "path": "unit.cell", "op": "eq", "value": [10, 1]}
##   {"source": "entity", "id": "main_menu.new_game", "path": "enabled", "op": "eq", "value": true}
##   {"all": [...]}, {"any": [...]}, {"not": {...}}
## Ops: eq ne gt gte lt lte contains in exists not_exists.

const OPS: PackedStringArray = ["eq", "ne", "gt", "gte", "lt", "lte", "contains", "in", "exists", "not_exists"]
const MAX_NODES: int = 64
const MAX_DEPTH: int = 6


## Returns problems with the predicate's shape (empty = valid).
static func validate(condition: Variant) -> PackedStringArray:
	return _validate(condition, 0, [0])


static func _validate(condition: Variant, depth: int, counter: Array) -> PackedStringArray:
	var problems := PackedStringArray()
	counter[0] += 1
	if depth > MAX_DEPTH or counter[0] > MAX_NODES:
		problems.append("condition too large")
		return problems
	if not condition is Dictionary:
		problems.append("condition must be an object")
		return problems
	var c: Dictionary = condition
	for combinator: String in ["all", "any"]:
		if c.has(combinator):
			if not c[combinator] is Array or (c[combinator] as Array).is_empty() or c.size() != 1:
				problems.append("%s must be the only key and a non-empty list" % combinator)
				return problems
			for child: Variant in c[combinator]:
				problems.append_array(_validate(child, depth + 1, counter))
			return problems
	if c.has("not"):
		if c.size() != 1:
			problems.append("not must be the only key")
			return problems
		return _validate(c["not"], depth + 1, counter)
	for key: Variant in c:
		if not key in ["source", "id", "path", "op", "value"]:
			problems.append("unknown condition field '%s'" % key)
	if not c.get("source", "") in ["state", "entity", "scene"]:
		problems.append("source must be state, entity or scene")
	if c.get("source", "") == "entity" and not c.get("id", null) is String:
		problems.append("entity conditions need an id")
	if not c.get("path", null) is String or str(c.get("path", "")).length() > 128:
		problems.append("path must be a string")
	if not c.get("op", "") in OPS:
		problems.append("op must be one of %s" % ", ".join(OPS))
	if not c.get("op", "") in ["exists", "not_exists"] and not c.has("value"):
		problems.append("value required for op %s" % c.get("op", ""))
	return problems


## [param resolve] is Callable(source: String, id: String) -> Variant (Dictionary or null).
## Returns {"passed": bool, "actual": Variant}.
static func evaluate(condition: Dictionary, resolve: Callable) -> Dictionary:
	if condition.has("all"):
		for child: Dictionary in condition["all"]:
			var r: Dictionary = evaluate(child, resolve)
			if not r["passed"]:
				return r
		return {"passed": true, "actual": null}
	if condition.has("any"):
		var last: Dictionary = {"passed": false, "actual": null}
		for child: Dictionary in condition["any"]:
			last = evaluate(child, resolve)
			if last["passed"]:
				return last
		return last
	if condition.has("not"):
		var inner: Dictionary = evaluate(condition["not"], resolve)
		return {"passed": not inner["passed"], "actual": inner["actual"]}
	var root: Variant = resolve.call(str(condition["source"]), str(condition.get("id", "")))
	var found: Array = lookup(root, str(condition["path"]))
	var exists: bool = found[0]
	var actual: Variant = found[1]
	var expected: Variant = condition.get("value")
	var passed: bool = false
	match str(condition["op"]):
		"exists": passed = exists
		"not_exists": passed = not exists
		"eq": passed = exists and _equal(actual, expected)
		"ne": passed = not exists or not _equal(actual, expected)
		"gt": passed = exists and _num(actual) and _num(expected) and float(actual) > float(expected)
		"gte": passed = exists and _num(actual) and _num(expected) and float(actual) >= float(expected)
		"lt": passed = exists and _num(actual) and _num(expected) and float(actual) < float(expected)
		"lte": passed = exists and _num(actual) and _num(expected) and float(actual) <= float(expected)
		"contains":
			if actual is String:
				passed = str(actual).contains(str(expected))
			elif actual is Array:
				passed = (actual as Array).any(func(v: Variant) -> bool: return _equal(v, expected))
			elif actual is Dictionary:
				passed = (actual as Dictionary).has(expected)
		"in": passed = exists and expected is Array and (expected as Array).any(func(v: Variant) -> bool: return _equal(actual, v))
	return {"passed": passed, "actual": actual}


## Dotted path lookup: "unit.cell.0", "screens.0.id". Returns [exists, value].
static func lookup(root: Variant, path: String) -> Array:
	var current: Variant = root
	if path.is_empty():
		return [root != null, root]
	for part: String in path.split("."):
		if current is Dictionary and (current as Dictionary).has(part):
			current = current[part]
		elif current is Array and part.is_valid_int() and int(part) >= 0 and int(part) < (current as Array).size():
			current = current[int(part)]
		else:
			return [false, null]
	return [true, current]


static func _num(v: Variant) -> bool:
	return v is int or v is float


static func _equal(a: Variant, b: Variant) -> bool:
	if _num(a) and _num(b):
		return is_equal_approx(float(a), float(b))
	if a is Array and b is Array:
		if (a as Array).size() != (b as Array).size():
			return false
		for i: int in (a as Array).size():
			if not _equal(a[i], b[i]):
				return false
		return true
	if a is Dictionary and b is Dictionary:
		if (a as Dictionary).size() != (b as Dictionary).size():
			return false
		for k: Variant in a:
			if not (b as Dictionary).has(k) or not _equal(a[k], b[k]):
				return false
		return true
	return typeof(a) == typeof(b) and a == b

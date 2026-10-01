class_name TurnOrder
extends RefCounted
## Speed-based initiative, recalculated every round.

## Returns entries sorted by descending speed. Ties keep their input order
## (stable), so authored unit order breaks ties deterministically.
## Each entry must expose a "speed" property or key.
static func sort_by_speed(entries: Array) -> Array:
	var indexed: Array = []
	for i: int in entries.size():
		indexed.append([entries[i], i])
	indexed.sort_custom(func(a: Array, b: Array) -> bool:
		var sa: int = int(a[0].get("speed"))
		var sb: int = int(b[0].get("speed"))
		if sa == sb:
			return a[1] < b[1]
		return sa > sb
	)
	var out: Array = []
	for pair: Array in indexed:
		out.append(pair[0])
	return out

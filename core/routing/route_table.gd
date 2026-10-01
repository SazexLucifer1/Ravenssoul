class_name RouteTable
extends Resource
## Maps stable route ids to scene paths so callers never hard-code scene files.

@export var routes: Dictionary[StringName, String] = {}


func has_route(route: StringName) -> bool:
	return routes.has(route)


func path_for(route: StringName) -> String:
	return routes.get(route, "")

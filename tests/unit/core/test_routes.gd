extends TestCase

const ROUTE_TABLE: RouteTable = preload("res://core/routing/route_table.tres")


func test_every_route_points_to_a_loadable_scene() -> void:
	for route: StringName in ROUTE_TABLE.routes:
		var path: String = ROUTE_TABLE.path_for(route)
		assert_true(ResourceLoader.exists(path), "route %s -> %s" % [route, path])
		assert_true(load(path) is PackedScene, "route %s is not a scene" % route)


func test_route_constants_are_registered() -> void:
	for route: StringName in [Routes.BOOTSTRAP, Routes.MAIN_MENU, Routes.GAMEPLAY]:
		assert_true(ROUTE_TABLE.has_route(route), String(route))


func test_main_scene_is_bootstrap() -> void:
	assert_eq(ProjectSettings.get_setting("application/run/main_scene"), ROUTE_TABLE.path_for(Routes.BOOTSTRAP))

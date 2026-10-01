extends TestCase
## Grid movement is the tactical "collision" system. These tests cover the
## grid equivalents of tunneling, corners, overlap, and repeated hits.

const MAP: BattleMapData = preload("res://features/combat/content/maps/map_training_ground.tres")


func _grid() -> BattleGrid:
	var grid := BattleGrid.new()
	grid.map_data = MAP
	return add_node(grid) as BattleGrid


func test_authored_map_is_valid() -> void:
	assert_empty(MAP.validate())


func test_blocked_and_outside_cells_cannot_be_entered() -> void:
	var grid := _grid()
	assert_false(grid.can_enter(Vector2i(4, 3)), "blocked")
	assert_false(grid.can_enter(Vector2i(-1, 0)), "outside left")
	assert_false(grid.can_enter(MAP.size), "outside bottom-right corner")
	assert_true(grid.can_enter(Vector2i(0, 0)))


func test_occupied_cells_cannot_overlap() -> void:
	var grid := _grid()
	var a := Node.new()
	var b := Node.new()
	assert_true(grid.place(a, Vector2i(1, 1)))
	assert_false(grid.place(b, Vector2i(1, 1)))
	assert_eq(grid.occupant_at(Vector2i(1, 1)), a)
	a.free()
	b.free()


func test_cell_world_round_trip_is_exact_at_cell_centers() -> void:
	var grid := _grid()
	for cell: Vector2i in [Vector2i(0, 0), Vector2i(5, 7), Vector2i(11, 11)]:
		assert_eq(grid.local_to_cell(grid.cell_to_local(cell)), cell)


func _controller_with_unit(start: Vector2i) -> PlayerUnitController:
	var grid := _grid()
	var unit := instantiate("res://features/characters/character.tscn", grid) as Character
	var stats := UnitStats.new()
	stats.max_health = 10
	unit.configure(stats, "HERO_DESERTER_NAME")
	grid.place(unit, start)
	unit.place_at(start, grid.cell_to_local(start))
	var controller := PlayerUnitController.new()
	controller.grid = grid
	controller.unit = unit
	add_node(controller)
	return controller


func test_no_tunneling_through_walls() -> void:
	# (3,4) -> right is the wall column x=4; a step must never skip past it.
	var controller := _controller_with_unit(Vector2i(3, 4))
	assert_false(controller.try_step(Vector2i.RIGHT))
	assert_eq(controller.unit.cell, Vector2i(3, 4))
	assert_false(controller.try_step(Vector2i(2, 0)), "multi-cell jumps are rejected")
	assert_false(controller.try_step(Vector2i(1, 1)), "diagonal corner cutting is rejected")


func test_hazard_hits_once_per_entry() -> void:
	var controller := _controller_with_unit(Vector2i(5, 7))
	var hits: Array[int] = []
	controller.hazard_entered.connect(func(_c: Vector2i, dealt: int) -> void: hits.append(dealt))
	assert_true(controller.try_step(Vector2i.DOWN)) # onto hazard (5,8)
	assert_eq(hits.size(), 1)
	assert_eq(controller.unit.health.current_health, 10 - MAP.hazard_damage)
	assert_false(controller.try_step(Vector2i.ZERO), "standing still is not a step")
	assert_eq(hits.size(), 1, "no repeated hit without re-entering")


func test_objective_signal() -> void:
	var controller := _controller_with_unit(MAP.objective_cell + Vector2i.LEFT)
	var reached: Array[Vector2i] = []
	controller.objective_reached.connect(func(c: Vector2i) -> void: reached.append(c))
	controller.try_step(Vector2i.RIGHT)
	assert_eq(reached, [MAP.objective_cell] as Array[Vector2i])

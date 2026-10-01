extends TestCase
## Movement and animation contract of the reusable character scene.

const CELL: int = 48


func _character() -> Character:
	var unit := instantiate("res://features/characters/character.tscn") as Character
	var stats := UnitStats.new()
	stats.max_health = 10
	unit.configure(stats, "HERO_DESERTER_NAME")
	unit.place_at(Vector2i(1, 1), Vector2(CELL * 1.5, CELL * 1.5))
	return unit


func test_logical_cell_updates_before_animation_finishes() -> void:
	Settings.reduced_motion = false
	var unit := _character()
	unit.step_to(Vector2i(2, 1), Vector2(CELL * 2.5, CELL * 1.5))
	assert_eq(unit.cell, Vector2i(2, 1), "gameplay state is immediate")
	assert_true(unit.mover.is_moving(), "visual is still catching up")


func test_visual_ends_exactly_on_cell_center() -> void:
	Settings.reduced_motion = false
	var unit := _character()
	var target := Vector2(CELL * 2.5, CELL * 1.5)
	unit.step_to(Vector2i(2, 1), target)
	assert_true(await wait_until(func() -> bool: return not unit.mover.is_moving(), 2.0))
	assert_eq(unit.position, target, "visible contact alignment")


func test_frame_rate_does_not_change_outcome() -> void:
	Settings.reduced_motion = false
	var unit := _character()
	var target := Vector2(CELL * 2.5, CELL * 1.5)
	Engine.time_scale = 4.0 # simulates very large frame deltas
	unit.step_to(Vector2i(2, 1), target)
	await wait_until(func() -> bool: return not unit.mover.is_moving(), 2.0)
	Engine.time_scale = 1.0
	assert_eq(unit.position, target)
	assert_eq(unit.cell, Vector2i(2, 1))


func test_reduced_motion_snaps() -> void:
	Settings.reduced_motion = true
	var unit := _character()
	unit.step_to(Vector2i(1, 2), Vector2(CELL * 1.5, CELL * 2.5))
	assert_false(unit.mover.is_moving())
	assert_eq(unit.position, Vector2(CELL * 1.5, CELL * 2.5))


func test_gameplay_state_drives_animation_transitions() -> void:
	Settings.reduced_motion = false
	var unit := _character()
	assert_eq(unit.animator.current_animation(), CharacterAnimator.ANIM_IDLE)
	unit.step_to(Vector2i(2, 1), Vector2(CELL * 2.5, CELL * 1.5))
	assert_eq(unit.animator.current_animation(), CharacterAnimator.ANIM_MOVE)
	await wait_until(func() -> bool: return not unit.mover.is_moving(), 2.0)
	assert_eq(unit.animator.current_animation(), CharacterAnimator.ANIM_IDLE)
	unit.health.apply_damage(1)
	assert_eq(unit.animator.current_animation(), CharacterAnimator.ANIM_HIT)
	await wait_until(func() -> bool: return unit.animator.current_animation() == CharacterAnimator.ANIM_IDLE, 2.0)
	unit.health.apply_damage(99)
	assert_eq(unit.animator.current_animation(), CharacterAnimator.ANIM_DEFEAT)
	unit.step_to(Vector2i(3, 1), Vector2(CELL * 3.5, CELL * 1.5))
	assert_eq(unit.animator.current_animation(), CharacterAnimator.ANIM_DEFEAT, "defeat is terminal")


func test_animation_events_fire_from_tracks() -> void:
	Settings.reduced_motion = false
	var unit := _character()
	var events: Array[StringName] = []
	unit.animator.animation_event.connect(func(e: StringName) -> void: events.append(e))
	unit.step_to(Vector2i(2, 1), Vector2(CELL * 2.5, CELL * 1.5))
	await wait_seconds(0.25)
	assert_contains(events, &"footstep")
	unit.health.apply_damage(1)
	await wait_frames(2)
	assert_contains(events, &"hit_impact")


func test_facing_follows_horizontal_movement() -> void:
	Settings.reduced_motion = true
	var unit := _character()
	unit.step_to(Vector2i(0, 1), Vector2(CELL * 0.5, CELL * 1.5))
	assert_eq(unit.visual.facing, -1)
	unit.step_to(Vector2i(1, 1), Vector2(CELL * 1.5, CELL * 1.5))
	assert_eq(unit.visual.facing, 1)


func test_defeated_signal() -> void:
	var unit := _character()
	var defeated: Array[int] = [0]
	unit.defeated.connect(func() -> void: defeated[0] += 1)
	unit.health.apply_damage(100)
	assert_eq(defeated[0], 1)
	assert_true(unit.is_defeated())

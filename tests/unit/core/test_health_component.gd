extends TestCase


func _make(max_health: int = 10) -> HealthComponent:
	var health := HealthComponent.new()
	health.max_health = max_health
	return add_node(health) as HealthComponent


func test_starts_at_full_health() -> void:
	var health := _make(12)
	assert_eq(health.current_health, 12)
	assert_false(health.is_dead())


func test_damage_is_clamped_and_reports_dealt_amount() -> void:
	var health := _make(5)
	assert_eq(health.apply_damage(3), 3)
	assert_eq(health.apply_damage(10), 2, "only remaining health can be dealt")
	assert_eq(health.current_health, 0)
	assert_true(health.is_dead())


func test_died_emits_exactly_once() -> void:
	var health := _make(3)
	var deaths: Array[int] = [0]
	health.died.connect(func() -> void: deaths[0] += 1)
	health.apply_damage(3)
	health.apply_damage(3)
	assert_eq(deaths[0], 1)


func test_zero_or_negative_damage_is_ignored() -> void:
	var health := _make(5)
	var events: Array[int] = [0]
	health.health_changed.connect(func(_c: int, _m: int, _d: int) -> void: events[0] += 1)
	assert_eq(health.apply_damage(0), 0)
	assert_eq(health.apply_damage(-4), 0)
	assert_eq(events[0], 0)


func test_heal_caps_at_max_and_cannot_heal_the_dead() -> void:
	var health := _make(10)
	health.apply_damage(4)
	assert_eq(health.heal(100), 4)
	assert_eq(health.current_health, 10)
	health.apply_damage(10)
	assert_eq(health.heal(5), 0)
	health.revive(3)
	assert_eq(health.current_health, 3)


func test_lowering_max_health_clamps_current() -> void:
	var health := _make(10)
	health.set_current_health(8)
	health.max_health = 5
	assert_eq(health.current_health, 5)


func test_damaged_signal_carries_source() -> void:
	var health := _make(10)
	var sources: Array[StringName] = []
	health.damaged.connect(func(_a: int, source: StringName) -> void: sources.append(source))
	health.apply_damage(1, &"hazard")
	assert_eq(sources, [&"hazard"] as Array[StringName])

class_name UtopiaAutomationProvider
extends AutomationProvider
## Game semantics for Utopia: units (+ components), the battle grid, the
## active mission, campaign resources, unit movement, pause/resume, and the
## allow-listed dev commands. Ids derive from content ids, so they are stable
## across runs and languages:
##   unit.<hero_id>, unit.<hero_id>/health, unit.spawned.<n>, grid.<map_id>,
##   mission.<mission_id>, resource.<resource_id>

const DIRECTIONS: Dictionary[String, Vector2i] = {
	"up": Vector2i.UP, "down": Vector2i.DOWN, "left": Vector2i.LEFT, "right": Vector2i.RIGHT,
}
const MAX_PATH: int = 64
const MAX_GRANT: int = 10000

var _mission_result: Variant = null # null = running, true/false = finished
var _spawned: Array[Character] = []


# --- gameplay scene contract (public fields of scenes/gameplay/gameplay.gd) --------

static func is_gameplay(scene: Node) -> bool:
	return scene != null and "player_unit" in scene and "controller" in scene and "grid" in scene


static func player_unit(scene: Node) -> Character:
	return scene.get("player_unit") as Character if is_gameplay(scene) else null


func player_unit_id(scene: Node) -> String:
	var loadout: UnitLoadout = GameSession.active_loadout
	return "unit.%s" % loadout.hero.id if loadout != null and loadout.hero != null else "unit.player"


# --- entities ----------------------------------------------------------------------

func collect(index: Dictionary, scene: Node) -> void:
	for id: StringName in ResourceWallet.ALL:
		index["resource.%s" % id] = {"kind": "resource", "provider": self, "resource": id}
	if GameSession.active_mission != null and GameSession.has_campaign:
		index["mission.%s" % GameSession.active_mission.id] = {"kind": "mission", "provider": self}
	if not is_gameplay(scene):
		return
	var grid: BattleGrid = scene.get("grid")
	if grid != null and grid.map_data != null:
		index["grid.%s" % grid.map_data.id] = {"kind": "grid", "provider": self, "node": grid}
	var unit: Character = player_unit(scene)
	if unit != null and is_instance_valid(unit):
		_add_unit(index, player_unit_id(scene), unit, true)
	for i: int in _spawned.size():
		if is_instance_valid(_spawned[i]):
			_add_unit(index, "unit.spawned.%d" % (i + 1), _spawned[i], false)


func _add_unit(index: Dictionary, id: String, unit: Character, is_player: bool) -> void:
	index[id] = {"kind": "unit", "provider": self, "node": unit, "player": is_player}
	index["%s/health" % id] = {"kind": "component", "provider": self, "node": unit.health, "component": "health"}
	index["%s/mover" % id] = {"kind": "component", "provider": self, "node": unit.mover, "component": "mover"}
	index["%s/animator" % id] = {"kind": "component", "provider": self, "node": unit.animator, "component": "animator"}


func describe(id: String, entry: Dictionary, deep: bool) -> Dictionary:
	var base: Dictionary = {"tags": [], "visible": true, "enabled": true, "interactable": false,
		"components": [], "actions": [], "properties": {}}
	match entry["kind"]:
		"resource":
			base.merge({"type": "Resource", "name": String(entry["resource"]), "role": "item",
				"text_key": "RESOURCE_%s" % String(entry["resource"]).to_upper(),
				"properties": {"amount": GameSession.wallet.amount(entry["resource"])}}, true)
		"mission":
			var mission: MissionData = GameSession.active_mission
			base.merge({"type": "Mission", "name": String(mission.id), "role": "quest",
				"text_key": mission.title_key,
				"properties": {"objective": MissionData.Objective.keys()[mission.objective].to_lower(),
					"objective_key": mission.objective_key,
					"completed": GameSession.completed_missions.has(String(mission.id)),
					"rewards": _plain(mission.rewards)}}, true)
		"grid":
			var grid: BattleGrid = entry["node"]
			var map: BattleMapData = grid.map_data
			base.merge({"type": "BattleGrid", "name": String(map.id), "role": "grid",
				"properties": {"size": [map.size.x, map.size.y], "objective_cell": _cell(map.objective_cell),
					"spawn_cell": _cell(map.player_spawn), "blocked_cells": map.blocked_cells.map(_cell),
					"hazard_cells": map.hazard_cells.map(_cell), "hazard_damage": map.hazard_damage}}, true)
		"unit":
			var unit: Character = entry["node"]
			var screen_pos: Vector2 = unit.get_global_transform_with_canvas().origin
			var controller: PlayerUnitController = unit.get_tree().current_scene.get("controller") if entry["player"] else null
			var can_act: bool = entry["player"] and controller != null and controller.enabled and not unit.is_defeated()
			base.merge({"type": "Character", "name": unit.display_name_key, "role": "unit",
				"tags": ["player"] if entry["player"] else ["spawned"],
				"visible": unit.is_visible_in_tree(), "enabled": not unit.is_defeated(), "interactable": can_act,
				"position": {"x": roundi(screen_pos.x), "y": roundi(screen_pos.y)},
				"cell": _cell(unit.cell),
				"components": ["health", "mover", "animator"],
				"actions": ["move"] if can_act else [],
				"text_key": unit.display_name_key,
				"properties": {"health": unit.health.current_health, "max_health": unit.health.max_health,
					"defeated": unit.is_defeated(), "moving": unit.mover.is_moving(),
					"facing": unit.visual.facing, "animation": String(unit.animator.current_animation())}}, true)
			if deep and unit.stats != null:
				base["properties"]["stats"] = unit.stats.to_dict()
		"component":
			var node: Node = entry["node"]
			var props: Dictionary = {}
			match entry["component"]:
				"health":
					props = {"current": (node as HealthComponent).current_health, "max": (node as HealthComponent).max_health, "dead": (node as HealthComponent).is_dead()}
				"mover":
					props = {"moving": (node as GridMover).is_moving(), "step_seconds": (node as GridMover).step_seconds}
				"animator":
					props = {"animation": String((node as CharacterAnimator).current_animation())}
			base.merge({"type": node.get_class() if node.get_script() == null else String(node.get_script().get_global_name()),
				"name": String(entry["component"]), "role": "component", "properties": props}, true)
	return base


# --- state & actions ---------------------------------------------------------------

func add_state(state: Dictionary, scene: Node) -> void:
	state["campaign"] = {
		"active": GameSession.has_campaign,
		"wallet": GameSession.wallet.to_save_data(),
		"completed_missions": Array(GameSession.completed_missions),
		"soul_energy_spent": GameSession.soul_energy_spent,
		"loadout": GameSession.active_loadout.to_save_data() if GameSession.active_loadout != null else {},
	}
	state["mission"] = null
	state["unit"] = null
	if not is_gameplay(scene):
		return
	var mission: MissionData = scene.get("mission")
	var result_screen: MissionResultScreen = _result_screen(scene)
	state["mission"] = {
		"id": String(mission.id) if mission != null else "",
		"finished": _mission_result != null,
		"victory": _mission_result,
		"save_state": MissionResultScreen.SaveState.keys()[result_screen.save_state].to_lower() if result_screen != null else null,
	}
	var unit: Character = player_unit(scene)
	if unit != null:
		state["unit"] = {"id": player_unit_id(scene), "cell": _cell(unit.cell), "health": unit.health.current_health,
			"max_health": unit.health.max_health, "defeated": unit.is_defeated(), "moving": unit.mover.is_moving(),
			"can_act": (scene.get("controller") as PlayerUnitController).enabled}


func add_actions(actions: Array[Dictionary], scene: Node) -> void:
	if not is_gameplay(scene):
		return
	var controller: PlayerUnitController = scene.get("controller")
	var unit: Character = player_unit(scene)
	var stack: ScreenStack = scene.get("screen_stack")
	if unit != null and controller.enabled and not unit.is_defeated():
		var grid: BattleGrid = scene.get("grid")
		for direction: String in DIRECTIONS:
			if grid.can_enter(unit.cell + DIRECTIONS[direction]):
				actions.append({"action": "move", "target": player_unit_id(scene), "args": {"direction": direction}})
	if stack.is_empty() and _mission_result == null:
		actions.append({"action": "pause", "target": "scene.gameplay"})
	elif stack.top() is PauseMenu:
		actions.append({"action": "resume", "target": "scene.gameplay"})


func perform(action: String, target: String, args: Dictionary, scene: Node) -> Variant:
	match action:
		"move":
			return _move(target, args, scene)
		"pause", "resume":
			if not is_gameplay(scene):
				return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "%s is only available during a mission" % action)}
			var stack: ScreenStack = scene.get("screen_stack")
			var allowed: bool = (action == "pause" and stack.is_empty() and _mission_result == null) \
				or (action == "resume" and stack.top() is PauseMenu)
			if not allowed:
				return {"error": AutomationProtocol.error(AutomationProtocol.NOT_INTERACTABLE, "cannot %s now" % action)}
			# The real "pause" input action, so the gameplay input path is exercised.
			input.action("pause", "tap", 0)
			return {"result": {"performed": true, "via": "pause"}}
	return null


func _move(target: String, args: Dictionary, scene: Node) -> Dictionary:
	if not is_gameplay(scene):
		return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "move is only available during a mission")}
	var unit_id: String = player_unit_id(scene)
	if not target.is_empty() and target != unit_id:
		return {"error": AutomationProtocol.error(AutomationProtocol.NOT_FOUND, "'%s' is not a controllable unit" % target)}
	var steps: Array = []
	if args.get("direction") is String:
		steps.append(args["direction"])
	elif args.get("path") is Array:
		steps = args["path"]
	if steps.is_empty() or steps.size() > MAX_PATH or steps.any(func(d: Variant) -> bool: return not DIRECTIONS.has(str(d))):
		return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "args.direction (up/down/left/right) or args.path (list of directions, max %d) required" % MAX_PATH)}
	var controller: PlayerUnitController = scene.get("controller")
	var unit: Character = player_unit(scene)
	if not controller.enabled or unit.is_defeated():
		return {"error": AutomationProtocol.error(AutomationProtocol.NOT_INTERACTABLE, "the unit cannot act now (paused, defeated, or mission over)")}
	var taken: int = 0
	for direction: Variant in steps:
		if not controller.enabled or not controller.try_step(DIRECTIONS[str(direction)]):
			return {"result": {"performed": taken > 0, "steps": taken, "cell": _cell(unit.cell), "stopped": "blocked" if controller.enabled else "unit_cannot_act"}}
		taken += 1
	return {"result": {"performed": true, "steps": taken, "cell": _cell(unit.cell)}}


# --- events --------------------------------------------------------------------------

func hook_scene(scene: Node) -> void:
	_mission_result = null
	_spawned.clear()
	if not is_gameplay(scene) or events == null:
		return
	var log: AutomationEventLog = events
	var unit: Character = player_unit(scene)
	var unit_id: String = player_unit_id(scene)
	scene.connect("mission_finished", func(victory: bool) -> void:
		_mission_result = victory
		log.emit("mission.finished", {"mission": String((scene.get("mission") as MissionData).id), "victory": victory}))
	unit.cell_changed.connect(func(cell: Vector2i) -> void: log.emit("unit.moved", {"unit": unit_id, "cell": _cell(cell)}))
	unit.health.damaged.connect(func(amount: int, source: StringName) -> void:
		log.emit("unit.damaged", {"unit": unit_id, "amount": amount, "source": String(source), "health": unit.health.current_health}))
	unit.defeated.connect(func() -> void: log.emit("unit.defeated", {"unit": unit_id}))
	var controller: PlayerUnitController = scene.get("controller")
	controller.step_blocked.connect(func(cell: Vector2i) -> void: log.emit("unit.blocked", {"unit": unit_id, "cell": _cell(cell)}))
	controller.objective_reached.connect(func(cell: Vector2i) -> void: log.emit("mission.objective_reached", {"unit": unit_id, "cell": _cell(cell)}))


func is_settled(scene: Node) -> bool:
	var unit: Character = player_unit(scene)
	if unit != null and unit.mover.is_moving():
		return false
	for spawned: Character in _spawned:
		if is_instance_valid(spawned) and spawned.mover.is_moving():
			return false
	return true


# --- dev profile ---------------------------------------------------------------------

func dev(method: String, params: Dictionary, scene: Node) -> Variant:
	match method:
		"dev.teleport":
			return _teleport(str(params["target"]), params["cell"], scene)
		"dev.spawn":
			return _spawn(params, scene)
		"dev.command":
			return _command(str(params["command"]), params.get("args", {}), scene)
	return null


func _teleport(target: String, cell_value: Array, scene: Node) -> Dictionary:
	if not is_gameplay(scene):
		return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "teleport is only available during a mission")}
	var index: Dictionary = {}
	collect(index, scene)
	if not index.has(target) or index[target]["kind"] != "unit":
		return {"error": AutomationProtocol.error(AutomationProtocol.NOT_FOUND, "no unit '%s'" % target)}
	var unit: Character = index[target]["node"]
	var grid: BattleGrid = scene.get("grid")
	var cell := Vector2i(int(cell_value[0]), int(cell_value[1]))
	if not grid.relocate(unit.cell, cell):
		return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "cell %s is blocked, occupied, or outside the map" % cell)}
	unit.place_at(cell, grid.cell_to_local(cell))
	return {"result": {"cell": _cell(cell)}}


func _spawn(params: Dictionary, scene: Node) -> Dictionary:
	if not is_gameplay(scene):
		return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "spawn is only available during a mission")}
	var hero: HeroData = GameSession.ROSTER.find_hero(StringName(params["hero_id"]))
	var battalion: BattalionData = GameSession.ROSTER.find_battalion(StringName(params["battalion_id"]))
	if hero == null or battalion == null:
		return {"error": AutomationProtocol.error(AutomationProtocol.NOT_FOUND, "unknown hero_id or battalion_id")}
	var grid: BattleGrid = scene.get("grid")
	var cell := Vector2i(int(params["cell"][0]), int(params["cell"][1]))
	if not grid.can_enter(cell):
		return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "cell %s is blocked, occupied, or outside the map" % cell)}
	var unit: Character = (scene.get("character_scene") as PackedScene).instantiate()
	(scene.get("units_root") as Node).add_child(unit)
	unit.configure(UnitLoadout.new(hero, battalion).combined_stats(), hero.name_key)
	grid.place(unit, cell)
	unit.place_at(cell, grid.cell_to_local(cell))
	_spawned.append(unit)
	return {"result": {"id": "unit.spawned.%d" % _spawned.size(), "cell": _cell(cell)}}


func _command(command: String, args: Variant, scene: Node) -> Dictionary:
	match command:
		"grant_resources":
			var resource := StringName(str(args.get("resource", ""))) if args is Dictionary else &""
			var amount: int = int(args.get("amount", 0)) if args is Dictionary else 0
			if not ResourceWallet.ALL.has(resource) or amount < 1 or amount > MAX_GRANT:
				return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "args.resource must be a resource id and args.amount 1..%d" % MAX_GRANT)}
			GameSession.wallet.add(resource, amount)
			return {"result": {"resource": String(resource), "amount": GameSession.wallet.amount(resource)}}
	if not is_gameplay(scene):
		return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "%s is only available during a mission" % command)}
	var unit: Character = player_unit(scene)
	match command:
		"heal_all":
			for u: Character in [unit] + _spawned.filter(func(s: Character) -> bool: return is_instance_valid(s)):
				if not u.is_defeated():
					u.health.set_current_health(u.health.max_health)
			return {"result": {"health": unit.health.current_health}}
		"win_mission":
			scene.call("_finish", true)
			return {"result": {"finished": true}}
		"lose_mission":
			unit.health.apply_damage(unit.health.current_health, &"dev_command")
			return {"result": {"finished": true}}
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, command)}


func cleanup_session(scene: Node) -> void:
	var grid: BattleGrid = scene.get("grid") if is_gameplay(scene) else null
	for unit: Character in _spawned:
		if is_instance_valid(unit):
			if grid != null:
				grid.remove(unit.cell)
			unit.queue_free()
	_spawned.clear()


# --- helpers -------------------------------------------------------------------------

func _result_screen(scene: Node) -> MissionResultScreen:
	var stack: ScreenStack = scene.get("screen_stack")
	return stack.top() as MissionResultScreen if stack != null and stack.top() is MissionResultScreen else null


static func _cell(cell: Vector2i) -> Array:
	return [cell.x, cell.y]


static func _plain(dict: Dictionary) -> Dictionary:
	var out: Dictionary = {}
	for key: Variant in dict:
		out[String(key)] = dict[key]
	return out

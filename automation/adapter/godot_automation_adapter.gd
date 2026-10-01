class_name GodotAutomationAdapter
extends AutomationAdapter
## Godot implementation of the automation contract.
##
## Generic layer: scenes (route ids), Controls tagged with the
## `automation_id` metadata, focus, modality, screenshots, performance, and
## input. Game semantics come from AutomationProvider instances.
## Reads existing game services (SceneRouter, GameSession, SaveService,
## Settings, InputMethod); it never keeps a parallel copy of game state.

const META_ID: StringName = &"automation_id"
const META_TAGS: StringName = &"automation_tags"
const SEMANTIC_ACTIONS: Dictionary[String, bool] = {
	"activate": true, "select": true, "toggle": true, "confirm": true, "cancel": true,
	"move": true, "pause": true, "resume": true,
	# Defined by the protocol; this game has no such systems yet.
	"interact": false, "equip": false, "use": false, "choose_dialogue": false,
}

var providers: Array[AutomationProvider] = []
var input: AutomationInputDriver
var _tree: SceneTree
var _events: AutomationEventLog
var _connections: Array[Array] = []


func _init(tree: SceneTree, input_driver: AutomationInputDriver) -> void:
	_tree = tree
	input = input_driver
	providers.append(UtopiaAutomationProvider.new())
	for provider: AutomationProvider in providers:
		provider.input = input_driver


func describe() -> Dictionary:
	var info: Dictionary = Engine.get_version_info()
	return {
		"engine": "godot",
		"engine_version": "%d.%d.%d" % [info["major"], info["minor"], info["patch"]],
		"adapter": "godot-utopia",
		"game": ProjectSettings.get_setting("application/config/name"),
		"game_version": ProjectSettings.get_setting("application/config/version"),
		"build": "debug" if OS.is_debug_build() else "release",
		"semantic_actions": SEMANTIC_ACTIONS,
		"input_devices": ["keyboard", "mouse", "gamepad", "touch", "action"],
		"screenshots": screenshots_available(),
		"logical_resolution": [1280, 720],
	}


func screenshots_available() -> bool:
	return DisplayServer.get_name() != "headless"


func scene() -> Node:
	return _tree.current_scene


func scene_info() -> Dictionary:
	return {
		"route": String(SceneRouter.current_route),
		"scene_id": "scene.%s" % SceneRouter.current_route if not String(SceneRouter.current_route).is_empty() else "scene.unknown",
		"transitioning": SceneRouter.is_transitioning,
		"paused": _tree.paused,
		"screens": _screen_ids(),
		"focused": _focused_id(),
	}


# --- index ------------------------------------------------------------------

## id -> {"kind": String, "node": Node, "provider": AutomationProvider|null}
func build_index() -> Dictionary:
	var index: Dictionary = {}
	var current: Node = scene()
	if current == null:
		return index
	var route: String = String(SceneRouter.current_route)
	index["scene.%s" % (route if not route.is_empty() else "unknown")] = {"kind": "scene", "node": current, "provider": null}
	for node: Node in find_nodes(current, func(n: Node) -> bool: return n is Control and n.has_meta(META_ID)):
		if node.is_inside_tree() and not node.is_queued_for_deletion():
			var id: String = str(node.get_meta(META_ID))
			var unique: String = id
			var n: int = 2
			while index.has(unique):
				unique = "%s#%d" % [id, n]
				n += 1
			index[unique] = {"kind": "screen" if node is UiScreen else "control", "node": node, "provider": null}
	for provider: AutomationProvider in providers:
		provider.collect(index, current)
	return index


func query(filters: Dictionary) -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	var index: Dictionary = build_index()
	var limit: int = int(filters.get("limit", 200))
	var ids: Array = index.keys()
	ids.sort()
	for id: String in ids:
		var desc: Dictionary = _describe(id, index[id], false)
		if filters.has("role") and desc.get("role") != filters["role"]:
			continue
		if filters.has("type") and desc.get("type") != filters["type"]:
			continue
		if filters.has("tag") and not (desc.get("tags", []) as Array).has(filters["tag"]):
			continue
		if filters.has("id_prefix") and not id.begins_with(str(filters["id_prefix"])):
			continue
		if filters.get("visible_only", false) and not desc.get("visible", false):
			continue
		if filters.get("interactable_only", false) and not desc.get("interactable", false):
			continue
		out.append(desc)
		if out.size() >= limit:
			break
	return out


func entity(id: String, deep: bool) -> Variant:
	var index: Dictionary = build_index()
	if not index.has(id):
		return null
	return _describe(id, index[id], deep)


func _describe(id: String, entry: Dictionary, deep: bool) -> Dictionary:
	if entry.get("provider") != null:
		var desc: Dictionary = (entry["provider"] as AutomationProvider).describe(id, entry, deep)
		desc["id"] = id
		return desc
	var node: Node = entry["node"]
	if entry["kind"] == "scene":
		return {"id": id, "type": "Scene", "name": String(SceneRouter.current_route), "role": "scene",
			"tags": [], "visible": true, "enabled": true, "interactable": false,
			"components": [], "actions": [], "properties": {"screens": _screen_ids()}}
	return _describe_control(id, node as Control, deep)


func _describe_control(id: String, control: Control, deep: bool) -> Dictionary:
	var role: String = "group"
	var actions: Array[String] = []
	var props: Dictionary = {}
	if control is UiScreen:
		role = "screen"
		props["cancel_closes"] = (control as UiScreen).cancel_closes
		props["top"] = _screen_ids().size() > 0 and _screen_ids().back() == id
	elif control is OptionButton:
		var option := control as OptionButton
		role = "combobox"
		actions.append("select")
		props["selected"] = option.selected
		props["item_count"] = option.item_count
		var items: Array = []
		for i: int in option.item_count:
			items.append({"index": i, "value": str(option.get_item_metadata(i)) if option.get_item_metadata(i) != null else str(i),
				"text_key": option.get_item_text(i), "text": control.tr(option.get_item_text(i)) if control.can_auto_translate() else option.get_item_text(i)})
		props["items"] = items
	elif control is CheckButton or control is CheckBox:
		role = "switch"
		actions.append_array(["toggle", "activate"])
		props["checked"] = (control as BaseButton).button_pressed
	elif control is BaseButton:
		role = "button"
		actions.append("activate")
		if control is GameButton:
			props["state"] = GameButton.State.keys()[(control as GameButton).get_state()].to_lower()
	elif control is ProgressBar:
		role = "progressbar"
		props["value"] = (control as ProgressBar).value
		props["max"] = (control as ProgressBar).max_value
	elif control is Label:
		role = "label"
	var text_key: String = ""
	var shown: String = ""
	if "text" in control and control.get("text") is String:
		var raw: String = control.get("text")
		shown = control.tr(raw) if control.can_auto_translate() else raw
		text_key = raw if control.can_auto_translate() else ""
	var tags: Array = []
	if control.has_meta(META_TAGS):
		tags.append_array(Array(control.get_meta(META_TAGS)))
	if control.theme_type_variation == &"PrimaryButton":
		tags.append("primary")
	elif control.theme_type_variation == &"DangerButton":
		tags.append("danger")
	var rect: Rect2 = control.get_global_rect()
	var desc: Dictionary = {
		"id": id,
		"type": control.get_class(),
		"name": String(control.name),
		"role": role,
		"tags": tags,
		"visible": control.is_visible_in_tree(),
		"enabled": not (control is BaseButton and (control as BaseButton).disabled),
		"interactable": is_interactable(control),
		"focused": control.has_focus(),
		"position": {"x": roundi(rect.get_center().x), "y": roundi(rect.get_center().y)},
		"rect": {"x": roundi(rect.position.x), "y": roundi(rect.position.y), "w": roundi(rect.size.x), "h": roundi(rect.size.y)},
		"components": [],
		"actions": actions if is_interactable(control) else [],
		"text_key": text_key,
		"text": shown,
		"properties": props,
	}
	if deep:
		desc["properties"]["theme_type_variation"] = String(control.theme_type_variation)
		desc["properties"]["focus_mode"] = control.get_focus_mode_with_override()
		desc["properties"]["layout_rtl"] = control.is_layout_rtl()
	return desc


## A control a player could use right now: visible, enabled, and not blocked
## by a modal screen (ScreenStack disables focus beneath the top screen).
static func is_interactable(control: Control) -> bool:
	if not control.is_visible_in_tree():
		return false
	if control is BaseButton and (control as BaseButton).disabled:
		return false
	if control is BaseButton or control is Range:
		return control.get_focus_mode_with_override() != Control.FOCUS_NONE
	return false


# --- state & actions -----------------------------------------------------------

func state() -> Dictionary:
	var out: Dictionary = scene_info()
	out["locale"] = TranslationServer.get_locale()
	out["input_method"] = "gamepad" if InputMethod.current == InputMethodService.Method.GAMEPAD else "keyboard_mouse"
	out["held_inputs"] = Array(input.held())
	out["save"] = {"has_save": SaveService.has_save()}
	for provider: AutomationProvider in providers:
		provider.add_state(out, scene())
	return out


func available_actions() -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	var index: Dictionary = build_index()
	var ids: Array = index.keys()
	ids.sort()
	for id: String in ids:
		var entry: Dictionary = index[id]
		if entry["kind"] != "control":
			continue
		var control: Control = entry["node"]
		if not is_interactable(control):
			continue
		if control is OptionButton:
			out.append({"action": "select", "target": id})
		elif control is CheckButton or control is CheckBox:
			out.append({"action": "toggle", "target": id})
		elif control is BaseButton:
			out.append({"action": "activate", "target": id})
	var top: UiScreen = _top_screen()
	if top != null and top.cancel_closes:
		out.append({"action": "cancel", "target": _id_of(top)})
	var focused: String = _focused_id()
	if not focused.is_empty():
		out.append({"action": "confirm", "target": focused})
	for provider: AutomationProvider in providers:
		provider.add_actions(out, scene())
	return out


func perform(action: String, target: String, args: Dictionary) -> Dictionary:
	if not SEMANTIC_ACTIONS.has(action) or not SEMANTIC_ACTIONS[action]:
		return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "action '%s' is not supported by this game" % action)}
	if SceneRouter.is_transitioning:
		return {"error": AutomationProtocol.error(AutomationProtocol.BUSY, "scene transition in progress")}
	for provider: AutomationProvider in providers:
		var handled: Variant = provider.perform(action, target, args, scene())
		if handled != null:
			return handled
	match action:
		"confirm":
			input.action("ui_accept", "tap", 0)
			return {"result": {"performed": true, "via": "ui_accept"}}
		"cancel":
			input.action("ui_cancel", "tap", 0)
			return {"result": {"performed": true, "via": "ui_cancel"}}
		"activate", "select", "toggle":
			return _perform_control(action, target, args)
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "action '%s' is not available here" % action)}


func _perform_control(action: String, target: String, args: Dictionary) -> Dictionary:
	if target.is_empty():
		return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "target required")}
	var index: Dictionary = build_index()
	if not index.has(target) or index[target]["kind"] != "control":
		return {"error": AutomationProtocol.error(AutomationProtocol.NOT_FOUND, "no control '%s'" % target)}
	var control: Control = index[target]["node"]
	if not is_interactable(control):
		return {"error": AutomationProtocol.error(AutomationProtocol.NOT_INTERACTABLE, "'%s' is hidden, disabled, or behind a modal screen" % target)}
	match action:
		"activate":
			if control is OptionButton:
				return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "use select for '%s'" % target)}
			if control is CheckButton or control is CheckBox:
				(control as BaseButton).button_pressed = not (control as BaseButton).button_pressed
			else:
				control.grab_focus()
				(control as BaseButton).pressed.emit()
			return {"result": {"performed": true}}
		"toggle":
			if not (control is CheckButton or control is CheckBox):
				return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "'%s' is not a toggle" % target)}
			var button := control as BaseButton
			button.button_pressed = bool(args.get("on", not button.button_pressed))
			return {"result": {"performed": true, "checked": button.button_pressed}}
		"select":
			if not control is OptionButton:
				return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "'%s' is not a combobox" % target)}
			var option := control as OptionButton
			var index_to_select: int = -1
			if args.has("value"):
				for i: int in option.item_count:
					if str(option.get_item_metadata(i)) == str(args["value"]):
						index_to_select = i
			elif args.get("index") is float or args.get("index") is int:
				index_to_select = int(args["index"])
			if index_to_select < 0 or index_to_select >= option.item_count:
				return {"error": AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "args.index or args.value must name an existing item")}
			option.select(index_to_select)
			option.item_selected.emit(index_to_select)
			return {"result": {"performed": true, "selected": index_to_select}}
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, action)}


# --- observation ------------------------------------------------------------------

func screenshot(max_width: int) -> Dictionary:
	if not screenshots_available():
		return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "screenshots need a renderer (run without --headless, e.g. under Xvfb)")}
	await RenderingServer.frame_post_draw
	var image: Image = _tree.root.get_texture().get_image()
	if image == null or image.is_empty():
		return {"error": AutomationProtocol.error(AutomationProtocol.INTERNAL_ERROR, "frame capture failed")}
	if max_width > 0 and image.get_width() > max_width:
		image.resize(max_width, roundi(image.get_height() * float(max_width) / image.get_width()), Image.INTERPOLATE_BILINEAR)
	var png: PackedByteArray = image.save_png_to_buffer()
	if png.size() > AutomationProtocol.MAX_SCREENSHOT_BYTES:
		return {"error": AutomationProtocol.error(AutomationProtocol.PAYLOAD_TOO_LARGE, "screenshot too large; pass max_width")}
	return {"result": {"width": image.get_width(), "height": image.get_height(), "png_base64": Marshalls.raw_to_base64(png)}}


func perf() -> Dictionary:
	return {
		"fps": Performance.get_monitor(Performance.TIME_FPS),
		"process_ms": Performance.get_monitor(Performance.TIME_PROCESS) * 1000.0,
		"physics_ms": Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS) * 1000.0,
		"nodes": Performance.get_monitor(Performance.OBJECT_NODE_COUNT),
		"orphan_nodes": Performance.get_monitor(Performance.OBJECT_ORPHAN_NODE_COUNT),
		"objects": Performance.get_monitor(Performance.OBJECT_COUNT),
		"static_memory_mb": Performance.get_monitor(Performance.MEMORY_STATIC) / 1048576.0,
		"frame": Engine.get_process_frames(),
	}


func is_settled() -> bool:
	if SceneRouter.is_transitioning or input.has_holds():
		return false
	for provider: AutomationProvider in providers:
		if not provider.is_settled(scene()):
			return false
	return true


# --- lifecycle helpers ----------------------------------------------------------

func reset(route: String) -> Dictionary:
	input.release_all()
	cleanup_session()
	_tree.paused = false
	GameSession.has_campaign = false
	GameSession.wallet = ResourceWallet.new()
	for slot: String in SaveService.list_saves():
		SaveService.delete_save(slot)
	var target: StringName = Routes.GAMEPLAY if route == "gameplay" else Routes.MAIN_MENU
	if target == Routes.GAMEPLAY:
		GameSession.start_new_campaign()
	if not await _goto(target):
		return {"error": AutomationProtocol.error(AutomationProtocol.BUSY, "could not change scene")}
	return {"result": {"route": String(target)}}


func capture_checkpoint() -> Dictionary:
	return {
		"route": String(SceneRouter.current_route),
		"campaign": GameSession.to_save_data() if GameSession.has_campaign else null,
	}


func restore_checkpoint(snapshot: Dictionary) -> Dictionary:
	input.release_all()
	_tree.paused = false
	if snapshot["campaign"] is Dictionary:
		if not GameSession.load_save_data(snapshot["campaign"]):
			return {"error": AutomationProtocol.error(AutomationProtocol.INTERNAL_ERROR, "checkpoint data no longer matches game content")}
	else:
		GameSession.has_campaign = false
	var route := StringName(snapshot["route"])
	if not SceneRouter.has_route(route) or route == Routes.BOOTSTRAP:
		route = Routes.MAIN_MENU
	if not await _goto(route):
		return {"error": AutomationProtocol.error(AutomationProtocol.BUSY, "could not change scene")}
	return {"result": {"route": String(route)}}


func apply_fixture(fixture: Dictionary, write_save: bool) -> Dictionary:
	input.release_all()
	_tree.paused = false
	var campaign: Variant = fixture.get("campaign")
	if campaign is Dictionary:
		if not GameSession.load_save_data(campaign):
			return {"error": AutomationProtocol.error(AutomationProtocol.INTERNAL_ERROR, "fixture references unknown content")}
		if write_save and not SaveService.save_game(campaign).is_ok():
			return {"error": AutomationProtocol.error(AutomationProtocol.INTERNAL_ERROR, "fixture save failed")}
	else:
		GameSession.has_campaign = false
	var route := StringName(fixture.get("route", "main_menu"))
	if route == Routes.GAMEPLAY and not GameSession.has_campaign:
		GameSession.start_new_campaign()
	if not await _goto(route if SceneRouter.has_route(route) else Routes.MAIN_MENU):
		return {"error": AutomationProtocol.error(AutomationProtocol.BUSY, "could not change scene")}
	return {"result": {"fixture": fixture.get("id", ""), "route": String(SceneRouter.current_route)}}


func dev(method: String, params: Dictionary) -> Dictionary:
	for provider: AutomationProvider in providers:
		var handled: Variant = provider.dev(method, params, scene())
		if handled != null:
			return handled
	return {"error": AutomationProtocol.error(AutomationProtocol.UNSUPPORTED, "%s is not available in this scene" % method)}


func connect_events(log: AutomationEventLog) -> void:
	_events = log
	for provider: AutomationProvider in providers:
		provider.events = log
	_link(SceneRouter.route_changing, func(from: StringName, to: StringName) -> void:
		input.release_all() # held input never leaks into the next scene
		log.emit("route.changing", {"from": String(from), "to": String(to)}))
	_link(SceneRouter.route_changed, func(route: StringName) -> void:
		log.emit("route.changed", {"route": String(route)})
		_hook_scene())
	_link(SceneRouter.route_failed, func(route: StringName) -> void: log.emit("route.failed", {"route": String(route)}))
	_link(SaveService.save_finished, func(slot: String, result: SaveResult) -> void:
		log.emit("save.finished", {"slot": slot, "ok": result.is_ok(), "status": SaveResult.Status.keys()[result.status].to_lower()}))
	_link(SaveService.load_finished, func(slot: String, result: SaveResult) -> void:
		log.emit("load.finished", {"slot": slot, "ok": result.is_ok(), "status": SaveResult.Status.keys()[result.status].to_lower()}))
	_link(InputMethod.method_changed, func(method: InputMethodService.Method) -> void:
		log.emit("input.method_changed", {"method": "gamepad" if method == InputMethodService.Method.GAMEPAD else "keyboard_mouse"}))
	_link(Settings.locale_changed, func(locale: String) -> void: log.emit("locale.changed", {"locale": locale}))
	_link(GameSession.campaign_started, func() -> void: log.emit("campaign.started", {}))
	_link(GameSession.campaign_loaded, func() -> void: log.emit("campaign.loaded", {}))
	_link(input.hold_released, func(hold_id: String) -> void: log.emit("input.released", {"hold": hold_id}))
	_hook_scene()


func cleanup_session() -> void:
	for provider: AutomationProvider in providers:
		provider.cleanup_session(scene())


func shutdown() -> void:
	for pair: Array in _connections:
		var sig: Signal = pair[0]
		if sig.is_connected(pair[1]):
			sig.disconnect(pair[1])
	_connections.clear()


func _link(sig: Signal, callable: Callable) -> void:
	sig.connect(callable)
	_connections.append([sig, callable])


func _hook_scene() -> void:
	var current: Node = scene()
	if current == null or _events == null:
		return
	for stack: ScreenStack in _stacks():
		stack.screen_opened.connect(func(s: UiScreen) -> void: _events.emit("screen.opened", {"screen": _id_of(s)}))
		stack.screen_closed.connect(func(s: UiScreen) -> void: _events.emit("screen.closed", {"screen": _id_of(s)}))
	for provider: AutomationProvider in providers:
		provider.hook_scene(current)


func _goto(route: StringName) -> bool:
	while SceneRouter.is_transitioning:
		await _tree.process_frame
	return await SceneRouter.goto(route)


func _stacks() -> Array[ScreenStack]:
	var out: Array[ScreenStack] = []
	for node: Node in find_nodes(scene(), func(n: Node) -> bool: return n is ScreenStack):
		out.append(node as ScreenStack)
	return out


## Depth-first search that also matches script classes (Node.find_children
## only matches engine classes).
static func find_nodes(root: Node, predicate: Callable) -> Array[Node]:
	var out: Array[Node] = []
	if root == null:
		return out
	var pending: Array[Node] = [root]
	while not pending.is_empty():
		var node: Node = pending.pop_front()
		if predicate.call(node):
			out.append(node)
		var children: Array[Node] = node.get_children()
		for i: int in range(children.size() - 1, -1, -1):
			pending.push_front(children[i])
	return out


func _top_screen() -> UiScreen:
	for stack: ScreenStack in _stacks():
		if not stack.is_empty():
			return stack.top()
	return null


func _screen_ids() -> Array:
	var ids: Array = []
	for node: Node in find_nodes(scene(), func(n: Node) -> bool: return n is UiScreen):
		if node.is_inside_tree() and not node.is_queued_for_deletion():
			ids.append(_id_of(node))
	return ids


func _focused_id() -> String:
	var focused: Control = _tree.root.gui_get_focus_owner()
	return _id_of(focused) if focused != null else ""


func _id_of(node: Node) -> String:
	return str(node.get_meta(META_ID)) if node != null and node.has_meta(META_ID) else ""

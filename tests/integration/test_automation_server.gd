extends AutomationTestCase
## Server behaviour through dispatch() and through real HTTP/WebSocket sockets.


func _goto(route: StringName) -> void:
	await SceneRouter.goto(route)
	await wait_until(func() -> bool: return not SceneRouter.is_transitioning)


# --- protocol & validation ----------------------------------------------------------

func test_every_method_has_a_handler() -> void:
	start_server()
	for method: String in AutomationProtocol.methods():
		assert_true(server.has_method("_h_" + method.replace(".", "_")), method)


func test_malformed_requests() -> void:
	start_server()
	for bad: Variant in [[], "x", {"method": "state.get"}, {"jsonrpc": "2.0"}, {"jsonrpc": "2.0", "method": 5},
			{"jsonrpc": "2.0", "method": "state.get", "params": []}, {"jsonrpc": "2.0", "id": {}, "method": "state.get"}]:
		var response: Dictionary = await server.dispatch(bad, {"transport": "test"})
		assert_eq(error_kind(response), "invalid_request", str(bad))
	assert_eq(error_kind(await rpc("no.such_method")), "method_not_found")
	assert_eq(error_kind(await rpc("entity.get", {})), "invalid_params", "missing id")
	assert_eq(error_kind(await rpc("entity.get", {"id": "x", "evil": 1})), "invalid_params", "unknown field")
	assert_eq(error_kind(await rpc("wait.until", {"condition": {"source": "state", "op": "eval", "path": "x", "value": 1}})), "invalid_params", "bad predicate")
	assert_eq(error_kind(await rpc("input.key", {"key": "NotAKey"})), "invalid_params")


func test_integer_ids_are_echoed() -> void:
	start_server()
	var response: Dictionary = await server.dispatch({"jsonrpc": "2.0", "id": 7.0, "method": "scene.current"}, {})
	assert_eq(typeof(response["id"]), TYPE_INT)


func test_qa_profile_rejects_dev_capabilities() -> void:
	start_server("qa")
	for method: String in ["dev.teleport", "dev.spawn", "dev.command"]:
		var params: Dictionary = {"target": "unit.x", "cell": [1, 1], "hero_id": "a", "battalion_id": "b", "command": "heal_all"}
		var allowed: Dictionary = {}
		for key: String in AutomationProtocol.methods()[method]["params"]["properties"]:
			if params.has(key):
				allowed[key] = params[key]
		var response: Dictionary = await rpc(method, allowed)
		assert_eq(error_kind(response), "forbidden", method)
	assert_eq(error_kind(await rpc("entity.get", {"id": "scene.main_menu", "deep": true})), "forbidden", "deep inspection is dev-only")


func test_rate_limiting() -> void:
	start_server()
	server.set("_rate", AutomationRateLimiter.new(2, 0))
	assert_true((await rpc("scene.current")).has("result"))
	assert_true((await rpc("scene.current")).has("result"))
	assert_eq(error_kind(await rpc("scene.current")), "rate_limited")


func test_capabilities_adapt_to_build() -> void:
	start_server()
	var caps: Dictionary = result_of(await rpc("protocol.capabilities"))
	assert_eq(caps["protocol"], AutomationProtocol.NAME)
	assert_false((caps["capabilities"] as Array).has("observe.screenshot"), "headless runs cannot take screenshots")
	assert_false((caps["methods"] as Array).has("dev.teleport"), "qa does not list dev methods")
	assert_false(caps["engine"]["semantic_actions"]["equip"], "unsupported actions are declared")
	assert_eq(error_kind(await rpc("observe.screenshot")), "unsupported")
	assert_eq(error_kind(await rpc("action.perform", {"action": "equip"})), "unsupported")


# --- discovery & stable ids -------------------------------------------------------------

func test_stable_ids_on_the_title_screen() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	var first: Array = (result_of(await rpc("tree.query")) as Dictionary)["entities"].map(func(e: Dictionary) -> String: return e["id"])
	await _goto(Routes.MAIN_MENU)
	var second: Array = (result_of(await rpc("tree.query")) as Dictionary)["entities"].map(func(e: Dictionary) -> String: return e["id"])
	assert_eq(first, second, "ids are identical across scene reloads")
	for id: String in ["scene.main_menu", "screen.main_menu", "main_menu.new_game", "main_menu.settings", "main_menu.quit", "resource.gold"]:
		assert_contains(first, id)
	var button: Dictionary = result_of(await rpc("entity.get", {"id": "main_menu.new_game"}))
	assert_eq(button["role"], "button")
	assert_eq(button["text_key"], "MAIN_MENU_NEW_GAME", "language-independent key")
	assert_eq(button["text"], "New Campaign")
	assert_contains(button["tags"], "primary")
	assert_eq(button["actions"], ["activate"])
	assert_eq(error_kind(await rpc("entity.get", {"id": "no.such_entity"})), "not_found")


func test_every_button_in_shipped_screens_has_an_automation_id() -> void:
	for path: String in ["res://scenes/main_menu/main_menu.tscn", "res://scenes/menus/pause/pause_menu.tscn",
			"res://scenes/menus/settings/settings_screen.tscn", "res://core/ui/screens/confirm_dialog.tscn",
			"res://scenes/menus/mission_result/mission_result_screen.tscn", "res://scenes/bootstrap/bootstrap.tscn"]:
		var root: Node = (load(path) as PackedScene).instantiate()
		var ids: Dictionary = {}
		for node: Node in GodotAutomationAdapter.find_nodes(root, func(n: Node) -> bool: return n is BaseButton or n is UiScreen):
			assert_true(node.has_meta(GodotAutomationAdapter.META_ID), "%s: %s has no automation_id" % [path.get_file(), node.name])
			var id: String = str(node.get_meta(GodotAutomationAdapter.META_ID, ""))
			assert_false(ids.has(id), "%s: duplicate id %s" % [path.get_file(), id])
			ids[id] = true
		root.free()


func test_gameplay_entities_and_state() -> void:
	start_server()
	GameSession.start_new_campaign()
	await _goto(Routes.GAMEPLAY)
	var ids: Array = (result_of(await rpc("tree.query")) as Dictionary)["entities"].map(func(e: Dictionary) -> String: return e["id"])
	for id: String in ["unit.hero_deserter", "unit.hero_deserter/health", "grid.map_training_ground", "mission.mission_first_spark", "hud.objective"]:
		assert_contains(ids, id)
	var unit: Dictionary = result_of(await rpc("entity.get", {"id": "unit.hero_deserter"}))
	assert_eq(unit["cell"], [1, 10])
	assert_eq(unit["role"], "unit")
	var state: Dictionary = result_of(await rpc("state.get"))
	assert_eq(state["route"], "gameplay")
	assert_eq(state["unit"]["cell"], [1, 10])
	assert_eq(state["mission"]["finished"], false)
	var actions: Array = (result_of(await rpc("actions.available")) as Dictionary)["actions"]
	assert_true(actions.any(func(a: Dictionary) -> bool: return a["action"] == "move" and a["args"]["direction"] == "left"))
	assert_true(actions.any(func(a: Dictionary) -> bool: return a["action"] == "pause"))


# --- semantic actions -------------------------------------------------------------------

func test_semantic_actions_respect_modals() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	assert_true(result_of(await rpc("action.perform", {"action": "activate", "target": "main_menu.settings"}))["performed"])
	var blocked: Dictionary = await rpc("action.perform", {"action": "activate", "target": "main_menu.new_game"})
	assert_eq(error_kind(blocked), "not_interactable", "cannot click through the settings modal")
	var select: Dictionary = result_of(await rpc("action.perform", {"action": "select", "target": "settings.language", "args": {"value": "de"}}))
	assert_eq(select["selected"], 1)
	assert_eq(TranslationServer.get_locale(), "de")
	var entity: Dictionary = result_of(await rpc("entity.get", {"id": "settings.back"}))
	assert_eq(entity["text_key"], "UI_BACK", "ids and keys stay stable when the language changes")
	assert_eq(entity["text"], "Zurück")
	result_of(await rpc("action.perform", {"action": "cancel"}))
	await wait_frames(2)
	assert_eq(result_of(await rpc("scene.current"))["screens"], [])
	Settings.set_locale("en")


func test_move_follows_game_rules() -> void:
	start_server()
	GameSession.start_new_campaign()
	await _goto(Routes.GAMEPLAY)
	var moved: Dictionary = result_of(await rpc("action.perform", {"action": "move", "args": {"direction": "left"}}))
	assert_eq(moved["cell"], [0, 10])
	var blocked: Dictionary = result_of(await rpc("action.perform", {"action": "move", "args": {"direction": "left"}}))
	assert_eq(blocked["performed"], false)
	assert_eq(blocked["stopped"], "blocked")
	assert_eq(error_kind(await rpc("action.perform", {"action": "move", "args": {"direction": "sideways"}})), "invalid_params")
	assert_eq(error_kind(await rpc("action.perform", {"action": "move", "target": "unit.enemy", "args": {"direction": "up"}})), "not_found")


func test_actions_during_transition_are_busy() -> void:
	start_server()
	SceneRouter.goto(Routes.MAIN_MENU)
	assert_eq(error_kind(await rpc("action.perform", {"action": "confirm"})), "busy")
	await wait_until(func() -> bool: return not SceneRouter.is_transitioning)


# --- waits, events, assertions -----------------------------------------------------------

func test_wait_frames_is_exact() -> void:
	start_server()
	var before: int = Engine.get_process_frames()
	result_of(await rpc("wait.frames", {"count": 5}))
	assert_eq(Engine.get_process_frames() - before, 5)


func test_wait_until_and_timeout() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	SceneRouter.goto(Routes.GAMEPLAY) if GameSession.has_campaign else (func() -> void:
		GameSession.start_new_campaign()
		SceneRouter.goto(Routes.GAMEPLAY)).call()
	var met: Dictionary = result_of(await rpc("wait.until", {"condition": {"source": "scene", "path": "route", "op": "eq", "value": "gameplay"}, "timeout_ms": 5000}))
	assert_true(met["met"])
	var timeout: Dictionary = await rpc("wait.until", {"condition": {"source": "state", "path": "unit.cell", "op": "eq", "value": [5, 5]}, "timeout_ms": 50})
	assert_eq(error_kind(timeout), "timeout")
	assert_eq(timeout["error"]["data"]["actual"], [1, 10], "timeouts report the actual value")


func test_wait_event_and_scene_transition_events() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	var seq: int = server.events.last_seq
	SceneRouter.goto(Routes.MAIN_MENU)
	var waited: Dictionary = result_of(await rpc("wait.event", {"type": "route.changed", "match": {"route": "main_menu"}, "after_seq": seq, "timeout_ms": 5000}))
	assert_eq(waited["event"]["data"]["route"], "main_menu")
	var events: Array = (result_of(await rpc("observe.events", {"since_seq": seq})) as Dictionary)["events"]
	var types: Array = events.map(func(e: Dictionary) -> String: return e["type"])
	assert_true(types.find("route.changing") < types.find("route.changed"))


func test_assert_reports_without_raising() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	var passed: Dictionary = result_of(await rpc("assert.check", {"condition": {"source": "scene", "path": "route", "op": "eq", "value": "main_menu"}}))
	assert_true(passed["passed"])
	var failed: Dictionary = result_of(await rpc("assert.check", {"condition": {"source": "entity", "id": "main_menu.new_game", "path": "enabled", "op": "eq", "value": false}, "message": "m"}))
	assert_false(failed["passed"])
	assert_eq(failed["actual"], true)
	assert_eq(server.session.assertion_failures.size(), 1)


# --- lifecycle --------------------------------------------------------------------------

func test_held_input_released_on_session_close() -> void:
	start_server()
	result_of(await rpc("input.key", {"key": "Right", "mode": "down"}))
	assert_true(Input.is_key_pressed(KEY_RIGHT))
	result_of(await rpc("session.close"))
	assert_false(Input.is_key_pressed(KEY_RIGHT))
	assert_null(server.session)


func test_held_input_released_on_scene_change() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	result_of(await rpc("input.gamepad", {"button": "a", "mode": "down"}))
	assert_true(Input.is_joy_button_pressed(0, JOY_BUTTON_A))
	await _goto(Routes.MAIN_MENU)
	assert_false(Input.is_joy_button_pressed(0, JOY_BUTTON_A))


func test_idle_timeout_cleans_up() -> void:
	start_server("qa", {"idle_timeout_ms": 100})
	result_of(await rpc("input.key", {"key": "Up", "mode": "down"}))
	var first_session: String = server.session.id
	await wait_seconds(0.2)
	assert_null(server.session, "idle session closed")
	assert_false(Input.is_key_pressed(KEY_UP))
	result_of(await rpc("session.hello"))
	assert_ne(server.session.id, first_session, "a reconnecting client gets a fresh session")


func test_shutdown_releases_everything_and_restores_player_files() -> void:
	var original: SaveBackend = SaveService.backend
	start_server()
	assert_true(SaveService.backend is JsonFileSaveBackend, "automation uses an isolated save folder")
	assert_ne(Settings.settings_path, "user://test_settings.cfg")
	result_of(await rpc("input.key", {"key": "Down", "mode": "down"}))
	var port: int = server.http.port()
	server.stop()
	assert_false(Input.is_key_pressed(KEY_DOWN))
	assert_eq(SaveService.backend, original)
	assert_eq(Settings.settings_path, "user://test_settings.cfg")
	assert_eq(error_kind(await rpc("scene.current")), "busy", "no requests after stop")
	var probe := TCPServer.new()
	assert_eq(probe.listen(port, "127.0.0.1"), OK, "socket released")
	probe.stop()


func test_reset_checkpoint_fixture_and_recording() -> void:
	start_server()
	result_of(await rpc("recording.start"))
	result_of(await rpc("fixture.load", {"fixture": "campaign_after_first_mission", "write_save": true}))
	assert_eq(GameSession.wallet.amount(ResourceWallet.GOLD), 50)
	assert_true(SaveService.has_save())
	result_of(await rpc("checkpoint.create", {"name": "rich"}))
	result_of(await rpc("session.reset"))
	assert_false(GameSession.has_campaign)
	assert_false(SaveService.has_save(), "reset removes automation saves")
	result_of(await rpc("checkpoint.create", {"name": "empty"}))
	result_of(await rpc("checkpoint.restore", {"name": "empty"}))
	assert_eq(error_kind(await rpc("checkpoint.restore", {"name": "rich"})), "not_found", "reset clears checkpoints")
	assert_eq(error_kind(await rpc("fixture.load", {"fixture": "../../etc/passwd"})), "invalid_params", "fixture ids only")
	assert_eq(error_kind(await rpc("fixture.load", {"fixture": "not_approved"})), "not_found")
	var entries: Array = (result_of(await rpc("recording.stop")) as Dictionary)["entries"]
	assert_true(entries.any(func(e: Dictionary) -> bool: return e["kind"] == "request" and e["data"]["method"] == "fixture.load"))
	assert_true(entries.any(func(e: Dictionary) -> bool: return e["kind"] == "event"))


func test_dev_profile_tools_and_cleanup() -> void:
	start_server("dev")
	GameSession.start_new_campaign()
	await _goto(Routes.GAMEPLAY)
	assert_eq(result_of(await rpc("dev.teleport", {"target": "unit.hero_deserter", "cell": [9, 1]}))["cell"], [9, 1])
	assert_eq(error_kind(await rpc("dev.teleport", {"target": "unit.hero_deserter", "cell": [4, 3]})), "invalid_params", "walls stay walls")
	var spawned: Dictionary = result_of(await rpc("dev.spawn", {"hero_id": "hero_deserter", "battalion_id": "battalion_militia", "cell": [2, 2]}))
	assert_eq(spawned["id"], "unit.spawned.1")
	assert_not_null(result_of(await rpc("entity.get", {"id": "unit.spawned.1", "deep": true}))["properties"].get("stats"))
	result_of(await rpc("dev.command", {"command": "grant_resources", "args": {"resource": "gold", "amount": 5}}))
	assert_eq(GameSession.wallet.amount(ResourceWallet.GOLD), 5)
	assert_eq(error_kind(await rpc("dev.command", {"command": "grant_resources", "args": {"resource": "gold", "amount": 999999}})), "invalid_params")
	assert_eq(error_kind(await rpc("dev.command", {"command": "rm -rf"})), "invalid_params", "allow-listed commands only")
	result_of(await rpc("session.close"))
	assert_eq((tree.current_scene.get("grid") as BattleGrid).occupant_at(Vector2i(2, 2)), null, "spawned units removed with the session")
	result_of(await rpc("dev.command", {"command": "win_mission"}))
	assert_true(result_of(await rpc("state.get"))["mission"]["finished"])


# --- transports -------------------------------------------------------------------------

func test_http_transport_security() -> void:
	start_server()
	var port: int = server.http.port()
	var health: Dictionary = await http("GET /v1/health HTTP/1.1\r\nHost: 127.0.0.1:%d\r\n\r\n" % port)
	assert_eq(health["status"], 200)
	assert_eq(health["body"]["ok"], true)
	var body: String = JSON.stringify({"jsonrpc": "2.0", "id": 1, "method": "scene.current"})
	assert_eq((await http_post(body))["status"], 200)
	assert_eq((await http_post(body, {"Authorization": null}))["status"], 401, "missing token")
	assert_eq((await http_post(body, {"Authorization": "Bearer wrong"}))["status"], 401, "wrong token")
	assert_eq((await http_post(body, {"Host": "attacker.example"}))["status"], 403, "DNS rebinding")
	assert_eq((await http_post(body, {"Origin": "https://attacker.example"}))["status"], 403, "browser origin")
	assert_eq((await http_post(body, {"Content-Type": "text/plain"}))["status"], 415)
	assert_eq((await http_post("{nope"))["status"], 400)
	assert_eq((await http_post("x".repeat(70000)))["status"], 413)
	assert_eq((await http("GET /v1/rpc HTTP/1.1\r\nHost: 127.0.0.1:%d\r\n\r\n" % port))["status"], 405)
	assert_eq((await http("GET /etc/passwd HTTP/1.1\r\nHost: 127.0.0.1:%d\r\n\r\n" % port))["status"], 404)
	assert_eq(server.http.connection_count(), 0, "connections are closed after each response")


func test_http_read_timeout_drops_slow_clients() -> void:
	start_server()
	var peer := StreamPeerTCP.new()
	peer.connect_to_host("127.0.0.1", server.http.port())
	await wait_frames(5)
	peer.poll()
	peer.put_data("POST /v1/rpc HTTP/1.1\r\n".to_utf8_buffer())
	await wait_frames(5)
	assert_eq(server.http.connection_count(), 1)
	await wait_seconds(AutomationProtocol.READ_TIMEOUT_MS / 1000.0 + 0.3)
	assert_eq(server.http.connection_count(), 0)


func test_websocket_auth_events_and_reconnect() -> void:
	start_server()
	await _goto(Routes.MAIN_MENU)
	var intruder: WebSocketPeer = await ws_connect()
	ws_send(intruder, {"jsonrpc": "2.0", "id": 1, "method": "scene.current"})
	var rejected: Variant = await ws_receive(intruder)
	assert_eq(int(rejected["error"]["code"]), AutomationProtocol.UNAUTHORIZED, "first message must authenticate")
	await ws_receive(intruder, 500)
	assert_eq(intruder.get_ready_state(), WebSocketPeer.STATE_CLOSED)

	var ws: WebSocketPeer = await ws_connect()
	ws_send(ws, {"jsonrpc": "2.0", "id": 1, "method": "session.authenticate", "params": {"token": TOKEN}})
	assert_eq((await ws_receive(ws))["result"]["authenticated"], true)
	ws_send(ws, {"jsonrpc": "2.0", "id": 2, "method": "events.subscribe", "params": {"types": ["route.changed"]}})
	assert_eq((await ws_receive(ws))["result"]["subscribed"], ["route.changed"])
	SceneRouter.goto(Routes.MAIN_MENU)
	var pushed: Variant = await ws_receive(ws, 5000)
	assert_eq(pushed["method"], "event")
	assert_eq(pushed["params"]["type"], "route.changed")
	var seq: int = int(pushed["params"]["seq"])
	ws.close()
	await wait_frames(5)

	# Reconnect: a new socket authenticates again and catches up via observe.events.
	SceneRouter.goto(Routes.MAIN_MENU)
	await wait_until(func() -> bool: return not SceneRouter.is_transitioning)
	var again: WebSocketPeer = await ws_connect()
	ws_send(again, {"jsonrpc": "2.0", "id": 1, "method": "session.authenticate", "params": {"token": TOKEN}})
	await ws_receive(again)
	ws_send(again, {"jsonrpc": "2.0", "id": 2, "method": "observe.events", "params": {"since_seq": seq, "types": ["route.changed"]}})
	var missed: Array = (await ws_receive(again))["result"]["events"]
	assert_eq(missed.size(), 1, "missed events are recoverable after reconnect")
	again.close()
	await wait_frames(5)


func test_events_subscribe_needs_websocket() -> void:
	start_server()
	assert_eq(error_kind(await rpc("events.subscribe", {"types": ["route.changed"]})), "unsupported")


func test_remote_bind_requires_explicit_opt_in() -> void:
	var refused := AutomationServer.new()
	refused.configure({"profile": "qa", "port": 0, "token": TOKEN, "bind": "0.0.0.0", "badge": false, "write_session_file": false})
	add_node(refused)
	assert_true(refused.start().contains("allow-remote"))
	var short_token := AutomationServer.new()
	short_token.configure({"profile": "qa", "port": 0, "token": "short", "badge": false})
	add_node(short_token)
	assert_true(short_token.start().contains("at least"))
	var production := AutomationServer.new()
	production.configure({"profile": "production", "port": 0, "token": TOKEN})
	add_node(production)
	assert_ne(production.start(), "")

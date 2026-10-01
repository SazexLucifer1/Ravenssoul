class_name AutomationServer
extends Node
## Remote automation server (development/QA builds only; started by
## AutomationGate). Owns transports, the single client session, the event
## log, log capture, and the input driver, and dispatches JSON-RPC requests
## to the engine adapter after authentication, capability, rate-limit, and
## schema checks. See docs/AUTOMATION.md.

signal session_closed(session_id: String, reason: String)

const ISOLATED_SAVE_DIR: String = "user://automation/saves"
const ISOLATED_SETTINGS: String = "user://automation/settings.cfg"
const SESSION_FILE: String = "user://automation/session.json"
const AUDIT_LOG: String = "user://automation/audit.log"
const FIXTURE_DIR: String = "res://automation/fixtures"
const MAX_CHECKPOINTS: int = 16

var profile: StringName = AutomationProfiles.QA
var capabilities: PackedStringArray = []
var auth: AutomationAuth
var adapter: GodotAutomationAdapter
var events := AutomationEventLog.new()
var logs := AutomationLogCapture.new()
var input_driver: AutomationInputDriver
var http: AutomationHttpTransport
var websocket: AutomationWebSocketTransport
var session: AutomationSession
var active: bool = false

var _config: Dictionary = {}
var _rate := AutomationRateLimiter.new()
var _original_backend: SaveBackend
var _original_settings_path: String
var _wrote_session_file: bool = false
var _badge: CanvasLayer


## config keys: profile, port, bind, token, token_ttl_s, allow_remote,
## write_session_file (bool), badge (bool), idle_timeout_ms (int).
## Player-file isolation is unconditional: reset/fixtures delete and write
## saves, which must never reach the player's real save folder.
func configure(config: Dictionary) -> void:
	_config = config


## Returns "" on success, else a developer-facing reason (server not started).
func start() -> String:
	profile = StringName(_config.get("profile", "qa"))
	if not AutomationProfiles.is_known(profile) or profile == AutomationProfiles.PRODUCTION:
		return "profile '%s' cannot start the automation server" % profile
	var bind: String = _config.get("bind", "127.0.0.1")
	var ttl: int = int(_config.get("token_ttl_s", AutomationProtocol.DEFAULT_TOKEN_TTL_S))
	if not AutomationAuth.is_loopback(bind):
		if not _config.get("allow_remote", false):
			return "non-local bind address requires --automation-allow-remote"
		ttl = mini(ttl, AutomationProtocol.REMOTE_MAX_TOKEN_TTL_S)
	var token: String = _config.get("token", "")
	if token.is_empty():
		token = AutomationAuth.generate_token()
		_wrote_session_file = _config.get("write_session_file", true)
	elif token.length() < AutomationProtocol.MIN_TOKEN_LENGTH:
		return "automation token must be at least %d characters" % AutomationProtocol.MIN_TOKEN_LENGTH
	capabilities = AutomationProfiles.capabilities(profile)
	var port: int = int(_config.get("port", 47801))

	input_driver = AutomationInputDriver.new(get_tree().root)
	adapter = GodotAutomationAdapter.new(get_tree(), input_driver)
	http = AutomationHttpTransport.new()
	http.name = "Http"
	http.server = self
	add_child(http)
	var err: Error = http.listen(port, bind)
	if err != OK:
		http.queue_free()
		return "could not listen on %s:%d (%s)" % [bind, port, error_string(err)]
	websocket = AutomationWebSocketTransport.new()
	websocket.name = "WebSocket"
	websocket.server = self
	add_child(websocket)
	err = websocket.listen(http.port() + 1, bind)
	if err != OK:
		stop()
		return "could not listen for WebSocket on %s:%d (%s)" % [bind, http.port() + 1, error_string(err)]
	auth = AutomationAuth.new(token, ttl, bind, http.port())

	OS.add_logger(logs)
	_isolate_player_files()
	adapter.connect_events(events)
	if _wrote_session_file:
		_write_session_file()
	if _config.get("badge", true):
		_show_badge()
	active = true
	process_mode = Node.PROCESS_MODE_ALWAYS
	print("[automation] listening on http://%s:%d and ws://%s:%d (profile %s)" % [bind, http.port(), bind, http.port() + 1, profile])
	_audit("server_started", {"profile": String(profile), "bind": bind, "port": http.port()})
	return ""


func _process(_delta: float) -> void:
	if not active:
		return
	input_driver.tick()
	if session != null and session.idle_ms() > int(_config.get("idle_timeout_ms", AutomationProtocol.SESSION_IDLE_TIMEOUT_MS)):
		close_session("idle_timeout")


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST or what == NOTIFICATION_EXIT_TREE:
		stop()


## Full cleanup: session state, held input, sockets, logger, event hooks,
## temporary files, and player-file isolation. Safe to call repeatedly.
func stop() -> void:
	if session != null:
		close_session("shutdown")
	if input_driver != null:
		input_driver.release_all()
	if http != null:
		http.stop()
	if websocket != null:
		websocket.stop()
	if not active:
		return
	active = false
	OS.remove_logger(logs)
	if adapter != null:
		adapter.shutdown()
	if _badge != null and is_instance_valid(_badge):
		_badge.queue_free()
	if _wrote_session_file:
		DirAccess.remove_absolute(SESSION_FILE)
	_restore_player_files()
	_audit("server_stopped", {})


# --- auth & dispatch ------------------------------------------------------------------

func authenticate(token: String) -> int:
	return auth.check_token(token) if auth != null else AutomationProtocol.UNAUTHORIZED


func granted(capability: String) -> bool:
	return capabilities.has(capability)


## Validates and executes one JSON-RPC request. Always returns a response.
func dispatch(request: Variant, ctx: Dictionary) -> Dictionary:
	var id: Variant = null
	if request is Dictionary and (request as Dictionary).has("id"):
		id = request["id"]
		if id is float and is_equal_approx(id, roundf(id)) and absf(id) < 9.0e15:
			id = int(id) # JSON numbers parse as float; echo integer ids as integers
		if not (id == null or id is String or id is int or id is float):
			return AutomationProtocol.error_response(null, AutomationProtocol.error(AutomationProtocol.INVALID_REQUEST, "id must be a string, number, or null"))
	if not request is Dictionary or request.get("jsonrpc") != "2.0" or not request.get("method") is String \
			or (request.has("params") and not request["params"] is Dictionary):
		return AutomationProtocol.error_response(id, AutomationProtocol.error(AutomationProtocol.INVALID_REQUEST, "expected {jsonrpc: \"2.0\", id, method, params?: object}"))
	if not active:
		return AutomationProtocol.error_response(id, AutomationProtocol.error(AutomationProtocol.BUSY, "server is shutting down"))
	if not _rate.allow():
		return AutomationProtocol.error_response(id, AutomationProtocol.error(AutomationProtocol.RATE_LIMITED, "too many requests"))
	var method: String = request["method"]
	var spec: Variant = AutomationProtocol.methods().get(method)
	if spec == null:
		return AutomationProtocol.error_response(id, AutomationProtocol.error(AutomationProtocol.METHOD_NOT_FOUND, "unknown method '%s'" % method.left(64)))
	if not granted(spec["capability"]):
		_audit("denied", {"method": method, "capability": spec["capability"]})
		return AutomationProtocol.error_response(id, AutomationProtocol.error(AutomationProtocol.FORBIDDEN,
			"capability '%s' is not granted to profile '%s'" % [spec["capability"], profile], {"capability": spec["capability"]}))
	var params: Dictionary = request.get("params", {})
	var problems: PackedStringArray = AutomationSchemaValidator.validate(params, spec["params"])
	if problems.is_empty() and params.has("condition"):
		problems = AutomationPredicate.validate(params["condition"])
	if not problems.is_empty():
		return AutomationProtocol.error_response(id, AutomationProtocol.error(AutomationProtocol.INVALID_PARAMS, "invalid params", {"problems": Array(problems.slice(0, 10))}))
	var current: AutomationSession = ensure_session()
	current.touch()
	if not method.begins_with("recording."):
		current.record("request", {"method": method, "params": params})
	var outcome: Dictionary = await call("_h_" + method.replace(".", "_"), params, ctx)
	_audit("call", {"method": method, "ok": outcome.has("result"), "code": outcome.get("error", {}).get("code", 0)})
	if outcome.has("error"):
		return AutomationProtocol.error_response(id, outcome["error"])
	return AutomationProtocol.response(id, outcome.get("result"))


func ensure_session() -> AutomationSession:
	if session == null:
		session = AutomationSession.new()
		events.emit("automation.session_opened", {"session": session.id})
		_audit("session_opened", {"session": session.id})
	return session


func close_session(reason: String) -> void:
	if session == null:
		return
	var closed: AutomationSession = session
	session = null
	var released: int = input_driver.release_all()
	adapter.cleanup_session()
	for slot: String in closed.fixture_slots:
		SaveService.delete_save(slot)
	closed.checkpoints.clear()
	closed.recording = null
	events.emit("automation.session_closed", {"session": closed.id, "reason": reason, "released_inputs": released})
	_audit("session_closed", {"session": closed.id, "reason": reason, "requests": closed.request_count,
		"assertion_failures": closed.assertion_failures.size()})
	session_closed.emit(closed.id, reason)


# --- handlers (one per methods.json entry; name = "_h_" + method with dots -> _) ----

func _ok(result: Variant) -> Dictionary:
	return {"result": result}


func _err(code: int, message: String, extra: Dictionary = {}) -> Dictionary:
	return {"error": AutomationProtocol.error(code, message, extra)}


func _h_session_hello(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"protocol": AutomationProtocol.NAME, "version": AutomationProtocol.VERSION,
		"session_id": session.id, "profile": String(profile), "capabilities": Array(capabilities),
		"expires_at_unix": auth.expires_at_unix})


func _h_session_close(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	close_session("client_closed")
	return _ok({"closed": true})


func _h_session_reset(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	session.checkpoints.clear()
	return await adapter.reset(p.get("route", "main_menu"))


func _h_protocol_capabilities(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var methods: Dictionary = AutomationProtocol.methods()
	var available: Array = []
	for name: String in methods:
		if granted(methods[name]["capability"]):
			available.append(name)
	available.sort()
	var info: Dictionary = adapter.describe()
	var caps: Array = Array(capabilities)
	if not info["screenshots"]:
		caps.erase("observe.screenshot")
	return _ok({
		"protocol": AutomationProtocol.NAME, "version": AutomationProtocol.VERSION,
		"engine": info, "profile": String(profile), "capabilities": caps,
		"methods": available,
		"events": ["route.changing", "route.changed", "route.failed", "screen.opened", "screen.closed",
			"unit.moved", "unit.blocked", "unit.damaged", "unit.defeated", "mission.objective_reached",
			"mission.finished", "save.finished", "load.finished", "input.method_changed", "input.released",
			"locale.changed", "campaign.started", "campaign.loaded", "automation.session_opened", "automation.session_closed"],
		"transports": {"http": {"port": http.port()}, "websocket": {"port": websocket.port()}},
		"limits": {"max_body_bytes": AutomationProtocol.MAX_BODY_BYTES, "rate_per_second": AutomationProtocol.RATE_PER_SECOND,
			"rate_burst": AutomationProtocol.RATE_BURST, "idle_timeout_ms": int(_config.get("idle_timeout_ms", AutomationProtocol.SESSION_IDLE_TIMEOUT_MS)),
			"max_wait_ms": 120000},
		"fixtures": _fixture_ids(),
	})


func _h_protocol_schema(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok(AutomationProtocol.schema_document())


func _h_scene_current(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok(adapter.scene_info())


func _h_tree_query(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"entities": adapter.query(p)})


func _h_entity_get(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	if p.get("deep", false) and not granted("inspect.deep"):
		return _err(AutomationProtocol.FORBIDDEN, "deep inspection needs capability 'inspect.deep'", {"capability": "inspect.deep"})
	var found: Variant = adapter.entity(p["id"], p.get("deep", false))
	return _ok(found) if found != null else _err(AutomationProtocol.NOT_FOUND, "no entity '%s'" % p["id"])


func _h_state_get(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok(adapter.state())


func _h_actions_available(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"actions": adapter.available_actions()})


func _h_action_perform(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return adapter.perform(p["action"], p.get("target", ""), p.get("args", {}))


func _input_result(problem: String) -> Dictionary:
	if not problem.is_empty():
		return _err(AutomationProtocol.INVALID_PARAMS, problem)
	return _ok({"held": Array(input_driver.held())})


func _h_input_key(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _input_result(input_driver.key(p["key"], p.get("mode", "tap"), int(p.get("duration_ms", 0))))


func _h_input_action(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _input_result(input_driver.action(p["action"], p.get("mode", "tap"), int(p.get("duration_ms", 0))))


func _h_input_mouse(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _input_result(input_driver.mouse(p["mode"], Vector2(p["x"], p["y"]), p.get("button", "left")))


func _h_input_gamepad(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	if p.has("button") == p.has("axis"):
		return _err(AutomationProtocol.INVALID_PARAMS, "pass exactly one of button or axis")
	if p.has("axis"):
		if not p.has("value"):
			return _err(AutomationProtocol.INVALID_PARAMS, "axis needs value")
		return _input_result(input_driver.gamepad_axis(p["axis"], float(p["value"])))
	return _input_result(input_driver.gamepad_button(p["button"], p.get("mode", "tap"), int(p.get("duration_ms", 0))))


func _h_input_touch(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _input_result(input_driver.touch(int(p.get("index", 0)), p.get("mode", "tap"), Vector2(p["x"], p["y"])))


func _h_input_release_all(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"released": input_driver.release_all()})


func _h_wait_frames(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	for i: int in int(p["count"]):
		await get_tree().process_frame
		if not active:
			return _err(AutomationProtocol.BUSY, "server stopped")
	return _ok({"frames": int(p["count"])})


func _h_wait_until(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var deadline: int = Time.get_ticks_msec() + int(p.get("timeout_ms", 10000))
	var started: int = Time.get_ticks_msec()
	var frames: int = 0
	while true:
		var outcome: Dictionary = AutomationPredicate.evaluate(p["condition"], _resolve)
		if outcome["passed"]:
			return _ok({"met": true, "frames": frames, "elapsed_ms": Time.get_ticks_msec() - started, "actual": outcome["actual"]})
		if Time.get_ticks_msec() >= deadline:
			return _err(AutomationProtocol.TIMEOUT, "condition not met within %d ms" % int(p.get("timeout_ms", 10000)), {"actual": outcome["actual"], "frames": frames})
		await get_tree().process_frame
		frames += 1
		if not active:
			return _err(AutomationProtocol.BUSY, "server stopped")
	return {}


func _h_wait_event(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var after: int = int(p.get("after_seq", events.last_seq))
	var deadline: int = Time.get_ticks_msec() + int(p.get("timeout_ms", 10000))
	var match: Dictionary = p.get("match", {})
	while true:
		for event: Dictionary in events.since(after, [p["type"]], AutomationProtocol.EVENT_BUFFER):
			if AutomationEventLog.matches(event, p["type"], match):
				return _ok({"met": true, "event": event})
		if Time.get_ticks_msec() >= deadline:
			return _err(AutomationProtocol.TIMEOUT, "no '%s' event within %d ms" % [p["type"], int(p.get("timeout_ms", 10000))], {"last_seq": events.last_seq})
		await get_tree().process_frame
		if not active:
			return _err(AutomationProtocol.BUSY, "server stopped")
	return {}


func _h_wait_settled(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var deadline: int = Time.get_ticks_msec() + int(p.get("timeout_ms", 10000))
	var frames: int = 0
	var stable: int = 0
	while stable < 2: # settled on two consecutive frames
		stable = stable + 1 if adapter.is_settled() else 0
		if stable >= 2:
			break
		if Time.get_ticks_msec() >= deadline:
			return _err(AutomationProtocol.TIMEOUT, "game did not settle", {"state": adapter.scene_info()})
		await get_tree().process_frame
		frames += 1
		if not active:
			return _err(AutomationProtocol.BUSY, "server stopped")
	return _ok({"met": true, "frames": frames})


func _h_assert_check(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var outcome: Dictionary = AutomationPredicate.evaluate(p["condition"], _resolve)
	var message: String = p.get("message", "")
	if not outcome["passed"]:
		session.assertion_failures.append({"condition": p["condition"], "actual": outcome["actual"], "message": message})
		_audit("assertion_failed", {"message": message})
	return _ok({"passed": outcome["passed"], "actual": outcome["actual"], "message": message})


func _h_checkpoint_create(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	if session.checkpoints.size() >= MAX_CHECKPOINTS and not session.checkpoints.has(p["name"]):
		return _err(AutomationProtocol.INVALID_PARAMS, "at most %d checkpoints per session" % MAX_CHECKPOINTS)
	session.checkpoints[p["name"]] = adapter.capture_checkpoint()
	return _ok({"name": p["name"]})


func _h_checkpoint_restore(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	if not session.checkpoints.has(p["name"]):
		return _err(AutomationProtocol.NOT_FOUND, "no checkpoint '%s'" % p["name"])
	var outcome: Dictionary = await adapter.restore_checkpoint(session.checkpoints[p["name"]])
	if outcome.has("result"):
		outcome["result"]["name"] = p["name"]
	return outcome


func _h_checkpoint_list(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"names": session.checkpoints.keys()})


func _h_fixture_list(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"fixtures": _fixture_ids()})


func _h_fixture_load(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	if not _fixture_ids().has(p["fixture"]):
		return _err(AutomationProtocol.NOT_FOUND, "no approved fixture '%s'" % p["fixture"])
	var data: Variant = JSON.parse_string(FileAccess.get_file_as_string("%s/%s.json" % [FIXTURE_DIR, p["fixture"]]))
	if not data is Dictionary:
		return _err(AutomationProtocol.INTERNAL_ERROR, "fixture file is malformed")
	var write_save: bool = p.get("write_save", false)
	var outcome: Dictionary = await adapter.apply_fixture(data, write_save)
	if write_save and outcome.has("result"):
		session.fixture_slots.append(SaveService.DEFAULT_SLOT)
	return outcome


func _h_recording_start(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	session.recording = []
	if not events.event_added.is_connected(_record_event):
		events.event_added.connect(_record_event)
	return _ok({"recording": true})


func _h_recording_stop(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var entries: Variant = session.recording
	session.recording = null
	if events.event_added.is_connected(_record_event):
		events.event_added.disconnect(_record_event)
	if not entries is Array:
		return _err(AutomationProtocol.INVALID_PARAMS, "no recording in progress")
	return _ok({"entries": entries})


func _h_observe_screenshot(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return await adapter.screenshot(int(p.get("max_width", 0)))


func _h_observe_logs(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok(logs.lines_since(int(p.get("since_seq", 0)), p.get("min_level", "info"), int(p.get("limit", 200))))


func _h_observe_events(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok({"events": events.since(int(p.get("since_seq", 0)), p.get("types", []), int(p.get("limit", 200))), "last_seq": events.last_seq})


func _h_events_subscribe(p: Dictionary, ctx: Dictionary) -> Dictionary:
	if ctx.get("transport") != "ws":
		return _err(AutomationProtocol.UNSUPPORTED, "events.subscribe needs the WebSocket transport; use observe.events over HTTP")
	var types: PackedStringArray = PackedStringArray(p.get("types", []))
	(ctx["peer"] as Dictionary)["subscriptions"] = types
	return _ok({"subscribed": Array(types)})


func _h_observe_perf(_p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return _ok(adapter.perf())


func _h_app_quit(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	var code: int = int(p.get("exit_code", 0))
	_audit("quit_requested", {"exit_code": code})
	# Let the transport deliver this response first, then clean up and quit.
	get_tree().create_timer(0.25, true, false, true).timeout.connect(func() -> void:
		stop()
		get_tree().quit(code))
	return _ok({"quitting": true})


func _h_dev_teleport(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return adapter.dev("dev.teleport", p)


func _h_dev_spawn(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return adapter.dev("dev.spawn", p)


func _h_dev_command(p: Dictionary, _ctx: Dictionary) -> Dictionary:
	return adapter.dev("dev.command", p)


# --- helpers ---------------------------------------------------------------------------

func _resolve(source: String, id: String) -> Variant:
	match source:
		"state":
			return adapter.state()
		"scene":
			return adapter.scene_info()
		"entity":
			return adapter.entity(id, false)
	return null


func _record_event(event: Dictionary) -> void:
	if session != null:
		session.record("event", event)


func _fixture_ids() -> Array:
	var ids: Array = []
	for file: String in DirAccess.get_files_at(FIXTURE_DIR):
		if file.ends_with(".json"):
			ids.append(file.get_basename())
	ids.sort()
	return ids


## Automation must never touch the player's real saves or preferences.
func _isolate_player_files() -> void:
	DirAccess.make_dir_recursive_absolute(ISOLATED_SAVE_DIR)
	_original_backend = SaveService.backend
	_original_settings_path = Settings.settings_path
	SaveService.backend = JsonFileSaveBackend.new(ISOLATED_SAVE_DIR)
	Settings.settings_path = ISOLATED_SETTINGS


func _restore_player_files() -> void:
	if _original_backend == null:
		return
	for file: String in DirAccess.get_files_at(ISOLATED_SAVE_DIR):
		DirAccess.remove_absolute(ISOLATED_SAVE_DIR.path_join(file))
	DirAccess.remove_absolute(ISOLATED_SETTINGS)
	SaveService.backend = _original_backend
	Settings.settings_path = _original_settings_path
	_original_backend = null


func _write_session_file() -> void:
	DirAccess.make_dir_recursive_absolute(SESSION_FILE.get_base_dir())
	var file := FileAccess.open(SESSION_FILE, FileAccess.WRITE)
	if file == null:
		return
	file.store_string(JSON.stringify({"http_port": http.port(), "ws_port": websocket.port(),
		"token": auth.token, "expires_at_unix": auth.expires_at_unix, "profile": String(profile)}))
	file.close()
	FileAccess.set_unix_permissions(SESSION_FILE, FileAccess.UNIX_READ_OWNER | FileAccess.UNIX_WRITE_OWNER)


func _show_badge() -> void:
	_badge = CanvasLayer.new()
	_badge.layer = 120
	_badge.process_mode = Node.PROCESS_MODE_ALWAYS
	var label := Label.new()
	label.text = "AUTOMATION_BADGE"
	label.theme_type_variation = &"KeycapLabel"
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP, Control.PRESET_MODE_MINSIZE, 6)
	label.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_badge.add_child(label)
	add_child(_badge)


func _audit(kind: String, data: Dictionary) -> void:
	var line: String = JSON.stringify({"t": Time.get_datetime_string_from_system(true), "kind": kind,
		"session": session.id if session != null else "", "data": data})
	if kind != "call":
		DevLog.info("automation", line)
	DirAccess.make_dir_recursive_absolute(AUDIT_LOG.get_base_dir())
	if FileAccess.file_exists(AUDIT_LOG) and FileAccess.get_size(AUDIT_LOG) > 1048576:
		DirAccess.rename_absolute(AUDIT_LOG, AUDIT_LOG + ".1") # simple rotation
	var file := FileAccess.open(AUDIT_LOG, FileAccess.READ_WRITE if FileAccess.file_exists(AUDIT_LOG) else FileAccess.WRITE)
	if file != null:
		file.seek_end()
		file.store_line(line)

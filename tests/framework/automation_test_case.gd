class_name AutomationTestCase
extends TestCase
## Helpers for automation tests: an in-process server on ephemeral ports,
## direct dispatch, and raw HTTP / WebSocket clients over real sockets.

const TOKEN: String = "0123456789abcdef0123456789abcdef0123456789abcdef"

var server: AutomationServer
var _next_id: int = 1


func start_server(profile: String = "qa", extra: Dictionary = {}) -> AutomationServer:
	server = AutomationServer.new()
	server.name = "Automation"
	var config: Dictionary = {"profile": profile, "port": 0, "token": TOKEN, "badge": false, "write_session_file": false}
	config.merge(extra, true)
	server.configure(config)
	tree.root.add_child(server)
	var error: String = server.start()
	assert_eq(error, "", "server start")
	return server


func after_each() -> void:
	if server != null and is_instance_valid(server):
		server.stop()
		await wait_frames(2) # let pending handler coroutines observe the stop
		server.queue_free()
		await wait_frames(1)
	server = null


## Dispatch without sockets. Returns the JSON-RPC response.
func rpc(method: String, params: Dictionary = {}) -> Dictionary:
	_next_id += 1
	return await server.dispatch({"jsonrpc": "2.0", "id": _next_id, "method": method, "params": params}, {"transport": "test"})


func result_of(response: Dictionary) -> Variant:
	if response.has("error"):
		fail("unexpected error: %s" % JSON.stringify(response["error"]))
		return null
	return response["result"]


func error_kind(response: Dictionary) -> String:
	return str(response.get("error", {}).get("data", {}).get("kind", ""))


## Raw HTTP over a real TCP socket. Returns {"status": int, "body": Variant}.
func http(raw_request: String, port: int = -1) -> Dictionary:
	var peer := StreamPeerTCP.new()
	peer.connect_to_host("127.0.0.1", server.http.port() if port < 0 else port)
	var deadline: int = Time.get_ticks_msec() + 5000
	while peer.get_status() == StreamPeerTCP.STATUS_CONNECTING and Time.get_ticks_msec() < deadline:
		await tree.process_frame
		peer.poll()
	peer.put_data(raw_request.to_utf8_buffer())
	var data := PackedByteArray()
	while Time.get_ticks_msec() < deadline:
		await tree.process_frame
		peer.poll()
		if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
			break
		var available: int = peer.get_available_bytes()
		if available > 0:
			data.append_array(peer.get_data(available)[1])
	var text: String = data.get_string_from_utf8()
	var split: int = text.find("\r\n\r\n")
	if split < 0:
		return {"status": 0, "body": null}
	return {"status": int(text.get_slice(" ", 1)), "body": JSON.parse_string(text.substr(split + 4))}


func http_post(body: String, headers: Dictionary = {}) -> Dictionary:
	var all: Dictionary = {"Host": "127.0.0.1:%d" % server.http.port(), "Content-Type": "application/json",
		"Authorization": "Bearer " + TOKEN, "Content-Length": str(body.to_utf8_buffer().size())}
	all.merge(headers, true)
	var head: String = "POST /v1/rpc HTTP/1.1\r\n"
	for key: String in all:
		if all[key] != null:
			head += "%s: %s\r\n" % [key, all[key]]
	return await http(head + "\r\n" + body)


func ws_connect() -> WebSocketPeer:
	var ws := WebSocketPeer.new()
	ws.connect_to_url("ws://127.0.0.1:%d" % server.websocket.port())
	var deadline: int = Time.get_ticks_msec() + 5000
	while ws.get_ready_state() == WebSocketPeer.STATE_CONNECTING and Time.get_ticks_msec() < deadline:
		await tree.process_frame
		ws.poll()
	return ws


func ws_send(ws: WebSocketPeer, payload: Dictionary) -> void:
	ws.send_text(JSON.stringify(payload))


## Next message (or null on timeout/close).
func ws_receive(ws: WebSocketPeer, timeout_ms: int = 3000) -> Variant:
	var deadline: int = Time.get_ticks_msec() + timeout_ms
	while Time.get_ticks_msec() < deadline:
		ws.poll()
		if ws.get_available_packet_count() > 0:
			return JSON.parse_string(ws.get_packet().get_string_from_utf8())
		if ws.get_ready_state() == WebSocketPeer.STATE_CLOSED:
			return null
		await tree.process_frame
	return null

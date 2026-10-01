class_name AutomationWebSocketTransport
extends Node
## WebSocket transport (port = HTTP port + 1). The first message must be
##   {"jsonrpc": "2.0", "id": 1, "method": "session.authenticate", "params": {"token": "..."}}
## within AUTH_TIMEOUT_MS; afterwards the socket carries JSON-RPC requests and
## server-pushed notifications {"jsonrpc": "2.0", "method": "event", "params": <event>}
## for types registered with events.subscribe.

const AUTH_TIMEOUT_MS: int = 3000
const CLOSE_UNAUTHORIZED: int = 4401

var server: AutomationServer
var _tcp := TCPServer.new()
## {"ws": WebSocketPeer, "authed": bool, "created": msec, "subscriptions": PackedStringArray, "close_at": msec}
var _peers: Array[Dictionary] = []


func listen(port_number: int, bind_address: String) -> Error:
	process_mode = Node.PROCESS_MODE_ALWAYS
	return _tcp.listen(port_number, bind_address)


func port() -> int:
	return _tcp.get_local_port()


func peer_count() -> int:
	return _peers.size()


func stop() -> void:
	for peer: Dictionary in _peers:
		(peer["ws"] as WebSocketPeer).close(1001, "server stopping")
	_peers.clear()
	_tcp.stop()
	if server != null and server.events.event_added.is_connected(_on_event):
		server.events.event_added.disconnect(_on_event)


func _ready() -> void:
	server.events.event_added.connect(_on_event)


func _process(_delta: float) -> void:
	while _tcp.is_listening() and _tcp.is_connection_available():
		var tcp: StreamPeerTCP = _tcp.take_connection()
		if _peers.size() >= AutomationProtocol.MAX_CONNECTIONS:
			tcp.disconnect_from_host()
			continue
		var ws := WebSocketPeer.new()
		ws.inbound_buffer_size = AutomationProtocol.MAX_BODY_BYTES
		ws.outbound_buffer_size = AutomationProtocol.MAX_SCREENSHOT_BYTES * 2
		ws.accept_stream(tcp)
		_peers.append({"ws": ws, "authed": false, "created": Time.get_ticks_msec(), "subscriptions": PackedStringArray(), "close_at": -1})
	for peer: Dictionary in _peers.duplicate():
		var ws: WebSocketPeer = peer["ws"]
		ws.poll()
		var state: WebSocketPeer.State = ws.get_ready_state()
		if state == WebSocketPeer.STATE_CLOSED:
			_peers.erase(peer)
			continue
		if int(peer["close_at"]) >= 0:
			# Rejected peer: the error frame was queued; close once it has been flushed.
			if Time.get_ticks_msec() >= int(peer["close_at"]):
				ws.close(CLOSE_UNAUTHORIZED, "unauthorized")
			continue
		if not peer["authed"] and Time.get_ticks_msec() - int(peer["created"]) > AUTH_TIMEOUT_MS:
			ws.close(CLOSE_UNAUTHORIZED, "authentication required")
			continue
		while state == WebSocketPeer.STATE_OPEN and ws.get_available_packet_count() > 0:
			_on_message(peer, ws.get_packet().get_string_from_utf8())


func _on_message(peer: Dictionary, text: String) -> void:
	if int(peer["close_at"]) >= 0:
		return # rejected; ignore anything else it sends
	var json := JSON.new()
	if json.parse(text) != OK:
		_send(peer, AutomationProtocol.error_response(null, AutomationProtocol.error(AutomationProtocol.PARSE_ERROR, "message is not valid JSON")))
		return
	var message: Variant = json.data
	if not peer["authed"]:
		var token: String = ""
		if message is Dictionary and message.get("method") == "session.authenticate" and message.get("params") is Dictionary:
			token = str(message["params"].get("token", ""))
		var code: int = server.authenticate(token)
		var id: Variant = message.get("id") if message is Dictionary else null
		if code != 0:
			_send(peer, AutomationProtocol.error_response(id, AutomationProtocol.error(code, "authenticate first with a valid token")))
			peer["close_at"] = Time.get_ticks_msec() + 100
			return
		peer["authed"] = true
		_send(peer, AutomationProtocol.response(id, {"authenticated": true}))
		return
	var response: Dictionary = await server.dispatch(message, {"transport": "ws", "peer": peer})
	_send(peer, response)


func _on_event(event: Dictionary) -> void:
	for peer: Dictionary in _peers:
		var subs: PackedStringArray = peer["subscriptions"]
		if peer["authed"] and not subs.is_empty() and (subs.has(event["type"]) or subs.has("*")):
			_send(peer, {"jsonrpc": "2.0", "method": "event", "params": event})


func _send(peer: Dictionary, payload: Dictionary) -> void:
	var ws: WebSocketPeer = peer["ws"]
	if ws.get_ready_state() == WebSocketPeer.STATE_OPEN:
		ws.send_text(JSON.stringify(payload))

class_name AutomationHttpTransport
extends Node
## Minimal HTTP/1.1 server for the automation protocol.
##   GET  /v1/health  -> {"ok": true, "protocol", "version"}   (no token; Host/Origin checked)
##   POST /v1/rpc     -> JSON-RPC 2.0 (Authorization: Bearer <token>)
## One request per connection (Connection: close). Enforces header/body size
## limits, a read timeout, and a connection cap.

const STATUS_TEXT: Dictionary[int, String] = {
	200: "OK", 400: "Bad Request", 401: "Unauthorized", 403: "Forbidden", 404: "Not Found",
	405: "Method Not Allowed", 408: "Request Timeout", 413: "Payload Too Large",
	415: "Unsupported Media Type", 429: "Too Many Requests", 431: "Request Header Fields Too Large",
	503: "Service Unavailable",
}

var server: AutomationServer
var _tcp := TCPServer.new()
## {"peer": StreamPeerTCP, "buffer": PackedByteArray, "started": msec, "busy": bool}
var _connections: Array[Dictionary] = []


func listen(port: int, bind_address: String) -> Error:
	process_mode = Node.PROCESS_MODE_ALWAYS
	return _tcp.listen(port, bind_address)


func port() -> int:
	return _tcp.get_local_port()


func connection_count() -> int:
	return _connections.size()


func stop() -> void:
	for conn: Dictionary in _connections:
		(conn["peer"] as StreamPeerTCP).disconnect_from_host()
	_connections.clear()
	_tcp.stop()


func _process(_delta: float) -> void:
	while _tcp.is_listening() and _tcp.is_connection_available():
		var peer: StreamPeerTCP = _tcp.take_connection()
		if _connections.size() >= AutomationProtocol.MAX_CONNECTIONS:
			_send_raw(peer, 503, {"error": "too many connections"})
			continue
		peer.set_no_delay(true)
		_connections.append({"peer": peer, "buffer": PackedByteArray(), "started": Time.get_ticks_msec(), "busy": false})
	for conn: Dictionary in _connections.duplicate():
		if conn["busy"]:
			continue
		var peer: StreamPeerTCP = conn["peer"]
		peer.poll()
		if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
			_connections.erase(conn)
			continue
		var available: int = peer.get_available_bytes()
		if available > 0:
			var chunk: Array = peer.get_partial_data(available)
			if chunk[0] == OK:
				# PackedByteArray is a value type: reassign, don't append to a copy.
				var buffer: PackedByteArray = conn["buffer"]
				buffer.append_array(chunk[1])
				conn["buffer"] = buffer
		var parsed: Dictionary = _try_parse(conn["buffer"])
		if parsed.has("status"):
			_finish(conn, parsed["status"], {"error": parsed["reason"]})
		elif parsed.has("method"):
			conn["busy"] = true
			_handle(conn, parsed)
		elif Time.get_ticks_msec() - int(conn["started"]) > AutomationProtocol.READ_TIMEOUT_MS:
			_finish(conn, 408, {"error": "request not completed in time"})


## Returns {} (incomplete), {"status", "reason"} (reject), or the parsed request.
static func _try_parse(buffer: PackedByteArray) -> Dictionary:
	var header_end: int = _find_header_end(buffer)
	if header_end < 0:
		if buffer.size() > AutomationProtocol.MAX_HEADER_BYTES:
			return {"status": 431, "reason": "headers too large"}
		return {}
	if header_end > AutomationProtocol.MAX_HEADER_BYTES:
		return {"status": 431, "reason": "headers too large"}
	var head: String = buffer.slice(0, header_end).get_string_from_ascii()
	var lines: PackedStringArray = head.split("\r\n")
	var request_line: PackedStringArray = lines[0].split(" ")
	if request_line.size() != 3 or not request_line[2].begins_with("HTTP/1."):
		return {"status": 400, "reason": "malformed request line"}
	var headers: Dictionary = {}
	for i: int in range(1, lines.size()):
		var colon: int = lines[i].find(":")
		if colon <= 0:
			return {"status": 400, "reason": "malformed header"}
		headers[lines[i].left(colon).strip_edges().to_lower()] = lines[i].substr(colon + 1).strip_edges()
	var length: int = 0
	if headers.has("content-length"):
		if not str(headers["content-length"]).is_valid_int() or int(headers["content-length"]) < 0:
			return {"status": 400, "reason": "invalid content-length"}
		length = int(headers["content-length"])
	if headers.has("transfer-encoding"):
		return {"status": 400, "reason": "chunked bodies are not supported"}
	if length > AutomationProtocol.MAX_BODY_BYTES:
		return {"status": 413, "reason": "body larger than %d bytes" % AutomationProtocol.MAX_BODY_BYTES}
	var body_start: int = header_end + 4
	if buffer.size() - body_start < length:
		return {}
	return {"method": request_line[0], "path": request_line[1], "headers": headers,
		"body": buffer.slice(body_start, body_start + length).get_string_from_utf8()}


static func _find_header_end(buffer: PackedByteArray) -> int:
	for i: int in range(0, buffer.size() - 3):
		if buffer[i] == 13 and buffer[i + 1] == 10 and buffer[i + 2] == 13 and buffer[i + 3] == 10:
			return i
	return -1


func _handle(conn: Dictionary, request: Dictionary) -> void:
	var headers: Dictionary = request["headers"]
	var rejection: String = server.auth.check_http_headers(headers)
	if not rejection.is_empty():
		_finish(conn, 403, {"error": rejection})
		return
	match request["path"]:
		"/v1/health":
			if request["method"] != "GET":
				_finish(conn, 405, {"error": "use GET"})
				return
			_finish(conn, 200, {"ok": true, "protocol": AutomationProtocol.NAME, "version": AutomationProtocol.VERSION})
		"/v1/rpc":
			if request["method"] != "POST":
				_finish(conn, 405, {"error": "use POST"})
				return
			if not str(headers.get("content-type", "")).begins_with("application/json"):
				_finish(conn, 415, {"error": "content-type must be application/json"})
				return
			var auth_code: int = server.authenticate(AutomationAuth.bearer_from(headers))
			if auth_code != 0:
				_finish(conn, 401, AutomationProtocol.error_response(null, AutomationProtocol.error(auth_code,
					"missing or invalid token" if auth_code == AutomationProtocol.UNAUTHORIZED else "token expired")))
				return
			var json := JSON.new()
			if json.parse(request["body"]) != OK:
				_finish(conn, 400, AutomationProtocol.error_response(null, AutomationProtocol.error(AutomationProtocol.PARSE_ERROR, "body is not valid JSON")))
				return
			var response: Dictionary = await server.dispatch(json.data, {"transport": "http"})
			var code: int = int(response.get("error", {}).get("code", 0))
			_finish(conn, 429 if code == AutomationProtocol.RATE_LIMITED else 200, response)
		_:
			_finish(conn, 404, {"error": "not found"})


func _finish(conn: Dictionary, status: int, payload: Dictionary) -> void:
	_send_raw(conn["peer"], status, payload)
	_connections.erase(conn)


static func _send_raw(peer: StreamPeerTCP, status: int, payload: Dictionary) -> void:
	if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return
	var body: PackedByteArray = JSON.stringify(payload).to_utf8_buffer()
	var head: String = "HTTP/1.1 %d %s\r\nContent-Type: application/json\r\nContent-Length: %d\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n" % [status, STATUS_TEXT.get(status, "Error"), body.size()]
	peer.put_data(head.to_ascii_buffer())
	peer.put_data(body)
	peer.disconnect_from_host()

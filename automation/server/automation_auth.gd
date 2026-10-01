class_name AutomationAuth
extends RefCounted
## Bearer-token authentication with expiry, plus request-origin checks that
## block browser-based attacks on localhost (DNS rebinding, CSRF).

var token: String
var expires_at_unix: int
var bind_address: String
var allowed_hosts: PackedStringArray = []
var _crypto := Crypto.new()


func _init(p_token: String, ttl_seconds: int, p_bind_address: String, port: int) -> void:
	token = p_token
	expires_at_unix = int(Time.get_unix_time_from_system()) + ttl_seconds
	bind_address = p_bind_address
	for host: String in ["127.0.0.1", "localhost", "[::1]"]:
		allowed_hosts.append("%s:%d" % [host, port])
		allowed_hosts.append("%s:%d" % [host, port + 1])
	if not is_loopback(p_bind_address):
		allowed_hosts.append("%s:%d" % [p_bind_address, port])
		allowed_hosts.append("%s:%d" % [p_bind_address, port + 1])


static func generate_token() -> String:
	return Crypto.new().generate_random_bytes(32).hex_encode()


static func is_loopback(address: String) -> bool:
	return address in ["127.0.0.1", "::1", "localhost"]


func is_expired() -> bool:
	return Time.get_unix_time_from_system() >= expires_at_unix


## Returns 0 when authorized, else an AutomationProtocol error code.
func check_token(presented: String) -> int:
	if presented.length() != token.length() or not _crypto.constant_time_compare(presented.to_utf8_buffer(), token.to_utf8_buffer()):
		return AutomationProtocol.UNAUTHORIZED
	if is_expired():
		return AutomationProtocol.SESSION_EXPIRED
	return 0


## Browsers always send Origin on cross-site requests; automation clients don't.
## A wrong Host header means DNS rebinding. Both are rejected.
func check_http_headers(headers: Dictionary) -> String:
	if headers.has("origin"):
		return "browser origins are not allowed"
	var host: String = headers.get("host", "")
	if not allowed_hosts.has(host):
		return "unexpected host header"
	return ""


static func bearer_from(headers: Dictionary) -> String:
	var value: String = headers.get("authorization", "")
	return value.substr(7).strip_edges() if value.begins_with("Bearer ") else ""

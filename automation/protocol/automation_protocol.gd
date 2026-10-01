class_name AutomationProtocol
extends RefCounted
## Protocol constants shared by transports, dispatcher, and adapters.
## Wire format: JSON-RPC 2.0. Method table: res://automation/protocol/methods.json.

const NAME: String = "utopia-automation"
const VERSION: String = "1.0"
const METHODS_PATH: String = "res://automation/protocol/methods.json"

# JSON-RPC standard codes.
const PARSE_ERROR: int = -32700
const INVALID_REQUEST: int = -32600
const METHOD_NOT_FOUND: int = -32601
const INVALID_PARAMS: int = -32602
const INTERNAL_ERROR: int = -32603
# Protocol-specific codes.
const UNAUTHORIZED: int = -32001
const FORBIDDEN: int = -32002
const RATE_LIMITED: int = -32003
const NOT_FOUND: int = -32004
const UNSUPPORTED: int = -32005
const NOT_INTERACTABLE: int = -32006
const TIMEOUT: int = -32007
const SESSION_EXPIRED: int = -32008
const BUSY: int = -32009
const PAYLOAD_TOO_LARGE: int = -32010

const ERROR_KINDS: Dictionary[int, String] = {
	PARSE_ERROR: "parse_error", INVALID_REQUEST: "invalid_request",
	METHOD_NOT_FOUND: "method_not_found", INVALID_PARAMS: "invalid_params",
	INTERNAL_ERROR: "internal", UNAUTHORIZED: "unauthorized", FORBIDDEN: "forbidden",
	RATE_LIMITED: "rate_limited", NOT_FOUND: "not_found", UNSUPPORTED: "unsupported",
	NOT_INTERACTABLE: "not_interactable", TIMEOUT: "timeout",
	SESSION_EXPIRED: "session_expired", BUSY: "busy", PAYLOAD_TOO_LARGE: "payload_too_large",
}

# Limits (also reported by protocol.capabilities).
const MAX_BODY_BYTES: int = 65536
const MAX_HEADER_BYTES: int = 8192
const MAX_CONNECTIONS: int = 16
const READ_TIMEOUT_MS: int = 5000
const RATE_BURST: float = 240.0
const RATE_PER_SECOND: float = 120.0
const SESSION_IDLE_TIMEOUT_MS: int = 120000
const DEFAULT_TOKEN_TTL_S: int = 3600
const REMOTE_MAX_TOKEN_TTL_S: int = 900
const MIN_TOKEN_LENGTH: int = 32
const EVENT_BUFFER: int = 1000
const LOG_BUFFER: int = 500
const MAX_SCREENSHOT_BYTES: int = 8 * 1024 * 1024


static var _methods_cache: Dictionary = {}


static func methods() -> Dictionary:
	if _methods_cache.is_empty():
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(METHODS_PATH))
		_methods_cache = (parsed as Dictionary)["methods"] if parsed is Dictionary else {}
	return _methods_cache


static func schema_document() -> Dictionary:
	return JSON.parse_string(FileAccess.get_file_as_string(METHODS_PATH))


static func error(code: int, message: String, extra: Dictionary = {}) -> Dictionary:
	var data: Dictionary = {"kind": ERROR_KINDS.get(code, "error")}
	data.merge(extra)
	return {"code": code, "message": message, "data": data}


static func response(id: Variant, result: Variant) -> Dictionary:
	return {"jsonrpc": "2.0", "id": id, "result": result}


static func error_response(id: Variant, err: Dictionary) -> Dictionary:
	return {"jsonrpc": "2.0", "id": id, "error": err}

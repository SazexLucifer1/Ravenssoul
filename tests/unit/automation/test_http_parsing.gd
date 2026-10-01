extends TestCase


func _parse(raw: String) -> Dictionary:
	return AutomationHttpTransport._try_parse(raw.to_utf8_buffer())


func test_complete_request() -> void:
	var parsed: Dictionary = _parse("POST /v1/rpc HTTP/1.1\r\nHost: x\r\nContent-Length: 2\r\n\r\n{}")
	assert_eq(parsed["method"], "POST")
	assert_eq(parsed["path"], "/v1/rpc")
	assert_eq(parsed["headers"]["host"], "x")
	assert_eq(parsed["body"], "{}")


func test_incomplete_requests_wait_for_more_data() -> void:
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nContent-Le"), {})
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nContent-Length: 10\r\n\r\n{}"), {})


func test_malformed_requests_are_rejected() -> void:
	assert_eq(_parse("GARBAGE\r\n\r\n")["status"], 400)
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nno-colon-header\r\n\r\n")["status"], 400)
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nContent-Length: -5\r\n\r\n")["status"], 400)
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n")["status"], 400)


func test_size_limits() -> void:
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nContent-Length: 999999\r\n\r\n")["status"], 413)
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nX: %s\r\n\r\n" % "a".repeat(9000))["status"], 431)
	assert_eq(_parse("POST /v1/rpc HTTP/1.1\r\nX: %s" % "a".repeat(9000))["status"], 431, "no header end yet")

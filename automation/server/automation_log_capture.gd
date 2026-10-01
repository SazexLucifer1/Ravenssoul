class_name AutomationLogCapture
extends Logger
## Captures engine/script messages for observe.logs, with local filesystem
## paths redacted. Callbacks can arrive from any thread, hence the mutex.

var _mutex := Mutex.new()
var _lines: Array[Dictionary] = []
var _seq: int = 0
var _redactions: Array[Array] = []
var error_count: int = 0


func _init() -> void:
	for pair: Array in [
		[OS.get_user_data_dir(), "<user_data>"],
		[ProjectSettings.globalize_path("res://").trim_suffix("/"), "<project>"],
		[OS.get_executable_path().get_base_dir(), "<engine>"],
		[OS.get_environment("HOME"), "<home>"],
	]:
		if str(pair[0]).length() > 1:
			_redactions.append(pair)


func _log_message(message: String, error: bool) -> void:
	_add("error" if error else "info", message.strip_edges())


func _log_error(function: String, file: String, line: int, code: String, rationale: String,
		_editor_notify: bool, error_type: int, _script_backtraces: Array[ScriptBacktrace]) -> void:
	var level: String = "warning" if error_type == ERROR_TYPE_WARNING else "error"
	var text: String = rationale if not rationale.is_empty() else code
	_add(level, "%s (%s:%d %s)" % [text, file.get_file(), line, function])


func redact(text: String) -> String:
	for pair: Array in _redactions:
		text = text.replace(str(pair[0]), str(pair[1]))
	return text


func lines_since(seq: int, min_level: String, limit: int) -> Dictionary:
	var rank: Dictionary = {"info": 0, "warning": 1, "error": 2}
	_mutex.lock()
	var out: Array[Dictionary] = []
	for entry: Dictionary in _lines:
		if entry["seq"] > seq and rank[entry["level"]] >= rank.get(min_level, 0):
			out.append(entry)
			if out.size() >= limit:
				break
	var last: int = _seq
	_mutex.unlock()
	return {"lines": out, "last_seq": last}


func _add(level: String, message: String) -> void:
	if message.is_empty():
		return
	_mutex.lock()
	_seq += 1
	if level == "error":
		error_count += 1
	_lines.append({"seq": _seq, "level": level, "message": redact(message).left(1000), "t_ms": Time.get_ticks_msec()})
	if _lines.size() > AutomationProtocol.LOG_BUFFER:
		_lines.pop_front()
	_mutex.unlock()

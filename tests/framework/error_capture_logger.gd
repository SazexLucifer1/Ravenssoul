class_name ErrorCaptureLogger
extends Logger
## Counts engine and script errors so the runner can fail the test that
## caused them (Godot keeps running after a script error, which would
## otherwise let a broken test look green).

var _mutex := Mutex.new()
var _errors: PackedStringArray = []


func _log_error(function: String, file: String, line: int, code: String, rationale: String,
		_editor_notify: bool, error_type: int, _script_backtraces: Array[ScriptBacktrace]) -> void:
	if error_type == ERROR_TYPE_WARNING:
		return
	_mutex.lock()
	_errors.append("%s (%s:%d %s) %s" % [code, file, line, function, rationale])
	_mutex.unlock()


func _log_message(_message: String, _error: bool) -> void:
	pass


func take() -> PackedStringArray:
	_mutex.lock()
	var copy: PackedStringArray = _errors.duplicate()
	_errors.clear()
	_mutex.unlock()
	return copy

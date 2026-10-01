class_name DevLog
extends RefCounted
## Development-only diagnostics. Nothing logged here is ever shown to players.
##
## Player-facing surfaces must use translation keys (see docs/LOCALIZATION.md).
## Use [method error] for programmer errors and unrecoverable states; it is
## routed through push_error so the test runner can detect it.

## Set to true by tests that deliberately exercise failure paths.
static var muted: bool = false


static func info(category: String, message: String) -> void:
	if muted or not OS.is_debug_build():
		return
	print("[%s] %s" % [category, message])


static func warn(category: String, message: String) -> void:
	if muted:
		return
	push_warning("[%s] %s" % [category, message])


static func error(category: String, message: String) -> void:
	if muted:
		return
	push_error("[%s] %s" % [category, message])

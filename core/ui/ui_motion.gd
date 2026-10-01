class_name UiMotion
extends RefCounted
## Shared motion rules for UI. All UI animation goes through these helpers so
## the reduced-motion preference is honored everywhere.

const TOKENS: UiTokens = preload("res://core/ui/theme/ui_tokens.tres")


static func reduced() -> bool:
	var settings: Node = Engine.get_main_loop().root.get_node_or_null(^"/root/Settings")
	return settings != null and bool(settings.get("reduced_motion"))


## Seconds for a standard screen transition (0 when motion is reduced).
static func transition_seconds() -> float:
	return 0.0 if reduced() else TOKENS.transition_seconds


static func press_seconds() -> float:
	return 0.0 if reduced() else TOKENS.press_feedback_seconds

class_name UiScreen
extends Control
## Base for every menu, dialog, and overlay pushed onto a ScreenStack.
##
## A screen owns its own layout and emits intent signals; it never changes
## game state directly and never reaches into other screens.

## Ask the owning ScreenStack to close this screen.
signal close_requested

## Control that receives focus when the screen opens (primary action).
@export var default_focus: Control
## If true, ui_cancel (Esc / B) closes the screen.
@export var cancel_closes: bool = true


## Called by ScreenStack right after the screen enters the tree.
func on_opened(_context: Dictionary) -> void:
	pass


## Called by ScreenStack when the player presses ui_cancel on this screen.
func on_cancel() -> void:
	if cancel_closes:
		close_requested.emit()


func focus_default() -> void:
	if default_focus != null and default_focus.is_visible_in_tree() and default_focus.focus_mode != FOCUS_NONE:
		default_focus.grab_focus()
		return
	var fallback: Control = find_next_valid_focus()
	if fallback != null and is_ancestor_of(fallback):
		fallback.grab_focus()


func play_open() -> void:
	var duration: float = UiMotion.transition_seconds()
	modulate.a = 0.0 if duration > 0.0 else 1.0
	if duration <= 0.0:
		return
	var tween := create_tween()
	tween.tween_property(self, "modulate:a", 1.0, duration)

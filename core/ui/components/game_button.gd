class_name GameButton
extends Button
## The project's button. Adds game-UI states on top of Godot's built-in
## idle/hover/focus/pressed/disabled theme states: selected, busy, error, and
## cooldown, plus press feedback and a repeat-input guard.
##
## Connect to [signal activated], not [signal BaseButton.pressed]: activated
## is filtered by the repeat guard, busy state, and cooldown.

signal activated

enum State { IDLE, HOVER, FOCUS, PRESSED, SELECTED, DISABLED, BUSY, ERROR, COOLDOWN }

## Presses within this window after an activation are ignored (double clicks,
## key repeat, mashing).
@export_range(0.0, 2.0, 0.05) var repeat_guard_seconds: float = 0.3
## Translation key shown while busy (e.g. "Saving…").
@export var busy_text_key: String = "UI_BUSY"
## Hovering with the mouse moves keyboard focus here, so only one element
## ever looks highlighted.
@export var focus_on_hover: bool = true

var _busy: bool = false
var _idle_text: String = ""
var _cooldown_total: float = 0.0
var _cooldown_left: float = 0.0
var _error_active: bool = false
var _last_activation_msec: int = -1_000_000
var _feedback_tween: Tween


func _ready() -> void:
	focus_mode = Control.FOCUS_ALL
	pressed.connect(_on_pressed)
	resized.connect(_update_pivot)
	mouse_entered.connect(_on_mouse_entered)
	set_process(false)
	_update_pivot()


func get_state() -> State:
	if _busy:
		return State.BUSY
	if _cooldown_left > 0.0:
		return State.COOLDOWN
	if _error_active:
		return State.ERROR
	if disabled:
		return State.DISABLED
	if toggle_mode and button_pressed:
		return State.SELECTED
	if is_pressed():
		return State.PRESSED
	if has_focus():
		return State.FOCUS
	if is_hovered():
		return State.HOVER
	return State.IDLE


func is_busy() -> bool:
	return _busy


## Busy: disabled and relabeled until the operation finishes.
func set_busy(value: bool) -> void:
	if value == _busy:
		return
	_busy = value
	if value:
		_idle_text = text
		text = busy_text_key
	else:
		text = _idle_text
	disabled = value


## Briefly marks the button as failed (color pulse; shake unless reduced motion).
func show_error() -> void:
	_error_active = true
	var danger: Color = get_theme_color(&"font_color", &"DangerLabel")
	_kill_feedback()
	_feedback_tween = create_tween()
	modulate = danger
	if not UiMotion.reduced():
		var origin: float = position.x
		for offset: float in [6.0, -6.0, 3.0, 0.0]:
			_feedback_tween.tween_property(self, "position:x", origin + offset, 0.04)
	_feedback_tween.tween_property(self, "modulate", Color.WHITE, 0.4)
	_feedback_tween.tween_callback(func() -> void: _error_active = false)


## Disables the button for [param seconds] and draws a draining overlay.
func start_cooldown(seconds: float) -> void:
	if seconds <= 0.0:
		return
	_cooldown_total = seconds
	_cooldown_left = seconds
	disabled = true
	set_process(true)
	queue_redraw()


func cooldown_ratio() -> float:
	return 0.0 if _cooldown_total <= 0.0 else _cooldown_left / _cooldown_total


func _process(delta: float) -> void:
	_cooldown_left = maxf(0.0, _cooldown_left - delta)
	queue_redraw()
	if _cooldown_left <= 0.0:
		set_process(false)
		disabled = _busy


func _draw() -> void:
	if _cooldown_left > 0.0:
		var overlay := Rect2(Vector2.ZERO, Vector2(size.x * cooldown_ratio(), size.y))
		draw_rect(overlay, Color(0, 0, 0, 0.45))


func _on_pressed() -> void:
	var now: int = Time.get_ticks_msec()
	if _busy or _cooldown_left > 0.0:
		return
	if now - _last_activation_msec < int(repeat_guard_seconds * 1000.0):
		return
	_last_activation_msec = now
	_play_press_feedback()
	activated.emit()


func _play_press_feedback() -> void:
	var duration: float = UiMotion.press_seconds()
	if is_zero_approx(duration) or not is_inside_tree():
		return
	_kill_feedback()
	_feedback_tween = create_tween()
	scale = Vector2(0.96, 0.96)
	_feedback_tween.tween_property(self, "scale", Vector2.ONE, duration)


func _on_mouse_entered() -> void:
	if focus_on_hover and not disabled and is_visible_in_tree():
		grab_focus()


func _update_pivot() -> void:
	pivot_offset = size * 0.5


func _kill_feedback() -> void:
	if _feedback_tween and _feedback_tween.is_valid():
		_feedback_tween.kill()
	scale = Vector2.ONE
	modulate = Color.WHITE

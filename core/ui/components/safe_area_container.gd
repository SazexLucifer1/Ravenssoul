class_name SafeAreaContainer
extends MarginContainer
## Keeps content inside the platform safe area plus a design margin.
##
## Desktop targets (Windows, Linux) report no insets, so this applies the base
## margin only; the inset path is exercised by tests via [member override_safe_rect]
## and becomes active automatically if a platform reports a safe area.

@export var base_margin: int = 32

## Safe rectangle in viewport coordinates. Empty = query the platform.
var override_safe_rect: Rect2 = Rect2():
	set(value):
		override_safe_rect = value
		refresh()


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	get_viewport().size_changed.connect(refresh)
	refresh()


func refresh() -> void:
	if not is_inside_tree():
		return
	var viewport_rect: Rect2 = get_viewport_rect()
	var safe: Rect2 = override_safe_rect if override_safe_rect.has_area() else _platform_safe_rect(viewport_rect)
	var insets: Dictionary = compute_insets(viewport_rect.size, safe)
	add_theme_constant_override(&"margin_left", base_margin + insets["left"])
	add_theme_constant_override(&"margin_top", base_margin + insets["top"])
	add_theme_constant_override(&"margin_right", base_margin + insets["right"])
	add_theme_constant_override(&"margin_bottom", base_margin + insets["bottom"])


static func compute_insets(viewport_size: Vector2, safe: Rect2) -> Dictionary:
	var clipped: Rect2 = safe.intersection(Rect2(Vector2.ZERO, viewport_size))
	return {
		"left": maxi(0, roundi(clipped.position.x)),
		"top": maxi(0, roundi(clipped.position.y)),
		"right": maxi(0, roundi(viewport_size.x - clipped.end.x)),
		"bottom": maxi(0, roundi(viewport_size.y - clipped.end.y)),
	}


func _platform_safe_rect(viewport_rect: Rect2) -> Rect2:
	# Windowed desktop: the whole viewport is safe. Mobile/console ports map
	# DisplayServer.get_display_safe_area() into viewport space here.
	return viewport_rect

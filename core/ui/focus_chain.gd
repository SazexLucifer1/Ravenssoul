class_name FocusChain
extends RefCounted
## Sets explicit focus neighbors so keyboard/controller navigation never
## reaches a dead end. Call after building a list of interactive controls.


## Links controls top-to-bottom. With [param wrap], the last wraps to the first.
static func link_vertical(controls: Array[Control], wrap: bool = true) -> void:
	_link(controls, wrap, &"focus_neighbor_top", &"focus_neighbor_bottom")


## Links controls in visual left-to-right order (mirrored for RTL layouts).
static func link_horizontal(controls: Array[Control], wrap: bool = true) -> void:
	var ordered: Array[Control] = controls.duplicate()
	if not ordered.is_empty() and ordered[0].is_layout_rtl():
		ordered.reverse()
	_link(ordered, wrap, &"focus_neighbor_left", &"focus_neighbor_right")


## Returns only the controls that can currently take focus.
static func focusable(controls: Array[Control]) -> Array[Control]:
	return controls.filter(func(c: Control) -> bool:
		return c.visible and c.focus_mode != Control.FOCUS_NONE
	)


static func _link(controls: Array[Control], wrap: bool, prev_prop: StringName, next_prop: StringName) -> void:
	var count: int = controls.size()
	for i: int in count:
		var control: Control = controls[i]
		var prev_index: int = i - 1 if i > 0 else (count - 1 if wrap else i)
		var next_index: int = i + 1 if i < count - 1 else (0 if wrap else i)
		control.set(prev_prop, control.get_path_to(controls[prev_index]))
		control.set(next_prop, control.get_path_to(controls[next_index]))
		control.focus_previous = control.get_path_to(controls[prev_index])
		control.focus_next = control.get_path_to(controls[next_index])

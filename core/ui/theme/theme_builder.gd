class_name ThemeBuilder
extends RefCounted
## Generates the project Theme from UiTokens.
## Rebuild after changing tokens:
##   godot --headless --script res://core/ui/theme/build_theme.gd

const TOKENS_PATH: String = "res://core/ui/theme/ui_tokens.tres"
const THEME_PATH: String = "res://assets/ui/theme/utopia_theme.tres"


static func build(t: UiTokens) -> Theme:
	var theme := Theme.new()
	theme.default_font_size = t.font_size_body

	# Labels and type variations.
	theme.set_color(&"font_color", &"Label", t.text_primary)
	_variation(theme, &"TitleLabel", &"Label")
	theme.set_font_size(&"font_size", &"TitleLabel", t.font_size_title)
	theme.set_color(&"font_color", &"TitleLabel", t.text_primary)
	_variation(theme, &"HeadingLabel", &"Label")
	theme.set_font_size(&"font_size", &"HeadingLabel", t.font_size_heading)
	_variation(theme, &"MutedLabel", &"Label")
	theme.set_color(&"font_color", &"MutedLabel", t.text_muted)
	_variation(theme, &"DangerLabel", &"Label")
	theme.set_color(&"font_color", &"DangerLabel", t.danger)
	_variation(theme, &"SuccessLabel", &"Label")
	theme.set_color(&"font_color", &"SuccessLabel", t.success)
	_variation(theme, &"KeycapLabel", &"Label")
	theme.set_font_size(&"font_size", &"KeycapLabel", t.font_size_small)
	theme.set_stylebox(&"normal", &"KeycapLabel", _box(t.surface_raised, t.border, t.border_width, t.corner_radius, t.spacing_s, t.spacing_xs))

	# Panels.
	theme.set_stylebox(&"panel", &"PanelContainer", _box(t.surface, t.border, t.border_width, t.corner_radius, t.spacing_l, t.spacing_l))
	_variation(theme, &"HudPanel", &"PanelContainer")
	theme.set_stylebox(&"panel", &"HudPanel", _box(Color(t.surface, 0.9), t.border, t.border_width, t.corner_radius, t.spacing_m, t.spacing_s))
	theme.set_stylebox(&"panel", &"Panel", _box(t.surface, t.border, t.border_width, t.corner_radius, 0, 0))

	# Buttons: every interactive state is explicit.
	_button_styles(theme, &"Button", t.surface_raised, t.border, t)
	theme.set_constant(&"h_separation", &"Button", t.spacing_s)
	_variation(theme, &"PrimaryButton", &"Button")
	_button_styles(theme, &"PrimaryButton", t.accent, t.accent_hover, t)
	theme.set_stylebox(&"hover", &"PrimaryButton", _box(t.accent_hover, t.accent_hover, t.border_width, t.corner_radius, t.spacing_l, t.spacing_s))
	theme.set_stylebox(&"pressed", &"PrimaryButton", _box(t.accent_pressed, t.accent_pressed, t.border_width, t.corner_radius, t.spacing_l, t.spacing_s))
	theme.set_stylebox(&"hover_pressed", &"PrimaryButton", theme.get_stylebox(&"pressed", &"PrimaryButton"))
	for state: StringName in [&"hover", &"pressed", &"hover_pressed"]:
		theme.set_stylebox(StringName(String(state) + "_mirrored"), &"PrimaryButton", theme.get_stylebox(state, &"PrimaryButton"))
	theme.set_color(&"font_color", &"PrimaryButton", t.text_on_accent)
	theme.set_color(&"font_hover_color", &"PrimaryButton", t.text_on_accent)
	theme.set_color(&"font_focus_color", &"PrimaryButton", t.text_on_accent)
	theme.set_color(&"font_pressed_color", &"PrimaryButton", t.text_on_accent)
	_variation(theme, &"DangerButton", &"Button")
	_button_styles(theme, &"DangerButton", t.surface_raised, t.danger, t)
	theme.set_color(&"font_color", &"DangerButton", t.danger)
	theme.set_color(&"font_hover_color", &"DangerButton", t.text_primary)

	_button_styles(theme, &"OptionButton", t.surface_raised, t.border, t)
	theme.set_color(&"font_color", &"CheckButton", t.text_primary)
	theme.set_color(&"font_hover_color", &"CheckButton", t.text_primary)
	theme.set_color(&"font_focus_color", &"CheckButton", t.text_primary)
	theme.set_color(&"font_pressed_color", &"CheckButton", t.text_primary)
	theme.set_stylebox(&"focus", &"CheckButton", _focus_box(t))
	theme.set_stylebox(&"panel", &"PopupMenu", _box(t.surface_raised, t.border, t.border_width, t.corner_radius, t.spacing_s, t.spacing_s))
	theme.set_stylebox(&"hover", &"PopupMenu", _box(t.accent, t.accent, 0, t.corner_radius, t.spacing_s, t.spacing_xs))
	theme.set_color(&"font_color", &"PopupMenu", t.text_primary)
	theme.set_color(&"font_hover_color", &"PopupMenu", t.text_on_accent)

	# Progress bars (health uses a semantic variation; text always accompanies color).
	theme.set_stylebox(&"background", &"ProgressBar", _box(t.background, t.border, t.border_width, t.corner_radius, 0, 0))
	theme.set_stylebox(&"fill", &"ProgressBar", _box(t.accent, t.accent, 0, t.corner_radius, 0, 0))
	theme.set_color(&"font_color", &"ProgressBar", t.text_primary)
	_variation(theme, &"HealthBar", &"ProgressBar")
	theme.set_stylebox(&"fill", &"HealthBar", _box(t.success, t.success, 0, t.corner_radius, 0, 0))
	_variation(theme, &"SoulBar", &"ProgressBar")
	theme.set_stylebox(&"fill", &"SoulBar", _box(t.soul, t.soul, 0, t.corner_radius, 0, 0))

	# Containers spacing.
	theme.set_constant(&"separation", &"VBoxContainer", t.spacing_m)
	theme.set_constant(&"separation", &"HBoxContainer", t.spacing_m)
	return theme


static func _button_styles(theme: Theme, type_name: StringName, fill: Color, edge: Color, t: UiTokens) -> void:
	var pad_x: int = t.spacing_l
	var pad_y: int = t.spacing_s
	theme.set_stylebox(&"normal", type_name, _box(fill, edge, t.border_width, t.corner_radius, pad_x, pad_y))
	theme.set_stylebox(&"hover", type_name, _box(fill.lightened(0.12), t.text_muted, t.border_width, t.corner_radius, pad_x, pad_y))
	theme.set_stylebox(&"pressed", type_name, _box(fill.darkened(0.25), t.text_primary, t.border_width, t.corner_radius, pad_x, pad_y))
	theme.set_stylebox(&"disabled", type_name, _box(Color(fill, 0.45), Color(edge, 0.35), t.border_width, t.corner_radius, pad_x, pad_y))
	theme.set_stylebox(&"hover_pressed", type_name, theme.get_stylebox(&"pressed", type_name))
	theme.set_stylebox(&"focus", type_name, _focus_box(t))
	# Right-to-left layouts use the *_mirrored variants (OptionButton arrow side).
	for state: StringName in [&"normal", &"hover", &"pressed", &"hover_pressed", &"disabled"]:
		theme.set_stylebox(StringName(String(state) + "_mirrored"), type_name, theme.get_stylebox(state, type_name))
	theme.set_color(&"font_color", type_name, t.text_primary)
	theme.set_color(&"font_hover_color", type_name, t.text_primary)
	theme.set_color(&"font_focus_color", type_name, t.text_primary)
	theme.set_color(&"font_pressed_color", type_name, t.text_primary)
	theme.set_color(&"font_disabled_color", type_name, t.text_disabled)


static func _focus_box(t: UiTokens) -> StyleBoxFlat:
	var box := _box(Color.TRANSPARENT, t.focus, t.focus_width, t.corner_radius + 2, 0, 0)
	box.draw_center = false
	box.set_expand_margin_all(t.focus_width + 1)
	return box


static func _box(fill: Color, edge: Color, edge_width: int, radius: int, pad_x: int, pad_y: int) -> StyleBoxFlat:
	var box := StyleBoxFlat.new()
	box.bg_color = fill
	box.border_color = edge
	box.set_border_width_all(edge_width)
	box.set_corner_radius_all(radius)
	box.content_margin_left = pad_x
	box.content_margin_right = pad_x
	box.content_margin_top = pad_y
	box.content_margin_bottom = pad_y
	return box


static func _variation(theme: Theme, variation: StringName, base: StringName) -> void:
	theme.set_type_variation(variation, base)

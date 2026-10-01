extends TestCase
## The committed Theme must be exactly what ThemeBuilder generates from the
## tokens, so nobody hand-edits it and the two never drift.


func test_committed_theme_matches_tokens() -> void:
	var built: Theme = ThemeBuilder.build(load(ThemeBuilder.TOKENS_PATH))
	var saved := ResourceLoader.load(ThemeBuilder.THEME_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as Theme
	assert_not_null(saved)
	for type_name: StringName in built.get_type_list():
		for color_name: StringName in built.get_color_list(type_name):
			assert_eq(saved.get_color(color_name, type_name), built.get_color(color_name, type_name), "%s/%s" % [type_name, color_name])
		for size_name: StringName in built.get_font_size_list(type_name):
			assert_eq(saved.get_font_size(size_name, type_name), built.get_font_size(size_name, type_name), "%s/%s" % [type_name, size_name])
		for box_name: StringName in built.get_stylebox_list(type_name):
			var a := built.get_stylebox(box_name, type_name) as StyleBoxFlat
			var b := saved.get_stylebox(box_name, type_name) as StyleBoxFlat
			assert_not_null(b, "%s/%s missing (rebuild the theme)" % [type_name, box_name])
			if b != null:
				assert_eq(b.bg_color, a.bg_color, "%s/%s bg" % [type_name, box_name])
				assert_eq(b.border_color, a.border_color, "%s/%s border" % [type_name, box_name])


func test_every_button_type_defines_all_interactive_states() -> void:
	var theme: Theme = ThemeDB.get_project_theme()
	for type_name: StringName in [&"Button", &"PrimaryButton", &"DangerButton", &"OptionButton"]:
		for state: StringName in [&"normal", &"hover", &"pressed", &"hover_pressed", &"disabled", &"focus",
				&"normal_mirrored", &"hover_mirrored", &"pressed_mirrored", &"disabled_mirrored"]:
			assert_true(theme.has_stylebox(state, type_name), "%s/%s" % [type_name, state])


func test_focus_ring_is_distinct_from_hover() -> void:
	var tokens: UiTokens = load(ThemeBuilder.TOKENS_PATH)
	assert_ne(tokens.focus, tokens.accent_hover)
	assert_ne(tokens.focus, tokens.text_muted)


func test_text_contrast_meets_wcag_aa() -> void:
	var t: UiTokens = load(ThemeBuilder.TOKENS_PATH)
	for pair: Array in [[t.text_primary, t.surface], [t.text_muted, t.surface], [t.text_on_accent, t.accent], [t.danger, t.surface], [t.danger, t.surface_raised], [t.success, t.surface], [t.text_primary, t.surface_raised]]:
		var ratio: float = _contrast(pair[0], pair[1])
		assert_true(ratio >= 4.5, "contrast %.2f for %s on %s" % [ratio, pair[0].to_html(), pair[1].to_html()])


func test_text_scale_scales_and_restores_without_compounding() -> void:
	var theme: Theme = ThemeBuilder.build(load(ThemeBuilder.TOKENS_PATH))
	var base: int = theme.get_font_size(&"font_size", &"TitleLabel")
	ThemeScaler.apply(theme, 1.5)
	ThemeScaler.apply(theme, 1.5)
	assert_eq(theme.get_font_size(&"font_size", &"TitleLabel"), roundi(base * 1.5))
	ThemeScaler.apply(theme, 1.0)
	assert_eq(theme.get_font_size(&"font_size", &"TitleLabel"), base)


func _contrast(a: Color, b: Color) -> float:
	var la: float = _luminance(a)
	var lb: float = _luminance(b)
	return (maxf(la, lb) + 0.05) / (minf(la, lb) + 0.05)


func _luminance(c: Color) -> float:
	var channels: Array[float] = []
	for v: float in [c.r, c.g, c.b]:
		channels.append(v / 12.92 if v <= 0.03928 else pow((v + 0.055) / 1.055, 2.4))
	return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]

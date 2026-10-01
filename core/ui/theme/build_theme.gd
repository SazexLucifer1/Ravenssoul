extends SceneTree
## Regenerates assets/ui/theme/utopia_theme.tres from ui_tokens.tres.
## Usage: godot --headless --script res://core/ui/theme/build_theme.gd


func _initialize() -> void:
	var tokens := load(ThemeBuilder.TOKENS_PATH) as UiTokens
	var theme: Theme = ThemeBuilder.build(tokens)
	var err: Error = ResourceSaver.save(theme, ThemeBuilder.THEME_PATH)
	print("Theme written to %s: %s" % [ThemeBuilder.THEME_PATH, error_string(err)])
	quit(0 if err == OK else 1)

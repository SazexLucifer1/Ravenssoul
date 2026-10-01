class_name ThemeScaler
extends RefCounted
## Scales every font size in a Theme for the "text size" accessibility option.
## Base sizes are remembered in theme metadata so scaling never compounds.

const META_KEY: StringName = &"base_font_sizes"


static func apply(theme: Theme, scale: float) -> void:
	if theme == null:
		return
	if not theme.has_meta(META_KEY):
		var bases: Dictionary = {}
		for type_name: StringName in theme.get_font_size_type_list():
			for size_name: StringName in theme.get_font_size_list(type_name):
				bases["%s/%s" % [type_name, size_name]] = theme.get_font_size(size_name, type_name)
		bases["__default__"] = theme.default_font_size
		theme.set_meta(META_KEY, bases)
	var stored: Dictionary = theme.get_meta(META_KEY)
	for key: String in stored:
		var scaled: int = roundi(int(stored[key]) * scale)
		if key == "__default__":
			theme.default_font_size = scaled
		else:
			theme.set_font_size(StringName(key.get_slice("/", 1)), StringName(key.get_slice("/", 0)), scaled)

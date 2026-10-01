extends Node
## Draws review annotations (focal path, competing focal points, unclear
## state, highest-impact fix) onto captured frames for
## docs/VISUAL_QUALITY_RUBRIC.md. Needs a renderer (Xvfb on Linux):
##   xvfb-run -a godot --path . --rendering-driver opengl3 --audio-driver Dummy \
##     res://tests/tools/annotate_frames.tscn -- --in=<captures dir> --out=docs/art/annotated
## Specs live in SPECS below; coordinates are in 1280x720 frame pixels.

const FOCAL := Color("6fe08a")
const COMPETING := Color("f2c14e")
const PROBLEM := Color("ff5a4f")
const FIX := Color("8cc4ff")

const SPECS: Array[Dictionary] = [
	{"input": "state_battle_start.png", "output": "battle_start_annotated.png", "marks": [
		{"type": "circle", "at": [352, 360], "r": 26, "color": FOCAL, "label": "1 unit (intended first read) - only ~12 art px tall", "label_at": [300, 312]},
		{"type": "circle", "at": [352, 365], "r": 62, "color": FOCAL, "label": "2 move range", "label_at": [140, 470]},
		{"type": "circle", "at": [928, 366], "r": 24, "color": FOCAL, "label": "3 objective", "label_at": [880, 410]},
		{"type": "arrow", "from": [380, 352], "to": [900, 362], "color": FOCAL},
		{"type": "rect", "rect": [32, 32, 260, 146], "color": COMPETING, "label": "competing: bright HUD frames out-weigh the unit", "label_at": [32, 196]},
		{"type": "rect", "rect": [948, 32, 300, 164], "color": COMPETING, "label": "", "label_at": [948, 210]},
		{"type": "rect", "rect": [430, 590, 420, 40], "color": PROBLEM, "label": "missing: turn / AP / hand / threat - no tactical state shown", "label_at": [470, 652]},
		{"type": "rect", "rect": [256, 176, 768, 384], "color": FIX, "label": "fix #1: 32x48 unit sprites + iso tile art; fix #2: card hand + AP bar bottom-centre", "label_at": [256, 572]},
	]},
	{"input": "en_main_menu.png", "output": "main_menu_annotated.png", "marks": [
		{"type": "circle", "at": [224, 298], "r": 34, "color": FOCAL, "label": "1 primary action (clear)", "label_at": [400, 290]},
		{"type": "rect", "rect": [460, 60, 780, 560], "color": PROBLEM, "label": "60% of frame empty: no key art / world fantasy", "label_at": [600, 340]},
		{"type": "rect", "rect": [60, 70, 200, 70], "color": FIX, "label": "fix: pixel display font + emblem; wood/brass frame art", "label_at": [60, 230]},
	]},
]

var _in_dir: String = ""
var _out_dir: String = "res://docs/art/annotated"


func _ready() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--in="):
			_in_dir = arg.trim_prefix("--in=")
		elif arg.begins_with("--out="):
			_out_dir = arg.trim_prefix("--out=")
	DirAccess.make_dir_recursive_absolute(_out_dir)
	for spec: Dictionary in SPECS:
		await _render(spec)
	get_tree().quit()


func _render(spec: Dictionary) -> void:
	var image := Image.load_from_file(_in_dir.path_join(spec["input"]))
	if image == null:
		push_error("missing capture %s" % spec["input"])
		return
	var viewport := SubViewport.new()
	viewport.size = Vector2i(1280, 720)
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	var frame := TextureRect.new()
	frame.texture = ImageTexture.create_from_image(image)
	viewport.add_child(frame)
	var overlay := _Overlay.new()
	overlay.marks = spec["marks"]
	overlay.size = Vector2(1280, 720)
	viewport.add_child(overlay)
	add_child(viewport)
	await RenderingServer.frame_post_draw
	await RenderingServer.frame_post_draw
	var out: String = _out_dir.path_join(spec["output"])
	viewport.get_texture().get_image().save_png(out)
	print("saved ", out)
	viewport.queue_free()


class _Overlay extends Control:
	var marks: Array = []

	func _draw() -> void:
		var font: Font = ThemeDB.fallback_font
		for mark: Dictionary in marks:
			var color: Color = mark["color"]
			var label_at: Vector2
			match mark["type"]:
				"circle":
					var at := Vector2(mark["at"][0], mark["at"][1])
					draw_arc(at, mark["r"], 0, TAU, 48, color, 3.0)
					label_at = at + Vector2(mark["r"] + 6, -4)
				"rect":
					var r: Array = mark["rect"]
					draw_rect(Rect2(r[0], r[1], r[2], r[3]), color, false, 3.0)
					label_at = Vector2(r[0], r[1] + r[3] + 18)
				"arrow":
					var a := Vector2(mark["from"][0], mark["from"][1])
					var b := Vector2(mark["to"][0], mark["to"][1])
					draw_dashed_line(a, b, color, 3.0, 10.0)
					var d: Vector2 = (b - a).normalized()
					draw_colored_polygon(PackedVector2Array([b, b - d * 14 + d.orthogonal() * 7, b - d * 14 - d.orthogonal() * 7]), color)
			if mark.has("label_at"):
				label_at = Vector2(mark["label_at"][0], mark["label_at"][1])
			var text: String = mark.get("label", "")
			if not text.is_empty():
				var width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, 16).x
				draw_rect(Rect2(label_at + Vector2(-4, -16), Vector2(width + 8, 22)), Color(0, 0, 0, 0.75))
				draw_string(font, label_at, text, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, color)

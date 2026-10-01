class_name UnitVisual
extends Node2D
## Placeholder unit art drawn in code until sprites exist. Swap this node for
## a Sprite2D/AnimatedSprite2D scene without touching gameplay scripts.

@export var body_color: Color = Color("3d6f9e")
@export var trim_color: Color = Color("ece6d6")
@export var radius: float = 6.0

## 1 = facing right, -1 = facing left. Mirrors the banner.
var facing: int = 1:
	set(value):
		facing = 1 if value >= 0 else -1
		queue_redraw()


func _draw() -> void:
	# Contact shadow grounds the unit on the tile (art guide: grounding).
	draw_set_transform(Vector2(0, radius * 0.9), 0.0, Vector2(1.0, 0.5))
	draw_circle(Vector2.ZERO, radius, Color(0, 0, 0, 0.35))
	draw_set_transform(Vector2(0, -radius * 0.6), 0.0, Vector2.ONE)
	draw_circle(Vector2.ZERO, radius, body_color)
	draw_arc(Vector2.ZERO, radius, 0.0, TAU, 24, trim_color, 1.0)
	var pole := Vector2(radius * 0.5 * facing, -radius)
	draw_line(pole, pole + Vector2(0, -radius), trim_color, 1.0)
	draw_colored_polygon(PackedVector2Array([
		pole + Vector2(0, -radius),
		pole + Vector2(radius * 0.8 * facing, -radius * 0.75),
		pole + Vector2(0, -radius * 0.5),
	]), body_color.lightened(0.2))

class_name CharacterAnimator
extends Node
## Translates gameplay signals into animation. Gameplay never calls
## AnimationPlayer directly, and animation never decides gameplay outcomes.
##
## Animation tracks call [method emit_animation_event] at authored frames
## (footsteps, impact frames); audio/VFX/camera listeners subscribe to
## [signal animation_event].

signal animation_event(event_name: StringName)

const ANIM_IDLE: StringName = &"unit/idle"
const ANIM_MOVE: StringName = &"unit/move"
const ANIM_HIT: StringName = &"unit/hit"
const ANIM_DEFEAT: StringName = &"unit/defeat"

@export var player: AnimationPlayer
@export var health: HealthComponent
@export var mover: GridMover
@export var visual: UnitVisual


func _ready() -> void:
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	mover.move_started.connect(_on_move_started)
	mover.move_finished.connect(_on_move_finished)
	player.animation_finished.connect(_on_animation_finished)
	play(ANIM_IDLE)


func play(animation: StringName) -> void:
	if health.is_dead() and animation != ANIM_DEFEAT:
		return
	if player.has_animation(animation):
		player.play(animation)


func current_animation() -> StringName:
	return StringName(player.current_animation)


## Called from animation method tracks.
func emit_animation_event(event_name: StringName) -> void:
	animation_event.emit(event_name)


func _on_move_started(direction: Vector2i) -> void:
	if direction.x != 0:
		visual.facing = signi(direction.x)
	play(ANIM_MOVE)


func _on_move_finished() -> void:
	if current_animation() == ANIM_MOVE:
		play(ANIM_IDLE)


func _on_damaged(_amount: int, _source: StringName) -> void:
	if not health.is_dead():
		play(ANIM_HIT)


func _on_died() -> void:
	play(ANIM_DEFEAT)


func _on_animation_finished(animation: StringName) -> void:
	if animation == ANIM_HIT:
		play(ANIM_IDLE)

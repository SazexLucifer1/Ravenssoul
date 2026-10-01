class_name PauseMenu
extends UiScreen
## Pause menu. Emits intent; the gameplay scene decides what happens.

signal resume_requested
signal settings_requested
signal abandon_requested

@export var resume_button: GameButton
@export var settings_button: GameButton
@export var abandon_button: GameButton


func _ready() -> void:
	resume_button.activated.connect(resume_requested.emit)
	settings_button.activated.connect(settings_requested.emit)
	abandon_button.activated.connect(abandon_requested.emit)
	FocusChain.link_vertical([resume_button, settings_button, abandon_button] as Array[Control])
	default_focus = resume_button


func on_cancel() -> void:
	resume_requested.emit()

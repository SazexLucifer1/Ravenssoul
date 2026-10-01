class_name ConfirmDialog
extends UiScreen
## Generic yes/no confirmation. Destructive confirmations focus "cancel" first
## so a stray press of the accept button never destroys progress.

signal confirmed
signal cancelled

@export var title_label: Label
@export var body_label: Label
@export var confirm_button: GameButton
@export var cancel_button: GameButton


func _ready() -> void:
	confirm_button.activated.connect(_on_confirm)
	cancel_button.activated.connect(on_cancel)


## context keys: title_key, body_key, confirm_key, cancel_key,
## destructive (bool), single_action (bool: message box with one button).
func on_opened(context: Dictionary) -> void:
	title_label.text = context.get("title_key", "CONFIRM_TITLE_DEFAULT")
	body_label.text = context.get("body_key", "")
	body_label.visible = not body_label.text.is_empty()
	confirm_button.text = context.get("confirm_key", "UI_CONFIRM")
	cancel_button.text = context.get("cancel_key", "UI_CANCEL")
	var destructive: bool = context.get("destructive", false)
	confirm_button.theme_type_variation = &"DangerButton" if destructive else &"PrimaryButton"
	var single: bool = context.get("single_action", false)
	cancel_button.visible = not single
	default_focus = cancel_button if destructive and not single else confirm_button
	FocusChain.link_horizontal(FocusChain.focusable([cancel_button, confirm_button] as Array[Control]))


func on_cancel() -> void:
	cancelled.emit()
	close_requested.emit()


func _on_confirm() -> void:
	confirmed.emit()
	close_requested.emit()

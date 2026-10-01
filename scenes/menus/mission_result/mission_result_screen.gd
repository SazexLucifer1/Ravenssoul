class_name MissionResultScreen
extends UiScreen
## Victory/defeat summary. Shows rewards and the autosave state in plain
## language, and offers a clear next action. Emits intent only.

signal continue_requested
signal retry_requested
signal save_retry_requested

enum SaveState { HIDDEN, SAVING, SAVED, FAILED }

@export var title_label: Label
@export var body_label: Label
@export var rewards_label: Label
@export var save_status_label: Label
@export var continue_button: GameButton
@export var retry_button: GameButton
@export var save_retry_button: GameButton

var save_state: SaveState = SaveState.HIDDEN


func _ready() -> void:
	cancel_closes = false
	continue_button.activated.connect(continue_requested.emit)
	retry_button.activated.connect(retry_requested.emit)
	save_retry_button.activated.connect(save_retry_requested.emit)


## context: victory (bool), rewards (Dictionary[StringName, int])
func on_opened(context: Dictionary) -> void:
	var victory: bool = context.get("victory", false)
	title_label.text = "RESULT_VICTORY_TITLE" if victory else "RESULT_DEFEAT_TITLE"
	title_label.theme_type_variation = &"SuccessLabel" if victory else &"DangerLabel"
	body_label.text = "RESULT_VICTORY_BODY" if victory else "RESULT_DEFEAT_BODY"
	rewards_label.text = format_rewards(context.get("rewards", {}))
	rewards_label.visible = not rewards_label.text.is_empty()
	retry_button.visible = not victory
	continue_button.text = "RESULT_CONTINUE" if victory else "RESULT_RETURN_TO_MENU"
	continue_button.theme_type_variation = &"PrimaryButton" if victory else &"Button"
	default_focus = continue_button if victory else retry_button
	_relink_focus()
	set_save_state(SaveState.HIDDEN)


func set_save_state(state: SaveState, message_key: String = "") -> void:
	save_state = state
	save_status_label.visible = state != SaveState.HIDDEN
	save_retry_button.visible = state == SaveState.FAILED
	continue_button.set_busy(state == SaveState.SAVING)
	match state:
		SaveState.SAVING:
			save_status_label.theme_type_variation = &"MutedLabel"
			save_status_label.text = "SAVE_STATUS_SAVING"
		SaveState.SAVED:
			save_status_label.theme_type_variation = &"SuccessLabel"
			save_status_label.text = "SAVE_STATUS_SAVED"
		SaveState.FAILED:
			save_status_label.theme_type_variation = &"DangerLabel"
			save_status_label.text = message_key
	_relink_focus()


static func format_rewards(rewards: Dictionary) -> String:
	if rewards.is_empty():
		return ""
	var parts := PackedStringArray()
	for resource_id: Variant in rewards:
		var amount: int = int(rewards[resource_id])
		parts.append(Loc.template(&"REWARD_LINE").format({
			"amount": LocaleFormat.integer(amount),
			"resource": Loc.template(StringName("RESOURCE_%s" % String(resource_id).to_upper())),
		}))
	return Loc.format(&"RESULT_REWARDS", {"list": Loc.template(&"LIST_SEPARATOR").join(parts)})


func _relink_focus() -> void:
	var buttons: Array[Control] = [retry_button, continue_button, save_retry_button]
	FocusChain.link_horizontal(FocusChain.focusable(buttons))

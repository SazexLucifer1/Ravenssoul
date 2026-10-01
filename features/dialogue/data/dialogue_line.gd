class_name DialogueLine
extends Resource
## One line of dialogue. Text is a translation key; voice/subtitle variants
## are resolved per locale at playback time.

@export var speaker_name_key: String
@export var text_key: String
@export var portrait: Texture2D
## Optional localized voice clip id (resolved per locale, see docs/LOCALIZATION.md).
@export var voice_id: StringName

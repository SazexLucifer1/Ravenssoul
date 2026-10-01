class_name UiTokens
extends Resource
## Design tokens for the "Ash & Ember" art direction (provisional; see
## docs/UI_ARCHITECTURE.md). The Theme is generated from these values by
## ThemeBuilder — change tokens, then rebuild the theme; never hand-edit it.

@export_group("Surfaces")
@export var background: Color = Color("14161c")
@export var surface: Color = Color("1f222b")
@export var surface_raised: Color = Color("2a2e39")
@export var border: Color = Color("4a4f5e")
@export var scrim: Color = Color(0.03, 0.03, 0.05, 0.72)

@export_group("Text")
@export var text_primary: Color = Color("ece6d6")
@export var text_muted: Color = Color("b3ad9f")
@export var text_disabled: Color = Color("7d7a72")
@export var text_on_accent: Color = Color("fff8ec")

@export_group("Semantic")
## Revolution ember: primary actions.
@export var accent: Color = Color("b8452f")
@export var accent_hover: Color = Color("d05a42")
@export var accent_pressed: Color = Color("8f3423")
## Spectral teal: soul energy only.
@export var soul: Color = Color("5fc9c0")
@export var danger: Color = Color("ee7366")
@export var success: Color = Color("7fb069")
@export var warning: Color = Color("e0b04f")
## Focus ring must stay distinct from hover and from every semantic color.
@export var focus: Color = Color("f2c14e")

@export_group("Type")
@export var font_size_small: int = 16
@export var font_size_body: int = 20
@export var font_size_heading: int = 30
@export var font_size_title: int = 56

@export_group("Shape and spacing")
@export var corner_radius: int = 4
@export var border_width: int = 2
@export var focus_width: int = 3
@export var spacing_xs: int = 4
@export var spacing_s: int = 8
@export var spacing_m: int = 16
@export var spacing_l: int = 24
@export var spacing_xl: int = 40
## Minimum interactive target, also used for touch-safe sizing.
@export var button_min_height: int = 52
@export var button_min_width: int = 280

@export_group("Motion")
@export_range(0.15, 0.3) var transition_seconds: float = 0.2
@export_range(0.05, 0.2) var press_feedback_seconds: float = 0.1

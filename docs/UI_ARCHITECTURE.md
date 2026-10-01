# UI architecture

Game interface, not a website: large focusable buttons, one primary action
per screen, controller-first navigation, short transitions, art-directed
theme.

## Screen inventory

| Screen | State | File |
|---|---|---|
| Boot / loading | Done (loading text + player-readable startup error) | `scenes/bootstrap/` |
| Title (main menu) | Done | `scenes/main_menu/` |
| Settings (language, text size, reduced motion) | Done | `scenes/menus/settings/` |
| Pause | Done | `scenes/menus/pause/` |
| Confirmation / message | Done (generic, destructive variant, single-action) | `core/ui/screens/confirm_dialog.tscn` |
| Mission result (victory/defeat + rewards + save state) | Done | `scenes/menus/mission_result/` |
| Battle HUD (unit, health, objective, prompts, hints) | Done | `scenes/gameplay/hud/` |
| Save/load slot list | Planned (single autosave slot now) | — |
| Unit combine (hero + battalion), deck view, card hand | Planned (core loop) | — |
| Base / buildings / card upgrades, scouting, mission select | Planned | — |
| Dialogue, notifications/toasts, tutorial | Planned | — |
| Inventory / equipment / shop / skills / quest log | Not in the blueprint's first playable | — |

## Building blocks (`core/ui/`)

| Piece | Role |
|---|---|
| `UiTokens` (`theme/ui_tokens.tres`) | Colors, type sizes, spacing, radii, minimum target size, motion durations |
| `ThemeBuilder` → `assets/ui/theme/utopia_theme.tres` | Generated project theme. Rebuild: `godot --headless --script res://core/ui/theme/build_theme.gd`. A test fails if the committed theme drifts from the tokens |
| `ThemeScaler` | Text-size setting (100/125/150 %) by scaling theme font sizes from stored bases |
| `GameButton` | Button with game states + `activated` signal |
| `UiScreen` | Base for pushed screens: `default_focus`, `cancel_closes`, `on_opened(context)`, `on_cancel()`, open transition |
| `ScreenStack` | Scene-owned modal stack: blocks focus/mouse below, scrim, restores focus on pop, routes `ui_cancel` to the top screen |
| `ConfirmDialog` | Reusable confirm/message dialog |
| `FocusChain` | Explicit focus neighbors (vertical, horizontal RTL-aware, wrap) |
| `SafeAreaContainer` | Design margin + platform safe-area insets |
| `ActionPrompt` | `[glyph] text` hint that follows the input method |
| `UiMotion` | Transition/press durations honoring reduced motion |

Theme type variations: `PrimaryButton`, `DangerButton`, `TitleLabel`,
`HeadingLabel`, `MutedLabel`, `DangerLabel`, `SuccessLabel`,
`DangerHeadingLabel`, `SuccessHeadingLabel`, `KeycapLabel`,
`HudPanel`, `HealthBar`, `SoulBar`.

### Art direction tokens

Warm wood / brass / parchment with darker undertones (approved art
direction). Palette, materials, typography, icon language, card and HUD
rules, and the "must never look like" list are in
[UI_STYLE_GUIDE.md](UI_STYLE_GUIDE.md); token values live in
`core/ui/theme/ui_tokens.tres`. Final art (fonts, frames, icons) replaces
tokens/styleboxes, not screen code.

## Button states

| State | How |
|---|---|
| idle / hover / pressed / disabled | Theme styleboxes (`normal`, `hover`, `pressed`, `hover_pressed`, `disabled`, + `*_mirrored` for RTL) |
| focus | Separate gold ring stylebox drawn over the state |
| selected | `toggle_mode` + `button_pressed` → `GameButton.State.SELECTED` |
| busy / loading | `set_busy(true)`: disabled + `busy_text_key` label (e.g. "Saving…") |
| error | `show_error()`: danger tint, shake unless reduced motion, then recovers |
| cooldown | `start_cooldown(s)`: disabled with draining overlay |

`GameButton.get_state()` exposes the effective state for tests.
Repeat input: presses within `repeat_guard_seconds` (0.3 s) of an activation
are ignored; busy/cooldown also block. Hover moves focus, so only one element
is ever highlighted.

## Rules

- **Game state never lives in Controls.** Screens emit `*_requested`
  signals; the owning scene changes state through services/features.
- **No UI autoload.** Each routed scene owns a `ScreenStack`.
- **One clear primary action** per screen (`PrimaryButton`, test-enforced
  for pause/settings). Destructive confirms use `DangerButton` and focus
  *cancel* first.
- **Progressive disclosure**: the HUD shows unit, health, objective, and
  controls only; detail screens open on demand.
- **Focus**: every screen sets `default_focus`; every interactive control has
  `focus_next/previous` (and directional neighbors) set via `FocusChain`;
  a test checks there are no dead ends. Closing a screen restores the
  previously focused control.
- **ui_cancel** always closes the top screen unless the screen opts out
  (result screen) — it never quits the game or loses progress silently.
- **Transitions**: 150–300 ms (`UiTokens.transition_seconds` = 0.2 s, range
  enforced by the export hint); route fade 0.2 s; press feedback 0.1 s.
  Reduced motion → 0 (snap, no shake).
- **Feedback**: immediate visual press feedback; audio hooks will attach to
  `GameButton.activated` once an audio service exists.
- **Targets**: interactive controls ≥ 44 px tall at 1280×720 (buttons are
  52 px); test-enforced.
- **Accessibility**: text contrast ≥ 4.5:1 (test-enforced for token pairs);
  text size 100/125/150 %; reduced motion; information never by color alone
  (health shows numbers, hazards are hatched, objectives have a marker).
- **Resolution/aspect**: base 1280×720, `canvas_items` + `expand`; layouts
  use containers and anchors. Tested at 1280×720, 1366×768, 1920×1080,
  2560×1440, 2560×1080 (21:9), 1280×1024 (5:4), 1024×768 (4:3).
- **Localization**: no display strings in scenes (test-enforced); labels that
  may grow use autowrap; tested with +30 % pseudo expansion, German, and RTL.
- **Player-readable output**: no raw errors, ids, paths, or enum names on
  screen; see "Player-facing messages" below.

## Player-facing messages

Errors explain what happened, whether progress is safe, and what to do next
(`SAVE_ERROR_*`, `BOOT_ERROR_BODY`, `ROUTE_FAILED_BODY`). Technical detail
goes to `DevLog` (development logs) only. Covered states today: loading
(boot), saving / saved / save failed + retry, load failed (damaged, newer
version, unreadable, missing), startup failure, scene failed to open,
victory, defeat, blocked move, hazard. Not applicable yet: offline,
permission prompts, reconnect (single-player, no online features).

## Automation badge

When the remote automation server is running, a small localized badge
(`AUTOMATION_BADGE`: "Automated test session active") is shown at the top
center on canvas layer 120, ignoring the mouse. It never appears in normal
play. Every interactive control must carry an `automation_id` (see
`SCENE_TREE_RULES.md`).

## Tests

`tests/unit/ui/` (button states, repeat input, busy/cooldown/error, stack
modality, focus restoration, cancel routing, safe area, theme
completeness/contrast/scaling) and `tests/integration/test_ui_layout.gd`
(resolutions, pseudo, German, 150 % text, RTL mirroring and focus order, no
focus dead ends, one primary action), plus flows in `test_boot_flow.gd` and
`test_gameplay_flow.gd`. Input-method switching: `test_input_method.gd`.
Visual review: screenshot tool (see `TESTING.md`).

## Known limitations

Text glyphs instead of controller icons; no UI sounds; no toast/notification
component yet; screen-reader support has not been configured or
tested; the settings screen has no display
options (fullscreen/resolution) yet.

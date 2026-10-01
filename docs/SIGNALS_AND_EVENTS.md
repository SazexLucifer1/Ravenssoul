# Signals and events

## When to use a signal

Use a signal when the **sender should not depend on the receiver**:
components announce facts (`HealthComponent.died`), screens announce intent
(`PauseMenu.resume_requested`), services announce state changes
(`Settings.locale_changed`). Call a method directly when the caller already
owns the callee (a scene root calling into its own child, a controller
calling `grid.relocate()`).

Rules:

- Name signals as past-tense facts (`damaged`, `route_changed`) or
  `*_requested` intents. Never imperative (`do_damage`).
- Signals carry stable data (ints, ids, `StringName`, Resources), never
  translated text.
- Connect in the composition root (`_ready` of the owning scene), not inside
  the emitter.
- No global event bus. If two features need to talk, the scene that owns
  both wires them.
- UI buttons: connect to `GameButton.activated`, never `pressed` (repeat
  guard, busy, cooldown are applied in `activated`).

## Inventory

### Autoload services

| Signal | Emitted when | Typical listeners |
|---|---|---|
| `Settings.locale_changed(locale)` | Player picks a language | Labels that format text in code (`HealthBar`, main menu version) |
| `Settings.reduced_motion_changed(enabled)` | Toggle changed | (read on demand via `UiMotion`) |
| `Settings.text_scale_changed(scale)` | Text size changed | (theme rescaled centrally) |
| `InputMethod.method_changed(method)` | Keyboard/mouse ↔ gamepad switch | `ActionPrompt` |
| `SaveService.save_finished(slot, result)` / `load_finished(slot, result)` | After each save/load | Future notifications/telemetry |
| `SceneRouter.route_changing(from, to)` / `route_changed(route)` / `route_failed(route)` | Scene transitions | `MainMenu` (failure → reset busy buttons, explain) |
| `GameSession.campaign_started` / `campaign_loaded` | New campaign / save applied | Future base scene |

### Components and features

| Signal | Contract |
|---|---|
| `HealthComponent.health_changed(current, max, delta)` | Every change incl. `set_current_health` |
| `HealthComponent.damaged(amount, source_id)` | Only real damage (> 0); `source_id` is a stable id like `&"hazard"` |
| `HealthComponent.healed(amount)` | Only real healing |
| `HealthComponent.died` | Exactly once per death |
| `Character.cell_changed(cell)` | Logical cell changed (immediately, before visuals) |
| `Character.defeated` | Relay of `died` |
| `GridMover.move_started(direction)` / `move_finished` | Visual tween start/end |
| `CharacterAnimator.animation_event(name)` | Authored animation frames (`footstep`, `hit_impact`) — feedback only |
| `BattleGrid.occupancy_changed(cell)` | Occupant placed/moved/removed |
| `PlayerUnitController.stepped / step_blocked / hazard_entered / objective_reached` | Results of `try_step` |
| `ResourceWallet.changed(id, amount)` | Resource amount changed |

### UI

| Signal | Contract |
|---|---|
| `GameButton.activated` | Filtered press (repeat guard 0.3 s default, not busy, not cooling down) |
| `UiScreen.close_requested` | Screen asks its stack to pop it |
| `ScreenStack.screen_opened / screen_closed / emptied` | Stack changes; gameplay unpauses on `emptied` |
| `ConfirmDialog.confirmed / cancelled` | Choice made (dialog closes itself) |
| `PauseMenu.resume_requested / settings_requested / abandon_requested` | Intent; gameplay decides |
| `MissionResultScreen.continue_requested / retry_requested / save_retry_requested` | Intent; gameplay decides |
| `Gameplay.mission_finished(victory)` | Mission over (rewards already applied on victory) |

## Animation events vs. gameplay events

Animation method tracks call `CharacterAnimator.emit_animation_event()`.
These events drive **feedback only** (audio, VFX, camera shake). Gameplay
outcomes (damage, death, movement legality) are decided before animation
plays and never wait for an animation event. See
[ANIMATION_AND_MOVEMENT.md](ANIMATION_AND_MOVEMENT.md).

## Input actions

| Action | Keyboard | Gamepad | Used by |
|---|---|---|---|
| `ui_accept` | Enter, Keypad Enter, Space | A | buttons |
| `ui_cancel` | Escape | B | `ScreenStack` (close top screen) |
| `pause` | Escape | Start | gameplay (open/close pause menu) |
| `ui_up/down/left/right` | Arrows | D-pad, left stick | focus navigation, unit movement |

Godot's defaults leave `ui_accept`/`ui_cancel` without gamepad bindings, so
`project.godot` overrides them; a test asserts every listed action has both
a keyboard and a gamepad binding.

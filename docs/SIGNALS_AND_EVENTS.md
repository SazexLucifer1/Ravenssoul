# Signals and events

## When to use a signal

Use a signal when the **sender should not depend on the receiver**:
components announce facts (`HealthComponent.Died`), screens announce intent
(`PauseMenu.ResumeRequested`), services announce state changes
(`Settings.LocaleChanged`). Call a method directly when the caller already
owns the callee (a scene root calling into its own child, a controller
calling `grid.Relocate()`).

Rules:

- Declare signals as `[Signal] public delegate void <Name>EventHandler(...)`;
  the signal is `<Name>`. Name them as past-tense facts (`Damaged`,
  `RouteChanged`) or `*Requested` intents. Never imperative (`DoDamage`).
- Signals carry stable data (ints, ids, `StringName`, Resources), never
  translated text.
- Connect in the composition root (`_Ready` of the owning scene), not inside
  the emitter. Use the generated C# events (`unit.Defeated += OnDefeated;`)
  and emit with the generated `EmitSignal<Name>(...)`.
- **C# does not disconnect a freed node from a longer-lived emitter.** A node
  that listens to an autoload (or any object that outlives it) subscribes in
  `_EnterTree` and unsubscribes in `_ExitTree` with a named method, never a
  lambda (guard G003, [MISTAKES.md](MISTAKES.md) M001). Listening to your own
  children or to objects freed together with you needs no unsubscribe.
- No global event bus. If two features need to talk, the scene that owns
  both wires them.
- UI buttons: connect to `GameButton.Activated`, never `pressed` (repeat
  guard, busy, cooldown are applied in `activated`).

## Inventory

### Autoload services

| Signal | Emitted when | Typical listeners |
|---|---|---|
| `Settings.LocaleChanged(locale)` | Player picks a language | Labels that format text in code (`HealthBar`, main menu version) |
| `Settings.ReducedMotionChanged(enabled)` | Toggle changed | (read on demand via `UiMotion`) |
| `Settings.TextScaleChanged(scale)` | Text size changed | (theme rescaled centrally) |
| `InputMethod.MethodChanged(method)` | Keyboard/mouse ↔ gamepad switch | `ActionPrompt` |
| `SaveService.SaveFinished(slot, result)` / `LoadFinished(slot, result)` | After each save/load | Future notifications/telemetry |
| `SceneRouter.RouteChanging(from, to)` / `RouteChanged(route)` / `RouteFailed(route)` | Scene transitions | `MainMenu` (failure → reset busy buttons, explain) |
| `GameSession.CampaignStarted` / `CampaignLoaded` | New campaign / save applied | Future base scene |

### Components and features

| Signal | Contract |
|---|---|
| `HealthComponent.HealthChanged(current, max, delta)` | Every change incl. `SetCurrentHealth` |
| `HealthComponent.Damaged(amount, sourceId)` | Only real damage (> 0); `sourceId` is a stable id like `"hazard"` |
| `HealthComponent.Healed(amount)` | Only real healing |
| `HealthComponent.Died` | Exactly once per death |
| `Character.CellChanged(cell)` | Logical cell changed (immediately, before visuals) |
| `Character.Defeated` | Relay of `Died` |
| `GridMover.MoveStarted(direction)` / `MoveFinished` | Visual tween start/end |
| `CharacterAnimator.AnimationEvent(name)` | Authored animation frames (`footstep`, `hit_impact`) — feedback only |
| `BattleGrid.OccupancyChanged(cell)` | Occupant placed/moved/removed |
| `PlayerUnitController.Stepped / StepBlocked / HazardEntered / ObjectiveReached` | Results of `TryStep` |
| `ResourceWallet.Changed(id, amount)` | Resource amount changed |

### UI

| Signal | Contract |
|---|---|
| `GameButton.Activated` | Filtered press (repeat guard 0.3 s default, not busy, not cooling down) |
| `UiScreen.CloseRequested` | Screen asks its stack to pop it |
| `ScreenStack.ScreenOpened / ScreenClosed / Emptied` | Stack changes; gameplay unpauses on `Emptied` |
| `ConfirmDialog.Confirmed / Cancelled` | Choice made (dialog closes itself) |
| `PauseMenu.ResumeRequested / SettingsRequested / AbandonRequested` | Intent; gameplay decides |
| `MissionResultScreen.ContinueRequested / RetryRequested / SaveRetryRequested` | Intent; gameplay decides |
| `Gameplay.MissionFinished(victory)` | Mission over (rewards already applied on victory) |

## Animation events vs. gameplay events

Animation method tracks call `CharacterAnimator.EmitAnimationEvent()`.
These events drive **feedback only** (audio, VFX, camera shake). Gameplay
outcomes (damage, death, movement legality) are decided before animation
plays and never wait for an animation event. See
[ANIMATION_AND_MOVEMENT.md](ANIMATION_AND_MOVEMENT.md).

## Automation events

The automation layer (`automation/adapter/`) subscribes to the signals above
and republishes them as protocol events (`route.changed`, `screen.opened`,
`unit.moved`, `unit.damaged`, `mission.finished`, `save.finished`, …) with
ids and numbers only. Game code never emits automation events itself; adding
a signal that tests should observe means hooking it in the provider's
`HookScene()` or the adapter's `ConnectEvents()`. Full list:
[AUTOMATION.md](AUTOMATION.md#events).

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

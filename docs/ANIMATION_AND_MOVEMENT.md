# Movement and animation audit

Audit of the approved blueprint for motion needs, and how responsibilities
are divided. Utopia is a **2D, turn-based, grid-tactics** game: units occupy
cells and act in speed order. That rules out most real-time locomotion
concerns and makes the logical grid the single source of truth.

## Audit

| Need (from the task checklist) | Required by the blueprint? | Approach |
|---|---|---|
| Character/creature locomotion | Yes: units step cell-to-cell on the map | Logical step in `BattleGrid`/`PlayerUnitController`; visual tween in `GridMover` |
| Directional movement / turns | Yes: facing left/right | `UnitVisual.facing` set by `CharacterAnimator` from step direction. 4/8-direction sprite sets later → AnimationTree `BlendSpace2D` |
| Starts and stops | Minor: per-step ease-out | `Tween` TRANS_SINE/EASE_OUT per step; no acceleration model |
| Jumps and landings | No | — |
| Traversal (climb, swim, ledges) | No (terrain costs only) | Terrain affects legality/cost in grid data, not animation |
| Combat actions (attack, defend, cast) | Yes, card-driven | Per-card animation id on the unit's library, played after the rules resolve |
| Hit reactions | Yes | `unit/hit` animation (flash) on `HealthComponent.damaged` |
| Death/defeat | Yes | `unit/defeat` (fade), terminal |
| Interaction actions | Later (doors/chests) | Same pattern: rule first, animation second |
| Facial animation / lip sync | No (2D portraits in dialogue) | Portrait swap per `DialogueLine`; no lip sync planned |
| Mechanical/vehicle motion | No | — |
| VFX movement (projectiles, card effects, soul energy) | Yes (feedback) | `GPUParticles2D`/`CPUParticles2D` + Tweens, spawned by listeners of gameplay/animation events. Compatibility renderer → prefer CPUParticles2D for consistency |
| Secondary motion (banners, idle bob) | Yes, cosmetic | `unit/idle` bob on the `Visual` node; shaders for cloth/banner sway later |
| Procedural IK (Skeleton2D) | No for now | Only if cut-out (bone) characters replace sprites; would live inside `Visual` |
| Physics-driven reactions/ragdolls | No | — |
| Camera feedback | Yes (shake on hits, focus on active unit) | Camera listens to `animation_event(&"hit_impact")`; disabled by reduced motion |
| Multiplayer authority | Not applicable (single-player) | — |

## Responsibility split

| System | Owns | Must not |
|---|---|---|
| Grid rules (`BattleGrid`, `PlayerUnitController`, later card resolution) | Cell occupancy, movement legality, damage, outcomes | Wait for animations |
| `Character` | Logical `cell`, component wiring | Read input, decide rules |
| `GridMover` (Tween) | Moving the unit's `Node2D` between cell centers | Change `cell` |
| `AnimationPlayer` + library `unit` | Sprite/visual tracks on `Visual`; method tracks for events | Move the unit root, apply damage |
| `CharacterAnimator` | Gameplay signal → animation; exposes `animation_event` | Contain rules |
| Tweens elsewhere | UI transitions (0.15–0.3 s), press feedback | Gameplay timing |
| Particles / shaders | Cosmetic VFX | Collision or rules |
| CharacterBody2D / physics | **Not used** for units (see collision doc) | — |

`AnimationPlayer` animates the child `Visual` node while `GridMover` tweens
the `Character` root, so the two never fight over the same property.

**AnimationTree**: not used yet. With 4 simple states an animator script is
clearer. Introduce `AnimationTree` with a state machine (idle, move, act,
hit, defeat) and a `BlendSpace2D` for facing when directional sprite sets
arrive; `CharacterAnimator` then sets tree parameters instead of calling
`play()`, and nothing else changes.

## Gameplay state drives animation

```
PlayerUnitController.try_step()      → grid.relocate() (rule)  → Character.step_to()
Character.step_to()                  → cell updated immediately → GridMover.move_to()
GridMover.move_started(direction)    → CharacterAnimator: facing + play("unit/move")
GridMover.move_finished              → CharacterAnimator: play("unit/idle")
HealthComponent.damaged              → CharacterAnimator: play("unit/hit") → idle
HealthComponent.died                 → CharacterAnimator: play("unit/defeat") (terminal)
```

## Animation events

Method tracks in `features/characters/animations/unit_animations.tres` call
`CharacterAnimator.emit_animation_event(name)`:

| Event | Animation | Consumers (planned) |
|---|---|---|
| `footstep` | `move` (t = 0.0, 0.18) | footstep audio by terrain |
| `hit_impact` | `hit` (t = 0) | hit SFX, camera shake, hit VFX |

Events are feedback only. Damage is applied by rules before the hit
animation starts; skipping or speeding up animations (reduced motion,
fast-forward) never changes outcomes.

## Reusable resources and ownership

- `unit_animations.tres` (AnimationLibrary "unit"): `idle`, `move`, `hit`,
  `defeat`. Tracks target `Visual:*` and `CharacterAnimator` by relative path,
  so any unit scene with that structure can use it. Unit-specific libraries
  (e.g. per-hero attack animations) are added as extra libraries.
- Owned by `features/characters/`.

## Import settings (for when art arrives)

- Pixel art: Texture filter `Nearest` on the textures (or project default),
  mipmaps off, `Lossless` compression.
- Painted 2D: filter `Linear`, mipmaps off for UI/sprites shown at 1:1.
- Sprite sheets: `SpriteFrames` resources per unit under
  `features/characters/content/<unit>/`; one atlas per unit.
- Audio: SFX `.wav` (short), music/VO `.ogg`.

## Performance budgets (targets, not yet measured)

| Item | Budget |
|---|---|
| Units on a 16×16 map | ≤ 40 animated `Node2D` units |
| Concurrent tweens | ≤ 50 |
| Particles on screen | ≤ 2,000 (CPUParticles2D) |
| Frame time | 16.6 ms at 1080p on integrated GPUs (GL Compatibility) |

No profiling has been done yet; record measurements here when available.

## Tests (implemented)

`tests/integration/test_character_scene.gd`:
logical cell updates before the visual (frame-independent outcome); the
visual ends exactly on the cell center; large frame deltas (`Engine.time_scale = 4`)
don't change the result; reduced motion snaps; state → animation transitions
(idle → move → idle → hit → idle → defeat, defeat terminal); `footstep` and
`hit_impact` events fire from tracks; facing follows horizontal steps;
`defeated` fires. Grid legality: `tests/unit/combat/test_battle_grid.gd`.

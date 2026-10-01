# Movement and animation audit

Audit of the approved blueprint for motion needs, and how responsibilities
are divided. Utopia is a **2D, turn-based, grid-tactics** game: units occupy
cells and act in speed order. That rules out most real-time locomotion
concerns and makes the logical grid the single source of truth.

## Audit (isometric pixel art — ADR 0010)

Inventory of the motion the finished game must communicate, derived from the
approved mechanics (cards, AP, speed order, morale, hero + battalion units,
base, world map, soul energy). Art scale: 640×360 frame, 32×48 character
canvas, 4 facings (SE/NE drawn, SW/NW mirrored). Target platform for every
row: Windows/Linux desktop, 60 fps (budgets in ART_IMPLEMENTATION_GUIDE.md §7).
"Review" always means in-engine at gameplay distance (1280×720 and
1920×1080 captures + live motion), scored with VISUAL_QUALITY_RUBRIC.md C8/C9.

**Clips vs. runtime system.** Clips are SpriteFrames animations
(`<clip>_<dir>`). The runtime system is: rules resolve → `GridMover`
(position tween) + `CharacterAnimator` (state → clip, facing, event relay;
becomes an AnimationTree state machine when frame sets arrive) + a planned
`ActionSequencer` that orders anticipation → travel → impact → reaction →
return for card actions and drives hit-stop, camera, and VFX from animation
events. Shipping clips without that sequencing is not "animation done".

### M1. Unit locomotion on the iso grid — required
- Purpose: show path and destination of moves; make speed/initiative feel physical.
- Assets: `walk_se`, `walk_ne` (6 frames @12 fps), mirrored for SW/NW; battalion minis share the cycle.
- Authored / procedural: authored cycle; procedural position tween per tile (0.18 s), integer-snapped.
- States & transitions: idle → walk (start: first contact frame), walk → idle (stop: settle frame), turn = instant facing swap at tile centre (no turn clip at this scale).
- Contact points: feet on the tile centre on frames 1 and 4; contact shadow stays on the ground.
- Sync: `footstep` events → footstep audio per terrain, dust puff VFX; camera follows the active unit; no controller rumble.
- Target quality: rubric C8 ≥ 8 (no sliding: one cycle per tile).
- Status: placeholder bob + tween + mirrored banner (implemented); frame clips missing.

### M2. Elevation step — required once maps have height
- Purpose: read cover and high ground.
- Assets: `hop_up`, `hop_down` (4 frames), shared by all humanoids.
- Procedural: vertical arc of 8 px per level on the tween. Contact: landing frame on tile. Sync: landing thud, dust.
- Status: not built (maps are flat; blocks are walls).

### M3. Idle and readiness — required
- Purpose: show which unit is active, selected, low morale, or defeated.
- Assets: `idle` (4 frames @8 fps), `idle_ready` (active turn, weapon raised), `idle_low_morale` (slumped).
- Procedural: active-unit ring pulse (UI overlay, not sprite).
- Transitions: idle ↔ ready on turn start/end; morale threshold swaps idle variant.
- Status: 1-px bob placeholder only.

### M4. Card actions — required (core loop)
- Purpose: make each card kind readable: move, attack, defend, buff, debuff, special.
- Assets per unit: `attack_melee` (8 frames: 3 anticipation, 1 contact, 4 recovery), `attack_ranged` (release frame + projectile VFX), `cast` (for magical damage), `guard`, `rally` (buff), hero signature `special`.
- Authored / procedural: authored body clips; procedural lunge offset (4 px toward target) and 2–3 frame hit-stop.
- Contact points: weapon contact / projectile release frame tagged `impact` / `release`.
- Sync: `impact` → damage number, hit flash on target, impact SFX, small camera shake (off with reduced motion), controller rumble 80 ms (optional setting); card UI plays before the body clip starts.
- Target quality: C8 ≥ 8, C9 ≥ 8; telegraph readable before damage numbers appear.
- Status: not built (no card system yet).

### M5. Reactions: hit, block, heal, status, defeat — required
- Assets: `hit` (3 frames), `block` (3), `heal` (VFX on sprite), status loops (small overhead icons), `defeat` (6 frames, ends on a held corpse or fades to banner).
- Procedural: palette flash on hit (implemented as modulate), knockback 2 px.
- Sync: `hit_impact` event (implemented) → SFX/VFX; morale change number.
- Status: modulate flash + fade defeat implemented and tested; frames missing.

### M6. Battalion composite motion — required (hero + battalion pillar)
- Purpose: show a unit is a hero leading a battalion; show battalion strength.
- Assets: 3 troop minis (16×24) per battalion type with idle/walk/attack/hit.
- Procedural: formation offsets around the hero on the same tile; staggered timing (50–80 ms) for walk and attack; minis drop out as battalion strength falls.
- Contact: minis share the hero's tile; never occupy neighbour tiles.
- Status: not built. Highest-value identity motion after M4.

### M7. Turn order and speed changes — required (UI motion)
- Assets: portrait chips in the turn bar.
- Procedural: chips slide to new order when speed buffs change initiative (200 ms), active chip enlarges.
- Status: not built.

### M8. Card hand motion — required (UI motion)
- Draw (fly from deck 150 ms), focus lift (120 ms), drag/aim with target line, play (fly to target), discard, reshuffle. Details in UI_STYLE_GUIDE.md "Cards".
- Status: not built.

### M9. VFX motion — required
- Projectiles (arc for thrown, straight for bolts), impact sparks, ember hazard loop, soul-energy wisps (teal, slow, upward), morale up/down particles, reward sparkle.
- Authored sprite-sheet effects + CPUParticles2D; additive light sprites. Never cover numbers or tiles for > 150 ms.
- Status: not built.

### M10. Camera — required
- Focus pan to the active unit (300 ms ease), shake on heavy hits (≤ 4 screen px, off with reduced motion). Integer zoom only (×2 base); no free zoom that breaks the pixel grid.
- Status: static camera centred on the board.

### M11. Hub, world map, and ambient — required
- Tavern NPC idle loops (drink, talk), recruit "step forward" when selected; world-map party token travel along paths; ambient (smoke, water shimmer).
- Status: not built (scenes do not exist).

### M12. Portrait expressions — required for dialogue
- 3–5 expressions per hero, 2-frame blink; no lip sync (not applicable for pixel portraits; text-paced talk bob instead).
- Status: not built.

**Not applicable:** jumps beyond elevation hops, free traversal/climbing,
vehicles/mechanical motion, thrusters, procedural IK, physics ragdolls and
cloth simulation (cloth and hair secondary motion are hand-drawn in frames),
facial capture and lip sync.

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

## Import settings and performance budgets

Defined once in [ART_IMPLEMENTATION_GUIDE.md](ART_IMPLEMENTATION_GUIDE.md)
(§1 grid and filtering, §5 naming/import, §7 budgets). Animation-specific
budget: ≤ 40 animated units, ≤ 4 sprites each (hero + 3 minis), clips at
8–15 fps; animation updates skip off-screen units.

## Tests (implemented)

`tests/integration/test_character_scene.gd`:
logical cell updates before the visual (frame-independent outcome); the
visual ends exactly on the cell center; large frame deltas (`Engine.time_scale = 4`)
don't change the result; reduced motion snaps; state → animation transitions
(idle → move → idle → hit → idle → defeat, defeat terminal); `footstep` and
`hit_impact` events fire from tracks; facing follows horizontal steps;
`defeated` fires. Grid legality: `tests/unit/combat/test_battle_grid.gd`.

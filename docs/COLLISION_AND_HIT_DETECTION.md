# Collision and hit-detection audit

## Decision

Gameplay collision in Utopia is **grid-logical, not physics-based**.
Units occupy cells; movement legality, line of attack, area effects, and
hits are computed from `BattleMapData` + `BattleGrid` occupancy with integer
cell math. Physics bodies would add nondeterminism and frame-rate
dependence to a turn-based game for no benefit. (ADR 0004.)

Collision stays separate from visuals: sprites, animations, and tweens never
decide what was hit. The grid decides; visuals show the result.

## Inventory

| Item from the checklist | Needed? | Approach |
|---|---|---|
| CharacterBody movement shapes | No | Units are `Node2D` on cells |
| StaticBody / AnimatableBody world collision | No | Blocked cells in `BattleMapData.blocked_cells` |
| Area interaction & hurtbox triggers | No (gameplay) | Cell queries: `BattleGrid.occupant_at`, hazards via `is_hazard` |
| Temporary attack hitboxes | No | Card `range_tiles` + target cell validation (planned in card resolution) |
| RayCast/ShapeCast aim and projectiles | No | Line-of-sight on the grid (Bresenham over cells) when ranged cards need it |
| Vehicles, moving platforms, doors, dynamic props | Doors/props later | Cell flags/occupants changed by interaction rules |
| Ragdolls, cloth | No | Cosmetic shaders only |
| Static-world surfaces (terrain) | Yes | Per-cell terrain data (planned: movement cost, cover) |
| Pointer picking (mouse selects a cell/unit) | Yes, soon | `BattleGrid.local_to_cell(get_local_mouse_position())` — no physics query needed. Layer `pointer_pick` (4) is reserved if a physics pick is ever required |

Collision layer names are reserved in `project.godot`
(`world`, `units`, `interactables`, `pointer_pick`) for future cosmetic or
editor use; no bodies use them today. If a convex/concave shape is ever
proposed, it needs an ADR explaining why grid math is insufficient.

## Isometric projection

Battles are drawn isometrically (ADR 0010) but all rules stay in integer
cell space. `BattleGrid.cell_to_local` (cell → 2:1 diamond centre, 32×16 art
px) and `BattleGrid.local_to_cell` (inverse, used for pointer picking) are
the only projection code; tests check the round trip for every cell and
that points inside a diamond pick that cell. Units are y-sorted by their
cell centre for correct overlap.

## Rules

- **Source of truth**: `BattleGrid` occupancy + `BattleMapData`. Visual
  position is derived from cell (`cell_to_local`).
- **Swept checks / tunneling**: a move is a sequence of single orthogonal
  steps, each validated (`PlayerUnitController.try_step` rejects multi-cell
  and diagonal deltas). Multi-tile card moves must call it per step, so
  walls can never be skipped regardless of frame rate.
- **One hit per window**: an effect resolves once per application. Hazards
  trigger once per entry into the cell (not per frame). Card effects resolve
  once per play; animation events cannot re-apply damage.
- **Overlap recovery**: occupancy is exclusive (`place`/`relocate` refuse
  occupied cells), so overlaps cannot occur; loading a save never restores
  positions mid-mission.
- **Animation timing**: hit animations play *after* the rule resolves; their
  `hit_impact` event drives feedback only.
- **Process ownership**: rules run in input/turn handlers, not
  `_physics_process`; no physics ticks are involved.
- **Authority / multiplayer**: single-player; the local rules are
  authoritative. If netplay is ever added, the same deterministic grid rules
  run on the authority and clients send intents (card id + target cell).
- **Activation/sleeping**: not applicable (no bodies).

## Tests

| Concern | Grid equivalent | Test |
|---|---|---|
| Tunneling | Wall cannot be skipped; multi-cell jump rejected | `test_battle_grid::test_no_tunneling_through_walls` |
| Corners | Diagonal corner cutting rejected; out-of-bounds corner cells refused | same + `test_blocked_and_outside_cells_cannot_be_entered` |
| Overlap | Two occupants cannot share a cell | `test_occupied_cells_cannot_overlap` |
| Repeated hits | Hazard hits once per entry | `test_hazard_hits_once_per_entry` |
| Frame-rate variation | Outcome identical at 4× time scale | `test_character_scene::test_frame_rate_does_not_change_outcome` |
| Visible contact alignment | Visual ends exactly on the cell center; cell↔world round trip exact | `test_visual_ends_exactly_on_cell_center`, `test_cell_world_round_trip_is_exact_at_cell_centers` |
| Slopes, stairs, moving platforms, doors | Not applicable to the current design; add terrain/door tests with those features | — |

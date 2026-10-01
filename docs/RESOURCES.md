# Resources

Reusable game data lives in `Resource` scripts (`data/` folders) and authored
`.tres` instances (`content/` folders). Scripts never hard-code tuning values
that design will change.

## When to create a Resource vs. a scene vs. a plain class

| Need | Use |
|---|---|
| Data edited by designers, shared by many instances (stats, cards, maps, missions) | `Resource` (`extends Resource`, `@export` fields) |
| Something in the world or on screen, with children/processing | Scene (`.tscn`) + script |
| Pure logic/state with no editor data (deck piles, wallet, turn order) | `RefCounted` class |
| Global lifetime service | Autoload (rare; see AI_INSTRUCTIONS.md) |

## Resource scripts

| Class | File | Fields / role |
|---|---|---|
| `UnitStats` | `features/characters/data/unit_stats.gd` | `max_health, physical_damage, magical_damage, movement, card_draw, max_hand_size, max_ap, morale, speed`; `combine(a, b)` adds |
| `UnitPartData` | `…/unit_part_data.gd` | base: `id, name_key, stats, deck`, `validate()` |
| `HeroData` | `…/hero_data.gd` | deck size 5 |
| `BattalionData` | `…/battalion_data.gd` | deck size 15 |
| `RosterCatalog` | `…/roster_catalog.gd` | all heroes/battalions by id + defaults; turns save ids back into Resources |
| `CardData` | `features/combat/data/card_data.gd` | `id, name_key, description_key, kind, ap_cost, power, range_tiles` |
| `BattleMapData` | `features/combat/data/battle_map_data.gd` | `size` (12–16), `blocked_cells, hazard_cells, hazard_damage, player_spawn, objective_cell`, `validate()` |
| `EnemyConfig` | `features/combat/data/enemy_config.gd` | `id, name_key, stats, deck, behavior_id` (no instances yet) |
| `MissionData` | `features/quests/data/mission_data.gd` | `id, title_key, objective_key, objective, map, rewards` |
| `DialogueLine` / `DialogueSequence` | `features/dialogue/data/` | keys, portrait, voice id (no instances yet) |
| `RouteTable` | `core/routing/route_table.gd` | route id → scene path |
| `UiTokens` | `core/ui/theme/ui_tokens.gd` | colors, type sizes, spacing, motion |

## Authored content

```
features/characters/content/   hero_deserter, battalion_militia, their stats, roster_catalog
features/combat/content/cards/ card_march, card_strike, card_guard, card_rally, card_deserters_resolve
features/combat/content/maps/  map_training_ground (12×12)
features/quests/content/       mission_first_spark (REACH, rewards 50 gold + 10 wood)
core/routing/route_table.tres
core/ui/theme/ui_tokens.tres
```

## Rules

- Every authored Resource has a stable `id` (`snake_case`, prefixed by kind:
  `hero_`, `battalion_`, `card_`, `map_`, `mission_`). Ids go into saves and
  analytics; **never rename an id after release** — add a save migration.
- Player-facing text is a translation key field (`name_key`, `title_key`…),
  never a display string.
- Shared Resources are read-only at runtime. Runtime state (current health,
  hand) lives in nodes or `RefCounted` objects, not in the `.tres`.
  If you must mutate, `duplicate()` first.
- Typed arrays of custom Resources: when iterating a `preload`ed catalog in
  GDScript, type the loop variable as the base class and the array `as Array`
  (a GDScript limitation with script-typed arrays from constants).
- Content is validated by tests (`validate()` methods, unique ids, map size,
  deck sizes, translation keys present).
- `id` fields are `StringName`; compare with `==`.

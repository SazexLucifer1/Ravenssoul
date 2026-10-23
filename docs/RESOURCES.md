# Resources

Reusable game data lives in `Resource` scripts (`data/` folders) and authored
`.tres` instances (`content/` folders). Scripts never hard-code tuning values
that design will change.

## When to create a Resource vs. a scene vs. a plain class

| Need | Use |
|---|---|
| Data edited by designers, shared by many instances (stats, cards, maps, missions) | `Resource` (`[GlobalClass] partial class X : Resource`, `[Export]` properties) |
| Something in the world or on screen, with children/processing | Scene (`.tscn`) + script |
| Pure logic/state with no editor data (deck piles, loadout, turn order) | Plain C# class (`RefCounted` only when it must carry Godot signals, like `ResourceWallet`) |
| Global lifetime service | Autoload (rare; see AI_INSTRUCTIONS.md) |

## Resource scripts

| Class | File | Fields / role |
|---|---|---|
| `UnitStats` | `features/characters/data/UnitStats.cs` | `MaxHealth, PhysicalDamage, MagicalDamage, Movement, CardDraw, MaxHandSize, MaxAp, Morale, Speed`; `Combine(a, b)` adds |
| `UnitPartData` | `…/UnitPartData.cs` | base: `Id, NameKey, Stats, Deck`, `Validate()` |
| `HeroData` | `…/HeroData.cs` | deck size 5 |
| `BattalionData` | `…/BattalionData.cs` | deck size 15 |
| `RosterCatalog` | `…/RosterCatalog.cs` | all heroes/battalions by id + defaults; turns save ids back into Resources |
| `CardData` | `features/combat/data/CardData.cs` | `Id, NameKey, DescriptionKey, Kind, ApCost, Power, RangeTiles` |
| `BattleMapData` | `features/combat/data/BattleMapData.cs` | `Size` (12–16), `BlockedCells, HazardCells, HazardDamage, PlayerSpawn, ObjectiveCell`, `Validate()` |
| `EnemyConfig` | `features/combat/data/EnemyConfig.cs` | `Id, NameKey, Stats, Deck, BehaviorId` (no instances yet) |
| `MissionData` | `features/quests/data/MissionData.cs` | `Id, TitleKey, ObjectiveKey, Objective, Map, Rewards` |
| `DialogueLine` / `DialogueSequence` | `features/dialogue/data/` | keys, portrait, voice id (no instances yet) |
| `RouteTable` | `core/routing/RouteTable.cs` | route id → scene path |
| `UiTokens` | `core/ui/theme/UiTokens.cs` | colors, type sizes, spacing, motion |

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

- Resource classes are C# `[GlobalClass] public partial class X : Resource`
  in `X.cs` with `[Export]` properties; the `.tres` files store those
  properties by their C# names (`NameKey = "..."`). Renaming a property means
  updating every `.tres` that sets it (tests load all authored content).
- Every authored Resource has a stable `Id` value (`snake_case`, prefixed by kind:
  `hero_`, `battalion_`, `card_`, `map_`, `mission_`). Ids go into saves and
  analytics; **never rename an id after release** — add a save migration.
- Player-facing text is a translation key field (`NameKey`, `TitleKey`…),
  never a display string.
- Shared Resources are read-only at runtime. Runtime state (current health,
  hand) lives in nodes or plain C# objects, not in the `.tres`.
  If you must mutate, `Duplicate()` first.
- Lists of Resources are `[Export] Godot.Collections.Array<CardData>`;
  dictionaries are `Godot.Collections.Dictionary<StringName, int>` (only
  exported fields use Godot collections; runtime code uses `List`/`Dictionary`).
- Content is validated by tests (`Validate()` methods, unique ids, map size,
  deck sizes, translation keys present).
- `Id` properties are `StringName`; compare with `==`.

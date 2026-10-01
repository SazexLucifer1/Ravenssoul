# features/

One folder per gameplay feature. A feature owns its scenes, scripts, Resource
definitions (`data/`), authored content (`content/`), and feature UI (`ui/`).
Features talk to each other through signals and Resources, never by reaching
into each other's nodes. See `docs/ADDING_A_FEATURE.md`.

| Feature | Status | Owns |
|---|---|---|
| `characters/` | Implemented (bootstrap) | `Character` scene, `UnitStats`, `HeroData`, `BattalionData`, `UnitLoadout`, `RosterCatalog`, unit animation library, `HealthBar` |
| `combat/` | Implemented (bootstrap) | `BattleGrid`, `BattleMapData`, `CardData`, `Deck`, `TurnOrder`, `PlayerUnitController`, `EnemyConfig` |
| `quests/` | Data only | `MissionData` + first mission |
| `inventory/` | Data only | `ResourceWallet` (wood, stone, food, gold, soul energy) |
| `dialogue/` | Data only | `DialogueLine`, `DialogueSequence` |
| `scouting/` | Planned | Intel tiers per enemy type |
| `base_building/` | Planned | Buildings, card unlocks/upgrades |
| `soul_energy/` | Planned | Global soul-energy tally and story threshold |
| `interactions/` | Planned | Map objects (doors, levers, chests) |

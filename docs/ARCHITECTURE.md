# Architecture

Initial architecture for **Utopia**, a 2D tactical, card-driven RPG
(design: [`game-design/mechanics-and-core-loop.md`](game-design/mechanics-and-core-loop.md)).
This document describes what exists today. Decisions with trade-offs are
recorded in [`decisions/`](decisions/).

## Technology

| Item | Choice | Why |
|---|---|---|
| Engine | Godot **4.6** (`config/features` = `4.6`) | Current stable. Needs ≥ 4.5 for `Logger`, `SceneTree.scene_changed`, `Control.focus_behavior_recursive` (all used). |
| Renderer | **GL Compatibility** | 2D only; widest Windows/Linux GPU support (OpenGL 3.3). No Forward+ features needed. See ADR 0002. |
| Language | **C#** (.NET 8, Godot .NET build) — ADR 0011 | One statically typed language for game, automation, and tests; warnings are errors (`Utopia.csproj`). Requires the Godot **.NET** editor/runtime and the .NET 8 SDK. |
| Platforms | Windows, Linux (x86_64) | `export_presets.cfg`: Windows and Linux release presets (exclude `automation/`; `Utopia.csproj` also drops `automation/**` and `tests/**` from `ExportRelease` builds), plus "Linux QA (automation)" with the `automation_qa` feature (build with `UtopiaAutomation=true`). |
| Multiplayer | None | `networking/` is reserved (see its README). |
| Base resolution | 1280×720 UI canvas, `canvas_items` stretch, `expand` aspect; world art in a 640×360 frame via gameplay camera zoom 2 | Scales to 1080p/1440p (integer for pixel art), ultrawide adds width. Nearest filtering + pixel snapping (ADR 0010). |

## Folder layout

```
addons/      third-party plugins (none yet)
assets/      localization catalogs, generated UI theme, (future) fonts/sprites/audio
autoload/    the five global services — and nothing else
core/        game-agnostic building blocks: components, routing, save, localization,
             input, diagnostics, UI kit (theme, buttons, screens)
features/    gameplay features: characters, combat, quests, inventory, dialogue,
             (planned) scouting, base_building, soul_energy, interactions
scenes/      top-level routed scenes (bootstrap, main_menu, gameplay) and game menus
networking/  reserved for gameplay networking (none)
shaders/     shared shaders (none yet)
automation/  remote automation server, protocol, Godot adapter (dev/QA builds only; excluded from release exports)
tools/       external tooling outside the Godot project (`.gdignore`): Python automation client/CLI/MCP,
             guard scripts (`tools/guards/`), toolchain setup (`tools/setup/`)
tests/       framework, unit tests, integration tests, tools (screenshots, key extraction)
docs/        this documentation, ADRs, game design
```

## Layers and dependency rules

```
scenes/   ──► features/ ──► core/
   │             │            ▲
   └─────────────┴──► autoload/ (services) ──┘
```

- **core/** depends on nothing game-specific. It may read the `Settings`
  autoload only through `UiMotion` (reduced motion). No `features/` imports.
- **features/** depend on `core/` and on Resources. A feature never reaches
  into another feature's nodes; it uses signals, Resources, or a scene-level
  coordinator.
- **scenes/** are composition roots: they instantiate feature scenes, wire
  signals, and call autoload services. Rules stay in features.
- **autoload/** services hold global-lifetime state and know nothing about
  specific scenes beyond route ids.

## Autoloads (and why each exists)

| Name | Script | Why it must be global | Owns |
|---|---|---|---|
| `Settings` | `autoload/SettingsService.cs` | Language, reduced motion, text size must apply before the first screen and survive every scene change. | `user://settings.cfg` |
| `InputMethod` | `autoload/InputMethodService.cs` (`InputMethodService`) | Must observe raw input before any screen consumes it; every prompt needs the active device. | current input method |
| `SaveService` | `autoload/SaveService.cs` | Saving is triggered from many scenes; one serialization/versioning path. | `user://saves/*.json` via `SaveBackend` |
| `GameSession` | `autoload/GameSession.cs` | Campaign state (roster, resources, soul-energy tally) must outlive routed scenes. | in-memory campaign |
| `SceneRouter` | `autoload/SceneRouter.cs` | A scene cannot replace itself safely; the fade overlay must outlive both scenes. Only caller of `ChangeSceneTo*` (guard G005). | current route, fade layer |

There is deliberately **no UI autoload** and no "GameManager". The remote
automation server is **not** an autoload either: `AutomationGate`
(`core/diagnostics/`) starts it from bootstrap only when `--automation` is
passed in a debug or QA build, and adds it under the root as `Automation`
(see [AUTOMATION.md](AUTOMATION.md), ADRs 0008/0009). Menus are
owned by the scene that shows them via a scene-local `ScreenStack`.
Adding an autoload requires an ADR (see `AI_INSTRUCTIONS.md`).

The autoload node names (`Settings`, `InputMethod`, …) stay as listed; C# code
reaches each service through its static `Instance` property
(`SettingsService.Instance`, `SceneRouter.Instance`, …), set while the autoload
is in the tree. Resources the services load once (`GameSession.Roster`,
`GameSession.FirstMission`, `UiTokens.Default`) are static properties.

## Runtime flow

1. `scenes/bootstrap/bootstrap.tscn` (main scene) waits one frame, asks
   `AutomationGate` whether to start the automation server, verifies
   routes, translations, and roster content, then calls
   `SceneRouter.Instance.Goto(Routes.MainMenu)`. On failure it shows a player-readable
   error with a Close button. Debug builds accept `-- --route=<id>`.
2. `scenes/main_menu/` — Continue (if a save exists, primary) / New Campaign /
   Settings / Quit. Settings and confirmations are pushed on its `ScreenStack`.
3. `scenes/gameplay/` — builds the mission from `GameSession.Instance.ActiveMission`:
   `BattleGrid` + one `Character` + `PlayerUnitController` + `Hud` +
   `ScreenStack` (pause, settings, confirm, result). Victory grants rewards and
   autosaves; defeat offers retry.

Routes are ids in `core/routing/route_table.tres`; code uses `Routes.*`
constants, never scene paths (guard G007).

## Blueprint coverage (what is real vs. planned)

| Blueprint element | State |
|---|---|
| Hero + battalion combination (stats add, 5+15 deck) | **Implemented** as data + tests: `UnitStats.Combine`, `UnitLoadout`, `HeroData`, `BattalionData` |
| Cards, AP, hand, draw | `CardData` + `Deck` (seeded draw/discard/reshuffle). AP spending and card effects: **not yet** |
| Speed turn order | `TurnOrder.SortBySpeed` only; no round loop yet |
| Tactical grid (12–16 tiles) | `BattleMapData` (validated size), `BattleGrid` occupancy + legality + isometric projection (ADR 0010) and move-range highlights, one 12×12 map |
| Morale | Stat exists; no rules yet |
| Missions/objectives | `MissionData`; REACH objective playable |
| Rewards/resources | `ResourceWallet`; mission rewards applied and saved |
| Enemies | `EnemyConfig` Resource only; no enemies on the map yet |
| Experience/levelling, base buildings, scouting, soul-energy spending | Planned (feature READMEs); `SoulEnergySpent` already tracked and saved (`soul_energy_spent` in saves) |

The first playable movement is "one step per key press". When card-driven
movement arrives it calls the same `PlayerUnitController.TryStep` /
`BattleGrid` rules.

## Related documents

- [SCENE_TREE_RULES.md](SCENE_TREE_RULES.md) — scene ownership, node paths
- [SIGNALS_AND_EVENTS.md](SIGNALS_AND_EVENTS.md) — every signal and its contract
- [RESOURCES.md](RESOURCES.md) — data Resources and content files
- [SAVE_SYSTEM.md](SAVE_SYSTEM.md) — save format, backends, migrations
- [UI_ARCHITECTURE.md](UI_ARCHITECTURE.md) — screens, buttons, theme, accessibility
- [LOCALIZATION.md](LOCALIZATION.md) — keys, catalogs, formatting, pseudo/RTL
- [ANIMATION_AND_MOVEMENT.md](ANIMATION_AND_MOVEMENT.md) — movement & animation audit
- [COLLISION_AND_HIT_DETECTION.md](COLLISION_AND_HIT_DETECTION.md) — collision audit
- [TESTING.md](TESTING.md) — test runner, commands, coverage
- [ADDING_A_FEATURE.md](ADDING_A_FEATURE.md) — step-by-step recipe
- [AUTOMATION.md](AUTOMATION.md) — remote automation protocol, security, SDK, CI
- [AI_INSTRUCTIONS.md](AI_INSTRUCTIONS.md) — rules for AI and human contributors (the rulebook)
- [AI_WORKFLOW.md](AI_WORKFLOW.md) — who plans, builds, reviews; briefs; nightly tests; asset pipeline
- [MISTAKES.md](MISTAKES.md) — repeated mistakes and the rule or guard each one produced

## Known limitations

- No enemies, AP economy, or card play yet; the battle is a movement slice.
- Placeholder art drawn in code (`UnitVisual`, `BattleGrid._Draw`); art direction, quality bar, and production rules are in [ART_DIRECTION.md](ART_DIRECTION.md), [VISUAL_QUALITY_RUBRIC.md](VISUAL_QUALITY_RUBRIC.md) (current AAA visual-readiness 32 %), [ART_IMPLEMENTATION_GUIDE.md](ART_IMPLEMENTATION_GUIDE.md), [UI_STYLE_GUIDE.md](UI_STYLE_GUIDE.md).
- Text glyphs for input prompts (no icon set yet).
- Engine default font only: covers Latin incl. German; **not** CJK/Arabic.
- Export presets exist but exports were not built (no export templates in CI).
- Save writes are synchronous (small JSON); fine for now, revisit for large saves.

# Scene tree rules

## Ownership

| Scene | Owner of | Must not |
|---|---|---|
| `scenes/bootstrap/bootstrap.tscn` | startup checks, first route | hold game state |
| `scenes/main_menu/main_menu.tscn` | title screen + its `ScreenStack` | load/save directly except through `SaveService` |
| `scenes/gameplay/gameplay.tscn` | one mission: grid, units, controller, HUD, menus | contain rules (they live in `features/`) |
| `features/characters/character.tscn` | one unit's components | read input or touch UI |
| `scenes/gameplay/hud/hud.tscn` | display only | change game state |
| `scenes/menus/*`, `core/ui/screens/*` | one screen's layout | change game state; they emit intent signals |

Each top-level routed scene is a **composition root**: it instantiates
children, assigns exported references, and connects signals. Logic belongs
to components and features.

### Reference scene trees

```
Gameplay (Node, gameplay.gd)
├─ Background (CanvasLayer -1)
├─ World (Node2D)
│  ├─ BattleGrid (Node2D, battle_grid.gd)
│  │  └─ Units (Node2D)        ← Character instances, positions in grid space
│  └─ Camera2D
├─ PlayerUnitController (Node)
├─ Hud (CanvasLayer, hud.tscn)
└─ UiLayer (CanvasLayer 10, process_mode ALWAYS)
   └─ ScreenStack              ← pause, settings, confirm, result screens

Character (Node2D, character.gd)
├─ Visual (UnitVisual)          ← art only; swap for sprites freely
├─ HealthComponent
├─ GridMover                    ← visual tween between cells
├─ AnimationPlayer (library "unit")
└─ CharacterAnimator            ← gameplay signals → animations, animation events
```

## Avoiding hard-coded node paths

In order of preference:

1. **Exported references** assigned in the scene file
   (`@export var health: HealthComponent`, set via `node_paths=`). Used by
   every scene in this project. Renaming a node in the editor updates them.
2. **Signals** when the sender shouldn't know the receiver.
3. **Resources** for shared data (`MissionData`, `RosterCatalog`).
4. **Groups** for "all X in the scene" queries (none needed yet).
5. **Ids → paths via a table** for scenes: `Routes.*` + `route_table.tres`.

Not allowed in gameplay/UI code: `get_node("../../Something")`,
`$Long/Path/Chains` across scene boundaries, `/root/...` lookups (autoloads
are referenced by their global name; `UiMotion` is the single documented
exception, using `/root/Settings` so core code works without the autoload
in isolated tool scripts).

`$Child` for a node **inside the same scene file** is acceptable but
exported references are preferred because they survive renames.

## Instancing and lifetime

- Packed scenes that a script instantiates are exported `PackedScene`
  properties (`gameplay.gd: character_scene`, `pause_menu_scene`, …), not
  `preload("res://...")` in scene scripts.
- `preload` is fine for data Resources and in `core/` (e.g. `UiTokens`).
- Screens pushed on a `ScreenStack` are freed by the stack on `pop()`.
- Never `await SceneRouter.goto()` from the scene being replaced: on success
  it is freed mid-coroutine (leaks a function state). Listen to
  `SceneRouter.route_failed` for failures instead (see `main_menu.gd`).

## Process modes and pausing

- `SceneRouter`, `Settings`, `InputMethod`: `PROCESS_MODE_ALWAYS`.
- Gameplay's `UiLayer` is `ALWAYS` so menus work while `get_tree().paused`.
- Pausing is done only by the gameplay scene (pause menu); the router
  always unpauses on route change.

## Naming

- Nodes: `PascalCase` (`HealthComponent`, `ResumeButton`).
- Files: `snake_case.gd/.tscn/.tres`; a scene and its script share a name.
- Scripts used as types get `class_name` in `PascalCase`.

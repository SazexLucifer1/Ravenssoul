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
Gameplay (Node, Gameplay.cs)
├─ Background (CanvasLayer -1)
├─ World (Node2D)
│  ├─ BattleGrid (Node2D, BattleGrid.cs)
│  │  └─ Units (Node2D)        ← Character instances, positions in grid space
│  └─ Camera2D
├─ PlayerUnitController (Node)
├─ Hud (CanvasLayer, hud.tscn)
└─ UiLayer (CanvasLayer 10, process_mode ALWAYS)
   └─ ScreenStack              ← pause, settings, confirm, result screens

Character (Node2D, Character.cs)
├─ Visual (UnitVisual)          ← art only; swap for sprites freely
├─ HealthComponent
├─ GridMover                    ← visual tween between cells
├─ AnimationPlayer (library "unit")
└─ CharacterAnimator            ← gameplay signals → animations, animation events
```

## Avoiding hard-coded node paths

In order of preference:

1. **Exported references** assigned in the scene file
   (`[Export] public HealthComponent Health { get; set; }`, set via `node_paths=`). Used by
   every scene in this project. Renaming a node in the editor updates them.
2. **Signals** when the sender shouldn't know the receiver.
3. **Resources** for shared data (`MissionData`, `RosterCatalog`).
4. **Groups** for "all X in the scene" queries (none needed yet).
5. **Ids → paths via a table** for scenes: `Routes.*` + `route_table.tres`.

Not allowed in gameplay/UI code (guard G004): `GetNode("../../Something")`,
long path chains across scene boundaries, `/root/...` lookups (autoloads are
reached through their static `Instance` property, e.g.
`SettingsService.Instance`; `UiMotion` tolerates a missing instance so core
code also works in isolated tool scenes).

`GetNode<T>("Child")` for a node **inside the same scene file** is acceptable
but exported references are preferred because they survive renames.

## Instancing and lifetime

- Packed scenes that a script instantiates are exported `PackedScene`
  properties (`Gameplay.cs: CharacterScene`, `PauseMenuScene`, …), not
  `GD.Load("res://...tscn")` in scene scripts (guard G007).
- Loading data Resources by path is fine in services and `core/` (e.g.
  `UiTokens.Default`, `GameSession.Roster`).
- Screens pushed on a `ScreenStack` are freed by the stack on `pop()`.
- Never `await SceneRouter.Goto()` from the scene being replaced: on success
  it is freed mid-coroutine (leaks a function state). Listen to
  `SceneRouter.RouteFailed` for failures instead (see `MainMenu.cs`).

## Process modes and pausing

- `SceneRouter`, `Settings`, `InputMethod`: `PROCESS_MODE_ALWAYS`.
- Gameplay's `UiLayer` is `ALWAYS` so menus work while `GetTree().Paused`.
- Pausing is done only by the gameplay scene (pause menu); the router
  always unpauses on route change.

## Automation ids

Interactive controls and screen roots carry a stable id in node metadata:
`metadata/automation_id = "pause.resume"` (screens: `screen.pause`). Ids are
`<area>.<name>`, lowercase, unique within a scene, and never derived from
display text. They are inert data (no script), so they are safe in every
build. Game objects get ids from content ids in the automation provider. See
`AUTOMATION.md`.

## Naming

- Nodes: `PascalCase` (`HealthComponent`, `ResumeButton`).
- Scenes and resources: `snake_case.tscn/.tres` (`pause_menu.tscn`).
- C# scripts: `PascalCase.cs`, and the file name **must** equal the class name
  (`PauseMenu.cs` holds `partial class PauseMenu`) or Godot cannot attach it
  (guard G002). A scene's script is the PascalCase form of its name.
- Classes used in the editor (exported types, Resources) get `[GlobalClass]`.

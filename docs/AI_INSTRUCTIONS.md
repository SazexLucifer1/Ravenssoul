# Instructions for AI assistants (and humans)

This is the project's rulebook and the most important file in the repository.
Read it before changing the project. When a task changes any rule below,
update this file in the same change. When the same mistake happens twice, it
gets a rule here — and, if a script can detect it, a guard (see "Guarded
rules" and [MISTAKES.md](MISTAKES.md)).

Who does what (owner, director, workers, reviewers), how briefs are written,
and how work is reviewed and played: [AI_WORKFLOW.md](AI_WORKFLOW.md).

## Before you start

1. Read `README.md`, `docs/ARCHITECTURE.md`, the docs for the area you touch,
   the relevant ADRs in `docs/decisions/`, and
   `docs/game-design/mechanics-and-core-loop.md` (approved design; do not
   change design decisions there without being asked). The design document
   is the only rules source for game mechanics — rules come from it (or from
   the owner), never from memory of other games or rule systems.
2. Engine: Godot **4.6 .NET**, **C#** (.NET 8), 2D, GL Compatibility,
   Windows/Linux, single-player (ADR 0011). No GDScript.
3. Toolchain: `bash tools/setup/install_toolchain.sh` installs the Godot .NET
   build and the .NET 8 SDK on Linux (cloud sessions run it automatically).

## Folder and naming conventions

| Folder | Put here |
|---|---|
| `autoload/` | Only the five documented services |
| `core/` | Game-agnostic code: components, routing, save, serialization, localization, input, diagnostics, UI kit |
| `features/<name>/` | Gameplay features: `data/` (Resource classes), `content/` (.tres), `ui/`, logic, scenes, README |
| `scenes/` | Routed top-level scenes and game menus |
| `assets/` | Localization catalogs, generated theme, art/audio/fonts (+ `PROVENANCE.md`) |
| `automation/` | Remote automation server/protocol/adapter only (see `docs/AUTOMATION.md`) |
| `tools/automation/` | Python automation client, CLI, MCP adapter, scenario (stdlib only) |
| `tools/guards/` | Guard scripts that enforce rules in this file (stdlib only) |
| `tools/setup/` | Toolchain install script (Godot .NET + .NET SDK) |
| `tests/` | `unit/<area>/Test*.cs`, `integration/Test*.cs`, `framework/`, `tools/` |
| `docs/` | Documentation, `decisions/` ADRs, `game-design/` |

- C# files are `PascalCase.cs` and **the file name equals the class name**
  (`PauseMenu.cs` → `partial class PauseMenu`), or Godot cannot attach the
  script (guard G002). Scenes and resources stay `snake_case.tscn/.tres`; a
  scene's script is the PascalCase form of its name (`pause_menu.tscn` + `PauseMenu.cs`).
- Namespaces: `Utopia` (game), `Utopia.Automation` (automation),
  `Utopia.Tests.*` (tests; the namespace mirrors the folder).
- Every class Godot instantiates or the editor shows (`Node`, `Resource`
  subclasses) is `public partial class`. Classes used as exported types or
  `.tres` scripts get `[GlobalClass]`.
- Members, properties, methods, signals, constants, enum values: `PascalCase`.
  Locals and parameters: `camelCase`. Private fields: `_camelCase`.
- Signals: `[Signal] public delegate void <Name>EventHandler(...)`, named as
  past-tense facts or `*Requested` intents. Emit with `EmitSignal<Name>(...)`.
- Nodes `PascalCase`. Resource ids `snake_case` with a kind prefix (`hero_`,
  `card_`, `map_`, `mission_`). Translation keys `UPPER_SNAKE_CASE` with an
  area prefix; plural id = `KEY_PLURAL`.
- Exported properties are what `.tscn`/`.tres` files store (`NameKey = "..."`);
  renaming one means updating every scene/resource that sets it.
- The build treats warnings as errors (`Utopia.csproj`). Don't suppress them.

## Scene ownership

Routed scenes are composition roots; components own one responsibility;
screens emit intent; HUD displays. Full table: `docs/SCENE_TREE_RULES.md`.
Never put a whole system into one script (no GameManager, no giant player
script, no level script with rules).

## When to create a scene, a Resource, or a class

- **Scene**: it exists in the world or on screen, has children, or is reused
  as a unit (character, screen, HUD, component bundles).
- **Resource**: designer-tuned or shared data (stats, cards, maps, missions,
  enemies, dialogue, tokens, route table). Text fields are translation keys.
- **Plain C# class**: pure logic/state without editor data (deck, loadout,
  turn order) — easiest to unit-test. Use `RefCounted` only if it must carry
  Godot signals (e.g. `ResourceWallet`).
- **Component node** (`Node` subclass): reusable behavior attached to scenes
  (`HealthComponent`, `GridMover`, `CharacterAnimator`).

## When to use a signal

When the sender must not depend on the receiver (facts and intents).
Call methods directly on things you own. No global event bus. Connect in the
composition root. Buttons: `GameButton.Activated`. Contracts and inventory:
`docs/SIGNALS_AND_EVENTS.md` — add every new signal there.

**C# does not disconnect a freed node from a longer-lived emitter.** A node
that listens to an autoload (or anything that outlives it) subscribes in
`_EnterTree` and unsubscribes in `_ExitTree`, using a named method — never a
lambda (guard G003). Listening to your own children needs no unsubscribe.

## When an Autoload is allowed

Only for genuinely global-lifetime services (scene routing, saving, session
state, settings, input method; later audio). Requirements: an ADR, a row in
the autoload table in `ARCHITECTURE.md` explaining why it must be global, a
static `Instance` property like the existing services, a reset in
`TestRunner.cs::ResetGlobalState()`, and no UI or game rules inside it. A UI
autoload is not allowed; use a scene-owned `ScreenStack`.

## Avoiding hard-coded node paths

Use `[Export]` node references (assigned in the `.tscn`), signals, Resources,
groups, or ids (`Routes.*`). No `GetNode("../..")`, no `/root/...` lookups
(guard G004; reach autoloads through `<Service>.Instance`), no scene paths in
code (guard G007) — routes go through `route_table.tres`, instantiated scenes
are exported `PackedScene` fields. Only `SceneRouter` changes scenes (guard G005).

## Async code

- Await engine signals with `await ToSignal(obj, Class.SignalName.X)`;
  continuations run on the main thread. Never use `Task.Run`/`Task.Delay` for
  game logic (they resume off the main thread); use timers or frames.
- Methods that await return `Task`/`Task<T>`. Start them fire-and-forget with
  `_ = DoThing();` when nobody awaits the result. `async void` is allowed only
  for Godot lifecycle overrides like `_Ready` (guard G006).
- Never `await SceneRouter.Instance.Goto()` from the scene being replaced.

## Data and JSON

Saves, fixtures, and the automation protocol are plain JSON handled with
`System.Text.Json.Nodes` (`JsonObject`, `JsonArray`). Read values through
`core/serialization/JsonNodes.cs` (`JsonNodes.Int(node, fallback)`, `Str`,
`Bool`, `ValuesEqual`), which treat `50` and `50.0` alike and never throw on
missing data. A `JsonNode` can have only one parent: copy (`JsonNodes.Copy`)
before putting an existing node into another container. Saved data uses
`snake_case` keys and stable ids only.

## Player-facing text and output

- Every visible string is a translation key in `en.po` **and** every launch
  locale (with `#.` note and, outside `en`, a `#. English:` reference line).
- Fill placeholders with `Loc.Format()`/`Loc.FormatPlural()`; format numbers
  with `LocaleFormat`. Never fill `Tr(key)` by hand or concatenate strings.
- Never show exceptions, ids, enum names, paths, JSON, or TODOs to players.
  Errors say what happened, whether progress is safe, and what to do next.
  Technical details go to `DevLog`.

## UI rules

`UiScreen` + `ScreenStack` + `GameButton` + `FocusChain`; one primary action;
`DefaultFocus` set; ≥ 44 px targets; transitions via `UiMotion` (reduced
motion respected); theme changes go through `ui_tokens.tres` + rebuilding the
theme (`godot --headless res://core/ui/theme/build_theme.tscn`), never by
hand-editing `utopia_theme.tres`. Details: `docs/UI_ARCHITECTURE.md`.

## Automation (remote testing interface)

- Every new button, switch, combobox, and `UiScreen` root gets
  `metadata/automation_id = "<area>.<name>"` (screens: `screen.<name>`);
  add new screens to `TestUiLayout.Screens` (checked by
  `TestEveryButtonInShippedScreensHasAnAutomationId`).
- New game objects get stable ids derived from content ids via
  `UtopiaAutomationProvider` — never from node names or display text.
- Game code (`autoload/`, `core/`, `features/`, `scenes/`) must never
  reference automation classes or the `Utopia.Automation` namespace
  (test-enforced); only `AutomationGate` starts the server, by type name,
  through `IAutomationServer`. `Utopia.csproj` compiles `automation/**` and
  `tests/**` out of release builds — keep it that way.
- Never add arbitrary code/console execution, file-path parameters, or
  unvalidated mutation to the protocol. New methods need a capability, a
  strict schema in `methods.json`, a registered handler, a client wrapper,
  tests, and a regenerated docs table (`python3 tools/automation/scripts/gen_method_table.py`).
- Automation exchanges ids and translation keys, never localized text.
- Details and how-tos: `docs/AUTOMATION.md`.

## Art, audio, and AI-generated assets

- Follow `docs/ART_DIRECTION.md` (confirmed target), `docs/ART_IMPLEMENTATION_GUIDE.md`
  (grid, palette, naming, import, budgets), and `docs/UI_STYLE_GUIDE.md`.
- Isometric projection lives only in `BattleGrid.CellToLocal/LocalToCell`;
  the gameplay camera zoom stays an integer; pixel art uses nearest filtering
  and integer positions (tests in `tests/unit/art/`).
- Soul teal (`#5fc9c0` ramp) is reserved for soul energy; never use it for decoration.
- Score visual changes with `docs/VISUAL_QUALITY_RUBRIC.md` from in-engine
  captures (screenshot tool), update the state matrix, and never score states
  you did not capture (mark N/E). Third-party reference images are not
  committed to the repository.
- Asset sources (see [AI_WORKFLOW.md](AI_WORKFLOW.md#art-and-sound)):
  sprites and animations from **PixelLab**, music from **Suno**, voices from
  **ElevenLabs**. Generated sprites may ship only after they meet the art
  guide's grid, palette, and proportion rules (fix or regenerate otherwise);
  other generated imagery is concept-only.
- Every generated or third-party asset gets a row in `assets/PROVENANCE.md`
  (file, tool, date, prompt summary, plan/license, approved by) before it is
  committed. Check the tool's current terms for commercial use; never commit
  an asset whose rights are unclear.
- Voice lines live at `assets/audio/vo/<locale>/<voice_id>.ogg` (see
  `docs/LOCALIZATION.md`); music at `assets/audio/music/`. Audio is `.ogg`.

## Gameplay rules vs. visuals

The logical grid is the source of truth. Rules resolve first and never wait
for animations; animation events are feedback only. No physics bodies for
units (`docs/COLLISION_AND_HIT_DETECTION.md`,
`docs/ANIMATION_AND_MOVEMENT.md`).

## How features are tested

Every change ships with tests (`docs/TESTING.md`): unit tests for logic and
data, integration tests for scenes/flows, new screens added to
`TestUiLayout.Screens`. Engine/script errors and exceptions in signal
handlers fail tests automatically. Validation commands (all must pass before
you finish):

```bash
python3 tools/guards/check_rules.py                          # seconds; run before every push
GODOT=/path/to/godot-mono tests/run_tests.sh                 # guards, C# build, tests, Python client tests,
                                                             # automation scenario, gating, smoke, key extraction
```

If UI changed, also capture screenshots (command in `docs/TESTING.md`) and
look at them.

## Guarded rules

These rules are checked by `tools/guards/check_rules.py` (part of
`run_tests.sh` and CI). A finding prints its id; fix the code, never the guard,
unless the rule itself changes (then update this table, the guard, its test,
and [MISTAKES.md](MISTAKES.md) together).

| Id | Rule | Why it exists |
|---|---|---|
| G001 | No GDScript files | C#-only project (ADR 0011) |
| G002 | A Godot C# class lives in a file of the same name | Godot silently fails to attach the script otherwise |
| G003 | Autoload signal `+=` needs a matching `-=`; no lambdas on autoload signals | M001: freed menus received callbacks and crashed |
| G004 | No `GetNode("../…")` or `/root/` lookups in game code | Scene ownership rules |
| G005 | Only `SceneRouter` changes scenes | One transition path with fade and failure handling |
| G006 | `async void` only for Godot lifecycle overrides | Exceptions in `async void` helpers are lost |
| G007 | No `.tscn` paths in game code | Routes and exported `PackedScene` fields instead |

## How architectural decisions are documented

Add `docs/decisions/NNNN-short-title.md` (next number) using the template in
`docs/decisions/README.md`: context, decision, consequences, alternatives.
Mark superseded ADRs instead of deleting them. Then update the affected docs
(ARCHITECTURE, SCENE_TREE_RULES, SIGNALS_AND_EVENTS, RESOURCES, SAVE_SYSTEM,
LOCALIZATION, UI_ARCHITECTURE, this file). Docs must describe what the code
does — never claim untested support.

## Definition of done for any task

Code + tests + docs updated, guards clean, `tests/run_tests.sh` green, no new
warnings in smoke runs, player text localized in all launch locales, a short
"how to play it" note for the owner in the PR (what to try, what should
happen), and the final report lists every documentation file created or
updated.

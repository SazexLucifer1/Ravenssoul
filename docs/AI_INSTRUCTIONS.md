# Instructions for AI assistants (and humans)

Read this before changing the project. It is the contract that keeps the
architecture scalable. When a task changes any rule below, update this file
in the same change.

## Before you start

1. Read `README.md`, `docs/ARCHITECTURE.md`, the docs for the area you touch,
   the relevant ADRs in `docs/decisions/`, and
   `docs/game-design/mechanics-and-core-loop.md` (approved design; do not
   change design decisions there without being asked).
2. Engine: Godot **4.6**, typed GDScript, 2D, GL Compatibility, Windows/Linux,
   single-player.

## Folder and naming conventions

| Folder | Put here |
|---|---|
| `autoload/` | Only the five documented services |
| `core/` | Game-agnostic code: components, routing, save, localization, input, diagnostics, UI kit |
| `features/<name>/` | Gameplay features: `data/` (Resource scripts), `content/` (.tres), `ui/`, logic, scenes, README |
| `scenes/` | Routed top-level scenes and game menus |
| `assets/` | Localization catalogs, generated theme, art/audio/fonts |
| `automation/` | Remote automation server/protocol/adapter only (see `docs/AUTOMATION.md`) |
| `tools/automation/` | Python automation client, CLI, MCP adapter, scenario (stdlib only) |
| `tests/` | `unit/<area>/test_*.gd`, `integration/test_*.gd`, `framework/`, `tools/` |
| `docs/` | Documentation, `decisions/` ADRs, `game-design/` |

- Files `snake_case`; scene and script share a name (`pause_menu.tscn/.gd`).
- `class_name` in `PascalCase` for any script used as a type.
- Nodes `PascalCase`. Signals past-tense or `*_requested`. Constants `UPPER_SNAKE`.
- Resource ids `snake_case` with a kind prefix (`hero_`, `card_`, `map_`, `mission_`).
- Translation keys `UPPER_SNAKE_CASE` with an area prefix; plural id = `KEY_PLURAL`.
- Type every variable, parameter, and return (`untyped_declaration` warns).

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
- **RefCounted class**: pure logic/state without editor data (deck, wallet,
  loadout, turn order) — easiest to unit-test.
- **Component node** (`extends Node`): reusable behavior attached to scenes
  (`HealthComponent`, `GridMover`, `CharacterAnimator`).

## When to use a signal

When the sender must not depend on the receiver (facts and intents).
Call methods directly on things you own. No global event bus. Connect in the
composition root. Buttons: `GameButton.activated`. Contracts and inventory:
`docs/SIGNALS_AND_EVENTS.md` — add every new signal there.

## When an Autoload is allowed

Only for genuinely global-lifetime services (scene routing, saving, session
state, settings, input method; later audio). Requirements: an ADR, a row in
the autoload table in `ARCHITECTURE.md` explaining why it must be global, a
reset in `test_runner.gd::_reset_global_state()`, and no UI or game rules
inside it. A UI autoload is not allowed; use a scene-owned `ScreenStack`.

## Avoiding hard-coded node paths

Use `@export` node references (assigned in the `.tscn`), signals, Resources,
groups, or ids (`Routes.*`). No `get_node("../..")`, no `/root/...` lookups
(autoloads by global name only), no scene paths in code — routes go through
`route_table.tres`, instantiated scenes are exported `PackedScene` fields.

## Player-facing text and output

- Every visible string is a translation key in `en.po` **and** every launch
  locale (with `#.` note and, outside `en`, a `#. English:` reference line).
- Fill placeholders with `Loc.format()`/`Loc.format_plural()`; format numbers
  with `LocaleFormat`. Never `tr(key).format()` or string concatenation.
- Never show exceptions, ids, enum names, paths, JSON, or TODOs to players.
  Errors say what happened, whether progress is safe, and what to do next.
  Technical details go to `DevLog`.

## UI rules

`UiScreen` + `ScreenStack` + `GameButton` + `FocusChain`; one primary action;
`default_focus` set; ≥ 44 px targets; transitions via `UiMotion` (reduced
motion respected); theme changes go through `ui_tokens.tres` + rebuilding the
theme (`godot --headless --script res://core/ui/theme/build_theme.gd`), never
by hand-editing `utopia_theme.tres`. Details: `docs/UI_ARCHITECTURE.md`.

## Automation (remote testing interface)

- Every new button, switch, combobox, and `UiScreen` root gets
  `metadata/automation_id = "<area>.<name>"` (screens: `screen.<name>`);
  add new screens to the list in `test_every_button_in_shipped_screens_has_an_automation_id`.
- New game objects get stable ids derived from content ids via
  `UtopiaAutomationProvider` — never from node names or display text.
- Game code (`autoload/`, `core/`, `features/`, `scenes/`) must never
  reference automation classes (test-enforced); only `AutomationGate` starts
  the server, by path.
- Never add arbitrary code/console execution, file-path parameters, or
  unvalidated mutation to the protocol. New methods need a capability, a
  strict schema in `methods.json`, a handler, a client wrapper, tests, and a
  regenerated docs table (`python3 tools/automation/scripts/gen_method_table.py`).
- Automation exchanges ids and translation keys, never localized text.
- Details and how-tos: `docs/AUTOMATION.md`.

## Art and visual quality

- Follow `docs/ART_DIRECTION.md` (confirmed target), `docs/ART_IMPLEMENTATION_GUIDE.md`
  (grid, palette, naming, import, budgets), and `docs/UI_STYLE_GUIDE.md`.
- Isometric projection lives only in `BattleGrid.cell_to_local/local_to_cell`;
  the gameplay camera zoom stays an integer; pixel art uses nearest filtering
  and integer positions (tests in `tests/unit/art/`).
- Soul teal (`#5fc9c0` ramp) is reserved for soul energy; never use it for decoration.
- Score visual changes with `docs/VISUAL_QUALITY_RUBRIC.md` from in-engine
  captures (screenshot tool), update the state matrix, and never score states
  you did not capture (mark N/E). Third-party reference images are not
  committed to the repository.
- AI-generated imagery is concept-only; shipped art must meet the guide's grid,
  palette, and proportion rules.

## Gameplay rules vs. visuals

The logical grid is the source of truth. Rules resolve first and never wait
for animations; animation events are feedback only. No physics bodies for
units (`docs/COLLISION_AND_HIT_DETECTION.md`,
`docs/ANIMATION_AND_MOVEMENT.md`).

## How features are tested

Every change ships with tests (`docs/TESTING.md`): unit tests for logic and
data, integration tests for scenes/flows, new screens added to
`test_ui_layout.gd::SCREENS`. Engine/script errors fail tests automatically.
Validation commands (all must pass before you finish):

```bash
GODOT=/path/to/godot tests/run_tests.sh            # full suite, Python client tests, automation scenario, gating, smoke, key extraction
godot --headless --script res://tests/tools/extract_keys.gd   # localization keys
```

If UI changed, also capture screenshots (command in `docs/TESTING.md`) and
look at them.

## How architectural decisions are documented

Add `docs/decisions/NNNN-short-title.md` (next number) using the template in
`docs/decisions/README.md`: context, decision, consequences, alternatives.
Mark superseded ADRs instead of deleting them. Then update the affected docs
(ARCHITECTURE, SCENE_TREE_RULES, SIGNALS_AND_EVENTS, RESOURCES, SAVE_SYSTEM,
LOCALIZATION, UI_ARCHITECTURE, this file). Docs must describe what the code
does — never claim untested support.

## Definition of done for any task

Code + tests + docs updated, `tests/run_tests.sh` green, no new warnings in
smoke runs, player text localized in all launch locales, and the final report
lists every documentation file created or updated.

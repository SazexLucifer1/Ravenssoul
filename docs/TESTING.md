# Testing

## Commands

```bash
# Everything CI's main job runs (import, Godot tests, leak check, key extraction,
# Python automation-client tests, headless automation scenario, production-gating
# check, two boot smoke runs):
GODOT=/path/to/godot tests/run_tests.sh

# Only the automated tests:
godot --headless --path . --audio-driver Dummy res://tests/framework/test_runner.tscn

# A subset (substring of the test file path):
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=save
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=integration/

# Locale screenshots (needs a renderer; Linux headless machines use Xvfb):
xvfb-run -a -s "-screen 0 1280x720x24" godot --path . --rendering-driver opengl3 \
  --audio-driver Dummy res://tests/tools/screenshot_capture.tscn -- --out=$PWD/screenshots

# Automation (see docs/AUTOMATION.md):
(cd tools/automation && python3 -m unittest discover -s tests -t .)
PYTHONPATH=tools/automation xvfb-run -a python3 -m utopia_automation scenario first_mission \
  --godot $GODOT --project . --out automation-output --require-screenshot
```

Run `godot --headless --import` once after cloning (or after adding
`class_name` scripts) so the global class cache exists. `run_tests.sh` does it.

## Framework

In-repo, no addon (ADR 0005):

- `tests/framework/test_runner.tscn` runs as the **main scene**, so all
  autoloads exist exactly as in the game. It discovers `test_*.gd` under
  `tests/unit/` and `tests/integration/`, runs every `test_*` method (tests
  may `await`), and exits 0/1.
- `TestCase` (`tests/framework/test_case.gd`): `assert_*`, `add_node`,
  `instantiate`, `wait_frames`, `wait_seconds`, `wait_until`, `press_action`,
  `expect_engine_errors(n)`.
- `ErrorCaptureLogger` (Godot ≥ 4.5 `Logger`) records engine and script
  errors; **any error during a test fails it** unless declared. Script errors
  in a test script fail the file.
- Before each test the runner resets global state: `MemorySaveBackend`
  (real saves are never touched), test settings file, reduced motion on,
  English, pseudolocalization off, no campaign, unpaused.
- Tests that change scenes use `SceneRouter`; the runner gives the router a
  placeholder current scene so the runner itself is never freed.

## Layout

```
tests/unit/core/          health, save service, routes, input method
tests/unit/characters/    stats combination, loadout, content validation
tests/unit/combat/        deck, turn order, grid legality ("collision")
tests/unit/inventory/     resource wallet
tests/unit/localization/  catalogs, keys, plurals, fallback, pseudo, formatting, fonts
tests/unit/ui/            game button states, screen stack, safe area, theme
tests/unit/automation/    schema validator, predicates, auth/origin/rate limit/profiles/gate,
                          HTTP parsing, event log, input driver, dependency boundary
tests/integration/        boot flow, character movement/animation, gameplay flow, UI layout,
                          automation server (dispatch, HTTP + WebSocket over real sockets)
tests/framework/          runner, TestCase, AutomationTestCase (in-process server + socket clients)
tests/tools/              screenshot capture, translation key extraction
tools/automation/tests/   Python: client vs fake server, MCP adapter, launcher, protocol/doc parity
```

## What is covered (179 Godot tests + 25 Python tests at the time of writing)

- Definition of done: bootstrap → main menu → gameplay (`test_boot_flow`) plus
  smoke runs of the real executable in `run_tests.sh`.
- Health component, stat combination, 20-card decks, deck draw rules, turn
  order, wallet, content validity (ids, deck sizes, map size).
- Save: round trip, envelope, missing/corrupt/newer schema, slot-id safety,
  write failure keeps the old save, atomic file writes with backup, session
  round trip, unknown content rejected.
- Router: rejection during transitions, unknown routes, motion on/off,
  failure feedback in the menu.
- Movement/animation and grid collision equivalents (see the two audit docs).
- UI: button states and repeat input, modal stacking, focus restoration,
  `ui_cancel`, input-method switching and glyphs, gamepad bindings for menu
  actions, safe areas, theme completeness, contrast, text scaling,
  7 resolutions, pseudo/German/150 % text fit, RTL mirroring and focus order,
  no focus dead ends, one primary action, pause/settings/abandon/result flows,
  autosave success/failure/retry.
- Localization: see `LOCALIZATION.md`.
- Automation: protocol validation, malformed commands, authorization and
  capability denial, rate limiting, production gating, export exclusion,
  stable ids, deterministic waits, held-input release (session close, scene
  change, idle timeout, shutdown), reconnects, scene transitions, HTTP/WebSocket
  security, and the end-to-end first-mission scenario — see `AUTOMATION.md`.

## Writing a test

```gdscript
extends TestCase

func test_something() -> void:
	var health := HealthComponent.new()
	add_node(health)                 # freed automatically after the test
	health.apply_damage(3)
	assert_eq(health.current_health, 7)
```

Name files `test_<subject>.gd`, methods `test_<behavior>`. Prefer unit tests
on `RefCounted`/Resource logic; use integration tests for scenes and flows.
Don't `await SceneRouter.goto()` from inside a scene that will be replaced
(see `SCENE_TREE_RULES.md`).

## Not covered / limitations

- Screenshots are produced for human review (and CI artifacts) but not
  diffed against baselines.
- Exports (Windows/Linux builds) are not built in CI.
- Real controller hardware, IME text input, and screen readers are untested.
- Performance budgets are not measured.

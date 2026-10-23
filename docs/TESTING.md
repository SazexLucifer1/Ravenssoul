# Testing

## Commands

```bash
# Everything CI's main job runs (guard scripts, C# build with warnings as errors,
# import, Godot tests, leak check, key extraction, guard self-tests, Python
# automation-client tests, headless automation scenario, production-gating check,
# two boot smoke runs). GODOT must be the Godot 4.6 .NET build; dotnet 8 on PATH:
GODOT=/path/to/godot-mono tests/run_tests.sh

# Toolchain for a fresh Linux machine or cloud session (Godot .NET + .NET 8 SDK):
bash tools/setup/install_toolchain.sh

# Only the guard scripts (seconds; run them before every push):
python3 tools/guards/check_rules.py

# Only the automated tests:
godot --headless --path . --audio-driver Dummy res://tests/framework/test_runner.tscn

# A subset (case-insensitive substring of the test label, e.g. unit/core/TestSaveService):
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=save
godot --headless --path . res://tests/framework/test_runner.tscn -- --filter=integration/

# Locale screenshots (needs a renderer; Linux headless machines use Xvfb):
xvfb-run -a -s "-screen 0 1280x720x24" godot --path . --rendering-driver opengl3 \
  --audio-driver Dummy res://tests/tools/screenshot_capture.tscn -- --out=$PWD/screenshots

# Visual review (docs/VISUAL_QUALITY_RUBRIC.md): annotate captured frames
xvfb-run -a godot --path . --rendering-driver opengl3 --audio-driver Dummy \
  res://tests/tools/annotate_frames.tscn -- --in=$PWD/screenshots --out=$PWD/docs/art/annotated

# Automation (see docs/AUTOMATION.md):
(cd tools/automation && python3 -m unittest discover -s tests -t .)
PYTHONPATH=tools/automation xvfb-run -a python3 -m utopia_automation scenario first_mission \
  --godot $GODOT --project . --out automation-output --require-screenshot
```

C# must be compiled before Godot runs it: `dotnet build` (or the editor's
Build button) after every code change, and `godot --headless --import` once
after cloning. `run_tests.sh` does both.

## Framework

In-repo, no addon (ADR 0005):

- `tests/framework/test_runner.tscn` runs as the **main scene**, so all
  autoloads exist exactly as in the game. It finds every `TestCase` subclass
  in the `Utopia.Tests.Unit.*` / `Utopia.Tests.Integration` namespaces by
  reflection, runs every public `Test*` method (a test may return `Task` and
  `await`), and exits 0/1. A test's label is its folder plus class name
  (`unit/core/TestSaveService`), derived from the namespace.
- `TestCase` (`tests/framework/TestCase.cs`): `AssertEq` (deep equality;
  JSON nodes compare by value, so `AssertEq(result["cell"], new[] { 1, 10 })`
  works), `AssertTrue/False/Ne/Null/NotNull/Empty/Contains`, `AddNode`,
  `Instantiate<T>`, `WaitFrames`, `WaitSeconds`, `WaitUntil`, `PressAction`,
  `ExpectEngineErrors(n)`. Exceptions thrown by a test fail it with the stack trace.
- `ErrorCaptureLogger` (Godot ≥ 4.5 `Logger`) records engine and script
  errors — including C# exceptions thrown inside signal handlers, which Godot
  logs instead of propagating; **any error during a test fails it** unless declared.
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
tests/unit/art/           pixel rules: nearest filtering, snapping, integer camera zoom, y-sort, 640x360 frame
tests/unit/automation/    schema validator, predicates, auth/origin/rate limit/profiles/gate,
                          HTTP parsing, event log, input driver, dependency boundary
tests/integration/        boot flow, character movement/animation, gameplay flow, UI layout,
                          automation server (dispatch, HTTP + WebSocket over real sockets)
tests/framework/          runner, TestCase, AutomationTestCase (in-process server + socket clients)
tests/tools/              screenshot capture (locales + review states), frame annotation, translation key extraction
tools/automation/tests/   Python: client vs fake server, MCP adapter, launcher, protocol/doc parity
tools/guards/tests/       Python: every guard catches its mistake; the repository is clean
```

## What is covered (186 Godot tests + 25 automation-client + 10 guard tests at the time of writing)

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

```csharp
namespace Utopia.Tests.Unit.Core;

public class TestSomething : TestCase
{
    public void TestDamage()
    {
        HealthComponent health = AddNode(new HealthComponent()); // freed after the test
        health.ApplyDamage(3);
        AssertEq(health.CurrentHealth, 7);
    }

    public async Task TestSomethingOverFrames()
    {
        await WaitFrames(2);
    }
}
```

Name files `Test<Subject>.cs` in `tests/unit/<area>/` (namespace
`Utopia.Tests.Unit.<Area>`) or `tests/integration/` (`Utopia.Tests.Integration`);
methods `Test<Behavior>`. Godot node subclasses a test needs (e.g.
`FakeScreen`) get their own file named after the class. Prefer unit tests on
plain C#/Resource logic; use integration tests for scenes and flows. Don't
`await SceneRouter.Instance.Goto()` from inside a scene that will be replaced
(see `SCENE_TREE_RULES.md`).

## Not covered / limitations

- Screenshots are produced for human review (and CI artifacts) but not
  diffed against baselines.
- Exports (Windows/Linux builds) are not built in CI.
- Real controller hardware, IME text input, and screen readers are untested.
- Performance budgets are not measured.

## Nightly run

`.github/workflows/nightly.yml` runs the whole suite, the automation scenario
with a real renderer, and the locale screenshots every night (and on demand).
A failure opens (or comments on) one issue labelled `nightly-failure`, so the
morning starts with a clear list instead of a red badge. See
[AI_WORKFLOW.md](AI_WORKFLOW.md).

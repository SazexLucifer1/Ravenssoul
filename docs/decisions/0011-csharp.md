# 0011 — C# instead of GDScript
Status: Accepted
Date: 2026-10-23

## Context
Replaces the "typed GDScript" language choice in `ARCHITECTURE.md`.
The owner builds the game with AI assistants and does not read code. Work is
planned and reviewed by one model and implemented by another (see
`docs/AI_WORKFLOW.md`). The owner decided that the project uses Godot 4.6
with C#. At that point the project was ~8,500 lines of typed GDScript with
186 engine tests, a remote automation server, and CI.

## Decision
Port everything to C# (.NET 8, Godot .NET build) in one step and remove
GDScript entirely:

- Game code in namespace `Utopia`, automation in `Utopia.Automation`, tests
  in `Utopia.Tests.*`; one assembly (`Utopia.csproj`), warnings as errors.
- C# file name = class name (`PascalCase.cs`); scenes/resources keep their
  `snake_case` names; exported properties (and therefore `.tscn`/`.tres`
  keys) are PascalCase.
- Autoload nodes keep their names; code reaches them through static
  `Instance` properties.
- JSON (saves, fixtures, automation protocol) uses `System.Text.Json.Nodes`
  with forgiving readers in `core/serialization/JsonNodes.cs`. The save file
  format and the automation wire protocol are unchanged.
- The in-repo test runner is C# and discovers `TestCase` subclasses by
  reflection; command-line tools that were `--script` GDScript files are now
  small scenes (`build_theme.tscn`, `extract_keys.tscn`).
- Release builds: Godot compiles every `.cs` file into the assembly, so
  export filters alone can't keep code out. `Utopia.csproj` removes
  `automation/**` and `tests/**` from `ExportRelease` builds (QA exports set
  `UtopiaAutomation=true`); `AutomationGate` finds the server by type name
  through `IAutomationServer` and reports "not included" when it is absent.
- Mechanical rules are enforced by guard scripts (`tools/guards/`).

## Consequences
- Requires the Godot **.NET** editor/runtime and the .NET 8 SDK everywhere
  (developer machines, CI, cloud sessions, Codex environments);
  `tools/setup/install_toolchain.sh` installs both on Linux.
- C# must be compiled before Godot runs it (`dotnet build`, done by
  `run_tests.sh` and the editor).
- C# signal subscriptions to longer-lived emitters are not cleaned up when
  the listener is freed (unlike GDScript); listeners must unsubscribe
  (rule + guard G003, `docs/MISTAKES.md` M001).
- Exceptions inside signal handlers are logged by Godot rather than
  propagated; the test runner's logger turns them into test failures.
- Static typing across game, automation, and tests; refactors are checked by
  the compiler. Web export is not available for Godot 4 C# (not a target).
- Screenshots of all captured states match the GDScript version pixel for
  pixel, except frames captured mid-animation (timing).

## Alternatives considered
- **Keep GDScript**: least work, but contradicts the owner's decision and
  splits examples/tutorials the workers rely on.
- **Mixed (new code in C#)**: two languages, two sets of rules, cross-language
  calls by string names — more room for the mistakes this workflow tries to
  eliminate.

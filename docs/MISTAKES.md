# Mistake log

Every mistake that happened (or would have happened) more than once, what it
cost, and the rule or guard that now prevents it. Newest first. Process:
[AI_WORKFLOW.md](AI_WORKFLOW.md#when-the-ai-makes-the-same-mistake-twice).
Guard ids refer to `tools/guards/check_rules.py` and the "Guarded rules"
table in [AI_INSTRUCTIONS.md](AI_INSTRUCTIONS.md).

| Id | Date | Mistake | Effect | Prevention |
|---|---|---|---|---|
| M002 | 2026-10-23 | A test cleaned up by listing a folder that only exists after another test created it | On a fresh checkout the first full run had 8 red save tests; reruns were green (looked flaky) | Fixed in `TestSaveService.AfterEach` (checks the folder exists). Rule: tests must pass on a clean machine; cleanup code tolerates missing files/folders |
| M001 | 2026-10-23 | C# nodes subscribed to autoload signals (`+=`) without unsubscribing — fine in GDScript, wrong in C# | After a menu closed, a language change or input-device switch called into the freed menu → `ObjectDisposedException` (9 tests red during the C# port) | Rule in AI_INSTRUCTIONS ("When to use a signal"); guard **G003**: autoload `+=` needs a matching `-=`, no lambdas |

## Guards that encode existing rules

These rules predate the log; the guards make them mechanical so workers
can't drift from them.

| Guard | Rule |
|---|---|
| G001 | No GDScript (ADR 0011) |
| G002 | Godot C# class name = file name |
| G004 | No `GetNode("../…")` / `/root/` lookups in game code |
| G005 | Only `SceneRouter` changes scenes |
| G006 | `async void` only for Godot lifecycle overrides |
| G007 | No `.tscn` paths in game code |

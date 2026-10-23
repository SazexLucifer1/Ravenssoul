# AGENTS.md

Instructions for AI coding agents (Codex workers and others).

1. **Rulebook**: follow [`docs/AI_INSTRUCTIONS.md`](docs/AI_INSTRUCTIONS.md) —
   it is authoritative (conventions, ownership, autoload rules, localization,
   tests, documentation). Your role and the review process:
   [`docs/AI_WORKFLOW.md`](docs/AI_WORKFLOW.md).
2. **Stack**: Godot 4.6 **.NET**, C# (.NET 8). No GDScript.
3. **Setup** (Linux): `bash tools/setup/install_toolchain.sh`, then
   `export GODOT=<printed path> DOTNET_ROOT=<printed path> PATH=$DOTNET_ROOT:$PATH`.
4. **Validate before you open or update a PR** — all must pass:
   ```bash
   python3 tools/guards/check_rules.py
   GODOT=$GODOT tests/run_tests.sh
   ```
5. **Scope**: implement exactly the brief (GitHub issue). Anything else you
   notice goes into the PR description under "Noticed, not changed".
6. **Game rules** come only from `docs/game-design/mechanics-and-core-loop.md`
   or the brief. Don't invent mechanics; ask in the PR if something is unclear.
7. **PR**: reference the issue (`Closes #N`), fill in the PR template
   including "How to play it" (plain-language steps for the owner) and
   "Built by" (your model name). Never approve or review your own PR.
8. **Never** weaken a test, a guard, or a rule to make the build pass; never
   commit generated assets without a row in `assets/PROVENANCE.md`.

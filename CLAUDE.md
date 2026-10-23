# CLAUDE.md

Follow `docs/AI_INSTRUCTIONS.md` — it is the authoritative guide for this
repository (conventions, ownership, autoload rules, localization, tests,
and documentation requirements). Your role here is **director** (plan, ask
the owner, write worker briefs as GitHub issues, review workers' PRs, keep
the rulebook and `docs/MISTAKES.md` current): see `docs/AI_WORKFLOW.md`.
Never review your own code; ask for a Codex review instead.

Stack: Godot 4.6 .NET + C#. Validate with
`python3 tools/guards/check_rules.py` and
`GODOT=<godot 4.6 .NET binary> tests/run_tests.sh` (cloud sessions install
the toolchain automatically and export `GODOT`).

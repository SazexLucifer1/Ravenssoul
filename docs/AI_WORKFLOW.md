# AI workflow

How this game gets built. The owner describes what they want in plain
language and judges the result by playing it; AI models plan, build, and
review. The rules for *what* code must look like are in
[AI_INSTRUCTIONS.md](AI_INSTRUCTIONS.md); this file is about *who does what, in
which order*.

## Roles

| Role | Who | Does | Never |
|---|---|---|---|
| Owner | Fabian | Describes features and changes in plain words (German or English), answers questions, plays builds, decides go / no-go | Has to read code |
| Director | Claude Code | Turns wishes into a plan, asks questions until the plan is unambiguous, writes worker briefs, reviews workers' PRs (reads the code, runs the suite, checks the playtest notes), keeps the rulebook and the mistake log up to date | Reviews its own code |
| Workers | Codex | Implement one brief per PR, run the guards and the full suite, write the "how to play it" note | Change the rulebook or the design without a brief that says so |

**The model that wrote something never reviews it.** Codex-built PRs are
reviewed by Claude; anything Claude builds itself is reviewed by Codex
(comment `@codex review` on the PR). The PR template records both.

## The loop

1. **Ask.** The owner says what they want ("Gegner sollen Karten spielen").
2. **Plan.** The director reads the design document and the code, asks the
   owner the questions only they can answer (what should happen, not how to
   code it), and proposes a short plan in plain language.
3. **Brief.** The director writes one GitHub issue per worker task with the
   *Worker brief* template (`.github/ISSUE_TEMPLATE/worker-brief.md`): goal in
   player terms, the design-doc section it implements, scope in/out, tests,
   docs, and the playtest script. Small, independent briefs beat big ones.
4. **Build.** A Codex worker takes the issue (Codex web: open the repository,
   paste the issue link or text, run; or mention `@codex` on the issue where
   the Codex GitHub integration supports it) and opens a PR that says
   `Closes #<issue>` and fills in the PR template.
5. **Check.** CI runs the guards, the C# build, and all tests on the PR.
6. **Review.** The other model reviews (see Roles). Findings go back to the
   worker as PR comments; a repeated mistake goes into the mistake log (below).
7. **Play.** The owner follows the PR's "how to play it" steps and says
   whether it feels right. Feel problems become new briefs, not silent fixes.
8. **Merge.** When CI is green, the review is approved, and the owner is
   happy (for anything player-visible), the PR is merged.

During the day: steps 1–8. At night: the full suite runs on `main`
(see "Nightly tests").

## Writing a good brief (director)

- One outcome the owner can see or feel, described as player behaviour.
- Name the exact design-doc section; quote the rule. Mechanics come from
  `docs/game-design/mechanics-and-core-loop.md` or from the owner — never
  from memory of other games or rule systems. If the design doc doesn't say,
  ask the owner before writing the brief.
- List what is out of scope, so the worker doesn't "helpfully" add it.
- List the tests that must exist and the docs that must change.
- Write the playtest script: what to click/press, what should happen.

## Reviewing a PR (reviewer)

- CI green; guards clean; no new warnings.
- The change does what the brief says and nothing else.
- The rulebook is followed (naming, scene ownership, signals, localization,
  automation ids, async rules, JSON rules).
- Tests actually exercise the new behaviour (read them, don't just count them).
- Docs updated; the "how to play it" note is concrete.
- If you see a mistake you have seen before: add it to the mistake log and
  propose the rule/guard in the same review (see next section).

## When the AI makes the same mistake twice

1. Add an entry to [MISTAKES.md](MISTAKES.md): what happened, why, how it was
   noticed.
2. Add or sharpen the rule in [AI_INSTRUCTIONS.md](AI_INSTRUCTIONS.md).
3. If a script can detect it, add a guard to `tools/guards/check_rules.py`
   with a test in `tools/guards/tests/` that shows the guard catching the
   mistake, and list it under "Guarded rules". Guards run in
   `tests/run_tests.sh` and CI, so the mistake can't merge again.
4. Prefer a guard over a sentence whenever possible: sentences get skimmed,
   guards fail the build.

## Nightly tests

`.github/workflows/nightly.yml` runs every night (~03:17 Berlin time) and on
demand (Actions → Nightly → Run workflow): guards, C# build, all engine
tests, Python client tests, the automation scenario with a real renderer and
a screenshot, and the locale screenshots. Screenshots and the automation
recording are kept as workflow artifacts. If anything fails, the workflow
opens an issue labelled `nightly-failure` (or comments on the open one); the
director starts the day by turning it into a fix brief. PRs still get the
fast CI check (`.github/workflows/ci.yml`) so nothing broken is merged.

## Art and sound

| What | Tool | Where it goes | Gate before it ships |
|---|---|---|---|
| Sprites, animations, tiles | PixelLab | `assets/sprites/<area>/` | Meets `docs/ART_IMPLEMENTATION_GUIDE.md` (grid, palette, proportions, naming, import); scored with the rubric from in-engine screenshots |
| Music | Suno | `assets/audio/music/*.ogg` | Loops cleanly; loudness consistent with other tracks |
| Voices | ElevenLabs | `assets/audio/vo/<locale>/<voice_id>.ogg` | Matches the line's translation key text in that locale; one voice per character |

- Every generated file gets a row in [`assets/PROVENANCE.md`](../assets/PROVENANCE.md)
  (file, tool, date, prompt summary, plan/license, approved by) in the same
  PR that adds it. Check the tool's current terms for commercial use; a file
  whose rights are unclear is not committed.
- Briefs that need assets say which ones, and who generates them (usually the
  owner, since the tools are used through their accounts). Workers integrate
  delivered files; they don't invent placeholder "final" assets.

## Environments

- **Claude Code (cloud)**: `.claude/settings.json` runs
  `.claude/hooks/session-start.sh`, which installs Godot .NET + .NET 8 via
  `tools/setup/install_toolchain.sh` and exports `GODOT`.
- **Codex**: set the environment's setup script to
  `bash tools/setup/install_toolchain.sh` and add the printed `GODOT`,
  `DOTNET_ROOT`, and `PATH` as environment variables. Codex reads
  [`AGENTS.md`](../AGENTS.md) at the repository root.
- **Owner's machine**: install the Godot 4.6 **.NET** editor and the .NET 8
  SDK, open `project.godot`, press Play (F5). To play a PR: check out its
  branch (GitHub Desktop → Branch), then press Play.

---
name: Worker brief
about: One task for an AI worker (written by the director, see docs/AI_WORKFLOW.md)
title: "[Brief] "
labels: brief
---

## Goal (player's view)
<!-- One outcome the owner can see or feel. Plain language. -->

## Design rule
<!-- Section of docs/game-design/mechanics-and-core-loop.md this implements; quote it.
     No section? The owner's decision, quoted with date. -->

## Scope
**In:**
-

**Out (do not build):**
-

## Where it lives
<!-- Feature folder, scenes, resources, signals expected. -->

## Tests required
- [ ] Unit:
- [ ] Integration:

## Docs to update
- [ ]

## How to play it (owner's playtest script)
1.
2.
**Expected:**

## Definition of done
- [ ] `python3 tools/guards/check_rules.py` clean
- [ ] `tests/run_tests.sh` green
- [ ] Player text in `en.po` and every launch locale
- [ ] New assets listed in `assets/PROVENANCE.md`
- [ ] PR template filled in, `Closes #<this issue>`

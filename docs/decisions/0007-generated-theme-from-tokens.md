# 0007 — Theme generated from design tokens
Status: Accepted
Date: 2026-10-01

## Context
The UI needs a consistent, art-directed look and states for every control;
hand-editing a large Theme drifts and is hard to review.

## Decision
`UiTokens` resource → `ThemeBuilder` → committed `utopia_theme.tres` (project
theme). A test fails when the committed theme differs from the tokens.
Contrast of token pairs is tested.

## Consequences
Visual changes are small token diffs. Editor-only theme tweaks are lost on
rebuild — put them in the builder.

## Alternatives considered
Hand-authored Theme in the editor; per-scene overrides (inconsistent).

# 0001 — Composition, scene-owned UI stacks, five autoloads
Status: Accepted
Date: 2026-10-01

## Context
The game will grow many features (combat, base, scouting, dialogue). Large
"manager" scripts and global UI singletons become bottlenecks quickly.

## Decision
- Scenes are composition roots wiring small components via exported
  references and signals.
- Exactly five autoloads: `Settings`, `InputMethod`, `SaveService`,
  `GameSession`, `SceneRouter` — each justified in ARCHITECTURE.md.
- No UI autoload: each routed scene owns a `ScreenStack`.

## Consequences
Features are testable in isolation; scenes stay small. Cross-feature wiring
happens in scenes, which must be kept thin. New globals need an ADR.

## Alternatives considered
Global event bus (hard to trace, easy to abuse); UI manager autoload
(couples every screen to one script); GameManager singleton.

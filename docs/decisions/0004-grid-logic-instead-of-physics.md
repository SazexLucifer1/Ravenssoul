# 0004 — Grid logic instead of physics for gameplay collision
Status: Accepted
Date: 2026-10-01

## Context
Turn-based tactics on 12–16 tile maps. Outcomes must be deterministic,
replayable, and independent of frame rate (also for scouting's "see the next
move" intel).

## Decision
Movement legality, occupancy, ranges, and hits are integer cell logic in
`BattleGrid`/`BattleMapData`. Units are `Node2D`, not `CharacterBody2D`.
Animation and tweens are visual only.

## Consequences
No tunneling or physics jitter by construction; trivial to unit-test.
Physics-style effects (knockback arcs, debris) are cosmetic only.

## Alternatives considered
CharacterBody2D + Area2D hurtboxes (nondeterministic for a turn-based game,
more tests for less value); TileMapLayer physics layers (same issue).

# 0010 — Isometric battles, 640×360 pixel art frame, integer scaling
Status: Accepted
Date: 2026-10-01

## Context
The approved art direction (docs/ART_DIRECTION.md) requires a tactical
battlefield in the style of the Metal Slug Tactics reference and handcrafted
pixel art that must stay crisp at every supported resolution. The prototype
used a top-down square grid drawn at 48 px cells in 1280×720 space.

## Decision
- Battles use a 2:1 isometric projection. `BattleGrid.cell_to_local` /
  `local_to_cell` are the only projection code; tile footprint 32×16 art px,
  elevation step 8 px. Units are y-sorted.
- World art is authored in a 640×360 art frame. The UI keeps the 1280×720
  logical canvas (crisp text); the gameplay camera uses zoom 2, so the world
  scales ×2/×3/×4/×6 at 720p/1080p/1440p/4K.
- Project defaults: nearest-neighbour texture filtering, pixel-snapped 2D
  transforms and vertices.
- Logical cells remain the source of truth (ADR 0004 unchanged).

## Consequences
Sprites need four facings (two drawn, two mirrored) and readable elevation.
1366×768 and other non-multiples scale by a non-integer factor (slight pixel
unevenness, documented). Tests enforce projection, round-trip picking,
integer zoom, filtering, and snapping.

## Alternatives considered
Top-down square grid (cheaper art, weaker match to the reference); a
640×360 SubViewport for the whole game (pixelated UI text); Godot TileMapLayer
in isometric mode (adopt later for authored maps — must keep the same tile
size and projection).

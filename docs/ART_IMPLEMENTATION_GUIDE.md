# Art implementation guide

Production rules that keep every asset consistent with
[ART_DIRECTION.md](ART_DIRECTION.md). Quality is judged with
[VISUAL_QUALITY_RUBRIC.md](VISUAL_QUALITY_RUBRIC.md); motion requirements
live in [ANIMATION_AND_MOVEMENT.md](ANIMATION_AND_MOVEMENT.md); UI art in
[UI_STYLE_GUIDE.md](UI_STYLE_GUIDE.md). Engine facts below are enforced by
tests in `tests/unit/art/` and `tests/unit/combat/test_battle_grid.gd`.

## 1. Pixel grid and scale

| Rule | Value |
|---|---|
| Art frame | 640×360 art pixels (gameplay camera zoom 2 on the 1280×720 canvas) |
| Display scale | ×2 (720p), ×3 (1080p), ×4 (1440p), ×6 (4K); other sizes non-integer (avoid in marketing captures) |
| Isometric tile | 32×16 px top diamond (2:1); elevation step 8 px |
| Character canvas | 32×48 px; standing figure 28–36 px tall; feet centre on the tile centre |
| Battalion troop mini | 16×24 px canvas (3 minis around the hero) |
| Large creatures/bosses | 64×64 or 64×96 canvas; footprint 2×2 tiles |
| Portraits | 64×64 (HUD/dialogue), 128×128 (recruit/tavern detail) |
| Icons | 16×16 (inline), 24×24 (buttons/HUD) |
| UI frames | 9-slice at art scale, displayed ×2 in the 1280×720 UI |
| Filtering | Nearest (project default); never enable mipmaps/filter on pixel art |
| Transforms | Integer positions, no rotation or non-integer scale of sprites (snapping is on) |

## 2. Proportions and shape language

- Heroes ~1:2.5 head-to-body, big eyes (2–3 px), readable hands as 2–3 px clusters.
- Rebels/allies: asymmetric, patched, warm materials, angular cloth.
- Empire (utopia): symmetric, clean geometric armour, cold whites and gold, few colours.
- Races (from R1): goblin = small/green/large ears, orc = broad/green/tusks, elf = tall/pale/long ears, drow = dark skin/white hair, tiefling = red skin/horns. Never rely on colour alone — each needs a silhouette cue.
- Detail hierarchy: silhouette > face/weapon > clothing folds > trim. Tertiary detail ≤ 20 % of sprite area.

## 3. Palette and materials

Master palette (extend only with art-director sign-off; ≤ 64 colours total,
≤ 24 per sprite):

| Ramp | Colours (dark → light) | Use |
|---|---|---|
| Night | `#16100c` `#2b1e16` `#3c2a1e` | backgrounds, deep shadow, UI surfaces |
| Wood | `#4a3020` `#6e4a2c` `#9a6a3c` `#c8955a` | furniture, frames, rebels' gear |
| Brass | `#5c4524` `#9a7a44` `#d4b06a` | trims, UI borders, buckles |
| Parchment | `#c9b391` `#e6d3ad` `#f1e4c8` | text, maps, scrolls |
| Stone | `#3a3129` `#4a4036` `#6f6253` `#9a8c78` | walls, blocks, floors |
| Earth/grass | `#3f3d27` `#545335` `#5b5a3a` `#7d7a4a` | battlefield ground |
| Ember | `#6a2a1c` `#8f3423` `#b8452f` `#d8742f` `#f2c14e` | fire, hazards, revolution accents, primary buttons, focus |
| Empire | `#d9dde3` `#aab4c2` `#e8d27a` | utopia architecture and troops |
| Soul (reserved) | `#24504c` `#3f8f88` `#5fc9c0` `#b8f2ea` | soul energy ONLY — never for UI decoration |
| Semantic UI | danger `#ee7366`, success `#8fbf74`, move `#8cc4ff` | states (see UI style guide) |

Rules: outlines are the darkest shade of the local ramp (never pure black);
skin ramps per race with 3–4 shades; materials are differentiated by
highlight shape (wood = soft, brass = hard specular pixel, cloth = none).

## 4. Lighting

- Key light from the top-left for all sprites and tiles; shadow side bottom-right.
- Contact shadow: flattened ellipse, 35 % black, under every unit/prop (placeholder already does this).
- Local light pools (torches, embers, soul energy) as additive light sprites with palette-matched colours; no unsourced glows.
- Mood per area: tavern warm/candle; battlefield dusk-warm with dark edges; empire spaces cold and bright; corruption desaturates warm ramps toward soul teal (drive with a CanvasModulate/shader parameter, not repainted assets).

## 5. Naming, files, and import

```
art_source/                      Aseprite sources (not imported; add .gdignore)
  units/<unit_id>/<unit_id>.aseprite
features/characters/content/<unit_id>/
  <unit_id>_sheet.png            exported sprite sheet
  <unit_id>_frames.tres          SpriteFrames
features/combat/content/tiles/<tileset_id>.png / .tres
assets/ui/frames/ui_<element>_<state>.png
assets/ui/icons/icon_<name>_<size>.png
assets/vfx/vfx_<effect>.png
```

- Ids match content ids (`hero_deserter`), snake_case, no spaces.
- Animation names: `<clip>_<dir>` where dir ∈ `se`, `sw`, `ne`, `nw` (only `se` and `ne` drawn; `sw`/`nw` mirror via `flip_h`). Clips listed in ANIMATION_AND_MOVEMENT.md.
- Import: Texture2D, filter off (project default nearest), mipmaps off, compression **Lossless**, `process/fix_alpha_border` on.
- Export from Aseprite with `--sheet-type packed --trim` off (keep frame canvas fixed so feet stay anchored).
- Animation event markers (footstep, impact, release) are authored as Aseprite tags/slices and transferred to AnimationPlayer method tracks (`CharacterAnimator.emit_animation_event`).

## 6. Animation principles (summary)

12 fps base (8 fps idle, up to 15 fps for impacts); anticipation ≥ 2 frames
before contact; impact frame holds 2 frames (plus 1–2 frame hit-stop driven
by code); every clip returns to the idle pose's first frame; feet locked to
the tile during contact frames; four facings. Gameplay resolves first;
animation never decides outcomes. Full inventory: ANIMATION_AND_MOVEMENT.md.

## 7. Performance budgets (targets — not yet measured)

Target: 60 fps at 1080p on integrated GPUs (GL Compatibility), Windows/Linux.

| Item | Budget |
|---|---|
| Units on a battle map | ≤ 40 animated (hero + 3 minis = 4 sprites per unit) |
| Unit sprite sheet | ≤ 512×512 per unit (all clips, 2 drawn facings) |
| Tile set | ≤ 1024×1024 per biome |
| Total texture memory (battle) | ≤ 128 MB |
| Canvas draw calls (battle) | ≤ 400 (batching: one atlas per biome / per UI skin) |
| Particles on screen | ≤ 1 500 (CPUParticles2D) |
| Light sprites | ≤ 24 visible |
| Frame time | ≤ 16.6 ms; scripts ≤ 4 ms |

Record measurements here when profiling is done (`observe.perf` from the
automation protocol reports fps, frame times, node counts, memory).

## 8. Review process

1. **Brief:** reference the rubric categories the asset touches and the relevant style rules.
2. **Blockout:** silhouette sheet at 1× → black-fill test (C2) before colour.
3. **In-engine check:** import, place on the battle map, capture with the screenshot tool at 1280×720 and 1920×1080; review motion live (no review from the Aseprite canvas only).
4. **Score:** every touched category ≥ 7 using VISUAL_QUALITY_RUBRIC.md; list failures with the rubric's AI-failure checklist (pixel-size drift, identity drift, garbled text, unsourced light).
5. **Sign-off:** art direction owner approves; update the rubric's state matrix when a new state becomes evaluable.
6. **AI-assisted art** is allowed only as concept/ideation. Shipped pixel art must be cleaned to the grid, palette, and proportion rules by an artist and pass the same checks.

## 9. Troubleshooting

- Blurry sprites: texture filter overridden on the node or import; non-integer camera zoom; window size not a multiple of 640×360.
- Jitter while moving: sub-pixel positions — keep snapping on, tween integer targets (cell centres are integral).
- Sorting errors: sprite origin not at the feet; y-sort disabled on the parent.
- Wrong cell picked under the mouse: use `BattleGrid.local_to_cell`; never compute projection elsewhere.

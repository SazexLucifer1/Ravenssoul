# Visual quality rubric and AAA visual-readiness assessment

Scores visual presentation against the approved art direction
([ART_DIRECTION.md](ART_DIRECTION.md)) and benchmarks. **The readiness score
measures presentation only — it does not claim an AAA budget, team size,
content volume, or production scope.**

Assessment date: 2026-10-01 · Build: `claude/youthful-brahmagupta-0sgoje`
after the quick wins listed in "Applied in this pass" · Evidence: captures
from `tests/tools/screenshot_capture.tscn` (Xvfb, GL Compatibility, 1280×720)
and the supplied reference images R1–R4.

## How to use

1. Capture the representative state set (see "Visual-state matrix") at
   1280×720 and 1920×1080 with the screenshot tool; review motion live.
2. Score each applicable category 1–10 using the level descriptors; cite
   visible evidence. Mark anything without evidence **N/E** (not evaluated)
   and exclude it from weighted totals — never guess.
3. Run the one-second read on every critical frame: after ~1 s, can a
   representative player name the focal subject, current state, immediate
   threat/opportunity, available action, and likely consequence?
4. New assets must reach **≥ 7** in every category they touch before merge
   (see ART_IMPLEMENTATION_GUIDE.md "Review process").

Weighted score = Σ(score × weight) / Σ(weight of evaluated categories).

## Rubric

Each category: what it measures · level descriptors (1 / 5 / 8 / 10) ·
common AI-generated failure modes · objective checks · weight.

### C1. Focal read and concept clarity — weight 6
Measures: whether each frame has one clear subject and the fantasy reads instantly.
- **1** No focal point; everything equal weight; genre unclear.
- **5** Subject findable after searching; fantasy generic.
- **8** Subject found in < 1 s; the "revolution vs utopia" fantasy is legible.
- **10** Eye path is authored (subject → action → consequence) in every state.
- AI failures: decorative clutter with no hierarchy; random focal glows; mood images with no gameplay subject.
- Checks: blur test (8 px Gaussian) — focal subject still the highest-contrast mass; one-second read passes.

### C2. Silhouette and character identity — weight 10
Measures: heroes, battalions, races, enemies recognisable by shape/hue alone.
- **1** Placeholder primitives or interchangeable bodies.
- **5** Distinct by colour only; silhouettes merge.
- **8** Each race/class distinct as a solid black silhouette at gameplay scale; hero vs battalion obvious.
- **10** Identity survives flipping, team tint, and status overlays; portraits match sprites exactly.
- AI failures: same body with re-coloured clothes; accessories that change between frames; fused limbs; inconsistent faces between portrait and sprite.
- Checks: fill sprites black at 1× — a tester names ≥ 90 % correctly; hue-shift test; facing flip test.

### C3. Pixel craft and grid discipline — weight 10 (medium-specific)
Measures: one pixel grid, clean clusters, outlines, palette discipline.
- **1** Mixed resolutions, blur, anti-aliased painting, sub-pixel jitter.
- **5** Mostly on grid; jaggies, pillow shading, noisy clusters.
- **8** One pixel density, deliberate clusters, coloured outlines, consistent ramps.
- **10** Selective outlining, manual AA only where intended, every pixel purposeful.
- AI failures: "pixel-ish" upscaled paintings, inconsistent pixel size within one image, orphan pixels, smeared dithering, garbled micro-text (R3).
- Checks: zoom 800 % — pixel size constant; colour count per sprite ≤ 24; nearest filtering and integer camera zoom (test-enforced); no rotation/scale of sprites except integer.

### C4. Proportion, form language, detail hierarchy — weight 4
Measures: ~1:2.5 proportions, shape language (rebels angular-warm, empire clean-geometric), primary/secondary/tertiary detail.
- **1** No consistent proportions or shapes. **5** Consistent proportions, detail evenly noisy. **8** Clear large/medium/small detail hierarchy, faction shape languages. **10** Every element reinforces faction and role.
- AI failures: anatomical drift between frames; over-detailed noise at 1×.
- Checks: proportion overlay sheet; detail-density map (tertiary detail ≤ 20 % of area).

### C5. Colour, value, and mood — weight 8
Measures: warm-with-darker-undertones palette, value separation, semantic colour (soul teal reserved).
- **1** Arbitrary colours, value mush. **5** Coherent palette, weak value separation. **8** Characters separate from ground in greyscale; warm/dark mood; teal only for soul energy. **10** Colour scripts per area and story beat; corruption visibly shifts palette.
- AI failures: oversaturated rainbow palettes; random teal/blue glows that steal soul-energy meaning.
- Checks: greyscale capture — unit vs floor contrast ≥ 3:1; palette audit against the master palette; UI text contrast ≥ 4.5:1 (test-enforced).

### C6. Isometric environment and world storytelling — weight 8
Measures: tiles, elevation, props, landmarks, density, functional spaces, story clues.
- **1** Flat grid, no props. **5** Tiles + generic props, repetitive. **8** Readable elevation, cover, landmarks, varied but tidy density. **10** Every map tells a story and guides the eye to objectives.
- AI failures: perspective drift (R2), props on wrong grid, impossible geometry, clutter hiding walkable tiles.
- Checks: walkable vs blocked readable in greyscale; ≥ 1 landmark per screen; props snap to 32×16 grid; repeated-tile pattern not visible at 1×.

### C7. Lighting and atmosphere — weight 4
Measures: consistent top-left key light, contact shadows, glows, time-of-day mood.
- **1** No light logic. **5** Consistent shading, flat scene. **8** Contact shadows, local light pools (torches, embers), atmospheric depth. **10** Lighting drives focus and mood per beat.
- AI failures: light direction differs per object; glows with no source.
- Checks: light-direction overlay; every unit has a contact shadow.

### C8. Animation and locomotion — weight 10
Measures: idle, walk, starts/stops, turns, actions, hits, defeat, battalion motion (see ANIMATION_AND_MOVEMENT.md).
- **1** Sliding or tweened static images. **5** Basic cycles, no anticipation/impact. **8** Readable anticipation → contact → recovery, 4 facings, grounded steps. **10** Personality per hero/race, battalion follow-through, perfect sync with audio/VFX.
- AI failures: frame-to-frame identity drift, jittering outlines, inconsistent limb counts.
- Checks: frame-by-frame scrub; feet on tile during contact frames; timing sheet within ±1 frame of spec.

### C9. VFX and impact readability — weight 5
Measures: telegraphs, trajectories, impacts, damage/heal/status feedback without obscuring play.
- **1** None. **5** Effects present but noisy or late. **8** Impact on the contact frame, clear ownership colour, never hides tiles or text. **10** Effects carry precise information (hit, crit, block) at a glance.
- AI failures: particle soup, effects in random palettes.
- Checks: effect never covers HUD numbers > 150 ms; colour = ownership/semantic.

### C10. Tactical state readability — weight 12
Measures: active unit/turn, move range, path, threat, targets, AP, hand, previews, objective, health, morale, initiative.
- **1** Player can't tell what they can do. **5** Some states shown, others implied. **8** All decision-relevant states visible with colour + shape; damage previews. **10** Zero mental arithmetic; consequences previewed before confirming.
- AI failures: decorative overlays with no meaning; inconsistent overlay colours.
- Checks: one-second read on combat frames; colour-blind simulation (deuteranopia) keeps states distinct.

### C11. Game-piece tactility (cards, unit tokens) — weight 5
Measures: card frames, hover lift, drag/aim, valid targets, play/discard motion, stacking.
- **1** Flat rectangles. **5** Framed cards, no motion language. **8** Lift, focus, drag ghost, valid-target highlight, return-to-rest. **10** Cards feel physical and every state is distinct.
- Checks: every card state (idle, focus, selected, unaffordable, disabled) distinct in greyscale.

### C12. HUD and menu UI presentation — weight 10
Measures: game-native UI per [UI_STYLE_GUIDE.md](UI_STYLE_GUIDE.md) (hierarchy, materials, icons, typography, states, focus).
- **1** Default engine widgets. **5** Consistent theme, generic flat panels, text-only. **8** In-world materials, pixel icons, clear hierarchy, all button states. **10** UI feels part of the world and never slows decisions.
- AI failures: garbled labels (R3), web-dashboard cards, inconsistent icon styles.
- Checks: UI rubric sub-scores (UI_STYLE_GUIDE.md); layout/focus tests (automated).

### C13. Composition and camera framing — weight 3
Measures: screen-space budget, negative space, HUD footprint, edge safety.
- **1** Important things cropped or tiny. **5** Centred but wasteful. **8** Board fills the safe frame; HUD in corners; no occlusion. **10** Camera reframes for action without losing context.
- Checks: board ≥ 55 % of frame width; HUD ≤ 20 % of area; nothing important within 32 px of edges.

### C14. Cross-layer cohesion — weight 3
Measures: characters, tiles, UI, icons, fonts, VFX share one grammar.
- **1** Each layer from a different game. **5** Palette shared, styles differ. **8** Same pixel density, outline logic, materials. **10** Indistinguishable authorship.
- Checks: side-by-side contact sheet of all layers.

### C15. Technical image integrity — weight 2
Measures: shimmer, blur, scaling, z-fighting/sorting, clipping, compression.
- **1** Blurry or unstable. **5** Mostly crisp, occasional sorting/scaling faults. **8** Pixel-perfect at target resolutions, correct y-sort. **10** Stable in motion at every supported resolution.
- Checks: captures at 720p/1080p/1440p; scrolling test; y-sort test with overlapping units.

Not applicable to this game: 3D modelling/deformation, materials/PBR,
facial performance capture and lip sync (dialogue uses portraits with
expression swaps — scored under C2/C8), vehicle motion, IK/physics.

## Scores: current game vs benchmark

Benchmark = Metal Slug Tactics (R4). Character/style categories also
cross-checked with R1. Unicorn Overlord and Triangle Strategy are **not
scored** (no footage provided).

| Cat | Weight | Current | Benchmark (R4) | Gap | Gap type | Realistic? |
|---|---:|---:|---:|---:|---|---|
| C1 Focal read | 6 | 3 | 8 | 5 | art direction / asset | Yes, with real sprites |
| C2 Silhouette & identity | 10 | 1 | 9 (R1: 9) | 8 | asset quality | Yes with a pixel artist; largest single gap |
| C3 Pixel craft | 10 | 1 | 9 (R1: 8) | 8 | asset quality | Specialist pixel artist required |
| C4 Proportion & detail | 4 | 1 | 9 (R1: 8) | 8 | asset quality | Yes |
| C5 Colour & mood | 8 | 4 | 8 (R1: 8) | 4 | art direction | Yes — palette defined, needs art |
| C6 Iso environment | 8 | 2 | 9 | 7 | asset quality | Partly — tile kit + props scope |
| C7 Lighting | 4 | 1 | 7 | 6 | lighting | Yes (palette lighting + light sprites) |
| C8 Animation | 10 | 2 | N/E | — | animation | Needs reference footage to score |
| C9 VFX | 5 | 1 | N/E | — | VFX | Needs footage |
| C10 Tactical readability | 12 | 4 | 9 | 5 | UI / camera | Yes — mostly engineering + UI art |
| C11 Game pieces | 5 | N/E | N/E | — | UI | Card system not built |
| C12 HUD & menus | 10 | 4 | 8 | 4 | UI | Yes |
| C13 Composition | 3 | 4 | 7 | 3 | camera / UI | Yes |
| C14 Cohesion | 3 | 3 | 9 | 6 | consistency | After art pass |
| C15 Technical integrity | 2 | 6 | 7 | 1 | technical | Yes |

### Evidence (facts vs interpretation)

- **C1** Fact: battle frame shows a blue 12 art-px circle, blue move diamonds, a gold objective diamond (`docs/art/annotated/battle_start_annotated.png`). Interpretation: the HUD frames are the brightest masses and compete with the unit. R4: the selected soldier, path line, and arcs form a clear path.
- **C2/C3/C4** Fact: units are code-drawn primitives (`UnitVisual._draw`); no sprite assets exist (`assets/sprites/` empty). R1/R4: distinct races and soldiers, coloured outlines, consistent pixel size.
- **C5** Fact: UI tokens and board use the warm-dark palette (dark wood `#2b1e16`, brass `#9a7a44`, parchment `#f1e4c8`, ember `#b8452f`); contrast tests pass. Interpretation: board olive and blocks read muddy; no colour scripting yet.
- **C6** Fact: 12×12 isometric diamond grid, raised blocks for walls, hatched hazards, objective marker; no props, landmarks, or elevation variation.
- **C7** Fact: only a contact shadow under the unit; no light sources.
- **C8** Fact (from code, `unit_animations.tres`): 1–2 px bob, colour-flash hit, fade defeat, tweened step; no frame animation, no facings beyond mirroring. Benchmark motion not supplied → N/E.
- **C9** Fact: no effects. Benchmark N/E (R4 arcs are UI overlays, counted under C10).
- **C10** Fact: move range highlighted (test-enforced), objective marker + text, hazard hatch, numeric health, blocked/hazard hints. Missing: turn/initiative, AP, card hand, threat ranges, enemy intent, damage previews (systems not built). R4: blue move tiles, path line, attack arcs, damage preview, turn order, objective, END TURN.
- **C12** Fact: themed panels and buttons with every state, gold focus ring, localized, safe areas, 150 % text, RTL (tests). Missing: pixel font, icons, framed wood/brass art, audio feedback; title screen 60 % empty (`main_menu_annotated.png`).
- **C13** Fact: board ≈ 60 % of frame width centred; HUD panels in corners; large unused margins.
- **C15** Fact: integer zoom ×2 (×3 at 1080p), nearest filtering and snapping on (tests); y-sorted units. Not verified: motion shimmer at 1366×768 (non-integer scale).

### Weighted totals and AAA visual-readiness

| | Evaluated weight | Weighted score |
|---|---:|---:|
| Current game, all evaluated categories (excl. C11) | 95 | **2.5 / 10** |
| Current game, categories also scored for benchmark | 80 | **2.7 / 10** |
| Benchmark (R4), same categories | 80 | **8.5 / 10** |

**AAA visual-readiness: 32 %** (2.7 ÷ 8.5 over comparable categories).
Animation, VFX, and game pieces are excluded for lack of benchmark footage or
built systems — they are the largest unknowns, not hidden strengths.

Interpretation: the systems that frame the art (UI theme and states, grid
readability rules, pixel pipeline, localization, accessibility) are in place;
the art itself is placeholder. The score will move mostly with C2, C3, C6,
C8 (sprites, tiles, animation) and C10 (tactical HUD once cards/enemies exist).

## Visual-state matrix

Scores per state on its most important categories (C1 focal, C10 state
readability, C12 UI, C14 cohesion). Captures: `docs/screenshots/`, regenerate
with the screenshot tool.

| State | Capture | Focal | State read | UI | Cohesion | State score | Notes |
|---|---|---:|---:|---:|---:|---:|---|
| Title screen | `en_main_menu.png` | 5 | 6 | 4 | 3 | 4.5 | Primary action clear; 60 % empty, no key art/logo |
| Settings modal | `en_settings.png` | 6 | 6 | 4 | 3 | 4.8 | Clear rows; generic controls look |
| Battle start (quiet) | `state_battle_start.png` | 3 | 4 | 4 | 3 | **3.5** | Unit tiny; no turn/AP/hand/threat |
| Battle, hazard hit | `en_gameplay.png` | 3 | 5 | 4 | 3 | 3.8 | Hint text clear; no on-unit damage number/VFX |
| Pause | `en_pause.png` | 6 | 7 | 4 | 3 | 5.0 | Good modality and focus |
| Victory + autosave | `en_victory.png` | 6 | 7 | 4 | 3 | 5.0 | Save state explicit; rewards text-only, no icons |
| Defeat | `state_defeat.png` | 6 | 6 | 4 | 3 | 4.8 | Unit not visible behind scrim; cause of defeat not shown |
| Confirm abandon | `state_confirm_abandon.png` | 6 | 7 | 4 | 3 | 5.0 | Safe default focused |
| Load failure | `state_load_failed.png` | 6 | 7 | 4 | 3 | 5.0 | Plain-language error (text fixed in this pass) |
| Pseudo-loc / long text | `pseudo_*.png` | — | — | 5 | — | — | Expansion fits (test-enforced) |
| RTL | `rtl_*.png` | — | — | 5 | — | — | Layout mirrors correctly |

**Weakest critical state:** battle (3.5) — the core loop frame.
**Largest communication failure:** tactical state is absent — no turn order,
AP, card hand, enemy threat, or previews — and the unit is ~12 art px tall
(target 32–48 px).

**Not evaluated (not provided or not built):** combat with enemies,
targeting / attack preview, enemy turn, card hand and AP, base/tavern hub,
recruit and hero+battalion pairing, world map, shop / building upgrade,
locked vs unlocked progression, affordable vs unaffordable choices, rewards
with icons, loading screen with art, close character presentation / dialogue
portraits, soul-energy decision. These are the core-loop states the art
pass must deliver; none should be claimed as passing.

### Annotated frames

- `docs/art/annotated/battle_start_annotated.png` — focal path 1 unit → 2 move range → 3 objective; competing HUD frames; missing tactical state; fix area.
- `docs/art/annotated/main_menu_annotated.png` — clear primary action; empty frame; fix (display font, emblem, key art).

Legend: green = intended focal path, yellow = competing focal points, red =
missing/unclear state, blue = highest-impact correction. Regenerate with
`tests/tools/annotate_frames.tscn` (see TESTING.md).

## Applied in this pass (quick wins already in the build)

| Change | Categories | Before → after (before = estimated from the previous captures) |
|---|---|---|
| Isometric grid with raised blocks, hatched hazards, objective marker (ADR 0010) | C6, C10, C14 | 1 → 2 (C6) |
| Blue move-range diamonds, hidden when the unit can't act | C10 | 3 → 4 |
| Warm-dark UI palette (wood/brass/parchment), 1 px corners | C5, C12, C14 | 3 → 4 (C5) |
| Result titles keep heading size with semantic colour | C12 | bug fixed |
| Damaged-save message no longer promises a future feature | C12 (player-readable) | fixed |
| Pixel pipeline: nearest filtering, snapping, integer camera zoom, y-sort, contact shadow | C3 pipeline, C15 | 5 → 6 (C15) |

## Prioritised improvement plan

### Quick wins (engineering, days)
1. Turn/initiative bar + active-unit ring (C10).
2. Floating damage/heal numbers and a hit flash on the unit (C9, C10).
3. Path preview line (green, Metal Slug Tactics style) for multi-step moves (C10).
4. Defeat screen: keep the unit visible (scrim with a cut-out or small portrait) and name the cause (C10, C12).
5. Board vignette / warm light pool, lower HUD frame brightness (C1, C7).
6. Placeholder icon set (resources, AP, health) at 16×16 in the UI style (C12).

### High-impact revisions (art + engineering, weeks)
1. Card hand + AP bar + targeting preview — designed per UI_STYLE_GUIDE.md (C10, C11, C12).
2. Isometric tile kit (ground variants, walls with height, cover props, hazard animation) and one landmark per map (C6, C7).
3. Title screen key art + logo; tavern hub scene (C1, C12, C13).
4. Battalion formation presentation (hero + troop minis) (C2, C8).

### Requires a professional artist or specialist
1. Pixel artist: master palette sign-off, hero/battalion/enemy sprite sheets (4 facings × clip list), portraits (C2–C4, C8).
2. Pixel animator: locomotion, attack/hit/defeat sets with event markers (C8, C9).
3. UI artist: wood/brass/parchment 9-slice frames, pixel display font licensing or custom font, icon set (C12).
4. VFX artist: soul-energy and impact effects in the palette (C9).
5. Unknowns to resolve: upload Unicorn Overlord / Triangle Strategy / Sea of Stars footage to score C8/C9 and refine the bar.

## Three.js rendering profiles

Not applicable: the game is Godot 4.6 (GL Compatibility) for Windows and
Linux desktop. Desktop performance budgets live in ART_IMPLEMENTATION_GUIDE.md.

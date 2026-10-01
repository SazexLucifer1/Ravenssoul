# Game UI style guide

How menus, HUD, cards, and buttons must look and behave. Architecture
(screens, stacks, focus code) is in [UI_ARCHITECTURE.md](UI_ARCHITECTURE.md);
tokens live in `core/ui/theme/ui_tokens.tres` and generate the theme.
Art direction: [ART_DIRECTION.md](ART_DIRECTION.md).

## Principles

1. **One-second read:** on every screen a player can tell where they are, what they can do, what matters next, whether they progress, and whether they are in danger.
2. **The UI is part of the world:** dark wood panels, brass trims, parchment text surfaces, wax-seal/ember accents. Not flat web cards.
3. **One primary action** (ember `PrimaryButton`), secondary actions in wood, destructive actions in `DangerButton` with the safe choice focused.
4. **Controller first:** everything reachable by D-pad/stick, `ui_cancel` always backs out, focus is always visible (gold ring).
5. **Show, don't calculate:** costs, gains, and consequences are shown as before → after, never left to mental arithmetic.

## Materials and shapes (target)

| Element | Target art | Current (placeholder) |
|---|---|---|
| Screen panel | Dark wood 9-slice, 2 px brass bevel, corner rivets | Flat `#2b1e16`, 2 px brass border, 1 px corners |
| HUD panel | Darker wood, 90 % opacity, brass top edge | Flat 90 % surface |
| Primary button | Ember lacquer plate with brass rim | Flat ember fill |
| Secondary button | Wood plate | Flat raised wood colour |
| Text surface (cards, scrolls) | Parchment with ink text | Not built |
| Focus | 3 px gold ring outside the control + subtle glint | 3 px gold ring |

## HUD information priority (battle)

1. **Active unit & turn** — turn bar top-centre (portraits in initiative order, active enlarged).
2. **Card hand + AP** — bottom-centre; AP as ember pips; unaffordable cards dimmed with a lock.
3. **Selected unit panel** — top-left: portrait, name, health (number + bar), morale, status icons.
4. **Objective** — top-right, one line + progress; expands on focus.
5. **Contextual prompts** — bottom-left glyph + verb; only actions valid now.
6. **Board overlays** — move (blue `#8cc4ff`), path (green), threat (orange/red), target (gold ring), damage preview numbers on the target.

Today items 3, 4, 5 and the move overlay exist; 1, 2, threat, target, and previews are not built.

## Typography

| Role | Size (1280×720 logical) | Font |
|---|---|---|
| Title | 56 | Pixel display font (to license/commission), ×2 integer |
| Heading | 30 | Display font |
| Body / buttons | 20 | Readable pixel sans (min 16) |
| Small / captions | 16 | Same, never below 16 |

Currently the engine default font (smooth sans) — a placeholder that breaks
cohesion (rubric C14). The final font must cover the launch locales' glyphs
(test-enforced) and have fallbacks for future locales.

## Spacing and layout

Spacing tokens 4 / 8 / 16 / 24 / 40; panel padding 24; screen margin 32
(safe area added on top). Layouts are containers, never absolute positions.
Grids: card grids snap to 8 px; comparison views put current and candidate
side by side with differences highlighted (green better, danger worse, plus ▲/▼ icons).

## Icon language

16×16 and 24×24 pixel icons, 1 px coloured outline, top-left light, palette
ramps only. Every ambiguous icon is paired with text. Resources: wood log,
stone block, bread loaf, gold coin, soul flame (soul teal — reserved).
States: lock (locked), check (done), hourglass (cooldown), exclamation (new).

## Semantic colours

| Meaning | Colour | Never use for |
|---|---|---|
| Primary action / revolution | ember `#b8452f` | errors |
| Focus | gold `#f2c14e` | rewards text |
| Danger / damage / invalid | `#ee7366` | decoration |
| Success / heal / valid | `#8fbf74` | — |
| Move range / info | `#8cc4ff` | soul energy |
| Soul energy | teal `#5fc9c0` | anything else |
Colour always paired with shape, icon, or text (colour-blind safe).

## Cards (core loop)

Card 96×144 logical (48×72 art ×2): cost gem top-left, name banner, art window,
rules text on parchment, kind icon (move/attack/defend/buff/debuff/special).
States: idle, focused (lift 12 px, scale 1.0 — no blur), selected (gold rim),
unaffordable (desaturated + lock + red cost), disabled, dragging (ghost +
target line), played (fly to target), discarded. Hand fans with ≤ 8° spread;
focus moves left/right; `ui_cancel` returns a dragged card to rest.

## Buttons

Minimum 52 px tall (touch-safe 44+), 200–320 px wide; text centred, icon left.

### Required states — current review

| State | Spec | Current implementation | Verdict |
|---|---|---|---|
| Idle | Wood/ember plate | Theme stylebox | ✓ (flat placeholder) |
| Hover | Lighter plate, moves focus | Theme + focus-on-hover | ✓ |
| Keyboard/controller focus | Gold ring, distinct from hover | 3 px gold ring | ✓ |
| Pressed | Darker, 0.96 scale for 0.1 s | Theme + scale tween | ✓ |
| Disabled | 45 % plate, muted text, reason tooltip | Theme; no reason text | Partial |
| Selected | Gold rim / inset | Pressed stylebox reused | Partial — not distinct from pressed |
| Busy / loading | Label swap + spinner | Label swap ("Saving…") | Partial — no spinner |
| Error | Danger tint + shake (reduced motion: tint) | Implemented | ✓ |
| Cooldown | Draining overlay + seconds | Overlay only | Partial — no number |
| Audio feedback | Click / confirm / back / error sounds | None | ✗ |

## Motion and feedback

Transitions 150–300 ms (screen fade 200 ms); press feedback 100 ms; card lift
120 ms; no decorative delays before input is accepted. Reduced motion
removes shakes, slides, and scaling but keeps colour feedback. Every
confirm/cancel/error has a sound (not yet implemented).

## Safe area, responsiveness, accessibility, localization

As implemented in UI_ARCHITECTURE.md: safe-area container, 7 tested
resolutions, 100/125/150 % text, reduced motion, ≥ 4.5:1 text contrast,
RTL mirroring, pseudo-loc expansion tests, no display strings in scenes.

## The UI must never look like

- Default Godot/browser controls, grey system checkboxes, or OS dropdowns.
- Web dashboard cards with drop shadows and rounded 12 px corners.
- Tiny web-sized buttons (< 44 px) or rows of equally weighted actions.
- Walls of text where an icon + number would do.
- Mixed icon styles (line icons next to pixel icons).
- Arbitrary colour (teal decoration, red for non-danger).
- Focus that disappears, traps, or jumps unpredictably.
- AI artefacts: garbled labels, nonsense words, inconsistent pixel size (reference R3).
- Developer text: ids, enum names, raw errors, file paths, placeholders.

## UI rubric (scores, same scale as VISUAL_QUALITY_RUBRIC.md C12)

| Sub-category | Current | Benchmark R4 | Evidence / gap |
|---|---:|---:|---|
| Hierarchy & primary action | 6 | 8 | Clear primary buttons; R4's END TURN more prominent and contextual |
| Game-native materials | 2 | 8 | Flat colour panels vs illustrated frames |
| Icons | 1 | 8 | None vs weapon/status/portrait icons |
| Typography | 3 | 7 | Smooth default font vs pixel font (R4 text small at capture size) |
| Semantic colour & states | 6 | 8 | Tokens + states; selected/busy incomplete |
| Controller focus & navigation | 8 | N/E | Tested focus chains, gold ring, cancel everywhere |
| Localization & accessibility | 8 | N/E | Tested; benchmark not checkable from a still |
| HUD information completeness | 3 | 9 | Health/objective only vs portraits, turn, abilities, objective, end turn |
| Feedback (motion/audio) | 4 | N/E | Motion yes, audio none |
| **C12 roll-up** | **4** | **8** | Comparable rows average 3.5 vs 8.0; rounded up to 4 for the tested navigation/accessibility strengths that the benchmark still cannot be checked on |

Gap type: UI art (materials, icons, font) and missing HUD systems; the
interaction architecture is ahead of the art.

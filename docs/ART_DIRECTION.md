# Art direction

Status: **confirmed** (2026-10-01) — target statement and the three
direction decisions approved by the project owner. The AAA comparison set is
**provisional** (proposed, not objected to; see "Benchmarks").

Related: [VISUAL_QUALITY_RUBRIC.md](VISUAL_QUALITY_RUBRIC.md) (scoring),
[ART_IMPLEMENTATION_GUIDE.md](ART_IMPLEMENTATION_GUIDE.md) (production rules),
[UI_STYLE_GUIDE.md](UI_STYLE_GUIDE.md), [ANIMATION_AND_MOVEMENT.md](ANIMATION_AND_MOVEMENT.md),
ADR [0010](decisions/0010-isometric-battles-and-pixel-scale.md).

## Target art-direction statement

> Handcrafted pixel art on a single pixel grid with integer scaling. High
> top-down 3/4 view for the base/tavern and world map; **isometric** tactical
> battlefield. Chunky characters (~1:2.5 head-to-body), coloured outlines,
> big expressive eyes; every hero, battalion, and enemy race readable by
> silhouette and dominant hue alone. **Warm with darker undertones:** allies
> and interiors are warm (wood, candlelight, brass, parchment) but sit in deep,
> low-key shadow; the empire's utopia is colder and too clean. Soul energy is
> the only cold teal/violet glow and visibly corrupts the warm palette as it
> is spent. Battle readability follows Metal Slug Tactics — bold colour-coded
> overlays for move / path / threat / target, damage previews, clear turn
> ownership. UI is made of in-world materials (dark wood, brass, parchment)
> with chunky pixel icons, large controller-friendly buttons, and one obvious
> primary action per screen.

The utopia-vs-corruption colour logic is an art-direction interpretation of
the story pillars, not something visible in the references.

## Decisions

| Decision | Choice | Consequence |
|---|---|---|
| Battle camera | **Isometric** (2:1 diamond grid) | `BattleGrid` projects cells isometrically; sprites need 4 facing directions (2 drawn + mirrored); blocks/elevation read as volumes; higher art cost than top-down (ADR 0010) |
| Pixel scale | **640×360 art frame, integer scaling**; iso tile 32×16 px; characters ~32×48 px canvas | Gameplay camera zoom = 2 on the 1280×720 canvas → ×2 at 720p, ×3 at 1080p, ×4 at 1440p, ×6 at 4K |
| Mood | **Warm with darker undertones** | UI tokens retuned to dark wood / brass / parchment; world palette low-key warm with ember accents; soul teal reserved |

## Reference analysis

Reference images were supplied in conversation and are **not stored in the
repository** (third-party copyright). Descriptions below are the record.

| # | Reference | Use it for | Keep | Do not copy |
|---|---|---|---|---|
| R1 | Pixel-art fantasy tavern, high 3/4 top-down | Base / tavern hub, recruiting and pairing heroes with battalions | Chunky characters, coloured (non-black) outlines, race identity via silhouette + hue (goblin, orc, elf, drow, tiefling), warm wood/candle vs cool stone, ambient particles (mug sparks) | — (hand-made quality; primary character bar) |
| R2 | Painted village/world map, straight top-down | World map / mission select | Path-led composition, landmarks (lake, big halls), forest framing, settlement clusters | Soft anti-aliased painting (not pixel art), drifting perspective — very likely AI-generated; mood/layout only |
| R3 | "ELDORADO" isometric city builder + side-panel UI | Base management UI mood | Dark wood + gold frames, minimap, resource bar, chunky command buttons | Garbled labels ("Managem", "Porthvay", "MAMA"), solar panels on medieval roofs, mixed pixel density — almost certainly AI-generated; never a quality bar |
| R4 | Metal Slug Tactics (Leikir / Dotemu) | Tactical battle presentation | Colour-coded overlays (blue move tiles, green path line, orange attack arcs), damage preview numbers, portrait cards with HP, objective panel top-right, prominent END TURN | Desert/military theme; HUD density at small sizes |

**Shared language:** hand-made pixel art, coloured outlines, warm saturated
palettes, high camera angle, chunky silhouettes, dark wood/metal UI framing.

**Differences / conflicts resolved:** perspective (top-down R1/R2 vs isometric
R3/R4 → isometric battles, 3/4 top-down hub and map — the two never share a
screen); pixel density (R2 painted, R3 inconsistent → one grid, integer
scale only); tone (references cosy → warm with darker undertones).

## Benchmarks (AAA comparison set — provisional)

"AAA" here means presentation quality at comparable genre and camera
distance, not budget or scope.

| Benchmark | Why comparable | Evidence available | Scored? |
|---|---|---|---|
| Metal Slug Tactics | Isometric pixel-art tactics, same camera distance and medium | R4 screenshot (628×376, compressed) | **Yes** — primary scored benchmark |
| R1 tavern | Same medium, hub/character bar | R1 screenshot | Character/style categories only |
| Unicorn Overlord | 2D tactics, units composed of several characters (matches hero + battalion) | none uploaded | **Not scored** — needs footage |
| Triangle Strategy | High-angle tactical grid with pixel characters; targeting UI bar | none uploaded | **Not scored** — needs footage |
| Sea of Stars (optional) | Pixel animation and lighting bar | none uploaded | Not scored |

Fire Emblem: Three Houses is deliberately excluded (3D; different art problems).
Upload footage of the unscored benchmarks to add them to the comparison.

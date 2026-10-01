# Utopia — Mechanics and Core Loop Blueprint

> Status: approved blueprint. This is the design source of truth for
> mechanics. Implementation status lives in `docs/ARCHITECTURE.md`
> ("Blueprint coverage"); keep the two in sync rather than editing approved
> decisions here without a design review.

## Game descriptor

A tactical single-player RPG where a former soldier uncovers a kingdom's dark secret and builds a revolutionary army to overthrow it. Gameplay mixes deck-driven tactical battles on Fire-Emblem-sized maps, freely combinable hero + battalion units, resource-driven base building, scouting-based enemy intel, and a soul energy system that grants power but decides whether the player liberates the realm or seizes it as a corrupted tyrant.

## Core fantasy

You are a disillusioned soldier-turned-revolutionary who outsmarts a powerful empire: you pair heroes with battalions to forge unique units, win tactical fights with their combined card decks, grow your base and your scouting network until you can read the enemy's every move, and choose how far you let the lure of soul energy carry you — toward a true liberation or toward becoming the next tyrant.

## Core verbs

Combine → Deploy → Resolve → Upgrade

## Design pillars

- **Tactical, Card-Driven Combat:** All combat actions (move, attack, defend, special abilities) are resolved through cards paid with AP. Every hero and battalion has its own unique cards that fit its identity, so fights are readable, repeatable, and balanceable.
- **Units as Combinations:** Heroes (5-card deck) and battalions (15-card deck) can be paired freely. Their stats and decks combine into one battlefield unit, so the same roster produces many different tactical options.
- **Visible Soul Energy Trade-off:** Soul energy unlocks new and stronger units, but every use is tracked and steers the story — toward overthrowing the utopia for the greater good, or toward corruption and the player subjugating the realm themselves.
- **Knowledge Is Power:** Scouting turns enemy behaviour from hidden to fully readable over time (deck → current hand → next move), rewarding long-term investment with tactical clarity.

## Mechanics

- **Heroes & Battalions:** Heroes carry a small 5-card deck; battalions carry a 15-card deck. Before a mission the player pairs any hero with any battalion. The resulting unit uses the combined 20-card deck and the combined stats of both. Decision: which hero to pair with which battalion to cover the needed roles (damage, defense, mobility, support).
- **Combined Stats:** Every hero and battalion has stats that are added together per unit. They determine physical damage, magical damage, movement, card draw, max hand size, max AP per round, morale, and speed. Decision: build units around specialisation (e.g. high AP + draw) or balance.
- **Cards, AP & Hand:** At the start of combat each unit draws cards up to its draw value (capped by max hand size). Cards cost AP and have unique effects that fit their unit — move, attack, defend, buff, debuff, special actions. Unspent AP and cards create the tactical puzzle each turn. Decision: spend AP now on aggression or hold cards for defense and combos.
- **Morale:** Morale rises when a unit deals damage and falls when it takes damage. Morale modifies damage and hit chance on top of the core stats. Decision: protect units on a morale streak, or pull back units whose morale is collapsing.
- **Speed & Turn Order:** Units act in order of their speed value, recalculated each round. Speed can be increased by buffs and by card effects during combat, letting a unit act earlier in the next round. Decision: invest cards into speed to seize initiative or spend them on direct impact.
- **Tactical Grid:** Combat takes place on Fire-Emblem-sized maps with terrain, objectives, and enemy units that also use decks under the same AP and draw rules. Decision: positioning, focus fire, and timing around enemy initiative.
- **Experience & Levelling:** After each mission heroes and battalions gain experience. On level-up the player distributes stat points freely. Decision: deepen a unit's strengths or patch its weaknesses.
- **Resources & Base Buildings:** Missions reward wood, stone, food, and gold (later also soul energy). In the base, resources upgrade buildings, unlock new cards for heroes and battalions, and upgrade existing cards. Decision: where to invest limited resources to shape long-term strategy.
- **Scouting & Enemy Intel:** Resources can be spent to send scouts and learn about specific enemy types. Intel unlocks in tiers per enemy type: (1) see which cards are in their deck, (2) see their current hand, (3) see the action they will take next turn. Decision: invest in scouting vs. raw unit power.
- **Soul Energy & Story Path:** Soul energy is earned over the course of the story and unlocks new, stronger units and cards. Total soul energy use is tracked globally and decides later story development: liberating the realm for the greater good, or becoming corrupted and conquering the realm for oneself. Decision: take the immediate power boost or keep the revolution clean.

## Moment-to-moment core loop

1. **Combine & Build Units:** Pair heroes with battalions, check combined stats and the combined 20-card deck; this defines the player's available in-combat decisions.
2. **Deploy to Mission:** Select a mission with clear objectives (defeat, hold, escort, reach) on a Fire-Emblem-sized map, using known scouting intel about the enemy types present.
3. **Resolve Tactical Turns:** In speed order, each unit draws cards and spends AP to move, attack, defend, or use abilities; damage dealt and taken shifts morale, and speed buffs change the next round's turn order.
4. **Collect Rewards & Penalties:** End of mission yields experience, wood, stone, food, gold, and possibly soul energy; apply unit losses and injuries.
5. **Level & Upgrade:** Distribute stat points on level-up, upgrade buildings, unlock and upgrade cards for heroes and battalions.
6. **Scout & Decide on Soul Energy:** Send scouts to deepen intel on enemy types; decide whether to spend soul energy on stronger units, knowing it affects the story path.
7. **Choose Next Mission:** Pick the next mission based on army strength, available intel, and story progress — loop repeats with evolved units and stakes.

## Session loop

1. Prepare: pair heroes with battalions and review their combined decks and stats (3–5 min).
2. Deploy: choose one of 2–3 missions with clear objectives (1 min).
3. Fight: play multiple rounds on a Fire-Emblem-sized map, resolving cards, AP, morale, and speed-based turn order (15–25 min).
4. Resolve: tally experience and resources, apply casualties (2–3 min).
5. Advance: level up, invest in one building or card upgrade, send scouts or spend soul energy, and select the next mission (3–6 min).

## Core playtest question

> Can a player pair heroes with battalions, complete one tactical mission on a Fire-Emblem-sized map (win or lose), and return to the base to level up and spend rewards within a single 30–45 minute playthrough?

## Scope rules

- Limit battlefield maps to a small library of Fire-Emblem-sized templates (roughly 12x12 to 16x16 tiles) with fixed objective placements for the first playable.
- Keep the first build to a handful of heroes and battalion types and a card pool under 60 unique cards; card upgrades are simple swaps or single-stat tweaks.
- Scouting intel tiers and soul energy tracking are implemented as simple counters and flags; story branching from soul energy is limited to a single threshold check in the first build, no branching quest trees yet.

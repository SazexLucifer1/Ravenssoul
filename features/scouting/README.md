# Scouting (planned)

Design: spending resources raises intel on one enemy type through three tiers:
1. see the enemy deck, 2. see the current hand, 3. see the next action.

First-playable scope (blueprint scope rules): a per-enemy-type integer tier
stored in `GameSession` and saved as `{"scouting": {"<enemy_id>": tier}}`.
Combat UI reads the tier; it never changes enemy behaviour. Keyed by
`EnemyConfig.id`.

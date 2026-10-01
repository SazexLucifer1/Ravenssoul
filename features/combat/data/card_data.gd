class_name CardData
extends Resource
## One authored card. Effects are data; combat rules interpret them.

enum Kind { MOVE, ATTACK, DEFEND, BUFF, DEBUFF, SPECIAL }

## Stable, language-independent identifier (saves, analytics, tests).
@export var id: StringName
@export var name_key: String
@export var description_key: String
@export var kind: Kind = Kind.ATTACK
@export_range(0, 10) var ap_cost: int = 1
## Generic magnitude: damage, block, tiles moved, or buff amount depending on kind.
@export_range(0, 99) var power: int = 0
@export_range(0, 12) var range_tiles: int = 1

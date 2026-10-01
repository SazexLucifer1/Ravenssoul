class_name DialogueSequence
extends Resource
## Ordered dialogue lines for one conversation. Branching is out of scope for
## the first playable (single soul-energy threshold check only).

@export var id: StringName
@export var lines: Array[DialogueLine] = []

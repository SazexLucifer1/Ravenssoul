class_name SaveMigrations
extends RefCounted
## Upgrades older save payloads to the current schema, one version at a time.
##
## To change the save format: bump SaveService.SCHEMA_VERSION, then add a
## `_migrate_<old>_to_<new>(data)` function here and register it in STEPS.
## Never edit an existing step after release.

## from_version -> method name producing from_version + 1.
const STEPS: Dictionary[int, StringName] = {}


## Returns the migrated payload, or null when a step is missing.
static func migrate(data: Dictionary, from_version: int, to_version: int) -> Variant:
	var current: Dictionary = data.duplicate(true)
	for version: int in range(from_version, to_version):
		if not STEPS.has(version):
			return null
		current = Callable(SaveMigrations, STEPS[version]).call(current)
	return current

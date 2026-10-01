class_name AutomationProfiles
extends RefCounted
## Capability profiles. A method runs only if its capability (methods.json)
## is granted by the active profile. See the matrix in docs/AUTOMATION.md.

const DEV: StringName = &"dev"
const QA: StringName = &"qa"
const PRODUCTION: StringName = &"production"

const QA_CAPABILITIES: PackedStringArray = [
	"session", "inspect", "observe.screenshot", "observe.logs", "observe.events",
	"observe.perf", "act.semantic", "act.input", "wait", "assert", "checkpoint",
	"reset", "fixture", "recording", "app.quit",
]
const DEV_ONLY: PackedStringArray = ["inspect.deep", "dev.teleport", "dev.spawn", "dev.command"]
## Production never starts the server (see AutomationGate); listed for the matrix.
const PRODUCTION_CAPABILITIES: PackedStringArray = []


static func capabilities(profile: StringName) -> PackedStringArray:
	match profile:
		DEV:
			var all: PackedStringArray = QA_CAPABILITIES.duplicate()
			all.append_array(DEV_ONLY)
			return all
		QA:
			return QA_CAPABILITIES.duplicate()
	return PRODUCTION_CAPABILITIES.duplicate()


static func is_known(profile: StringName) -> bool:
	return profile in [DEV, QA, PRODUCTION]

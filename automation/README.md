# automation/

Godot side of the remote automation interface (dev/QA builds only; excluded
from release exports). Full documentation: [`docs/AUTOMATION.md`](../docs/AUTOMATION.md).

```
protocol/   methods.json (method table: capability + param schema), constants, schema validator
server/     AutomationServer, HTTP + WebSocket transports, auth, profiles, rate limiter,
            sessions, event log, log capture, predicates, input driver
adapter/    AutomationAdapter (engine-neutral contract), GodotAutomationAdapter,
            AutomationProvider + UtopiaAutomationProvider (game semantics)
fixtures/   approved fixture JSON files (referenced by id only)
```

Rules: nothing outside `automation/` and `tests/` may reference these classes
(test-enforced); the server is started only by `core/diagnostics/automation_gate.gd`.

# utopia-automation (Python client)

Typed client, launcher, CLI, MCP adapter, and end-to-end scenario for the
Utopia automation protocol. Standard library only, Python ≥ 3.10.
Protocol and security model: [`docs/AUTOMATION.md`](../../docs/AUTOMATION.md).

```bash
pip install -e tools/automation            # optional; or use PYTHONPATH=tools/automation
python -m utopia_automation --help
python -m utopia_automation scenario first_mission --godot $GODOT --project . --headless
python -m unittest discover -s tools/automation/tests -t tools/automation
python tools/automation/scripts/gen_method_table.py   # after editing automation/protocol/methods.json
```

| Module | Purpose |
|---|---|
| `client.py` | `AutomationClient`: one typed method per protocol method |
| `models.py` | `Entity`, `Event`, `Capabilities`, `Cond` predicate builder |
| `errors.py` | Exceptions per protocol error code |
| `launcher.py` | `GameProcess`: launch with fresh token (env), free ports, graceful stop |
| `cli.py` | `utopia-automation` command line |
| `mcp_server.py` | MCP stdio adapter over the typed client |
| `scenario_first_mission.py` | The CI end-to-end scenario |

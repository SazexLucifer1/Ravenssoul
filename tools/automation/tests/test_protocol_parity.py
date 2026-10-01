"""The Python client must stay in sync with the protocol table and error codes."""

import inspect
import json
import re
import unittest
from pathlib import Path

from utopia_automation import client as client_module
from utopia_automation import errors

REPO = Path(__file__).resolve().parents[3]
METHODS = json.loads((REPO / "automation/protocol/methods.json").read_text())


class ProtocolParityTest(unittest.TestCase):
    def test_every_protocol_method_has_a_client_wrapper(self):
        source = inspect.getsource(client_module)
        called = set(re.findall(r'self\.call\("([a-z_.]+)"', source))
        server_only = {"events.subscribe"}  # WebSocket-only push subscription
        missing = set(METHODS["methods"]) - called - server_only
        self.assertFalse(missing, f"client lacks wrappers for {sorted(missing)}")

    def test_client_calls_only_existing_methods(self):
        source = inspect.getsource(client_module)
        called = set(re.findall(r'self\.call\("([a-z_.]+)"', source))
        self.assertFalse(called - set(METHODS["methods"]))

    def test_error_codes_match_godot_constants(self):
        gd = (REPO / "automation/protocol/automation_protocol.gd").read_text()
        godot_codes = {int(v) for v in re.findall(r"const [A-Z_]+: int = (-32\d{3})", gd)}
        python_codes = set(errors._BY_CODE)
        self.assertEqual(godot_codes - {-32700, -32603}, python_codes)

    def test_every_method_declares_capability_and_object_params(self):
        for name, spec in METHODS["methods"].items():
            self.assertIn("capability", spec, name)
            self.assertEqual(spec["params"]["type"], "object", name)
            self.assertFalse(spec["params"].get("additionalProperties", True), f"{name} must reject unknown params")


    def test_documentation_lists_every_method(self):
        doc = (REPO / "docs/AUTOMATION.md").read_text()
        missing = [name for name in METHODS["methods"] if f"| `{name}` |" not in doc]
        self.assertFalse(missing, f"docs/AUTOMATION.md method table is missing {missing}")


if __name__ == "__main__":
    unittest.main()

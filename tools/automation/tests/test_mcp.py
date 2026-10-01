"""MCP adapter: protocol handshake, tool listing, and forwarding to the typed client."""

import io
import json
import unittest

from utopia_automation.errors import ForbiddenError
from utopia_automation.mcp_server import TOOLS, McpServer, serve


class FakeClient:
    def __init__(self):
        self.calls = []

    def state(self):
        self.calls.append("state")
        return {"route": "main_menu"}

    def perform(self, action, target=None, **args):
        self.calls.append(("perform", action, target, args))
        if action == "equip":
            raise ForbiddenError("nope", -32002, {"kind": "forbidden"})
        return {"performed": True}

    def call(self, method, params):
        self.calls.append((method, params))
        return {"png_base64": "AAAA"}


class McpTest(unittest.TestCase):
    def setUp(self):
        self.client = FakeClient()
        self.server = McpServer(self.client)  # type: ignore[arg-type]

    def test_initialize_and_tools_list(self):
        init = self.server.handle({"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}})
        self.assertIn("tools", init["result"]["capabilities"])
        listed = self.server.handle({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})["result"]["tools"]
        names = {t["name"] for t in listed}
        self.assertEqual(names, {t.name for t in TOOLS} | {"game_screenshot"})
        self.assertTrue(all(t["inputSchema"]["type"] == "object" for t in listed))

    def test_tool_call_forwards_to_client(self):
        result = self.server.handle({"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                                     "params": {"name": "game_perform", "arguments": {"action": "activate", "target": "main_menu.new_game"}}})["result"]
        self.assertEqual(json.loads(result["content"][0]["text"]), {"performed": True})
        self.assertEqual(self.client.calls[-1], ("perform", "activate", "main_menu.new_game", {}))

    def test_game_errors_become_tool_errors(self):
        result = self.server.handle({"jsonrpc": "2.0", "id": 4, "method": "tools/call",
                                     "params": {"name": "game_perform", "arguments": {"action": "equip"}}})["result"]
        self.assertTrue(result["isError"])

    def test_screenshot_returns_image_content(self):
        result = self.server.handle({"jsonrpc": "2.0", "id": 5, "method": "tools/call", "params": {"name": "game_screenshot"}})["result"]
        self.assertEqual(result["content"][0]["type"], "image")

    def test_notifications_get_no_response_and_unknown_methods_error(self):
        self.assertIsNone(self.server.handle({"jsonrpc": "2.0", "method": "notifications/initialized"}))
        self.assertEqual(self.server.handle({"jsonrpc": "2.0", "id": 6, "method": "nope"})["error"]["code"], -32601)

    def test_stdio_loop(self):
        stdin = io.StringIO('{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"game_state"}}\nnot json\n')
        stdout = io.StringIO()
        serve(self.client, stdin, stdout)  # type: ignore[arg-type]
        lines = [json.loads(l) for l in stdout.getvalue().splitlines()]
        self.assertEqual(json.loads(lines[0]["result"]["content"][0]["text"]), {"route": "main_menu"})
        self.assertEqual(lines[1]["error"]["code"], -32700)


if __name__ == "__main__":
    unittest.main()

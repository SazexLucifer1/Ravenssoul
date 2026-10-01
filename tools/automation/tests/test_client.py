"""AutomationClient against an in-process fake JSON-RPC server."""

import base64
import json
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

from utopia_automation import (AutomationAssertionError, AutomationClient, Cond, ForbiddenError,
                               RateLimitedError, TimeoutError_, TransportError, UnauthorizedError)

TOKEN = "t" * 64


class FakeGame(BaseHTTPRequestHandler):
    calls: list = []
    rate_limited = False

    def log_message(self, *args):  # silence
        pass

    def _send(self, status, payload):
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        self._send(200, {"ok": True, "protocol": "utopia-automation", "version": "1.0"})

    def do_POST(self):
        if self.headers.get("Authorization") != f"Bearer {TOKEN}":
            return self._send(401, {"jsonrpc": "2.0", "id": None, "error": {"code": -32001, "message": "bad token", "data": {"kind": "unauthorized"}}})
        request = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        FakeGame.calls.append(request)
        method, rid = request["method"], request["id"]
        if FakeGame.rate_limited:
            return self._send(429, {"jsonrpc": "2.0", "id": rid, "error": {"code": -32003, "message": "slow down", "data": {"kind": "rate_limited"}}})
        results = {
            "session.hello": {"session_id": "abc", "profile": "qa", "capabilities": ["session"]},
            "assert.check": {"passed": False, "actual": [0, 10], "message": ""},
            "observe.screenshot": {"width": 1, "height": 1, "png_base64": base64.b64encode(b"\x89PNG").decode()},
            "tree.query": {"entities": [{"id": "main_menu.new_game", "type": "Button", "role": "button", "tags": ["primary"],
                                         "visible": True, "enabled": True, "interactable": True, "actions": ["activate"],
                                         "properties": {}, "text_key": "MAIN_MENU_NEW_GAME"}]},
        }
        if method == "dev.teleport":
            return self._send(200, {"jsonrpc": "2.0", "id": rid, "error": {"code": -32002, "message": "no", "data": {"kind": "forbidden", "capability": "dev.teleport"}}})
        if method == "wait.until":
            return self._send(200, {"jsonrpc": "2.0", "id": rid, "error": {"code": -32007, "message": "late", "data": {"kind": "timeout", "actual": 1}}})
        self._send(200, {"jsonrpc": "2.0", "id": rid, "result": results.get(method, {})})


class ClientTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = HTTPServer(("127.0.0.1", 0), FakeGame)
        cls.url = f"http://127.0.0.1:{cls.server.server_address[1]}"
        threading.Thread(target=cls.server.serve_forever, daemon=True).start()

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()

    def setUp(self):
        FakeGame.calls.clear()
        FakeGame.rate_limited = False
        self.client = AutomationClient(self.url, TOKEN)

    def test_requests_are_json_rpc_with_increasing_ids(self):
        self.client.state()
        self.client.scene()
        self.assertEqual([c["id"] for c in FakeGame.calls], [1, 2])
        self.assertTrue(all(c["jsonrpc"] == "2.0" for c in FakeGame.calls))

    def test_context_manager_opens_and_closes_session(self):
        with self.client as c:
            self.assertEqual(c.session_id, "abc")
        self.assertEqual([x["method"] for x in FakeGame.calls], ["session.hello", "session.close"])

    def test_bad_token_raises_unauthorized(self):
        with self.assertRaises(UnauthorizedError):
            AutomationClient(self.url, "wrong").state()

    def test_error_codes_map_to_exceptions(self):
        with self.assertRaises(ForbiddenError) as ctx:
            self.client.dev_teleport("unit.x", (1, 1))
        self.assertEqual(ctx.exception.data["capability"], "dev.teleport")
        with self.assertRaises(TimeoutError_) as ctx:
            self.client.wait_until(Cond.state("x").eq(1), 100)
        self.assertEqual(ctx.exception.data["actual"], 1)
        FakeGame.rate_limited = True
        with self.assertRaises(RateLimitedError):
            self.client.state()

    def test_assert_that_raises_with_actual_value(self):
        with self.assertRaises(AutomationAssertionError) as ctx:
            self.client.assert_that(Cond.state("unit.cell").eq([1, 10]), "unit on spawn")
        self.assertEqual(ctx.exception.actual, [0, 10])

    def test_none_params_are_omitted(self):
        self.client.query(role="button")
        self.assertEqual(FakeGame.calls[-1]["params"], {"role": "button"})
        self.client.move("left")
        self.assertEqual(FakeGame.calls[-1]["params"], {"action": "move", "args": {"direction": "left"}})

    def test_query_returns_typed_entities(self):
        entity = self.client.query()[0]
        self.assertEqual((entity.id, entity.text_key, entity.tags), ("main_menu.new_game", "MAIN_MENU_NEW_GAME", ["primary"]))

    def test_screenshot_writes_decoded_png(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = self.client.screenshot(Path(tmp) / "shots" / "a.png")
            self.assertEqual(path.read_bytes(), b"\x89PNG")

    def test_unreachable_game_is_a_transport_error(self):
        with self.assertRaises(TransportError):
            AutomationClient("http://127.0.0.1:9", TOKEN, timeout_s=1).state()


class ConditionBuilderTest(unittest.TestCase):
    def test_shapes(self):
        self.assertEqual(Cond.state("unit.cell").eq([1, 2]), {"source": "state", "path": "unit.cell", "op": "eq", "value": [1, 2]})
        self.assertEqual(Cond.entity("a.b", "visible").exists(), {"source": "entity", "id": "a.b", "path": "visible", "op": "exists"})
        self.assertEqual(Cond.not_(Cond.scene("paused").eq(True)), {"not": {"source": "scene", "path": "paused", "op": "eq", "value": True}})
        self.assertEqual(list(Cond.all(Cond.state("a").gt(1), Cond.state("b").lt(2))), ["all"])


if __name__ == "__main__":
    unittest.main()

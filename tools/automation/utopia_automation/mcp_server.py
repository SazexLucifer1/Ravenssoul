"""MCP (Model Context Protocol) adapter so AI agents can drive the game.

Runs over stdio (newline-delimited JSON-RPC 2.0) and forwards every tool call
through the typed AutomationClient — it adds no capabilities of its own and
inherits the game's profile, auth, rate limits, and validation. It only
attaches to an already-running game (it never launches processes).

    UTOPIA_AUTOMATION_URL=http://127.0.0.1:47801 UTOPIA_AUTOMATION_TOKEN=... utopia-automation-mcp
"""

from __future__ import annotations

import json
import sys
from typing import Any, Callable, TextIO

from .client import AutomationClient
from .errors import AutomationAssertionError, AutomationError

MCP_PROTOCOL_VERSION = "2025-06-18"
COND_SCHEMA: dict[str, Any] = {
    "type": "object",
    "description": 'Predicate, e.g. {"source":"state","path":"unit.cell","op":"eq","value":[0,10]}; '
                   'combine with {"all":[...]}, {"any":[...]}, {"not":{...}}',
}


def _obj(props: dict[str, Any] | None = None, required: list[str] | None = None) -> dict[str, Any]:
    schema: dict[str, Any] = {"type": "object", "properties": props or {}, "additionalProperties": False}
    if required:
        schema["required"] = required
    return schema


class Tool:
    def __init__(self, name: str, description: str, schema: dict[str, Any], handler: Callable[[AutomationClient, dict[str, Any]], Any]):
        self.name, self.description, self.schema, self.handler = name, description, schema, handler


TOOLS: list[Tool] = [
    Tool("game_capabilities", "Protocol version, profile, granted capabilities, supported actions, events.", _obj(),
         lambda c, a: c.capabilities().raw),
    Tool("game_state", "Authoritative game state (route, screens, focus, unit, mission, campaign).", _obj(),
         lambda c, a: c.state()),
    Tool("game_query", "List entities (UI controls, screens, units, grid, mission, resources) by filter.",
         _obj({"role": {"type": "string"}, "tag": {"type": "string"}, "id_prefix": {"type": "string"},
               "interactable_only": {"type": "boolean"}}),
         lambda c, a: [e.raw for e in c.query(**a)]),
    Tool("game_entity", "Describe one entity by stable automation id.", _obj({"id": {"type": "string"}}, ["id"]),
         lambda c, a: c.entity(a["id"]).raw),
    Tool("game_actions", "Semantic actions available right now.", _obj(), lambda c, a: c.available_actions()),
    Tool("game_perform", "Perform a semantic action (activate, select, toggle, confirm, cancel, move, pause, resume).",
         _obj({"action": {"type": "string"}, "target": {"type": "string"}, "args": {"type": "object"}}, ["action"]),
         lambda c, a: c.perform(a["action"], a.get("target"), **a.get("args", {}))),
    Tool("game_key", "Tap/press/release a keyboard key through the real input path.",
         _obj({"key": {"type": "string"}, "mode": {"type": "string", "enum": ["tap", "down", "up"]}}, ["key"]),
         lambda c, a: {"held": c.key(a["key"], a.get("mode", "tap"))}),
    Tool("game_gamepad", "Tap/press/release a gamepad button through the real input path.",
         _obj({"button": {"type": "string"}, "mode": {"type": "string", "enum": ["tap", "down", "up"]}}, ["button"]),
         lambda c, a: {"held": c.gamepad_button(a["button"], a.get("mode", "tap"))}),
    Tool("game_release_inputs", "Release every held input.", _obj(), lambda c, a: {"released": c.release_all()}),
    Tool("game_wait_until", "Wait until a condition holds (checked every frame).",
         _obj({"condition": COND_SCHEMA, "timeout_ms": {"type": "integer"}}, ["condition"]),
         lambda c, a: c.wait_until(a["condition"], a.get("timeout_ms", 10000))),
    Tool("game_wait_settled", "Wait until no transition, movement, or held input is in progress.", _obj(),
         lambda c, a: (c.wait_settled(), {"settled": True})[1]),
    Tool("game_assert", "Assert a condition now; returns passed/actual.",
         _obj({"condition": COND_SCHEMA, "message": {"type": "string"}}, ["condition"]),
         lambda c, a: c.check(a["condition"], a.get("message"))),
    Tool("game_events", "Recent semantic events after a sequence number.",
         _obj({"since_seq": {"type": "integer"}}),
         lambda c, a: {"events": [e.__dict__ for e in c.events(a.get("since_seq", 0))[0]]}),
    Tool("game_logs", "Recent game log lines (paths redacted).",
         _obj({"min_level": {"type": "string", "enum": ["info", "warning", "error"]}}),
         lambda c, a: c.logs(min_level=a.get("min_level"))),
    Tool("game_reset", "Reset to the title screen (or a fresh mission).",
         _obj({"route": {"type": "string", "enum": ["main_menu", "gameplay"]}}),
         lambda c, a: c.reset(a.get("route"))),
    Tool("game_load_fixture", "Load an approved fixture by id.", _obj({"fixture": {"type": "string"}}, ["fixture"]),
         lambda c, a: c.load_fixture(a["fixture"])),
]
TOOLS_BY_NAME = {tool.name: tool for tool in TOOLS}
SCREENSHOT_TOOL = {"name": "game_screenshot", "description": "PNG screenshot of the current frame (needs a non-headless run).",
                   "inputSchema": _obj({"max_width": {"type": "integer"}})}


class McpServer:
    def __init__(self, client: AutomationClient):
        self.client = client

    def handle(self, message: dict[str, Any]) -> dict[str, Any] | None:
        method = message.get("method")
        msg_id = message.get("id")
        if msg_id is None:  # notification (e.g. notifications/initialized)
            return None
        try:
            result = self._handle(method, message.get("params") or {})
        except KeyError as exc:
            return {"jsonrpc": "2.0", "id": msg_id, "error": {"code": -32602, "message": f"missing {exc}"}}
        except LookupError as exc:
            return {"jsonrpc": "2.0", "id": msg_id, "error": {"code": -32601, "message": str(exc)}}
        return {"jsonrpc": "2.0", "id": msg_id, "result": result}

    def _handle(self, method: str | None, params: dict[str, Any]) -> Any:
        if method == "initialize":
            return {"protocolVersion": MCP_PROTOCOL_VERSION, "capabilities": {"tools": {}},
                    "serverInfo": {"name": "utopia-automation", "version": "1.0.0"}}
        if method == "ping":
            return {}
        if method == "tools/list":
            return {"tools": [{"name": t.name, "description": t.description, "inputSchema": t.schema} for t in TOOLS]
                    + [SCREENSHOT_TOOL]}
        if method == "tools/call":
            return self._call_tool(params["name"], params.get("arguments") or {})
        raise LookupError(f"unknown method {method}")

    def _call_tool(self, name: str, arguments: dict[str, Any]) -> dict[str, Any]:
        try:
            if name == "game_screenshot":
                result = self.client.call("observe.screenshot", {k: v for k, v in arguments.items() if k == "max_width"})
                return {"content": [{"type": "image", "data": result["png_base64"], "mimeType": "image/png"}]}
            tool = TOOLS_BY_NAME.get(name)
            if tool is None:
                return {"isError": True, "content": [{"type": "text", "text": f"unknown tool {name}"}]}
            value = tool.handler(self.client, arguments)
            return {"content": [{"type": "text", "text": json.dumps(value, default=str)}]}
        except (AutomationError, AutomationAssertionError, ValueError, TypeError) as exc:
            return {"isError": True, "content": [{"type": "text", "text": str(exc)}]}


def serve(client: AutomationClient, stdin: TextIO = sys.stdin, stdout: TextIO = sys.stdout) -> None:
    server = McpServer(client)
    for line in stdin:
        line = line.strip()
        if not line:
            continue
        try:
            message = json.loads(line)
        except ValueError:
            response: dict[str, Any] | None = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": "parse error"}}
        else:
            response = server.handle(message) if isinstance(message, dict) else None
        if response is not None:
            stdout.write(json.dumps(response) + "\n")
            stdout.flush()


def main() -> None:
    serve(AutomationClient())


if __name__ == "__main__":
    main()

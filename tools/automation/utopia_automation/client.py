"""Typed JSON-RPC client for the Utopia automation protocol (HTTP transport)."""

from __future__ import annotations

import base64
import itertools
import json
import os
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

from .errors import AutomationAssertionError, TransportError, error_from_payload
from .models import Capabilities, Condition, Direction, Entity, Event, GamepadButton, InputMode, SemanticAction

DEFAULT_URL = "http://127.0.0.1:47801"
URL_ENV = "UTOPIA_AUTOMATION_URL"
TOKEN_ENV = "UTOPIA_AUTOMATION_TOKEN"


class AutomationClient:
    """One client = one automation session. Use as a context manager so the
    session is closed (held inputs released, fixtures removed) even when a
    test fails."""

    def __init__(self, url: str | None = None, token: str | None = None, timeout_s: float = 30.0):
        self.url = (url or os.environ.get(URL_ENV) or DEFAULT_URL).rstrip("/")
        self.token = token if token is not None else os.environ.get(TOKEN_ENV, "")
        self.timeout_s = timeout_s
        self._ids = itertools.count(1)
        self.session_id: str | None = None

    # -- context management --------------------------------------------------------

    def __enter__(self) -> "AutomationClient":
        self.hello()
        return self

    def __exit__(self, *exc: object) -> None:
        try:
            self.close()
        except Exception:  # the game may already be gone; cleanup also runs server-side on idle timeout
            pass

    # -- transport ----------------------------------------------------------------------

    def health(self) -> dict[str, Any]:
        try:
            with urllib.request.urlopen(f"{self.url}/v1/health", timeout=5) as response:
                return json.loads(response.read())
        except (urllib.error.URLError, OSError) as exc:
            raise TransportError(f"game not reachable at {self.url}: {exc}") from exc

    def call(self, method: str, params: dict[str, Any] | None = None, timeout_s: float | None = None) -> Any:
        """Send one JSON-RPC request and return its result (raises on error)."""
        request = {"jsonrpc": "2.0", "id": next(self._ids), "method": method, "params": params or {}}
        http_request = urllib.request.Request(
            f"{self.url}/v1/rpc",
            data=json.dumps(request).encode(),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {self.token}"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(http_request, timeout=timeout_s or self.timeout_s) as response:
                payload = json.loads(response.read())
        except urllib.error.HTTPError as exc:
            body = exc.read()
            try:
                payload = json.loads(body)
            except ValueError:
                raise TransportError(f"HTTP {exc.code} from {method}") from exc
            if "error" in payload and isinstance(payload["error"], dict):
                raise error_from_payload(payload["error"]) from None
            raise TransportError(f"HTTP {exc.code}: {payload.get('error', payload)}") from exc
        except (urllib.error.URLError, OSError) as exc:
            raise TransportError(f"{method} failed: {exc}") from exc
        if "error" in payload:
            raise error_from_payload(payload["error"])
        return payload.get("result")

    @staticmethod
    def _clean(**params: Any) -> dict[str, Any]:
        return {k: v for k, v in params.items() if v is not None}

    # -- session & discovery ----------------------------------------------------------------

    def hello(self) -> dict[str, Any]:
        result = self.call("session.hello")
        self.session_id = result["session_id"]
        return result

    def close(self) -> None:
        self.call("session.close")

    def reset(self, route: str | None = None) -> dict[str, Any]:
        return self.call("session.reset", self._clean(route=route), timeout_s=60)

    def capabilities(self) -> Capabilities:
        return Capabilities.from_payload(self.call("protocol.capabilities"))

    def schema(self) -> dict[str, Any]:
        return self.call("protocol.schema")

    # -- inspection ------------------------------------------------------------------------

    def scene(self) -> dict[str, Any]:
        return self.call("scene.current")

    def query(self, *, role: str | None = None, type: str | None = None, tag: str | None = None,
              id_prefix: str | None = None, visible_only: bool | None = None,
              interactable_only: bool | None = None, limit: int | None = None) -> list[Entity]:
        result = self.call("tree.query", self._clean(role=role, type=type, tag=tag, id_prefix=id_prefix,
                                                     visible_only=visible_only, interactable_only=interactable_only, limit=limit))
        return [Entity.from_payload(e) for e in result["entities"]]

    def entity(self, entity_id: str, deep: bool = False) -> Entity:
        return Entity.from_payload(self.call("entity.get", self._clean(id=entity_id, deep=deep or None)))

    def state(self) -> dict[str, Any]:
        return self.call("state.get")

    def available_actions(self) -> list[dict[str, Any]]:
        return self.call("actions.available")["actions"]

    # -- semantic actions ----------------------------------------------------------------------

    def perform(self, action: SemanticAction, target: str | None = None, **args: Any) -> dict[str, Any]:
        return self.call("action.perform", self._clean(action=action, target=target, args=args or None))

    def activate(self, target: str) -> dict[str, Any]:
        return self.perform("activate", target)

    def select(self, target: str, *, index: int | None = None, value: str | None = None) -> dict[str, Any]:
        return self.perform("select", target, **self._clean(index=index, value=value))

    def toggle(self, target: str, on: bool | None = None) -> dict[str, Any]:
        return self.perform("toggle", target, **self._clean(on=on))

    def confirm(self) -> dict[str, Any]:
        return self.perform("confirm")

    def cancel(self) -> dict[str, Any]:
        return self.perform("cancel")

    def move(self, direction: Direction | None = None, *, path: list[Direction] | None = None,
             unit: str | None = None) -> dict[str, Any]:
        return self.perform("move", unit, **self._clean(direction=direction, path=path))

    def pause(self) -> dict[str, Any]:
        return self.perform("pause")

    def resume(self) -> dict[str, Any]:
        return self.perform("resume")

    # -- raw input (the real input path) -----------------------------------------------------------

    def key(self, key: str, mode: InputMode = "tap", duration_ms: int | None = None) -> list[str]:
        return self.call("input.key", self._clean(key=key, mode=mode, duration_ms=duration_ms))["held"]

    def input_action(self, action: str, mode: InputMode = "tap", duration_ms: int | None = None) -> list[str]:
        return self.call("input.action", self._clean(action=action, mode=mode, duration_ms=duration_ms))["held"]

    def mouse(self, mode: str, x: float, y: float, button: str | None = None) -> list[str]:
        return self.call("input.mouse", self._clean(mode=mode, x=x, y=y, button=button))["held"]

    def click(self, x: float, y: float, button: str = "left") -> list[str]:
        return self.mouse("click", x, y, button)

    def click_entity(self, entity_id: str) -> list[str]:
        position = self.entity(entity_id).position
        if position is None:
            raise ValueError(f"{entity_id} has no screen position")
        return self.click(position["x"], position["y"])

    def gamepad_button(self, button: GamepadButton, mode: InputMode = "tap", duration_ms: int | None = None) -> list[str]:
        return self.call("input.gamepad", self._clean(button=button, mode=mode, duration_ms=duration_ms))["held"]

    def gamepad_axis(self, axis: str, value: float) -> list[str]:
        return self.call("input.gamepad", {"axis": axis, "value": value})["held"]

    def touch(self, x: float, y: float, mode: str = "tap", index: int = 0) -> list[str]:
        return self.call("input.touch", {"x": x, "y": y, "mode": mode, "index": index})["held"]

    def release_all(self) -> int:
        return self.call("input.release_all")["released"]

    # -- waits & assertions --------------------------------------------------------------------------

    def wait_frames(self, count: int) -> None:
        self.call("wait.frames", {"count": count})

    def wait_until(self, condition: Condition, timeout_ms: int = 10000) -> dict[str, Any]:
        return self.call("wait.until", {"condition": condition, "timeout_ms": timeout_ms}, timeout_s=timeout_ms / 1000 + 10)

    def wait_event(self, type: str, match: dict[str, Any] | None = None, after_seq: int | None = None,
                   timeout_ms: int = 10000) -> Event:
        result = self.call("wait.event", self._clean(type=type, match=match, after_seq=after_seq, timeout_ms=timeout_ms),
                           timeout_s=timeout_ms / 1000 + 10)
        return Event.from_payload(result["event"])

    def wait_settled(self, timeout_ms: int = 10000) -> None:
        self.call("wait.settled", {"timeout_ms": timeout_ms}, timeout_s=timeout_ms / 1000 + 10)

    def check(self, condition: Condition, message: str | None = None) -> dict[str, Any]:
        return self.call("assert.check", self._clean(condition=condition, message=message))

    def assert_that(self, condition: Condition, message: str = "") -> Any:
        """Evaluate in the game; raise AutomationAssertionError with the actual value on failure."""
        result = self.check(condition, message or None)
        if not result["passed"]:
            raise AutomationAssertionError(message, result.get("actual"), dict(condition))
        return result.get("actual")

    # -- checkpoints, fixtures, recordings -------------------------------------------------------------

    def checkpoint(self, name: str) -> None:
        self.call("checkpoint.create", {"name": name})

    def restore(self, name: str) -> dict[str, Any]:
        return self.call("checkpoint.restore", {"name": name}, timeout_s=60)

    def checkpoints(self) -> list[str]:
        return self.call("checkpoint.list")["names"]

    def fixtures(self) -> list[str]:
        return self.call("fixture.list")["fixtures"]

    def load_fixture(self, fixture: str, write_save: bool = False) -> dict[str, Any]:
        return self.call("fixture.load", {"fixture": fixture, "write_save": write_save}, timeout_s=60)

    def start_recording(self) -> None:
        self.call("recording.start")

    def stop_recording(self) -> list[dict[str, Any]]:
        return self.call("recording.stop")["entries"]

    def replay(self, entries: list[dict[str, Any]]) -> int:
        """Re-send the recorded requests in order (events are skipped). Returns the count."""
        count = 0
        for entry in entries:
            if entry.get("kind") == "request" and entry["data"]["method"] not in ("session.close", "app.quit"):
                self.call(entry["data"]["method"], entry["data"].get("params", {}), timeout_s=120)
                count += 1
        return count

    # -- observation ------------------------------------------------------------------------------------

    def screenshot(self, path: str | os.PathLike[str], max_width: int | None = None) -> Path:
        result = self.call("observe.screenshot", self._clean(max_width=max_width), timeout_s=30)
        out = Path(path)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_bytes(base64.b64decode(result["png_base64"]))
        return out

    def logs(self, since_seq: int = 0, min_level: str | None = None, limit: int | None = None) -> dict[str, Any]:
        return self.call("observe.logs", self._clean(since_seq=since_seq, min_level=min_level, limit=limit))

    def events(self, since_seq: int = 0, types: list[str] | None = None, limit: int | None = None) -> tuple[list[Event], int]:
        result = self.call("observe.events", self._clean(since_seq=since_seq, types=types, limit=limit))
        return [Event.from_payload(e) for e in result["events"]], int(result["last_seq"])

    def perf(self) -> dict[str, Any]:
        return self.call("observe.perf")

    def quit(self, exit_code: int = 0) -> None:
        self.call("app.quit", {"exit_code": exit_code})

    # -- dev profile only ----------------------------------------------------------------------------------

    def dev_teleport(self, target: str, cell: tuple[int, int]) -> dict[str, Any]:
        return self.call("dev.teleport", {"target": target, "cell": list(cell)})

    def dev_spawn(self, hero_id: str, battalion_id: str, cell: tuple[int, int]) -> dict[str, Any]:
        return self.call("dev.spawn", {"hero_id": hero_id, "battalion_id": battalion_id, "cell": list(cell)})

    def dev_command(self, command: str, **args: Any) -> dict[str, Any]:
        return self.call("dev.command", self._clean(command=command, args=args or None))

    # -- helpers -------------------------------------------------------------------------------------------

    def wait_for_route(self, route: str, timeout_ms: int = 10000) -> None:
        from .models import Cond
        self.wait_until(Cond.all(Cond.scene("route").eq(route), Cond.scene("transitioning").eq(False)), timeout_ms)

    def wait_until_reachable(self, timeout_s: float = 30.0) -> None:
        deadline = time.monotonic() + timeout_s
        while True:
            try:
                self.health()
                return
            except TransportError:
                if time.monotonic() > deadline:
                    raise
                time.sleep(0.2)

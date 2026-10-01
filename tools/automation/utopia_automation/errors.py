"""Exceptions mapped from protocol error codes."""

from __future__ import annotations

from typing import Any


class AutomationError(Exception):
    """A JSON-RPC error returned by the game (or a transport failure)."""

    code: int = 0
    kind: str = "error"

    def __init__(self, message: str, code: int | None = None, data: dict[str, Any] | None = None):
        super().__init__(message)
        if code is not None:
            self.code = code
        self.data: dict[str, Any] = data or {}
        self.kind = self.data.get("kind", self.kind)

    def __str__(self) -> str:
        extra = {k: v for k, v in self.data.items() if k != "kind"}
        return f"[{self.kind}] {self.args[0]}" + (f" {extra}" if extra else "")


class TransportError(AutomationError):
    kind = "transport"


class InvalidRequestError(AutomationError):
    code, kind = -32600, "invalid_request"


class MethodNotFoundError(AutomationError):
    code, kind = -32601, "method_not_found"


class InvalidParamsError(AutomationError):
    code, kind = -32602, "invalid_params"


class UnauthorizedError(AutomationError):
    code, kind = -32001, "unauthorized"


class ForbiddenError(AutomationError):
    code, kind = -32002, "forbidden"


class RateLimitedError(AutomationError):
    code, kind = -32003, "rate_limited"


class NotFoundError(AutomationError):
    code, kind = -32004, "not_found"


class UnsupportedError(AutomationError):
    code, kind = -32005, "unsupported"


class NotInteractableError(AutomationError):
    code, kind = -32006, "not_interactable"


class TimeoutError_(AutomationError):
    """A wait did not complete in time (named to avoid shadowing the builtin)."""

    code, kind = -32007, "timeout"


class SessionExpiredError(UnauthorizedError):
    code, kind = -32008, "session_expired"


class BusyError(AutomationError):
    code, kind = -32009, "busy"


class PayloadTooLargeError(AutomationError):
    code, kind = -32010, "payload_too_large"


class AutomationAssertionError(AssertionError):
    """assert_that() failed; carries the actual value the game reported."""

    def __init__(self, message: str, actual: Any, condition: dict[str, Any]):
        super().__init__(f"{message or 'assertion failed'}: actual={actual!r} condition={condition!r}")
        self.actual = actual
        self.condition = condition


_BY_CODE: dict[int, type[AutomationError]] = {
    cls.code: cls
    for cls in (
        InvalidRequestError, MethodNotFoundError, InvalidParamsError, UnauthorizedError,
        ForbiddenError, RateLimitedError, NotFoundError, UnsupportedError,
        NotInteractableError, TimeoutError_, SessionExpiredError, BusyError, PayloadTooLargeError,
    )
}


def error_from_payload(payload: dict[str, Any]) -> AutomationError:
    code = int(payload.get("code", 0))
    cls = _BY_CODE.get(code, AutomationError)
    return cls(str(payload.get("message", "error")), code, payload.get("data") or {})

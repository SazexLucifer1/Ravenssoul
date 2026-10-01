"""Typed client for the Utopia remote automation protocol (utopia-automation/1.0).

See docs/AUTOMATION.md in the game repository for the protocol and security model.
"""

from .client import AutomationClient
from .errors import (
    AutomationAssertionError,
    AutomationError,
    BusyError,
    ForbiddenError,
    InvalidParamsError,
    NotFoundError,
    NotInteractableError,
    RateLimitedError,
    TimeoutError_,
    TransportError,
    UnauthorizedError,
    UnsupportedError,
)
from .launcher import GameProcess
from .models import Capabilities, Cond, Entity, Event

PROTOCOL = "utopia-automation"
PROTOCOL_VERSION = "1.0"

__all__ = [
    "AutomationClient", "GameProcess", "Capabilities", "Cond", "Entity", "Event",
    "AutomationError", "AutomationAssertionError", "BusyError", "ForbiddenError",
    "InvalidParamsError", "NotFoundError", "NotInteractableError", "RateLimitedError",
    "TimeoutError_", "TransportError", "UnauthorizedError", "UnsupportedError",
    "PROTOCOL", "PROTOCOL_VERSION",
]

"""Typed views of protocol payloads and a builder for wait/assert conditions."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Literal, TypedDict

Direction = Literal["up", "down", "left", "right"]
InputMode = Literal["tap", "down", "up"]
GamepadButton = Literal[
    "a", "b", "x", "y", "start", "back", "dpad_up", "dpad_down", "dpad_left", "dpad_right",
    "left_shoulder", "right_shoulder",
]
SemanticAction = Literal[
    "activate", "select", "toggle", "confirm", "cancel", "move", "pause", "resume",
    "interact", "equip", "use", "choose_dialogue",
]


class Condition(TypedDict, total=False):
    source: Literal["state", "entity", "scene"]
    id: str
    path: str
    op: Literal["eq", "ne", "gt", "gte", "lt", "lte", "contains", "in", "exists", "not_exists"]
    value: Any
    all: list["Condition"]
    any: list["Condition"]
    not_: "Condition"


@dataclass(frozen=True)
class Entity:
    id: str
    type: str
    name: str
    role: str
    tags: list[str]
    visible: bool
    enabled: bool
    interactable: bool
    actions: list[str]
    properties: dict[str, Any]
    position: dict[str, int] | None = None
    cell: list[int] | None = None
    text_key: str = ""
    text: str = ""
    focused: bool = False
    components: list[str] = field(default_factory=list)
    raw: dict[str, Any] = field(default_factory=dict, repr=False)

    @classmethod
    def from_payload(cls, data: dict[str, Any]) -> "Entity":
        return cls(
            id=data["id"], type=data.get("type", ""), name=data.get("name", ""),
            role=data.get("role", ""), tags=list(data.get("tags", [])),
            visible=bool(data.get("visible", False)), enabled=bool(data.get("enabled", False)),
            interactable=bool(data.get("interactable", False)), actions=list(data.get("actions", [])),
            properties=dict(data.get("properties", {})), position=data.get("position"),
            cell=data.get("cell"), text_key=data.get("text_key", ""), text=data.get("text", ""),
            focused=bool(data.get("focused", False)), components=list(data.get("components", [])),
            raw=data,
        )


@dataclass(frozen=True)
class Event:
    seq: int
    type: str
    frame: int
    t_ms: int
    data: dict[str, Any]

    @classmethod
    def from_payload(cls, data: dict[str, Any]) -> "Event":
        return cls(int(data["seq"]), data["type"], int(data.get("frame", 0)), int(data.get("t_ms", 0)), dict(data.get("data", {})))


@dataclass(frozen=True)
class Capabilities:
    protocol: str
    version: str
    profile: str
    capabilities: list[str]
    methods: list[str]
    engine: dict[str, Any]
    events: list[str]
    limits: dict[str, Any]
    fixtures: list[str]
    raw: dict[str, Any] = field(default_factory=dict, repr=False)

    @classmethod
    def from_payload(cls, data: dict[str, Any]) -> "Capabilities":
        return cls(
            protocol=data["protocol"], version=data["version"], profile=data["profile"],
            capabilities=list(data["capabilities"]), methods=list(data["methods"]),
            engine=dict(data["engine"]), events=list(data.get("events", [])),
            limits=dict(data.get("limits", {})), fixtures=list(data.get("fixtures", [])), raw=data,
        )

    def has(self, capability: str) -> bool:
        return capability in self.capabilities

    def supports_action(self, action: str) -> bool:
        return bool(self.engine.get("semantic_actions", {}).get(action, False))

    @property
    def screenshots(self) -> bool:
        return self.has("observe.screenshot")


class _Ref:
    def __init__(self, source: str, path: str, entity_id: str | None = None):
        self._base: dict[str, Any] = {"source": source, "path": path}
        if entity_id is not None:
            self._base["id"] = entity_id

    def _op(self, op: str, value: Any = None, with_value: bool = True) -> Condition:
        cond: dict[str, Any] = dict(self._base, op=op)
        if with_value:
            cond["value"] = value
        return cond  # type: ignore[return-value]

    def eq(self, value: Any) -> Condition: return self._op("eq", value)
    def ne(self, value: Any) -> Condition: return self._op("ne", value)
    def gt(self, value: float) -> Condition: return self._op("gt", value)
    def gte(self, value: float) -> Condition: return self._op("gte", value)
    def lt(self, value: float) -> Condition: return self._op("lt", value)
    def lte(self, value: float) -> Condition: return self._op("lte", value)
    def contains(self, value: Any) -> Condition: return self._op("contains", value)
    def is_in(self, values: list[Any]) -> Condition: return self._op("in", values)
    def exists(self) -> Condition: return self._op("exists", with_value=False)
    def not_exists(self) -> Condition: return self._op("not_exists", with_value=False)


class Cond:
    """Condition builder: Cond.state("unit.cell").eq([0, 10])."""

    @staticmethod
    def state(path: str) -> _Ref:
        return _Ref("state", path)

    @staticmethod
    def scene(path: str) -> _Ref:
        return _Ref("scene", path)

    @staticmethod
    def entity(entity_id: str, path: str) -> _Ref:
        return _Ref("entity", path, entity_id)

    @staticmethod
    def all(*conditions: Condition) -> Condition:
        return {"all": list(conditions)}

    @staticmethod
    def any(*conditions: Condition) -> Condition:
        return {"any": list(conditions)}

    @staticmethod
    def not_(condition: Condition) -> Condition:
        return {"not": condition}  # type: ignore[typeddict-unknown-key]

#!/usr/bin/env python3
"""Guard scripts: mechanical checks for rules in docs/AI_INSTRUCTIONS.md.

Every rule here exists because an AI (or a human) made the same mistake more
than once. Each finding names its rule id; the id is listed with the rule in
docs/AI_INSTRUCTIONS.md ("Guarded rules") and in docs/MISTAKES.md.

Usage: python3 tools/guards/check_rules.py [repo_root]   (exit 1 on findings)
Adding a guard: write a function below, register it in GUARDS, add a test in
tools/guards/tests/, and document it (see docs/AI_WORKFLOW.md).
Standard library only.
"""

from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable

GAME_DIRS = ("autoload", "core", "features", "scenes")
CODE_DIRS = GAME_DIRS + ("automation", "tests")
AUTOLOADS = ("SettingsService", "InputMethodService", "SaveService", "GameSession", "SceneRouter")
GODOT_LIFECYCLE = ("_Ready", "_EnterTree", "_ExitTree", "_Process", "_PhysicsProcess", "_Input", "_UnhandledInput", "_Notification")


@dataclass(frozen=True)
class Finding:
    rule: str
    path: str
    line: int
    message: str

    def __str__(self) -> str:
        return f"{self.rule} {self.path}:{self.line}: {self.message}"


def _files(root: Path, dirs: Iterable[str], suffix: str) -> Iterable[Path]:
    for name in dirs:
        base = root / name
        if base.is_dir():
            yield from sorted(p for p in base.rglob(f"*{suffix}") if ".godot" not in p.parts)


def _rel(root: Path, path: Path) -> str:
    return path.relative_to(root).as_posix()


def _code_lines(path: Path) -> list[tuple[int, str]]:
    """Lines with // comments stripped (string contents are kept)."""
    out = []
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        stripped = line.lstrip()
        if stripped.startswith("//"):
            continue
        out.append((number, line))
    return out


def no_gdscript(root: Path) -> list[Finding]:
    """G001: the project is C# only (ADR 0011)."""
    findings = []
    for path in sorted(root.rglob("*.gd")):
        if ".godot" in path.parts or "node_modules" in path.parts:
            continue
        findings.append(Finding("G001", _rel(root, path), 1, "GDScript file in a C#-only project; write it in C#"))
    return findings


def godot_class_matches_file(root: Path) -> list[Finding]:
    """G002: Godot only attaches a C# script whose class name equals its file name."""
    findings = []
    pattern = re.compile(r"^\s*(?:public|internal)\s+(?:sealed\s+|abstract\s+)?partial\s+class\s+(\w+)\s*:", re.M)
    for path in _files(root, CODE_DIRS, ".cs"):
        match = pattern.search(path.read_text(encoding="utf-8"))
        if match and match.group(1) != path.stem:
            line = path.read_text(encoding="utf-8")[: match.start()].count("\n") + 1
            findings.append(Finding("G002", _rel(root, path), line,
                                    f"partial class {match.group(1)} must live in {match.group(1)}.cs"))
    return findings


def autoload_signals_are_disconnected(root: Path) -> list[Finding]:
    """G003: C# does not auto-disconnect a freed node from a longer-lived emitter.

    Every `<Autoload>.Instance.<Signal> += handler` needs the matching `-=` in
    the same file (normally _EnterTree / _ExitTree), or callbacks hit freed nodes.
    """
    findings = []
    subscribe = re.compile(r"(\b(?:%s)\.Instance\.\w+)\s*\+=\s*([\w.]+)\s*;" % "|".join(AUTOLOADS))
    for path in _files(root, GAME_DIRS, ".cs"):
        text = path.read_text(encoding="utf-8")
        for number, line in _code_lines(path):
            for match in subscribe.finditer(line):
                target, handler = match.group(1), match.group(2)
                if not re.search(re.escape(target) + r"\s*-=\s*" + re.escape(handler) + r"\s*;", text):
                    findings.append(Finding("G003", _rel(root, path), number,
                                            f"{target} += {handler} has no matching -= (disconnect in _ExitTree)"))
        for number, line in _code_lines(path):
            if re.search(r"\b(?:%s)\.Instance\.\w+\s*\+=\s*\(" % "|".join(AUTOLOADS), line):
                findings.append(Finding("G003", _rel(root, path), number,
                                        "lambda subscribed to an autoload signal cannot be disconnected; use a named method"))
    return findings


def no_hard_coded_node_paths(root: Path) -> list[Finding]:
    """G004: no GetNode("../..") climbing or /root/ lookups in game code (exported references instead)."""
    findings = []
    pattern = re.compile(r"GetNode(?:OrNull)?(?:<[^>]*>)?\(\s*\"(?:\.\.|/root)")
    for path in _files(root, GAME_DIRS, ".cs"):
        for number, line in _code_lines(path):
            if pattern.search(line):
                findings.append(Finding("G004", _rel(root, path), number, "hard-coded node path; use an [Export] reference or a signal"))
    return findings


def scene_changes_go_through_router(root: Path) -> list[Finding]:
    """G005: only SceneRouter may change the top-level scene."""
    findings = []
    for path in _files(root, GAME_DIRS + ("automation",), ".cs"):
        if path.name == "SceneRouter.cs":
            continue
        for number, line in _code_lines(path):
            if re.search(r"\bChangeSceneTo(?:Packed|File|Node)\s*\(", line):
                findings.append(Finding("G005", _rel(root, path), number, "scene change outside SceneRouter; call SceneRouter.Instance.Goto(route)"))
    return findings


def async_void_only_for_engine_callbacks(root: Path) -> list[Finding]:
    """G006: `async void` swallows exceptions; only Godot lifecycle overrides may use it."""
    findings = []
    pattern = re.compile(r"\basync\s+void\s+(\w+)\s*\(")
    for path in _files(root, CODE_DIRS, ".cs"):
        for number, line in _code_lines(path):
            match = pattern.search(line)
            if match and match.group(1) not in GODOT_LIFECYCLE:
                findings.append(Finding("G006", _rel(root, path), number,
                                        f"async void {match.group(1)}: return Task (fire-and-forget with `_ = ...`)"))
    return findings


def no_scene_paths_in_game_code(root: Path) -> list[Finding]:
    """G007: scenes are reached through route ids or exported PackedScene fields, never by path."""
    findings = []
    for path in _files(root, GAME_DIRS, ".cs"):
        for number, line in _code_lines(path):
            if re.search(r"\"res://[^\"]+\.tscn\"", line):
                findings.append(Finding("G007", _rel(root, path), number,
                                        "scene path in code; add a route (route_table.tres) or an exported PackedScene"))
    return findings


GUARDS: list[Callable[[Path], list[Finding]]] = [
    no_gdscript,
    godot_class_matches_file,
    autoload_signals_are_disconnected,
    no_hard_coded_node_paths,
    scene_changes_go_through_router,
    async_void_only_for_engine_callbacks,
    no_scene_paths_in_game_code,
]


def run(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    for guard in GUARDS:
        findings.extend(guard(root))
    return findings


def main(argv: list[str]) -> int:
    root = Path(argv[1]).resolve() if len(argv) > 1 else Path(__file__).resolve().parents[2]
    findings = run(root)
    for finding in findings:
        print(finding)
    print(f"{len(findings)} guard finding(s) from {len(GUARDS)} guards")
    if findings:
        print("Rules: docs/AI_INSTRUCTIONS.md (Guarded rules). History: docs/MISTAKES.md.")
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

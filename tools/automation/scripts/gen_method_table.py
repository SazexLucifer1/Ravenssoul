#!/usr/bin/env python3
"""Regenerate the method table in docs/AUTOMATION.md from automation/protocol/methods.json."""

import json
import re
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]


def params(spec: dict) -> str:
    props = spec["params"].get("properties", {})
    required = set(spec["params"].get("required", []))
    out = []
    for key, value in props.items():
        kind = value.get("type", "")
        if "enum" in value:
            kind = "|".join(value["enum"]) if len(value["enum"]) <= 6 else f"{kind} (enum)"
        out.append((f"**{key}**" if key in required else key) + ": " + kind)
    return "<br>".join(out) or "—"


def main() -> None:
    methods = json.loads((REPO / "automation/protocol/methods.json").read_text())["methods"]
    rows = ["| Method | Capability | Params (**required**) | Result | Purpose |", "|---|---|---|---|---|"]
    for name, spec in methods.items():
        rows.append(f"| `{name}` | `{spec['capability']}` | {params(spec)} | {spec['result'].replace('|', '/')} | {spec['summary']} |")
    doc_path = REPO / "docs/AUTOMATION.md"
    doc = doc_path.read_text()
    doc = re.sub(r"<!-- methods:start -->.*<!-- methods:end -->",
                 "<!-- methods:start -->\n" + "\n".join(rows) + "\n<!-- methods:end -->", doc, flags=re.S)
    doc_path.write_text(doc)
    print(f"wrote {len(methods)} methods to {doc_path.relative_to(REPO)}")


if __name__ == "__main__":
    main()

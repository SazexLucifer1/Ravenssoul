"""Command-line front end: `python -m utopia_automation <command>` (or `utopia-automation`).

Connection: --url/--token or env UTOPIA_AUTOMATION_URL / UTOPIA_AUTOMATION_TOKEN.
Exit codes: 0 success, 1 automation/transport error, 2 assertion failed.
"""

from __future__ import annotations

import argparse
import json
import signal
import sys
from pathlib import Path
from typing import Any

from .client import AutomationClient
from .errors import AutomationAssertionError, AutomationError
from .launcher import GameProcess


def _print(value: Any) -> None:
    if hasattr(value, "raw"):
        value = value.raw
    elif isinstance(value, list) and value and hasattr(value[0], "raw"):
        value = [v.raw for v in value]
    print(json.dumps(value, indent=2, default=str))


def _json_arg(text: str) -> Any:
    try:
        return json.loads(text)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(f"not valid JSON: {exc}") from exc


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="utopia-automation", description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--url", help="automation base URL (default env or http://127.0.0.1:47801)")
    parser.add_argument("--token", help="bearer token (default env UTOPIA_AUTOMATION_TOKEN)")
    sub = parser.add_subparsers(dest="command", required=True)

    launch = sub.add_parser("launch", help="start the game with automation and print connection info")
    launch.add_argument("--godot", required=True)
    launch.add_argument("--project", default=".")
    launch.add_argument("--profile", choices=["dev", "qa"], default="qa")
    launch.add_argument("--headless", action="store_true")
    launch.add_argument("--port", type=int)
    launch.add_argument("--log", help="write game output to this file")

    for name in ("caps", "schema", "state", "scene", "actions", "perf", "fixtures", "release-all", "hello"):
        sub.add_parser(name)
    query = sub.add_parser("query")
    query.add_argument("--role")
    query.add_argument("--tag")
    query.add_argument("--id-prefix")
    query.add_argument("--interactable", action="store_true")
    entity = sub.add_parser("entity")
    entity.add_argument("id")
    entity.add_argument("--deep", action="store_true")
    act = sub.add_parser("act", help="semantic action, e.g. act activate --target main_menu.new_game")
    act.add_argument("action")
    act.add_argument("--target")
    act.add_argument("--args", type=_json_arg, default={})
    key = sub.add_parser("key")
    key.add_argument("key")
    key.add_argument("--mode", choices=["tap", "down", "up"], default="tap")
    pad = sub.add_parser("gamepad")
    pad.add_argument("button")
    pad.add_argument("--mode", choices=["tap", "down", "up"], default="tap")
    click = sub.add_parser("click")
    click.add_argument("x", type=float)
    click.add_argument("y", type=float)
    wait = sub.add_parser("wait", help='wait for a condition, e.g. wait \'{"source":"scene","path":"route","op":"eq","value":"gameplay"}\'')
    wait.add_argument("condition", type=_json_arg)
    wait.add_argument("--timeout-ms", type=int, default=10000)
    sub.add_parser("settle")
    check = sub.add_parser("assert")
    check.add_argument("condition", type=_json_arg)
    check.add_argument("--message", default="")
    shot = sub.add_parser("screenshot")
    shot.add_argument("out")
    shot.add_argument("--max-width", type=int)
    logs = sub.add_parser("logs")
    logs.add_argument("--since", type=int, default=0)
    logs.add_argument("--min-level", choices=["info", "warning", "error"])
    events = sub.add_parser("events")
    events.add_argument("--since", type=int, default=0)
    events.add_argument("--types", help="comma separated")
    fixture = sub.add_parser("fixture")
    fixture.add_argument("name")
    fixture.add_argument("--write-save", action="store_true")
    reset = sub.add_parser("reset")
    reset.add_argument("--route", choices=["main_menu", "gameplay"])
    quit_cmd = sub.add_parser("quit")
    quit_cmd.add_argument("--exit-code", type=int, default=0)

    scenario = sub.add_parser("scenario", help="run a bundled end-to-end scenario")
    scenario.add_argument("name", choices=["first_mission"])
    scenario.add_argument("--godot", required=True)
    scenario.add_argument("--project", default=".")
    scenario.add_argument("--out", default="automation-output")
    scenario.add_argument("--headless", action="store_true")
    scenario.add_argument("--require-screenshot", action="store_true")
    return parser


def run(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        return _dispatch(args)
    except AutomationAssertionError as exc:
        print(f"ASSERTION FAILED: {exc}", file=sys.stderr)
        return 2
    except AutomationError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


def _dispatch(args: argparse.Namespace) -> int:
    if args.command == "launch":
        game = GameProcess(args.godot, args.project, profile=args.profile, headless=args.headless,
                           port=args.port, log_path=args.log).start()
        print(json.dumps({"url": game.url, "token": game.token, "pid": game.process.pid if game.process else None}), flush=True)
        signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
        try:
            return game.wait(timeout_s=10**9)
        except KeyboardInterrupt:
            return game.stop() or 0
    if args.command == "scenario":
        from .scenario_first_mission import main as scenario_main
        return scenario_main(["--godot", args.godot, "--project", args.project, "--out", args.out]
                             + (["--headless"] if args.headless else [])
                             + (["--require-screenshot"] if args.require_screenshot else []))

    client = AutomationClient(args.url, args.token)
    c = args.command
    if c == "hello": _print(client.hello())
    elif c == "caps": _print(client.capabilities())
    elif c == "schema": _print(client.schema())
    elif c == "state": _print(client.state())
    elif c == "scene": _print(client.scene())
    elif c == "actions": _print(client.available_actions())
    elif c == "perf": _print(client.perf())
    elif c == "fixtures": _print(client.fixtures())
    elif c == "release-all": _print({"released": client.release_all()})
    elif c == "query": _print(client.query(role=args.role, tag=args.tag, id_prefix=args.id_prefix, interactable_only=args.interactable or None))
    elif c == "entity": _print(client.entity(args.id, args.deep))
    elif c == "act": _print(client.perform(args.action, args.target, **args.args))
    elif c == "key": _print({"held": client.key(args.key, args.mode)})
    elif c == "gamepad": _print({"held": client.gamepad_button(args.button, args.mode)})
    elif c == "click": _print({"held": client.click(args.x, args.y)})
    elif c == "wait": _print(client.wait_until(args.condition, args.timeout_ms))
    elif c == "settle": client.wait_settled(); _print({"settled": True})
    elif c == "assert": _print({"passed": True, "actual": client.assert_that(args.condition, args.message)})
    elif c == "screenshot": _print({"saved": str(client.screenshot(Path(args.out), args.max_width))})
    elif c == "logs": _print(client.logs(args.since, args.min_level))
    elif c == "events":
        events, last = client.events(args.since, args.types.split(",") if args.types else None)
        _print({"events": [e.__dict__ for e in events], "last_seq": last})
    elif c == "fixture": _print(client.load_fixture(args.name, args.write_save))
    elif c == "reset": _print(client.reset(args.route))
    elif c == "quit": client.quit(args.exit_code); _print({"quitting": True})
    return 0


def main() -> None:
    sys.exit(run())


if __name__ == "__main__":
    main()

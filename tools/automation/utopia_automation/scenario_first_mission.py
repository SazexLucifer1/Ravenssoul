"""End-to-end scenario: launch -> title screen -> first mission -> victory -> quit.

Exercises discovery, semantic actions, raw keyboard and gamepad input,
deterministic waits, events, checkpoints, assertions on authoritative state,
recording, logs, a screenshot, and a clean exit.

    python -m utopia_automation scenario first_mission --godot $GODOT --project . --require-screenshot
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from .client import AutomationClient
from .errors import AutomationError
from .launcher import GameProcess
from .models import Cond

MISSION = "mission_first_spark"
UNIT = "unit.hero_deserter"
SPAWN = [1, 10]
OBJECTIVE = [10, 1]
SAFE_PATH = ["right"] * 9 + ["up"] * 9  # row 10 east, then column 10 north (no walls or hazards)


def step(name: str) -> None:
    print(f"- {name}", flush=True)


def run(client: AutomationClient, out_dir: Path, require_screenshot: bool) -> dict[str, Any]:
    out_dir.mkdir(parents=True, exist_ok=True)
    caps = client.capabilities()
    assert caps.protocol == "utopia-automation" and caps.version.split(".")[0] == "1", caps.version
    assert caps.supports_action("move") and not caps.supports_action("equip")
    step(f"connected: profile={caps.profile} engine={caps.engine['engine']} {caps.engine['engine_version']} screenshots={caps.screenshots}")

    client.wait_for_route("main_menu", 20000)
    client.start_recording()

    step("discover title screen controls by stable id")
    ids = {e.id for e in client.query(interactable_only=True)}
    assert {"main_menu.new_game", "main_menu.settings", "main_menu.quit"} <= ids, ids
    new_game = client.entity("main_menu.new_game")
    assert new_game.text_key == "MAIN_MENU_NEW_GAME" and "primary" in new_game.tags, new_game

    step("raw keyboard focus navigation (Down/Up)")
    client.key("Down")
    client.wait_settled()
    client.assert_that(Cond.scene("focused").eq("main_menu.settings"), "Down moves focus to Settings")
    client.key("Up")
    client.wait_settled()
    client.assert_that(Cond.scene("focused").eq("main_menu.new_game"), "Up returns to New Campaign")

    step("semantic action: activate New Campaign, wait for the route event")
    _, last_seq = client.events()
    client.activate("main_menu.new_game")
    client.wait_event("route.changed", {"route": "gameplay"}, after_seq=last_seq, timeout_ms=15000)
    client.wait_settled()
    client.assert_that(Cond.state("unit.cell").eq(SPAWN), "unit starts on the spawn cell")
    client.checkpoint("mission_start")

    step("raw keyboard: Left arrow moves the unit")
    client.key("Left")
    client.wait_settled()
    client.assert_that(Cond.state("unit.cell").eq([0, 10]), "keyboard Left moved one cell west")

    step("raw gamepad: D-pad right moves back; input method switches to gamepad")
    client.gamepad_button("dpad_right")
    client.wait_settled()
    client.assert_that(Cond.all(Cond.state("unit.cell").eq(SPAWN), Cond.state("input_method").eq("gamepad")))

    step("held input is tracked and released")
    held = client.key("Shift", mode="down")
    assert "key:Shift" in held, held
    assert client.release_all() >= 1
    client.assert_that(Cond.state("held_inputs").eq([]))

    step("raw gamepad Start pauses, B resumes")
    client.gamepad_button("start")
    client.wait_until(Cond.all(Cond.scene("paused").eq(True), Cond.scene("screens").contains("screen.pause")))
    client.assert_that(Cond.entity("pause.resume", "focused").eq(True), "Resume has focus")
    client.gamepad_button("b")
    client.wait_until(Cond.scene("paused").eq(False))

    step("semantic move along the safe path to the objective")
    result = client.move(path=SAFE_PATH)
    assert result["steps"] == len(SAFE_PATH) and result["cell"] == OBJECTIVE, result
    client.wait_event("mission.finished", {"victory": True}, after_seq=last_seq)
    client.wait_until(Cond.state("mission.save_state").eq("saved"), 10000)

    step("verify authoritative state")
    client.assert_that(Cond.state("campaign.wallet.gold").eq(50), "victory reward granted")
    client.assert_that(Cond.state("campaign.completed_missions").contains(MISSION))
    client.assert_that(Cond.state("save.has_save").eq(True), "autosave written (isolated automation save folder)")
    client.assert_that(Cond.entity("result.title", "text_key").eq("RESULT_VICTORY_TITLE"))
    client.assert_that(Cond.entity("result.continue", "interactable").eq(True))
    client.assert_that(Cond.entity(f"{UNIT}/health", "properties.current").eq(20), "safe path took no damage")

    screenshot: str | None = None
    if caps.screenshots:
        screenshot = str(client.screenshot(out_dir / "first_mission_victory.png"))
        step(f"screenshot saved: {screenshot}")
    elif require_screenshot:
        raise AutomationError("screenshots are unavailable (headless run) but --require-screenshot was set")
    else:
        step("screenshot skipped (headless)")

    step("checkpoint restore returns to the mission start")
    client.restore("mission_start")
    client.wait_settled()
    client.assert_that(Cond.state("unit.cell").eq(SPAWN))

    recording = client.stop_recording()
    (out_dir / "first_mission_recording.json").write_text(json.dumps(recording, indent=1))
    errors = client.logs(min_level="error")["lines"]
    assert not errors, f"game logged errors: {errors}"
    events, _ = client.events()
    (out_dir / "first_mission_events.json").write_text(json.dumps([e.__dict__ for e in events], indent=1))
    return {"screenshot": screenshot, "recorded_entries": len(recording), "events": len(events)}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--godot", required=True)
    parser.add_argument("--project", default=".")
    parser.add_argument("--out", default="automation-output")
    parser.add_argument("--headless", action="store_true")
    parser.add_argument("--require-screenshot", action="store_true")
    args = parser.parse_args(argv)
    out_dir = Path(args.out)
    game = GameProcess(args.godot, args.project, profile="qa", headless=args.headless, log_path=out_dir / "game.log")
    with game:
        with game.client() as client:
            summary = run(client, out_dir, args.require_screenshot)
        step("quit cleanly")
        game.client().quit()
        exit_code = game.wait()
    print(json.dumps({"result": "passed", "exit_code": exit_code, **summary}), flush=True)
    log = (out_dir / "game.log").read_text(errors="replace")
    if exit_code != 0 or "SCRIPT ERROR" in log or "leaked" in log:
        print("game did not exit cleanly; see game.log", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())

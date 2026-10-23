#!/usr/bin/env bash
# Runs everything CI runs: guards, C# build, import, automated tests, and boot smoke checks.
# Usage: GODOT=/path/to/godot-mono tests/run_tests.sh [--filter=<test label substring>]
# GODOT must be the .NET ("mono") build of Godot 4.6; the .NET 8 SDK must be on PATH.
set -euo pipefail
GODOT="${GODOT:-godot}"
cd "$(dirname "$0")/.."

echo "== Guard scripts (rules from docs/AI_INSTRUCTIONS.md)"
python3 tools/guards/check_rules.py

echo "== C# build (warnings are errors)"
dotnet build Utopia.csproj --nologo -v quiet

echo "== Import project"
"$GODOT" --headless --import >/dev/null 2>&1 || true

echo "== Automated tests"
set +e
test_out="$("$GODOT" --headless --audio-driver Dummy res://tests/framework/test_runner.tscn -- "$@" 2>&1)"
status=$?
set -e
echo "$test_out"
if [[ $status -ne 0 ]]; then echo "Tests failed" >&2; exit $status; fi
if grep -q "leaked" <<<"$test_out"; then
  echo "Tests leaked objects (suspended coroutine or orphan node); run with --verbose" >&2; exit 1
fi

if [[ $# -gt 0 ]]; then exit 0; fi

echo "== Localization key extraction"
"$GODOT" --headless --audio-driver Dummy res://tests/tools/extract_keys.tscn

smoke() {
  local expected="$1"; shift
  local out
  out="$("$GODOT" --headless --audio-driver Dummy --quit-after 300 -- "$@" 2>&1)"
  echo "$out"
  if grep -qE "ERROR|Parse Error|WARNING" <<<"$out"; then
    echo "Smoke run ($expected) logged errors or warnings" >&2; exit 1
  fi
  if ! grep -q "Entered route '$expected'" <<<"$out"; then
    echo "Smoke run never reached '$expected'" >&2; exit 1
  fi
}
echo "== Guard script self-tests"
python3 -m unittest discover -s tools/guards/tests -q

echo "== Automation client unit tests (Python, stdlib only)"
(cd tools/automation && python3 -m unittest discover -s tests -t . -q)

echo "== Automation end-to-end scenario (headless; CI also runs it under Xvfb with a screenshot)"
PYTHONPATH=tools/automation python3 -m utopia_automation scenario first_mission \
  --godot "$GODOT" --project . --out "${AUTOMATION_OUT:-build/automation}" --headless

echo "== Production gating: the production profile must never open a socket"
gate_out="$("$GODOT" --headless --audio-driver Dummy --quit-after 120 -- --automation --automation-profile=production 2>&1)"
if ! grep -q "Automation server not started" <<<"$gate_out" || grep -q "listening on" <<<"$gate_out"; then
  echo "$gate_out"; echo "production profile was not refused" >&2; exit 1
fi

echo "== Smoke: bootstrap -> main menu"
smoke main_menu
echo "== Smoke: bootstrap -> gameplay"
smoke gameplay --route=gameplay
echo "All checks passed."

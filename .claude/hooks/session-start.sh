#!/usr/bin/env bash
# Claude Code SessionStart hook: in cloud sessions, install Godot .NET + .NET 8
# so `tests/run_tests.sh` works immediately, and export GODOT/DOTNET_ROOT.
set -euo pipefail
[[ "${CLAUDE_CODE_REMOTE:-}" == "true" ]] || exit 0
cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
out="$(bash tools/setup/install_toolchain.sh)"
godot="$(sed -n 's/^GODOT=//p' <<<"$out")"
dotnet_root="$(sed -n 's/^DOTNET_ROOT=//p' <<<"$out")"
if [[ -n "${CLAUDE_ENV_FILE:-}" ]]; then
  {
    echo "export GODOT=\"$godot\""
    echo "export DOTNET_ROOT=\"$dotnet_root\""
    echo "export PATH=\"$dotnet_root:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1"
  } >> "$CLAUDE_ENV_FILE"
fi
echo "Toolchain ready: GODOT=$godot"

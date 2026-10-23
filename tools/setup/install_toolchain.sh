#!/usr/bin/env bash
# Installs the toolchain this project needs on Linux x86_64:
#   - Godot 4.6 .NET ("mono") build  -> $GODOT_DIR (default /opt/godot)
#   - .NET 8 SDK                      -> $DOTNET_DIR (default /opt/dotnet)
# Idempotent: skips what is already installed. Prints the two environment
# variables to use (GODOT, DOTNET_ROOT). Used by cloud sessions (Claude Code,
# Codex) and CI; see docs/AI_WORKFLOW.md.
set -euo pipefail

GODOT_VERSION="${GODOT_VERSION:-4.6-stable}"
GODOT_DIR="${GODOT_DIR:-/opt/godot}"
DOTNET_DIR="${DOTNET_DIR:-/opt/dotnet}"
GODOT_NAME="Godot_v${GODOT_VERSION}_mono_linux_x86_64"
GODOT_BIN="$GODOT_DIR/$GODOT_NAME/Godot_v${GODOT_VERSION}_mono_linux.x86_64"

mkdir -p "$GODOT_DIR" "$DOTNET_DIR"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Installing Godot ${GODOT_VERSION} (.NET) ..."
  tmp="$(mktemp -d)"
  curl -sSfL -o "$tmp/godot.zip" \
    "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}/${GODOT_NAME}.zip"
  unzip -q "$tmp/godot.zip" -d "$GODOT_DIR"
  rm -rf "$tmp"
fi

if [[ ! -x "$DOTNET_DIR/dotnet" ]] || ! "$DOTNET_DIR/dotnet" --list-sdks | grep -q '^8\.'; then
  echo "Installing .NET 8 SDK ..."
  tmp="$(mktemp -d)"
  curl -sSfL -o "$tmp/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh
  bash "$tmp/dotnet-install.sh" --channel 8.0 --install-dir "$DOTNET_DIR" >/dev/null
  rm -rf "$tmp"
fi

echo "GODOT=$GODOT_BIN"
echo "DOTNET_ROOT=$DOTNET_DIR"
echo "Add to your shell: export GODOT=$GODOT_BIN DOTNET_ROOT=$DOTNET_DIR PATH=$DOTNET_DIR:\$PATH"

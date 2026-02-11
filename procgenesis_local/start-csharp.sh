#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CSPROJ_DIR="$SCRIPT_DIR/csharp"

cd "$CSPROJ_DIR"

if command -v godot4-mono >/dev/null 2>&1; then
  exec godot4-mono --path .
elif command -v godot4 >/dev/null 2>&1; then
  exec godot4 --path .
elif command -v godot >/dev/null 2>&1; then
  exec godot --path .
else
  echo "Error: Godot is not found in PATH. Please install Godot 4 (.NET), then run again." >&2
  exit 1
fi

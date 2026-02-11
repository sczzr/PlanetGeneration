#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WEB_DIR="$SCRIPT_DIR/web"

cd "$WEB_DIR"

if command -v python3 >/dev/null 2>&1; then
  echo "Starting web server with Python at http://localhost:8000"
  exec python3 server.py
elif command -v node >/dev/null 2>&1; then
  echo "Starting web server with Node.js at http://localhost:8080"
  exec node server.js
else
  echo "Error: python3 or node is required to run the web server." >&2
  exit 1
fi

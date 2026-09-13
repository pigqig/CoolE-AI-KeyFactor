#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

if [ ! -x "$ROOT/python/.venv/bin/python" ]; then
  python3 -m venv "$ROOT/python/.venv"
  "$ROOT/python/.venv/bin/pip" install -r "$ROOT/python/requirements.txt"
fi

cd "$ROOT"
dotnet run --project src/KeyFactorDashboard --urls http://127.0.0.1:43123

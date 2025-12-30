#!/usr/bin/env bash
set -euo pipefail

ROOT=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
cd "$ROOT"
dotnet run --project DevTools/HandoffBridge/HandoffBridge.csproj -- export --root "$ROOT" "$@"

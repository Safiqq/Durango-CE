#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$REPO_ROOT/.work/client"
LOG_FILE="${DURANGO_LOG:-/tmp/durango_v336_wine.log}"

if ! command -v wine64 >/dev/null 2>&1; then
  echo "[error] wine64 not found on PATH. Install Wine (e.g. apt install wine64)." >&2
  exit 1
fi

if [ ! -f "$CLIENT_DIR/DurangoV2.exe" ]; then
  echo "[play] Sanitized client not found; running scripts/setup-client.py..."
  python3 "$REPO_ROOT/scripts/setup-client.py"
fi

echo "[play] Launching sanitized DurangoV2.exe via wine64 (log: $LOG_FILE)"
cd "$CLIENT_DIR"
exec env \
  WINEDEBUG="${WINEDEBUG:--all}" \
  WINEESYNC="${WINEESYNC:-1}" \
  WINEFSYNC="${WINEFSYNC:-1}" \
  DXVK_STATE_CACHE="${DXVK_STATE_CACHE:-1}" \
  DXVK_LOG_LEVEL="${DXVK_LOG_LEVEL:-none}" \
  wine64 "$CLIENT_DIR/DurangoV2.exe" \
  -force-d3d11 \
  -screen-fullscreen 0 \
  -screen-width 1280 \
  -screen-height 720 \
  -logFile "Z:${LOG_FILE//\//\\}" "$@"

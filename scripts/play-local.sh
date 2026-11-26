#!/usr/bin/env bash
# Build + run local Durango-CE Rust server (:8190/:8191) and launch sanitized Unity client.
# Optionally merges a remote VM server list (/servers.json) so "Select Server" shows BOTH
# your local server and your VM / active trusted servers in one launcher.
#
# Usage:
#   ./scripts/play-local.sh                              # Local server (+ saved .work/vm-servers-url if set)
#   ./scripts/play-local.sh --vm <VM_IP_OR_SERVERS_URL>  # Merge VM servers.json (saved for future runs)
#   ./scripts/play-local.sh --no-vm                      # Clear saved VM URL and run local-only
#
# Env equivalents:
#   DURANGO_VM_SERVERS (comma-separated IPs or servers.json URLs)
#   DURANGO_HTTP_BIND (default: 127.0.0.1:8190)

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

export PATH="$HOME/.cargo/bin:$PATH"

VM_STATE_FILE=".work/vm-servers-url"
VM_ARG=""
CLEAR_VM=0
CLIENT_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --vm|--servers-url)
      if [[ $# -lt 2 ]]; then
        echo "[play-local] ERROR: $1 requires a VM host/IP or servers.json URL." >&2
        exit 1
      fi
      VM_ARG="$2"
      shift 2
      ;;
    --no-vm|--local-only)
      CLEAR_VM=1
      shift
      ;;
    -h|--help)
      sed -n '2,15p' "$0"
      exit 0
      ;;
    --)
      shift
      CLIENT_ARGS+=("$@")
      break
      ;;
    *)
      CLIENT_ARGS+=("$1")
      shift
      ;;
  esac
done

mkdir -p .work
if [[ "$CLEAR_VM" -eq 1 ]]; then
  rm -f "$VM_STATE_FILE"
  VM_SERVERS=""
elif [[ -n "$VM_ARG" ]]; then
  VM_SERVERS="$VM_ARG"
  printf '%s\n' "$VM_SERVERS" > "$VM_STATE_FILE"
elif [[ -n "${DURANGO_VM_SERVERS:-}" ]]; then
  VM_SERVERS="$DURANGO_VM_SERVERS"
elif [[ -n "${DURANGO_UPSTREAM_SERVERS_URLS:-}" ]]; then
  VM_SERVERS="$DURANGO_UPSTREAM_SERVERS_URLS"
elif [[ -f "$VM_STATE_FILE" ]]; then
  VM_SERVERS="$(tr -d '\r\n' < "$VM_STATE_FILE")"
else
  VM_SERVERS=""
fi

if [[ ! -f ".work/client/DurangoV2.exe" ]]; then
  echo "[play-local] Setting up and sanitizing v3.3.6 Unity client into .work/client/..."
  python3 scripts/setup-client.py
else
  MOD_DLL=".work/client/DurangoV2_Data/Managed/DurangoCE.ClientMod.dll"
  if [[ ! -f "$MOD_DLL" || "client-mod/HudPolishMod.cs" -nt "$MOD_DLL" || "client-mod/AutoPlaytestMod.cs" -nt "$MOD_DLL" || "client-mod/DurangoCE.ClientMod.csproj" -nt "$MOD_DLL" || "scripts/sanitize_client.py" -nt "$MOD_DLL" ]]; then
    echo "[play-local] Updating DurangoCE.ClientMod.dll / sanitized client assemblies..."
    python3 scripts/setup-client.py --mod-only
  fi
fi

echo "[play-local] Building cleanroom Rust server (durango-server)..."
cargo build -p durango-server

TARGET_DIR="${CARGO_TARGET_DIR:-target}"
SERVER_BIN="$TARGET_DIR/debug/durango-server"
mkdir -p .work/logs
SERVER_LOG=".work/logs/durango-server.log"
PID_FILE=".work/durango-server.pid"

if [[ -f "$PID_FILE" ]]; then
  OLD_PID="$(cat "$PID_FILE" 2>/dev/null || true)"
  if [[ -n "$OLD_PID" ]] && kill -0 "$OLD_PID" 2>/dev/null; then
    echo "[play-local] Stopping previous tracked durango-server instance (pid=$OLD_PID)..."
    kill "$OLD_PID" 2>/dev/null || true
    for _ in $(seq 1 20); do
      if ! kill -0 "$OLD_PID" 2>/dev/null; then
        break
      fi
      sleep 0.1
    done
  fi
  rm -f "$PID_FILE"
fi

HTTP_ADDR="${DURANGO_HTTP_BIND:-127.0.0.1:8190}"
HTTP_HOST="${HTTP_ADDR%:*}"
HTTP_PORT="${HTTP_ADDR##*:}"
if [[ "$HTTP_HOST" == "0.0.0.0" || -z "$HTTP_HOST" ]]; then
  HTTP_HOST="127.0.0.1"
fi
LOCAL_BASE="http://${HTTP_HOST}:${HTTP_PORT}"
LOCAL_SERVERS_URL="${LOCAL_BASE}/servers.json"

if [[ -n "$VM_SERVERS" ]]; then
  echo "[play-local] Starting local Rust server ($HTTP_ADDR) merged with VM directory: $VM_SERVERS"
else
  echo "[play-local] Starting local Rust server ($HTTP_ADDR) [local-only; pass --vm <IP_OR_URL> to include VM servers]..."
fi

DURANGO_UPSTREAM_SERVERS_URLS="$VM_SERVERS" "$SERVER_BIN" > "$SERVER_LOG" 2>&1 &
SRV_PID=$!
echo "$SRV_PID" > "$PID_FILE"

STOPPED=0
cleanup() {
  if [[ "$STOPPED" -eq 0 ]]; then
    STOPPED=1
    echo "[play-local] Stopping local Rust server (pid=$SRV_PID)..."
    kill "$SRV_PID" 2>/dev/null || true
    wait "$SRV_PID" 2>/dev/null || true
    rm -f "$PID_FILE"
  fi
}
trap cleanup EXIT INT TERM

STATUS_URL="${LOCAL_BASE}/status"
for _ in $(seq 1 50); do
  if ! kill -0 "$SRV_PID" 2>/dev/null; then
    echo "[play-local] ERROR: Rust server exited early ($HTTP_ADDR in use?). Log:"
    cat "$SERVER_LOG"
    exit 1
  fi
  if curl --noproxy '*' -sf "$STATUS_URL" >/dev/null 2>&1; then
    break
  fi
  sleep 0.2
done

if ! kill -0 "$SRV_PID" 2>/dev/null || ! curl --noproxy '*' -sf "$STATUS_URL" >/dev/null 2>&1; then
  echo "[play-local] ERROR: Rust server failed to start. Log:"
  cat "$SERVER_LOG"
  exit 1
fi

# Point client at unified /servers.json and cache merged server list to .work/client/servers.json
python3 - <<PY
import pathlib, urllib.request

client_dir = pathlib.Path(".work/client")
offserver = client_dir / "offserver.txt"
servers_file = client_dir / "servers.json"

preserved_lines = []
if offserver.exists():
    for raw in offserver.read_text(encoding="utf-8", errors="ignore").splitlines():
        s = raw.strip()
        if s and not s.startswith("#"):
            k = s.split("=", 1)[0].strip().lower()
            if k not in ("gateway", "name", "servers_url"):
                preserved_lines.append(s)

lines = [
    "# Durango-CE client configuration",
    "gateway=${LOCAL_BASE}",
    "name=Durango-CE Local Server",
    "servers_url=${LOCAL_SERVERS_URL}",
    *preserved_lines,
]
offserver.write_text("\n".join(lines) + "\n", encoding="utf-8")

try:
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with opener.open("${LOCAL_SERVERS_URL}", timeout=4) as resp:
        data = resp.read().decode("utf-8")
    if '"servers"' in data:
        servers_file.write_text(data, encoding="utf-8")
        print("[play-local] Synced merged server list ->", servers_file)
except Exception as ex:
    print("[play-local] WARN: could not pre-cache servers.json:", ex)
PY

echo "[play-local] Server ready! Launching sanitized client (log: $SERVER_LOG)..."
case "$(uname -s)" in
  Linux)
    ./scripts/play-linux.sh "${CLIENT_ARGS[@]}"
    ;;
  Darwin)
    echo "[play-local] macOS detected: launch your Wine/macOS wrapper against $STATUS_URL (Ctrl+C to stop server)."
    wait "$SRV_PID"
    ;;
  *)
    echo "[play-local] Windows/other OS detected: launch .work/client/DurangoV2.exe directly against $STATUS_URL (Ctrl+C to stop server)."
    wait "$SRV_PID"
    ;;
esac

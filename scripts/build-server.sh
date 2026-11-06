#!/usr/bin/env bash
# Build the Durango: Community Edition Standalone / Headless Dedicated Server.
#
# Requirements:
#   - .NET SDK 9.x (tested with 9.0.320)
#
# The output is placed into `build/server/`, containing DurangoServer.dll,
# runtime configuration, game data, and admin tools.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

DOTNET="${DOTNET:-dotnet}"
CONFIG="${1:-Release}"
OUT_DIR="${2:-build/server}"

if ! command -v "$DOTNET" >/dev/null 2>&1; then
  echo "error: dotnet not found. Install the .NET SDK or set DOTNET=/path/to/dotnet." >&2
  exit 1
fi

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "==> Building Durango Dedicated Server ($CONFIG) -> $OUT_DIR"
"$DOTNET" build src/Durango.Server/DurangoServer.csproj -c "$CONFIG" -o "$OUT_DIR" --nologo

# Copy server configuration
if [ -f config/server.json ]; then
  cp config/server.json "$OUT_DIR/server.json"
fi

echo
echo "==> Durango Dedicated Server built successfully."
echo "Binary: $OUT_DIR/DurangoServer"
echo "To run:"
echo "  cd $OUT_DIR && DOTNET_ROOT=\${DOTNET_ROOT:-~/.dotnet} ./DurangoServer"

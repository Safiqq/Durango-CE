#!/usr/bin/env bash
# Build the decompiled client assemblies from source.
#
# These projects reproduce the Unity Mono/.NET Standard 2.1 compilation profile:
# implicit framework references are disabled and the Unity engine/BCL assemblies
# from lib/managed/engine are referenced explicitly.
#
# Requirements:
#   - .NET SDK 9.x (tested with 9.0.320)
#
# The built DLLs are used by `scripts/restructure.py assemble` to produce
# the playable Durango: Community Edition build.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

DOTNET="${DOTNET:-dotnet}"
CONFIG="${1:-Release}"

if ! command -v "$DOTNET" >/dev/null 2>&1; then
  echo "error: dotnet not found. Install the .NET SDK or set DOTNET=/path/to/dotnet." >&2
  exit 1
fi

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Dependency order. The main project references the original DLLs from
# lib/managed/game, but every project is still built so the source stays honest.
PROJECTS=(
  "src/Debug/Debug.csproj"
  "src/ExternalLibrary/ExternalLibrary.csproj"
  "src/Assembly-CSharp-firstpass/Assembly-CSharp-firstpass.csproj"
  "src/Durango.Localization.Overrides/Durango.Localization.Overrides.csproj"
  "src/Assembly-CSharp/Assembly-CSharp.csproj"
)

for project in "${PROJECTS[@]}"; do
  echo
  echo "==> $project"
  "$DOTNET" build "$project" -c "$CONFIG" --nologo
done

echo
echo "Build finished. Outputs are under src/*/bin/$CONFIG/netstandard2.1/."
echo "Main assembly: src/Assembly-CSharp/bin/$CONFIG/netstandard2.1/Assembly-CSharp.dll"

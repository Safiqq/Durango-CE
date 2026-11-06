#!/usr/bin/env bash
# Reproducible decompilation of the Durango client assemblies with ILSpy.
#
# The committed source tree under src/ is the sanitized, hand-cleaned result.
# Running this script regenerates raw decompiler output, so by default it writes
# to a staging directory instead of overwriting the reviewed source.
#
# Usage:
#   scripts/decompile.sh                 # write to build/decompile
#   scripts/decompile.sh /tmp/my-out     # write somewhere else
#   scripts/decompile.sh --in-place      # DANGER: overwrite src/*/ decompiled trees
#
# Requirements:
#   - .NET SDK (tested with 9.0.320)
#   - ilspycmd (tested with 9.1.0.7988); install with:
#       dotnet tool install --global ilspycmd --version 9.1.0.7988
#
# ilspycmd 9.1 targets .NET 8, so DOTNET_ROLL_FORWARD=Major is required on a
# .NET 9-only machine.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

IN_PLACE=0
if [[ "${1:-}" == "--in-place" ]]; then
  IN_PLACE=1
  shift
fi

OUT_DIR="${1:-$REPO_ROOT/build/decompile}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet not found. Install the .NET SDK first." >&2
  exit 1
fi
if ! command -v ilspycmd >/dev/null 2>&1; then
  echo "error: ilspycmd not found. Install with:" >&2
  echo "  dotnet tool install --global ilspycmd --version 9.1.0.7988" >&2
  exit 1
fi

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

SANITIZED="lib/managed/game/Assembly-CSharp.dll.sanitized"
if [[ ! -f "$SANITIZED" ]]; then
  echo "error: missing $SANITIZED" >&2
  echo "Extract the release first (see scripts/restructure.py)." >&2
  exit 1
fi

decompile() {
  local assembly="$1"
  local name="$2"
  local out="$3"

  echo "==> $name"
  rm -rf "$out"
  mkdir -p "$out"
  ilspycmd -p \
    -o "$out" \
    -r lib/managed/engine \
    -r lib/managed/third-party \
    -r lib/managed/game \
    --disable-updatecheck \
    "$assembly"
}

# Rewrite ILSpy's repo-root-relative HintPaths so they resolve from the project
# directory wherever we put the generated tree.
fix_hintpaths() {
  local out_root="$1"
  python3 - "$REPO_ROOT" "$out_root" <<'PY'
import os
import pathlib
import sys

repo = pathlib.Path(sys.argv[1]).resolve()
out_root = pathlib.Path(sys.argv[2]).resolve()

for project in out_root.glob("*/*.csproj"):
    project_dir = project.parent
    text = project.read_text()
    lines = []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("<HintPath>lib/"):
            rest = stripped[len("<HintPath>"):]
            target = rest.split("</HintPath>", 1)[0]
            absolute = (repo / target).resolve()
            rel = os.path.relpath(absolute, project_dir).replace(os.sep, "/")
            lines.append("      <HintPath>" + rel + "</HintPath>")
        else:
            lines.append(line)
    project.write_text("\n".join(lines) + "\n")
    print(f"  fixed HintPaths: {project.relative_to(out_root)}")
PY
}

if [[ "$IN_PLACE" -eq 1 ]]; then
  echo "WARNING: overwriting reviewed source under src/. Manual hardening edits will be lost." >&2
  TMP_OUT="$(mktemp -d "${TMPDIR:-/tmp}/durango-decompile.XXXXXX")"
  trap 'rm -rf "$TMP_OUT"' EXIT
  TARGET="$TMP_OUT"
else
  TARGET="$OUT_DIR"
  mkdir -p "$TARGET"
fi

# Main game assembly: decompile the sanitized binary so removed secrets and the
# disabled IL methods are reflected in the generated source.
decompile "$SANITIZED" "Assembly-CSharp" "$TARGET/Assembly-CSharp"

# Secondary game/plugin assemblies.
for name in Assembly-CSharp-firstpass Durango.Localization.Overrides Debug ExternalLibrary; do
  decompile "lib/managed/game/$name.dll" "$name" "$TARGET/$name"
done

fix_hintpaths "$TARGET"

if [[ "$IN_PLACE" -eq 1 ]]; then
  for name in Assembly-CSharp Assembly-CSharp-firstpass Durango.Localization.Overrides Debug ExternalLibrary; do
    rm -rf "src/$name"
    cp -a "$TARGET/$name" "src/$name"
  done
  mv src/Assembly-CSharp/Assembly-CSharp.dll.csproj src/Assembly-CSharp/Assembly-CSharp.csproj

  echo
  echo "Raw source copied into src/."
  echo "You must now re-apply the source hardening documented in docs/DECOMPILATION.md:"
  echo "  - TestBridge network listener removal"
  echo "  - OffServerHardwareId fingerprint removal"
  echo "  - RavenClient/ErrorReporter/SendReportSystem telemetry removal"
  echo "  - TestBridgePort/servers_url defaults"
  echo "  - loopback-only Durango.Offline Listener/WebServer"
  echo "  - netstandard2.1 + Unity BCL project configuration (docs/DECOMPILATION.md §4)"
else
  echo
  echo "Raw decompiler output written to: $TARGET"
  echo "Compare with src/ before copying; src/ also contains the hardening edits."
fi

#!/usr/bin/env python3
"""
Restructure the Durango client release archive into a clean workspace layout
(static extraction only - nothing from the archive is ever executed), and
optionally assemble a runnable Unity layout from that workspace.

    python3 scripts/restructure.py extract  [--zip release/Durango-Client-v3.3.6.zip]
    python3 scripts/restructure.py assemble [--out build/Durango-CE]

Workspace layout produced by `extract`
    config/unity/         boot.config, ScriptingAssemblies.json, version.txt ... (Unity runtime config)
    config/server.original.txt   file exactly as shipped
    lib/managed/game/     custom/project assemblies (Assembly-CSharp*.dll ...) <- the only "source-like" code
    lib/managed/third-party/  Concentus, LiteNetLib, MsgPack, NCalc, ... (3rd-party managed libs)
    lib/managed/engine/   Unity engine + Mono BCL assemblies
    lib/native/plugins/   native x64 plugins (Wwise, ffmpeg, Dwarf, snappy ...)
    lib/engine/           Durango.exe, UnityPlayer.dll, MonoBleedingEdge, D3D12, dstorage ...
    assets/streaming/     StreamingAssets (Audio, Movie, AssetBundles, Reborn)
    assets/unity/         serialized Unity data (levels, *.assets, *.resS, Resources)
    quarantine/           DurangoLauncher.exe  (UNTRUSTED - never run, see docs/SECURITY_AUDIT.md)
    docs/                 vendor README / third-party notices / sha256 manifest
    layout_map.json       original-path -> workspace-path (used by `assemble`)
"""
import argparse
import hashlib
import json
import os
import shutil
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PREFIX = "Durango-OffServer/"

# --- classification of Durango_Data/Managed ---------------------------------
GAME_ASSEMBLIES = {
    "Assembly-CSharp.dll",
    "Assembly-CSharp-firstpass.dll",
    "Durango.Localization.Overrides.dll",
    "Debug.dll",
    "ExternalLibrary.dll",
}
THIRD_PARTY_ASSEMBLIES = {
    "Concentus.dll", "ICSharpCode.SharpZipLib.dll", "LiteNetLib.dll", "MsgPack.dll",
    "NCalc.dll", "Vectrosity.dll", "Wwise.dll", "UnityFbxPrefab.dll",
}

UNITY_CFG = {"boot.config", "ScriptingAssemblies.json", "RuntimeInitializeOnLoads.json",
             "app.info", "version.txt", "obsolete-files.txt"}


def map_path(rel):
    """Map a path inside the release zip (without the top folder) to the workspace."""
    if rel in ("offserver.txt", "server.txt"):
        return "config/server.original.txt"
    if rel in ("README.txt", "THIRD-PARTY-NOTICES.txt"):
        return "docs/vendor-" + rel
    if rel == "DurangoLauncher.exe":
        return "quarantine/DurangoLauncher.exe"
    if rel.startswith("Durango_Data/Managed/"):
        name = rel.split("/", 2)[2]
        if name in GAME_ASSEMBLIES:
            return "lib/managed/game/" + name
        if name in THIRD_PARTY_ASSEMBLIES:
            return "lib/managed/third-party/" + name
        return "lib/managed/engine/" + name
    if rel.startswith("Durango_Data/Plugins/"):
        return "lib/native/plugins/" + rel.split("/", 2)[2]
    if rel.startswith("Durango_Data/StreamingAssets/"):
        return "assets/streaming/" + rel.split("/", 2)[2]
    if rel.startswith("Durango_Data/Resources/"):
        return "assets/unity/Resources/" + rel.split("/", 2)[2]
    if rel.startswith("Durango_Data/"):
        name = rel.split("/", 1)[1]
        if name in UNITY_CFG:
            return "config/unity/" + name
        return "assets/unity/data/" + name
    # engine bits: Durango.exe, UnityPlayer.dll, UnityCrashHandler64.exe,
    # dstorage*.dll, D3D12/, MonoBleedingEdge/
    return "lib/engine/" + rel


def extract(zip_path):
    layout = {}
    manifest = []
    with zipfile.ZipFile(zip_path) as zf:
        for info in zf.infolist():
            if not info.filename.startswith(PREFIX) or info.is_dir():
                continue
            rel = info.filename[len(PREFIX):]
            dest_rel = map_path(rel)
            dest = os.path.join(ROOT, dest_rel)
            # zip-slip guard
            if not os.path.abspath(dest).startswith(ROOT + os.sep):
                sys.exit("refusing path outside workspace: " + info.filename)
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            h = hashlib.sha256()
            with zf.open(info) as src, open(dest, "wb") as out:
                while chunk := src.read(1 << 20):
                    h.update(chunk)
                    out.write(chunk)
            layout[rel] = dest_rel
            manifest.append(f"{h.hexdigest()}  {dest_rel}")
    os.makedirs(os.path.join(ROOT, "docs"), exist_ok=True)
    with open(os.path.join(ROOT, "layout_map.json"), "w") as f:
        json.dump(layout, f, indent=1, sort_keys=True)
    with open(os.path.join(ROOT, "docs", "manifest_sha256.txt"), "w") as f:
        f.write("\n".join(sorted(manifest)) + "\n")
    print(f"extracted {len(layout)} files")


def assemble(out_dir):
    """Rebuild the folder layout Unity requires, preferring *.sanitized files and
    the local server.txt. Uses hard links when possible (no extra disk)."""
    with open(os.path.join(ROOT, "layout_map.json")) as f:
        layout = json.load(f)
    out_dir = os.path.abspath(out_dir)
    if os.path.exists(out_dir):
        shutil.rmtree(out_dir)
    n = 0
    for rel, ws_rel in layout.items():
        if rel == "DurangoLauncher.exe":
            continue  # untrusted: never part of a runnable build
        src = os.path.join(ROOT, ws_rel)
        if rel in ("offserver.txt", "server.txt"):
            continue  # Standardized as server.txt below
        elif rel.startswith("Durango_Data/Managed/"):
            dll_name = rel.split("/", 2)[2]
            proj_name = os.path.splitext(dll_name)[0]
            built_dll = os.path.join(ROOT, "src", proj_name, "bin", "Release", "netstandard2.1", dll_name)
            if os.path.exists(built_dll):
                src = built_dll
            elif os.path.exists(src + ".sanitized"):
                src += ".sanitized"
        elif os.path.exists(src + ".sanitized"):
            src += ".sanitized"
        dst = os.path.join(out_dir, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        try:
            os.link(src, dst)
        except OSError:
            shutil.copy2(src, dst)
        n += 1

    servers_src = os.path.join(ROOT, "servers.json")
    if os.path.exists(servers_src):
        dst = os.path.join(out_dir, "servers.json")
        try:
            os.link(servers_src, dst)
        except OSError:
            shutil.copy2(servers_src, dst)
        n += 1

    server_config_src = os.path.join(ROOT, "config", "server.local.txt")
    if os.path.exists(server_config_src):
        dst = os.path.join(out_dir, "server.txt")
        try:
            os.link(server_config_src, dst)
        except OSError:
            shutil.copy2(server_config_src, dst)
        n += 1
    print(f"assembled {n} files -> {out_dir}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    e = sub.add_parser("extract")
    e.add_argument("--zip", default=os.path.join(ROOT, "release", "Durango-OffServer-Client-v3.3.6.zip"))
    a = sub.add_parser("assemble")
    a.add_argument("--out", default=os.path.join(ROOT, "build", "Durango-CE"))
    args = ap.parse_args()
    extract(args.zip) if args.cmd == "extract" else assemble(args.out)

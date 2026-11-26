#!/usr/bin/env python3
"""
Extract and IL-sanitize the freshly forked Durango-OffServer v3.3.6 client into
gitignored `.work/client/` without ever extracting upstream `DurangoLauncher.exe`.

Usage:
    python3 scripts/setup-client.py [--force]
"""
import argparse
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
RELEASE_ZIP = REPO_ROOT / "release" / "Durango-OffServer-Client-v3.3.6.zip"
WORK_DIR = REPO_ROOT / ".work"
CLIENT_DIR = WORK_DIR / "client"
SANITIZER = REPO_ROOT / "scripts" / "sanitize_client.py"

SKIP_NAMES = {"DurangoLauncher.exe"}


def ensure_wine_fonts() -> None:
    if not sys.platform.startswith("linux"):
        return
    prefix = Path(os.environ.get("WINEPREFIX", str(Path.home() / ".wine")))
    wine_fonts = prefix / "drive_c" / "windows" / "Fonts"
    if not wine_fonts.parent.exists():
        return
    wine_fonts.mkdir(parents=True, exist_ok=True)
    candidates = [
        ("arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
        ("arialbd.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
    ]
    for dst_name, src_path in candidates:
        src = Path(src_path)
        dst = wine_fonts / dst_name
        if src.exists():
            if dst.is_symlink() or dst.exists():
                dst.unlink()
            dst.symlink_to(src)


def extract_client(force: bool) -> None:
    if not RELEASE_ZIP.exists():
        raise SystemExit(f"[error] Missing upstream archive: {RELEASE_ZIP}")

    exe_path = CLIENT_DIR / "DurangoV2.exe"
    if exe_path.exists() and not force:
        print(f"[setup] Existing client found at {CLIENT_DIR} (pass --force to re-extract).")
        return

    if CLIENT_DIR.exists():
        shutil.rmtree(CLIENT_DIR)
    CLIENT_DIR.mkdir(parents=True, exist_ok=True)

    print(f"[setup] Extracting {RELEASE_ZIP.name} -> {CLIENT_DIR} (skipping {sorted(SKIP_NAMES)})...")
    extracted = 0
    skipped = 0
    with zipfile.ZipFile(RELEASE_ZIP, "r") as zf:
        for info in zf.infolist():
            name = info.filename
            if name.startswith("Durango-OffServer/"):
                rel = name[len("Durango-OffServer/"):]
            else:
                rel = name
            if not rel:
                continue
            parts = Path(rel).parts
            if any(p in SKIP_NAMES for p in parts):
                skipped += 1
                continue
            target = CLIENT_DIR / rel
            if info.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with zf.open(info, "r") as src, open(target, "wb") as dst:
                    shutil.copyfileobj(src, dst)
                extracted += 1
    print(f"[setup] Extracted {extracted} files ({skipped} untrusted launcher files skipped).")


def ensure_upstream_assembly() -> Path:
    dll = CLIENT_DIR / "DurangoV2_Data" / "Managed" / "Assembly-CSharp.dll"
    orig = dll.with_suffix(".dll.upstream")
    if orig.exists():
        return orig
    if not RELEASE_ZIP.exists():
        raise SystemExit(f"[error] Missing upstream archive: {RELEASE_ZIP}")
    with zipfile.ZipFile(RELEASE_ZIP, "r") as zf:
        for info in zf.infolist():
            if info.filename.endswith("DurangoV2_Data/Managed/Assembly-CSharp.dll"):
                orig.parent.mkdir(parents=True, exist_ok=True)
                with zf.open(info, "r") as src, open(orig, "wb") as dst:
                    shutil.copyfileobj(src, dst)
                print(f"[setup] Cached clean upstream Assembly-CSharp.dll -> {orig.name}")
                return orig
    raise SystemExit("[error] Could not locate Assembly-CSharp.dll inside release zip")


def build_and_hook_client_mod(base_dll: Path, hooked_dll: Path) -> Path:
    import json

    csproj = REPO_ROOT / "client-mod" / "DurangoCE.ClientMod.csproj"
    if not csproj.exists():
        return base_dll
    dotnet_bin = shutil.which("dotnet") or str(Path.home() / ".dotnet" / "dotnet")
    if not Path(dotnet_bin).exists():
        print("[setup] dotnet SDK not found — skipping optional DurangoCE.ClientMod build.")
        return base_dll

    managed_dir = CLIENT_DIR / "DurangoV2_Data" / "Managed"
    obj_dir = WORK_DIR / "client-mod-obj"
    print("[setup] Building cleanroom DurangoCE.ClientMod.dll...")
    subprocess.run(
        [
            dotnet_bin,
            "build",
            str(csproj),
            "-c",
            "Release",
            "--nologo",
            "-v",
            "minimal",
            f"-p:ManagedDir={managed_dir}",
            f"-p:BaseIntermediateOutputPath={obj_dir}/",
        ],
        check=True,
    )

    sa_path = CLIENT_DIR / "DurangoV2_Data" / "ScriptingAssemblies.json"
    if sa_path.exists():
        sa = json.loads(sa_path.read_text(encoding="utf-8"))
        mod_name = "DurangoCE.ClientMod.dll"
        if mod_name not in sa.get("names", []):
            sa["names"].append(mod_name)
            sa["types"].append(16)
            sa_path.write_text(json.dumps(sa, separators=(",", ":")), encoding="utf-8")
            print(f"[setup] Registered {mod_name} in {sa_path.name}")

    cecil_candidates = [
        *Path.home().glob(".nuget/packages/microsoft.net.illink.tasks/*/tools/net9.0/Mono.Cecil.dll"),
        *Path.home().glob(".dotnet/tools/.store/ilspycmd/*/ilspycmd/*/tools/net8.0/any/Mono.Cecil.dll"),
    ]
    injector_proj = REPO_ROOT / "client-mod" / "inject" / "DurangoCE.ModInjector.csproj"
    if not cecil_candidates or not injector_proj.exists():
        print("[setup] Mono.Cecil.dll not found in local dotnet/nuget cache — skipping bootstrap hook.")
        return base_dll

    cecil_dll = sorted(cecil_candidates)[-1]
    inj_obj = WORK_DIR / "mod-injector-obj"
    inj_bin = WORK_DIR / "mod-injector-bin" / "DurangoCE.ModInjector.dll"
    subprocess.run(
        [
            dotnet_bin,
            "build",
            str(injector_proj),
            "-c",
            "Release",
            "--nologo",
            "-v",
            "minimal",
            f"-p:CecilDll={cecil_dll}",
            f"-p:BaseIntermediateOutputPath={inj_obj}/",
        ],
        check=True,
    )
    subprocess.run(
        [
            dotnet_bin,
            str(inj_bin),
            str(managed_dir),
            str(base_dll),
            str(hooked_dll),
            str(managed_dir / "DurangoCE.ClientMod.dll"),
        ],
        check=True,
    )
    return hooked_dll


def sanitize_assembly(servers_url: str = "") -> None:
    dll = CLIENT_DIR / "DurangoV2_Data" / "Managed" / "Assembly-CSharp.dll"
    if not dll.exists():
        raise SystemExit(f"[error] Managed assembly not found: {dll}")

    orig = ensure_upstream_assembly()
    hooked = dll.with_suffix(".dll.hooked")
    src_for_sanitizer = build_and_hook_client_mod(orig, hooked)

    tmp_out = dll.with_suffix(".dll.sanitized")
    print(f"[setup] Running deterministic IL sanitizer on {dll.name}...")
    cmd = [sys.executable, str(SANITIZER), str(src_for_sanitizer), str(tmp_out)]
    if servers_url:
        cmd.extend(["--servers-url", servers_url])
    subprocess.run(cmd, check=True)
    shutil.move(str(tmp_out), str(dll))
    if hooked.exists():
        hooked.unlink()
    print(f"[setup] Sanitized {dll} successfully.")


def write_local_config(gateway: str, name: str, servers_url: str) -> None:
    reborn_dir = CLIENT_DIR / "DurangoV2_Data" / "StreamingAssets" / "Reborn"
    if reborn_dir.exists():
        shutil.rmtree(reborn_dir)
        print(f"[setup] Removed third-party Reborn splash assets: {reborn_dir}")
    offserver = CLIENT_DIR / "offserver.txt"
    offserver.write_text(
        "# Durango-CE client configuration\n"
        f"gateway={gateway}\n"
        f"name={name}\n"
        f"servers_url={servers_url}\n",
        encoding="utf-8",
    )
    shutil.copy2(REPO_ROOT / "servers.json", CLIENT_DIR / "servers.json")
    print(f"[setup] Wrote client config -> {offserver} (gateway={gateway}, servers_url={servers_url or '<default>'})")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--force", action="store_true", help="Re-extract .work/client from release zip")
    parser.add_argument(
        "--mod-only",
        action="store_true",
        help="Rebuild DurangoCE.ClientMod.dll & re-run Assembly-CSharp sanitizer without re-extracting assets",
    )
    parser.add_argument(
        "--gateway",
        default="http://127.0.0.1:8190",
        help="Default community server gateway URL (default: http://127.0.0.1:8190)",
    )
    parser.add_argument(
        "--name",
        default="Durango-CE Local Server",
        help="Default server display name",
    )
    parser.add_argument(
        "--servers-url",
        default="",
        help="Source-of-truth community directory URL (e.g. http://<host>:8190/servers.json)",
    )
    args = parser.parse_args()

    if args.mod_only and (CLIENT_DIR / "DurangoV2.exe").exists():
        sanitize_assembly(servers_url=args.servers_url)
        print("[setup] Updated DurangoCE.ClientMod.dll & sanitized Assembly-CSharp.dll cleanly.")
        return

    extract_client(args.force)
    sanitize_assembly(servers_url=args.servers_url)
    write_local_config(gateway=args.gateway, name=args.name, servers_url=args.servers_url)
    ensure_wine_fonts()
    print("[setup] Ready. Launch via ./scripts/play-linux.sh")


if __name__ == "__main__":
    main()

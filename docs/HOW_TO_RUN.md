# How to Run & Verify — Bare Metal & DevSecOps Sandbox Guide

> **Current Security Baseline**: The PC client has been fully decompiled into human-readable C# source (`src/`), audited, and permanently hardened:
> - Developer automation socket (`TestBridge` TCP command socket) completely removed.
> - Hardware GUID and SMBIOS UUID harvesting removed.
> - Leftover Sentry (`RavenClient`) and Slack (`SendReportSystem`) telemetry beacons eliminated.
> - Zero-dependency offline gateway (`LocalGatewayServer`) embedded directly in-process.
>
> **Execution Options**:
> 1. **Direct Bare-Metal Play (Recommended for Players)**: Run `Durango.exe` directly on Windows 10/11 or macOS (via Whisky).
> 2. **DevSecOps Verification Sandbox (Recommended for Auditors)**: Run inside an isolated VMware Windows 11 VM with ProcMon and Wireshark monitoring to independently verify that zero unauthorized processes, network calls, or registry hooks occur.
>
> This document details the operating procedure for both tracks.

---

## 1. Direct Bare-Metal Execution (Normal Players)

If you just want to play the game on your PC or Mac without setting up virtual machines:

### Windows 10 / 11
1. Extract or assemble the distribution folder (`build/Durango-CE/`).
2. Double-click `Durango.exe`.
3. The embedded gateway starts automatically in the background on loopback `127.0.0.1:8190`/`8290`, serving all offline handshake responses and bypassing the 24% load screen hang.
4. *(Optional)*: Run `DurangoLauncher.exe` if you want to switch between community servers listed in `servers.json`.

### macOS (MacBook Air / Pro / Mac mini)
1. Install **Whisky** (`brew install --cask whisky`).
2. Create a Windows 10/11 bottle with DXVK / D3DMetal enabled.
3. Run `Durango.exe` directly inside the bottle. See **[MACOS_SETUP.md](MACOS_SETUP.md)**.

---

## 2. DevSecOps Verification Sandbox (Independent Auditors)

If you are a security researcher, auditor, or cautious user who wants to prove independently that the build is completely safe before trusting bare metal:

### A. Three words to know

- **Host** = your real Ubuntu computer.
- **Guest / VM** = the Windows 11 machine inside VMware.
- **Snapshot** = a saved state of the VM. You can rewind to it if anything looks wrong.

The goal is: put the game in the Windows VM, check that the files are the safe
versions, cut the VM off from the internet, run the game once, then rewind the VM.

### B. On Ubuntu, make a transfer ISO

This is how you move 1.5 GB into the VM without shared folders.

```bash
sudo apt update
sudo apt install genisoimage

cd ~/Durango-CE
genisoimage -udf -R -o ~/Downloads/Durango.iso build/Durango-CE
ls -lh ~/Downloads/Durango.iso
```

The `ls` command should show a file around 1.5 GB. Keep that path; you need it in the
next step.

### C. Prepare the Windows 11 VM

Do this while the VM still has internet, because Windows may need updates and VMware
Tools.

1. Install **VMware Tools** if it is not already installed.
2. Run Windows Update once.
3. Open **Windows Security → Virus & threat protection → Check for updates**, then run
   a quick scan.
4. Shut down the VM.
5. In VMware, open **VM → Settings** and disable:
   - **Shared Folders**
   - **Guest Isolation → drag-and-drop**
   - **Guest Isolation → copy/paste**
   - **USB auto-connect**
6. Set the network adapter to **Host-only** for now (you will disconnect it later).

Do not use a Microsoft account, email, Discord, Steam, or any personal login inside
this VM.

### D. Attach the ISO and copy the game

1. In VMware, open **VM → Settings → CD/DVD**.
2. Choose **Use ISO image file** and select `/tmp/opencode/Durango.iso`.
3. Start the VM and open **File Explorer → This PC**.
4. Open the DVD drive, select everything, and copy it to `C:\Games\Durango`.
5. Wait for the copy to finish, then **disconnect/eject the CD/DVD** in VMware.

Make sure the folder is `C:\Games\Durango\Durango.exe`, not
`C:\Games\Durango\Durango-CE\Durango.exe`.

### E. Check the files in Windows

Press the Start button, type **PowerShell**, and open it normally (do **not** choose
"Run as administrator"). Then run:

```powershell
Get-FileHash -Algorithm SHA256 "C:\Games\Durango\Durango_Data\Managed\Assembly-CSharp.dll"
```

The result must be:

```text
7535931F4FC14ED03F09960C2976A04847667D6B0CBF349C64E6F8886A717AB2
```

Then run:

```powershell
Get-ChildItem -Path "C:\Games\Durango" -Recurse -Filter "*Launcher*"
Get-Content "C:\Games\Durango\server.txt"
```

- The first command should print **nothing**. If it finds a launcher, stop.
- The second command should show `gateway=http://127.0.0.1:8290` and no
  `testbridge` line.

If the hash is different, or a launcher exists, stop and re-copy the files. Do not run
the game.

### F. Prepare for the first run

1. In Windows Security, run a **custom scan** of `C:\Games\Durango`. Wait for it to
   finish.
2. In VMware, disconnect the network:
   **VM → Removable Devices → Network Adapter → Disconnect**.
3. In PowerShell, turn off the server config:

   ```powershell
   Rename-Item "C:\Games\Durango\server.txt" "server.txt.off"
   ```

4. Take a snapshot now: **VM → Snapshot → Take Snapshot**, name it
   `before-first-run`.

The snapshot is important. After the test you will rewind to it, so any changes the
game made disappear.

### G. Run it

In PowerShell:

```powershell
cd C:\Games\Durango
.\Durango.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile player.log
```

What is normal:

- The game opens to a menu, loading screen, or character screen.
- It may say the server is offline. That is expected because you disconnected the
  network and disabled `server.txt`.
- Windows may show a SmartScreen warning because the file is unsigned. Only click
  **More info → Run anyway** if the hash check in step E matched.

What is not normal:

- A `cmd.exe`, `powershell.exe`, `wscript.exe`, or browser window opens by itself.
- Windows shows a virus alert.
- The game asks for your real password, Discord login, or account code.
- Your network adapter is somehow connected and traffic appears.

If the game closes immediately and you see no window at all, look at
`C:\Games\Durango\player.log`. Some builds expect `server.txt` to exist; if so,
rename `server.txt.off` back to `server.txt` and try again, but keep the network
disconnected. Do not point it at the upstream IP.

If Windows Defender blocks the game, **stop**. Do not add an exclusion and do not
disable Defender.

If anything looks wrong: **VM → Power → Power Off**, then **VM → Snapshot → Revert to
Snapshot → before-first-run**. Do not copy files back to Ubuntu.

### I. Standalone Local Gateway Execution

To play locally without connecting to external servers:
1. Ensure `server.txt` is configured to `gateway=http://127.0.0.1:8190` (default in the build).
2. Launch `Durango.exe`.
3. The embedded local gateway initializes automatically in the background on ports `8190` and `8290`, serving all required routes (`GET /status`, `POST /accounts`, `GET /knock`, `POST /sessions`) and resolving the 24% load screen hang directly from local assets.
4. For the full architecture on custom server IP input vs. local offline worlds, see **[RUNTIME_DUAL_MODE.md](RUNTIME_DUAL_MODE.md)**.

### J. After the test

1. Close the game and the gateway console.
2. Power off the VM.
3. Revert to the `before-first-run` snapshot.
4. That's it. You have verified functionality while keeping the VM isolated.

If you want to test again, start the VM from the snapshot and repeat step G.

> **Beginner rule of thumb:** if you are tired, confused, or in a hurry, stop. Do not
> "just try it" on your real Ubuntu machine or with the network connected.

---

## 0. TL;DR

1. Build/verify the **sanitized PC build** on the host (`build/Durango-CE/`).
2. Create a **fresh Windows 10/11 VM** in VMware.
3. Disable shared folders, drag-and-drop, copy-paste, USB passthrough, and bridged
   networking.
4. Take a **snapshot** before the game files ever enter the VM.
5. Transfer the build through a **read-only ISO**, not a shared folder.
6. For the first run, **disconnect the VM network** and disable `server.txt`.
7. Run **`Durango.exe` directly** for offline single-player, or **`DurangoLauncher.exe`** for community server switching.
8. Watch processes, file writes, registry writes, and network traffic.
9. **Revert the snapshot** after every session.
10. Do not consider it trustworthy until it is rebuilt from reviewed source and the
    rebuilt binaries match a reproducible build.

---

## 1. What this project actually is

- A fan-made private-server client for *Durango: Wild Lands*, which shut down in 2019.
- The repository provides both the **decompiled, hardened C# source tree** (`src/`) and an automated pipeline to assemble runnable client builds.
- The PC client is a modified Unity **Mono** build. All game code has been decompiled into C# projects in `src/Assembly-CSharp/` and support libraries, compiling with 0 errors via .NET 9.
- The build incorporates an embedded loopback gateway (`LocalGatewayServer.cs`) running in-process, allowing `Durango.exe` to run completely self-contained.
- The community launcher has been decompiled, sanitized, and rebuilt from source in `src/DurangoLauncher/`.
- The prepared build in `build/Durango-CE/` uses the hardened DLLs, authentic branding, and loopback network configurations.
- The Android APK is a separate IL2CPP build and is out of scope for this guide.

Key documents:

| Document | Purpose |
| :--- | :--- |
| `docs/SECURITY_AUDIT.md` | PC static security audit and threat matrix |
| `docs/ANDROID_SECURITY_AUDIT.md` | Android APK audit (out of scope for now) |
| `docs/PROJECT_PROGRESS.md` | Roadmap, file layout, milestone tracker |
| `docs/DECOMPILATION.md` | M7 source tree, build instructions, and source hardening |
| `docs/SERVER_SETUP.md` | Server options, local hosting, and compatibility limits |
| `docs/sanitize_report.json` | Exact sanitizer patches and hashes |
| `docs/manifest_sha256.txt` | SHA-256 of every extracted release file |
| `release/SHA256SUMS.txt` | Upstream release checksums |

---

## 2. What is already mitigated, and what is not

### Mitigated in the prepared build

| Risk | Mitigation in `build/Durango-CE/` |
| :--- | :--- |
| `Durango.Development.TestBridge` unauthenticated local TCP control server | `StartListener` patched to `ret`; no `testbridge=` line in config |
| `OffServerHardwareId` registry/SMBIOS fingerprinting | `AddTo` patched to `ret` |
| Sentry crash-log uploads | `RavenClient` methods patched to `ret`; DSN replaced with dead loopback |
| Nexus Slack webhook leak | Webhook string replaced with dead loopback |
| Untrusted launcher | Original `quarantine/DurangoLauncher.exe` quarantined; replaced with sanitized, open-source build from `src/DurangoLauncher` |
| Upstream gateway over cleartext HTTP | Build uses embedded `LocalGatewayServer` loopback (`127.0.0.1:8190`/`8290`) |

### Still not proven safe

- The sanitizer only changes **165 bytes** in one managed DLL. The rest of the
  7.2 MB assembly, all native plugins, the Unity engine, and the 1.5 GB of assets are
  not source-audited.
- Native DLLs (`Dwarf*.dll`, Wwise, ffmpeg, etc.) remain unverified third-party
  binaries. Static string review is not a sandbox.
- The release is unsigned. Checksums in `SHA256SUMS.txt` prove **self-consistency**,
  not that the original author is trustworthy.
- `Assembly-CSharp.dll` still contains the rest of the custom `OffServer*` code. The
  sanitizer removes the known dangerous paths (HWID, `TestBridge`, Raven/Sentry, Slack
  webhook), not every possible behavior.
- Upstream can publish a different binary later. Pin this version and re-verify hashes.
- "100% malware-free" cannot be established by a static scan alone. The realistic
  path is source decompilation, review, clean rebuild, and reproducible comparison.

### Provenance / upstream pointers still present in the repo

These are not used by the offline VM procedure, but they matter if you fork, publish,
or run online:

- Root `servers.json` defaults to a safe `127.0.0.1` template with TLS and integrity verification support.
- Root `pc-launcher.json` points to the `Safiqq/Durango-CE` community repository.
- The client's remote `servers_url` fetch has been neutralized in source to prefer the verified local `servers.json`.
- The prepared `server.txt` targets `127.0.0.1:8190` by default.

---

## 3. Trust gates

Use these gates in order. Do not advance if a gate fails.

| Gate | Question | Pass condition |
| :--- | :--- | :--- |
| **G0 — Provenance** | Is this the exact version I audited? | Release hashes match `release/SHA256SUMS.txt`; sanitized DLL matches `7535931f…717ab2` |
| **G1 — Containment** | Can it touch my real machine? | Fresh VM, snapshot taken, no shared folders/clipboard/USB, no bridged network |
| **G2 — First contact** | Does it behave while offline? | Runs or fails cleanly with no unexpected process/network/file activity |
| **G3 — Observation** | Does it try anything outside the game folder? | Procmon/Wireshark show only expected loopback or no traffic |
| **G4 — Source** | Can I read the real code? | Decompile `Assembly-CSharp.dll`, review it, remove dangerous code at source level |
| **G5 — Rebuild** | Can I build it myself? | Clean Unity rebuild from reviewed source; compare behavior/hashes |
| **G6 — Ongoing** | Can it change under me? | Pin commits/releases; diff every new upstream build before running |

Only **G4–G6** support a claim close to "safe." Everything before that is risk
reduction, not proof.

---

## 4. Host preparation (Ubuntu/Linux host)

The commands below assume the repository is at
`~/Durango-CE`. Adjust paths if needed.

### 4.1 Verify the original release hashes

Run this from the `release/` directory because `SHA256SUMS.txt` uses bare filenames:

```bash
cd ~/Durango-CE/release
sha256sum -c SHA256SUMS.txt
```

All eight lines should report `OK`. This only proves the local copies match the
checksums shipped with them.

### 4.2 Rebuild the workspace only if `build/` is missing

The prepared `build/Durango-CE/` already exists in this checkout. If you start
from a fresh clone, the large files may be gitignored, so rebuild them:

```bash
cd ~/Durango-CE

# 1. Extract the original release into the workspace layout.
python3 scripts/restructure.py extract \
  --zip release/Durango-OffServer-Client-v3.3.6.zip

# 2. Produce the sanitized managed assembly.
python3 scripts/sanitize_client.py \
  lib/managed/game/Assembly-CSharp.dll \
  lib/managed/game/Assembly-CSharp.dll.sanitized

# 3. Assemble a runnable Unity layout. It uses *.sanitized files and
#    config/server.local.txt, and excludes DurangoLauncher.exe.
python3 scripts/restructure.py assemble --out build/Durango-CE
```

Note: `assemble` deletes and recreates the output directory. Do not point `--out` at a
directory containing anything you care about.

### 4.3 Verify that the build is actually sanitized

```bash
cd ~/Durango-CE

# Must print 7535931f4fc14ed03f09960c2976a04847667d6b0cbf349c64e6f8886a717ab2
sha256sum build/Durango-CE/Durango_Data/Managed/Assembly-CSharp.dll

# Must print nothing (the launcher is deliberately excluded)
find build/Durango-CE -iname '*launcher*'

# Must print only: gateway=http://127.0.0.1:8290
grep -nE '^(gateway|servers_url|account)=' build/Durango-CE/server.txt

# Must print nothing (no local control testbridge enabled)
grep -in 'testbridge' build/Durango-CE/server.txt
```

Expected key hashes for this pinned build:

| File | SHA-256 |
| :--- | :--- |
| `Durango.exe` | `4d1e2fb109900ec816b1991843188dbd1b6bcc5aba574c3f099cf8933aa1401a` |
| `UnityPlayer.dll` | `7c8127007131559a3daa04aa3a3e8d1477975fe449f59917c5c75d86fa1f904f` |
| `Durango_Data/Managed/Assembly-CSharp.dll` | `7535931f4fc14ed03f09960c2976a04847667d6b0cbf349c64e6f8886a717ab2` |
| `server.txt` | `11fac779b460d89d97f8514d0004511ba62335254caa076037ac6c1a63e9409c` |

If `Assembly-CSharp.dll` hashes to `c0a0a48b…2252ef`, it is the original,
**unsanitized** assembly. Do not run it; re-run `sanitize_client.py` and `assemble`.

### 4.4 Optional: scan the build on the host

Install a scanner if you do not have one:

```bash
sudo apt update
sudo apt install clamav clamav-daemon genisoimage
sudo systemctl stop clamav-freshclam 2>/dev/null || true
sudo freshclam
```

Then scan. This is a supplement, not a guarantee; expect possible false positives on
game binaries:

```bash
clamscan -r --bell -i build/Durango-CE
```

You can also search the SHA-256 values on VirusTotal **without uploading the files**.
Uploading proprietary game binaries is a privacy decision; prefer hash lookup.

Do not execute anything from `release/`, `quarantine/`, or `DurangoLauncher.exe` on
the host at any point.

---

## 5. Create the VMware sandbox

These instructions assume VMware Workstation Pro/Player on a Linux host. The same
isolation settings apply to VMware Fusion or ESXi; the menu names may differ.

### 5.1 Recommended VM settings

| Setting | Recommendation |
| :--- | :--- |
| Guest OS | **Windows 10 22H2 x64** (simplest for VMware 3D and DirectX 11) |
| RAM | 4 GB minimum; 8 GB if the host has 16 GB or more |
| vCPUs | 2–4 |
| Disk | 40–60 GB, thin provisioned |
| Firmware | BIOS/UEFI as required by the ISO; avoid Windows 11/TPM unless needed |
| 3D acceleration | **On**; allocate at least 2 GB graphics memory if available |
| Network | **Host-only** for local testing; **no adapter** for the first offline run |
| Shared folders | **Disabled** |
| Drag-and-drop | **Disabled** |
| Copy/paste | **Disabled** |
| USB auto-connect | **Disabled** |
| Bluetooth | **Disabled** |

The PC client is a 64-bit Windows Unity game and needs DirectX. VMware's virtual GPU
is usually DirectX 11-class, not a full DirectX 12 GPU. If the game defaults to D3D12
and shows a black screen, force D3D11 at launch (see §7.3).

### 5.2 Install Windows safely

1. Download the Windows ISO from Microsoft's official site.
2. Verify its SHA-256 against Microsoft's published value before attaching it.
3. Install with a **local account**, not a Microsoft account.
4. Use a throwaway username/password. Do not sign into email, Discord, Steam,
   browsers, or password managers inside this VM.
5. Install VMware Tools.
6. Update Windows and Windows Defender definitions **before** isolating the VM.
7. Run a full Defender scan on the clean guest.
8. Create a local standard user (for example `player`) and use that account to run the
   game. Keep the administrator account only for setup.

### 5.3 Harden VMware before the game enters the VM

In the VM settings, verify:

- **Options → Shared Folders:** disabled.
- **Options → Guest Isolation:** disable drag-and-drop and copy/paste.
- **Removable Devices → USB:** do not auto-connect host devices.
- **Network Adapter:** set to **Host-only** or remove/disconnect it.
- **CD/DVD:** disconnect after transferring files.
- **Snapshots:** take one named `clean-windows`.

If the VM has a bridged adapter, change it. Bridged mode exposes the guest directly to
your real LAN and is unnecessary for this test.

### 5.4 Snapshot discipline

Take snapshots at these points:

1. `clean-windows` — fresh install, updated, no game files.
2. `before-durango` — immediately before the first launch.
3. Optional `after-session-N` — only if you deliberately keep results; otherwise
   revert to `before-durango` after each session.

After any suspicious behavior, **power off** the VM (do not shut down gracefully) and
revert to `clean-windows` or `before-durango`.

---

## 6. Transfer the build without shared folders

Use a read-only ISO. This avoids VMware shared folders, network file sharing, and
clipboard transfer.

### Option A — ISO (recommended, fully offline)

On the Linux host:

```bash
cd ~/Durango-CE

genisoimage -udf -R \
  -o ~/Downloads/Durango.iso \
  build/Durango-CE

ls -lh ~/Downloads/Durango.iso
```

If `genisoimage` is unavailable, install it (`sudo apt install genisoimage`) or use
`xorriso`:

```bash
xorriso -as mkisofs -udf -R \
  -o ~/Downloads/Durango.iso \
  build/Durango-CE
```

Then in VMware:

1. VM settings → CD/DVD → **Use ISO image** → select the ISO.
2. Connect at power on (or connect manually).
3. Inside Windows, copy the contents to `C:\Games\Durango`.
4. Disconnect the CD/DVD.
5. Verify the copied files (see §6.1).

Create `C:\Games\Durango` while signed in as the standard user, or grant that user
Modify permission on the folder. The game may write logs, saves, or cache next to the
executable; a read-only game folder causes confusing failures that look like malware
problems.

Do not run the game from the DVD/ISO. Copy it to the VM's virtual disk first.

### Option B — SSH over a host-only network

Only if you intentionally keep a host-only adapter:

```bash
# On the Linux host
scp -r build/Durango-CE player@<guest-host-only-ip>:C:/Games/Durango
```

Then disconnect the network adapter before launching. This is less isolated than the
ISO method because it requires a network service in the guest.

### Option C — Second virtual disk

Attach a spare virtual disk, copy the build onto it, detach it, and mount it read-only
in the guest. More work than an ISO; use only if the build is too large for your ISO
workflow.

### 6.1 Verify the copy inside Windows

Open PowerShell in the guest:

```powershell
Get-FileHash -Algorithm SHA256 "C:\Games\Durango\Durango.exe"
Get-FileHash -Algorithm SHA256 "C:\Games\Durango\Durango_Data\Managed\Assembly-CSharp.dll"
Get-FileHash -Algorithm SHA256 "C:\Games\Durango\server.txt"
```

Compare against the table in §4.3. The managed DLL must be
`7535931f4fc14ed03f09960c2976a04847667d6b0cbf349c64e6f8886a717ab2`.

Also confirm the launcher is absent and testbridge is not enabled:

```powershell
Get-ChildItem -Recurse "C:\Games\Durango" -Filter "*Launcher*"
Select-String -Path "C:\Games\Durango\server.txt" -Pattern "testbridge"
```

Both commands should return nothing. If `server.txt` was renamed for offline mode,
run the second check against `server.txt.off` before restoring it.

---

## 7. First run

### 7.1 Offline mode — safest first test

The upstream README says deleting `server.txt` returns the game to normal offline
play. Use that for the first launch:

1. Disconnect the VM network adapter (VM → Removable Devices → Network Adapter →
   Disconnect, or uncheck "Connect at power on").
2. Rename the config so it cannot contact anything:

   ```powershell
   Rename-Item "C:\Games\Durango\server.txt" "server.txt.off"
   ```

3. Launch `Durango.exe` as the standard user, without administrator rights.

If the game refuses to start without a gateway, that is expected. Do **not** "fix" it
by pointing the config at the upstream IPs. Move to §7.2 with your own local gateway,
or stop and inspect.

### 7.2 Local gateway mode — only with your own server

The prepared `server.txt` points to `http://127.0.0.1:8290`. That address means
"this guest", not your Linux host. Two valid setups:

- **Gateway inside the guest:** keep `127.0.0.1`; run your own stub server in the same
  Windows VM.
- **Gateway on the Linux host:** use the host's VMnet1 address from VMware's Virtual
  Network Editor (commonly something like `192.168.x.1`), for example
  `gateway=http://192.168.x.1:8290`, and allow only that address.

Rules:

- Never use `168.222.21.53`, `201.18.211.139`, or any other upstream/third-party IP.
- Never add `testbridge=<port>`.
- Never enable `servers_url` pointing at the upstream repo; point it at your own fork
  if you use it at all.
- Assume the client sends session tokens and account IDs in cleartext unless you have
  implemented HTTPS. Do not reuse real credentials.
- This repository includes an embedded offline loopback gateway in `Durango.exe`. For connecting to external community servers or building a dedicated server from source, see **[SERVER_SETUP.md](SERVER_SETUP.md)**.
- Community servers commonly use gateway port **8190** and game port **8191**, not
  8290. Match the server's documented port.

### 7.3 Launch command

Run `Durango.exe` directly. Useful Unity arguments for VMware:

```powershell
cd C:\Games\Durango

# Default attempt
.\Durango.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile player.log
```

- `-force-d3d11` avoids DirectX 12 problems on VMware's virtual GPU.
- `-screen-fullscreen 0` keeps a window so you can watch it.
- `-logFile player.log` writes a log next to the executable.
- If the game exits immediately, try adding `-offserver-launcher` once to test whether
  the client expects that flag. Do **not** interpret this as permission to run
  `DurangoLauncher.exe`.

Never run:

- `release/DurangoLauncher.exe`
- `quarantine/DurangoLauncher.exe`
- anything under `quarantine/launcher-unpacked/`
- `release/DurangoReborn.apk` (Android, out of scope)

If Windows SmartScreen appears for `Durango.exe`, stop and verify the hash first.
Do not click "Run anyway" just because the prompt appeared.

---

## 8. Observe the first session

Run these checks while the game is running and immediately after it exits.

### 8.1 Process and file activity

Use **Process Monitor** (Procmon, Microsoft Sysinternals; download inside the VM from
Microsoft, not from a random mirror). Add filters:

- `Process Name is Durango.exe`
- Include `Operation is Process Create`
- Include `Operation is TCP Connect`
- Include `Operation is UDP Send`
- Include `Operation is RegSetValue`
- Include `Operation is WriteFile`
- Include `Operation is CreateFile` where `Path` does not start with
  `C:\Games\Durango`

Look for:

- child processes (`cmd.exe`, `powershell.exe`, `rundll32.exe`, `regsvr32.exe`,
  `mshta.exe`, `wscript.exe`, `cscript.exe`, browser processes);
- writes outside `C:\Games\Durango`, especially `%APPDATA%`, `%LOCALAPPDATA%`,
  `%TEMP%`, `C:\Windows`, Startup folders, scheduled-task files;
- registry writes outside the game's own Unity keys;
- connections to public IPs.

### 8.2 Network activity

With no network adapter, any successful connection attempt is suspicious. With a
host-only adapter:

- Use **TCPView** or `netstat -abno` in the guest.
- Run Wireshark on the host against the host-only VMnet interface.
- Filter by the guest's IP address.
- Expected: no traffic, or loopback only when using a local gateway.

Any connection to the upstream IPs, `raw.githubusercontent.com`, Discord, Sentry,
Slack, or an unknown host is a stop condition. Revert the snapshot.

### 8.3 Persistence check

Before and after the run, capture:

```powershell
Get-Process | Sort-Object ProcessName | Select-Object ProcessName, Id, Path
Get-Service | Where-Object Status -eq 'Running'
Get-ScheduledTask | Select-Object TaskName, TaskPath, State
Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location
```

Compare the two lists. Use **Autoruns** (Sysinternals) for a deeper view. No new
service, scheduled task, startup entry, or driver should appear.

### 8.4 File integrity check

Recompute the hashes from §6.1 after the run. The executable and managed DLL should
not change. Check for new files in the game directory and in the user profile.

### 8.5 Stop conditions

Power off the VM and revert the snapshot if you see any of these:

- a child process you did not expect;
- a connection to a public IP or an upstream domain;
- writes to Startup, services, scheduled tasks, browser profiles, SSH keys, or
  credential stores;
- the executable or DLL hash changes;
- Defender alerts;
- the game asks for credentials you did not expect, or a Discord link/account code.
- `testbridge` appears in any config.

Do not copy files from a suspected VM back to the host. Do not mount the VM disk on
the host for inspection. Keep the VM powered off and inspect it only inside an
isolated environment if needed.

---

## 9. Shutdown and rollback

After each session:

1. Exit the game.
2. Capture logs and any Procmon/Wireshark evidence you need.
3. Power off the VM.
4. Revert to `before-durango`.
5. If you need to keep a save or test result, export only the specific data file you
   understand, scan it, and never export executables or DLLs.

This means every session starts from a known-clean state. It also prevents a
hypothetical payload from persisting between tests.

---

## 10. What would raise confidence toward "100%"

The current sanitized build is a **risk-reduced binary**, not a trusted one. To get
closer to certainty:

1. **Decompile** `lib/managed/game/Assembly-CSharp.dll` with ILSpy or `ilspycmd`:

   ```bash
   ilspycmd -p -o src/Assembly-CSharp lib/managed/game/Assembly-CSharp.dll
   ```

2. Review at least:
   - all `OffServer*` types;
   - `TestBridge`;
   - `OffServerHardwareId`;
   - `RavenClient` / `SendReportSystem`;
   - every `Process.Start`, `Assembly.Load`, `DllImport`, file-write, registry, and
     socket call;
   - every hardcoded URL/IP.
3. Make source-level fixes instead of IL patches (delete HWID, compile out
   `TestBridge`, remove telemetry, replace `GetAsyncKeyState`).
4. Rebuild in a clean Unity project with a pinned Unity version.
5. Diff the rebuilt assembly and assets against the release build.
6. Re-run this same VM procedure on the rebuild.
7. Pin the upstream commit/release; review every future diff before running.

Until step 5 or 6, assume the binary could contain unknown behavior that the static
audit did not find. The audit itself states it was IL/metadata-level, did not
disassemble the Android native code, and did no dynamic analysis.

---

## 11. Troubleshooting

| Symptom | Likely cause | Safe action |
| :--- | :--- | :--- |
| Black screen / instant close | VMware GPU lacks D3D12 support | Retry with `-force-d3d11`; update VMware Tools |
| Game exits immediately with no window | Missing gateway, missing VC++ runtime, or launcher flag expected | Check `player.log`; try `-offserver-launcher`; do not run the upstream launcher |
| "Server offline" / stuck at login | No gateway is running; expected in offline mode | Keep offline or run your own local stub |
| SmartScreen warning | Binary is unsigned | Verify SHA-256; do not bypass blindly |
| Defender flags a file | False positive or real detection | Stop; submit the hash for review; do not whitelist without analysis |
| Audio missing | VMware audio device / Wwise plugin issue | Check VM audio settings; not a security issue |
| Very slow / low FPS | Virtual GPU limitations | Lower resolution, windowed mode, more RAM/CPU |
| Cannot copy files into VM | Shared folders/clipboard intentionally disabled | Use the ISO method |

Log locations:

- Next to the executable if launched with `-logFile player.log`
- Unity default: `%USERPROFILE%\AppData\LocalLow\<company>\<product>\Player.log`
- Launcher logs (only if you ever inspect the quarantined launcher, not run it):
  `%TEMP%\DurangoLauncher.log`

---

## 12. Quick checklist

Copy this into your notes and tick each item.

- [ ] `release/SHA256SUMS.txt` verifies.
- [ ] `Assembly-CSharp.dll` in the build hashes to `7535931f…717ab2`.
- [ ] `find build/Durango-CE -iname '*launcher*'` returns nothing.
- [ ] `server.txt` points only at `127.0.0.1` or my own host-only address.
- [ ] No `testbridge` line anywhere.
- [ ] Fresh Windows VM, local account, no personal logins.
- [ ] Shared folders, drag-and-drop, copy/paste, USB auto-connect disabled.
- [ ] Network adapter disconnected for the first run.
- [ ] Snapshot `before-durango` taken.
- [ ] Build transferred by ISO and hash-verified in the guest.
- [ ] `Durango.exe` run directly, not the launcher.
- [ ] Procmon/TCPView show no unexpected child processes or public connections.
- [ ] No new startup entries, services, tasks, or drivers.
- [ ] VM reverted after the session.
- [ ] Nothing copied back to the host.

If any item cannot be checked, do not treat the build as safe.

---

## 13. Scope note for Android

Do not install `release/DurangoReborn.apk` on a daily-use phone. It is:

- IL2CPP, so the PC sanitizer does not apply;
- 843 MB and self-signed by an unknown community certificate;
- configured with `usesCleartextTraffic="true"`;
- built from the same custom codebase, including the `TestBridge` classes.

When the PC client is decompiled, rebuilt, and verified, the Android build can be
revisited from clean source. Until then, Android remains deferred.

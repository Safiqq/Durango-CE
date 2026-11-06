# Durango: Community Edition – Project Progress & Technical Roadmap

> **Active Sprint**: Security Audit, Workspace Restructuring, Sanitization & Decompilation Preparation  
> **Repository**: [Safiqq/Durango-CE](https://github.com/Safiqq/Durango-CE) (Durango: Community Edition)  
> **Target Version**: `1.0.0-CE` (Unity 6000.6.3f1 / Mono / .NET 9)  
> **Security Baseline**: Cleaned & Sanitized local build available at `build/Durango-CE/`

---

## Navigation Tabs

* [Tab 1: Executive Overview & Milestone Tracker](#tab-1-executive-overview--milestone-tracker)
* [Tab 2: Comprehensive Security Audit & Threat Matrix](#tab-2-comprehensive-security-audit--threat-matrix)
* [Tab 3: Workspace Architecture & File Mapping](#tab-3-workspace-architecture--file-mapping)
* [Tab 4: Binary Sanitization & Toolchain](#tab-4-binary-sanitization--toolchain)
* [Tab 5: Git Commit History & Deliverables](#tab-5-git-commit-history--deliverables)
* [Tab 6: Next Steps & Active Development Plan](#tab-6-next-steps--active-development-plan)

---

## Tab 1: Executive Overview & Milestone Tracker

### Project Background
The repository is a fork of an unmaintained private server client for *Durango: Wild Lands* (*Durango: Reborn* / *Dorangu: Renewal*). The upstream repository was utilized as a live HTTP CDN and metadata host rather than a source repository. All client binaries were provided as compiled releases (`Durango-OffServer-Client-v3.3.6.zip`, `DurangoLauncher.exe`, `DurangoReborn.apk`).

### Current Maintainer Focus
- **PC (Windows x64) first.** Android is deferred until the PC client has been
  decompiled, reviewed, and rebuilt from source.
- **Run only inside a VMware VM.** The host is never used to execute upstream
  binaries. Use a fresh snapshot, no shared folders/clipboard/USB, and no bridged
  network.
- **Treat the build as untrusted until proven otherwise.** The sanitized build removes
  the known dangerous paths, but it is not a 100% malware-free guarantee.
- Full procedure: **[HOW_TO_RUN.md](HOW_TO_RUN.md)**.

### Milestone Tracker

| Milestone | Status | Details |
| :--- | :---: | :--- |
| **M1: Static Security Audit (PC)** | **COMPLETED** | Bytecode & PE analysis of `Assembly-CSharp.dll`, `DurangoLauncher.exe`, and engine DLLs. |
| **M2: Static Security Audit (Android)** | **COMPLETED** | IL2CPP metadata (`global-metadata.dat`), ELF shared libraries (`libil2cpp.so`), and APK signature audit. |
| **M3: Workspace Restructuring** | **COMPLETED** | Organized 2,410 files into `/config`, `/lib`, `/src`, `/assets`, `/scripts`, `/docs`, `/quarantine`. |
| **M4: Automated Binary Sanitization** | **COMPLETED** | Developed `sanitize_client.py` to neutralize test sockets, HWID harvesting, and leaked secrets at IL level. |
| **M5: Runnable Local Assembly** | **COMPLETED** | `scripts/restructure.py assemble` builds safe, runnable client in `build/Durango-CE/`. |
| **M6: Git History & Documentation** | **COMPLETED** | Atomic conventional commits logged; full reports saved in `docs/`. |
| **M7: C# Source Decompilation** | **COMPLETED** | Decompiled all five game assemblies with `ilspycmd` into `src/`. Source builds with 0 errors on .NET SDK 9.0. See `docs/DECOMPILATION.md`. |
| **M7.1: Source-Level Sanitization** | **COMPLETED** | Removed TestBridge networking, hardware fingerprinting, Raven/Sentry telemetry, and device-metadata collection from source. Loopback-bound the embedded offline server. |
| **M7.2: Visual Branding & Assets** | **COMPLETED** | Restored authentic title typography and dynamic server connection indicators. |
| **M8: Embedded Local Gateway & Self-Contained Execution** | **COMPLETED** | Built `LocalGatewayServer.cs` directly into `Assembly-CSharp.dll` (`[RuntimeInitializeOnLoadMethod]`). Auto-starts loopback gateway on `127.0.0.1:8190/8290`, resolves 24% load screen hang (`Yaml.Util.Loader`), and serves all handshake routes. `Durango.exe` runs completely standalone without external Python or batch scripts. |
| **M8.1: Cleanroom Community Launcher** | **COMPLETED** | Decompiled and hardened launcher in `src/DurangoLauncher/`. Neutralized `DeleteExtras` file wiping and `LauncherSelfUpdate` unsigned remote execution. Compiles to `build/launcher-dist/DurangoLauncher.exe`. |
| **M8.2: Authentic Icon & PE Metadata Branding** | **COMPLETED** | Extracted authentic 432x432 claw emblem from APK, generated `assets/branding/durango.ico`, and injected into `Durango.exe` and `DurangoLauncher.exe`. Set official PE metadata (`Durango: Community Edition`). |
| **M8.3: Custom PE DIB Icon & Character Slot Bypass** | **COMPLETED** | High-DPI dinosaur icon injected into `Durango.exe` PE resources. Integrated `config.json` system and bypassed cash-shop character slot purchase restrictions. |
| **M9: Android Clean Rebuild** | *DEFERRED* | Out of scope while PC-first verification is in progress; rebuild only from reviewed C# source. |
| **M10: Modular Server Architecture (Dedicated & Listen)** | **IN PROGRESS** | Decoupling local emulator into standalone headless server (`DurangoServer`) and embedded listen server. Reference: `docs/MODULAR_SERVER_ARCHITECTURE.md`. |

---

## Tab 2: Comprehensive Security Audit & Threat Matrix

### Summary of Vulnerabilities & Findings

```
[CRITICAL]  Durango.Development.TestBridge (Unauthenticated Local TCP Command Server)
[HIGH]      Durango.Logic.Clusters.OffServerHardwareId (Registry & SMBIOS UUID Harvester)
[HIGH]      Plaintext HTTP Gateways (Unencrypted SessionToken, Account UUID, HWID transmission)
[HIGH]      DurangoLauncher.dll GameSync::DeleteExtras (Unchecked recursive deletion of unlisted files)
[HIGH]      DurangoLauncher.dll LauncherSelfUpdate (Unsigned remote binary replacement & execution)
[MEDIUM]    RavenClient (Telemetry uploading gzipped error logs to legacy Nexon Sentry DSN)
[MEDIUM]    SendReportSystem (Diagnostic logs & player details sent to legacy Nexon Slack webhook)
[MEDIUM]    WindowsInput.WinInput (Global asynchronous keyboard state polling via user32!GetAsyncKeyState)
[MEDIUM]    AndroidManifest.xml (Cleartext traffic enabled: usesCleartextTraffic="true")
```

### Threat Assessment Matrix

| Threat ID | Component | Location / RVA | Risk Summary | Status |
| :--- | :--- | :--- | :--- | :---: |
| **THREAT-01** | `TestBridge` | `Assembly-CSharp.dll`<br>`0x2d4784` | Unauthenticated TCP socket on `127.0.0.1:<port>`. Exposes `movecode click` (account transfer code theft), `discordlink start`, `screenshot [path]`, and 40+ cheat commands. | **NEUTRALIZED** (binary stub + listener removed from source) |
| **THREAT-02** | `OffServerHardwareId` | `Assembly-CSharp.dll`<br>`0x2cb3a8` | Reads `HKLM\...\Cryptography\MachineGuid` and motherboard SMBIOS UUID via `GetSystemFirmwareTable`. Sent as `hw` form field. | **NEUTRALIZED** (binary stub + fingerprint code removed from source) |
| **THREAT-03** | Cleartext HTTP | `servers.json`<br>`server.txt` | Sends credentials over cleartext HTTP to hardcoded legacy upstream IPs without TLS encryption. | **MITIGATED** (All third-party IPs removed; default set to `127.0.0.1` template with runtime TLS warnings) |
| **THREAT-04** | `DeleteExtras` | `DurangoLauncher.dll`<br>`0xd6d8` | Recursively deletes every file in the directory not listed in the remote JSON manifest. | **NEUTRALIZED** (Stubbed to no-op in `src/DurangoLauncher`) |
| **THREAT-05** | Unsigned Auto-Update | `DurangoLauncher.dll`<br>`0x54ee4` | Downloads new `.exe` from URL and executes via `Process.Start` without Authenticode check. | **NEUTRALIZED** (Remote replacement disabled in `src/DurangoLauncher`) |
| **THREAT-06** | Sentry Telemetry | `RavenClient`<br>`0xa0594` | Legacy Nexon DSN with embedded private key (`https://<redacted>@app.getsentry.com/48217`). | **NEUTRALIZED** (Stubbed to `ret` & loopback string) |
| **THREAT-07** | Slack Webhook | `SendReportSystem`<br>`0xa5128` | Transmits diagnostic logs to hardcoded Slack webhook (`https://hooks.slack.com/services/<redacted>`). | **NEUTRALIZED** (Loopback string replacement) |
| **THREAT-08** | APK Cleartext | `DurangoReborn.apk`<br>`AndroidManifest.xml` | `usesCleartextTraffic="true"` allows unencrypted traffic on Android. | **DOCUMENTED** (Needs rebuild from source) |
| **THREAT-09** | APK Signing | `DurangoReborn.apk` | Self-signed community certificate (`C=TH, ST=Bangkok, OU=Community Server`). | **VERIFIED** (Valid fan-server cert) |

---

## Tab 3: Workspace Architecture & File Mapping

### Directory Layout

```
Durango-CE/
├── config/                      # All configuration and manifests
│   ├── server.local.txt         # Safe local development gateway configuration (127.0.0.1)
│   └── unity/                   # Unity runtime configs (boot.config, ScriptingAssemblies.json, etc.)
├── lib/                         # Managed and native libraries
│   ├── managed/game/            # [SOURCE-EQUIVALENT] Assembly-CSharp.dll, Durango.Localization.Overrides.dll
│   ├── managed/third-party/     # Third-party managed plugins (LiteNetLib, Concentus, MsgPack, etc.)
│   ├── managed/engine/          # [gitignored] Unity engine and Mono BCL assemblies
│   ├── native/plugins/          # [gitignored] x64 native DLLs (Wwise, ffmpeg, snappy, Dwarf)
│   └── engine/                  # [gitignored] UnityPlayer.dll, Durango.exe, D3D12Core.dll, MonoBleedingEdge
├── src/                         # Source-level workspace (M7 & M8 complete)
│   ├── Assembly-CSharp/         # Decompiled main game assembly (includes LocalGatewayServer)
│   ├── Assembly-CSharp-firstpass/  # Decompiled first-pass assembly
│   ├── Durango.Localization.Overrides/  # Decompiled localization overrides
│   ├── Debug/                   # Decompiled Debug.dll
│   ├── ExternalLibrary/         # Decompiled plugins and libraries (880 files)
│   ├── DurangoLauncher/         # Decompiled and hardened .NET 9 WinForms launcher
│   └── inventory/               # Injected custom code type inventory (214 types mapped)
├── assets/                      # [gitignored] Game assets
│   ├── branding/                # [tracked] Authentic Durango game icons (ICO, PNG)
│   ├── streaming/               # StreamingAssets (Audio soundbanks, Movies, AssetBundles)
│   └── unity/data/              # Serialized scene files (level0-3, resources.assets, globalgamemanagers)
├── scripts/                     # Toolchain & automation
│   ├── restructure.py           # Release extractor & build assembler
│   ├── sanitize_client.py       # Standalone binary IL patcher
│   ├── decompile.sh             # Reproducible ILSpy decompilation
│   ├── build.sh                 # Builds all src/ projects
│   └── lib/dotnet_meta.py       # Custom ECMA-335 metadata parser & CIL disassembler
├── docs/                        # Project documentation & audit reports
│   ├── SECURITY_AUDIT.md        # Comprehensive PC security audit
│   ├── ANDROID_SECURITY_AUDIT.md# Android IL2CPP security audit
│   ├── DECOMPILATION.md         # M7 source tree, build, and hardening notes
│   ├── PROJECT_PROGRESS.md      # This progress tracking dashboard
│   ├── sanitize_report.json     # Sanitizer verification log
│   ├── manifest_sha256.txt      # Cryptographic SHA-256 hashes of all 2,410 extracted files
│   ├── vendor-README.txt        # Preserved upstream README
│   └── vendor-THIRD-PARTY-NOTICES.txt # Preserved upstream licensing notices
├── quarantine/                  # [gitignored] Untrusted files isolated from execution
│   ├── DurangoLauncher.exe      # Untrusted original launcher
│   └── launcher-unpacked/       # Extracted .NET 9 assembly and runtime configs
├── build/                       # [gitignored] Working sanitized client build (assembled via hardlinks)
├── release/                     # [gitignored] Immutable upstream release archives
├── servers.json                 # Maintained at repo root for manifest URL compatibility
├── pc-launcher.json             # Maintained at repo root
└── android.json                 # Maintained at repo root
```

### File Classification

* **Source-Equivalent (Decompile target)**:
  * `lib/managed/game/Assembly-CSharp.dll` (7.2 MB, 2,872 types)
  * `lib/managed/game/Durango.Localization.Overrides.dll` (8.1 KB)
* **Compiled Binaries**:
  * `lib/engine/Durango.exe`, `lib/engine/UnityPlayer.dll`, `lib/native/plugins/*.dll`
* **Generic Engine Assets**:
  * `assets/streaming/`, `assets/unity/data/`
* **Quarantined**:
  * `quarantine/DurangoLauncher.exe` (Never execute or distribute)

---

## Tab 4: Binary Sanitization & Toolchain

### Sanitizer Architecture (`scripts/sanitize_client.py`)
Initial binary sanitization operated directly on the shipped compiled bytecode (`lib/managed/game/Assembly-CSharp.dll`) at the CIL opcode level, which has since been superseded by full C# source decompilation and hardening in `src/Assembly-CSharp/`:

1. **Bytecode Level (Immediate `ret` 0x2A)**:
   * `OffServerHardwareId::AddTo`: Neutralizes registry and SMBIOS UUID extraction.
   * `TestBridge::StartListener`: Disables local TCP command server.
   * `RavenClient::CaptureUntiyLog`, `RavenClient::Send`, `RavenClient::.cctor`: Completely stops Sentry crash log uploads.
2. **String Heap Level (User String #US Redaction)**:
   * Replaces the hardcoded Sentry DSN with `http://k:k@127.0.0.1:9/111...` (exact byte length preserved).
   * Replaces the hardcoded Slack Incoming Webhook with `http://127.0.0.1:9/xxx...` (exact byte length preserved).
3. **Safety Properties**:
   * Never modifies input file (outputs `Assembly-CSharp.dll.sanitized`).
   * Verifies method exists, has a single definition, and returns `void`.
   * Exactly 165 bytes modified in the 7.2 MB binary.
   * 100% idempotent.

### Toolchain Commands

```bash
# Extract original release archive into workspace
python3 scripts/restructure.py extract --zip release/Durango-OffServer-Client-v3.3.6.zip

# Run binary sanitizer (reference binary pipeline)
python3 scripts/sanitize_client.py lib/managed/game/Assembly-CSharp.dll lib/managed/game/Assembly-CSharp.dll.sanitized

# Assemble clean, playable client directory (using built DLLs from src/ or sanitized binaries)
python3 scripts/restructure.py assemble --out build/Durango-CE

# Build all decompiled projects from source
./scripts/build.sh
```

---

## Tab 5: Git Commit History & Deliverables

Repository commits follow strict Conventional Commits:

```
* docs: update documentation and project progress
* build(scripts): support assembling rebuilt DLLs from decompiled source
* feat(ui): implement Community Edition v1.0 branding, dynamic server label, and English localization
* fix(render): reset UV rect for static background and eliminate upside-down rendering
* chore(gitignore): ignore screenshots and allow custom title branding assets
* docs(progress): record VM test results and update M8 milestone roadmap
* docs(gateway): specify runtime dual-mode and local world architecture
* docs: add contributing guidelines and commit conventions
* build(gateway): add local gateway scripts for PowerShell and Python
* docs: mark M7 complete and update security status
* docs(run): add VMware run guide and server setup options
* docs(source): document M7 decompilation and hardening
* build(source): add reproducible decompile and build scripts
* feat(source): decompile PC client assemblies into C# projects
* chore(gitignore): ignore local .NET build output
* docs: add tabbed project progress and active development dashboard
* docs: add Android IL2CPP security audit report for DurangoReborn.apk
* docs: keep vendor notices and add type inventory of custom code
* docs: add security audit of the 3.3.6 client and sanitization report
* chore: add local-dev server.txt and Unity runtime configs
* feat: add IL sanitizer for Assembly-CSharp.dll
* feat: add script to extract the release zip into a structured workspace
* chore: ignore extracted release artefacts and build output
```

---

## Tab 6: Next Steps & Active Development Plan

### Phase 1: Source Decompilation & Source Tree Setup
- [x] Install .NET SDK / `ilspycmd` and decompile all game assemblies.
- [x] Place decompiled source into `src/Assembly-CSharp/` and the four support projects.
- [x] Add `scripts/decompile.sh` and document the exact tool versions and commands.
- [x] Make all five projects build with 0 errors (`scripts/build.sh`).

### Phase 2: Permanent Source Code Sanitization
- [x] Delete `OffServerHardwareId` fingerprinting logic in C# source.
- [x] Remove the `TestBridge` TCP listener and ignore `testbridge=`.
- [x] Remove `RavenClient`, `ErrorReporter`, and `SendReportSystem.SendServerStatus` telemetry.
- [x] Bind the embedded `Durango.Offline` server to loopback only.
- [x] Disable the default upstream `servers_url` fetch.
- [ ] Replace `user32!GetAsyncKeyState` in `WindowsInput.WinInput` with Unity's standard `Input.GetKey` (audit pending).
- [ ] Runtime-test the rebuilt DLL in a VM snapshot.

### Phase 3: Network & Gateway Integration (Milestone M8)
- [x] Verified sanitized client runs in Windows 11 VMware VM (Gate G2/G3 passed; zero network leaks).
- [x] Developed standalone local gateway (`scripts/local_gateway.ps1`, `scripts/local_gateway.py`).
- [x] Verified initial cluster handshake: client connects to `Local Server`, parses `/status`, `/accounts`, and renders character slot (`No Character`).
- [x] **M8 (Embedded Local Gateway)**: Integrated `LocalGatewayServer.cs` into `Assembly-CSharp.dll` with auto-listener on `127.0.0.1:8290/8190`. Resolves `/assets/*` and serves `Info.5.2.1.json`, eliminating 24% load screen hang.
- [x] **M8.1 (Cleanroom Community Launcher)**: Decompiled and sanitized `src/DurangoLauncher/` with .NET 9. Neutralized `DeleteExtras` and `LauncherSelfUpdate`.
- [x] **M8.2 (Authentic Branding & PE Metadata)**: Injected authentic claw emblem and `Durango: Community Edition` metadata into `Durango.exe` and `DurangoLauncher.exe`.
- [ ] Final gameplay verification: enter Prologue train and open-world survival in isolated VM.

### Phase 4: Clean Android Pipeline
- [ ] Open project in Unity with Android Build Support.
- [ ] Set `usesCleartextTraffic="false"` and configure custom domain network security config.
- [ ] Build a self-signed, clean `DurangoReborn.apk`.

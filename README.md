# Durango: Community Edition (v1.0.0-CE)

> **Repository**: [Safiqq/Durango-CE](https://github.com/Safiqq/Durango-CE)  
> **Upstream Foundations**: Based on foundational community research by [SoloCode228](https://github.com/SoloCode228) and [ShuuuuShi](https://github.com/ShuuuuShi)  
> **Maintained by**: Durango Community Contributors  
> **Scope**: Cross-Platform Preservation Project (Windows, Android, macOS, iOS)

---

## 1. Overview & Project Metadata

**Durango: Community Edition** is an independent, non-commercial open-source preservation initiative dedicated to keeping *Durango: Wild Lands* playable following the end of official live service in December 2019.

| Property | Details |
| :--- | :--- |
| **Original Publisher** | NEXON Korea Corporation / What! Studio (2018–2019) |
| **Legal Status** | Unofficial fan-made open-source preservation project. Zero commercial affiliation. |
| **Licensing & Monetization** | 100% Free and Open-Source. Zero paywalls, microtransactions, or paid game features. |
| **Engine & Architecture** | Unity Mono / IL2CPP · .NET SDK 9.0 · DirectX 11 / 12 Agility · Metal |
| **Primary Executable** | `Durango.exe` (All-in-one standalone client: host local LAN worlds & join servers) |
| **Optional Utility** | `DurangoLauncher.exe` (Optional desktop companion) |

---

## 2. Platform Status & Readiness Assessment

| Platform | Implementation State | Verification Status | Operational Notes |
| :--- | :---: | :---: | :--- |
| **Windows** | **Ready** | **Verified (Bare Metal & VM)** | Full standalone client with internal background server (`127.0.0.1:8190`). Decompiled C# source compiling cleanly with .NET 9. |
| **Android** | **Not Ready** | **Audited (Pending Device Testing)** | Static security audit of APK and IL2CPP metadata complete ([`docs/ANDROID_SECURITY_AUDIT.md`](docs/ANDROID_SECURITY_AUDIT.md)). Next development priority before macOS. |
| **macOS** | **Not Ready** | **Architecture Defined** | Configured to run via Whisky (GPTK / D3DMetal) and VMware Fusion. Physical hardware testing on Apple Silicon (M1–M4) scheduled after Android. |
| **iOS** | **Not Ready** | **Feasibility Research** | Requires IL2CPP compilation to Xcode, Apple code signing or AltStore sideloading, and native ARM64 audio plugin integration. |

---

## 3. Strategic Masterplan & Future Roadmap

| Phase / Milestone | Target Platform | Objective & Scope | Status |
| :--- | :--- | :--- | :---: |
| **Phase 1: Source Decompilation** | Windows (PC) | Decompile all 5 client assemblies into readable C# source compiling cleanly with .NET 9 SDK. | **Completed** |
| **Phase 2: Security Sanitization** | Windows (PC) | Remove developer `TestBridge` test socket, hardware ID collection, and leftover live-service telemetry. | **Completed** |
| **Phase 3: Internal Background Server** | Windows (PC) | Integrate background server into client runtime for zero-setup, single-executable offline play. | **Completed** |
| **Phase 4: Launcher Hardening** | Windows (PC) | Cleanroom decompilation and hardening. Remove file-wiping routines and unsigned remote update hooks. | **Completed** |
| **Phase 5: Android Client Validation & Build** | Android | Perform physical device testing on Android; investigate compiling clean Android binaries directly from reviewed C# source. | **In Progress** |
| **Phase 6: macOS Hardware Verification** | macOS | Conduct rigorous benchmark and gameplay stability testing on physical MacBook Air, Pro, and Mac mini hardware via Whisky / D3DMetal. | **Planned** |
| **Phase 7: iOS Feasibility & Porting** | iOS | Investigate IPA packaging, AltStore/Sideloadly unsigned deployment, and IL2CPP Xcode project generation. | **Planned** |
| **Phase 8: Protocol Expansion** | Dedicated Server | Implement remaining Durango network packet handlers (island warp transitions, domain claims, crafting stations, creature taming). | **Planned** |
| **Phase 9: Automated CI/CD Pipelines** | Multi-Platform | Establish automated GitHub Actions builds, release asset packaging, and automated SHA-256 integrity verification. | **Planned** |

---

## 4. Quick Start Matrix (How to Play)

`Durango.exe` is the all-in-one standalone client that handles both hosting and joining servers directly with zero external tools:

| Operational Mode | Target Platform | Primary Action | Required Configuration |
| :--- | :--- | :--- | :--- |
| **Host Local World (Solo / LAN)** | Windows 10 / 11 | Double-click `Durango.exe` | None. The embedded server starts in the background and automatically exposes on your LAN/Wi-Fi. |
| **Join Another Local/LAN Host** | Windows 10 / 11 | Double-click `Durango.exe` | Click the server name on the title screen to pick your server, or set `gateway=http://<host-ip>:8190` in `server.txt`. |
| **Join Remote / Community Server** | Windows 10 / 11 | Double-click `Durango.exe` | Click the server name on the title screen to pick from `servers.json`, or configure `server.txt`. |

---

## 5. Building from Source

The entire stack compiles cleanly from source using the .NET 9 SDK and Python 3.

### Build Toolchain Requirements

| Dependency | Minimum Version | Purpose |
| :--- | :--- | :--- |
| **.NET SDK** | `9.0.100+` | Compiles managed game assemblies and launcher executable |
| **Python** | `3.8+` | Workspace layout automation and game assembly packaging |
| **Bash** | Standard POSIX | Executes automated build pipeline script (`scripts/build.sh`) |

### Compilation Commands

```bash
# 1. Compile managed game assemblies into src/*/bin/Release/netstandard2.1/
./scripts/build.sh

# 2. Publish standalone community launcher
dotnet publish src/DurangoLauncher/DurangoLauncher.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o build/launcher-dist

# 3. Assemble complete runnable client distribution
python3 scripts/restructure.py assemble --out build/Durango-CE
cp build/launcher-dist/DurangoLauncher.exe build/Durango-CE/DurangoLauncher.exe
```

### Distribution File Layout

| Path in `build/Durango-CE/` | Type | Purpose |
| :--- | :--- | :--- |
| `Durango.exe` | Executable | Standalone game executable (with embedded background local server) |
| `Durango_Data/` | Directory | Managed assemblies, engine configuration, 2,177 compiled asset bundles |
| `DurangoLauncher.exe` | Executable | Community launcher utility for server selection |
| `UnityPlayer.dll` | Library | Unity engine runtime binary |
| `servers.json` | Config | Dynamic runtime server directory |
| `server.txt` | Config | Primary network configuration (Local: `127.0.0.1:8190`, or remote IP) |
| `D3D12/` | Directory | DirectX 12 Agility runtime components |
| `MonoBleedingEdge/` | Directory | Mono embedded scripting runtime |

---

## 6. DevSecOps Verification Matrix

For security researchers, system administrators, or cautious players who wish to independently verify runtime isolation:

| Audit Dimension | Tool / Utility | Inspection Procedure | Expected Safe Result |
| :--- | :--- | :--- | :--- |
| **Network Traffic** | **Wireshark** | Capture filter: `tcp.port == 8190 \|\| tcp.port == 8191 \|\| tcp.port == 8290 \|\| dns` | 100% loopback traffic (`127.0.0.1`). Zero outbound DNS requests, zero third-party handshakes. |
| **Process Hierarchy** | **Sysinternals ProcMon** | Filter: `Process Name is Durango.exe` & `Operation is Process Create` | Zero spawned child processes (`cmd.exe`, `powershell.exe`, or browser sessions). |
| **Filesystem Activity** | **Sysinternals ProcMon** | Filter: `Process Name is Durango.exe` & `Operation is CreateFile` | Writes strictly confined to game folder and `%LOCALAPPDATA%\..\LocalLow\NEXON\Durango`. |
| **Registry Operations** | **Sysinternals ProcMon** | Filter: `Operation is RegSetValue` | Zero persistence mechanisms, autostart keys, or service registrations created. |

For the full sandbox isolation procedure, see **[docs/HOW_TO_RUN.md](docs/HOW_TO_RUN.md)**.

---

## 7. Supporting the Project (Donations & Sponsorships)

*Durango: Community Edition* is an independent, non-commercial open-source preservation effort. The game client, decompiled source code, and offline tools are **100% free and open-source**.

If you appreciate the hundreds of hours invested in static code audits, C# source decompilation, security hardening, and ongoing client maintenance, voluntary developer tips and sponsorships are warmly welcomed:

| Platform | Link / Address | Description |
| :--- | :--- | :--- |
| **Ko-fi** | [ko-fi.com/safiqq](https://ko-fi.com/safiqq) | Direct developer tip jar with 0% platform fee |

> [!NOTE]
> All donations are strictly voluntary gratuities to support the developer's time, hardware costs, and open-source tooling research. Donations do not purchase game access, copyrighted assets, or private server goods.

---

## 8. Documentation Index

| Document | Description |
| :--- | :--- |
| **[docs/TRIBUTE.md](docs/TRIBUTE.md)** | Community tribute to SoloCode228 and ShuuuuShi |
| **[docs/PROJECT_PROGRESS.md](docs/PROJECT_PROGRESS.md)** | Detailed milestone logs, changelogs, and technical deliverables |
| **[docs/RUNTIME_DUAL_MODE.md](docs/RUNTIME_DUAL_MODE.md)** | Remote server vs. local offline world networking architecture |
| **[docs/DECOMPILATION.md](docs/DECOMPILATION.md)** | C# source architecture, project inventories, and assembly references |
| **[docs/HOW_TO_RUN.md](docs/HOW_TO_RUN.md)** | Bare-metal execution and DevSecOps sandbox verification guide |
| **[docs/MACOS_SETUP.md](docs/MACOS_SETUP.md)** | Setup guide for running on macOS via Whisky and VMware Fusion |
| **[docs/SERVER_SETUP.md](docs/SERVER_SETUP.md)** | Server connection guide and dedicated server compilation instructions |
| **[docs/SECURITY_AUDIT.md](docs/SECURITY_AUDIT.md)** | Comprehensive security audit report and threat analysis |
| **[CONTRIBUTING.md](CONTRIBUTING.md)** | Contribution standards, code style, and Conventional Commits guidelines |

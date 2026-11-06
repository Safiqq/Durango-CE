# Decompilation (M7) — Source Tree, Build, and Hardening

This document records how the compiled client assemblies were turned into C# source,
what was changed during sanitization, how to rebuild, and what is still unverified.

> **Scope:** PC / Windows x64 client only. Android remains deferred.
>
> **Safety:** the source is for review and local development. Do not run unknown
> binaries or servers on your real machine. Use the VMware VM described in
> [HOW_TO_RUN.md](HOW_TO_RUN.md).

---

## 1. Result at a glance

| Item | Value |
| :--- | :--- |
| Decompiler | `ilspycmd` **9.1.0.7988** (ICSharpCode.Decompiler 9.1.0.7988) |
| SDK | .NET SDK **9.0.320** |
| Input | `lib/managed/game/Assembly-CSharp.dll.sanitized` |
| Sanitized input SHA-256 | `7535931f4fc14ed03f09960c2976a04847667d6b0cbf349c64e6f8886a717ab2` |
| Source files | **4,813** `.cs` files, about **625,000** lines |
| Main source tree | `src/Assembly-CSharp/` — 3,921 C# files, about 23 MB |
| Build status | All five projects compile with **0 errors** |
| Main rebuilt DLL | `src/Assembly-CSharp/bin/Release/netstandard2.1/Assembly-CSharp.dll` (7,226,368 bytes) |
| Main rebuilt SHA-256 | `cf760eb0d8147846764355acfa4230fbc6e7eba684fba32d4502c01865b59451` |
| Runtime tested? | **Yes.** Loaded in Windows 11 VMware VM; title screen, NGUI audio/render, local gateway handshake, and character slot verified. |

The rebuilt DLL is **not** byte-identical to the shipped 3.3.6 DLL. Different compiler,
reference assembly, and source-level hardening all change the output. Treat it as a
development build, not a drop-in release replacement.

---

## 2. Source layout

```
src/
├── Assembly-CSharp/                 # Main game code (includes LocalGatewayServer)
│   ├── Assembly-CSharp.csproj
│   ├── Durango.Development/         # TestBridge (neutralized), dev commands
│   ├── Durango.Logic.Clusters/      # OffServerLink, account flow
│   ├── Durango.Network/             # TCP/UDP transport
│   ├── Durango.Offline/             # Embedded offline server & LocalGatewayServer
│   ├── Durango.UI/                  # UI code
│   └── ...
├── Assembly-CSharp-firstpass/       # 3 files (Unity image effects)
├── Durango.Localization.Overrides/  # 6 files
├── Debug/                           # 3 files
├── ExternalLibrary/                 # 880 C# files (plugins, SharpRaven, Newtonsoft, etc.)
├── DurangoLauncher/                 # Cleanroom .NET 9 WinForms community launcher
└── inventory/                       # Existing type-name inventories
```

Each project has its own `.csproj` at `src/<name>/<name>.csproj`.

Source hashes of the inputs:

| Assembly | SHA-256 |
| :--- | :--- |
| `Assembly-CSharp.dll` (original) | `c0a0a48b16846b6a6bdbb4ca61953266df4b281ae35191e1e8e0846f7a2252ef` |
| `Assembly-CSharp.dll.sanitized` | `7535931f4fc14ed03f09960c2976a04847667d6b0cbf349c64e6f8886a717ab2` |
| `Assembly-CSharp-firstpass.dll` | `ab5dfa2aa6ce1ef043c067195e8212a8354bfe0415cb505e79db95965d53823d` |
| `Debug.dll` | `d4bbf4fe68df23dd44e5c3cd7b791b45739d88ab134ba4881307f33ed0a28c39` |
| `Durango.Localization.Overrides.dll` | `4294c53dd35cca2dac31f66c8cab73a076b7b6b853d5ed949edea0df030829f0` |
| `ExternalLibrary.dll` | `307785882f54ed38b44470a9cea5604442acdfc1c5d3c8bec2490c6440fa3349` |

The main assembly was decompiled from the **sanitized** DLL, so the removed secrets
and the two IL-stubbed methods are already absent from the generated source.

---

## 3. Reproduce the decompilation

Install the toolchain once:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROLL_FORWARD=Major

curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 9.0 --install-dir "$HOME/.dotnet" --no-path

dotnet tool install --global ilspycmd --version 9.1.0.7988
```

`ilspycmd` 9.1 targets .NET 8. On a .NET 9-only machine it needs
`DOTNET_ROLL_FORWARD=Major`. Newer `ilspycmd` versions can fail to install on the
9.0 SDK because of NuGet manifest-format checks; 9.1.0.7988 is known-good.

Regenerate raw output into a staging directory:

```bash
scripts/decompile.sh
# or:
scripts/decompile.sh /tmp/opencode/durango-source
```

The script:

1. Decompiles `Assembly-CSharp.dll.sanitized` with project mode.
2. Decompiles the other four game DLLs.
3. Rewrites ILSpy's `HintPath` values so they resolve from the project directory.

`--in-place` overwrites `src/` and **discards the manual hardening edits**. Do not use
it unless you intend to re-apply the changes from §5.

The exact main command used for this tree:

```bash
ilspycmd -p \
  -o /tmp/opencode/decompile-test \
  -r lib/managed/engine \
  -r lib/managed/third-party \
  -r lib/managed/game \
  --disable-updatecheck \
  lib/managed/game/Assembly-CSharp.dll.sanitized
```

---

## 4. Building the source

```bash
scripts/build.sh
# or:
DOTNET="$HOME/.dotnet/dotnet" scripts/build.sh
```

The projects target **`netstandard2.1`**, matching the Unity 6 Mono profile. ILSpy
initially guessed `net40`, which produced thousands of false errors because the Unity
engine DLLs reference .NET Standard 2.1.

Two project-level settings make the build work on Linux:

```xml
<TargetFramework>netstandard2.1</TargetFramework>
<DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
```

With implicit framework references disabled, the projects explicitly reference the
Unity-provided BCL from `lib/managed/engine/`:

- `mscorlib.dll`
- `System.dll`
- `System.Core.dll`
- `System.Runtime.dll`
- `netstandard.dll`
- `System.Memory.dll`
- `System.Runtime.CompilerServices.Unsafe.dll`
- `System.Xml.dll`

`UnityEngine.ScriptingModule.dll` was also added because many `[Preserve]` attributes
require it.

### Build results

| Project | Errors | Warnings |
| :--- | :---: | :---: |
| `Assembly-CSharp` | 0 | 5,413 |
| `Assembly-CSharp-firstpass` | 0 | 0 |
| `Debug` | 0 | 0 |
| `Durango.Localization.Overrides` | 0 | 0 |
| `ExternalLibrary` | 0 | 76 |

The warnings are mostly unused decompiler-generated fields, obsolete Unity APIs, and
nullable-flow noise. They do not block the build.

Build output is ignored by git:

```gitignore
/src/**/bin/
/src/**/obj/
/build/decompile/
```

### Is the rebuilt DLL playable?

Not yet verified. To test it you would need to replace `Assembly-CSharp.dll` inside a
copy of the assembled client **in the VM**, keep a snapshot, and watch for errors.
Do not overwrite the known-good sanitized DLL without a backup. A rebuilt DLL can fail
at runtime because of missing compiler-generated details, reflection expectations, or
Unity serialization layout changes.

---

## 5. Source-level hardening applied

The binary sanitizer already stubbed two methods. The source tree goes further so a
rebuild cannot silently restore the dangerous behavior.

| Area | File | Change |
| :--- | :--- | :--- |
| TestBridge server | `Durango.Development/TestBridge.cs` | Removed TCP listener, accept loop, client loop, dispatcher, and reply writer. `EnsureStarted` is a no-op. |
| TestBridge config | `Durango.Logic.Clusters/OffServerLink.cs` | `testbridge=` is ignored; `TestBridgePort` always returns 0; `CreateCluster` no longer starts the bridge. |
| Hardware fingerprint | `Durango.Logic.Clusters/OffServerHardwareId.cs` | Removed registry, SMBIOS, SHA-256 fingerprint and header injection. `Value` is null; `AddTo` is a no-op. |
| Sentry/Raven telemetry | `RavenClient.cs` | Removed DSN, gzip payload, and send path. No-op. |
| Device metadata collection | `ErrorReporter.cs` | Removed processor/GPU/device-ID/player-ID tag collection. No-op. |
| Slack-style reporting | `SendReportSystem.cs` | `SendServerStatus` is a local no-op. `SendReport` still posts to the **configured gateway** `/reports` endpoint, which is a normal server feature. |
| Default server list | `Durango.Logic.Clusters/OffServerLink.cs` | Default `servers_url` is empty. No upstream GitHub fetch. Remote lists are opt-in only. |
| Embedded TCP server | `Durango.Offline/Listener.cs` | Binds `127.0.0.1`, not `IPAddress.Any`. |
| Embedded HTTP gateway | `Durango.Offline/WebServer.cs` | Prefix is `http://127.0.0.1:<port>/`; UDP knock socket binds loopback. |

### Decompiler artifacts fixed

ILSpy output needed a few source repairs to compile:

- `CameraController.Target(null, ...)` was ambiguous between the `GameObject` and
  `Vector3` overloads. All 16 calls now cast to `(UnityEngine.GameObject)null`.
- `TestBridge.OfflineRangeFx` had local functions emitted in the wrong scope and a
  reused variable name. They were moved to a single declaration and the loop was
  rewritten.
- `PetUtil` had a `foreach` variable shadowing the outer `ItemData item` parameter.
- `Commands.cs` assigned a `QuestState` to a `PacketHeader` discard parameter; the
  lambda parameter was renamed.
- `ExternalLibrary` had `[SerializeField]` on a class and an enum, an obsolete
  `GUIText` field, and a `TextEditor.content` call. Those were removed or updated.
- `ExternalLibrary` needs `LangVersion 13.0` for one iterator method.

### Deliberately left alone

These still exist in the source and are documented rather than silently changed:

- Original Nexon/Akamai asset CDN URLs in `Durango.Offline/Gateway.cs`
  (`assetbundles.k.nexon.com`, `durango-assetbundles.akamaized.net`). They are used
  only when the client asks for missing bundles. Rewriting them would break asset
  loading.
- `http://maps.durango.nexon.com/` in `InfoTooltip.cs` — an in-game "open map" link.
- `WebEventSystem.cs` still appends the platform token and player ID to an event URL.
  This is a server-driven webview feature; only use it with a gateway you trust.
- `ExternalLibrary/SharpRaven.*` source is still present as an unused library. It does
  not contain a DSN and nothing calls it after `RavenClient` was stubbed.

---

## 6. Embedded offline server discovered

The client contains a `Durango.Offline` namespace with 18 source files:

| File | Purpose |
| :--- | :--- |
| `Server.cs` | Creates the local cluster, loads worlds/players, starts gateway + game server |
| `Gateway.cs` | HTTP routes: `/knock`, `/sessions`, `/entry`, `/players`, `/terrains/1`, ... |
| `WebServer.cs` | `HttpListener` wrapper and UDP knock listener |
| `GameServer.cs` | TCP game server on port **8191** |
| `Listener.cs` | TCP accept loop |
| `World.cs`, `WorldContext.cs` | World state |
| `Player.cs`, `PlayerContext.cs` | Player state |
| `MarketManager.cs`, `ArtifactManager.cs`, `Cheats.cs` | Feature managers |

Ports in the source:

- HTTP gateway: **8190**
- Gameplay TCP: **8191**
- UDP knock: **8192** (`port + 1`)

Important caveats:

- This is the **offline/single-player** server path embedded by the original game, not
  a standalone online server and not the community `Durango-CustomServer`.
- In the decompiled tree, `Server` is not instantiated by any remaining call site we
  could find. It may be dead code, wired from a Unity scene, or activated by a code
  path that did not survive decompilation. Do not assume a visible "host server" menu
  exists in this client.
- The **shipped sanitized binary** still contains the original wildcard bindings
  (`IPAddress.Any` and `http://*:<port>/`) because the binary sanitizer did not patch
  them. Only the **source tree** is loopback-only. If the embedded server ever starts
  from the release build, it can listen on every interface.
- Tests belong in the VM. Do not expose ports 8190-8192.

This discovery is also why the project's earlier "no server" statement needs nuance:
there is no standalone server program in the repo, but the client embeds offline
server code. See [SERVER_SETUP.md](SERVER_SETUP.md).

---

## 7. What M7 does and does not give you

### You get

- Readable C# for the whole game assembly and the four supporting assemblies.
- A project layout that compiles on Linux with the .NET SDK and Unity reference DLLs.
- Source-level removal of developer test sockets, hardware fingerprinting, and telemetry.
- A reproducible decompile script and build script.
- The embedded offline server code, which is the same lineage as the community
  `Durango-CustomServer` port.

### You do not get

- The original source code. This is decompiler output, not NEXON's repository.
- Comments, meaningful names, or original formatting.
- Game assets. `assets/` is still the separate, gitignored Unity data.
- A tested replacement DLL.
- A working standalone online server. The embedded code is offline-oriented and does
  not implement the full 3.3.6 online protocol.
- A guarantee that the rebuilt assembly matches the original at runtime.

---

## 8. Next work

1. **Runtime test the rebuilt DLL** in a VM snapshot. Compare `player.log` against the
   sanitized release build.
2. **Audit the remaining external calls** (`WebEventSystem`, CDN URLs, map links) and
   decide whether offline builds should block them.
3. **Try the embedded `Durango.Offline` server** from source: find or recreate the
   missing entry point, bind to loopback, and test character creation.
4. **Compare `Durango.Offline` with `Durango-CustomServer`** to see which fixes can be
   ported into the client.
5. **Only then** consider an Android rebuild. Android is still deferred.

---

*Tool versions and hashes recorded on 2026-11-06. Re-run `scripts/decompile.sh` and
`scripts/build.sh` if the input DLLs or SDK change.*

# Durango Client v3.3.6 – Security Audit, Restructuring & Sanitization

Scope: static analysis only (nothing was executed). Artefacts: all files in `release/` (PC zip, update zip, `DurangoLauncher.exe`, `DurangoReborn.apk`, manifests), plus the 4 root files. All 8 `release/` files match `SHA256SUMS.txt` (that proves self-consistency, **not** authenticity).

> **How to run:** PC-first, inside a disposable VMware VM. See
> [`HOW_TO_RUN.md`](HOW_TO_RUN.md) for the isolation, verification, and monitoring
> procedure. Do not execute the upstream binaries on the host.

## 0. Verdict

**No classic malware found**: no reverse shell, no C2, no credential/cookie/browser-data theft, no packed or encrypted payloads, no secondary-payload downloader in the game assemblies. The "hidden source" is explained: the repo is a *CDN/manifest host* (`servers.json`, `pc-launcher.json`, `android.json`) for a Thai private-server fan project; the client is a modified Nexon Unity (Mono) build with ~200 injected `OffServer*` types in `Assembly-CSharp.dll` and a custom .NET 9 WinForms launcher.

**But it is not safe to run as shipped.** It contains an unauthenticated local developer control socket, invasive hardware fingerprinting, cleartext transport of session tokens, leaked third-party secrets, and a launcher that deletes files and self-replaces from remote JSON without signature checks.

## 1. Threat Assessment

| # | Sev | Component (RVA) | What it does |
|---|-----|-----------------|--------------|
| 1 | **High** | `Assembly-CSharp.dll` → `Durango.Development.TestBridge` (`StartListener` 0x2d4784, `RegisterHandlers` 0x2d4a88). Enabled by `testbridge=<port>` in `server.txt` (`OffServerLink::CreateCluster` 0x2cc4d0) | `TcpListener` on 127.0.0.1, **no authentication**, 40+ commands: `movecode click` (returns 9-digit account-transfer code → full account takeover), `discordlink start`, `cheat <text>`, `screenshot [path]` (writes PNG to any path), `exchange`, `coupon`, `drop`, `use`, `ui`, `key`, `log`. Any local process / DNS-rebinding page can drive it. Off by default, but one config line enables it. |
| 2 | **High** | `OffServerHardwareId` (`get_Value` 0x2cb3d0, `ReadSmbiosUuid` 0x2cb6a4, `Part` 0x2cb52c, `AddTo` 0x2cb3a8) | Reads `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`, SMBIOS UUID via `kernel32!GetSystemFirmwareTable`, `SystemInfo.deviceUniqueIdentifier`; SHA-256 (`"durango-hw-v1:"`) → sent as form field `hw` over **plaintext HTTP**. Hashed, so not raw exfiltration, but it is a persistent hardware ID for a third-party server (hardware banning/tracking). |
| 3 | **High** | Transport: `http://168.222.21.53:8290/8292` (servers.json, server.txt), `http://201.18.211.139:8290` (launcher default, `GameInstall::EnsureConfig` 0xcf54); old git IPs `58.8.81.105`, `157.85.96.159`, `187.53.129.69` | `Authorization: <SessionToken>`, `account_id`, move-codes and `hw` all go over cleartext HTTP to hard-coded IPs of an unknown operator. Anyone on-path can hijack sessions. |
| 4 | Med | `DurangoLauncher.dll` → `GameSync::DeleteExtras` (0xd6d8) | Deletes every file/dir in the game folder not listed in the remote manifest (exempt: `server.txt`, `DurangoLauncher.exe`, `*.log`). Put the launcher in the wrong folder, or serve a hostile manifest → data loss. The vendor README admits it. |
| 5 | Med | `LauncherForm.<UpdateLauncherAsync>` 0x54980, `LauncherSelfUpdate.<TryDownloadAsync>` 0x54ee4, `GameSync::SwapLauncher` 0xd9d4 | Downloads a new `DurangoLauncher.exe` from URL in `pc-launcher.json`, checks only the SHA-256 **from the same JSON**, renames running exe to `.old`, starts the new one (`UseShellExecute=true`). No Authenticode/pinned key → whoever controls the repo/gateway gets code execution on every user. Same for the whole game file set (manifest-driven). The exe is unsigned (README: SmartScreen warning). |
| 6 | Med | `ErrorReporter::HandleLog` → `RavenClient::CaptureUntiyLog/Send`; DSN in `RavenClient::.cctor` 0xa0594 | Unity error logs are gzipped and POSTed to Nexon's legacy Sentry project. Leaked secret DSN `https://<public-key>:<secret-key>@app.getsentry.com/<project>` (values redacted here). |
| 7 | Med | `SendReportSystem::SendServerStatus` 0xa5128 (callers `SendReportPopup.Send`, `SunsetMailPopup.SendReply`) | POSTs player name, entity id, cluster key, last errors to Nexon's Slack webhook `https://hooks.slack.com/services/<redacted>` (leaked secret). |
| 8 | Med | Hard-coded dead Nexon CDNs over HTTP: `http://assetbundles.k.nexon.com/`, `http://durango-assetbundles.akamaized.net/`, `http://maps.durango.nexon.com/` | If a domain lapses and is re-registered, attacker serves AssetBundles to the client (MITM-able even today). Game has an "embed" mode that reads bundles from `StreamingAssets` first. |
| 9 | Low | `servers.json` fetched from `raw.githubusercontent.com/ShuuuuShi/Durango-OffServer-Client/main/servers.json` (0x78212) | Repo owner decides which gateway the client trusts. Override with `servers_url=` in `server.txt`. |
| 10 | Low | `OffServerDiscordLink` (0x2c9f3c) / `DurangoLauncher.DiscordLoopback` (0x4b5c) | Loopback `TcpListener` for the OAuth `/callback`. Legit pattern; short-lived. |
| 11 | Info | `WindowsInput.WinInput` (0xe5f80) `user32!GetAsyncKeyState` | Used only for game hotkeys (`InputKeyboard.Process`); not a keylogger, but polls even when unfocused. |
| 12 | Info | `OffServerLink.get_WritableRoot`, `OffServerLangPack`, `NGUITools.Save`, `AppData.Delete*` | File ops confined to game/`Languages`/cache dirs. No registry writes anywhere. |
| 13 | Info | `DurangoReborn.apk` | `classes.dex`: Unity player + `com.durango.offserver.DurangoActivity` stub only; no `DexClassLoader`/`Runtime.exec`. Permissions: INTERNET, ACCESS_NETWORK_STATE, BLUETOOTH, MODIFY_AUDIO_SETTINGS, RECORD_AUDIO (voice chat). Intent filter `durangooffserver://discord-link`. Declares `usesCleartextTraffic`. |
| 14 | Info | Native: `Dwarf.dll`, `DwarfUnityPlugin.dll` (Nexon/Symantec-signed legacy), Wwise, ffmpeg 3.x/4.x DLLs | No URLs/IPs beyond cert chains; `CreateRemoteThread` string present in `Dwarf.dll`/`AkWaapiClient.dll` (not analysable offline). Treat as unverified third-party binaries. |

Obfuscation check: no Base64 payload loaders. `Convert.FromBase64String` appears only in chat item-link decoding (`OffServerItemShare.Decode`) and one `PlayerManager` lambda; `ldtoken <PrivateImplementationDetails>` = ordinary array initialisers. No `Assembly.Load`, `Process.Start` or `DynamicInvoke` in the game assemblies.

**Limitations**: IL/metadata-level review (no decompiler available at audit time); the IL2CPP APK's native `libil2cpp.so` was not disassembled; no dynamic analysis; no diff against the original Nexon build. Re-run the same checks on any binary you didn't build yourself.

> **Update 2026-11-06 (M7):** the PC assemblies have now been decompiled into
> `src/Assembly-CSharp/` and the four support projects. Source review confirms the
> findings above and adds source-level fixes: the `TestBridge` listener is removed,
> hardware fingerprinting and Raven/Sentry telemetry are deleted, device metadata
> collection is removed, the default upstream `servers_url` is disabled, and the
> embedded `Durango.Offline` server is loopback-bound. See
> [`DECOMPILATION.md`](DECOMPILATION.md). The rebuilt DLL is not runtime-tested, so
> this does not change the "VM only" rule.

## 2. New layout (already created in the repo)

```
config/            server.local.txt (safe dev link) · unity/*.json|config
lib/managed/game/          Assembly-CSharp(.dll|-firstpass) · Durango.Localization.Overrides · Debug · ExternalLibrary   ← the project's real code
lib/managed/third-party/   Concentus, LiteNetLib, MsgPack, NCalc, Vectrosity, Wwise, SharpZipLib, UnityFbxPrefab
lib/managed/engine/        Unity + Mono BCL (generic)           [gitignored]
lib/native/plugins/        Wwise, ffmpeg, Dwarf, snappy ...      [gitignored]
lib/engine/                Durango.exe, UnityPlayer.dll, MonoBleedingEdge, D3D12, dstorage  [gitignored]
assets/streaming|unity/    StreamingAssets + serialized Unity data [gitignored]
quarantine/                DurangoLauncher.exe + unpacked DurangoLauncher.dll  [gitignored, never run]
src/inventory/             type lists of the injected OffServer* code and the launcher
scripts/                   restructure.py · sanitize_client.py · lib/dotnet_meta.py (ECMA-335/CIL reader)
docs/                      manifest_sha256.txt · sanitize_report.json · vendor README/notices
release/  (untouched)      original archives
```

Classification: **source-like** = the 5 managed DLLs in `lib/managed/game` (decompile with ILSpy/dnSpy; no `.cs` exists anywhere). **Compiled binaries** = everything in `lib/engine`, `lib/native`, `quarantine`. **Generic engine assets** = `lib/managed/engine`, `assets/*`. Root `servers.json`, `pc-launcher.json`, `android.json` stay at repo root on purpose: clients fetch them via `raw.githubusercontent.com/.../main/<name>`, so moving them breaks updates.

> [!NOTE]
> Unity needs the stock folder shape to run, so the workspace is the source of truth and `build/Durango-CE/` is generated from it (hard links, no extra disk).

## 3. Migration plan

```bash
cd Durango-CE
python3 scripts/restructure.py extract                       # zip -> workspace (hash manifest in docs/)
python3 scripts/sanitize_client.py lib/managed/game/Assembly-CSharp.dll lib/managed/game/Assembly-CSharp.dll.sanitized
python3 scripts/restructure.py assemble                       # -> build/Durango-CE (uses .sanitized + server.local.txt, omits launcher)
```
Then decompile `lib/managed/game/*.dll` into `src/` with ILSpy (`ilspycmd -p -o src/Assembly-CSharp lib/managed/game/Assembly-CSharp.dll`) and commit sources, not binaries.

## 4. Sanitization guide

`scripts/sanitize_client.py` (verified: finds each method once, confirms void signature, aborts on mismatch, idempotent, never touches the input) applies:

| Patch | Effect |
|---|---|
| `OffServerHardwareId::AddTo` → `ret` | no `hw` field; registry/SMBIOS never read |
| `TestBridge::StartListener` → `ret` | no local control socket even if `testbridge=` is set |
| `RavenClient::CaptureUntiyLog`, `::Send`, `::.cctor` → `ret` | no Sentry traffic |
| Slack URL and Sentry DSN in `#US` heap → same-length loopback dummies | secrets removed; report popup fails harmlessly (callback gets an error) |

Result: `Assembly-CSharp.dll` sha256 `7535931f…717ab2` (input `c0a0a48b…2252ef`), 165 bytes changed.

Manual steps / not covered by the patcher:
1. **Original `DurangoLauncher.exe` quarantined & replaced.** Do not run the upstream `quarantine/DurangoLauncher.exe`. A sanitized, open-source community launcher has been built in `src/DurangoLauncher/` where `DeleteExtras` (file wiping) and `LauncherSelfUpdate` (unsigned remote execution) have been completely eliminated. Users can run either `build/Durango-CE/Durango.exe` directly (with embedded gateway) or the rebuilt `DurangoLauncher.exe`.
2. Point `gateway=` at **your own server over HTTPS**; set `servers_url=` to your fork. Do not ship the `168.222.21.53` / `201.18.211.139` gateways.
3. Keep the server from *requiring* `hw`, or have it accept an empty value.
4. Once you have decompiled sources, replace the IL patches with real edits, e.g.:
   ```csharp
   // OffServerHardwareId.AddTo(WWWForm form) { }              // remove entirely
   // TestBridge: compile out of release builds
   #if DEVELOPMENT_BUILD || UNITY_EDITOR
       TestBridge.StartListener(port);   // and require a random per-run token on every connection
   #endif
   // delete SendReportSystem.SendServerStatus webhook, RavenClient, SharpRaven.DSN
   ```
5. Replace `http://assetbundles.k.nexon.com/`, `http://durango-assetbundles.akamaized.net/`, `http://maps.durango.nexon.com/` with your HTTPS CDN or ship bundles in `StreamingAssets` (embed mode).
6. Rotate/revoke nothing on your side – the leaked Slack/Sentry secrets are Nexon's; just don't ship them.
7. Android: repeat the audit on `libil2cpp.so` + `global-metadata.dat` (Il2CppDumper) and set `usesCleartextTraffic=false` once the gateway is HTTPS.
8. Native third-party binaries (`Dwarf*.dll`, ffmpeg, Wwise): replace with official copies or test removal. `DwarfPluginHelper.cs` has now been stubbed out in managed C#, completely neutralizing native `DwarfUnityPlugin.dll` and `Dwarf.dll` execution.

---

## 5. Threat Intelligence on Upstream Hard-Coded IPs

Static and dynamic analysis revealed several hardcoded public IPs shipped in original legacy commits and configs:

| IP Address | Geolocation / AS / Provider | Ports Observed | Role & Threat Assessment | Status in This Fork |
| :--- | :--- | :--- | :--- | :--- |
| `168.222.21.53` | Bangkok, Thailand (`AS146996 DEXSERVER`) | 8290, 8292 | Upstream third-party gateway. Serves simulated online stats (`"online": 255`) over cleartext HTTP. Intercepts session tokens, UUIDs, and account move codes without TLS encryption. | **Completely Removed** from `servers.json`, configs, and source. |
| `201.18.211.139` | Kuala Lumpur, Malaysia (`AS47583 Hostinger`) | 8290 | Alternate launcher gateway (`srv2019707.hstgr.cloud`). Plaintext HTTP third-party service. | **Completely Removed**. |
| `58.8.81.105` | Nonthaburi, Thailand (`AS17552 True Online`) | Dynamic | Legacy residential/broadband connection previously used by upstream dev. | **Archived / Removed**. |
| `157.85.96.159` | Bangkok, Thailand (`AS56309 Siamdata`) | Dynamic | Legacy upstream host. | **Archived / Removed**. |
| `187.53.129.69` | Kuala Lumpur, Malaysia (`AS47583 Hostinger`) | Dynamic | Legacy Hostinger VPS (`srv1937817.hstgr.cloud`). | **Archived / Removed**. |

All active configurations now default strictly to `127.0.0.1` loopback, and all custom server entries in `servers.json` are loaded dynamically at runtime.

---

## 6. Self-Signed Authenticode Certificate Verification Guide

To eliminate SmartScreen and antivirus heuristic warnings on custom community distributions without purchasing expensive commercial certificates, the community can operate as its own trusted Root Certificate Authority (CA).

### Generating the Community Root CA and Code Signing Certificate (Maintainers)
On a secure Linux build machine using OpenSSL:

```bash
# 1. Generate Community Root CA (valid 10 years)
openssl req -x509 -newkey rsa:4096 -days 3650 -nodes \
  -keyout DurangoCommunityCA.key -out DurangoCommunityCA.crt \
  -subj "/CN=Durango Community Root CA/O=Durango Community Contributors"

# 2. Export public certificate for distribution (.cer)
openssl x509 -outform der -in DurangoCommunityCA.crt -out DurangoCommunityCA.cer

# 3. Generate Code Signing leaf certificate signed by Root CA
openssl req -newkey rsa:2048 -nodes \
  -keyout DurangoCodeSign.key -out DurangoCodeSign.csr \
  -subj "/CN=Durango Community Edition/O=Durango Community Contributors"

openssl x509 -req -in DurangoCodeSign.csr -CA DurangoCommunityCA.crt \
  -CAkey DurangoCommunityCA.key -CAcreateserial -out DurangoCodeSign.crt \
  -days 1095 -extfile <(echo -e "extendedKeyUsage = codeSigning\nbasicConstraints = CA:FALSE")

# 4. Pack into PKCS#12 bundle (.pfx) for signing
openssl pkcs12 -export -out DurangoCodeSign.pfx \
  -inkey DurangoCodeSign.key -in DurangoCodeSign.crt \
  -certfile DurangoCommunityCA.crt -passout pass:durango123
```

### Signing Binaries (CI / Release Packaging)
```bash
# Sign Durango.exe using osslsigncode
osslsigncode sign -pkcs12 DurangoCodeSign.pfx -pass durango123 \
  -n "Durango: Community Edition" \
  -i "https://github.com/Safiqq/Durango-CE" \
  -in build/Durango-CE/Durango.exe \
  -out build/Durango-CE/Durango.exe.signed

mv build/Durango-CE/Durango.exe.signed build/Durango-CE/Durango.exe
```

### End-User Cryptographic Verification (Players)
Before installing the community root certificate, users verify the published SHA-256 fingerprint:

1. **Verify Fingerprint in PowerShell**:
   ```powershell
   Get-FileHash -Algorithm SHA256 .\DurangoCommunityCA.cer
   ```
   Confirm the output matches the SHA-256 fingerprint published in the official GitHub release notes.

2. **Import into Machine Trusted Root Store**:
   ```powershell
   Import-Certificate -FilePath .\DurangoCommunityCA.cer -CertStoreLocation Cert:\LocalMachine\Root
   ```

3. **Verify Windows Signature**:
   Right-click `Durango.exe` → **Properties** → **Digital Signatures**:
   * Status: **"This digital signature is OK"**
   * Signer: `Durango Community Contributors`
   * SmartScreen and Defender treat the binary as signed by a trusted authority. All future updates signed with the same certificate are automatically trusted.

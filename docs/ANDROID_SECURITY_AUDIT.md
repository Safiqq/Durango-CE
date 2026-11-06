# Durango Reborn Android APK (v3.3.6) – IL2CPP Security Audit Report

## 1. Executive Summary

| Attribute | Details |
| :--- | :--- |
| **Target File** | `release/DurangoReborn.apk` |
| **File Size** | 883,831,593 bytes (~842.9 MB) |
| **SHA-256** | `c7c504753744cc703f16b661aee6bf80e3764e91c2b0744c35b61e316f494c37` (Matches `SHA256SUMS.txt`) |
| **Package Name** | `com.durango.offserver` |
| **Version Code / Name** | `336` / `3.3.6` |
| **Engine & Compiler** | Unity 6000.6.3f1 / IL2CPP Ahead-Of-Time (arm64-v8a + armeabi-v7a) |
| **Static Audit Verdict** | **CLEAN of conventional mobile malware / trojans / RATs.** The APK is built from the exact same modified Unity codebase as the PC client, and inherits the same custom features and architectural risks. |

---

## 2. APK Signature & Developer Identity

Analysis of the **APK Signing Block 42** (APK Signature Scheme v2, block ID `0x7109871a`):

* **Signer Certificate Type**: Self-signed X.509 Certificate
* **Subject**: `C=TH, ST=Bangkok, L=Bangkok, O=Durango OffServer, OU=Community Server, CN=Durango OffServer`
* **Issuer**: `C=TH, ST=Bangkok, L=Bangkok, O=Durango OffServer, OU=Community Server, CN=Durango OffServer`
* **Validity Period**: `Sep 21 14:37:15 2026 GMT` to `Sep 13 14:37:15 2056 GMT` (30 years)
* **SHA-256 Fingerprint**: `4B:00:F2:D3:E2:FB:5E:E3:5E:27:5C:84:69:5D:7C:D2:48:37:0C:1A:E6:6C:27:4D:2E:B8:66:B1:85:7B:DF:62`

**Key Takeaway**: The APK is not signed by Nexon, nor by Google Play. It was signed using a custom community keystore created in Bangkok, Thailand, consistent with the fan-server development origin.

---

## 3. Manifest & Permissions Audit (`AndroidManifest.xml`)

* **Target SDK**: `28` (Android 9.0 Pie)
* **Minimum SDK**: `26` (Android 8.0 Oreo)
* **Declared Permissions**:
  * `android.permission.INTERNET` (Normal): Required for game networking.
  * `android.permission.ACCESS_NETWORK_STATE` (Normal): Checking connection state.
  * `android.permission.RECORD_AUDIO` (Dangerous / Runtime): Required for the in-game distance/proximity voice chat (`OffServerVoice`).
  * `android.permission.MODIFY_AUDIO_SETTINGS` (Normal): Audio device management.
  * `android.permission.BLUETOOTH` (Normal): Routing audio to Bluetooth headsets.
* **Intrusive Permissions NOT Requested**:
  * ❌ No `READ_PHONE_STATE` (cannot harvest IMEI / IMSI / carrier info).
  * ❌ No `READ_EXTERNAL_STORAGE` / `WRITE_EXTERNAL_STORAGE` (isolated to internal sandbox).
  * ❌ No `ACCESS_FINE_LOCATION` / `ACCESS_COARSE_LOCATION` (no GPS tracking).
  * ❌ No `CAMERA`, `READ_CONTACTS`, `SEND_SMS`, or `RECEIVE_BOOT_COMPLETED`.
  * ❌ No `SYSTEM_ALERT_WINDOW` (cannot draw overlays over other apps).
* **Network Security Configuration**:
  * ⚠️ `android:usesCleartextTraffic="true"`: Allows plaintext unencrypted HTTP requests. Like the PC client, session tokens and account IDs are transmitted over cleartext HTTP if pointed at standard HTTP gateway servers.
* **Activities & Deep Linking**:
  * Single Activity: `com.durango.offserver.DurangoActivity` (exported=`true`).
  * Intent Filter:
    * Action: `android.intent.action.VIEW`
    * Categories: `DEFAULT`, `BROWSABLE`
    * Scheme: `durangooffserver`
    * Host: `discord-link`
    * *Purpose*: Handles deep-link callbacks when linking Discord accounts in a browser (`durangooffserver://discord-link?...`).

---

## 4. Native Shared Objects Analysis (`lib/arm64-v8a/*.so`)

All native `.so` files were inspected for dynamic symbol imports, undefined external calls, and potential root/tamper exploits:

| Native Library | Size | Audit Result & Purpose |
| :--- | :--- | :--- |
| `libil2cpp.so` | 89.8 MB | Ahead-Of-Time compiled Unity C# assemblies. Uses standard BSD socket APIs (`connect`, `bind`, `listen`, `accept`, `getaddrinfo`). **No** shell execution (`system`, `popen`, `execve`), **no** root escalation or hooking exploits. |
| `libunity.so` | 29.6 MB | Official Unity engine runtime (Android sensors, Vulkan/GLES, lifecycle). |
| `libAkSoundEngine.so` | 2.9 MB | Official Audiokinetic Wwise sound engine runtime. |
| `libsnappy.so` | 202 KB | Google Snappy compression library. |
| `libBlueDoveMediaRender.so`| 132 KB | Video rendering plugin. |
| `libmain.so` | 6.6 KB | Standard minimal Unity native entry point. |

---

## 5. IL2CPP Metadata Analysis (`global-metadata.dat`)

The IL2CPP metadata table (`version 108`, 16.8 MB) was cross-referenced with PC's `Assembly-CSharp.dll`:

1. **Injected Custom Types**:
   * All 131 custom `OffServer*` classes found in the PC client (e.g., `OffServerAuctionUI`, `OffServerDiscordLink`, `OffServerHardwareId`, `OffServerMoveDevice`, `OffServerVoice`) are compiled into `libil2cpp.so`.
   * All 16 `TestBridge` classes are also present in the Android compilation.
2. **Hardware Fingerprinting (`OffServerHardwareId`) on Android**:
   * Windows-specific registry access (`MachineGuid`) and SMBIOS P/Invoke (`GetSystemFirmwareTable`) are **absent** from the Android metadata string pool.
   * On Android, `OffServerHardwareId` relies solely on `UnityEngine.SystemInfo.deviceUniqueIdentifier` (which maps to Android ID / MediaDrm UUID), hashed with `"durango-hw-v1:"`.
3. **Developer Automation Socket (`TestBridge`) on Android**:
   * If a user or script places `testbridge=<port>` in `server.txt` in the app's internal files directory (`/data/data/com.durango.offserver/files/server.txt`), `TestBridge.StartListener` binds a `TcpListener` on `127.0.0.1:<port>`.
   * Any co-located app on the same Android device with standard `INTERNET` permission could communicate with `127.0.0.1` and trigger `movecode click` or other commands.
4. **Third-Party Secrets**:
   * The same legacy Nexon Slack incoming webhook and Sentry DSN URLs are compiled into the Android metadata pool.

---

## 6. Security Recommendations for Android Development

1. **Never Enable `testbridge` on Mobile**:
   Keep `testbridge` out of `server.txt` on mobile devices to prevent local TCP socket listening.
2. **Migrate Gateway to HTTPS**:
   Enforce HTTPS for the game gateway and disable cleartext traffic (`android:usesCleartextTraffic="false"`).
3. **Rebuilding Clean Android Builds**:
   Because `libil2cpp.so` is precompiled machine code, binary patching is fragile compared to Unity Mono. Once you have decompiled the PC source into clean C# (`src/Assembly-CSharp`), the recommended path is opening the project in Unity with Android Build Support and building a pristine, self-signed APK directly from sanitized C# source.

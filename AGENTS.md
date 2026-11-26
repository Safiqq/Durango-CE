# Durango-CE — Project Rules & Baseline Strategy (Freshly Forked v3.3.6 `6f427ad`)

> **Applies to ALL human contributors and ALL AI coding agents** operating in this repository.

---

## 1. Baseline & Architecture Strategy

- **Baseline**: Freshly forked upstream `Durango-OffServer-Client` v3.3.6 (`6f427ad`) + untouched upstream archives in gitignored `release/`.
- **Core Strategy (Lean Client Patch + Incremental Rust Server)**:
  1. **Keep Git ultra-lean**: Never commit decompiled C# trees (625k+ LOC), Unity assets, AssetBundles, `.dll`/`.exe` binaries, `.iso` images, or upstream `release/` archives.
  2. **Sanitize the freshly forked v3.3.6 Unity client via deterministic IL patching** (`scripts/setup-client.py` -> `scripts/sanitize_client.py`) into gitignored `.work/client/`:
     - Neutralize `OffServerHardwareId::AddTo`, `TestBridge::StartListener`, `RavenClient::*`, leaked Sentry DSN, and leaked Slack webhook.
     - **NEVER extract or execute upstream `DurangoLauncher.exe`**.
     - Point `.work/client/offserver.txt` strictly to loopback (`http://127.0.0.1:8190`) by default.
  3. **Build the authoritative server incrementally in cleanroom Rust** behind the **exact v3.3.6 HTTP + MsgPack/Snappy wire protocol** spoken by the real Unity client (PC/Linux via Wine/DXVK, Windows, and Android APK):
     - **Step 1**: Rust HTTP Gateway (`:8190`) — `/status`, `/knock`, `/notice`, `/events/calendar`, `/cs/answer`, `/assets/*`, `/accounts`, `/sessions`, `/admission`, `/entry`, `/terrains/*`.
     - **Step 2**: Rust TCP Game Server (`:8191`) — exact MsgPack/Snappy wire codec + handshake + world join + chunk streaming + movement sync + SQLite persistence.
     - **Step 3+**: Survival gameplay vertical slices (gathering, crafting, building/estates, wildlife/combat/taming, clans/market) — server-authoritative so PC, Linux, and Android all share the same features automatically.
  4. *(Earlier branches are preserved read-only in Git for reference: `backup/pre-reset-staging-2026-11-19` and `archive/rust-cleanroom-prototype-2026-11-20`.)*

---

## 2. Mandatory Rules

1. **Always Test Against the Real Running Game Yourself**:
   - Never ask the user to manually test unverified code.
   - Start the live server on Linux and launch the sanitized `.work/client/DurangoV2.exe` via `wine64` (`./scripts/play-linux.sh`), inspecting real server HTTP/TCP logs and Unity `Player.log` before claiming any milestone works.
2. **Strict Security & Non-Commercial Preservation**:
   - **NEVER execute `DurangoLauncher.exe`** or unmodified upstream binaries.
   - Zero paywalls, zero microtransactions, zero third-party telemetry or hardcoded external VPS IPs.
   - Always check `git status --short` before finishing any task.
3. **Anti-Bloat Guardrails**:
   - Keep tracked docs concise (no doc sprawl).
   - Finish and verify one vertical slice end-to-end before starting the next.

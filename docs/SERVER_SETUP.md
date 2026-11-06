# Server Setup — Do You Need One, and How to Get It

> **Short answer:** yes, you need a server to use the online/multiplayer features.
> This repository does **not** contain a standalone server program. Running
> `Durango.exe` alone gives you the offline client, not a private online server.
>
> The client does embed an **offline** server (`Durango.Offline` in
> `src/Assembly-CSharp/`), with an HTTP gateway on 8190 and a gameplay port on 8191.
> That code was decompiled in M7, but it is offline-oriented and is not the same as a
> full 3.3.6 online server. Do not confuse it with the community server projects.
>
> **Second short answer:** there is no fully verified, 100% safe, 100% complete server
> path today. You have to choose between convenience, completeness, and trust.

This page explains the realistic options and gives step-by-step instructions for each.
All server experiments belong in the **Windows VM**, never on your Ubuntu host.

---

## 1. What the client can do without a server

The client is a modified Unity game. If you remove or rename `server.txt`, the
upstream README says it returns to normal offline single-player mode.

That means:

- You can launch the game and explore whatever offline content exists.
- You cannot use accounts, multiplayer, clans, markets, mail, or server-driven events.
- Some menus may fail or stay empty because no server is answering.

The decompiled source also contains `Durango.Offline` server code. It was not wired to
a visible menu in the source we recovered, and the source tree binds it to loopback.
The shipped sanitized binary still has the original wildcard bindings, so do not
assume it is safe or functional just because the code is present. See
[DECOMPILATION.md](DECOMPILATION.md#6-embedded-offline-server-discovered).

Offline mode is the safest way to confirm the client launches. It is not the full
game experience.

---

## 2. Your options at a glance

| Option | What it means | Difficulty | Features | Safety |
| :--- | :--- | :---: | :---: | :---: |
| **A. Embedded Local Gateway** | Launch `Durango.exe` directly (auto-runs `LocalGatewayServer`) | **Easiest** | Offline Handshake / Single-Player | **Best** |
| **B. Public community server** | Connect to a remote community server in `servers.json` | Easy | Multiplayer / Online World | Low (requires TLS & trust) |
| **C. Build server from source** | Compile the community .NET server yourself | Hard | Dedicated Local Server | Medium |
| **D. Write your own server** | Reverse-engineer and implement the protocol | Very hard | Custom gameplay systems | High |

> [!TIP]
> **Option A is built directly into Durango.exe.** Thanks to `LocalGatewayServer.cs` compiled directly into `Assembly-CSharp.dll`, double-clicking `Durango.exe` spins up an in-process loopback gateway on `127.0.0.1:8190` and `8290` before scenes load, resolving the 24% load screen hang and serving offline handshake requests with zero external scripts.

---

## 3. Option A — Embedded Local Gateway (Recommended for Offline Play)

Use this to jump directly into the game without running external Python or batch servers.

1. Ensure `server.txt` is present with `gateway=http://127.0.0.1:8190/` (default in the build).
2. Launch `Durango.exe`.
3. The embedded server automatically:
   * Binds an asynchronous loopback listener to `127.0.0.1:8190` and `8290`.
   * Serves `/status`, `/knock`, `/Info.5.2.1.json`, `/notice`, `/admission`, `/entry`, `/accounts`, `/sessions`, `/players`.
   * Returns 404 for remote `/assets/*`, safely instructing `Yaml.Util.Loader` to load full embedded assets.
   * Shuts down automatically when `Durango.exe` quits.

For VM sandbox testing:
1. Disconnect the VM network adapter.
2. Run:

   ```powershell
   cd C:\Games\Durango
   .\Durango.exe -force-d3d11 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile player.log
   ```

If it opens a menu or offline world, that is success. If it hangs at login, that is
expected because there is no server. Do not “fix” it by pointing at a random IP.

---

## 4. Option B — Connecting to a Remote Public Community Server (Security Warnings)

All hardcoded third-party public IPs (including the legacy `168.222.21.53` and `201.18.211.139`) have been removed from the repository's active configs and `servers.json`.

### Threat Intelligence & Origin Analysis of Upstream IP (`168.222.21.53`)

* **Geolocation**: Bangkok, Thailand (Coordinates: 13.7540, 100.5014)
* **Autonomous System**: `AS146996 DEXSERVER` (DexServer Co., Ltd., Thailand)
* **Historical Endpoints**: Ports `8290` and `8292` (HTTP gateway, serving simulated gateway JSON `{"ok":true,"online":255,...}`)
* **Transport**: Cleartext HTTP only (TLS/HTTPS port 443 timed out / rejected)
* **Risks Identified**:
  - Plaintext credential and session token transmission over public networks.
  - Potential session hijacking and device tracking.
  - Operators are unaffiliated third parties.

If you connect to any remote community server:

1. Always use **TLS/HTTPS** if supported (`https://your-community-domain.com:8190`).
2. Do not reuse account passwords.
3. Keep the disposable VM snapshot active.

   Do **not** add `testbridge=`.
   Do **not** add an `account=` copied from someone else.
4. Run `Durango.exe` normally.
5. The server should create a throwaway account. Find the **Account Number** in
   **Settings → Account**. Treat that number like a password; never share it.
6. Do not link Discord. Do not enter real credentials.
7. When finished, close the game, power off the VM, and revert to the snapshot.

If it does not connect, the server may be offline, the version may not match, or the
sanitized build may be missing something the server requires. Do not download random
“fix” executables. Stop and use a local server instead.

### Setting up your own equivalent

There are two very different goals:

**1. Local server (recommended for you)**

- Runs on `127.0.0.1` inside the Windows VM.
- No public IP, DNS, TLS certificate, or router port forwarding.
- Gateway: `http://127.0.0.1:8190` (community server) or `http://127.0.0.1:8290`
  (your own stub).
- Game port: `8191` for the community server.
- Windows Firewall: allow loopback only; no inbound public rule.
- This is enough to create a character, test features, and learn how it works.

**2. Public server (what `168.222.21.53` is)**

This is a server other people can reach over the internet. At minimum you need:

1. A machine with a **public IPv4 address** — a VPS is the usual choice.
2. The server runtime (.NET 9) and the server's `data` directory copied to it.
3. The HTTP gateway listening on a public port, for example `8190`.
4. The gameplay TCP port listening and reachable, for example `8191`.
5. Firewall/security-group rules for those ports.
6. A DNS name and an HTTPS reverse proxy (Caddy or Nginx + Let's Encrypt) if you want
   `https://` instead of `http://` for the gateway.
7. The server configured to advertise the correct public hostname and ports.
8. Monitoring, backups, rate limits, and abuse handling.

This is **not turnkey** with the current community server code. Dedicated server
protocol emulation is still incomplete. Home router port-forwarding works technically
but exposes your home IP and network; do not do it for an unaudited game server.

Also remember: the gateway can be wrapped in HTTPS, but the separate TCP gameplay
connection is not automatically encrypted. Do not describe the whole server as “HTTPS”
just because the login page is.

---

## 5. Option C — Build Dedicated Server from Source (`SoloCode228/Durango-CustomServer`)

The community server source repository is:

- <https://github.com/SoloCode228/Durango-CustomServer>

The upstream README describes a .NET 9 server with:

- HTTP gateway on **8190**: `/knock`, `/sessions`, `/entry`, `/players`, `/terrains`
- TCP game port on **8191**: handshake `GetClock → Auth → Ready`
- `server/data` terrain and game data
- A startup batch script (`start_server.bat` / upstream menu script) with options to start, build, and stop the server

### Steps (inside the Windows VM)

1. Install **.NET 9 SDK** from Microsoft's official site.
2. Install Git for Windows, or download the repository ZIP from GitHub.
3. Clone the server:

   ```powershell
   cd C:\Games
   git clone https://github.com/SoloCode228/Durango-CustomServer.git
   cd Durango-CustomServer
   ```

4. Read `README.md` first. Commands and flags can change.
5. Build the server:

   ```powershell
   dotnet build server -c Release
   ```

6. Start it using the project's batch file, or:

   ```powershell
   dotnet run --project server/DurangoServer.csproj -c Release -- --data server/data --terrains server/data/terrains
   ```

7. Point a compatible client's `server.txt` at the gateway:

   ```text
   gateway=http://127.0.0.1:8190
   name=Local Server
   account=<a-random-private-string>
   ```

8. Start the client and create a character. Some server bundles use `server.txt`
   in `server.txt` to connect.

### Limits

- The upstream server README reports support for **42 of 391** message types at the
  time it was written. Many systems return `Abort` or empty results.
- Many complex gameplay systems (friends, parties, clans, market listings, shared music, and story warps) are still incomplete or unverified in open-source server implementations.
- This is a development project, not a complete replacement for the original live game.
- Compatibility with this repo's v3.3.6 client is **not guaranteed**. If the handshake
  fails, check protocol packet definitions rather than mixing versions.

### Safety notes

- Server code is also untrusted third-party code. Run it in the VM, not on Ubuntu.
- Bind it to `127.0.0.1` or the host-only adapter. Do not expose ports 8190/8191 to
  the internet.
- Do not run it as administrator unless the project explicitly requires it for a
  one-time URL ACL.
- Keep the server and client in the same disposable snapshot.

---

## 6. Version compatibility: do not mix clients and servers

| Component | Version / date |
| :--- | :--- |
| This repo's client | v1.0.0-CE, November 2026 |
| Durango-CustomServer source | last pushed September 2026 |

A client and server must agree on the network protocol. A newer client may send
messages an older server does not understand, and an older server may send data the
newer client rejects.

Rules:
- If you use this repo's client, expect to debug protocol mismatches against an unmaintained server.
- Do not copy `Assembly-CSharp.dll` between projects as a “fix”; it can break
  everything and reintroduces unaudited code.
- If you see repeated “no handler for type=…” messages in the server log, the versions
  do not match or the feature is not implemented.

---

## 7. Safety rules for every server option

1. **VM only.** Never run the client or server on the Ubuntu host.
2. **Snapshot first.** Name it clearly, for example `before-server-test`.
3. **No shared folders, clipboard, or USB.** Use a read-only ISO or local transfer.
4. **No real credentials.** Use a throwaway local Windows account and a throwaway game
   account.
5. **No Discord linking.** The account number is already a password; linking adds
   another attack surface.
6. **No internet exposure.** Do not port-forward 8190, 8191, or 8290. Do not put the
   server on a public VPS.
7. **Monitor the first session.** Watch for unexpected child processes, public
   connections, and writes outside the game folder.
8. **Revert after testing.** Power off and restore the snapshot. Do not copy files
   back to the host.
9. **Treat every server operator as hostile.** They can see your account ID, session
   token, IP, and gameplay data. They can ban or impersonate you.
10. **The account number is the password.** Never share it, screenshot it, or post it.

---

## 8. Which option should you choose?

- **“I just want to see if it runs.”** → Option A (embedded local gateway).
- **“I want to play right now and accept the risk.”** → Option B (remote community server), in a throwaway VM.
- **“I want to read and build dedicated server code.”** → Option C (Durango-CustomServer).
- **“I want 100% safety and 100% features.”** → Not available today. That requires
  auditing and rebuilding both the client and server from source.

For your situation — running in a VMware VM — I recommend **Option A first**.

### Recommended end-to-end sequence for you

1. Finish the offline test from `HOW_TO_RUN.md` (Option A).
2. Launch `Durango.exe` directly inside `C:\Games\Durango\`.
3. The embedded gateway starts automatically, serving all initial handshake routes.
4. Play only with a throwaway character. Do not link Discord and do not reuse a password.
5. Exit the game, power off the VM, and revert to the clean snapshot.

---

## 9. Troubleshooting

| Symptom | Likely cause | What to do |
| :--- | :--- | :--- |
| Game hangs at login / “server offline” | No server running | Start the server or use offline mode |
| “Connection refused” | Wrong gateway or port | Check `server.txt`; ensure server is listening on 8190/8290 |
| Server starts, client cannot join | Client/server version mismatch | Check packet definition compatibility |
| Repeated `no handler for type=` | Server does not implement that message | Expected in incomplete server builds |
| Client crashes at start | D3D12/VMware GPU issue | Use `-force-d3d11`; update VMware Tools |
| Account/character missing | Account number changed or lost | Use the same `account=` value; it is the only identity |
| Server visible on LAN unexpectedly | Bound to `0.0.0.0` | Block the ports in Windows Firewall; use loopback/host-only |
| Antivirus flags a launcher | Unsigned third-party binary | Stop; verify hash; do not whitelist blindly |

---

## 10. Checklist

- [ ] I understand this repo provides client code and an embedded offline gateway.
- [ ] I chose an option: embedded gateway, public community server, or source build.
- [ ] I took a VM snapshot before installing anything.
- [ ] I did not use real credentials or link Discord.
- [ ] I did not expose server ports to the internet.
- [ ] I watched the first session for unexpected processes/connections.
- [ ] I reverted the VM snapshot afterward.
- [ ] I did not copy anything back to the Ubuntu host.

---

*Third-party projects and versions were checked on 2026-11-06. They can change without
notice; re-check the hashes and READMEs before running anything.*

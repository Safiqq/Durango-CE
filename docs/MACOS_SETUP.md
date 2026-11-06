# Running Durango: Community Edition on macOS (MacBook Guide)

> **Target Systems**: MacBook Air / MacBook Pro / Mac mini / iMac  
> **Supported Chips**: Apple Silicon (M1, M2, M3, M4) & Intel Macs  
> **Recommended Method**: **Whisky** (Wine + Apple Game Porting Toolkit)

---

## Technical Overview

Durango was released by Nexon as a Windows 64-bit Unity application containing compiled x86_64 native plugins (`Dwarf.dll`, `AkSoundEngine.dll`, `snappy.dll`, `avcodec-57.dll`). Because proprietary C++ plugins like `Dwarf.dll` have no native macOS binaries, the game cannot be compiled directly as a native `.app` bundle.

However, MacBook users can run **Durango: Community Edition** smoothly using two proven methods:
1. **Whisky (GPTK / Wine)** — *Recommended*: Lightweight, free, runs the Windows client directly with high-performance Metal translation.
2. **VMware Fusion Pro** — Full isolated Windows 11 VM (now 100% free for personal use).

---

## Method 1: Running via Whisky (Recommended)

Whisky is a modern, free, open-source macOS app built on top of Wine and Apple's Game Porting Toolkit (GPTK / D3DMetal). It runs 64-bit DirectX 11 Unity games on macOS with near-native performance.

### Step 1: Install Whisky
1. Download Whisky from **[getwhisky.app](https://getwhisky.app/)** (or install via Homebrew):
   ```bash
   brew install --cask whisky
   ```
2. Open Whisky from your Applications folder.

### Step 2: Create a Bottle
1. In Whisky, click the **+ (Create Bottle)** button.
2. Configure:
   * **Bottle Name**: `Durango`
   * **Windows Version**: `Windows 10` or `Windows 11`
3. Click **Create**.

### Step 3: Run Durango
1. Copy the assembled `Durango-CE` folder to your Mac (e.g., inside `~/Games/Durango-CE`).
2. In Whisky, click **Run...** and select `Durango.exe` from your folder.
3. The game will launch with full audio, graphics, and controls.

> [!TIP]
> **Performance Setting**: In Whisky's Bottle Configuration, ensure **DXVK** or **Metal FX / D3DMetal** is toggled ON for optimal frame rates on Apple Silicon.

---

## Method 2: Running via VMware Fusion (Virtual Machine)

If your friends prefer an isolated virtual machine matching your Windows 11 test setup:

1. Download **VMware Fusion Pro** (free for personal use from Broadcom).
2. Install **Windows 11**:
   * On Apple Silicon (M1–M4): Use Windows 11 ARM (includes built-in x64 binary emulation for Unity x64).
   * On Intel Macs: Use standard Windows 11 x64.
3. Mount `Durango.iso` in the VM and run `Durango.exe`.

---

## Multiplayer: Playing Together with Friends

To connect Mac players to a host:

### Option A: Local Network / Wi-Fi
1. Find the host machine's local IP address (e.g., `192.168.1.50`).
2. On the client machine, open `server.txt` inside the Durango game directory:
   ```text
   gateway=http://192.168.1.50:8190/
   ```
   *(Alternatively, select the host from the in-game title screen cluster list).*
3. Both Windows and Mac players can join the same local server session.

### Option B: Virtual LAN (Radmin VPN / Tailscale / ZeroTier)
If you are playing over the internet without port forwarding:
1. Both you and your friends install **Tailscale** or **ZeroTier** (available natively on macOS and Windows).
2. Point the client's `server.txt` to the host's virtual mesh IP.

---

## Running Offline on macOS

Thanks to the embedded `LocalGatewayServer` compiled directly into `Assembly-CSharp.dll`, Mac users running `Durango.exe` via Whisky can play offline immediately without launching any background Python terminal scripts.

For external local gateway testing or hosting on a Mac:
```bash
python3 scripts/local_gateway.py
```
This starts an external local gateway on `http://127.0.0.1:8290/` directly under macOS.

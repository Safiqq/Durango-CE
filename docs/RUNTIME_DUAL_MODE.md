# Runtime Dual-Mode Architecture: Remote Server vs. Fully Local World

> **Document Status**: Draft / Specification for Milestone M8  
> **Target Version**: Durango PC Client v3.3.6 (Hardened C# Source)  
> **Key Capabilities**: Runtime IP input validation (`[0-9.]`), embedded offline world generation, local asset bundle resolution.

---

## 1. Executive Summary & User Requirements

During runtime startup, the player must have a seamless, in-game choice between two distinct operational modes without editing configuration files manually:

1. **Remote / LAN Server Mode**:
   * The user clicks to add/connect to a custom server IP.
   * **Input Constraint**: The input field strictly accepts **only digits (`0-9`) and dot (`.`)**. No alphabetic characters, schemes (`http://`), or special characters may be injected.
   * Gateway connection targets `http://<sanitized_ip>:<port>`.

2. **Fully Local World Mode ("Local Server")**:
   * Selecting "Local Server" requires **no external server** and **no internet connection**.
   * Activates the embedded `Durango.Offline` server subsystem recovered in M7:
     * `Durango.Offline.Server` spins up a loopback listener (`127.0.0.1:8190`).
     * Automatically initializes a `WorldContext` (`*.world`) and `PlayerContext` (`*.player`).
     * Bypasses remote asset downloading by serving the local game assets and offline definitions.

---

## 2. Root Cause Analysis: The `(CheckDataLoaded)` Roadblock

In the live VMware test, the client reached the title screen, connected to the local gateway, retrieved account metadata (`Server: 3.3.6 / NPA: Discord: Dev`), and successfully displayed the character creation slot (`No Character`).

When clicking **"Create Character"**, the client halted with:
```text
Failed to retrieve data.
Tap the screen to retry. (CheckDataLoaded)
```

### Decompiled Code Trace (`Yaml.Util.Loader.cs` & `TitleMenuGroup.cs`)
1. Clicking character creation advances `TitleMenuGroup` to `State.Knock` and then `State.CheckDataLoaded`.
2. In `CheckDataLoaded`, the engine executes `Yaml.Util.Loader.Load(this)`.
3. `Loader.cs` initializes an array of **71 game definition routines** (including battle actions, recipes, blueprints, crops, pet skills, natural entity prototypes):
   ```csharp
   IEnumerator[] routines = new IEnumerator[71]
   {
       LoadYaml<PlayerActions>("/assets/player/player_battle_actions"),
       LoadYaml<TagAllowActions>("/assets/tag_allow_actions"),
       LoadYaml<TagYaml>("/assets/tags"),
       LoadYaml<RecipeDict>("/assets/item/recipes"),
       ...
   };
   ```
4. For each routine in `Mode.Online`, it issues an HTTP GET to:
   ```text
   http://127.0.0.1:8290/assets/<path>
   ```
5. Because the minimal initial gateway returned `404 Not Found`, all 71 routines failed. After 5 retries, `Loader.LoadState` became `State.Failure`, setting `CurState = State.Error`.

---

## 3. Project Asset Folders & Storage Mapping

The project repository already contains the complete asset trees required for local play:

| Asset Directory | Destination in VM / Client | Contents & Purpose |
| :--- | :--- | :--- |
| `assets/streaming/AssetBundles/` | `C:\Games\Durango\Durango_Data\StreamingAssets\AssetBundles\` | **2,177 compiled asset bundles** (~980 MB): 3D dinosaur models, character equipment, animations, audio, and `Info.5.2.1.json`. |
| `assets/streaming/Audio/` | `C:\Games\Durango\Durango_Data\StreamingAssets\Audio\` | Wwise soundbanks (`init.bnk`, `soundbank.bnk`). |
| `assets/streaming/Movie/` | `C:\Games\Durango\Durango_Data\StreamingAssets\Movie\` | Intro and title video cinematics (`title.mp4`). |
| `assets/unity/data/` | `C:\Games\Durango\Durango_Data\` | Unity serialized data: `resources.assets`, `level0`, `level1`, `level2`, `level3`. Contains embedded offline tables (`offline/clusters`, `offline/assets/...`). |
| Embedded World Data | `%APPDATA%\..\LocalLow\NEXON\Durango\` or `AppData/` | Persistent local save files: `slot_0.world` and `slot_0.player`. |

---

## 4. Architectural Implementation Plan

### A. Input Sanitization Specification (Server IP)
When entering a server address, user input is filtered at the UI layer to allow only standard IPv4 characters:

```csharp
public static bool IsValidServerIp(string input)
{
    if (string.IsNullOrEmpty(input)) return false;
    
    // Strict whitelist: only digits 0-9 and period '.'
    foreach (char c in input)
    {
        if ((c < '0' || c > '9') && c != '.')
        {
            return false;
        }
    }
    
    // Validate IPv4 quartet format
    string[] parts = input.Split('.');
    if (parts.Length != 4) return false;
    
    foreach (string part in parts)
    {
        if (!int.TryParse(part, out int val) || val < 0 || val > 255)
        {
            return false;
        }
    }
    return true;
}
```

### B. Dual-Mode Flowchart

```mermaid
flowchart TD
    Start([Launch Durango.exe]) --> TitleScreen[Title Screen Loaded]
    TitleScreen --> SelectionDialog{User Choice}
    
    SelectionDialog -->|Option 1: Add Server IP| IPPrompt[Enter Server IP]
    IPPrompt --> ValidateIP{Strict Check: [0-9.]}
    ValidateIP -->|Invalid| IPError[Show Validation Error] --> IPPrompt
    ValidateIP -->|Valid IPv4| ConnectRemote[Set Gateway = http://IP:8190\nMode = Mode.Online]
    ConnectRemote --> RemoteHandshake[Connect to Remote / LAN Server]
    
    SelectionDialog -->|Option 2: Play Locally| LocalDev[Select Local Server]
    LocalDev --> StartEmbedded[Start Durango.Offline.Server\nBind loopback 127.0.0.1:8190\nLoad WorldContext & PlayerContext]
    StartEmbedded --> SetOfflineMode[Set Cluster.Mode = Mode.Offline\nUse Local Asset Bundles]
    SetOfflineMode --> Prologue[Direct Entrance into Train Prologue / Character Creation]
```

### C. Milestone Work Items
* **M8 (Embedded In-Process Gateway - COMPLETED)**: Implemented `LocalGatewayServer.cs` in `Assembly-CSharp.dll` with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]`. Automatically binds loopback ports `8190` and `8290`, serves handshake routes, returns 404 for remote `/assets/*` to engage embedded assets, and safely shuts down on `Application.quitting`.
* **M8.1 (Cleanroom Launcher Integration - COMPLETED)**: Decompiled and sanitized `src/DurangoLauncher/` to provide multi-server selection via `servers.json`, client integrity verification, and Discord loopback auth.
* **M8.2 (Standalone Game Verification)**: Verify prologue train entrance and local world persistence in the VMware isolated sandbox.

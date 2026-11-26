# Durango-CE Changelog

All notable changes to the cleanroom Rust server (`durango-server`, `durango-wire`, `durango-gateway`), deterministic client sanitizer/mod (`scripts/sanitize_client.py`, `client-mod/DurangoCE.ClientMod`), and community deployment tooling.

---

## [Unreleased] — 2026-11-23

### Gameplay & World Fixes (Live Playtest Polish)
- **Visible 3D Player-Built Structures**: Loaded `building/blueprints.json` (`default_look` + slot `model_key`s) and `building/artifact_models.json` into `ArtifactSpec.default_parts`; hydrated `ArtifactDisplay.Parts` in `AppearArtifact` (`93`) and broadcast `TC_ARTIFACT_DISPLAY` (`2433`) + `TC_ARTIFACT_STATE` (`323`) on `TC_COMPLETE_ARTIFACT` so Moss Huts and all player-built structures render immediately in 3D.
- **Equippable Crafted Weapons, Tools & Gear**: Loaded `assets/performance.json` (with level-scaling formula evaluation) into `GameCatalog` and populated `Item.Performance` (`PerformanceMsg`) on every item, enabling equipping and using crafted weapons/tools like `Stone Blade`, armor, bags, and consumables.
- **Authentic Island-Tier Wildlife Ecology**: Parsed real `size_level`, `difficulty`, and `represent_combat_level` from `animal.json`, excluded seasonal/holiday/pet/event variants (`watermelon`, `dodophysis`, `*_for_pet`, `*_xmas`, etc.) from wild spawns, and restricted Tamed Island (`pe10gr_*`) to gentle starter grassland fauna only (no `Brachiosaurus` or `Dodophysis` on starter islands; giant/apex species gated to high-level unstable islands).
- **Real Pioneer Grade Progression**: Fixed `PioneerGradeInfoMsg` so new characters start at Pioneer Grade `1` instead of hardcoded `9`, scaling naturally with character level.
- **Authentic Resting State & Fatigue Recovery Visuals**: Added wire message `TC_FATIGUE_VELOCITIES` (`318`) so resting (`TC_REST_ON`) drives Durango's native animated Fatigue bar arrows (`-2.0/s`) while `DurangoCE.ClientMod` switches the HUD character state to **`Resting · Fatigue -2.0/s`** and displays a pulsing **`▼ -2.0/s Resting`** indicator beside the Fatigue bar until resting stops or fatigue reaches `0`.
- **Native `ESC` → Menu Window → Highlighted Bottom `Exit` → Native Quit Modal**: Fixed `EnsureMovementKeyLayers` to access `GameSystem<InputSystem>.Instance()` directly (making `ESC` open the Menu window cleanly instead of popping the quit dialog), fixed `UITexture` shader resolution in Unity 6 (`FindNguiShader`) for the highlighted crimson/gold **`Exit`** button at the bottom of the Menu window, and routed `Exit` straight into Durango's native `UIManager.MessageBox.Show` (`"Do you really want to quit?"` with **Return to Main Menu**, **Exit Game**, and **Cancel**).
- **Bounded Gzip-Rotated Activity Logs, Prometheus `/metrics` & Self-Hosted Grafana OSS Stack**:
  - Added `GATHER`, `CRAFT`, `DOMAIN`, and `WORLD` warp audit events, automatic `5 MiB` NDJSON log rotation + `flate2` gzip compression (max `14` `.jsonl.gz` archives), `GET /admin/logs/export`, CORS support on `/admin/*`, and enriched `GET /metrics` (Prometheus exposition format with server gauges, per-category event counts, and per-online-player HP/Fatigue/Energy/Level/T-Stone metrics).
  - Added IP-agnostic [scripts/deploy-grafana-vps.sh](scripts/deploy-grafana-vps.sh) deploying real **Grafana OSS (`:3000`) + Prometheus (`127.0.0.1:9090`)** via Docker Compose with tight memory limits (`180m` + `108m`), 2s Prometheus scraping, and an auto-provisioned Home Dashboard combining live Prometheus Stat/Time-Series/Bar-Gauge panels with an interactive Moderation, Server Config, Players, Bans & Audit Log console.
- **Item & Domain-Protected Building Durability**: Added SQLite-persisted durability (`durability`, `max_durability`, `last_decay_at`) and visual `Condition` states (`Normal`/`Old`/`Worn`/`Broken`) for buildings, real repair restoration (`TC_REPAIR_ARTIFACT`/`TC_REPAIR_IMMEDIATE`), and periodic wilderness decay that leaves all buildings inside an active player/clan Domain at **100% undecreased durability**.
- **Strict Inland Dry-Land Wildlife Spawning & Wandering**: Fixed `LoadedTerrain::is_dry_land` to decode Durango's signed `i8` `whole.ocean` height/depth corners (`(b as i8) <= 0` = dry land, `1..=127` = coastal/deep ocean), added `is_safe_inland_land` (4-neighbor dry buffer), anchored Harbor-nearby spawns strictly on the nearest inland meadow (eliminating out-of-bounds ocean spawns), and validated wander midpoints/respawns.
- **Canonical SWE Identifiers Stored Directly in Data (`usr_*` & `tamed-grassland-lv10-01`)**:
  - Canonicalized account keys at HTTP ingress (`POST /accounts`, `POST /sessions`) into Stripe/Clerk-style prefixed IDs (`usr_<12-hex>`, e.g., `usr_ef72cdc18325`) stored directly in SQLite `characters.owner_key`, `bans`, sessions, and audit logs.
  - Stored semantic kebab-case zone codes (`tamed-grassland-lv10-01`, `tamed-tropical-lv10-01`) directly as `region_id` in SQLite (`characters`, `artifacts`, `estates`, `market_*`, `player_points`, `audit_logs`) and wire `RegionMsg.id`, resolving back to static terrain `.zip` assets via `terrain_id_for_region`. Legacy non-canonical rows are purged automatically on startup (`purge_legacy_identifiers`).
  - Replaced hardcoded placeholders (`pioneer_exp: 0`, `TC_OCCUPY_ARTIFACT_SITE` `level: 1`, `player_slot_count: 10`, `"LocalPlayer"`, and inline magic numbers for container slots, starting T-Stone, estate leases, and mail expiry) with live values and shared constants, and added `POST /season/pass` support.
- **Grafana-Style Operations Console & Real-Time Telemetry (`/admin`)**:
  - Rebuilt `/admin` with authentic Grafana dark UI/UX (`#111217` canvas, `#181b1f` panels, `2px` crisp geometry, left icon navigation rail with `#ff9830` accent, 12-column panel grid, stat panels with inline SVG sparklines, live Time-Series SVG graph, dense tables, and Loki-style live-tail log explorer).
  - Added single-request `GET /admin/snapshot` + 1-second server telemetry ring buffer (`metrics_history`) powering Grafana-style **`Auto`** refresh (`1s` on 5m window, `2s` on 15m, `5s` on 1h) plus explicit **`1s` / `2s` / `5s` / `10s` / `30s` / `Off`**.
  - Added SQLite `audit_logs` table + `GET /admin/logs` capturing player joins/leaves, building, combat, market trades, chat, moderation actions, blocked auth attempts, and server lifecycle events.
- **PC Window, Camera & Native Menu Exit UX (`DurangoCE.ClientMod` + Sanitizer)**:
  - Neutralized `OffServerMoveClose::CloseWindows` and removed `InventoryUI | FullscreenUI` from WASD/Arrow movement bindings so misclicking movement keys never closes open Bag/Character/Craft/Domain windows or walks the player away.
  - Applied a comfortable zoomed-out default camera view (`0.62`) once on login while keeping scroll-wheel zoom fully player-controlled.
  - Rebound `ESC` on the main HUD to open/close the native Menu Drawer (`MenuListGroupBase`), added a native NGUI **Exit** button at the bottom of the Menu Drawer, and built a native NGUI Leave Game modal (`Where would you like to go?`) offering **Return to Main Menu**, **Exit to Desktop**, and **Cancel**.

### Community Hosting, Directory & Moderation (`14eadd3`)
- **Single-Binary Community Server + Source-of-Truth Directory**:
  - Native Unity v3.3.6 `GET /servers.json` automatically serves the host server plus any federated community servers reporting via `POST /api/directory/heartbeat` (opt-in `DURANGO_DIRECTORY_URL`, `30s` heartbeat, `90s` TTL auto-pruning).
  - Added public `GET /directory` HTML server browser and `GET /api/directory/servers` JSON feed.
- **Per-Server Join Password**: Optional `DURANGO_JOIN_PASSWORD` (or live toggle in `/admin`) enforced on HTTP (`/accounts`, `/sessions`, `/admission`, `/entry`) and TCP (`TC_AUTH`), supporting both `X-Durango-Join-Password` header and `/p/<password>/...` URL prefix (stock Android compatible).
- **3-Layer Admin Blacklist & Web Admin Panel (`/admin`)**:
  - SQLite `bans` table supporting Account (`owner_key`), Character (`name`/`entity_id` auto-resolving to account), IP/CIDR subnet (`203.0.113.0/24`) with optional TTL expiry, Chat Mutes, and instant live TCP kick.
  - Protected `/admin` web dashboard & REST API (`DURANGO_ADMIN_TOKEN`) plus in-game GM slash commands (`/who`, `/kick`, `/ban`, `/banip`, `/mute`, `/unban`).
- **One-Command VPS Deployment**: Added IP-agnostic [scripts/deploy-vps.sh](scripts/deploy-vps.sh) (Docker `rust:1-bookworm` glibc-2.36 build, `rsync` assets/terrains/locales, isolated `durango-ce.service` systemd unit with `MemoryMax=512M` and live endpoint verification) and parameterized `--gateway` / `--servers-url` flags in [scripts/setup-client.py](scripts/setup-client.py).

### Phase 2B — Economy, Storage, Warehouses & Mail (`98e8313`)
- Persisted SQLite `t_stone` wallet (`50,000` default) hydrated in `InventoryMsg` and synced via `TC_WALLET_UPDATED` (`5198234`).
- Full Island Market flow: atomic T-Stone deduction on purchase, seller payment collection (`TC_MARKET_COLLECT_PAYMENT`, `TC_MARKET_COLLECT_ALL_PAYMENTS`), tabs (`Registered`, `Expired`, `Sold`, `Purchased`, `Favorites`), and multi-listing registration.
- Persisted Storage Containers (`TC_PUT_IN_ITEM`, `TC_TAKE_OUT_ITEM`, `TC_GET_INVENTORY`) and multi-section Warehouses (create/remove/rename/reorder sections and items).
- Persisted In-Game Mail (`TC_SEND_MAIL`, `TC_ACCEPT_MAILS`, `TC_ACCEPT_USER_MAILS`, `TC_DELETE_*`, `TC_MARK_*`, login inbox push).

### Phase 2A — Wildlife AI, Combat Feel, Survival & Tool Wear (`ad080c8`)
- Real dead dinosaur collapse animation (`TC_MOVE(dead_motion)` + `TC_COMBAT_INTERACTION(Dead=8)` + `TC_ENTITY_DIED`).
- `herds.yml` island-wide randomized wildlife spawning & dry-land respawning.
- 500ms responsive wildlife AI tick with interpolated movement (`MOTION_OPT_LOOP_ALIGN`), carnivore proximity aggro (vision cone + scent), Threat Telegraph roar, varied `attacks[]` clips, knockback/recovery, and 10s leash forget.
- Tool durability wear persisted to SQLite, `TC_REPAIR_ITEM`, `TC_DUMP_ITEMS`, `TC_CHANGE_EQUIP_SLOT_TYPE`, `TC_OPEN_GATE`/`TC_CLOSE_GATE`, `TC_DRINK_WATER`, `TC_WASH_BODY`, `TC_GET_STATUS_EFFECTS`, and Well-Rested (`energetic`) +25% XP buff.

### Phase 1 — Core Gameplay & Wire Parity (`e2c7494`)
- Combat Action Bar (`tag_allow_actions.json` + `"main"`/`"both"` weapon slots + `use_range`).
- Full Craft/Build Catalog (`skill/rewards.json`, `artifact.json`, 71 free `0-SP` starter skills, `TC_GET_RECIPES`, `TC_GET_ARTIFACT_BLUEPRINTS`, `TC_ESTIMATE_BUILD`, `TC_GET_WORKBENCH`, `TC_CANCEL_CRAFTING`).
- Personal Region & Domain/Estates (`TC_GET_PERSONAL_REGION_INFO`, `Welcome.personal_region_id`, SQLite `estates`/`estate_cells`, all 12 estate handlers).
- Harbor POIs (`pois.yml`), `TC_WARP_TO_PORT`, `TC_RETURN_TO_HOME`, `TC_SET_AS_HOME`, `TC_SET_RETURNING_POINT`, `TC_REMOVE_DEATH_POINT`, `Keepalive`, and exact `Timer` → `Teleported` wire sequence.
- Official 80-level cumulative XP curve (`statistics/player.json`) fixing negative level progress percentages, plus HTTP `GET /season/pass`.

### Foundation — Cleanroom Rust Server & Sanitized Client
- Deterministic IL sanitizer (`scripts/sanitize_client.py`) removing `OffServerHardwareId`, `TestBridge`, `RavenClient`/Sentry, leaked Slack/Sentry secrets, third-party launcher/reborn branding, dead CloudFront prologue videos, and Thai UI strings.
- Cleanroom `DurangoCE.ClientMod.dll` HUD polish (bottom-center state indicator, main-screen button captions, walk vs Hold-Shift sprint polish, reliable WASD gather-cancel).
- Cleanroom Rust HTTP Gateway (`:8190`) + MsgPack/Snappy TCP Game Server (`:8191`) + SQLite persistence + English `.mo` localization (`33,107` entries).

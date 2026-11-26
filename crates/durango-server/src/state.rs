use std::collections::{BTreeMap, HashMap, HashSet, VecDeque};
use std::path::PathBuf;
use std::sync::Arc;
use std::time::{SystemTime, UNIX_EPOCH};

use durango_wire::{CharacterAppearance, GaugeMsg, SurvivalMsg};
use serde_json::Value;
use tokio::sync::{mpsc, RwLock};
use uuid::Uuid;

use crate::catalog::{GameCatalog, NaturalSpec};
use crate::db::{ArtifactRecord, CharacterRecord, Database};
use crate::terrain::{LoadedTerrain, TerrainStore, DEFAULT_TERRAIN_ID};

#[derive(Debug, Clone, serde::Serialize)]
pub struct MetricSample {
    pub ts: f64,
    pub online: usize,
    pub sessions: usize,
    pub resting: usize,
    pub bans: usize,
    pub events_1m: usize,
}

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct DirectoryServerEntry {
    pub server_id: String,
    pub name: String,
    pub gateway: String,
    pub region: String,
    pub online_players: usize,
    pub max_players: usize,
    pub password_required: bool,
    pub pvp_enabled: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub verified: Option<bool>,
    pub version: String,
    pub last_seen_at: f64,
}

#[derive(Debug, Clone)]
pub struct ServerConfig {
    pub http_bind: String,
    pub tcp_bind: String,
    pub public_host: String,
    pub server_name: String,
    pub server_mode: String,
    pub assets_dir: Option<PathBuf>,
    pub terrains_dir: Option<PathBuf>,
    pub db_path: Option<PathBuf>,
    pub collect_delay_ms: u64,
    pub craft_delay_ms: u64,
    pub build_delay_ms: u64,
    pub wildlife_wander_interval_ms: Option<u64>,
    pub wildlife_attack_cooldown_secs: f64,
    pub wildlife_respawn_secs: f64,
    pub directory_url: Option<String>,
    pub directory_secret: Option<String>,
    pub upstream_servers_urls: Vec<String>,
    pub attestation_key: Option<String>,
    pub ca_allowlist_path: Option<PathBuf>,
    pub ca_require_attestation: bool,
    pub join_password: Option<String>,
    pub admin_token: Option<String>,
    pub admin_keys: Vec<String>,
    pub max_players: usize,
    pub region_tag: String,
    pub notice_text: String,
}

impl Default for ServerConfig {
    fn default() -> Self {
        Self {
            http_bind: "127.0.0.1:8190".to_string(),
            tcp_bind: "127.0.0.1:8191".to_string(),
            public_host: "127.0.0.1".to_string(),
            server_name: "Durango-CE Local Server".to_string(),
            server_mode: "Survival".to_string(),
            assets_dir: None,
            terrains_dir: None,
            db_path: None,
            collect_delay_ms: 1500,
            craft_delay_ms: 500,
            build_delay_ms: 400,
            wildlife_wander_interval_ms: Some(500),
            wildlife_attack_cooldown_secs: 2.2,
            wildlife_respawn_secs: 75.0,
            directory_url: None,
            directory_secret: None,
            upstream_servers_urls: Vec::new(),
            attestation_key: None,
            ca_allowlist_path: None,
            ca_require_attestation: false,
            join_password: None,
            admin_token: None,
            admin_keys: Vec::new(),
            max_players: 100,
            region_tag: "Global".to_string(),
            notice_text: String::new(),
        }
    }
}

#[derive(Debug, Clone)]
pub struct SessionEntry {
    pub owner_key: String,
    pub entity_id: String,
    pub password_ok: bool,
    pub created_at: f64,
}

pub const SESSION_TTL_SECS: f64 = 86_400.0;
pub const TEMP_CHAR_TTL_SECS: f64 = 7_200.0;

pub const STAMINA_REGEN_PER_SEC: f32 = 5.0;
pub const DASH_STAMINA_COST: f32 = 20.0;
pub const GATHER_ENERGY_COST: f32 = 5.0;
pub const GATHER_FATIGUE_GAIN: f32 = 3.0;
pub const REST_FATIGUE_PER_SEC: f32 = 2.0;
pub const REST_ENERGY_PER_SEC: f32 = 4.0;
pub const REST_LIFE_PER_SEC: f32 = 3.0;
pub const DEFAULT_STARTING_T_STONE: i64 = 50_000;
pub const DEFAULT_PLAYER_SLOTS: usize = 5;
pub const ESTATE_LEASE_DAYS: f64 = 7.0;
pub const MAIL_EXPIRY_DAYS: f64 = 30.0;
/// Canonical SWE kebab-case default region/zone code stored directly in SQLite & wire RegionMsg.id.
pub const DEFAULT_REGION_CODE: &str = "tamed-grassland-lv10-01";

/// Pure-Rust FIPS 180-4 SHA-256 digest helper (no extra Cargo dependencies).
pub fn sha256(data: &[u8]) -> [u8; 32] {
    const K: [u32; 64] = [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4,
        0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe,
        0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f,
        0x4a7484aa, 0x5cb0a9dc, 0x76f988da, 0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7,
        0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc,
        0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b,
        0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070, 0x19a4c116,
        0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7,
        0xc67178f2,
    ];
    let mut h: [u32; 8] = [
        0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab,
        0x5be0cd19,
    ];
    let bit_len = (data.len() as u64).wrapping_mul(8);
    let mut padded = Vec::with_capacity(data.len() + 72);
    padded.extend_from_slice(data);
    padded.push(0x80);
    while (padded.len() % 64) != 56 {
        padded.push(0);
    }
    padded.extend_from_slice(&bit_len.to_be_bytes());
    for chunk in padded.chunks_exact(64) {
        let mut w = [0u32; 64];
        for (i, word) in w.iter_mut().take(16).enumerate() {
            let j = i * 4;
            *word = u32::from_be_bytes([chunk[j], chunk[j + 1], chunk[j + 2], chunk[j + 3]]);
        }
        for i in 16..64 {
            let s0 = w[i - 15].rotate_right(7) ^ w[i - 15].rotate_right(18) ^ (w[i - 15] >> 3);
            let s1 = w[i - 2].rotate_right(17) ^ w[i - 2].rotate_right(19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16]
                .wrapping_add(s0)
                .wrapping_add(w[i - 7])
                .wrapping_add(s1);
        }
        let [mut a, mut b, mut c, mut d, mut e, mut f, mut g, mut hh] = h;
        for i in 0..64 {
            let s1 = e.rotate_right(6) ^ e.rotate_right(11) ^ e.rotate_right(25);
            let ch = (e & f) ^ ((!e) & g);
            let t1 = hh
                .wrapping_add(s1)
                .wrapping_add(ch)
                .wrapping_add(K[i])
                .wrapping_add(w[i]);
            let s0 = a.rotate_right(2) ^ a.rotate_right(13) ^ a.rotate_right(22);
            let maj = (a & b) ^ (a & c) ^ (b & c);
            let t2 = s0.wrapping_add(maj);
            hh = g;
            g = f;
            f = e;
            e = d.wrapping_add(t1);
            d = c;
            c = b;
            b = a;
            a = t1.wrapping_add(t2);
        }
        h[0] = h[0].wrapping_add(a);
        h[1] = h[1].wrapping_add(b);
        h[2] = h[2].wrapping_add(c);
        h[3] = h[3].wrapping_add(d);
        h[4] = h[4].wrapping_add(e);
        h[5] = h[5].wrapping_add(f);
        h[6] = h[6].wrapping_add(g);
        h[7] = h[7].wrapping_add(hh);
    }
    let mut out = [0u8; 32];
    for (i, word) in h.iter().enumerate() {
        out[i * 4..i * 4 + 4].copy_from_slice(&word.to_be_bytes());
    }
    out
}

fn sha256_usr_24(secret: &str) -> String {
    let digest = sha256(secret.as_bytes());
    let mut s = String::with_capacity(28);
    s.push_str("usr_");
    for byte in &digest[..12] {
        use std::fmt::Write as _;
        let _ = write!(&mut s, "{byte:02x}");
    }
    s
}

/// Derives the authoritative `usr_<24-hex>` Account ID strictly by hashing the full client secret.
/// Never accepts an existing `usr_...` string verbatim so public IDs cannot be replayed as secrets.
pub fn canonical_account_id_from_secret(raw_secret: &str) -> Option<String> {
    let trimmed = raw_secret.trim();
    if trimmed.len() < 4 || trimmed.len() > 256 {
        return None;
    }
    if trimmed.chars().any(char::is_control) {
        return None;
    }
    Some(sha256_usr_24(&trimmed.to_ascii_lowercase()))
}

/// Canonicalizes an account identifier or secret (`usr_<24-hex>` normalized lowercase,
/// or a raw secret hashed with full SHA-256 entropy to `usr_<24-hex>`).
pub fn canonical_account_id(raw: &str) -> Option<String> {
    let trimmed = raw.trim().to_ascii_lowercase();
    if trimmed.len() < 4 || trimmed.len() > 256 {
        return None;
    }
    if let Some(rest) = trimmed.strip_prefix("usr_") {
        if rest.len() == 24 && rest.bytes().all(|b| b.is_ascii_hexdigit()) {
            return Some(trimmed);
        }
    }
    canonical_account_id_from_secret(&trimmed)
}

/// Returns the canonical Account ID (`usr_<24-hex>`) from an account or display string.
pub fn format_account_tag(owner_key: &str) -> String {
    canonical_account_id(owner_key).unwrap_or_else(|| "usr_guest".to_string())
}

/// Returns a shortened public-safe account tag (`usr_<12-hex>`) without exposing full owner keys.
pub fn public_account_tag(owner_key: &str) -> String {
    match canonical_account_id(owner_key) {
        Some(tag) if tag.len() >= 16 => tag[..16].to_string(),
        Some(tag) => tag,
        None => "usr_guest".to_string(),
    }
}

/// Authoritative metadata for each of the 18 official v3.3.6 island terrains (`data/terrains/*.zip`)
/// and `assets/region_templates.json`, mapping losslessly 1-to-1 between:
/// - `terrain_id`: `.zip` terrain archive name (`pe10gr_1`, `ri15sv01`, `ua60vol`, ...)
/// - `template_id`: `region_templates.json` template ID (`pe10gr_1`, `gen002020`, `ri35deSub01`, ...)
/// - `zone_code`: canonical SWE kebab-case zone code (`tamed-grassland-lv10-01`, `unstable-volcanic-lv60-01`, ...)
/// - `role`: `durango_wire::RegionRole` (`Personal = 9`, `Risky = 4`)
/// - `level`: island level (`10..=60`)
/// - `biome`: `Shared.Region.Biome` enum (`0=TemperateForest, 1=TropicalForest, 2=Desert, 3=Tundra, 4=SnowField, 5=Grassland, 6=SwampMud, 7=Volcanic`)
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct RegionTemplateMeta {
    pub terrain_id: &'static str,
    pub template_id: &'static str,
    pub zone_code: &'static str,
    pub role: durango_wire::RegionRole,
    pub level: i32,
    pub biome: i32,
    pub biome_name: &'static str,
}

pub const KNOWN_REGION_TEMPLATES: &[RegionTemplateMeta] = &[
    RegionTemplateMeta {
        terrain_id: "pe10gr_1",
        template_id: "pe10gr_1",
        zone_code: "tamed-grassland-lv10-01",
        role: durango_wire::RegionRole::Personal,
        level: 10,
        biome: 5,
        biome_name: "Grassland",
    },
    RegionTemplateMeta {
        terrain_id: "pe10gr_2",
        template_id: "pe10gr_2",
        zone_code: "tamed-grassland-lv10-02",
        role: durango_wire::RegionRole::Personal,
        level: 10,
        biome: 5,
        biome_name: "Grassland",
    },
    RegionTemplateMeta {
        terrain_id: "pe10gr_3",
        template_id: "pe10gr_3",
        zone_code: "tamed-grassland-lv10-03",
        role: durango_wire::RegionRole::Personal,
        level: 10,
        biome: 5,
        biome_name: "Grassland",
    },
    RegionTemplateMeta {
        terrain_id: "pe10gr_4",
        template_id: "pe10gr_4",
        zone_code: "tamed-grassland-lv10-04",
        role: durango_wire::RegionRole::Personal,
        level: 10,
        biome: 5,
        biome_name: "Grassland",
    },
    RegionTemplateMeta {
        terrain_id: "pe10gr_5",
        template_id: "pe10gr_5",
        zone_code: "tamed-grassland-lv10-05",
        role: durango_wire::RegionRole::Personal,
        level: 10,
        biome: 5,
        biome_name: "Grassland",
    },
    RegionTemplateMeta {
        terrain_id: "ri15sv01",
        template_id: "ri15sv01",
        zone_code: "unstable-savanna-lv15-01",
        role: durango_wire::RegionRole::Risky,
        level: 15,
        biome: 5,
        biome_name: "Savanna",
    },
    RegionTemplateMeta {
        terrain_id: "ri18tp01",
        template_id: "ri18tp01",
        zone_code: "unstable-tropical-lv18-01",
        role: durango_wire::RegionRole::Risky,
        level: 18,
        biome: 1,
        biome_name: "Tropical",
    },
    RegionTemplateMeta {
        terrain_id: "sn20snow",
        template_id: "gen002020",
        zone_code: "unstable-snow-lv20-01",
        role: durango_wire::RegionRole::Risky,
        level: 20,
        biome: 4,
        biome_name: "SnowField",
    },
    RegionTemplateMeta {
        terrain_id: "ri25tp01",
        template_id: "ri25tp01",
        zone_code: "unstable-tropical-lv25-01",
        role: durango_wire::RegionRole::Risky,
        level: 25,
        biome: 1,
        biome_name: "Tropical",
    },
    RegionTemplateMeta {
        terrain_id: "ri30td01",
        template_id: "ri30td01",
        zone_code: "unstable-tundra-lv30-01",
        role: durango_wire::RegionRole::Risky,
        level: 30,
        biome: 3,
        biome_name: "Tundra",
    },
    RegionTemplateMeta {
        terrain_id: "ri35de",
        template_id: "ri35deSub01",
        zone_code: "unstable-desert-lv35-01",
        role: durango_wire::RegionRole::Risky,
        level: 35,
        biome: 2,
        biome_name: "Desert",
    },
    RegionTemplateMeta {
        terrain_id: "ri35te",
        template_id: "ri35teSub01",
        zone_code: "unstable-temperate-lv35-01",
        role: durango_wire::RegionRole::Risky,
        level: 35,
        biome: 0,
        biome_name: "Temperate",
    },
    RegionTemplateMeta {
        terrain_id: "ri40tr",
        template_id: "ri40trSub01",
        zone_code: "unstable-tropical-lv40-01",
        role: durango_wire::RegionRole::Risky,
        level: 40,
        biome: 1,
        biome_name: "Tropical",
    },
    RegionTemplateMeta {
        terrain_id: "ri45sa",
        template_id: "ri45saSub01",
        zone_code: "unstable-savanna-lv45-01",
        role: durango_wire::RegionRole::Risky,
        level: 45,
        biome: 5,
        biome_name: "Savanna",
    },
    RegionTemplateMeta {
        terrain_id: "ri50sn",
        template_id: "ri50snSub01",
        zone_code: "unstable-snow-lv50-01",
        role: durango_wire::RegionRole::Risky,
        level: 50,
        biome: 4,
        biome_name: "SnowField",
    },
    RegionTemplateMeta {
        terrain_id: "ri55tu",
        template_id: "ri55tuSub01",
        zone_code: "unstable-tundra-lv55-01",
        role: durango_wire::RegionRole::Risky,
        level: 55,
        biome: 3,
        biome_name: "Tundra",
    },
    RegionTemplateMeta {
        terrain_id: "ra60sw",
        template_id: "ra60sw180226",
        zone_code: "unstable-swamp-lv60-01",
        role: durango_wire::RegionRole::Risky,
        level: 60,
        biome: 6,
        biome_name: "Swamp",
    },
    RegionTemplateMeta {
        terrain_id: "ua60vol",
        template_id: "ua60vol_06_03",
        zone_code: "unstable-volcanic-lv60-01",
        role: durango_wire::RegionRole::Risky,
        level: 60,
        biome: 7,
        biome_name: "Volcanic",
    },
];

pub fn lookup_region_template(id_or_code: &str) -> Option<&'static RegionTemplateMeta> {
    let trimmed = id_or_code.trim();
    if trimmed.is_empty() {
        return None;
    }
    let clean = trimmed
        .strip_prefix("arch-")
        .or_else(|| trimmed.strip_prefix("arch_"))
        .unwrap_or(trimmed);
    if clean.eq_ignore_ascii_case("tropical_event_ancora_01") {
        return KNOWN_REGION_TEMPLATES.iter().find(|m| m.terrain_id == "ri18tp01");
    }
    if clean.eq_ignore_ascii_case("grass_company_safehouse_01") {
        return KNOWN_REGION_TEMPLATES.iter().find(|m| m.terrain_id == "pe10gr_2");
    }
    KNOWN_REGION_TEMPLATES.iter().find(|m| {
        m.zone_code.eq_ignore_ascii_case(clean)
            || m.terrain_id.eq_ignore_ascii_case(clean)
            || m.template_id.eq_ignore_ascii_case(clean)
    })
}

/// Parses either a canonical zone code (`tamed-grassland-lv10-01`) OR a legacy terrain ID (`pe10gr_1`)
/// into `(role_slug, role_name, level_num, biome_slug, biome_name, instance_num)`.
fn parse_region_parts(region_id: &str) -> (&'static str, &'static str, u32, &'static str, &'static str, u32) {
    if let Some(meta) = lookup_region_template(region_id) {
        let (role_slug, role_name) = match meta.role {
            durango_wire::RegionRole::Personal => ("tamed", "Tamed Island"),
            _ => ("unstable", "Unstable Island"),
        };
        let biome_slug = match meta.biome_name {
            "Grassland" => "grassland",
            "Savanna" => "savanna",
            "Tropical" => "tropical",
            "Temperate" => "temperate",
            "Desert" => "desert",
            "Tundra" => "tundra",
            "SnowField" => "snow",
            "Swamp" => "swamp",
            "Volcanic" => "volcanic",
            _ => "wilderness",
        };
        let inst = meta
            .zone_code
            .rsplit_once('-')
            .and_then(|(_, s)| s.parse::<u32>().ok())
            .unwrap_or(1);
        return (
            role_slug,
            role_name,
            meta.level as u32,
            biome_slug,
            meta.biome_name,
            inst,
        );
    }
    let lower = region_id.trim().to_ascii_lowercase();
    let (role_slug, role_name) = if lower.starts_with("tamed-") || lower.starts_with("pe") {
        ("tamed", "Tamed Island")
    } else if lower.starts_with("outpost-") || lower.starts_with("op") {
        ("outpost", "Outpost Island")
    } else if lower.starts_with("clan-") || lower.starts_with("cl") {
        ("clan", "Clan Island")
    } else if lower.starts_with("civilized-")
        || lower.starts_with("tr")
        || lower.starts_with("s30")
        || lower.starts_with("ur")
        || lower.starts_with('c')
    {
        ("civilized", "Civilized Island")
    } else if lower.starts_with("event-") || lower.starts_with("ev") {
        ("event", "Event Island")
    } else if lower.starts_with("starter-") || lower.starts_with("i01") {
        ("starter", "Starter Island")
    } else {
        ("unstable", "Unstable Island")
    };

    let digits: String = lower
        .chars()
        .skip_while(|c| !c.is_ascii_digit())
        .take_while(|c| c.is_ascii_digit())
        .take(2)
        .collect();
    let level_num = digits
        .parse::<u32>()
        .ok()
        .filter(|&n| n > 0 && n <= 80)
        .unwrap_or(10);

    let (biome_slug, biome_name) = if lower.contains("grassland") || lower.contains("gr") {
        ("grassland", "Grassland")
    } else if lower.contains("tropical") || lower.contains("tp") || (lower.contains("tr") && !lower.starts_with("tr")) {
        ("tropical", "Tropical")
    } else if lower.contains("temperate") || lower.contains("te") {
        ("temperate", "Temperate")
    } else if lower.contains("forest") || lower.contains("fo") {
        ("forest", "Forest")
    } else if lower.contains("savanna") || lower.contains("sa") || lower.contains("sv") {
        ("savanna", "Savanna")
    } else if lower.contains("desert") || lower.contains("de") {
        ("desert", "Desert")
    } else if lower.contains("snow") || lower.contains("sn") {
        ("snow", "SnowField")
    } else if lower.contains("tundra") || lower.contains("tu") || lower.contains("td") {
        ("tundra", "Tundra")
    } else if lower.contains("swamp") || lower.contains("sw") {
        ("swamp", "Swamp")
    } else if lower.contains("volcanic") || lower.contains("vol") {
        ("volcanic", "Volcanic")
    } else {
        ("wilderness", "Wilderness")
    };

    let instance_num = lower
        .rsplit_once('-')
        .or_else(|| lower.rsplit_once('_'))
        .and_then(|(_, suf)| suf.parse::<u32>().ok())
        .filter(|&n| n > 0 && n < 100)
        .unwrap_or(1);

    (role_slug, role_name, level_num, biome_slug, biome_name, instance_num)
}

/// Canonicalizes a legacy terrain ID (`pe10gr_1`) or existing zone code into the authoritative
/// SWE kebab-case zone code stored in SQLite & sent in `RegionMsg.id` (`tamed-grassland-lv10-01`).
pub fn format_region_code(region_id: &str) -> String {
    let id = region_id.trim();
    if id.is_empty() {
        return String::new();
    }
    if let Some(meta) = lookup_region_template(id) {
        return meta.zone_code.to_string();
    }
    let (role_slug, _, level_num, biome_slug, _, inst) = parse_region_parts(id);
    format!("{role_slug}-{biome_slug}-lv{level_num}-{inst:02}")
}

/// Resolves a canonical zone code (`tamed-grassland-lv10-01`) back to its static terrain map asset ID
/// (`pe10gr_1`) for loading `.zip` terrain geometry. Passes raw terrain asset IDs straight through.
pub fn terrain_id_for_region(region_or_terrain: &str) -> String {
    let id = region_or_terrain.trim();
    if id.is_empty() || id == "1" {
        return DEFAULT_TERRAIN_ID.to_string();
    }
    if let Some(meta) = lookup_region_template(id) {
        return meta.terrain_id.to_string();
    }
    // Already a raw terrain asset filename (no hyphens, e.g. `pe10gr_1`)
    if !id.contains('-') {
        return id.to_string();
    }
    let (role_slug, _, level_num, biome_slug, biome_name, inst) = parse_region_parts(id);
    if let Some(meta) = KNOWN_REGION_TEMPLATES.iter().find(|m| {
        m.zone_code.starts_with(role_slug)
            && m.biome_name.eq_ignore_ascii_case(biome_name)
            && m.level == level_num as i32
    }) {
        return meta.terrain_id.to_string();
    }
    let role_prefix = match role_slug {
        "tamed" => "pe",
        "outpost" => "op",
        "clan" => "cl",
        "civilized" => "c",
        "event" => "ev",
        "starter" => "i01",
        _ => "ri",
    };
    let biome_code = match biome_slug {
        "grassland" => "gr",
        "tropical" => "tr",
        "temperate" => "te",
        "forest" => "fo",
        "savanna" => "sa",
        "desert" => "de",
        "tundra" => "tu",
        "snow" => "sn",
        "swamp" => "sw",
        "volcanic" => "vol",
        _ => "gr",
    };
    format!("{role_prefix}{level_num}{biome_code}_{inst}")
}

/// Standardized human-readable region label (e.g. `tamed-grassland-lv10-01` -> `Tamed Island · Lv.10 Grassland`).
pub fn region_display_name(region_id: &str) -> String {
    let (_, role_name, level_num, _, biome_name, _) = parse_region_parts(region_id);
    format!("{role_name} · Lv.{level_num} {biome_name}")
}

#[derive(Debug, Clone)]
pub struct LiveClient {
    pub conn_id: u64,
    pub entity_id: String,
    pub owner_key: String,
    pub peer_ip: String,
    pub name: String,
    pub level: i32,
    pub region_id: String,
    pub pos_x: f32,
    pub pos_y: f32,
    pub life: f32,
    pub max_life: f32,
    pub stamina: f32,
    pub stamina_at: f64,
    pub energy: f32,
    pub fatigue: f32,
    pub is_alive: bool,
    pub is_resting: bool,
    pub rest_updated_at: f64,
    pub last_dodge_at: f64,
    pub last_battle_action_at: f64,
    pub well_rested_until: f64,
    pub warp_generation: u64,
    pub in_battle: bool,
    pub battle_target_id: Option<String>,
    pub boarding_on: i32,
    pub boarding_since: Option<f64>,
    pub boarding_until: Option<f64>,
    pub vehicle_entity_id: String,
    pub tx: mpsc::Sender<Vec<u8>>,
    pub kick_tx: Option<tokio::sync::watch::Sender<bool>>,
}

impl LiveClient {
    pub fn current_stamina(&self, now: f64) -> f32 {
        let dt = (now - self.stamina_at).max(0.0) as f32;
        (self.stamina + dt * STAMINA_REGEN_PER_SEC).min(100.0)
    }

    pub fn consume_stamina(&mut self, now: f64, cost: f32) -> f32 {
        let cur = self.current_stamina(now);
        self.stamina = (cur - cost.max(0.0)).max(0.0);
        self.stamina_at = now;
        self.stamina
    }

    pub fn consume_dash(&mut self, now: f64) -> f32 {
        self.consume_stamina(now, DASH_STAMINA_COST)
    }

    pub fn apply_gather(&mut self, now: f64) {
        self.stamina = self.current_stamina(now);
        self.stamina_at = now;
        self.energy = (self.energy - GATHER_ENERGY_COST).max(0.0);
        self.fatigue = (self.fatigue + GATHER_FATIGUE_GAIN).min(100.0);
    }

    pub fn apply_eat_with(&mut self, now: f64, life_delta: f32, energy_delta: f32, fatigue_delta: f32) {
        self.stamina = self.current_stamina(now);
        self.stamina_at = now;
        self.life = (self.life + life_delta).clamp(0.0, self.max_life);
        self.energy = (self.energy + energy_delta).clamp(0.0, 100.0);
        self.fatigue = (self.fatigue + fatigue_delta).clamp(0.0, 100.0);
    }

    pub fn apply_eat(&mut self, now: f64) {
        self.apply_eat_with(now, 12.0, 25.0, -10.0);
    }

    pub fn apply_craft(&mut self, now: f64, energy_cost: f32) {
        self.stamina = self.current_stamina(now);
        self.stamina_at = now;
        self.energy = (self.energy - energy_cost.clamp(0.0, 40.0)).max(0.0);
        self.fatigue = (self.fatigue + 2.0).min(100.0);
    }

    pub fn apply_rest(&mut self, now: f64) {
        self.stamina = self.current_stamina(now);
        self.stamina_at = now;
        // Start continuous resting recovery (decreases fatigue smoothly while staying resting!)
        self.is_resting = true;
        self.rest_updated_at = now;
        // Well-Rested ("energetic") buff lasts 10 minutes (+25% XP gain)
        self.well_rested_until = now + 600.0;
    }

    /// Advances continuous resting recovery (`-2.0` fatigue/sec, `+4.0` energy/sec, `+3.0` life/sec).
    /// Returns `true` if any gauge changed.
    pub fn tick_rest(&mut self, now: f64) -> bool {
        if !self.is_resting || !self.is_alive {
            if !self.is_alive {
                self.is_resting = false;
            }
            return false;
        }
        let dt = (now - self.rest_updated_at).max(0.0) as f32;
        if dt < 0.05 {
            return false;
        }
        self.rest_updated_at = now;
        let old_f = self.fatigue;
        let old_e = self.energy;
        let old_l = self.life;
        self.fatigue = (self.fatigue - dt * REST_FATIGUE_PER_SEC).max(0.0);
        self.energy = (self.energy + dt * REST_ENERGY_PER_SEC).min(100.0);
        self.life = (self.life + dt * REST_LIFE_PER_SEC).min(self.max_life);
        if self.fatigue <= 0.001 && self.energy >= 99.99 && self.life >= self.max_life - 0.01 {
            self.is_resting = false;
        }
        (self.fatigue - old_f).abs() > 0.001
            || (self.energy - old_e).abs() > 0.001
            || (self.life - old_l).abs() > 0.001
    }

    /// Stops resting (e.g., player moves or acts), finalizing any recovery accrued so far.
    pub fn stop_rest(&mut self, now: f64) -> bool {
        if !self.is_resting {
            return false;
        }
        self.tick_rest(now);
        self.is_resting = false;
        true
    }

    pub fn to_survival_msg(&self, now: f64) -> SurvivalMsg {
        let cur_stamina = self.current_stamina(now);
        let mut gauges = BTreeMap::new();
        gauges.insert(
            "stamina".into(),
            GaugeMsg::ramp(now, cur_stamina, 100.0, STAMINA_REGEN_PER_SEC, 100.0),
        );
        if self.is_resting {
            let energy_g = if self.energy < 100.0 {
                GaugeMsg::ramp(now, self.energy, 100.0, REST_ENERGY_PER_SEC, 100.0)
            } else {
                GaugeMsg::constant(now, self.energy, 100.0)
            };
            let fatigue_g = if self.fatigue > 0.0 {
                let dur = (self.fatigue / REST_FATIGUE_PER_SEC).max(0.01) as f64;
                GaugeMsg {
                    min: 0.0,
                    max: 100.0,
                    nodes: vec![(now, self.fatigue), (now + dur, 0.0)],
                }
            } else {
                GaugeMsg::constant(now, 0.0, 100.0)
            };
            let life_g = if self.life < self.max_life {
                GaugeMsg::ramp(now, self.life, self.max_life, REST_LIFE_PER_SEC, self.max_life)
            } else {
                GaugeMsg::constant(now, self.life, self.max_life)
            };
            gauges.insert("energy".into(), energy_g);
            gauges.insert("fatigue".into(), fatigue_g);
            SurvivalMsg {
                entity_id: self.entity_id.clone(),
                life: life_g,
                gauges,
            }
        } else {
            gauges.insert("energy".into(), GaugeMsg::constant(now, self.energy, 100.0));
            gauges.insert(
                "fatigue".into(),
                GaugeMsg::constant(now, self.fatigue, 100.0),
            );
            SurvivalMsg {
                entity_id: self.entity_id.clone(),
                life: GaugeMsg::constant(now, self.life, self.max_life),
                gauges,
            }
        }
    }
}

#[derive(Debug, Clone)]
pub struct LiveAnimal {
    pub entity_id: String,
    pub region_id: String,
    pub entity_type: u16,
    pub name: String,
    pub level: i32,
    pub hp: f32,
    pub max_hp: f32,
    pub home_x: f32,
    pub home_y: f32,
    pub pos_x: f32,
    pub pos_y: f32,
    pub walk_from_x: f32,
    pub walk_from_y: f32,
    pub walk_start_at: f64,
    pub walk_end_at: f64,
    pub yaw: f32,
    pub base_scale: f32,
    pub size_level: i32,
    pub is_aggressive: bool,
    pub attack_cooltime: f32,
    pub stand_motion: String,
    pub move_motion: String,
    pub dead_motion: String,
    pub battle_stand_motion: String,
    pub attack_motion: String,
    pub attacks: Vec<String>,
    pub attack_damage: i32,
    pub next_wander_at: f64,
    pub stop_walking_at: f64,
    pub stand_at: f64,
    pub threat_telegraph_until: f64,
    pub aggro_seen_at: f64,
    pub next_attack_at: f64,
    pub respawn_at: f64,
    pub is_alive: bool,
    pub butchered: bool,
    pub butcher_stock: HashMap<String, i32>,
    pub enemy_id: Option<String>,
}

impl LiveAnimal {
    /// Returns the animal's real-time interpolated world position mid-walk so chases,
    /// proximity aggro, and death poses never snap back to a stale coordinate.
    pub fn current_pos(&self, now: f64) -> (f32, f32) {
        if self.walk_end_at > self.walk_start_at && now < self.walk_end_at {
            let t = ((now - self.walk_start_at) / (self.walk_end_at - self.walk_start_at))
                .clamp(0.0, 1.0) as f32;
            (
                self.walk_from_x + (self.pos_x - self.walk_from_x) * t,
                self.walk_from_y + (self.pos_y - self.walk_from_y) * t,
            )
        } else {
            (self.pos_x, self.pos_y)
        }
    }
}

#[derive(Clone)]
pub struct ServerState {
    pub config: Arc<ServerConfig>,
    pub db: Arc<Database>,
    pub terrains: Arc<TerrainStore>,
    pub catalog: Arc<GameCatalog>,
    pub sessions: Arc<RwLock<HashMap<String, SessionEntry>>>,
    pub temp_characters: Arc<RwLock<HashMap<String, CharacterRecord>>>,
    pub live_clients: Arc<RwLock<HashMap<String, LiveClient>>>,
    pub removed_naturals: Arc<RwLock<HashMap<String, HashSet<(u16, u16)>>>>,
    pub natural_stocks: Arc<RwLock<HashMap<(String, u16, u16), HashMap<String, i32>>>>,
    pub animals: Arc<RwLock<HashMap<String, LiveAnimal>>>,
    pub seeded_animal_regions: Arc<RwLock<HashSet<String>>>,
    pub seeded_poi_regions: Arc<RwLock<HashSet<String>>>,
    pub directory_servers: Arc<RwLock<HashMap<String, DirectoryServerEntry>>>,
    pub metrics_history: Arc<RwLock<VecDeque<MetricSample>>>,
    pub self_exe_sha256: String,
    pub assets_digest: String,
    pub attestation_release_id: String,
    pub ca_allowlist: Arc<RwLock<Option<crate::attestation::CaAllowlist>>>,
    pub ca_challenges: Arc<crate::attestation::CaChallengeStore>,
    pub started_at: f64,
    pub game_tcp_port: u16,
}

impl ServerState {
    pub fn effective_attestation_key(&self) -> Option<&str> {
        self.config
            .attestation_key
            .as_deref()
            .or(crate::attestation::COMPILED_BUILD_KEY)
            .map(str::trim)
            .filter(|s| !s.is_empty())
    }

    pub fn new(config: ServerConfig, game_tcp_port: u16) -> Result<Self, rusqlite::Error> {
        let db = Database::open(config.db_path.as_deref())?;
        let terrains = TerrainStore::new(config.terrains_dir.clone());
        let catalog = GameCatalog::load(config.assets_dir.as_deref());
        let self_exe_sha256 = crate::attestation::compute_self_exe_sha256();
        let assets_digest = crate::attestation::compute_assets_digest(config.assets_dir.as_deref());
        let attestation_release_id = crate::attestation::COMPILED_RELEASE_ID
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .unwrap_or("source-build")
            .to_string();
        let loaded_allowlist = config
            .ca_allowlist_path
            .as_deref()
            .and_then(|p| match crate::attestation::CaAllowlist::load_from_file(p) {
                Ok(al) => Some(al),
                Err(err) => {
                    tracing::warn!(%err, "failed to load DURANGO_CA_ALLOWLIST_FILE");
                    None
                }
            });
        let started_at = Self::now_unix_secs();
        db.log_audit(
            started_at,
            "SERVER",
            "system",
            "",
            "",
            &format!(
                "Server started: '{}' (HTTP {}, TCP {})",
                config.server_name, config.http_bind, game_tcp_port
            ),
        );
        let mut initial_metrics = VecDeque::with_capacity(360);
        initial_metrics.push_back(MetricSample {
            ts: started_at,
            online: 0,
            sessions: 0,
            resting: 0,
            bans: db.list_active_bans(started_at).map_or(0, |b| b.len()),
            events_1m: 1,
        });
        let mut initial_sessions = HashMap::new();
        if let Ok(persisted) = db.load_valid_sessions(started_at - SESSION_TTL_SECS) {
            for s in persisted {
                initial_sessions.insert(
                    s.token,
                    SessionEntry {
                        owner_key: s.owner_key,
                        entity_id: s.entity_id,
                        password_ok: s.password_ok,
                        created_at: s.created_at,
                    },
                );
            }
        }
        Ok(Self {
            config: Arc::new(config),
            db: Arc::new(db),
            terrains: Arc::new(terrains),
            catalog: Arc::new(catalog),
            sessions: Arc::new(RwLock::new(initial_sessions)),
            temp_characters: Arc::new(RwLock::new(HashMap::new())),
            live_clients: Arc::new(RwLock::new(HashMap::new())),
            removed_naturals: Arc::new(RwLock::new(HashMap::new())),
            natural_stocks: Arc::new(RwLock::new(HashMap::new())),
            animals: Arc::new(RwLock::new(HashMap::new())),
            seeded_animal_regions: Arc::new(RwLock::new(HashSet::new())),
            seeded_poi_regions: Arc::new(RwLock::new(HashSet::new())),
            directory_servers: Arc::new(RwLock::new(HashMap::new())),
            metrics_history: Arc::new(RwLock::new(initial_metrics)),
            self_exe_sha256,
            assets_digest,
            attestation_release_id,
            ca_allowlist: Arc::new(RwLock::new(loaded_allowlist)),
            ca_challenges: Arc::new(crate::attestation::CaChallengeStore::default()),
            started_at,
            game_tcp_port,
        })
    }

    pub async fn sample_metrics(&self) {
        let now = Self::now_unix_secs();
        let (online, resting) = {
            let live = self.live_clients.read().await;
            let rest = live.values().filter(|c| c.is_resting).count();
            (live.len(), rest)
        };
        let sessions = self.sessions.read().await.len();
        let bans = self.db.count_active_bans(now).unwrap_or(0);
        let events_1m = self.db.count_audit_logs_since(now - 60.0).unwrap_or(0);
        let mut hist = self.metrics_history.write().await;
        if let Some(last) = hist.back() {
            if now - last.ts < 0.85 {
                return;
            }
        }
        hist.push_back(MetricSample {
            ts: now,
            online,
            sessions,
            resting,
            bans,
            events_1m,
        });
        while hist.len() > 360 {
            hist.pop_front();
        }
    }

    pub fn now_unix_secs() -> f64 {
        SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map_or(0.0, |d| d.as_secs_f64())
    }

    pub fn effective_join_password(&self) -> Option<String> {
        if let Ok(Some(saved)) = self.db.get_setting("join_password") {
            let trimmed = saved.trim().to_string();
            return if trimmed.is_empty() { None } else { Some(trimmed) };
        }
        self.config
            .join_password
            .as_deref()
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(ToString::to_string)
    }

    pub fn effective_server_name(&self) -> String {
        if let Ok(Some(saved)) = self.db.get_setting("server_name") {
            let trimmed = saved.trim().to_string();
            if !trimmed.is_empty() {
                return trimmed;
            }
        }
        self.config.server_name.clone()
    }

    pub fn effective_notice(&self) -> String {
        if let Ok(Some(saved)) = self.db.get_setting("notice") {
            return saved;
        }
        self.config.notice_text.clone()
    }

    pub fn verify_join_password(&self, provided: Option<&str>) -> bool {
        let Some(expected) = self.effective_join_password() else {
            return true;
        };
        let Some(given) = provided.map(str::trim) else {
            return false;
        };
        // Constant-time byte compare
        let a = expected.as_bytes();
        let b = given.as_bytes();
        if a.len() != b.len() {
            return false;
        }
        let mut diff = 0u8;
        for (&x, &y) in a.iter().zip(b.iter()) {
            diff |= x ^ y;
        }
        diff == 0
    }

    pub fn is_admin_owner(&self, owner_key: &str) -> bool {
        let Some(needle) = canonical_account_id(owner_key) else {
            return false;
        };
        self.config.admin_keys.iter().any(|k| {
            canonical_account_id(k)
                .is_some_and(|canon_k| canon_k.eq_ignore_ascii_case(&needle))
        })
    }

    pub async fn kick_by_entity(&self, entity_id: &str) -> bool {
        let client = self.live_clients.read().await.get(entity_id).cloned();
        if let Some(c) = client {
            if let Some(ktx) = &c.kick_tx {
                let _ = ktx.send(true);
            }
            return true;
        }
        false
    }

    pub async fn kick_by_owner(&self, owner_key: &str) -> usize {
        let targets: Vec<LiveClient> = self
            .live_clients
            .read()
            .await
            .values()
            .filter(|c| c.owner_key.eq_ignore_ascii_case(owner_key))
            .cloned()
            .collect();
        let n = targets.len();
        for c in targets {
            if let Some(ktx) = &c.kick_tx {
                let _ = ktx.send(true);
            }
        }
        n
    }

    pub async fn kick_by_ip(&self, ip_pattern: &str) -> usize {
        let targets: Vec<LiveClient> = self
            .live_clients
            .read()
            .await
            .values()
            .filter(|c| crate::db::ip_matches_pattern(&c.peer_ip, ip_pattern))
            .cloned()
            .collect();
        let n = targets.len();
        for c in targets {
            if let Some(ktx) = &c.kick_tx {
                let _ = ktx.send(true);
            }
        }
        n
    }

    pub async fn upsert_directory_server(&self, entry: DirectoryServerEntry) {
        self.directory_servers
            .write()
            .await
            .insert(entry.server_id.clone(), entry);
    }

    pub async fn remove_directory_server(&self, server_id: &str) -> bool {
        self.directory_servers
            .write()
            .await
            .remove(server_id)
            .is_some()
    }

    pub async fn replace_upstream_directory_servers(&self, entries: Vec<DirectoryServerEntry>) {
        let mut map = self.directory_servers.write().await;
        map.retain(|k, _| !k.starts_with("upstream:"));
        for entry in entries {
            map.insert(entry.server_id.clone(), entry);
        }
    }

    /// Returns this server first (always active), followed by all live federated community servers
    /// that sent a heartbeat within the last 90 seconds.
    pub async fn list_live_directory_servers(
        &self,
        self_gateway: &str,
        now: f64,
    ) -> Vec<DirectoryServerEntry> {
        const TTL_SECS: f64 = 90.0;
        let online_self = self.live_clients.read().await.len();
        let norm_self = self_gateway.trim_end_matches('/').to_string();

        let mut list = vec![DirectoryServerEntry {
            server_id: "self".to_string(),
            name: self.effective_server_name(),
            gateway: norm_self.clone(),
            region: self.config.region_tag.clone(),
            online_players: online_self,
            max_players: self.config.max_players,
            password_required: self.effective_join_password().is_some(),
            pvp_enabled: false,
            verified: None,
            version: "3.3.6".to_string(),
            last_seen_at: now,
        }];

        let mut map = self.directory_servers.write().await;
        map.retain(|_, v| now - v.last_seen_at <= TTL_SECS);
        let mut remotes: Vec<DirectoryServerEntry> = map
            .values()
            .filter(|e| !e.gateway.trim_end_matches('/').eq_ignore_ascii_case(&norm_self))
            .cloned()
            .collect();
        remotes.sort_by(|a, b| a.name.to_ascii_lowercase().cmp(&b.name.to_ascii_lowercase()));
        list.extend(remotes);
        list
    }

    pub async fn prune_stale_sessions(&self) {
        let now = Self::now_unix_secs();
        let min_session_ts = now - SESSION_TTL_SECS;
        let min_temp_ts = now - TEMP_CHAR_TTL_SECS;
        self.sessions
            .write()
            .await
            .retain(|_, s| s.created_at >= min_session_ts);
        let active_entities: HashSet<String> = self
            .sessions
            .read()
            .await
            .values()
            .map(|s| s.entity_id.clone())
            .collect();
        self.temp_characters.write().await.retain(|eid, ch| {
            ch.created_at >= min_temp_ts || active_entities.contains(eid)
        });
        let _ = self.db.prune_expired_sessions(min_session_ts);
    }

    pub async fn get_valid_session(&self, token: &str) -> Option<SessionEntry> {
        let now = Self::now_unix_secs();
        let entry = self.sessions.read().await.get(token).cloned()?;
        if entry.created_at + SESSION_TTL_SECS < now {
            self.sessions.write().await.remove(token);
            let _ = self.db.delete_session(token);
            return None;
        }
        Some(entry)
    }

    pub async fn issue_session(&self, owner_key: &str) -> (String, String) {
        self.issue_session_with_auth(owner_key, true).await
    }

    pub async fn issue_session_with_auth(
        &self,
        owner_key: &str,
        password_ok: bool,
    ) -> (String, String) {
        let now = Self::now_unix_secs();
        self.prune_stale_sessions().await;
        let existing = self.db.list_by_owner(owner_key).unwrap_or_default();
        let entity_id = if let Some(first) = existing.first() {
            first.entity_id.clone()
        } else {
            let id = Uuid::new_v4().to_string();
            let short = id[..8].to_string();
            let temp = CharacterRecord {
                entity_id: id.clone(),
                owner_key: owner_key.to_string(),
                name: short,
                job: 0,
                level: 1,
                region_id: DEFAULT_REGION_CODE.to_string(),
                terrain_id: DEFAULT_TERRAIN_ID.to_string(),
                pos_x: 63.0,
                pos_y: 71.0,
                disconnected_at: 0.0,
                created_at: now,
                gender: "male".to_string(),
                model_info: String::new(),
                exp: 0,
                untrained_count: 0,
                life: 100.0,
                stamina: 100.0,
                energy: 100.0,
                fatigue: 0.0,
            };
            self.temp_characters.write().await.insert(id.clone(), temp);
            id
        };

        let token = Uuid::new_v4().simple().to_string();
        let entry = SessionEntry {
            owner_key: owner_key.to_string(),
            entity_id: entity_id.clone(),
            password_ok,
            created_at: now,
        };
        self.sessions.write().await.insert(token.clone(), entry);
        let _ = self
            .db
            .upsert_session(&token, owner_key, &entity_id, password_ok, now);
        (entity_id, token)
    }

    pub async fn resolve_character(&self, entity_id: &str) -> Option<CharacterRecord> {
        if let Ok(Some(rec)) = self.db.get_by_entity_id(entity_id) {
            return Some(rec);
        }
        self.temp_characters.read().await.get(entity_id).cloned()
    }

    pub async fn bind_session_to_entity(&self, token: &str, entity_id: &str) -> bool {
        let Some(entry) = self.get_valid_session(token).await else {
            return false;
        };
        let Some(ch) = self.resolve_character(entity_id).await else {
            return false;
        };
        if !ch.owner_key.eq_ignore_ascii_case(&entry.owner_key) {
            return false;
        }
        let updated = {
            let mut sessions = self.sessions.write().await;
            if let Some(s) = sessions.get_mut(token) {
                s.entity_id = entity_id.to_string();
                true
            } else {
                false
            }
        };
        if updated {
            let _ = self.db.update_session_entity(token, entity_id);
        }
        updated
    }

    pub async fn broadcast_region_except(
        &self,
        region_id: &str,
        skip_entity_id: &str,
        frame: &[u8],
    ) {
        let live = self.live_clients.read().await;
        for (eid, client) in live.iter() {
            if eid != skip_entity_id && client.region_id == region_id {
                let _ = client.tx.try_send(frame.to_vec());
            }
        }
    }

    pub async fn broadcast_region_all(&self, region_id: &str, frame: &[u8]) {
        let live = self.live_clients.read().await;
        for client in live.values() {
            if client.region_id == region_id {
                let _ = client.tx.try_send(frame.to_vec());
            }
        }
    }

    /// Snapshots all live connected players (releasing the lock before SQLite I/O)
    /// and flushes their position, survival vitals, and disconnect timestamp on shutdown.
    pub async fn flush_all_live_clients(&self) -> usize {
        let now = Self::now_unix_secs();
        let snapshot: Vec<(String, f32, f32, f32, f32, f32, f32)> = {
            let live = self.live_clients.read().await;
            live.iter()
                .map(|(eid, c)| {
                    (
                        eid.clone(),
                        c.pos_x,
                        c.pos_y,
                        c.life,
                        c.current_stamina(now),
                        c.energy,
                        c.fatigue,
                    )
                })
                .collect()
        };
        let count = snapshot.len();
        for (eid, px, py, life, stamina, energy, fatigue) in snapshot {
            let _ = self
                .db
                .flush_character_vitals(&eid, px, py, life, stamina, energy, fatigue, now);
        }
        count
    }

    pub async fn removed_naturals_snapshot(&self, region_id: &str) -> HashSet<(u16, u16)> {
        if let Some(existing) = self.removed_naturals.read().await.get(region_id) {
            return existing.clone();
        }
        let loaded: HashSet<(u16, u16)> = self
            .db
            .list_removed_naturals(region_id)
            .unwrap_or_default()
            .into_iter()
            .collect();
        let mut map = self.removed_naturals.write().await;
        map.entry(region_id.to_string())
            .or_insert_with(|| loaded.clone())
            .clone()
    }

    /// Atomically reserves/removes a natural tile for a region. Returns `true` if newly removed,
    /// `false` if already depleted.
    pub async fn reserve_and_remove_natural(&self, region_id: &str, tile: (u16, u16)) -> bool {
        let _ = self.removed_naturals_snapshot(region_id).await;
        let inserted = {
            let mut map = self.removed_naturals.write().await;
            map.entry(region_id.to_string()).or_default().insert(tile)
        };
        if inserted {
            let _ = self.db.mark_natural_removed(region_id, tile.0, tile.1);
        }
        inserted
    }

    /// Returns the remaining per-generator resource stock for a natural tile without mutating
    /// `natural_stocks` on read-only touch/inspect calls (prevents unbounded map growth).
    pub async fn peek_natural_stock(
        &self,
        region_id: &str,
        tile: (u16, u16),
        spec: &NaturalSpec,
    ) -> HashMap<String, i32> {
        let key = (region_id.to_string(), tile.0, tile.1);
        if let Some(existing) = self.natural_stocks.read().await.get(&key) {
            return existing.clone();
        }
        let mut stock = HashMap::new();
        for g in &spec.generators {
            stock.insert(g.id.clone(), g.amount.max(1));
        }
        stock
    }

    /// Returns the remaining per-generator resource stock for a natural tile (initializing from
    /// `NaturalSpec` on first touch so trees/bushes/rocks yield ALL their logs, branches, leaves, etc.).
    pub async fn get_or_init_natural_stock(
        &self,
        region_id: &str,
        tile: (u16, u16),
        spec: &NaturalSpec,
    ) -> HashMap<String, i32> {
        self.peek_natural_stock(region_id, tile, spec).await
    }

    /// Atomically decrements a single unit from `generator_id` on a natural tile.
    /// Returns `Some((remaining_in_generator, tile_fully_depleted))` or `None` if already depleted.
    pub async fn consume_natural_generator(
        &self,
        region_id: &str,
        tile: (u16, u16),
        spec: &NaturalSpec,
        generator_id: &str,
    ) -> Option<(i32, bool)> {
        let _ = self.removed_naturals_snapshot(region_id).await;
        let key = (region_id.to_string(), tile.0, tile.1);
        // Hold `removed_naturals` and `natural_stocks` together across check+decrement+removal
        // so two concurrent gathers cannot double-consume or re-seed a just-depleted tile.
        let (gen_left, fully_depleted, newly_removed) = {
            let mut removed_map = self.removed_naturals.write().await;
            let removed_set = removed_map.entry(region_id.to_string()).or_default();
            if removed_set.contains(&tile) {
                return None;
            }
            let mut stocks = self.natural_stocks.write().await;
            let entry = stocks.entry(key.clone()).or_insert_with(|| {
                spec.generators
                    .iter()
                    .map(|g| (g.id.clone(), g.amount.max(1)))
                    .collect()
            });
            let target_key = if generator_id.is_empty() {
                entry
                    .iter()
                    .find(|(_, &v)| v > 0)
                    .map(|(k, _)| k.clone())?
            } else if entry.get(generator_id).copied().unwrap_or(0) > 0 {
                generator_id.to_string()
            } else {
                return None;
            };
            let rem = entry.get_mut(&target_key)?;
            if *rem <= 0 {
                return None;
            }
            *rem -= 1;
            let gen_left = *rem;
            let total_left: i32 = entry.values().copied().filter(|&v| v > 0).sum();
            let depleted = total_left <= 0;
            let newly_removed = if depleted {
                stocks.remove(&key);
                removed_set.insert(tile)
            } else {
                false
            };
            (gen_left, depleted, newly_removed)
        };
        if newly_removed {
            let _ = self.db.mark_natural_removed(region_id, tile.0, tile.1);
        }
        Some((gen_left, fully_depleted))
    }

    /// Builds a full-color Durango `CharacterAppearance` from the player's persisted gender,
    /// `model_info` JSON, and currently equipped body clothing so characters never render black/gray.
    pub fn build_character_appearance(&self, ch: &CharacterRecord) -> CharacterAppearance {
        let is_male = !ch.gender.eq_ignore_ascii_case("female");
        let mut app = CharacterAppearance::default_for_gender(is_male);

        // Apply saved model_info customization from Character Creation
        if !ch.model_info.trim().is_empty() {
            if let Ok(v) = serde_json::from_str::<Value>(&ch.model_info) {
                if let Some(h) = v["hair"].as_str().filter(|s| !s.is_empty()) {
                    app.hair = h.to_string();
                }
                if let Some(arr) = v["body_color"].as_array() {
                    for (i, c) in arr.iter().take(3).enumerate() {
                        if let Some(s) = c.as_str().filter(|s| !s.is_empty()) {
                            app.body_color[i] = s.trim_start_matches('#').to_string();
                        }
                    }
                }
                if let Some(arr) = v["head_color"].as_array() {
                    for (i, c) in arr.iter().take(3).enumerate() {
                        if let Some(s) = c.as_str().filter(|s| !s.is_empty()) {
                            app.head_color[i] = s.trim_start_matches('#').to_string();
                        }
                    }
                }
                if let Some(s) = v["skin_color"].as_str().filter(|s| !s.is_empty()) {
                    app.skin_color = s.trim_start_matches('#').to_string();
                }
                if let Some(s) = v["hair_color"].as_str().filter(|s| !s.is_empty()) {
                    app.hair_color = s.trim_start_matches('#').to_string();
                }
                if let Some(s) = v["eye_color"].as_str().filter(|s| !s.is_empty()) {
                    app.eye_color = s.trim_start_matches('#').to_string();
                }
                if let Some(s) = v["lip_color"].as_str().filter(|s| !s.is_empty()) {
                    app.lip_color = s.trim_start_matches('#').to_string();
                }
                if let Some(p) = v["portrait"].as_i64() {
                    app.portrait = p as i32;
                }
                if let Some(p) = v["portrait_bg"].as_i64() {
                    app.portrait_bg = p as i32;
                }
                if let Some(s) = v["portrait_bg_color"].as_str().filter(|s| !s.is_empty()) {
                    app.portrait_bg_color = Some(s.trim_start_matches('#').to_string());
                }
                if let Some(s) = v["beard"].as_str().filter(|s| !s.is_empty()) {
                    app.beard = Some(s.to_string());
                }
                if let Some(vt) = v["voice_type"].as_i64() {
                    app.voice_type = vt as i32;
                }
                if let Some(bs) = v["body_size"].as_f64() {
                    app.body_size = (bs as f32).clamp(0.0, 1.0);
                }
            }
        }

        // Resolve equipped body clothing & weapon framework for 3D rendering
        let equipped = self.db.list_equipped(&ch.entity_id).unwrap_or_default();
        let items = self.db.list_items(&ch.entity_id).unwrap_or_default();
        if let Some((_, body_item_id)) = equipped.iter().find(|(s, _)| s.eq_ignore_ascii_case("body")) {
            if let Some(item) = items.iter().find(|it| &it.item_id == body_item_id) {
                if let Some(path) = clothes_prototype_to_fbx(&item.prototype_id, is_male) {
                    app.body = path;
                }
            }
        }
        if let Some((_, weapon_item_id)) = equipped.iter().find(|(s, _)| {
            s.eq_ignore_ascii_case("main")
                || s.eq_ignore_ascii_case("both")
                || s.eq_ignore_ascii_case("weapon")
        }) {
            if let Some(item) = items.iter().find(|it| &it.item_id == weapon_item_id) {
                app.weapon_framework = self
                    .catalog
                    .weapon_framework_for(&item.prototype_id, item.level);
            }
        }
        app
    }

    pub async fn ensure_region_pois(&self, region_id: &str, terrain: &LoadedTerrain) {
        if self.seeded_poi_regions.read().await.contains(region_id) {
            return;
        }
        let existing = self.db.list_artifacts_in_region(region_id).unwrap_or_default();
        let existing_ids: HashSet<String> = existing.into_iter().map(|a| a.entity_id).collect();
        let now = Self::now_unix_secs();

        let poi_specs: [(&str, u16, &str, u16, u16, &[(u16, u16)]); 4] = [
            ("port", 7001, "dock", 3, 3, &terrain.pois.port_points),
            ("warphole", 9450, "neutral_warphole", 6, 6, &terrain.pois.warpholes),
            ("rift", 6282, "warp_accelerator", 2, 2, &terrain.pois.rifts),
            ("crater", 7037, "crack_01", 3, 3, &terrain.pois.craters),
        ];

        for (kind, entity_type, bp_id, default_sx, default_sy, coords) in poi_specs {
            let (sx, sy, height) = self
                .catalog
                .resolve_artifact_type(entity_type)
                .map(|s| (s.size_x, s.size_y, s.height))
                .unwrap_or((default_sx, default_sy, 1));
            for &(tx, ty) in coords {
                let eid = format!("poi-{kind}-{region_id}-{tx}-{ty}");
                if existing_ids.contains(&eid) {
                    continue;
                }
                let rec = ArtifactRecord {
                    entity_id: eid,
                    region_id: region_id.to_string(),
                    entity_type,
                    blueprint_id: bp_id.to_string(),
                    tile_x: tx,
                    tile_y: ty,
                    size_x: sx,
                    size_y: sy,
                    height,
                    rotation: 0,
                    building_state: durango_wire::BUILDING_STATE_COMPLETED,
                    founder_entity_id: "system".to_string(),
                    level: 1,
                    created_at: now,
                    durability: 100.0,
                    max_durability: 100.0,
                    last_decay_at: now,
                    gate_opened: false,
                    is_lit: false,
                    materials_json: "{}".to_string(),
                };
                let _ = self.db.upsert_artifact(&rec);
            }
        }
        self.seeded_poi_regions
            .write()
            .await
            .insert(region_id.to_string());
    }

    pub async fn ensure_region_wildlife(&self, region_id: &str, entry_tile: (u16, u16)) {
        {
            if self.seeded_animal_regions.read().await.contains(region_id) {
                return;
            }
        }
        let mut seeded = self.seeded_animal_regions.write().await;
        if !seeded.insert(region_id.to_string()) {
            return;
        }
        let terrain = self.terrains.get_or_load(region_id).await;
        let now = Self::now_unix_secs();
        let mut animals = self.animals.write().await;
        let mut used_tiles: std::collections::HashSet<(u16, u16)> = std::collections::HashSet::new();

        // Time-seeded PRNG so real islands get varied, non-static dinosaur species & placements
        let mut rng_state: u64 = ((now * 1_000_000.0) as u64) ^ 0xA5A5_D1A0_2026_1123;
        let mut next_rand = || -> u64 {
            rng_state = rng_state
                .wrapping_mul(6364136223846793005)
                .wrapping_add(1442695040888963407);
            rng_state >> 16
        };

        let max_tx = terrain.width.saturating_sub(3) as i32;
        let max_ty = terrain.height.saturating_sub(3) as i32;
        let find_dry_near = |base_x: u16,
                             base_y: u16,
                             jitter: i32,
                             used: &mut HashSet<(u16, u16)>,
                             rng: &mut dyn FnMut() -> u64|
         -> Option<(u16, u16)> {
            if jitter > 0 {
                let span = (jitter * 2 + 1) as u64;
                for _ in 0..12 {
                    let ox = (rng() % span) as i32 - jitter;
                    let oy = (rng() % span) as i32 - jitter;
                    let cx = (i32::from(base_x) + ox).clamp(2, max_tx) as u16;
                    let cy = (i32::from(base_y) + oy).clamp(2, max_ty) as u16;
                    if terrain.is_safe_inland_land(cx, cy) && !used.contains(&(cx, cy)) {
                        used.insert((cx, cy));
                        return Some((cx, cy));
                    }
                }
            }
            if terrain.is_safe_inland_land(base_x, base_y) && !used.contains(&(base_x, base_y)) {
                used.insert((base_x, base_y));
                return Some((base_x, base_y));
            }
            for r in 1..=40i32 {
                for ox in -r..=r {
                    for oy in -r..=r {
                        if ox.abs() != r && oy.abs() != r {
                            continue;
                        }
                        let cx = (i32::from(base_x) + ox).clamp(2, max_tx) as u16;
                        let cy = (i32::from(base_y) + oy).clamp(2, max_ty) as u16;
                        if terrain.is_safe_inland_land(cx, cy) && !used.contains(&(cx, cy)) {
                            used.insert((cx, cy));
                            return Some((cx, cy));
                        }
                    }
                }
            }
            None
        };

        // Build spawn plan:
        // - Headless/fallback terrains (no herds.yml): 3 deterministic nearby dinosaurs for unit tests
        // - Real islands with herds.yml: varied herds strictly on verified dry inland tiles (never deep ocean!)
        let mut spawn_plan: Vec<(crate::catalog::AnimalSpec, u16, u16, i32)> = Vec::new();
        if terrain.herds.is_empty() {
            let defaults: [(u16, i16, i16, i32); 3] = [
                (2048, 4, 2, 2),
                (2051, 6, -1, 3),
                (2056, 3, 5, 4),
            ];
            for (et, dx, dy, level) in defaults {
                let spec = self.catalog.resolve_animal(et);
                let bx = (i32::from(entry_tile.0) + i32::from(dx)).clamp(2, max_tx) as u16;
                let by = (i32::from(entry_tile.1) + i32::from(dy)).clamp(2, max_ty) as u16;
                if let Some((tx, ty)) = find_dry_near(bx, by, 0, &mut used_tiles, &mut next_rand) {
                    spawn_plan.push((spec, tx, ty, level));
                }
            }
        } else {
            let is_safe_tamed_island = region_id.starts_with("tamed-")
                || region_id.starts_with("civilized-")
                || region_id.starts_with("pe")
                || region_id.starts_with("tr")
                || region_id.starts_with("s30")
                || region_id.starts_with("ur")
                || (region_id.starts_with('c') && !region_id.starts_with("cl") && !region_id.starts_with("clan-"));
            let (_, _, parsed_lv, _, _, _) = parse_region_parts(region_id);
            let island_lv = if region_id.contains("60") {
                60
            } else if region_id.contains("55") {
                55
            } else if region_id.contains("50") {
                50
            } else if region_id.contains("45") {
                45
            } else if region_id.contains("40") {
                40
            } else if region_id.contains("35") {
                35
            } else if region_id.contains("30") {
                30
            } else if region_id.contains("25") {
                25
            } else if region_id.contains("20") {
                20
            } else if region_id.contains("18") {
                18
            } else if region_id.contains("15") {
                15
            } else if region_id.contains("10") || is_safe_tamed_island {
                10
            } else {
                (parsed_lv as i32).clamp(10, 60)
            };
            let (peaceful_pool, aggro_pool) =
                self.catalog.island_wildlife_pool(island_lv, is_safe_tamed_island);
            // Strictly filter herd points to verified inland dry land (excludes ocean/beach water!)
            let mut herd_pts: Vec<(u16, u16)> = terrain
                .herds
                .land
                .iter()
                .chain(terrain.herds.beach.iter())
                .copied()
                .filter(|&(hx, hy)| terrain.is_safe_inland_land(hx, hy))
                .collect();
            // Shuffle herd points with Fisher-Yates so every fresh island has different placements
            for i in (1..herd_pts.len()).rev() {
                let j = (next_rand() as usize) % (i + 1);
                herd_pts.swap(i, j);
            }
            // Seed up to 18 varied dinosaurs across real dry-land herds.yml points
            for (idx, (hx, hy)) in herd_pts.into_iter().take(18).enumerate() {
                let want_aggro = !is_safe_tamed_island && idx % 3 == 2 && !aggro_pool.is_empty();
                let spec = if want_aggro {
                    aggro_pool[(next_rand() as usize) % aggro_pool.len()].clone()
                } else if !peaceful_pool.is_empty() {
                    peaceful_pool[(next_rand() as usize) % peaceful_pool.len()].clone()
                } else {
                    self.catalog.resolve_animal(2015)
                };
                let max_cap = if is_safe_tamed_island { 8 } else { island_lv };
                let min_lv = spec.min_level.min(max_cap);
                let lv_span = (spec.max_level.min(max_cap) - min_lv).max(0) as u64 + 1;
                let level = min_lv + (next_rand() % lv_span) as i32;
                if let Some((tx, ty)) = find_dry_near(hx, hy, 4, &mut used_tiles, &mut next_rand) {
                    spawn_plan.push((spec, tx, ty, level.clamp(1, 60)));
                }
            }
            // Anchor 3 nearby starter dinosaurs strictly on DRY INLAND meadow closest to the Harbor
            // (never on ocean tiles outside the playable shoreline!)
            if let Some(inland_anchor) =
                find_dry_near(entry_tile.0, entry_tile.1, 0, &mut HashSet::new(), &mut next_rand)
            {
                let nearby_specs: [(bool, i16, i16); 3] = [
                    (false, 4, 2),
                    (false, 8, -4),
                    (!is_safe_tamed_island, 12, 6),
                ];
                for (want_aggro, dx, dy) in nearby_specs {
                    let spec = if want_aggro && !aggro_pool.is_empty() {
                        aggro_pool[(next_rand() as usize) % aggro_pool.len()].clone()
                    } else if !peaceful_pool.is_empty() {
                        peaceful_pool[(next_rand() as usize) % peaceful_pool.len()].clone()
                    } else {
                        self.catalog.resolve_animal(if want_aggro { 2001 } else { 2015 })
                    };
                    let bx = (i32::from(inland_anchor.0) + i32::from(dx)).clamp(2, max_tx) as u16;
                    let by = (i32::from(inland_anchor.1) + i32::from(dy)).clamp(2, max_ty) as u16;
                    if let Some((tx, ty)) = find_dry_near(bx, by, 3, &mut used_tiles, &mut next_rand) {
                        let max_cap = if is_safe_tamed_island { 6 } else { island_lv };
                        let lv = (spec.min_level.min(max_cap) + (next_rand() % 4) as i32).clamp(1, max_cap);
                        spawn_plan.push((spec, tx, ty, lv));
                    }
                }
            }
        }

        for (idx, (spec, tile_x, tile_y, level)) in spawn_plan.into_iter().enumerate() {
            let wx = f32::from(tile_x) * 200.0;
            let wy = f32::from(tile_y) * 200.0;
            let yaw = ((next_rand() % 360) as f32).rem_euclid(360.0);
            let id = format!("wild-{region_id}-{idx}");
            animals.insert(
                id.clone(),
                LiveAnimal {
                    entity_id: id,
                    region_id: region_id.to_string(),
                    entity_type: spec.entity_type,
                    name: spec.name,
                    level,
                    hp: spec.max_hp,
                    max_hp: spec.max_hp,
                    home_x: wx,
                    home_y: wy,
                    pos_x: wx,
                    pos_y: wy,
                    walk_from_x: wx,
                    walk_from_y: wy,
                    walk_start_at: 0.0,
                    walk_end_at: 0.0,
                    yaw,
                    base_scale: spec.base_scale,
                    size_level: spec.size_level,
                    is_aggressive: spec.is_aggressive,
                    attack_cooltime: spec.attack_cooltime,
                    stand_motion: spec.stand_motion,
                    move_motion: spec.move_motion,
                    dead_motion: spec.dead_motion,
                    battle_stand_motion: spec.battle_stand_motion,
                    attack_motion: spec.attack_motion,
                    attacks: spec.attacks,
                    attack_damage: spec.attack_damage,
                    next_wander_at: now + 0.8 + ((idx % 6) as f64) * 0.6,
                    stop_walking_at: 0.0,
                    stand_at: 0.0,
                    threat_telegraph_until: 0.0,
                    aggro_seen_at: 0.0,
                    next_attack_at: 0.0,
                    respawn_at: 0.0,
                    is_alive: true,
                    butchered: false,
                    butcher_stock: HashMap::new(),
                    enemy_id: None,
                },
            );
        }
    }

    pub async fn list_animals_in_region(&self, region_id: &str) -> Vec<LiveAnimal> {
        let animals = self.animals.read().await;
        let mut out: Vec<LiveAnimal> = animals
            .values()
            .filter(|a| a.region_id == region_id && !a.butchered)
            .cloned()
            .collect();
        out.sort_by(|a, b| a.entity_id.cmp(&b.entity_id));
        out
    }
}

fn clothes_prototype_to_fbx(proto: &str, is_male: bool) -> Option<String> {
    let (dir, pfx) = if is_male {
        ("Male", "m")
    } else {
        ("Female", "f")
    };
    let suffix = match proto {
        "clothes_engineer" => "engineer",
        "clothes_officeworker" => "officelook",
        "clothes_student" => "school",
        "clothes_farmer" => "farmer",
        "clothes_waiter" => "waiter",
        "clothes_soldier" => "soldier",
        "clothes_homeworker" | "clothes_jobless" => "hoody",
        _ => return None,
    };
    Some(format!("Models/PC/{dir}/Body/{pfx}_body_{suffix}.FBX"))
}

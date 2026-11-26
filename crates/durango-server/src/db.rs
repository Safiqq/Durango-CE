use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{mpsc, Mutex, MutexGuard};

use flate2::write::GzEncoder;
use flate2::Compression;
use rusqlite::{params, Connection, OptionalExtension};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CharacterRecord {
    pub entity_id: String,
    pub owner_key: String,
    pub name: String,
    pub job: i32,
    pub level: i32,
    pub region_id: String,
    pub terrain_id: String,
    pub pos_x: f32,
    pub pos_y: f32,
    pub disconnected_at: f64,
    pub created_at: f64,
    pub gender: String,
    pub model_info: String,
    pub exp: i32,
    pub untrained_count: i32,
    pub life: f32,
    pub stamina: f32,
    pub energy: f32,
    pub fatigue: f32,
}

#[derive(Debug, Clone)]
pub struct LearnedSkillRecord {
    pub category: i32,
    pub skill_id: String,
    pub sub_id: String,
    pub level: i32,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ItemRecord {
    pub item_id: String,
    pub entity_id: String,
    pub prototype_id: String,
    pub name: String,
    pub icon: String,
    pub level: i32,
    pub size: i32,
    pub durability: f32,
    pub max_durability: f32,
    pub locked: bool,
    pub collectible_id: Option<String>,
    pub generator_id: Option<String>,
    pub created_at: f64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ArtifactRecord {
    pub entity_id: String,
    pub region_id: String,
    pub entity_type: u16,
    pub blueprint_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub size_x: u16,
    pub size_y: u16,
    pub height: i32,
    pub rotation: i32,
    pub building_state: i32,
    pub founder_entity_id: String,
    pub level: u8,
    pub created_at: f64,
    pub durability: f32,
    pub max_durability: f32,
    pub last_decay_at: f64,
    pub gate_opened: bool,
    pub is_lit: bool,
    pub materials_json: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MarketProductRecord {
    pub product_id: String,
    pub seller_entity_id: String,
    pub region_id: String,
    pub item: ItemRecord,
    pub price: i64,
    pub listed_at: f64,
    pub expires_at: f64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct EstateRecord {
    pub estate_id: String,
    pub owner_type: i32,
    pub owner_id: String,
    pub region_id: String,
    pub anchor_x: u16,
    pub anchor_y: u16,
    pub activated_at: f64,
    pub expires_at: f64,
    pub access_for_others: i32,
    pub cells: Vec<(u8, u8)>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExploredPoiRecord {
    pub region_id: String,
    pub tile_x: u16,
    pub tile_y: u16,
    pub poi_type: i32,
    pub icon: String,
    pub title: String,
    pub entity_type: Option<u16>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MailRecord {
    pub mail_id: String,
    pub recipient_entity_id: String,
    pub sender_entity_id: String,
    pub is_user_mail: bool,
    pub mail_type: i32,
    pub text: String,
    pub t_stone: i32,
    pub items: Vec<ItemRecord>,
    pub accepted: bool,
    pub read: bool,
    pub sent_at: f64,
    pub expires_at: Option<f64>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MarketSaleRecord {
    pub product_id: String,
    pub seller_entity_id: String,
    pub buyer_entity_id: String,
    pub region_id: String,
    pub item: ItemRecord,
    pub price: i64,
    pub sold_at: f64,
    pub payment_collected: bool,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct PlayerPointsRecord {
    pub home_artifact_id: Option<String>,
    pub returning_x: Option<u16>,
    pub returning_y: Option<u16>,
    pub returning_region_id: Option<String>,
    pub personal_region_id: Option<String>,
    pub personal_region_template_id: Option<String>,
    pub admission_categories: Vec<i32>,
}

#[derive(Debug, Clone)]
pub struct SessionRecord {
    pub token: String,
    pub owner_key: String,
    pub entity_id: String,
    pub password_ok: bool,
    pub created_at: f64,
}

const AUDIT_HOT_MAX_ROWS: i64 = 5_000;
const AUDIT_ROTATE_BYTES: u64 = 5 * 1024 * 1024; // 5 MiB uncompressed (~400 KiB gzipped)
const AUDIT_MAX_ARCHIVES: usize = 14;

type AuditFileEvent = (f64, String, String, String, String, String);

pub struct Database {
    conn: Mutex<Connection>,
    audit_log_dir: Option<PathBuf>,
    audit_tx: Option<mpsc::SyncSender<AuditFileEvent>>,
    audit_insert_count: AtomicU64,
}

fn escape_like_pattern(raw: &str) -> String {
    let mut out = String::with_capacity(raw.len());
    for ch in raw.chars() {
        match ch {
            '\\' | '%' | '_' => {
                out.push('\\');
                out.push(ch);
            }
            _ => out.push(ch),
        }
    }
    out
}

fn ensure_column(
    conn: &Connection,
    table: &str,
    column: &str,
    col_def: &str,
) -> Result<(), rusqlite::Error> {
    let pragma = format!("PRAGMA table_info({table})");
    let mut stmt = conn.prepare(&pragma)?;
    let exists = stmt
        .query_map([], |row| row.get::<_, String>(1))?
        .filter_map(Result::ok)
        .any(|name| name.eq_ignore_ascii_case(column));
    if !exists {
        let alter = format!("ALTER TABLE {table} ADD COLUMN {column} {col_def}");
        conn.execute(&alter, [])?;
    }
    Ok(())
}

impl Database {
    #[inline]
    pub fn lock_conn(&self) -> MutexGuard<'_, Connection> {
        self.conn.lock().unwrap_or_else(|e| e.into_inner())
    }

    pub fn open(path: Option<&Path>) -> Result<Self, rusqlite::Error> {
        let mut audit_log_dir: Option<PathBuf> = None;
        let conn = match path {
            Some(p) => {
                if let Some(parent) = p.parent() {
                    if !parent.as_os_str().is_empty() {
                        let _ = std::fs::create_dir_all(parent);
                        let logs_dir = parent.join("logs");
                        let _ = std::fs::create_dir_all(&logs_dir);
                        audit_log_dir = Some(logs_dir);
                    }
                }
                Connection::open(p)?
            }
            None => Connection::open_in_memory()?,
        };
        conn.execute_batch(
            "PRAGMA journal_mode = WAL;
             PRAGMA synchronous = NORMAL;
             PRAGMA busy_timeout = 5000;
             PRAGMA temp_store = MEMORY;
             PRAGMA cache_size = -16000;
             PRAGMA foreign_keys = ON;
             CREATE TABLE IF NOT EXISTS characters (
                 entity_id TEXT PRIMARY KEY,
                 owner_key TEXT NOT NULL,
                 name TEXT NOT NULL,
                 job INTEGER NOT NULL DEFAULT 0,
                 level INTEGER NOT NULL DEFAULT 1,
                 region_id TEXT NOT NULL,
                 terrain_id TEXT NOT NULL,
                 pos_x REAL NOT NULL DEFAULT 63.0,
                 pos_y REAL NOT NULL DEFAULT 71.0,
                 disconnected_at REAL NOT NULL DEFAULT 0.0,
                 created_at REAL NOT NULL DEFAULT 0.0,
                 gender TEXT NOT NULL DEFAULT 'male',
                 model_info TEXT NOT NULL DEFAULT '',
                 exp INTEGER NOT NULL DEFAULT 0,
                 untrained_count INTEGER NOT NULL DEFAULT 0,
                 last_ip TEXT NOT NULL DEFAULT '',
                 life REAL NOT NULL DEFAULT 100.0,
                 stamina REAL NOT NULL DEFAULT 100.0,
                 energy REAL NOT NULL DEFAULT 100.0,
                 fatigue REAL NOT NULL DEFAULT 0.0,
                 t_stone INTEGER NOT NULL DEFAULT 50000
             );
             CREATE INDEX IF NOT EXISTS idx_characters_owner ON characters(owner_key);
             CREATE INDEX IF NOT EXISTS idx_characters_name ON characters(name COLLATE NOCASE);
             CREATE TABLE IF NOT EXISTS sessions (
                 token TEXT PRIMARY KEY,
                 owner_key TEXT NOT NULL,
                 entity_id TEXT NOT NULL,
                 password_ok INTEGER NOT NULL DEFAULT 1,
                 created_at REAL NOT NULL
             );
             CREATE INDEX IF NOT EXISTS idx_sessions_owner ON sessions(owner_key);
             CREATE TABLE IF NOT EXISTS inventory_items (
                 item_id TEXT PRIMARY KEY,
                 entity_id TEXT NOT NULL,
                 prototype_id TEXT NOT NULL,
                 name TEXT NOT NULL,
                 icon TEXT NOT NULL,
                 level INTEGER NOT NULL DEFAULT 1,
                 size INTEGER NOT NULL DEFAULT 1,
                 durability REAL NOT NULL DEFAULT 100.0,
                 max_durability REAL NOT NULL DEFAULT 100.0,
                 locked INTEGER NOT NULL DEFAULT 0,
                 collectible_id TEXT,
                 generator_id TEXT,
                 created_at REAL NOT NULL DEFAULT 0.0,
                 slot_order INTEGER NOT NULL DEFAULT 0
             );
             CREATE INDEX IF NOT EXISTS idx_inventory_entity ON inventory_items(entity_id);
             CREATE TABLE IF NOT EXISTS equipped_items (
                 entity_id TEXT NOT NULL,
                 slot_name TEXT NOT NULL,
                 item_id TEXT NOT NULL,
                 PRIMARY KEY (entity_id, slot_name)
             );
             CREATE INDEX IF NOT EXISTS idx_equipped_item ON equipped_items(entity_id, item_id);
             CREATE TABLE IF NOT EXISTS removed_naturals (
                 region_id TEXT NOT NULL,
                 tile_x INTEGER NOT NULL,
                 tile_y INTEGER NOT NULL,
                 PRIMARY KEY (region_id, tile_x, tile_y)
             );
             CREATE TABLE IF NOT EXISTS artifacts (
                 entity_id TEXT PRIMARY KEY,
                 region_id TEXT NOT NULL,
                 entity_type INTEGER NOT NULL,
                 blueprint_id TEXT NOT NULL,
                 tile_x INTEGER NOT NULL,
                 tile_y INTEGER NOT NULL,
                 size_x INTEGER NOT NULL DEFAULT 1,
                 size_y INTEGER NOT NULL DEFAULT 1,
                 height INTEGER NOT NULL DEFAULT 1,
                 rotation INTEGER NOT NULL DEFAULT 0,
                 building_state INTEGER NOT NULL DEFAULT 0,
                 founder_entity_id TEXT NOT NULL,
                 level INTEGER NOT NULL DEFAULT 1,
                 created_at REAL NOT NULL DEFAULT 0.0,
                 durability REAL NOT NULL DEFAULT 100.0,
                 max_durability REAL NOT NULL DEFAULT 100.0,
                 last_decay_at REAL NOT NULL DEFAULT 0.0,
                 gate_opened INTEGER NOT NULL DEFAULT 0,
                 is_lit INTEGER NOT NULL DEFAULT 0,
                 materials_json TEXT NOT NULL DEFAULT '{}'
             );
             CREATE INDEX IF NOT EXISTS idx_artifacts_region ON artifacts(region_id);
             CREATE TABLE IF NOT EXISTS market_products (
                 product_id TEXT PRIMARY KEY,
                 seller_entity_id TEXT NOT NULL,
                 region_id TEXT NOT NULL,
                 item_json TEXT NOT NULL,
                 prototype_id TEXT NOT NULL,
                 item_name TEXT NOT NULL,
                 item_level INTEGER NOT NULL DEFAULT 1,
                 price INTEGER NOT NULL DEFAULT 0,
                 listed_at REAL NOT NULL DEFAULT 0.0,
                 expires_at REAL NOT NULL DEFAULT 0.0
             );
             CREATE INDEX IF NOT EXISTS idx_market_expires_listed ON market_products(expires_at, listed_at DESC);
             CREATE INDEX IF NOT EXISTS idx_market_proto ON market_products(prototype_id COLLATE NOCASE);
             CREATE TABLE IF NOT EXISTS clans (
                 clan_id TEXT PRIMARY KEY,
                 name TEXT NOT NULL UNIQUE,
                 leader_entity_id TEXT NOT NULL,
                 created_at REAL NOT NULL DEFAULT 0.0
             );
             CREATE TABLE IF NOT EXISTS clan_members (
                 entity_id TEXT PRIMARY KEY,
                 clan_id TEXT NOT NULL,
                 role_id INTEGER NOT NULL DEFAULT 1,
                 joined_at REAL NOT NULL DEFAULT 0.0
             );
             CREATE INDEX IF NOT EXISTS idx_clan_members_clan ON clan_members(clan_id);
             CREATE TABLE IF NOT EXISTS player_skills (
                 entity_id TEXT NOT NULL,
                 category INTEGER NOT NULL,
                 skill_id TEXT NOT NULL,
                 sub_id TEXT NOT NULL,
                 level INTEGER NOT NULL DEFAULT 1,
                 PRIMARY KEY (entity_id, skill_id, sub_id)
             );
             CREATE INDEX IF NOT EXISTS idx_player_skills_entity ON player_skills(entity_id);
             CREATE TABLE IF NOT EXISTS player_skill_categories (
                 entity_id TEXT NOT NULL,
                 category INTEGER NOT NULL,
                 level INTEGER NOT NULL DEFAULT 1,
                 exp INTEGER NOT NULL DEFAULT 0,
                 PRIMARY KEY (entity_id, category)
             );
             CREATE TABLE IF NOT EXISTS player_storage (
                 entity_id TEXT NOT NULL,
                 key TEXT NOT NULL,
                 value BLOB NOT NULL,
                 PRIMARY KEY (entity_id, key)
             );
             CREATE TABLE IF NOT EXISTS player_points (
                 entity_id TEXT PRIMARY KEY,
                 home_artifact_id TEXT,
                 returning_x INTEGER,
                 returning_y INTEGER,
                 returning_region_id TEXT,
                 personal_region_id TEXT,
                 personal_region_template_id TEXT,
                 admission_csv TEXT NOT NULL DEFAULT ''
             );
             CREATE TABLE IF NOT EXISTS estates (
                 estate_id TEXT PRIMARY KEY,
                 owner_type INTEGER NOT NULL,
                 owner_id TEXT NOT NULL,
                 region_id TEXT NOT NULL,
                 anchor_x INTEGER NOT NULL,
                 anchor_y INTEGER NOT NULL,
                 activated_at REAL NOT NULL DEFAULT 0.0,
                 expires_at REAL NOT NULL DEFAULT 0.0,
                 access_for_others INTEGER NOT NULL DEFAULT 1
             );
             CREATE INDEX IF NOT EXISTS idx_estates_owner ON estates(owner_id);
             CREATE INDEX IF NOT EXISTS idx_estates_region ON estates(region_id);
             CREATE TABLE IF NOT EXISTS estate_cells (
                 region_id TEXT NOT NULL,
                 cell_x INTEGER NOT NULL,
                 cell_y INTEGER NOT NULL,
                 estate_id TEXT NOT NULL,
                 PRIMARY KEY (region_id, cell_x, cell_y)
             );
             CREATE INDEX IF NOT EXISTS idx_estate_cells_estate ON estate_cells(estate_id);
             CREATE TABLE IF NOT EXISTS explored_pois (
                 entity_id TEXT NOT NULL,
                 region_id TEXT NOT NULL,
                 tile_x INTEGER NOT NULL,
                 tile_y INTEGER NOT NULL,
                 poi_type INTEGER NOT NULL,
                 icon TEXT NOT NULL DEFAULT '',
                 title TEXT NOT NULL DEFAULT '',
                 entity_type INTEGER,
                 PRIMARY KEY (entity_id, region_id, tile_x, tile_y)
             );
             CREATE INDEX IF NOT EXISTS idx_explored_pois_region ON explored_pois(entity_id, region_id);
             CREATE TABLE IF NOT EXISTS container_items (
                 item_id TEXT PRIMARY KEY,
                 container_id TEXT NOT NULL,
                 section_name TEXT NOT NULL DEFAULT 'default',
                 item_json TEXT NOT NULL,
                 slot_order INTEGER NOT NULL DEFAULT 0
             );
             CREATE INDEX IF NOT EXISTS idx_container_items_owner ON container_items(container_id, section_name);
             CREATE TABLE IF NOT EXISTS warehouse_sections (
                 container_id TEXT NOT NULL,
                 section_name TEXT NOT NULL,
                 sort_order INTEGER NOT NULL DEFAULT 0,
                 PRIMARY KEY (container_id, section_name)
             );
             CREATE TABLE IF NOT EXISTS market_sales (
                 product_id TEXT PRIMARY KEY,
                 seller_entity_id TEXT NOT NULL,
                 buyer_entity_id TEXT NOT NULL,
                 region_id TEXT NOT NULL,
                 item_json TEXT NOT NULL,
                 price INTEGER NOT NULL DEFAULT 0,
                 sold_at REAL NOT NULL DEFAULT 0.0,
                 payment_collected INTEGER NOT NULL DEFAULT 0
             );
             CREATE INDEX IF NOT EXISTS idx_market_sales_seller ON market_sales(seller_entity_id, payment_collected);
             CREATE INDEX IF NOT EXISTS idx_market_sales_buyer ON market_sales(buyer_entity_id);
             CREATE TABLE IF NOT EXISTS market_favorites (
                 entity_id TEXT NOT NULL,
                 product_id TEXT NOT NULL,
                 PRIMARY KEY (entity_id, product_id)
             );
             CREATE TABLE IF NOT EXISTS mails (
                 mail_id TEXT PRIMARY KEY,
                 recipient_entity_id TEXT NOT NULL,
                 sender_entity_id TEXT NOT NULL,
                 is_user_mail INTEGER NOT NULL DEFAULT 1,
                 mail_type INTEGER NOT NULL DEFAULT 0,
                 text TEXT NOT NULL DEFAULT '',
                 t_stone INTEGER NOT NULL DEFAULT 0,
                 items_json TEXT NOT NULL DEFAULT '[]',
                 accepted INTEGER NOT NULL DEFAULT 0,
                 is_read INTEGER NOT NULL DEFAULT 0,
                 sent_at REAL NOT NULL DEFAULT 0.0,
                 expires_at REAL
             );
             CREATE INDEX IF NOT EXISTS idx_mails_recipient ON mails(recipient_entity_id, sent_at DESC);
             CREATE TABLE IF NOT EXISTS bans (
                 id INTEGER PRIMARY KEY AUTOINCREMENT,
                 ban_type TEXT NOT NULL,
                 target_value TEXT NOT NULL,
                 reason TEXT NOT NULL DEFAULT '',
                 banned_by TEXT NOT NULL DEFAULT 'admin',
                 created_at REAL NOT NULL DEFAULT 0.0,
                 expires_at REAL
             );
             CREATE INDEX IF NOT EXISTS idx_bans_lookup ON bans(ban_type, target_value);
             CREATE TABLE IF NOT EXISTS server_settings (
                 key TEXT PRIMARY KEY,
                 value TEXT NOT NULL,
                 updated_at REAL NOT NULL DEFAULT 0.0
             );
             CREATE TABLE IF NOT EXISTS audit_logs (
                 id INTEGER PRIMARY KEY AUTOINCREMENT,
                 ts REAL NOT NULL,
                 category TEXT NOT NULL,
                 actor TEXT NOT NULL DEFAULT '',
                 actor_ip TEXT NOT NULL DEFAULT '',
                 region_id TEXT NOT NULL DEFAULT '',
                 summary TEXT NOT NULL
             );
             CREATE INDEX IF NOT EXISTS idx_audit_logs_ts ON audit_logs(ts DESC);
             CREATE INDEX IF NOT EXISTS idx_audit_logs_cat ON audit_logs(category, id DESC);",
        )?;
        // Forward-compatible migrations for existing DBs
        for (table, col, def) in [
            ("inventory_items", "slot_order", "INTEGER NOT NULL DEFAULT 0"),
            ("characters", "gender", "TEXT NOT NULL DEFAULT 'male'"),
            ("characters", "model_info", "TEXT NOT NULL DEFAULT ''"),
            ("characters", "exp", "INTEGER NOT NULL DEFAULT 0"),
            ("characters", "untrained_count", "INTEGER NOT NULL DEFAULT 0"),
            ("characters", "last_ip", "TEXT NOT NULL DEFAULT ''"),
            ("characters", "life", "REAL NOT NULL DEFAULT 100.0"),
            ("characters", "stamina", "REAL NOT NULL DEFAULT 100.0"),
            ("characters", "energy", "REAL NOT NULL DEFAULT 100.0"),
            ("characters", "fatigue", "REAL NOT NULL DEFAULT 0.0"),
            ("characters", "t_stone", "INTEGER NOT NULL DEFAULT 50000"),
            ("artifacts", "durability", "REAL NOT NULL DEFAULT 100.0"),
            ("artifacts", "max_durability", "REAL NOT NULL DEFAULT 100.0"),
            ("artifacts", "last_decay_at", "REAL NOT NULL DEFAULT 0.0"),
            ("artifacts", "gate_opened", "INTEGER NOT NULL DEFAULT 0"),
            ("artifacts", "is_lit", "INTEGER NOT NULL DEFAULT 0"),
            ("artifacts", "materials_json", "TEXT NOT NULL DEFAULT '{}'"),
            ("player_points", "returning_region_id", "TEXT"),
        ] {
            ensure_column(&conn, table, col, def)?;
        }
        Self::purge_legacy_identifiers(&conn);
        let _ = conn.execute(
            "UPDATE inventory_items
             SET prototype_id = 'bone_leg', name = 'Leg Bone', icon = 'icon_nat_bone'
             WHERE prototype_id = 'bone' AND icon = 'icon_nat_wood_log'",
            [],
        );
        let _ = conn.execute(
            "UPDATE inventory_items
             SET prototype_id = 'stone', icon = 'icon_nat_mine_stone'
             WHERE icon IN ('icon_nat_stone', 'icon_nat_stone_pebble')",
            [],
        );
        let _ = conn.execute(
            "UPDATE inventory_items
             SET prototype_id = 'wildberry', name = 'Wild Berry', icon = 'icon_nat_fruit_wildberry'
             WHERE icon = 'icon_nat_fruit'",
            [],
        );
        let _ = conn.execute(
            "UPDATE inventory_items
             SET icon = 'blade_stone'
             WHERE icon = 'icon_tool_knife'",
            [],
        );

        let audit_tx = if let Some(dir) = audit_log_dir.clone() {
            let (tx, rx) = mpsc::sync_channel::<AuditFileEvent>(2048);
            let _ = std::thread::Builder::new()
                .name("durango-audit-writer".into())
                .spawn(move || {
                    for (ts, category, actor, actor_ip, region_id, summary) in rx {
                        let active_path = dir.join("audit.jsonl");
                        if let Ok(line) = serde_json::to_string(&serde_json::json!({
                            "ts": ts,
                            "category": category,
                            "actor": actor,
                            "actor_ip": actor_ip,
                            "region_id": region_id,
                            "summary": summary,
                        })) {
                            if let Ok(mut f) =
                                OpenOptions::new().create(true).append(true).open(&active_path)
                            {
                                let _ = writeln!(f, "{line}");
                            }
                            if let Ok(meta) = fs::metadata(&active_path) {
                                if meta.len() >= AUDIT_ROTATE_BYTES {
                                    Self::rotate_and_gzip_audit(&dir, &active_path, ts as u64);
                                }
                            }
                        }
                    }
                });
            Some(tx)
        } else {
            None
        };

        Ok(Self {
            conn: Mutex::new(conn),
            audit_log_dir,
            audit_tx,
            audit_insert_count: AtomicU64::new(0),
        })
    }

    /// Removes any pre-standardization rows still using legacy non-`usr_<24-hex>` account keys,
    /// non-hyphenated legacy terrain codes as `region_id`, or orphaned child rows.
    fn purge_legacy_identifiers(conn: &Connection) {
        let _ = conn.execute(
            "DELETE FROM characters WHERE owner_key NOT LIKE 'usr_%' OR LENGTH(owner_key) < 28",
            [],
        );
        let _ = conn.execute(
            "DELETE FROM bans WHERE ban_type IN ('account', 'mute') AND (target_value NOT LIKE 'usr_%' OR LENGTH(target_value) < 28)",
            [],
        );
        let _ = conn.execute(
            "DELETE FROM sessions WHERE owner_key NOT LIKE 'usr_%' OR LENGTH(owner_key) < 28",
            [],
        );
        for (table, col) in [
            ("characters", "region_id"),
            ("artifacts", "region_id"),
            ("estates", "region_id"),
            ("explored_pois", "region_id"),
            ("removed_naturals", "region_id"),
            ("market_products", "region_id"),
            ("market_sales", "region_id"),
            ("player_points", "personal_region_id"),
            ("audit_logs", "region_id"),
        ] {
            let sql = format!(
                "DELETE FROM {table} WHERE {col} IS NOT NULL AND {col} != '' AND {col} NOT LIKE '%-%'"
            );
            let _ = conn.execute(&sql, []);
        }
        // Clean orphaned estate cells and character child records after purge
        let _ = conn.execute(
            "DELETE FROM estate_cells WHERE estate_id NOT IN (SELECT estate_id FROM estates)",
            [],
        );
        let _ = conn.execute(
            "DELETE FROM sessions WHERE entity_id NOT IN (SELECT entity_id FROM characters)",
            [],
        );
        for (table, col) in [
            ("inventory_items", "entity_id"),
            ("equipped_items", "entity_id"),
            ("player_skills", "entity_id"),
            ("player_skill_categories", "entity_id"),
            ("player_storage", "entity_id"),
            ("player_points", "entity_id"),
            ("explored_pois", "entity_id"),
            ("clan_members", "entity_id"),
            ("market_favorites", "entity_id"),
        ] {
            let sql = format!(
                "DELETE FROM {table} WHERE {col} NOT IN (SELECT entity_id FROM characters)"
            );
            let _ = conn.execute(&sql, []);
        }
    }

    pub fn upsert_character(&self, rec: &CharacterRecord) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO characters (
                 entity_id, owner_key, name, job, level, region_id, terrain_id,
                 pos_x, pos_y, disconnected_at, created_at, gender, model_info,
                 exp, untrained_count, life, stamina, energy, fatigue
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19)
             ON CONFLICT(entity_id) DO UPDATE SET
                 name = excluded.name,
                 job = excluded.job,
                 level = excluded.level,
                 region_id = excluded.region_id,
                 terrain_id = excluded.terrain_id,
                 pos_x = excluded.pos_x,
                 pos_y = excluded.pos_y,
                 disconnected_at = excluded.disconnected_at,
                 gender = excluded.gender,
                 model_info = excluded.model_info,
                 exp = excluded.exp,
                 untrained_count = excluded.untrained_count,
                 life = excluded.life,
                 stamina = excluded.stamina,
                 energy = excluded.energy,
                 fatigue = excluded.fatigue",
            params![
                rec.entity_id,
                rec.owner_key,
                rec.name,
                rec.job,
                rec.level,
                rec.region_id,
                rec.terrain_id,
                rec.pos_x,
                rec.pos_y,
                rec.disconnected_at,
                rec.created_at,
                rec.gender,
                rec.model_info,
                rec.exp,
                rec.untrained_count,
                f64::from(rec.life),
                f64::from(rec.stamina),
                f64::from(rec.energy),
                f64::from(rec.fatigue),
            ],
        )?;
        Ok(())
    }

    pub fn count_characters(&self) -> Result<usize, rusqlite::Error> {
        let conn = self.lock_conn();
        let n: i64 = conn.query_row("SELECT COUNT(*) FROM characters", [], |r| r.get(0))?;
        Ok(n.max(0) as usize)
    }

    pub fn character_name_exists(&self, name: &str) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n: i64 = conn.query_row(
            "SELECT COUNT(*) FROM characters WHERE name = ?1 COLLATE NOCASE",
            params![name.trim()],
            |r| r.get(0),
        )?;
        Ok(n > 0)
    }

    #[allow(clippy::too_many_arguments)]
    pub fn flush_character_vitals(
        &self,
        entity_id: &str,
        pos_x: f32,
        pos_y: f32,
        life: f32,
        stamina: f32,
        energy: f32,
        fatigue: f32,
        disconnected_at: f64,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters
             SET pos_x = ?1, pos_y = ?2, life = ?3, stamina = ?4, energy = ?5, fatigue = ?6, disconnected_at = ?7
             WHERE entity_id = ?8",
            params![
                pos_x,
                pos_y,
                f64::from(life.clamp(0.0, 100.0)),
                f64::from(stamina.clamp(0.0, 100.0)),
                f64::from(energy.clamp(0.0, 100.0)),
                f64::from(fatigue.clamp(0.0, 100.0)),
                disconnected_at,
                entity_id,
            ],
        )?;
        Ok(())
    }

    pub fn upsert_session(
        &self,
        token: &str,
        owner_key: &str,
        entity_id: &str,
        password_ok: bool,
        created_at: f64,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO sessions (token, owner_key, entity_id, password_ok, created_at)
             VALUES (?1, ?2, ?3, ?4, ?5)
             ON CONFLICT(token) DO UPDATE SET
                 owner_key = excluded.owner_key,
                 entity_id = excluded.entity_id,
                 password_ok = excluded.password_ok,
                 created_at = excluded.created_at",
            params![
                token,
                owner_key,
                entity_id,
                if password_ok { 1i64 } else { 0i64 },
                created_at
            ],
        )?;
        Ok(())
    }

    pub fn update_session_entity(
        &self,
        token: &str,
        entity_id: &str,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "UPDATE sessions SET entity_id = ?1 WHERE token = ?2",
            params![entity_id, token],
        )?;
        Ok(n > 0)
    }

    pub fn delete_session(&self, token: &str) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute("DELETE FROM sessions WHERE token = ?1", params![token])?;
        Ok(())
    }

    pub fn delete_sessions_for_entity(&self, entity_id: &str) -> Result<usize, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "DELETE FROM sessions WHERE entity_id = ?1",
            params![entity_id],
        )?;
        Ok(n)
    }

    pub fn load_valid_sessions(
        &self,
        min_created_at: f64,
    ) -> Result<Vec<SessionRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT token, owner_key, entity_id, password_ok, created_at
             FROM sessions
             WHERE created_at >= ?1",
        )?;
        let rows = stmt.query_map(params![min_created_at], |row| {
            Ok(SessionRecord {
                token: row.get(0)?,
                owner_key: row.get(1)?,
                entity_id: row.get(2)?,
                password_ok: row.get::<_, i64>(3)? != 0,
                created_at: row.get(4)?,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn prune_expired_sessions(&self, min_created_at: f64) -> Result<usize, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "DELETE FROM sessions WHERE created_at < ?1",
            params![min_created_at],
        )?;
        Ok(n)
    }

    pub fn delete_character(
        &self,
        entity_id: &str,
        owner_key: &str,
    ) -> Result<bool, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let n = tx.execute(
            "DELETE FROM characters WHERE entity_id = ?1 AND owner_key = ?2",
            params![entity_id, owner_key],
        )?;
        if n > 0 {
            let old_clan: Option<String> = tx
                .query_row(
                    "SELECT clan_id FROM clan_members WHERE entity_id = ?1",
                    params![entity_id],
                    |row| row.get(0),
                )
                .optional()?;
            tx.execute("DELETE FROM sessions WHERE entity_id = ?1", params![entity_id])?;
            tx.execute(
                "DELETE FROM inventory_items WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM equipped_items WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM player_skills WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM player_skill_categories WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM player_storage WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM player_points WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM explored_pois WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM mails WHERE recipient_entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM market_products WHERE seller_entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM market_favorites WHERE entity_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM estate_cells WHERE estate_id IN (SELECT estate_id FROM estates WHERE owner_type != 2 AND owner_id = ?1)",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM estates WHERE owner_type != 2 AND owner_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM clan_members WHERE entity_id = ?1",
                params![entity_id],
            )?;
            if let Some(cid) = old_clan {
                let remaining: i64 = tx.query_row(
                    "SELECT COUNT(*) FROM clan_members WHERE clan_id = ?1",
                    params![cid],
                    |row| row.get(0),
                )?;
                if remaining == 0 {
                    tx.execute(
                        "DELETE FROM estate_cells WHERE estate_id IN (SELECT estate_id FROM estates WHERE owner_type = 2 AND owner_id = ?1)",
                        params![cid],
                    )?;
                    tx.execute(
                        "DELETE FROM estates WHERE owner_type = 2 AND owner_id = ?1",
                        params![cid],
                    )?;
                    tx.execute("DELETE FROM clans WHERE clan_id = ?1", params![cid])?;
                } else {
                    let current_leader: Option<String> = tx
                        .query_row(
                            "SELECT leader_entity_id FROM clans WHERE clan_id = ?1",
                            params![cid],
                            |row| row.get(0),
                        )
                        .optional()?;
                    if current_leader.as_deref() == Some(entity_id) {
                        if let Some(next_leader) = tx
                            .query_row(
                                "SELECT entity_id FROM clan_members WHERE clan_id = ?1 ORDER BY role_id ASC, joined_at ASC LIMIT 1",
                                params![cid],
                                |row| row.get::<_, String>(0),
                            )
                            .optional()?
                        {
                            tx.execute(
                                "UPDATE clans SET leader_entity_id = ?1 WHERE clan_id = ?2",
                                params![next_leader, cid],
                            )?;
                            tx.execute(
                                "UPDATE clan_members SET role_id = 1 WHERE entity_id = ?1",
                                params![next_leader],
                            )?;
                        }
                    }
                }
            }
        }
        tx.commit()?;
        Ok(n > 0)
    }

    pub fn list_by_owner(&self, owner_key: &str) -> Result<Vec<CharacterRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT entity_id, owner_key, name, job, level, region_id, terrain_id,
                    pos_x, pos_y, disconnected_at, created_at, gender, model_info, exp, untrained_count,
                    life, stamina, energy, fatigue
             FROM characters
             WHERE owner_key = ?1
             ORDER BY created_at ASC",
        )?;
        let rows = stmt.query_map(params![owner_key], |row| {
            Ok(CharacterRecord {
                entity_id: row.get(0)?,
                owner_key: row.get(1)?,
                name: row.get(2)?,
                job: row.get(3)?,
                level: row.get(4)?,
                region_id: row.get(5)?,
                terrain_id: row.get(6)?,
                pos_x: row.get(7)?,
                pos_y: row.get(8)?,
                disconnected_at: row.get(9)?,
                created_at: row.get(10)?,
                gender: row.get(11)?,
                model_info: row.get(12)?,
                exp: row.get(13)?,
                untrained_count: row.get(14)?,
                life: row.get(15)?,
                stamina: row.get(16)?,
                energy: row.get(17)?,
                fatigue: row.get(18)?,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn get_by_entity_id(
        &self,
        entity_id: &str,
    ) -> Result<Option<CharacterRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        conn.query_row(
            "SELECT entity_id, owner_key, name, job, level, region_id, terrain_id,
                    pos_x, pos_y, disconnected_at, created_at, gender, model_info, exp, untrained_count,
                    life, stamina, energy, fatigue
             FROM characters
             WHERE entity_id = ?1",
            params![entity_id],
            |row| {
                Ok(CharacterRecord {
                    entity_id: row.get(0)?,
                    owner_key: row.get(1)?,
                    name: row.get(2)?,
                    job: row.get(3)?,
                    level: row.get(4)?,
                    region_id: row.get(5)?,
                    terrain_id: row.get(6)?,
                    pos_x: row.get(7)?,
                    pos_y: row.get(8)?,
                    disconnected_at: row.get(9)?,
                    created_at: row.get(10)?,
                    gender: row.get(11)?,
                    model_info: row.get(12)?,
                    exp: row.get(13)?,
                    untrained_count: row.get(14)?,
                    life: row.get(15)?,
                    stamina: row.get(16)?,
                    energy: row.get(17)?,
                    fatigue: row.get(18)?,
                })
            },
        )
        .optional()
    }

    pub fn update_survival(
        &self,
        entity_id: &str,
        life: f32,
        stamina: f32,
        energy: f32,
        fatigue: f32,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters SET life = ?1, stamina = ?2, energy = ?3, fatigue = ?4 WHERE entity_id = ?5",
            params![
                life.clamp(0.0, 100.0),
                stamina.clamp(0.0, 100.0),
                energy.clamp(0.0, 100.0),
                fatigue.clamp(0.0, 100.0),
                entity_id
            ],
        )?;
        Ok(())
    }

    pub fn update_position(
        &self,
        entity_id: &str,
        pos_x: f32,
        pos_y: f32,
        disconnected_at: f64,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters SET pos_x = ?1, pos_y = ?2, disconnected_at = ?3 WHERE entity_id = ?4",
            params![pos_x, pos_y, disconnected_at, entity_id],
        )?;
        Ok(())
    }

    pub fn list_items(&self, entity_id: &str) -> Result<Vec<ItemRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT item_id, entity_id, prototype_id, name, icon, level, size,
                    durability, max_durability, locked, collectible_id, generator_id, created_at
             FROM inventory_items
             WHERE entity_id = ?1
             ORDER BY slot_order ASC, created_at ASC, item_id ASC",
        )?;
        let rows = stmt.query_map(params![entity_id], |row| {
            let locked_i: i64 = row.get(9)?;
            Ok(ItemRecord {
                item_id: row.get(0)?,
                entity_id: row.get(1)?,
                prototype_id: row.get(2)?,
                name: row.get(3)?,
                icon: row.get(4)?,
                level: row.get(5)?,
                size: row.get(6)?,
                durability: row.get(7)?,
                max_durability: row.get(8)?,
                locked: locked_i != 0,
                collectible_id: row.get(10)?,
                generator_id: row.get(11)?,
                created_at: row.get(12)?,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn insert_items(&self, items: &[ItemRecord]) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        {
            let mut stmt = tx.prepare(
                "INSERT INTO inventory_items (
                     item_id, entity_id, prototype_id, name, icon, level, size,
                     durability, max_durability, locked, collectible_id, generator_id, created_at
                 ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13)
                 ON CONFLICT(item_id) DO UPDATE SET
                     entity_id = excluded.entity_id,
                     prototype_id = excluded.prototype_id,
                     name = excluded.name,
                     icon = excluded.icon,
                     level = excluded.level,
                     size = excluded.size,
                     durability = excluded.durability,
                     max_durability = excluded.max_durability,
                     locked = excluded.locked,
                     collectible_id = excluded.collectible_id,
                     generator_id = excluded.generator_id,
                     created_at = excluded.created_at",
            )?;
            for it in items {
                stmt.execute(params![
                    it.item_id,
                    it.entity_id,
                    it.prototype_id,
                    it.name,
                    it.icon,
                    it.level,
                    it.size,
                    it.durability,
                    it.max_durability,
                    if it.locked { 1i64 } else { 0i64 },
                    it.collectible_id,
                    it.generator_id,
                    it.created_at,
                ])?;
            }
        }
        tx.commit()
    }

    pub fn set_items_locked(
        &self,
        entity_id: &str,
        item_ids: &[String],
        locked: bool,
    ) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let flag: i64 = if locked { 1 } else { 0 };
        {
            let mut stmt = tx.prepare(
                "UPDATE inventory_items SET locked = ?1 WHERE entity_id = ?2 AND item_id = ?3",
            )?;
            for id in item_ids {
                stmt.execute(params![flag, entity_id, id])?;
            }
        }
        tx.commit()
    }

    pub fn list_removed_naturals(
        &self,
        region_id: &str,
    ) -> Result<Vec<(u16, u16)>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT tile_x, tile_y FROM removed_naturals WHERE region_id = ?1",
        )?;
        let rows = stmt.query_map(params![region_id], |row| {
            let x: i64 = row.get(0)?;
            let y: i64 = row.get(1)?;
            Ok((x.clamp(0, u16::MAX as i64) as u16, y.clamp(0, u16::MAX as i64) as u16))
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn mark_natural_removed(
        &self,
        region_id: &str,
        tile_x: u16,
        tile_y: u16,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT OR IGNORE INTO removed_naturals (region_id, tile_x, tile_y) VALUES (?1, ?2, ?3)",
            params![region_id, i64::from(tile_x), i64::from(tile_y)],
        )?;
        Ok(())
    }

    pub fn delete_items(&self, entity_id: &str, item_ids: &[String]) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        {
            let mut del_item = tx.prepare(
                "DELETE FROM inventory_items WHERE entity_id = ?1 AND item_id = ?2",
            )?;
            let mut del_eq = tx.prepare(
                "DELETE FROM equipped_items WHERE entity_id = ?1 AND item_id = ?2",
            )?;
            for id in item_ids {
                del_item.execute(params![entity_id, id])?;
                del_eq.execute(params![entity_id, id])?;
            }
        }
        tx.commit()
    }

    pub fn update_item_order(
        &self,
        entity_id: &str,
        ordered_ids: &[String],
    ) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        {
            let mut stmt = tx.prepare(
                "UPDATE inventory_items SET slot_order = ?1 WHERE entity_id = ?2 AND item_id = ?3",
            )?;
            for (idx, id) in ordered_ids.iter().enumerate() {
                stmt.execute(params![(idx as i64) + 1, entity_id, id])?;
            }
        }
        tx.commit()
    }

    pub fn list_equipped(&self, entity_id: &str) -> Result<Vec<(String, String)>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT slot_name, item_id FROM equipped_items WHERE entity_id = ?1 ORDER BY slot_name ASC",
        )?;
        let rows = stmt.query_map(params![entity_id], |row| Ok((row.get(0)?, row.get(1)?)))?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn set_equipped(
        &self,
        entity_id: &str,
        slot_name: &str,
        item_id: &str,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO equipped_items (entity_id, slot_name, item_id) VALUES (?1, ?2, ?3)
             ON CONFLICT(entity_id, slot_name) DO UPDATE SET item_id = excluded.item_id",
            params![entity_id, slot_name, item_id],
        )?;
        Ok(())
    }

    pub fn clear_equipped(
        &self,
        entity_id: &str,
        slot_name: &str,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "DELETE FROM equipped_items WHERE entity_id = ?1 AND slot_name = ?2",
            params![entity_id, slot_name],
        )?;
        Ok(n > 0)
    }

    pub fn is_item_equipped(&self, entity_id: &str, item_id: &str) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let found: Option<i64> = conn
            .query_row(
                "SELECT 1 FROM equipped_items WHERE entity_id = ?1 AND item_id = ?2 LIMIT 1",
                params![entity_id, item_id],
                |row| row.get(0),
            )
            .optional()?;
        Ok(found.is_some())
    }

    pub fn equipped_item_ids(
        &self,
        entity_id: &str,
    ) -> Result<std::collections::HashSet<String>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT item_id FROM equipped_items WHERE entity_id = ?1",
        )?;
        let rows = stmt.query_map(params![entity_id], |row| row.get::<_, String>(0))?;
        let mut out = std::collections::HashSet::new();
        for r in rows {
            out.insert(r?);
        }
        Ok(out)
    }

    pub fn list_artifacts_in_region(
        &self,
        region_id: &str,
    ) -> Result<Vec<ArtifactRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT entity_id, region_id, entity_type, blueprint_id, tile_x, tile_y,
                    size_x, size_y, height, rotation, building_state, founder_entity_id,
                    level, created_at, durability, max_durability, last_decay_at,
                    gate_opened, is_lit, materials_json
             FROM artifacts
             WHERE region_id = ?1
             ORDER BY created_at ASC",
        )?;
        let rows = stmt.query_map(params![region_id], |row| {
            let et: i64 = row.get(2)?;
            let tx: i64 = row.get(4)?;
            let ty: i64 = row.get(5)?;
            let sx: i64 = row.get(6)?;
            let sy: i64 = row.get(7)?;
            let lvl: i64 = row.get(12)?;
            let dur: f64 = row.get::<_, f64>(14).unwrap_or(100.0);
            let max_dur: f64 = row.get::<_, f64>(15).unwrap_or(100.0).max(1.0);
            let last_decay: f64 = row.get::<_, f64>(16).unwrap_or(0.0);
            let gate_opened: i64 = row.get::<_, i64>(17).unwrap_or(0);
            let is_lit: i64 = row.get::<_, i64>(18).unwrap_or(0);
            let materials_json: String = row.get::<_, String>(19).unwrap_or_else(|_| "{}".into());
            Ok(ArtifactRecord {
                entity_id: row.get(0)?,
                region_id: row.get(1)?,
                entity_type: et.clamp(0, u16::MAX as i64) as u16,
                blueprint_id: row.get(3)?,
                tile_x: tx.clamp(0, u16::MAX as i64) as u16,
                tile_y: ty.clamp(0, u16::MAX as i64) as u16,
                size_x: sx.clamp(1, u16::MAX as i64) as u16,
                size_y: sy.clamp(1, u16::MAX as i64) as u16,
                height: row.get(8)?,
                rotation: row.get(9)?,
                building_state: row.get(10)?,
                founder_entity_id: row.get(11)?,
                level: lvl.clamp(1, u8::MAX as i64) as u8,
                created_at: row.get(13)?,
                durability: dur as f32,
                max_durability: max_dur as f32,
                last_decay_at: last_decay,
                gate_opened: gate_opened != 0,
                is_lit: is_lit != 0,
                materials_json,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn get_artifact(&self, entity_id: &str) -> Result<Option<ArtifactRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        conn.query_row(
            "SELECT entity_id, region_id, entity_type, blueprint_id, tile_x, tile_y,
                    size_x, size_y, height, rotation, building_state, founder_entity_id,
                    level, created_at, durability, max_durability, last_decay_at,
                    gate_opened, is_lit, materials_json
             FROM artifacts
             WHERE entity_id = ?1",
            params![entity_id],
            |row| {
                let et: i64 = row.get(2)?;
                let tx: i64 = row.get(4)?;
                let ty: i64 = row.get(5)?;
                let sx: i64 = row.get(6)?;
                let sy: i64 = row.get(7)?;
                let lvl: i64 = row.get(12)?;
                let dur: f64 = row.get::<_, f64>(14).unwrap_or(100.0);
                let max_dur: f64 = row.get::<_, f64>(15).unwrap_or(100.0).max(1.0);
                let last_decay: f64 = row.get::<_, f64>(16).unwrap_or(0.0);
                let gate_opened: i64 = row.get::<_, i64>(17).unwrap_or(0);
                let is_lit: i64 = row.get::<_, i64>(18).unwrap_or(0);
                let materials_json: String =
                    row.get::<_, String>(19).unwrap_or_else(|_| "{}".into());
                Ok(ArtifactRecord {
                    entity_id: row.get(0)?,
                    region_id: row.get(1)?,
                    entity_type: et.clamp(0, u16::MAX as i64) as u16,
                    blueprint_id: row.get(3)?,
                    tile_x: tx.clamp(0, u16::MAX as i64) as u16,
                    tile_y: ty.clamp(0, u16::MAX as i64) as u16,
                    size_x: sx.clamp(1, u16::MAX as i64) as u16,
                    size_y: sy.clamp(1, u16::MAX as i64) as u16,
                    height: row.get(8)?,
                    rotation: row.get(9)?,
                    building_state: row.get(10)?,
                    founder_entity_id: row.get(11)?,
                    level: lvl.clamp(1, u8::MAX as i64) as u8,
                    created_at: row.get(13)?,
                    durability: dur as f32,
                    max_durability: max_dur as f32,
                    last_decay_at: last_decay,
                    gate_opened: gate_opened != 0,
                    is_lit: is_lit != 0,
                    materials_json,
                })
            },
        )
        .optional()
    }

    pub fn upsert_artifact(&self, rec: &ArtifactRecord) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO artifacts (
                 entity_id, region_id, entity_type, blueprint_id, tile_x, tile_y,
                 size_x, size_y, height, rotation, building_state, founder_entity_id,
                 level, created_at, durability, max_durability, last_decay_at,
                 gate_opened, is_lit, materials_json
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14, ?15, ?16, ?17, ?18, ?19, ?20)
             ON CONFLICT(entity_id) DO UPDATE SET
                 building_state = excluded.building_state,
                 durability = excluded.durability,
                 max_durability = excluded.max_durability,
                 last_decay_at = excluded.last_decay_at,
                 gate_opened = excluded.gate_opened,
                 is_lit = excluded.is_lit,
                 materials_json = excluded.materials_json",
            params![
                rec.entity_id,
                rec.region_id,
                i64::from(rec.entity_type),
                rec.blueprint_id,
                i64::from(rec.tile_x),
                i64::from(rec.tile_y),
                i64::from(rec.size_x),
                i64::from(rec.size_y),
                rec.height,
                rec.rotation,
                rec.building_state,
                rec.founder_entity_id,
                i64::from(rec.level),
                rec.created_at,
                f64::from(rec.durability),
                f64::from(rec.max_durability.max(1.0)),
                rec.last_decay_at,
                if rec.gate_opened { 1i64 } else { 0i64 },
                if rec.is_lit { 1i64 } else { 0i64 },
                if rec.materials_json.is_empty() { "{}" } else { &rec.materials_json },
            ],
        )?;
        Ok(())
    }

    pub fn update_artifact_flags(
        &self,
        entity_id: &str,
        gate_opened: bool,
        is_lit: bool,
        materials_json: &str,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "UPDATE artifacts SET gate_opened = ?1, is_lit = ?2, materials_json = ?3 WHERE entity_id = ?4",
            params![
                if gate_opened { 1i64 } else { 0i64 },
                if is_lit { 1i64 } else { 0i64 },
                materials_json,
                entity_id,
            ],
        )?;
        Ok(n > 0)
    }

    pub fn update_artifact_durability(
        &self,
        entity_id: &str,
        durability: f32,
        now: f64,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "UPDATE artifacts SET durability = ?1, last_decay_at = ?2 WHERE entity_id = ?3",
            params![f64::from(durability.max(0.0)), now, entity_id],
        )?;
        Ok(n > 0)
    }

    /// Returns true if the artifact's tile footprint `(tile_x..+size_x, tile_y..+size_y)` falls
    /// inside ANY active (unexpired) Domain/Estate cell on `region_id` (grid cell = tile / 4).
    pub fn is_artifact_in_domain_footprint(
        &self,
        region_id: &str,
        tile_x: u16,
        tile_y: u16,
        size_x: u16,
        size_y: u16,
        now: f64,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        Self::is_footprint_in_domain_conn(&conn, region_id, tile_x, tile_y, size_x, size_y, now)
    }

    fn is_footprint_in_domain_conn(
        conn: &Connection,
        region_id: &str,
        tile_x: u16,
        tile_y: u16,
        size_x: u16,
        size_y: u16,
        now: f64,
    ) -> Result<bool, rusqlite::Error> {
        let min_cx = i64::from(tile_x / 4);
        let min_cy = i64::from(tile_y / 4);
        let max_tx = tile_x.saturating_add(size_x.max(1) - 1);
        let max_ty = tile_y.saturating_add(size_y.max(1) - 1);
        let max_cx = i64::from(max_tx / 4);
        let max_cy = i64::from(max_ty / 4);
        let count: i64 = conn.query_row(
            "SELECT COUNT(*) FROM estate_cells ec
             JOIN estates e ON e.estate_id = ec.estate_id
             WHERE ec.region_id = ?1
               AND ec.cell_x BETWEEN ?2 AND ?3
               AND ec.cell_y BETWEEN ?4 AND ?5
               AND e.expires_at > ?6",
            params![region_id, min_cx, max_cx, min_cy, max_cy, now],
            |r| r.get(0),
        )?;
        Ok(count > 0)
    }

    pub fn is_artifact_in_domain(
        &self,
        region_id: &str,
        tile_x: u16,
        tile_y: u16,
    ) -> Result<bool, rusqlite::Error> {
        self.is_artifact_in_domain_footprint(region_id, tile_x, tile_y, 1, 1, 0.0)
    }

    /// Decays durability of completed non-permanent wilderness buildings outside any active domain.
    /// Runs in a single transaction instead of per-row mutex re-acquisition.
    pub fn decay_wilderness_artifacts(
        &self,
        region_id: &str,
        now: f64,
        decay_per_hour: f32,
    ) -> Result<Vec<ArtifactRecord>, rusqlite::Error> {
        let arts = self.list_artifacts_in_region(region_id)?;
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let mut updated = Vec::new();
        {
            let mut stmt = tx.prepare(
                "UPDATE artifacts SET durability = ?1, last_decay_at = ?2 WHERE entity_id = ?3",
            )?;
            for mut a in arts {
                if a.founder_entity_id == "world"
                    || a.founder_entity_id == "system"
                    || a.building_state != 2
                {
                    continue;
                }
                if Self::is_footprint_in_domain_conn(
                    &tx, region_id, a.tile_x, a.tile_y, a.size_x, a.size_y, now,
                )? {
                    continue;
                }
                let base_t = if a.last_decay_at > 0.0 {
                    a.last_decay_at
                } else {
                    a.created_at
                };
                let elapsed_hours = ((now - base_t) / 3600.0).max(0.0) as f32;
                if elapsed_hours < 0.05 {
                    continue;
                }
                let loss = elapsed_hours * decay_per_hour;
                if loss >= 0.5 && a.durability > 0.0 {
                    a.durability = (a.durability - loss).max(0.0);
                    a.last_decay_at = now;
                    stmt.execute(params![f64::from(a.durability), now, a.entity_id])?;
                    updated.push(a);
                }
            }
        }
        tx.commit()?;
        Ok(updated)
    }

    /// Finds the active (unexpired) estate covering any cell of `(tile_x..+size_x, tile_y..+size_y)`.
    pub fn find_active_estate_for_tile(
        &self,
        region_id: &str,
        tile_x: u16,
        tile_y: u16,
        size_x: u16,
        size_y: u16,
        now: f64,
    ) -> Result<Option<EstateRecord>, rusqlite::Error> {
        let min_cx = i64::from(tile_x / 4);
        let min_cy = i64::from(tile_y / 4);
        let max_tx = tile_x.saturating_add(size_x.max(1) - 1);
        let max_ty = tile_y.saturating_add(size_y.max(1) - 1);
        let max_cx = i64::from(max_tx / 4);
        let max_cy = i64::from(max_ty / 4);
        let conn = self.lock_conn();
        let opt = conn
            .query_row(
                "SELECT e.estate_id, e.owner_type, e.owner_id, e.region_id, e.anchor_x, e.anchor_y,
                        e.activated_at, e.expires_at, e.access_for_others
                 FROM estate_cells ec
                 JOIN estates e ON e.estate_id = ec.estate_id
                 WHERE ec.region_id = ?1
                   AND ec.cell_x BETWEEN ?2 AND ?3
                   AND ec.cell_y BETWEEN ?4 AND ?5
                   AND e.expires_at > ?6
                 LIMIT 1",
                params![region_id, min_cx, max_cx, min_cy, max_cy, now],
                |row| {
                    Ok(EstateRecord {
                        estate_id: row.get(0)?,
                        owner_type: row.get(1)?,
                        owner_id: row.get(2)?,
                        region_id: row.get(3)?,
                        anchor_x: row.get::<_, i64>(4)? as u16,
                        anchor_y: row.get::<_, i64>(5)? as u16,
                        activated_at: row.get(6)?,
                        expires_at: row.get(7)?,
                        access_for_others: row.get(8)?,
                        cells: Vec::new(),
                    })
                },
            )
            .optional()?;
        Ok(opt)
    }

    pub fn purge_expired_estates(&self, region_id: &str, now: f64) -> Result<usize, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM estate_cells WHERE estate_id IN (SELECT estate_id FROM estates WHERE region_id = ?1 AND expires_at <= ?2)",
            params![region_id, now],
        )?;
        let n = tx.execute(
            "DELETE FROM estates WHERE region_id = ?1 AND expires_at <= ?2",
            params![region_id, now],
        )?;
        tx.commit()?;
        Ok(n)
    }

    pub fn update_artifact_state(
        &self,
        entity_id: &str,
        building_state: i32,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "UPDATE artifacts SET building_state = ?1 WHERE entity_id = ?2",
            params![building_state, entity_id],
        )?;
        Ok(n > 0)
    }

    pub fn update_artifact_state_if(
        &self,
        entity_id: &str,
        expected_state: i32,
        new_state: i32,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "UPDATE artifacts SET building_state = ?1 WHERE entity_id = ?2 AND building_state = ?3",
            params![new_state, entity_id, expected_state],
        )?;
        Ok(n > 0)
    }

    pub fn get_item(&self, entity_id: &str, item_id: &str) -> Result<Option<ItemRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        conn.query_row(
            "SELECT item_id, entity_id, prototype_id, name, icon, level, size,
                    durability, max_durability, locked, collectible_id, generator_id, created_at
             FROM inventory_items
             WHERE entity_id = ?1 AND item_id = ?2",
            params![entity_id, item_id],
            |row| {
                let locked_i: i64 = row.get(9)?;
                Ok(ItemRecord {
                    item_id: row.get(0)?,
                    entity_id: row.get(1)?,
                    prototype_id: row.get(2)?,
                    name: row.get(3)?,
                    icon: row.get(4)?,
                    level: row.get(5)?,
                    size: row.get(6)?,
                    durability: row.get(7)?,
                    max_durability: row.get(8)?,
                    locked: locked_i != 0,
                    collectible_id: row.get(10)?,
                    generator_id: row.get(11)?,
                    created_at: row.get(12)?,
                })
            },
        )
        .optional()
    }

    pub fn delete_artifact(&self, entity_id: &str) -> Result<bool, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let n = tx.execute(
            "DELETE FROM artifacts WHERE entity_id = ?1",
            params![entity_id],
        )?;
        if n > 0 {
            tx.execute(
                "DELETE FROM container_items WHERE container_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "DELETE FROM warehouse_sections WHERE container_id = ?1",
                params![entity_id],
            )?;
            tx.execute(
                "UPDATE player_points SET home_artifact_id = NULL WHERE home_artifact_id = ?1",
                params![entity_id],
            )?;
        }
        tx.commit()?;
        Ok(n > 0)
    }

    pub fn search_characters_by_name(
        &self,
        needle: &str,
    ) -> Result<Vec<CharacterRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let pattern = format!("%{}%", escape_like_pattern(needle));
        let mut stmt = conn.prepare(
            "SELECT entity_id, owner_key, name, job, level, region_id, terrain_id,
                    pos_x, pos_y, disconnected_at, created_at, gender, model_info, exp, untrained_count,
                    life, stamina, energy, fatigue
             FROM characters
             WHERE LOWER(name) LIKE LOWER(?1) ESCAPE '\\'
             LIMIT 20",
        )?;
        let rows = stmt.query_map(params![pattern], |row| {
            Ok(CharacterRecord {
                entity_id: row.get(0)?,
                owner_key: row.get(1)?,
                name: row.get(2)?,
                job: row.get(3)?,
                level: row.get(4)?,
                region_id: row.get(5)?,
                terrain_id: row.get(6)?,
                pos_x: row.get(7)?,
                pos_y: row.get(8)?,
                disconnected_at: row.get(9)?,
                created_at: row.get(10)?,
                gender: row.get(11)?,
                model_info: row.get(12)?,
                exp: row.get(13)?,
                untrained_count: row.get(14)?,
                life: row.get(15)?,
                stamina: row.get(16)?,
                energy: row.get(17)?,
                fatigue: row.get(18)?,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn insert_market_product(
        &self,
        rec: &MarketProductRecord,
    ) -> Result<(), rusqlite::Error> {
        let item_json = serde_json::to_string(&rec.item).unwrap_or_default();
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO market_products (
                 product_id, seller_entity_id, region_id, item_json, prototype_id,
                 item_name, item_level, price, listed_at, expires_at
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10)",
            params![
                rec.product_id,
                rec.seller_entity_id,
                rec.region_id,
                item_json,
                rec.item.prototype_id,
                rec.item.name,
                rec.item.level,
                rec.price,
                rec.listed_at,
                rec.expires_at,
            ],
        )?;
        Ok(())
    }

    pub fn search_market_products(
        &self,
        now: f64,
        name_filter: Option<&str>,
        proto_filter: Option<&str>,
        skip: usize,
    ) -> Result<Vec<MarketProductRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let name_pat = name_filter
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(|s| format!("%{}%", escape_like_pattern(&s.to_ascii_lowercase())));
        let proto_q = proto_filter
            .map(str::trim)
            .filter(|s| !s.is_empty())
            .map(|s| s.to_ascii_lowercase());
        let mut stmt = conn.prepare(
            "SELECT product_id, seller_entity_id, region_id, item_json, price, listed_at, expires_at
             FROM market_products
             WHERE expires_at > ?1
               AND (?2 IS NULL OR LOWER(item_name) LIKE ?2 ESCAPE '\\' OR LOWER(prototype_id) LIKE ?2 ESCAPE '\\')
               AND (?3 IS NULL OR LOWER(prototype_id) = ?3)
             ORDER BY listed_at DESC
             LIMIT 50 OFFSET ?4",
        )?;
        let rows = stmt.query_map(params![now, name_pat, proto_q, skip as i64], |row| {
            let item_json: String = row.get(3)?;
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, String>(2)?,
                item_json,
                row.get::<_, i64>(4)?,
                row.get::<_, f64>(5)?,
                row.get::<_, f64>(6)?,
            ))
        })?;
        let mut out = Vec::new();
        for r in rows {
            let (product_id, seller_entity_id, region_id, item_json, price, listed_at, expires_at) =
                r?;
            let Ok(item) = serde_json::from_str::<ItemRecord>(&item_json) else {
                continue;
            };
            out.push(MarketProductRecord {
                product_id,
                seller_entity_id,
                region_id,
                item,
                price,
                listed_at,
                expires_at,
            });
        }
        Ok(out)
    }

    pub fn take_market_product(
        &self,
        product_id: &str,
        now: f64,
    ) -> Result<Option<MarketProductRecord>, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let found: Option<(String, String, String, i64, f64, f64)> = tx
            .query_row(
                "SELECT seller_entity_id, region_id, item_json, price, listed_at, expires_at
                 FROM market_products WHERE product_id = ?1 AND expires_at > ?2",
                params![product_id, now],
                |row| {
                    Ok((
                        row.get(0)?,
                        row.get(1)?,
                        row.get(2)?,
                        row.get(3)?,
                        row.get(4)?,
                        row.get(5)?,
                    ))
                },
            )
            .optional()?;
        let Some((seller_entity_id, region_id, item_json, price, listed_at, expires_at)) = found
        else {
            return Ok(None);
        };
        let Ok(item) = serde_json::from_str::<ItemRecord>(&item_json) else {
            return Ok(None);
        };
        tx.execute(
            "DELETE FROM market_products WHERE product_id = ?1",
            params![product_id],
        )?;
        tx.commit()?;
        Ok(Some(MarketProductRecord {
            product_id: product_id.to_string(),
            seller_entity_id,
            region_id,
            item,
            price,
            listed_at,
            expires_at,
        }))
    }

    pub fn update_character_region(
        &self,
        entity_id: &str,
        region_id: &str,
        terrain_id: &str,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters SET region_id = ?1, terrain_id = ?2, pos_x = 0.0, pos_y = 0.0 WHERE entity_id = ?3",
            params![region_id, terrain_id, entity_id],
        )?;
        Ok(())
    }

    pub fn get_player_clan(
        &self,
        entity_id: &str,
    ) -> Result<Option<(String, String, i32)>, rusqlite::Error> {
        let conn = self.lock_conn();
        conn.query_row(
            "SELECT c.clan_id, c.name, m.role_id
             FROM clan_members m
             JOIN clans c ON c.clan_id = m.clan_id
             WHERE m.entity_id = ?1",
            params![entity_id],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .optional()
    }

    pub fn create_clan(
        &self,
        clan_id: &str,
        name: &str,
        leader_entity_id: &str,
        now: f64,
    ) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "INSERT INTO clans (clan_id, name, leader_entity_id, created_at) VALUES (?1, ?2, ?3, ?4)",
            params![clan_id, name, leader_entity_id, now],
        )?;
        tx.execute(
            "INSERT OR REPLACE INTO clan_members (entity_id, clan_id, role_id, joined_at) VALUES (?1, ?2, 1, ?3)",
            params![leader_entity_id, clan_id, now],
        )?;
        tx.commit()
    }

    pub fn leave_clan(&self, entity_id: &str) -> Result<bool, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let old_clan: Option<String> = tx
            .query_row(
                "SELECT clan_id FROM clan_members WHERE entity_id = ?1",
                params![entity_id],
                |row| row.get(0),
            )
            .optional()?;
        let n = tx.execute(
            "DELETE FROM clan_members WHERE entity_id = ?1",
            params![entity_id],
        )?;
        if n > 0 {
            if let Some(cid) = old_clan {
                let remaining: i64 = tx.query_row(
                    "SELECT COUNT(*) FROM clan_members WHERE clan_id = ?1",
                    params![cid],
                    |row| row.get(0),
                )?;
                if remaining == 0 {
                    tx.execute(
                        "DELETE FROM estate_cells WHERE estate_id IN (SELECT estate_id FROM estates WHERE owner_type = 2 AND owner_id = ?1)",
                        params![cid],
                    )?;
                    tx.execute(
                        "DELETE FROM estates WHERE owner_type = 2 AND owner_id = ?1",
                        params![cid],
                    )?;
                    tx.execute("DELETE FROM clans WHERE clan_id = ?1", params![cid])?;
                } else {
                    let current_leader: Option<String> = tx
                        .query_row(
                            "SELECT leader_entity_id FROM clans WHERE clan_id = ?1",
                            params![cid],
                            |row| row.get(0),
                        )
                        .optional()?;
                    if current_leader.as_deref() == Some(entity_id) {
                        if let Some(next_leader) = tx
                            .query_row(
                                "SELECT entity_id FROM clan_members WHERE clan_id = ?1 ORDER BY role_id ASC, joined_at ASC LIMIT 1",
                                params![cid],
                                |row| row.get::<_, String>(0),
                            )
                            .optional()?
                        {
                            tx.execute(
                                "UPDATE clans SET leader_entity_id = ?1 WHERE clan_id = ?2",
                                params![next_leader, cid],
                            )?;
                            tx.execute(
                                "UPDATE clan_members SET role_id = 1 WHERE entity_id = ?1",
                                params![next_leader],
                            )?;
                        }
                    }
                }
            }
        }
        tx.commit()?;
        Ok(n > 0)
    }

    pub fn update_character_level_exp(
        &self,
        entity_id: &str,
        level: i32,
        exp: i32,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters SET level = ?1, exp = ?2 WHERE entity_id = ?3",
            params![level.max(1), exp.max(0), entity_id],
        )?;
        Ok(())
    }

    pub fn increment_untrained_count(&self, entity_id: &str) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters SET untrained_count = untrained_count + 1 WHERE entity_id = ?1",
            params![entity_id],
        )?;
        Ok(())
    }

    pub fn list_player_skills(
        &self,
        entity_id: &str,
    ) -> Result<Vec<LearnedSkillRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT category, skill_id, sub_id, level
             FROM player_skills
             WHERE entity_id = ?1 AND level > 0
             ORDER BY category ASC, skill_id ASC, sub_id ASC",
        )?;
        let rows = stmt.query_map(params![entity_id], |row| {
            Ok(LearnedSkillRecord {
                category: row.get(0)?,
                skill_id: row.get(1)?,
                sub_id: row.get(2)?,
                level: row.get(3)?,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn upsert_player_skill(
        &self,
        entity_id: &str,
        category: i32,
        skill_id: &str,
        sub_id: &str,
        level: i32,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        if level <= 0 {
            conn.execute(
                "DELETE FROM player_skills WHERE entity_id = ?1 AND skill_id = ?2 AND sub_id = ?3",
                params![entity_id, skill_id, sub_id],
            )?;
        } else {
            conn.execute(
                "INSERT INTO player_skills (entity_id, category, skill_id, sub_id, level)
                 VALUES (?1, ?2, ?3, ?4, ?5)
                 ON CONFLICT(entity_id, skill_id, sub_id) DO UPDATE SET level = excluded.level",
                params![entity_id, category, skill_id, sub_id, level],
            )?;
        }
        Ok(())
    }

    pub fn list_skill_categories(
        &self,
        entity_id: &str,
    ) -> Result<std::collections::BTreeMap<i32, (i32, i32)>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT category, level, exp FROM player_skill_categories WHERE entity_id = ?1",
        )?;
        let rows = stmt.query_map(params![entity_id], |row| {
            Ok((row.get::<_, i32>(0)?, (row.get::<_, i32>(1)?, row.get::<_, i32>(2)?)))
        })?;
        let mut out = std::collections::BTreeMap::new();
        for r in rows {
            let (cat, pair) = r?;
            out.insert(cat, pair);
        }
        Ok(out)
    }

    pub fn upsert_skill_category(
        &self,
        entity_id: &str,
        category: i32,
        level: i32,
        exp: i32,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO player_skill_categories (entity_id, category, level, exp)
             VALUES (?1, ?2, ?3, ?4)
             ON CONFLICT(entity_id, category) DO UPDATE SET
                 level = excluded.level,
                 exp = excluded.exp",
            params![entity_id, category, level.max(1), exp.max(0)],
        )?;
        Ok(())
    }

    pub fn list_player_storage(
        &self,
        entity_id: &str,
    ) -> Result<std::collections::BTreeMap<String, Vec<u8>>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt =
            conn.prepare("SELECT key, value FROM player_storage WHERE entity_id = ?1")?;
        let rows = stmt.query_map(params![entity_id], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, Vec<u8>>(1)?))
        })?;
        let mut out = std::collections::BTreeMap::new();
        for r in rows {
            let (k, v) = r?;
            out.insert(k, v);
        }
        Ok(out)
    }

    pub fn set_player_storage(
        &self,
        entity_id: &str,
        key: &str,
        value: &[u8],
    ) -> Result<(), rusqlite::Error> {
        if key.is_empty() || key.len() > 128 || value.len() > 65_536 {
            return Err(rusqlite::Error::InvalidParameterName("storage key/value size".into()));
        }
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let (key_count, total_bytes): (i64, i64) = tx.query_row(
            "SELECT COUNT(*), COALESCE(SUM(LENGTH(value)), 0)
             FROM player_storage WHERE entity_id = ?1 AND key != ?2",
            params![entity_id, key],
            |r| Ok((r.get(0)?, r.get(1)?)),
        )?;
        if key_count >= 64 || total_bytes.saturating_add(value.len() as i64) > 262_144 {
            return Err(rusqlite::Error::InvalidParameterName("storage quota exceeded".into()));
        }
        tx.execute(
            "INSERT INTO player_storage (entity_id, key, value)
             VALUES (?1, ?2, ?3)
             ON CONFLICT(entity_id, key) DO UPDATE SET value = excluded.value",
            params![entity_id, key, value],
        )?;
        tx.commit()?;
        Ok(())
    }

    pub fn get_player_points(&self, entity_id: &str) -> Result<PlayerPointsRecord, rusqlite::Error> {
        let conn = self.lock_conn();
        let row = conn
            .query_row(
                "SELECT home_artifact_id, returning_x, returning_y, returning_region_id, personal_region_id, personal_region_template_id, admission_csv
                 FROM player_points WHERE entity_id = ?1",
                params![entity_id],
                |row| {
                    let csv: String = row.get(6)?;
                    let admission_categories = csv
                        .split(',')
                        .filter_map(|s| s.trim().parse::<i32>().ok())
                        .collect();
                    Ok(PlayerPointsRecord {
                        home_artifact_id: row.get(0)?,
                        returning_x: row.get::<_, Option<i64>>(1)?.map(|v| v as u16),
                        returning_y: row.get::<_, Option<i64>>(2)?.map(|v| v as u16),
                        returning_region_id: row.get(3)?,
                        personal_region_id: row.get(4)?,
                        personal_region_template_id: row.get(5)?,
                        admission_categories,
                    })
                },
            )
            .optional()?;
        Ok(row.unwrap_or_default())
    }

    pub fn upsert_player_points(
        &self,
        entity_id: &str,
        pts: &PlayerPointsRecord,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        let csv = pts
            .admission_categories
            .iter()
            .map(|v| v.to_string())
            .collect::<Vec<_>>()
            .join(",");
        conn.execute(
            "INSERT INTO player_points (
                entity_id, home_artifact_id, returning_x, returning_y, returning_region_id,
                personal_region_id, personal_region_template_id, admission_csv
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)
             ON CONFLICT(entity_id) DO UPDATE SET
                home_artifact_id = excluded.home_artifact_id,
                returning_x = excluded.returning_x,
                returning_y = excluded.returning_y,
                returning_region_id = excluded.returning_region_id,
                personal_region_id = excluded.personal_region_id,
                personal_region_template_id = excluded.personal_region_template_id,
                admission_csv = excluded.admission_csv",
            params![
                entity_id,
                pts.home_artifact_id,
                pts.returning_x.map(i64::from),
                pts.returning_y.map(i64::from),
                pts.returning_region_id,
                pts.personal_region_id,
                pts.personal_region_template_id,
                csv,
            ],
        )?;
        Ok(())
    }

    fn load_estate_cells(
        conn: &Connection,
        estate_id: &str,
    ) -> Result<Vec<(u8, u8)>, rusqlite::Error> {
        let mut stmt = conn.prepare(
            "SELECT cell_x, cell_y FROM estate_cells WHERE estate_id = ?1 ORDER BY cell_x, cell_y",
        )?;
        let rows = stmt.query_map(params![estate_id], |row| {
            Ok((row.get::<_, i64>(0)? as u8, row.get::<_, i64>(1)? as u8))
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn list_estates_by_owner(
        &self,
        owner_id: &str,
    ) -> Result<Vec<EstateRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT estate_id, owner_type, owner_id, region_id, anchor_x, anchor_y, activated_at, expires_at, access_for_others
             FROM estates WHERE owner_id = ?1",
        )?;
        let rows = stmt.query_map(params![owner_id], |row| {
            Ok(EstateRecord {
                estate_id: row.get(0)?,
                owner_type: row.get(1)?,
                owner_id: row.get(2)?,
                region_id: row.get(3)?,
                anchor_x: row.get::<_, i64>(4)? as u16,
                anchor_y: row.get::<_, i64>(5)? as u16,
                activated_at: row.get(6)?,
                expires_at: row.get(7)?,
                access_for_others: row.get(8)?,
                cells: Vec::new(),
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            let mut rec = r?;
            rec.cells = Self::load_estate_cells(&conn, &rec.estate_id)?;
            out.push(rec);
        }
        Ok(out)
    }

    pub fn list_estates_in_region(
        &self,
        region_id: &str,
    ) -> Result<Vec<EstateRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT estate_id, owner_type, owner_id, region_id, anchor_x, anchor_y, activated_at, expires_at, access_for_others
             FROM estates WHERE region_id = ?1",
        )?;
        let rows = stmt.query_map(params![region_id], |row| {
            Ok(EstateRecord {
                estate_id: row.get(0)?,
                owner_type: row.get(1)?,
                owner_id: row.get(2)?,
                region_id: row.get(3)?,
                anchor_x: row.get::<_, i64>(4)? as u16,
                anchor_y: row.get::<_, i64>(5)? as u16,
                activated_at: row.get(6)?,
                expires_at: row.get(7)?,
                access_for_others: row.get(8)?,
                cells: Vec::new(),
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            let mut rec = r?;
            rec.cells = Self::load_estate_cells(&conn, &rec.estate_id)?;
            out.push(rec);
        }
        Ok(out)
    }

    pub fn get_estate_by_id(&self, estate_id: &str) -> Result<Option<EstateRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let opt = conn
            .query_row(
                "SELECT estate_id, owner_type, owner_id, region_id, anchor_x, anchor_y, activated_at, expires_at, access_for_others
                 FROM estates WHERE estate_id = ?1",
                params![estate_id],
                |row| {
                    Ok(EstateRecord {
                        estate_id: row.get(0)?,
                        owner_type: row.get(1)?,
                        owner_id: row.get(2)?,
                        region_id: row.get(3)?,
                        anchor_x: row.get::<_, i64>(4)? as u16,
                        anchor_y: row.get::<_, i64>(5)? as u16,
                        activated_at: row.get(6)?,
                        expires_at: row.get(7)?,
                        access_for_others: row.get(8)?,
                        cells: Vec::new(),
                    })
                },
            )
            .optional()?;
        if let Some(mut rec) = opt {
            rec.cells = Self::load_estate_cells(&conn, &rec.estate_id)?;
            Ok(Some(rec))
        } else {
            Ok(None)
        }
    }

    pub fn upsert_estate(&self, rec: &EstateRecord) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "INSERT INTO estates (
                estate_id, owner_type, owner_id, region_id, anchor_x, anchor_y,
                activated_at, expires_at, access_for_others
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)
             ON CONFLICT(estate_id) DO UPDATE SET
                anchor_x = excluded.anchor_x,
                anchor_y = excluded.anchor_y,
                activated_at = excluded.activated_at,
                expires_at = excluded.expires_at,
                access_for_others = excluded.access_for_others",
            params![
                rec.estate_id,
                rec.owner_type,
                rec.owner_id,
                rec.region_id,
                i64::from(rec.anchor_x),
                i64::from(rec.anchor_y),
                rec.activated_at,
                rec.expires_at,
                rec.access_for_others,
            ],
        )?;
        tx.execute(
            "DELETE FROM estate_cells WHERE estate_id = ?1",
            params![rec.estate_id],
        )?;
        for &(cx, cy) in &rec.cells {
            tx.execute(
                "INSERT INTO estate_cells (region_id, cell_x, cell_y, estate_id)
                 VALUES (?1, ?2, ?3, ?4)",
                params![rec.region_id, i64::from(cx), i64::from(cy), rec.estate_id],
            )?;
        }
        tx.commit()?;
        Ok(())
    }

    pub fn delete_estate(&self, estate_id: &str) -> Result<bool, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM estate_cells WHERE estate_id = ?1",
            params![estate_id],
        )?;
        let n = tx.execute(
            "DELETE FROM estates WHERE estate_id = ?1",
            params![estate_id],
        )?;
        tx.commit()?;
        Ok(n > 0)
    }

    pub fn upsert_explored_poi(
        &self,
        entity_id: &str,
        poi: &ExploredPoiRecord,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO explored_pois (
                entity_id, region_id, tile_x, tile_y, poi_type, icon, title, entity_type
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)
             ON CONFLICT(entity_id, region_id, tile_x, tile_y) DO UPDATE SET
                poi_type = excluded.poi_type,
                icon = excluded.icon,
                title = excluded.title,
                entity_type = excluded.entity_type",
            params![
                entity_id,
                poi.region_id,
                i64::from(poi.tile_x),
                i64::from(poi.tile_y),
                poi.poi_type,
                poi.icon,
                poi.title,
                poi.entity_type.map(i64::from),
            ],
        )?;
        Ok(())
    }

    pub fn list_explored_pois(
        &self,
        entity_id: &str,
        region_id: &str,
    ) -> Result<Vec<ExploredPoiRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT region_id, tile_x, tile_y, poi_type, icon, title, entity_type
             FROM explored_pois WHERE entity_id = ?1 AND region_id = ?2",
        )?;
        let rows = stmt.query_map(params![entity_id, region_id], |row| {
            Ok(ExploredPoiRecord {
                region_id: row.get(0)?,
                tile_x: row.get::<_, i64>(1)? as u16,
                tile_y: row.get::<_, i64>(2)? as u16,
                poi_type: row.get(3)?,
                icon: row.get(4)?,
                title: row.get(5)?,
                entity_type: row.get::<_, Option<i64>>(6)?.map(|v| v as u16),
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn update_item_durability(
        &self,
        entity_id: &str,
        item_id: &str,
        durability: f32,
    ) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute(
            "UPDATE inventory_items SET durability = ?1 WHERE entity_id = ?2 AND item_id = ?3",
            params![durability.max(0.0), entity_id, item_id],
        )?;
        Ok(n > 0)
    }

    // ── Wallet (T-Stone) ─────────────────────────────────────────────────────

    pub fn get_t_stone(&self, entity_id: &str) -> Result<i64, rusqlite::Error> {
        let conn = self.lock_conn();
        let bal: Option<i64> = conn
            .query_row(
                "SELECT t_stone FROM characters WHERE entity_id = ?1",
                params![entity_id],
                |row| row.get(0),
            )
            .optional()?;
        Ok(bal.unwrap_or(50_000).max(0))
    }

    /// Atomically spends `cost` T-Stone if `t_stone >= cost`. Returns `Some(new_balance)` on success.
    pub fn try_spend_t_stone(
        &self,
        entity_id: &str,
        cost: i64,
    ) -> Result<Option<i64>, rusqlite::Error> {
        if cost <= 0 {
            return Ok(Some(self.get_t_stone(entity_id)?));
        }
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let changed = tx.execute(
            "UPDATE characters SET t_stone = t_stone - ?1 WHERE entity_id = ?2 AND t_stone >= ?1",
            params![cost, entity_id],
        )?;
        if changed == 0 {
            tx.commit()?;
            return Ok(None);
        }
        let bal: i64 = tx.query_row(
            "SELECT t_stone FROM characters WHERE entity_id = ?1",
            params![entity_id],
            |row| row.get(0),
        )?;
        tx.commit()?;
        Ok(Some(bal))
    }

    pub fn credit_t_stone(&self, entity_id: &str, amount: i64) -> Result<i64, rusqlite::Error> {
        if amount <= 0 {
            return self.get_t_stone(entity_id);
        }
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "UPDATE characters SET t_stone = t_stone + ?1 WHERE entity_id = ?2",
            params![amount, entity_id],
        )?;
        let bal: i64 = tx
            .query_row(
                "SELECT t_stone FROM characters WHERE entity_id = ?1",
                params![entity_id],
                |row| row.get(0),
            )
            .optional()?
            .unwrap_or(0);
        tx.commit()?;
        Ok(bal)
    }

    // ── Storage Containers & Warehouses ──────────────────────────────────────

    pub fn list_container_items(
        &self,
        container_id: &str,
        section_name: Option<&str>,
    ) -> Result<Vec<ItemRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut out = Vec::new();
        if let Some(sec) = section_name {
            let mut stmt = conn.prepare(
                "SELECT item_json FROM container_items
                 WHERE container_id = ?1 AND section_name = ?2
                 ORDER BY slot_order ASC, rowid ASC",
            )?;
            let rows = stmt.query_map(params![container_id, sec], |row| row.get::<_, String>(0))?;
            for r in rows {
                if let Ok(item) = serde_json::from_str::<ItemRecord>(&r?) {
                    out.push(item);
                }
            }
        } else {
            let mut stmt = conn.prepare(
                "SELECT item_json FROM container_items
                 WHERE container_id = ?1
                 ORDER BY slot_order ASC, rowid ASC",
            )?;
            let rows = stmt.query_map(params![container_id], |row| row.get::<_, String>(0))?;
            for r in rows {
                if let Ok(item) = serde_json::from_str::<ItemRecord>(&r?) {
                    out.push(item);
                }
            }
        }
        Ok(out)
    }

    pub fn put_items_in_container(
        &self,
        container_id: &str,
        section_name: &str,
        items: &[ItemRecord],
    ) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "INSERT OR IGNORE INTO warehouse_sections (container_id, section_name, sort_order)
             VALUES (?1, ?2, 0)",
            params![container_id, section_name],
        )?;
        for it in items {
            let json = serde_json::to_string(it).unwrap_or_else(|_| "{}".into());
            tx.execute(
                "INSERT OR REPLACE INTO container_items (item_id, container_id, section_name, item_json, slot_order)
                 VALUES (?1, ?2, ?3, ?4, 0)",
                params![it.item_id, container_id, section_name, json],
            )?;
        }
        tx.commit()?;
        Ok(())
    }

    pub fn take_items_from_container(
        &self,
        container_id: &str,
        section_name: Option<&str>,
        item_ids: &[String],
    ) -> Result<Vec<ItemRecord>, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let mut taken = Vec::new();
        for id in item_ids {
            let row: Option<String> = if let Some(sec) = section_name {
                tx.query_row(
                    "SELECT item_json FROM container_items
                     WHERE container_id = ?1 AND section_name = ?2 AND item_id = ?3",
                    params![container_id, sec, id],
                    |r| r.get(0),
                )
                .optional()?
            } else {
                tx.query_row(
                    "SELECT item_json FROM container_items
                     WHERE container_id = ?1 AND item_id = ?2",
                    params![container_id, id],
                    |r| r.get(0),
                )
                .optional()?
            };
            if let Some(json) = row {
                let Ok(item) = serde_json::from_str::<ItemRecord>(&json) else {
                    continue;
                };
                tx.execute(
                    "DELETE FROM container_items WHERE container_id = ?1 AND item_id = ?2",
                    params![container_id, id],
                )?;
                taken.push(item);
            }
        }
        tx.commit()?;
        Ok(taken)
    }

    pub fn list_warehouse_sections(
        &self,
        container_id: &str,
    ) -> Result<Vec<(String, i32)>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT ws.section_name,
                    (SELECT COUNT(*) FROM container_items ci
                     WHERE ci.container_id = ws.container_id AND ci.section_name = ws.section_name)
             FROM warehouse_sections ws
             WHERE ws.container_id = ?1
             ORDER BY ws.sort_order ASC, ws.section_name ASC",
        )?;
        let rows = stmt.query_map(params![container_id], |row| {
            Ok((row.get::<_, String>(0)?, row.get::<_, i32>(1)?))
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        if out.is_empty() {
            let def_cnt: i32 = conn.query_row(
                "SELECT COUNT(*) FROM container_items WHERE container_id = ?1 AND section_name = 'default'",
                params![container_id],
                |r| r.get(0),
            )?;
            out.push(("default".to_string(), def_cnt));
        }
        Ok(out)
    }

    /// Atomically moves unlocked, unequipped items from a player's inventory into a container/warehouse
    /// section in a single SQLite transaction, enforcing container item capacity.
    pub fn transfer_items_to_container(
        &self,
        entity_id: &str,
        container_id: &str,
        section_name: &str,
        item_ids: &[String],
        max_container_items: usize,
    ) -> Result<Vec<ItemRecord>, rusqlite::Error> {
        let sec = section_name.trim();
        let sec = if sec.is_empty() || sec.chars().count() > 32 {
            "default"
        } else {
            sec
        };
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "INSERT OR IGNORE INTO warehouse_sections (container_id, section_name, sort_order)
             VALUES (?1, ?2, 0)",
            params![container_id, sec],
        )?;
        let mut cur_container_count: usize = tx.query_row(
            "SELECT COUNT(*) FROM container_items WHERE container_id = ?1",
            params![container_id],
            |r| r.get::<_, i64>(0),
        )? as usize;

        let mut moved = Vec::new();
        let mut seen = std::collections::HashSet::new();
        for id in item_ids {
            if !seen.insert(id.clone()) || cur_container_count >= max_container_items {
                continue;
            }
            let row: Option<ItemRecord> = tx
                .query_row(
                    "SELECT item_id, entity_id, prototype_id, name, icon, level, size,
                            durability, max_durability, locked, collectible_id, generator_id, created_at
                     FROM inventory_items
                     WHERE entity_id = ?1 AND item_id = ?2 AND locked = 0
                       AND item_id NOT IN (SELECT item_id FROM equipped_items WHERE entity_id = ?1)",
                    params![entity_id, id],
                    |row| {
                        let locked_i: i64 = row.get(9)?;
                        Ok(ItemRecord {
                            item_id: row.get(0)?,
                            entity_id: row.get(1)?,
                            prototype_id: row.get(2)?,
                            name: row.get(3)?,
                            icon: row.get(4)?,
                            level: row.get(5)?,
                            size: row.get(6)?,
                            durability: row.get(7)?,
                            max_durability: row.get(8)?,
                            locked: locked_i != 0,
                            collectible_id: row.get(10)?,
                            generator_id: row.get(11)?,
                            created_at: row.get(12)?,
                        })
                    },
                )
                .optional()?;
            if let Some(it) = row {
                let del = tx.execute(
                    "DELETE FROM inventory_items WHERE entity_id = ?1 AND item_id = ?2",
                    params![entity_id, id],
                )?;
                if del == 1 {
                    let json = serde_json::to_string(&it).unwrap_or_else(|_| "{}".into());
                    tx.execute(
                        "INSERT OR REPLACE INTO container_items (item_id, container_id, section_name, item_json, slot_order)
                         VALUES (?1, ?2, ?3, ?4, 0)",
                        params![it.item_id, container_id, sec, json],
                    )?;
                    cur_container_count += 1;
                    moved.push(it);
                }
            }
        }
        tx.commit()?;
        Ok(moved)
    }

    /// Atomically withdraws items from a container/warehouse section into a player's inventory
    /// in a single SQLite transaction, enforcing player inventory capacity.
    pub fn transfer_items_from_container(
        &self,
        entity_id: &str,
        container_id: &str,
        section_name: Option<&str>,
        item_ids: &[String],
        max_inv_items: usize,
    ) -> Result<Vec<ItemRecord>, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let mut cur_inv_count: usize = tx.query_row(
            "SELECT COUNT(*) FROM inventory_items WHERE entity_id = ?1",
            params![entity_id],
            |r| r.get::<_, i64>(0),
        )? as usize;

        let mut taken = Vec::new();
        let mut seen = std::collections::HashSet::new();
        for id in item_ids {
            if !seen.insert(id.clone()) || cur_inv_count >= max_inv_items {
                continue;
            }
            let row: Option<String> = if let Some(sec) = section_name {
                tx.query_row(
                    "SELECT item_json FROM container_items
                     WHERE container_id = ?1 AND section_name = ?2 AND item_id = ?3",
                    params![container_id, sec, id],
                    |r| r.get(0),
                )
                .optional()?
            } else {
                tx.query_row(
                    "SELECT item_json FROM container_items
                     WHERE container_id = ?1 AND item_id = ?2",
                    params![container_id, id],
                    |r| r.get(0),
                )
                .optional()?
            };
            if let Some(json) = row {
                let Ok(mut item) = serde_json::from_str::<ItemRecord>(&json) else {
                    continue;
                };
                let del = tx.execute(
                    "DELETE FROM container_items WHERE container_id = ?1 AND item_id = ?2",
                    params![container_id, id],
                )?;
                if del == 1 {
                    item.entity_id = entity_id.to_string();
                    item.locked = false;
                    tx.execute(
                        "INSERT INTO inventory_items (
                            item_id, entity_id, prototype_id, name, icon, level, size,
                            durability, max_durability, locked, collectible_id, generator_id, created_at
                         ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, 0, ?10, ?11, ?12)
                         ON CONFLICT(item_id) DO UPDATE SET
                            entity_id = excluded.entity_id,
                            locked = 0,
                            durability = excluded.durability",
                        params![
                            item.item_id,
                            item.entity_id,
                            item.prototype_id,
                            item.name,
                            item.icon,
                            item.level,
                            item.size,
                            item.durability,
                            item.max_durability,
                            item.collectible_id,
                            item.generator_id,
                            item.created_at,
                        ],
                    )?;
                    cur_inv_count += 1;
                    taken.push(item);
                }
            }
        }
        tx.commit()?;
        Ok(taken)
    }

    pub fn upsert_warehouse_section(
        &self,
        container_id: &str,
        section_name: &str,
    ) -> Result<bool, rusqlite::Error> {
        let trimmed = section_name.trim();
        if trimmed.is_empty() || trimmed.chars().count() > 32 {
            return Ok(false);
        }
        let conn = self.lock_conn();
        let count: i64 = conn.query_row(
            "SELECT COUNT(*) FROM warehouse_sections WHERE container_id = ?1",
            params![container_id],
            |r| r.get(0),
        )?;
        if count >= 20 {
            return Ok(false);
        }
        conn.execute(
            "INSERT OR IGNORE INTO warehouse_sections (container_id, section_name, sort_order)
             VALUES (?1, ?2, (SELECT COALESCE(MAX(sort_order), 0) + 1 FROM warehouse_sections WHERE container_id = ?1))",
            params![container_id, trimmed],
        )?;
        Ok(true)
    }

    pub fn delete_warehouse_section(
        &self,
        container_id: &str,
        section_name: &str,
    ) -> Result<bool, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let count: i64 = tx.query_row(
            "SELECT COUNT(*) FROM container_items WHERE container_id = ?1 AND section_name = ?2",
            params![container_id, section_name],
            |r| r.get(0),
        )?;
        if count > 0 || section_name == "default" {
            tx.commit()?;
            return Ok(false);
        }
        let n = tx.execute(
            "DELETE FROM warehouse_sections WHERE container_id = ?1 AND section_name = ?2",
            params![container_id, section_name],
        )?;
        tx.commit()?;
        Ok(n > 0)
    }

    pub fn rename_warehouse_section(
        &self,
        container_id: &str,
        old_name: &str,
        new_name: &str,
    ) -> Result<bool, rusqlite::Error> {
        let new_trimmed = new_name.trim();
        if old_name == "default" || new_trimmed.is_empty() || new_trimmed.chars().count() > 32 {
            return Ok(false);
        }
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let n = tx.execute(
            "UPDATE warehouse_sections SET section_name = ?3
             WHERE container_id = ?1 AND section_name = ?2",
            params![container_id, old_name, new_trimmed],
        )?;
        if n > 0 {
            tx.execute(
                "UPDATE container_items SET section_name = ?3
                 WHERE container_id = ?1 AND section_name = ?2",
                params![container_id, old_name, new_trimmed],
            )?;
        }
        tx.commit()?;
        Ok(n > 0)
    }

    pub fn move_items_in_warehouse(
        &self,
        container_id: &str,
        source_section: &str,
        target_section: &str,
        item_ids: &[String],
    ) -> Result<(), rusqlite::Error> {
        let target_trimmed = target_section.trim();
        if target_trimmed.is_empty() || target_trimmed.chars().count() > 32 {
            return Ok(());
        }
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "INSERT OR IGNORE INTO warehouse_sections (container_id, section_name, sort_order)
             VALUES (?1, ?2, 0)",
            params![container_id, target_trimmed],
        )?;
        for id in item_ids {
            tx.execute(
                "UPDATE container_items SET section_name = ?4
                 WHERE container_id = ?1 AND section_name = ?2 AND item_id = ?3",
                params![container_id, source_section, id, target_trimmed],
            )?;
        }
        tx.commit()?;
        Ok(())
    }

    // ── Market Sales, Tabs, Favorites & Payouts ──────────────────────────────

    pub fn list_seller_registered_products(
        &self,
        seller_entity_id: &str,
        now: f64,
        expired_only: bool,
    ) -> Result<Vec<MarketProductRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let sql = if expired_only {
            "SELECT product_id, seller_entity_id, region_id, item_json, price, listed_at, expires_at
             FROM market_products WHERE seller_entity_id = ?1 AND expires_at <= ?2
             ORDER BY listed_at DESC LIMIT 50"
        } else {
            "SELECT product_id, seller_entity_id, region_id, item_json, price, listed_at, expires_at
             FROM market_products WHERE seller_entity_id = ?1 AND expires_at > ?2
             ORDER BY listed_at DESC LIMIT 50"
        };
        let mut stmt = conn.prepare(sql)?;
        let rows = stmt.query_map(params![seller_entity_id, now], |row| {
            let item_json: String = row.get(3)?;
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, String>(2)?,
                item_json,
                row.get::<_, i64>(4)?,
                row.get::<_, f64>(5)?,
                row.get::<_, f64>(6)?,
            ))
        })?;
        let mut out = Vec::new();
        for r in rows {
            let (product_id, seller_entity_id, region_id, item_json, price, listed_at, expires_at) =
                r?;
            if let Ok(item) = serde_json::from_str::<ItemRecord>(&item_json) {
                out.push(MarketProductRecord {
                    product_id,
                    seller_entity_id,
                    region_id,
                    item,
                    price,
                    listed_at,
                    expires_at,
                });
            }
        }
        Ok(out)
    }

    pub fn record_market_sale(&self, sale: &MarketSaleRecord) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        let item_json = serde_json::to_string(&sale.item).unwrap_or_else(|_| "{}".into());
        conn.execute(
            "INSERT OR REPLACE INTO market_sales (
                product_id, seller_entity_id, buyer_entity_id, region_id, item_json, price, sold_at, payment_collected
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![
                sale.product_id,
                sale.seller_entity_id,
                sale.buyer_entity_id,
                sale.region_id,
                item_json,
                sale.price,
                sale.sold_at,
                if sale.payment_collected { 1i64 } else { 0i64 },
            ],
        )?;
        Ok(())
    }

    pub fn list_market_sales_for_seller(
        &self,
        seller_entity_id: &str,
    ) -> Result<Vec<MarketSaleRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT product_id, seller_entity_id, buyer_entity_id, region_id, item_json, price, sold_at, payment_collected
             FROM market_sales WHERE seller_entity_id = ?1 AND payment_collected = 0
             ORDER BY sold_at DESC LIMIT 50",
        )?;
        let rows = stmt.query_map(params![seller_entity_id], |row| {
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, String>(2)?,
                row.get::<_, String>(3)?,
                row.get::<_, String>(4)?,
                row.get::<_, i64>(5)?,
                row.get::<_, f64>(6)?,
                row.get::<_, i64>(7)? != 0,
            ))
        })?;
        let mut out = Vec::new();
        for r in rows {
            let (pid, sid, bid, rid, ij, price, sold_at, collected) = r?;
            if let Ok(item) = serde_json::from_str::<ItemRecord>(&ij) {
                out.push(MarketSaleRecord {
                    product_id: pid,
                    seller_entity_id: sid,
                    buyer_entity_id: bid,
                    region_id: rid,
                    item,
                    price,
                    sold_at,
                    payment_collected: collected,
                });
            }
        }
        Ok(out)
    }

    pub fn list_market_purchases_for_buyer(
        &self,
        buyer_entity_id: &str,
    ) -> Result<Vec<MarketSaleRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT product_id, seller_entity_id, buyer_entity_id, region_id, item_json, price, sold_at, payment_collected
             FROM market_sales WHERE buyer_entity_id = ?1
             ORDER BY sold_at DESC LIMIT 50",
        )?;
        let rows = stmt.query_map(params![buyer_entity_id], |row| {
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, String>(2)?,
                row.get::<_, String>(3)?,
                row.get::<_, String>(4)?,
                row.get::<_, i64>(5)?,
                row.get::<_, f64>(6)?,
                row.get::<_, i64>(7)? != 0,
            ))
        })?;
        let mut out = Vec::new();
        for r in rows {
            let (pid, sid, bid, rid, ij, price, sold_at, collected) = r?;
            if let Ok(item) = serde_json::from_str::<ItemRecord>(&ij) {
                out.push(MarketSaleRecord {
                    product_id: pid,
                    seller_entity_id: sid,
                    buyer_entity_id: bid,
                    region_id: rid,
                    item,
                    price,
                    sold_at,
                    payment_collected: collected,
                });
            }
        }
        Ok(out)
    }

    /// Atomically purchases a market listing in a single SQLite transaction:
    /// checks expiry, forbids self-buy, checks inventory cap, deducts buyer T-Stone,
    /// removes listing & favorites, records seller sale, and inserts item into buyer bag.
    pub fn buy_market_product(
        &self,
        buyer_id: &str,
        product_id: &str,
        max_inv_items: usize,
        now: f64,
    ) -> Result<Result<(ItemRecord, MarketProductRecord, i64), &'static str>, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let found: Option<(String, String, String, i64, f64, f64)> = tx
            .query_row(
                "SELECT seller_entity_id, region_id, item_json, price, listed_at, expires_at
                 FROM market_products WHERE product_id = ?1 AND expires_at > ?2",
                params![product_id, now],
                |row| {
                    Ok((
                        row.get(0)?,
                        row.get(1)?,
                        row.get(2)?,
                        row.get(3)?,
                        row.get(4)?,
                        row.get(5)?,
                    ))
                },
            )
            .optional()?;
        let Some((seller_entity_id, region_id, item_json, price, listed_at, expires_at)) = found
        else {
            return Ok(Err("Market listing is no longer available."));
        };
        if seller_entity_id == buyer_id {
            return Ok(Err("Cannot buy your own market listing."));
        }
        let Ok(mut item) = serde_json::from_str::<ItemRecord>(&item_json) else {
            return Ok(Err("Market listing is corrupted."));
        };
        let inv_count: usize = tx.query_row(
            "SELECT COUNT(*) FROM inventory_items WHERE entity_id = ?1",
            params![buyer_id],
            |r| r.get::<_, i64>(0),
        )? as usize;
        if inv_count >= max_inv_items {
            return Ok(Err("Inventory is full."));
        }
        let price = price.max(0);
        if price > 0 {
            let changed = tx.execute(
                "UPDATE characters SET t_stone = t_stone - ?1 WHERE entity_id = ?2 AND t_stone >= ?1",
                params![price, buyer_id],
            )?;
            if changed == 0 {
                return Ok(Err("Not enough T-Stone to purchase this item."));
            }
        }
        let new_bal: i64 = tx
            .query_row(
                "SELECT t_stone FROM characters WHERE entity_id = ?1",
                params![buyer_id],
                |r| r.get(0),
            )
            .optional()?
            .unwrap_or(50_000);
        let del = tx.execute(
            "DELETE FROM market_products WHERE product_id = ?1",
            params![product_id],
        )?;
        if del == 0 {
            return Ok(Err("Market listing is no longer available."));
        }
        tx.execute(
            "DELETE FROM market_favorites WHERE product_id = ?1",
            params![product_id],
        )?;
        let sale_item = item.clone();
        tx.execute(
            "INSERT OR REPLACE INTO market_sales (
                product_id, seller_entity_id, buyer_entity_id, region_id, item_json, price, sold_at, payment_collected
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, 0)",
            params![
                product_id,
                seller_entity_id,
                buyer_id,
                region_id,
                item_json,
                price,
                now,
            ],
        )?;
        item.entity_id = buyer_id.to_string();
        item.locked = false;
        item.created_at = now;
        tx.execute(
            "INSERT INTO inventory_items (
                item_id, entity_id, prototype_id, name, icon, level, size,
                durability, max_durability, locked, collectible_id, generator_id, created_at
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, 0, ?10, ?11, ?12)
             ON CONFLICT(item_id) DO UPDATE SET
                entity_id = excluded.entity_id,
                locked = 0,
                created_at = excluded.created_at",
            params![
                item.item_id,
                item.entity_id,
                item.prototype_id,
                item.name,
                item.icon,
                item.level,
                item.size,
                item.durability,
                item.max_durability,
                item.collectible_id,
                item.generator_id,
                item.created_at,
            ],
        )?;
        tx.commit()?;
        let prod = MarketProductRecord {
            product_id: product_id.to_string(),
            seller_entity_id,
            region_id,
            item: sale_item,
            price,
            listed_at,
            expires_at,
        };
        Ok(Ok((item, prod, new_bal)))
    }

    /// Atomically lists owned, unlocked, unequipped items on the market in a single SQLite transaction.
    /// Deletes each item before inserting into `market_products`, enforcing seller active listing cap.
    pub fn register_market_products(
        &self,
        seller_id: &str,
        region_id: &str,
        item_ids: &[String],
        each_price: i64,
        duration_hours: f32,
        max_active_listings: usize,
        now: f64,
    ) -> Result<Vec<(String, String)>, rusqlite::Error> {
        let expires_at = now + f64::from(duration_hours.clamp(1.0, 168.0)) * 3600.0;
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let mut active_count: usize = tx.query_row(
            "SELECT COUNT(*) FROM market_products WHERE seller_entity_id = ?1 AND expires_at > ?2",
            params![seller_id, now],
            |r| r.get::<_, i64>(0),
        )? as usize;

        let mut listed = Vec::new();
        let mut seen = std::collections::HashSet::new();
        for iid in item_ids {
            if !seen.insert(iid.clone()) || active_count >= max_active_listings {
                continue;
            }
            let row: Option<ItemRecord> = tx
                .query_row(
                    "SELECT item_id, entity_id, prototype_id, name, icon, level, size,
                            durability, max_durability, locked, collectible_id, generator_id, created_at
                     FROM inventory_items
                     WHERE entity_id = ?1 AND item_id = ?2 AND locked = 0
                       AND item_id NOT IN (SELECT item_id FROM equipped_items WHERE entity_id = ?1)",
                    params![seller_id, iid],
                    |row| {
                        let locked_i: i64 = row.get(9)?;
                        Ok(ItemRecord {
                            item_id: row.get(0)?,
                            entity_id: row.get(1)?,
                            prototype_id: row.get(2)?,
                            name: row.get(3)?,
                            icon: row.get(4)?,
                            level: row.get(5)?,
                            size: row.get(6)?,
                            durability: row.get(7)?,
                            max_durability: row.get(8)?,
                            locked: locked_i != 0,
                            collectible_id: row.get(10)?,
                            generator_id: row.get(11)?,
                            created_at: row.get(12)?,
                        })
                    },
                )
                .optional()?;
            if let Some(it) = row {
                let del = tx.execute(
                    "DELETE FROM inventory_items WHERE entity_id = ?1 AND item_id = ?2",
                    params![seller_id, iid],
                )?;
                if del == 1 {
                    let pid = uuid::Uuid::new_v4().to_string();
                    let item_json = serde_json::to_string(&it).unwrap_or_else(|_| "{}".into());
                    tx.execute(
                        "INSERT INTO market_products (
                             product_id, seller_entity_id, region_id, item_json, prototype_id,
                             item_name, item_level, price, listed_at, expires_at
                         ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10)",
                        params![
                            pid,
                            seller_id,
                            region_id,
                            item_json,
                            it.prototype_id,
                            it.name,
                            it.level,
                            each_price,
                            now,
                            expires_at,
                        ],
                    )?;
                    active_count += 1;
                    listed.push((iid.clone(), it.name));
                }
            }
        }
        tx.commit()?;
        Ok(listed)
    }

    /// Atomically marks one (or all, if `product_id` is None) uncollected seller sales as collected
    /// and credits the seller's T-Stone balance in a single SQLite transaction.
    pub fn collect_market_payments(
        &self,
        seller_entity_id: &str,
        product_id: Option<&str>,
    ) -> Result<(i64, i64), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let payout: i64 = if let Some(pid) = product_id {
            let amt: Option<i64> = tx
                .query_row(
                    "SELECT price FROM market_sales
                     WHERE product_id = ?1 AND seller_entity_id = ?2 AND payment_collected = 0",
                    params![pid, seller_entity_id],
                    |r| r.get(0),
                )
                .optional()?;
            if let Some(p) = amt {
                tx.execute(
                    "UPDATE market_sales SET payment_collected = 1
                     WHERE product_id = ?1 AND seller_entity_id = ?2 AND payment_collected = 0",
                    params![pid, seller_entity_id],
                )?;
                p.max(0)
            } else {
                0
            }
        } else {
            let sum: i64 = tx.query_row(
                "SELECT COALESCE(SUM(MAX(price, 0)), 0) FROM market_sales
                 WHERE seller_entity_id = ?1 AND payment_collected = 0",
                params![seller_entity_id],
                |r| r.get(0),
            )?;
            tx.execute(
                "UPDATE market_sales SET payment_collected = 1
                 WHERE seller_entity_id = ?1 AND payment_collected = 0",
                params![seller_entity_id],
            )?;
            sum.max(0)
        };
        if payout > 0 {
            tx.execute(
                "UPDATE characters SET t_stone = MIN(t_stone + ?1, 9000000000000) WHERE entity_id = ?2",
                params![payout, seller_entity_id],
            )?;
        }
        let new_bal: i64 = tx
            .query_row(
                "SELECT t_stone FROM characters WHERE entity_id = ?1",
                params![seller_entity_id],
                |r| r.get(0),
            )
            .optional()?
            .unwrap_or(50_000);
        tx.commit()?;
        Ok((payout, new_bal))
    }

    pub fn set_market_favorite(
        &self,
        entity_id: &str,
        product_id: &str,
        fav: bool,
    ) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        if fav {
            conn.execute(
                "INSERT OR IGNORE INTO market_favorites (entity_id, product_id) VALUES (?1, ?2)",
                params![entity_id, product_id],
            )?;
        } else {
            conn.execute(
                "DELETE FROM market_favorites WHERE entity_id = ?1 AND product_id = ?2",
                params![entity_id, product_id],
            )?;
        }
        Ok(())
    }

    pub fn list_favorite_products(
        &self,
        entity_id: &str,
        now: f64,
    ) -> Result<Vec<MarketProductRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT mp.product_id, mp.seller_entity_id, mp.region_id, mp.item_json, mp.price, mp.listed_at, mp.expires_at
             FROM market_favorites mf
             JOIN market_products mp ON mp.product_id = mf.product_id
             WHERE mf.entity_id = ?1 AND mp.expires_at > ?2
             ORDER BY mp.listed_at DESC LIMIT 50",
        )?;
        let rows = stmt.query_map(params![entity_id, now], |row| {
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, String>(2)?,
                row.get::<_, String>(3)?,
                row.get::<_, i64>(4)?,
                row.get::<_, f64>(5)?,
                row.get::<_, f64>(6)?,
            ))
        })?;
        let mut out = Vec::new();
        for r in rows {
            let (pid, sid, rid, ij, price, listed_at, expires_at) = r?;
            if let Ok(item) = serde_json::from_str::<ItemRecord>(&ij) {
                out.push(MarketProductRecord {
                    product_id: pid,
                    seller_entity_id: sid,
                    region_id: rid,
                    item,
                    price,
                    listed_at,
                    expires_at,
                });
            }
        }
        Ok(out)
    }

    // ── In-Game Mail ─────────────────────────────────────────────────────────

    pub fn insert_mail(&self, mail: &MailRecord) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        let items_json = serde_json::to_string(&mail.items).unwrap_or_else(|_| "[]".into());
        conn.execute(
            "INSERT INTO mails (
                mail_id, recipient_entity_id, sender_entity_id, is_user_mail, mail_type,
                text, t_stone, items_json, accepted, is_read, sent_at, expires_at
             ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12)",
            params![
                mail.mail_id,
                mail.recipient_entity_id,
                mail.sender_entity_id,
                if mail.is_user_mail { 1i64 } else { 0i64 },
                mail.mail_type,
                mail.text,
                mail.t_stone,
                items_json,
                if mail.accepted { 1i64 } else { 0i64 },
                if mail.read { 1i64 } else { 0i64 },
                mail.sent_at,
                mail.expires_at,
            ],
        )?;
        Ok(())
    }

    pub fn send_mail_atomic(
        &self,
        sender_id: &str,
        recipient_id: &str,
        mail_id: &str,
        text: &str,
        attach_item_ids: &[String],
        now: f64,
        expires_at: Option<f64>,
    ) -> Result<Vec<String>, rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let mut attached = Vec::new();
        let mut removed_ids = Vec::new();
        let mut seen = std::collections::HashSet::new();
        for iid in attach_item_ids {
            if !seen.insert(iid.clone()) {
                continue;
            }
            let row: Option<ItemRecord> = tx
                .query_row(
                    "SELECT item_id, entity_id, prototype_id, name, icon, level, size,
                            durability, max_durability, locked, collectible_id, generator_id, created_at
                     FROM inventory_items
                     WHERE entity_id = ?1 AND item_id = ?2 AND locked = 0
                       AND item_id NOT IN (SELECT item_id FROM equipped_items WHERE entity_id = ?1)",
                    params![sender_id, iid],
                    |row| {
                        let locked_i: i64 = row.get(9)?;
                        Ok(ItemRecord {
                            item_id: row.get(0)?,
                            entity_id: row.get(1)?,
                            prototype_id: row.get(2)?,
                            name: row.get(3)?,
                            icon: row.get(4)?,
                            level: row.get(5)?,
                            size: row.get(6)?,
                            durability: row.get(7)?,
                            max_durability: row.get(8)?,
                            locked: locked_i != 0,
                            collectible_id: row.get(10)?,
                            generator_id: row.get(11)?,
                            created_at: row.get(12)?,
                        })
                    },
                )
                .optional()?;
            let Some(it) = row else {
                return Err(rusqlite::Error::QueryReturnedNoRows);
            };
            let del = tx.execute(
                "DELETE FROM inventory_items WHERE entity_id = ?1 AND item_id = ?2",
                params![sender_id, iid],
            )?;
            if del != 1 {
                return Err(rusqlite::Error::QueryReturnedNoRows);
            }
            attached.push(it);
            removed_ids.push(iid.clone());
        }
        let items_json = serde_json::to_string(&attached).unwrap_or_else(|_| "[]".into());
        tx.execute(
            "INSERT INTO mails (
                mail_id, recipient_entity_id, sender_entity_id, is_user_mail, mail_type,
                text, t_stone, items_json, accepted, is_read, sent_at, expires_at
             ) VALUES (?1, ?2, ?3, 1, 0, ?4, 0, ?5, 0, 0, ?6, ?7)",
            params![
                mail_id,
                recipient_id,
                sender_id,
                text,
                items_json,
                now,
                expires_at,
            ],
        )?;
        tx.commit()?;
        Ok(removed_ids)
    }

    pub fn list_mails(&self, recipient_entity_id: &str) -> Result<Vec<MailRecord>, rusqlite::Error> {
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map_or(0.0, |d| d.as_secs_f64());
        self.list_mails_unexpired(recipient_entity_id, now)
    }

    pub fn list_mails_unexpired(
        &self,
        recipient_entity_id: &str,
        now: f64,
    ) -> Result<Vec<MailRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT mail_id, recipient_entity_id, sender_entity_id, is_user_mail, mail_type,
                    text, t_stone, items_json, accepted, is_read, sent_at, expires_at
             FROM mails
             WHERE recipient_entity_id = ?1
               AND (expires_at IS NULL OR expires_at > ?2)
             ORDER BY sent_at DESC LIMIT 100",
        )?;
        let rows = stmt.query_map(params![recipient_entity_id, now], |row| {
            let ij: String = row.get(7)?;
            Ok(MailRecord {
                mail_id: row.get(0)?,
                recipient_entity_id: row.get(1)?,
                sender_entity_id: row.get(2)?,
                is_user_mail: row.get::<_, i64>(3)? != 0,
                mail_type: row.get(4)?,
                text: row.get(5)?,
                t_stone: row.get(6)?,
                items: serde_json::from_str(&ij).unwrap_or_default(),
                accepted: row.get::<_, i64>(8)? != 0,
                read: row.get::<_, i64>(9)? != 0,
                sent_at: row.get(10)?,
                expires_at: row.get(11)?,
            })
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn accept_mails(
        &self,
        recipient_entity_id: &str,
        mail_ids: &[String],
        is_user_mail: bool,
        max_inv_items: usize,
        now: f64,
    ) -> Result<(Vec<ItemRecord>, i64, i64), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let mut cur_inv_count: usize = tx.query_row(
            "SELECT COUNT(*) FROM inventory_items WHERE entity_id = ?1",
            params![recipient_entity_id],
            |r| r.get::<_, i64>(0),
        )? as usize;

        let mut items_out = Vec::new();
        let mut total_tstone = 0i64;
        let user_flag = if is_user_mail { 1i64 } else { 0i64 };
        for mid in mail_ids {
            let row: Option<(i32, String)> = tx
                .query_row(
                    "SELECT t_stone, items_json FROM mails
                     WHERE mail_id = ?1 AND recipient_entity_id = ?2 AND accepted = 0
                       AND is_user_mail = ?3
                       AND (expires_at IS NULL OR expires_at > ?4)",
                    params![mid, recipient_entity_id, user_flag, now],
                    |r| Ok((r.get(0)?, r.get(1)?)),
                )
                .optional()?;
            if let Some((ts, ij)) = row {
                let mut list = serde_json::from_str::<Vec<ItemRecord>>(&ij).unwrap_or_default();
                if cur_inv_count + list.len() > max_inv_items {
                    continue;
                }
                tx.execute(
                    "UPDATE mails SET accepted = 1, is_read = 1
                     WHERE mail_id = ?1 AND recipient_entity_id = ?2 AND accepted = 0",
                    params![mid, recipient_entity_id],
                )?;
                total_tstone += i64::from(ts.max(0));
                for it in &mut list {
                    it.entity_id = recipient_entity_id.to_string();
                    it.locked = false;
                    tx.execute(
                        "INSERT INTO inventory_items (
                            item_id, entity_id, prototype_id, name, icon, level, size,
                            durability, max_durability, locked, collectible_id, generator_id, created_at
                         ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, 0, ?10, ?11, ?12)
                         ON CONFLICT(item_id) DO UPDATE SET
                            entity_id = excluded.entity_id,
                            locked = 0",
                        params![
                            it.item_id,
                            it.entity_id,
                            it.prototype_id,
                            it.name,
                            it.icon,
                            it.level,
                            it.size,
                            it.durability,
                            it.max_durability,
                            it.collectible_id,
                            it.generator_id,
                            it.created_at,
                        ],
                    )?;
                }
                cur_inv_count += list.len();
                items_out.extend(list);
            }
        }
        if total_tstone > 0 {
            tx.execute(
                "UPDATE characters SET t_stone = MIN(t_stone + ?1, 9000000000000) WHERE entity_id = ?2",
                params![total_tstone, recipient_entity_id],
            )?;
        }
        let new_bal: i64 = tx
            .query_row(
                "SELECT t_stone FROM characters WHERE entity_id = ?1",
                params![recipient_entity_id],
                |r| r.get(0),
            )
            .optional()?
            .unwrap_or(50_000);
        tx.commit()?;
        Ok((items_out, total_tstone, new_bal))
    }

    pub fn delete_mails(
        &self,
        recipient_entity_id: &str,
        mail_ids: &[String],
        is_user_mail: bool,
    ) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        let user_flag = if is_user_mail { 1i64 } else { 0i64 };
        for mid in mail_ids {
            tx.execute(
                "DELETE FROM mails
                 WHERE mail_id = ?1 AND recipient_entity_id = ?2 AND is_user_mail = ?3
                   AND (accepted = 1 OR (t_stone <= 0 AND (items_json = '[]' OR items_json = '')))",
                params![mid, recipient_entity_id, user_flag],
            )?;
        }
        tx.commit()?;
        Ok(())
    }

    pub fn mark_mails_read(
        &self,
        recipient_entity_id: &str,
        mail_ids: &[String],
    ) -> Result<(), rusqlite::Error> {
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        for mid in mail_ids {
            tx.execute(
                "UPDATE mails SET is_read = 1 WHERE mail_id = ?1 AND recipient_entity_id = ?2",
                params![mid, recipient_entity_id],
            )?;
        }
        tx.commit()?;
        Ok(())
    }

    // -------------------------------------------------------------------------
    // Community Moderation: 3-Layer Blacklist (Account / Character / IP+CIDR / Mute)
    // and Live Server Settings
    // -------------------------------------------------------------------------

    pub fn update_character_last_ip(&self, entity_id: &str, ip: &str) -> Result<(), rusqlite::Error> {
        if ip.is_empty() {
            return Ok(());
        }
        let conn = self.lock_conn();
        conn.execute(
            "UPDATE characters SET last_ip = ?1 WHERE entity_id = ?2",
            params![ip, entity_id],
        )?;
        Ok(())
    }

    pub fn list_recent_characters(
        &self,
        limit: usize,
    ) -> Result<Vec<PlayerSummaryRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT entity_id, owner_key, name, level, region_id, last_ip, disconnected_at, t_stone
             FROM characters
             ORDER BY disconnected_at DESC, created_at DESC
             LIMIT ?1",
        )?;
        let rows = stmt.query_map(params![limit.clamp(1, 500) as i64], |r| {
            Ok(PlayerSummaryRecord {
                entity_id: r.get(0)?,
                owner_key: r.get(1)?,
                name: r.get(2)?,
                level: r.get(3)?,
                region_id: r.get(4)?,
                last_ip: r.get(5)?,
                disconnected_at: r.get(6)?,
                t_stone: r.get::<_, i64>(7)?,
            })
        })?;
        let mut out = Vec::new();
        for row in rows {
            out.push(row?);
        }
        Ok(out)
    }

    pub fn resolve_character_by_name_or_id(
        &self,
        target: &str,
    ) -> Result<Option<PlayerSummaryRecord>, rusqlite::Error> {
        let needle = target.trim();
        if needle.is_empty() {
            return Ok(None);
        }
        let conn = self.lock_conn();
        conn.query_row(
            "SELECT entity_id, owner_key, name, level, region_id, last_ip, disconnected_at, t_stone
             FROM characters
             WHERE entity_id = ?1 OR name = ?1 COLLATE NOCASE
             LIMIT 1",
            params![needle],
            |r| {
                Ok(PlayerSummaryRecord {
                    entity_id: r.get(0)?,
                    owner_key: r.get(1)?,
                    name: r.get(2)?,
                    level: r.get(3)?,
                    region_id: r.get(4)?,
                    last_ip: r.get(5)?,
                    disconnected_at: r.get(6)?,
                    t_stone: r.get::<_, i64>(7)?,
                })
            },
        )
        .optional()
    }

    pub fn get_t_stone_batch(
        &self,
        entity_ids: &[String],
    ) -> Result<std::collections::HashMap<String, i64>, rusqlite::Error> {
        let mut out = std::collections::HashMap::new();
        if entity_ids.is_empty() {
            return Ok(out);
        }
        let conn = self.lock_conn();
        let mut stmt = conn.prepare("SELECT t_stone FROM characters WHERE entity_id = ?1")?;
        for eid in entity_ids {
            if let Some(ts) = stmt
                .query_row(params![eid], |r| r.get::<_, i64>(0))
                .optional()?
            {
                out.insert(eid.clone(), ts.max(0));
            }
        }
        Ok(out)
    }

    pub fn count_active_bans(&self, now: f64) -> Result<usize, rusqlite::Error> {
        let conn = self.lock_conn();
        let c: i64 = conn.query_row(
            "SELECT COUNT(*) FROM bans WHERE expires_at IS NULL OR expires_at > ?1",
            params![now],
            |r| r.get(0),
        )?;
        Ok(c.max(0) as usize)
    }

    pub fn count_audit_logs_since(&self, min_ts: f64) -> Result<usize, rusqlite::Error> {
        let conn = self.lock_conn();
        let c: i64 = conn.query_row(
            "SELECT COUNT(*) FROM audit_logs WHERE ts >= ?1",
            params![min_ts],
            |r| r.get(0),
        )?;
        Ok(c.max(0) as usize)
    }

    pub fn search_characters_with_clans(
        &self,
        needle: &str,
    ) -> Result<Vec<(CharacterRecord, Option<(String, String, i32)>)>, rusqlite::Error> {
        let conn = self.lock_conn();
        let pattern = format!("%{}%", escape_like_pattern(needle));
        let mut stmt = conn.prepare(
            "SELECT c.entity_id, c.owner_key, c.name, c.job, c.level, c.region_id, c.terrain_id,
                    c.pos_x, c.pos_y, c.disconnected_at, c.created_at, c.gender, c.model_info,
                    c.exp, c.untrained_count, c.life, c.stamina, c.energy, c.fatigue,
                    cl.clan_id, cl.name, cm.role_id
             FROM characters c
             LEFT JOIN clan_members cm ON cm.entity_id = c.entity_id
             LEFT JOIN clans cl ON cl.clan_id = cm.clan_id
             WHERE LOWER(c.name) LIKE LOWER(?1) ESCAPE '\\'
             LIMIT 20",
        )?;
        let rows = stmt.query_map(params![pattern], |row| {
            let ch = CharacterRecord {
                entity_id: row.get(0)?,
                owner_key: row.get(1)?,
                name: row.get(2)?,
                job: row.get(3)?,
                level: row.get(4)?,
                region_id: row.get(5)?,
                terrain_id: row.get(6)?,
                pos_x: row.get(7)?,
                pos_y: row.get(8)?,
                disconnected_at: row.get(9)?,
                created_at: row.get(10)?,
                gender: row.get(11)?,
                model_info: row.get(12)?,
                exp: row.get(13)?,
                untrained_count: row.get(14)?,
                life: row.get(15)?,
                stamina: row.get(16)?,
                energy: row.get(17)?,
                fatigue: row.get(18)?,
            };
            let cid: Option<String> = row.get(19)?;
            let cname: Option<String> = row.get(20)?;
            let role: Option<i32> = row.get(21)?;
            let clan = match (cid, cname, role) {
                (Some(id), Some(nm), Some(rl)) => Some((id, nm, rl)),
                _ => None,
            };
            Ok((ch, clan))
        })?;
        let mut out = Vec::new();
        for r in rows {
            out.push(r?);
        }
        Ok(out)
    }

    pub fn add_ban(
        &self,
        ban_type: &str,
        target_value: &str,
        reason: &str,
        banned_by: &str,
        created_at: f64,
        expires_at: Option<f64>,
    ) -> Result<i64, rusqlite::Error> {
        let bt = ban_type.trim().to_ascii_lowercase();
        let tv = target_value.trim().to_ascii_lowercase();
        let mut conn = self.lock_conn();
        let tx = conn.transaction()?;
        tx.execute(
            "DELETE FROM bans WHERE ban_type = ?1 AND target_value = ?2",
            params![bt, tv],
        )?;
        tx.execute(
            "INSERT INTO bans (ban_type, target_value, reason, banned_by, created_at, expires_at)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![bt, tv, reason.trim(), banned_by.trim(), created_at, expires_at],
        )?;
        let id = tx.last_insert_rowid();
        tx.commit()?;
        Ok(id)
    }

    pub fn remove_ban_by_id(&self, id: i64) -> Result<bool, rusqlite::Error> {
        let conn = self.lock_conn();
        let n = conn.execute("DELETE FROM bans WHERE id = ?1", params![id])?;
        Ok(n > 0)
    }

    pub fn remove_bans_by_target(&self, target: &str) -> Result<usize, rusqlite::Error> {
        let tv = target.trim().to_ascii_lowercase();
        let conn = self.lock_conn();
        let n = conn.execute(
            "DELETE FROM bans WHERE target_value = ?1 COLLATE NOCASE",
            params![tv],
        )?;
        Ok(n)
    }

    pub fn list_active_bans(&self, now: f64) -> Result<Vec<BanRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let _ = conn.execute(
            "DELETE FROM bans WHERE expires_at IS NOT NULL AND expires_at <= ?1",
            params![now],
        );
        let mut stmt = conn.prepare(
            "SELECT id, ban_type, target_value, reason, banned_by, created_at, expires_at
             FROM bans
             ORDER BY id DESC",
        )?;
        let rows = stmt.query_map([], |r| {
            Ok(BanRecord {
                id: r.get(0)?,
                ban_type: r.get(1)?,
                target_value: r.get(2)?,
                reason: r.get(3)?,
                banned_by: r.get(4)?,
                created_at: r.get(5)?,
                expires_at: r.get(6)?,
            })
        })?;
        let mut out = Vec::new();
        for row in rows {
            out.push(row?);
        }
        Ok(out)
    }

    fn list_active_bans_readonly(&self, now: f64) -> Result<Vec<BanRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let mut stmt = conn.prepare(
            "SELECT id, ban_type, target_value, reason, banned_by, created_at, expires_at
             FROM bans
             WHERE expires_at IS NULL OR expires_at > ?1
             ORDER BY id DESC",
        )?;
        let rows = stmt.query_map(params![now], |r| {
            Ok(BanRecord {
                id: r.get(0)?,
                ban_type: r.get(1)?,
                target_value: r.get(2)?,
                reason: r.get(3)?,
                banned_by: r.get(4)?,
                created_at: r.get(5)?,
                expires_at: r.get(6)?,
            })
        })?;
        let mut out = Vec::new();
        for row in rows {
            out.push(row?);
        }
        Ok(out)
    }

    pub fn check_ban(
        &self,
        owner_key: Option<&str>,
        entity_id: Option<&str>,
        name: Option<&str>,
        ip: Option<&str>,
        now: f64,
    ) -> Option<BanRecord> {
        let bans = self.list_active_bans_readonly(now).ok()?;
        let ok_l = owner_key.map(|s| s.trim().to_ascii_lowercase());
        let eid_l = entity_id.map(|s| s.trim().to_ascii_lowercase());
        let name_l = name.map(|s| s.trim().to_ascii_lowercase());
        let ip_clean = ip.map(str::trim).filter(|s| !s.is_empty());

        for b in bans {
            match b.ban_type.as_str() {
                "account" => {
                    if ok_l.as_deref() == Some(b.target_value.as_str()) {
                        return Some(b);
                    }
                }
                "entity" => {
                    if eid_l.as_deref() == Some(b.target_value.as_str()) {
                        return Some(b);
                    }
                }
                "name" => {
                    if name_l.as_deref() == Some(b.target_value.as_str()) {
                        return Some(b);
                    }
                }
                "ip" => {
                    if let Some(client_ip) = ip_clean {
                        if ip_matches_pattern(client_ip, &b.target_value) {
                            return Some(b);
                        }
                    }
                }
                _ => {}
            }
        }
        None
    }

    pub fn check_mute(
        &self,
        owner_key: Option<&str>,
        entity_id: Option<&str>,
        now: f64,
    ) -> Option<BanRecord> {
        let bans = self.list_active_bans_readonly(now).ok()?;
        let ok_l = owner_key.map(|s| s.trim().to_ascii_lowercase());
        let eid_l = entity_id.map(|s| s.trim().to_ascii_lowercase());
        for b in bans {
            if b.ban_type == "mute"
                && (ok_l.as_deref() == Some(b.target_value.as_str())
                    || eid_l.as_deref() == Some(b.target_value.as_str()))
            {
                return Some(b);
            }
        }
        None
    }

    pub fn get_setting(&self, key: &str) -> Result<Option<String>, rusqlite::Error> {
        let conn = self.lock_conn();
        conn.query_row(
            "SELECT value FROM server_settings WHERE key = ?1",
            params![key],
            |r| r.get(0),
        )
        .optional()
    }

    pub fn set_setting(&self, key: &str, value: &str, now: f64) -> Result<(), rusqlite::Error> {
        let conn = self.lock_conn();
        conn.execute(
            "INSERT INTO server_settings (key, value, updated_at) VALUES (?1, ?2, ?3)
             ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at",
            params![key, value, now],
        )?;
        Ok(())
    }

    pub fn log_audit(
        &self,
        ts: f64,
        category: &str,
        actor: &str,
        actor_ip: &str,
        region_id: &str,
        summary: &str,
    ) {
        {
            let conn = self.lock_conn();
            let _ = conn.execute(
                "INSERT INTO audit_logs (ts, category, actor, actor_ip, region_id, summary)
                 VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
                params![ts, category, actor, actor_ip, region_id, summary],
            );
            // Prune hot SQLite table only every 100 inserts instead of every single event.
            let n = self.audit_insert_count.fetch_add(1, Ordering::Relaxed);
            if n % 100 == 0 {
                let _ = conn.execute(
                    "DELETE FROM audit_logs WHERE id < (SELECT COALESCE(MAX(id), 0) - ?1 FROM audit_logs)",
                    params![AUDIT_HOT_MAX_ROWS],
                );
            }
        }
        if let Some(tx) = &self.audit_tx {
            let _ = tx.try_send((
                ts,
                category.to_string(),
                actor.to_string(),
                actor_ip.to_string(),
                region_id.to_string(),
                summary.to_string(),
            ));
        }
    }

    fn rotate_and_gzip_audit(dir: &Path, active_path: &Path, ts_secs: u64) {
        let Ok(raw) = fs::read(active_path) else {
            return;
        };
        let gz_path = dir.join(format!("audit-{ts_secs}.jsonl.gz"));
        if let Ok(out_file) = fs::File::create(&gz_path) {
            let mut enc = GzEncoder::new(out_file, Compression::default());
            if enc.write_all(&raw).is_ok() && enc.finish().is_ok() {
                let _ = fs::write(active_path, b"");
            }
        }
        // Enforce bounded retention: keep only the newest AUDIT_MAX_ARCHIVES .jsonl.gz files
        if let Ok(rd) = fs::read_dir(dir) {
            let mut archives: Vec<PathBuf> = rd
                .flatten()
                .map(|e| e.path())
                .filter(|p| {
                    p.file_name()
                        .and_then(|n| n.to_str())
                        .is_some_and(|n| n.starts_with("audit-") && n.ends_with(".jsonl.gz"))
                })
                .collect();
            archives.sort();
            if archives.len() > AUDIT_MAX_ARCHIVES {
                let drop_count = archives.len() - AUDIT_MAX_ARCHIVES;
                for old in archives.into_iter().take(drop_count) {
                    let _ = fs::remove_file(old);
                }
            }
        }
    }

    /// Returns `(archive_file_count, total_log_bytes_on_disk)` across active + gzipped archives.
    pub fn audit_disk_stats(&self) -> (usize, u64) {
        let Some(dir) = &self.audit_log_dir else {
            return (0, 0);
        };
        let mut archives = 0usize;
        let mut bytes = 0u64;
        if let Ok(rd) = fs::read_dir(dir) {
            for entry in rd.flatten() {
                let p = entry.path();
                if let Ok(meta) = entry.metadata() {
                    bytes = bytes.saturating_add(meta.len());
                }
                if p.file_name()
                    .and_then(|n| n.to_str())
                    .is_some_and(|n| n.ends_with(".jsonl.gz"))
                {
                    archives += 1;
                }
            }
        }
        (archives, bytes)
    }

    pub fn list_audit_logs(
        &self,
        category: Option<&str>,
        search: Option<&str>,
        limit: usize,
    ) -> Result<Vec<AuditLogRecord>, rusqlite::Error> {
        let conn = self.lock_conn();
        let lim = limit.clamp(1, 500) as i64;
        let cat = category
            .map(str::trim)
            .filter(|s| !s.is_empty() && !s.eq_ignore_ascii_case("all"));
        let q = search.map(str::trim).filter(|s| !s.is_empty());

        let mut sql = String::from(
            "SELECT id, ts, category, actor, actor_ip, region_id, summary FROM audit_logs WHERE 1=1",
        );
        if cat.is_some() {
            sql.push_str(" AND category = ?1 COLLATE NOCASE");
        }
        if q.is_some() {
            let idx = if cat.is_some() { 2 } else { 1 };
            sql.push_str(&format!(
                " AND (summary LIKE ?{idx} ESCAPE '\\' OR actor LIKE ?{idx} ESCAPE '\\' OR actor_ip LIKE ?{idx} ESCAPE '\\' OR region_id LIKE ?{idx} ESCAPE '\\')"
            ));
        }
        sql.push_str(" ORDER BY id DESC LIMIT ");
        sql.push_str(&lim.to_string());

        let mut stmt = conn.prepare(&sql)?;
        let map_row = |r: &rusqlite::Row<'_>| -> Result<AuditLogRecord, rusqlite::Error> {
            Ok(AuditLogRecord {
                id: r.get(0)?,
                ts: r.get(1)?,
                category: r.get(2)?,
                actor: r.get(3)?,
                actor_ip: r.get(4)?,
                region_id: r.get(5)?,
                summary: r.get(6)?,
            })
        };

        let mut out = Vec::new();
        match (cat, q) {
            (Some(c), Some(s)) => {
                let pat = format!("%{}%", escape_like_pattern(s));
                let rows = stmt.query_map(params![c, pat], map_row)?;
                for r in rows {
                    out.push(r?);
                }
            }
            (Some(c), None) => {
                let rows = stmt.query_map(params![c], map_row)?;
                for r in rows {
                    out.push(r?);
                }
            }
            (None, Some(s)) => {
                let pat = format!("%{}%", escape_like_pattern(s));
                let rows = stmt.query_map(params![pat], map_row)?;
                for r in rows {
                    out.push(r?);
                }
            }
            (None, None) => {
                let rows = stmt.query_map([], map_row)?;
                for r in rows {
                    out.push(r?);
                }
            }
        }
        Ok(out)
    }
}

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct AuditLogRecord {
    pub id: i64,
    pub ts: f64,
    pub category: String,
    pub actor: String,
    pub actor_ip: String,
    pub region_id: String,
    pub summary: String,
}

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct BanRecord {
    pub id: i64,
    pub ban_type: String,
    pub target_value: String,
    pub reason: String,
    pub banned_by: String,
    pub created_at: f64,
    pub expires_at: Option<f64>,
}

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub struct PlayerSummaryRecord {
    pub entity_id: String,
    pub owner_key: String,
    pub name: String,
    pub level: i32,
    pub region_id: String,
    pub last_ip: String,
    pub disconnected_at: f64,
    pub t_stone: i64,
}

/// Match an IP address against either an exact IP (`203.0.113.5`) or a CIDR subnet (`203.0.113.0/24`).
pub fn ip_matches_pattern(ip_str: &str, pattern: &str) -> bool {
    let pat = pattern.trim();
    if pat.is_empty() {
        return false;
    }
    if ip_str.eq_ignore_ascii_case(pat) {
        return true;
    }
    let Ok(addr) = ip_str.parse::<std::net::IpAddr>() else {
        return false;
    };
    if let Some((net_s, prefix_s)) = pat.split_once('/') {
        let (Ok(net_addr), Ok(prefix)) = (net_s.parse::<std::net::IpAddr>(), prefix_s.parse::<u32>())
        else {
            return false;
        };
        match (addr, net_addr) {
            (std::net::IpAddr::V4(a), std::net::IpAddr::V4(n)) if prefix <= 32 => {
                let mask = if prefix == 0 {
                    0u32
                } else {
                    u32::MAX << (32 - prefix)
                };
                (u32::from(a) & mask) == (u32::from(n) & mask)
            }
            (std::net::IpAddr::V6(a), std::net::IpAddr::V6(n)) if prefix <= 128 => {
                let mask = if prefix == 0 {
                    0u128
                } else {
                    u128::MAX << (128 - prefix)
                };
                (u128::from(a) & mask) == (u128::from(n) & mask)
            }
            _ => false,
        }
    } else {
        pat.parse::<std::net::IpAddr>().is_ok_and(|p| p == addr)
    }
}

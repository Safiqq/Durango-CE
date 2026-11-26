use std::collections::{HashMap, HashSet};
use std::fs::File;
use std::io::Read;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use axum::body::Bytes;
use serde_json::{json, Value};
use tokio::sync::RwLock;

pub const DEFAULT_TERRAIN_ID: &str = "pe10gr_1";

#[derive(Debug, Clone, Default)]
pub struct TerrainPois {
    pub port_points: Vec<(u16, u16)>,
    pub warpholes: Vec<(u16, u16)>,
    pub rifts: Vec<(u16, u16)>,
    pub craters: Vec<(u16, u16)>,
}

impl TerrainPois {
    /// Parses `pois.yml` from a Durango terrain zip (supports both `- - x\n  - y` and `- [x, y]`).
    pub fn parse_yaml(raw: &str) -> Self {
        let mut pois = Self::default();
        let mut section: Option<&str> = None;
        let mut pending_x: Option<u16> = None;

        for line in raw.lines() {
            let trimmed = line.trim();
            if trimmed.is_empty() || trimmed.starts_with('#') {
                continue;
            }
            if !line.starts_with(' ') && !line.starts_with('-') && trimmed.contains(':') {
                pending_x = None;
                let key = trimmed.split(':').next().unwrap_or("").trim();
                section = match key {
                    "port_points" => Some("port_points"),
                    "warpholes" => Some("warpholes"),
                    "rifts" => Some("rifts"),
                    "craters" => Some("craters"),
                    _ => None,
                };
                continue;
            }
            let Some(sec) = section else {
                continue;
            };
            // Inline pair: `- [63, 71]`
            if let Some(bracket) = trimmed.strip_prefix("- [").and_then(|s| s.strip_suffix(']')) {
                let mut parts = bracket.split(',').filter_map(|p| p.trim().parse::<u16>().ok());
                if let (Some(x), Some(y)) = (parts.next(), parts.next()) {
                    Self::push_point(&mut pois, sec, (x, y));
                }
                pending_x = None;
                continue;
            }
            // Nested YAML pair: `- - 63` followed by `- 71`
            if let Some(rest) = trimmed.strip_prefix("- -") {
                if let Ok(x) = rest.trim().parse::<u16>() {
                    pending_x = Some(x);
                }
                continue;
            }
            if let Some(rest) = trimmed.strip_prefix('-') {
                if let Ok(val) = rest.trim().parse::<u16>() {
                    if let Some(x) = pending_x.take() {
                        Self::push_point(&mut pois, sec, (x, val));
                    } else {
                        pending_x = Some(val);
                    }
                }
            }
        }
        pois
    }

    fn push_point(&mut self, sec: &str, pt: (u16, u16)) {
        match sec {
            "port_points" => self.port_points.push(pt),
            "warpholes" => self.warpholes.push(pt),
            "rifts" => self.rifts.push(pt),
            "craters" => self.craters.push(pt),
            _ => {}
        }
    }
}

#[derive(Debug, Clone, Default)]
pub struct TerrainHerds {
    pub beach: Vec<(u16, u16)>,
    pub land: Vec<(u16, u16)>,
}

impl TerrainHerds {
    pub fn is_empty(&self) -> bool {
        self.beach.is_empty() && self.land.is_empty()
    }

    pub fn all_points(&self) -> Vec<(u16, u16)> {
        let mut out = Vec::with_capacity(self.beach.len() + self.land.len());
        out.extend_from_slice(&self.beach);
        out.extend_from_slice(&self.land);
        out
    }

    /// Parses `herds.yml` (`herds: -> beach/land -> - id: ... tile: - x - y`).
    pub fn parse_yaml(raw: &str) -> Self {
        let mut out = Self::default();
        let mut in_herds = false;
        let mut group: Option<&str> = None;
        let mut reading_tile = false;
        let mut pending_x: Option<u16> = None;

        for line in raw.lines() {
            let trimmed_end = line.trim_end();
            let trimmed = trimmed_end.trim();
            if trimmed.is_empty() || trimmed.starts_with('#') {
                continue;
            }
            let indent = trimmed_end.len() - trimmed_end.trim_start_matches(' ').len();
            if indent == 0 && trimmed.ends_with(':') {
                in_herds = trimmed.eq_ignore_ascii_case("herds:");
                group = None;
                reading_tile = false;
                pending_x = None;
                continue;
            }
            if !in_herds {
                continue;
            }
            if indent == 2 && trimmed.ends_with(':') {
                let k = trimmed.trim_end_matches(':').trim();
                group = match k {
                    "beach" => Some("beach"),
                    "land" => Some("land"),
                    _ => None,
                };
                reading_tile = false;
                pending_x = None;
                continue;
            }
            if trimmed.starts_with("tile:") {
                reading_tile = true;
                pending_x = None;
                if let Some(inline) = trimmed
                    .strip_prefix("tile:")
                    .map(str::trim)
                    .and_then(|s| s.strip_prefix('['))
                    .and_then(|s| s.strip_suffix(']'))
                {
                    let mut nums = inline.split(',').filter_map(|p| p.trim().parse::<u16>().ok());
                    if let (Some(x), Some(y)) = (nums.next(), nums.next()) {
                        match group {
                            Some("beach") => out.beach.push((x, y)),
                            Some("land") => out.land.push((x, y)),
                            _ => {}
                        }
                    }
                    reading_tile = false;
                }
                continue;
            }
            if reading_tile {
                if let Some(rest) = trimmed.strip_prefix('-') {
                    if let Ok(val) = rest.trim().parse::<u16>() {
                        if let Some(x) = pending_x.take() {
                            match group {
                                Some("beach") => out.beach.push((x, val)),
                                Some("land") => out.land.push((x, val)),
                                _ => {}
                            }
                            reading_tile = false;
                        } else {
                            pending_x = Some(val);
                        }
                        continue;
                    }
                }
                reading_tile = false;
                pending_x = None;
            }
        }
        out
    }
}

#[derive(Debug, Clone)]
pub struct LoadedTerrain {
    pub id: String,
    pub template_id: String,
    pub width: usize,
    pub height: usize,
    pub entry_tile: (u16, u16),
    pub pois: TerrainPois,
    pub herds: TerrainHerds,
    pub info_json: Bytes,
    pub biomes: Bytes,
    pub ocean: Vec<u8>,
    pub rivers: Vec<u8>,
    pub chunk_gardens: HashMap<(u8, u8), Vec<u8>>,
    pub chunk_landmarks: HashMap<(u8, u8), Vec<u8>>,
    pub natural_tiles: HashMap<(u16, u16), u16>,
}

impl LoadedTerrain {
    fn index_natural_tiles(bytes: &[u8]) -> HashMap<(u16, u16), u16> {
        let mut tiles = HashMap::new();
        if bytes.len() % 6 != 0 {
            return tiles;
        }
        for rec in bytes.chunks_exact(6) {
            let x = u16::from_le_bytes([rec[0], rec[1]]);
            let y = u16::from_le_bytes([rec[2], rec[3]]);
            let et = u16::from_le_bytes([rec[4], rec[5]]);
            if et != 0 {
                tiles.insert((x, y), et);
            }
        }
        tiles
    }

    fn bucket_by_chunk(bytes: &[u8], stride: usize, chunks_x: usize, chunks_y: usize) -> HashMap<(u8, u8), Vec<u8>> {
        let mut map: HashMap<(u8, u8), Vec<u8>> = HashMap::new();
        if stride < 4 || bytes.len() % stride != 0 {
            return map;
        }
        for rec in bytes.chunks_exact(stride) {
            let x = u16::from_le_bytes([rec[0], rec[1]]) as usize;
            let y = u16::from_le_bytes([rec[2], rec[3]]) as usize;
            let cx = x / 16;
            let cy = y / 16;
            if cx < chunks_x && cy < chunks_y {
                map.entry((cx as u8, cy as u8))
                    .or_default()
                    .extend_from_slice(rec);
            }
        }
        map
    }

    pub fn fallback(id: &str) -> Self {
        let width = 256usize;
        let height = 256usize;
        let edges = (width + 1) * (height + 1);
        let info = json!({
            "tile_count": [width, height],
            "lake_biome": "grassland",
            "ocean_biome": "warm_ocean",
            "river_biome": "temperate_forest",
            "color_set": "grassland",
            "region_template": id,
            "tile_set": "grassland",
            "entry_points": [[63, 71]],
            "landmarks": []
        });
        // Provide a deterministic starter natural node right beside spawn in synthetic fallback mode
        let mut fallback_garden = Vec::with_capacity(6);
        fallback_garden.extend_from_slice(&64u16.to_le_bytes());
        fallback_garden.extend_from_slice(&71u16.to_le_bytes());
        fallback_garden.extend_from_slice(&16048u16.to_le_bytes());
        let mut chunk_gardens = HashMap::new();
        chunk_gardens.insert((4u8, 4u8), fallback_garden);
        let mut natural_tiles = HashMap::new();
        natural_tiles.insert((64, 71), 16048);

        Self {
            id: id.to_string(),
            template_id: id.to_string(),
            width,
            height,
            entry_tile: (63, 71),
            pois: TerrainPois {
                port_points: vec![(63, 71)],
                ..Default::default()
            },
            herds: TerrainHerds::default(),
            info_json: Bytes::from(info.to_string().into_bytes()),
            biomes: Bytes::from(vec![1u8; width * height]),
            ocean: vec![0u8; edges],
            rivers: vec![0u8; edges * 3],
            chunk_gardens,
            chunk_landmarks: HashMap::new(),
            natural_tiles,
        }
    }

    pub fn load_from_zip(id: &str, zip_path: &Path) -> Result<Self, String> {
        let file = File::open(zip_path).map_err(|e| e.to_string())?;
        let mut archive = zip::ZipArchive::new(file).map_err(|e| e.to_string())?;

        let read_entry = |arch: &mut zip::ZipArchive<File>, name: &str| -> Option<Vec<u8>> {
            let mut f = arch.by_name(name).ok()?;
            let mut buf = Vec::with_capacity(f.size() as usize);
            f.read_to_end(&mut buf).ok()?;
            Some(buf)
        };

        let info_bytes = read_entry(&mut archive, "info.yml")
            .ok_or_else(|| format!("missing info.yml in {}", zip_path.display()))?;
        let info_str = String::from_utf8_lossy(&info_bytes).into_owned();
        let parsed: Value = serde_json::from_str(&info_str)
            .map_err(|e| format!("invalid info.yml json in {}: {e}", zip_path.display()))?;

        let width = parsed["tile_count"][0].as_u64().unwrap_or(256) as usize;
        let height = parsed["tile_count"][1].as_u64().unwrap_or(256) as usize;
        let template_id = parsed["region_template"]
            .as_str()
            .unwrap_or(id)
            .to_string();
        let entry_x = parsed["entry_points"][0][0].as_u64().unwrap_or(63) as u16;
        let entry_y = parsed["entry_points"][0][1].as_u64().unwrap_or(71) as u16;

        let biomes = read_entry(&mut archive, "whole.biomes")
            .unwrap_or_else(|| vec![1u8; width * height]);
        let edges = (width + 1) * (height + 1);
        let ocean = read_entry(&mut archive, "whole.ocean").unwrap_or_else(|| vec![0u8; edges]);
        let rivers =
            read_entry(&mut archive, "whole.rivers").unwrap_or_else(|| vec![0u8; edges * 3]);

        let chunks_x = (width / 16).max(1);
        let chunks_y = (height / 16).max(1);
        let raw_garden = read_entry(&mut archive, "whole.garden");
        let natural_tiles = raw_garden
            .as_deref()
            .map(Self::index_natural_tiles)
            .unwrap_or_default();
        let chunk_gardens = raw_garden
            .as_deref()
            .map(|b| Self::bucket_by_chunk(b, 6, chunks_x, chunks_y))
            .unwrap_or_default();
        let chunk_landmarks = read_entry(&mut archive, "whole.landmarks")
            .map(|b| Self::bucket_by_chunk(&b, 16, chunks_x, chunks_y))
            .unwrap_or_default();
        let mut pois = read_entry(&mut archive, "pois.yml")
            .map(|b| TerrainPois::parse_yaml(&String::from_utf8_lossy(&b)))
            .unwrap_or_default();
        if pois.port_points.is_empty() {
            pois.port_points.push((entry_x, entry_y));
        }
        let herds = read_entry(&mut archive, "herds.yml")
            .map(|b| TerrainHerds::parse_yaml(&String::from_utf8_lossy(&b)))
            .unwrap_or_default();

        Ok(Self {
            id: id.to_string(),
            template_id,
            width,
            height,
            entry_tile: (entry_x, entry_y),
            pois,
            herds,
            info_json: Bytes::from(info_str.into_bytes()),
            biomes: Bytes::from(biomes),
            ocean,
            rivers,
            chunk_gardens,
            chunk_landmarks,
            natural_tiles,
        })
    }

    pub fn num_chunks_x(&self) -> usize {
        (self.width / 16).max(1)
    }

    pub fn num_chunks_y(&self) -> usize {
        (self.height / 16).max(1)
    }

    /// Exact Durango `World.CopyChunk` byte-for-byte slicing.
    fn copy_chunk(
        chunk_x: i32,
        chunk_y: i32,
        src: &[u8],
        dst: &mut [u8],
        count: usize,
        prev_offset: i32,
        post_offset: i32,
    ) {
        if src.is_empty() || count == 0 {
            return;
        }
        let base_x = chunk_x * 16;
        let base_y = chunk_y * 16;
        let dim = ((src.len() / count) as f64).sqrt() as i32;
        if dim <= 0 {
            return;
        }
        let dst_side = (16 + prev_offset + post_offset) as usize;
        for i in -prev_offset..(16 + post_offset) {
            for j in -prev_offset..(16 + post_offset) {
                let sx = (base_x + i).clamp(0, dim - 1) as usize;
                let sy = (base_y + j).clamp(0, dim - 1) as usize;
                let src_idx = (sx + sy * (dim as usize)) * count;
                let dx = (i + prev_offset) as usize;
                let dy = (j + prev_offset) as usize;
                let dst_idx = (dx + dy * dst_side) * count;
                if src_idx + count <= src.len() && dst_idx + count <= dst.len() {
                    dst[dst_idx..dst_idx + count].copy_from_slice(&src[src_idx..src_idx + count]);
                }
            }
        }
    }

    pub fn chunk_biomes(&self, cx: i32, cy: i32) -> Vec<u8> {
        let mut dst = vec![0u8; 324];
        Self::copy_chunk(cx, cy, &self.biomes, &mut dst, 1, 1, 1);
        dst
    }

    pub fn chunk_ocean(&self, cx: i32, cy: i32) -> Vec<u8> {
        let mut dst = vec![0u8; 289];
        Self::copy_chunk(cx, cy, &self.ocean, &mut dst, 1, 0, 1);
        dst
    }

    pub fn chunk_rivers(&self, cx: i32, cy: i32) -> Vec<u8> {
        let mut dst = vec![0u8; 867];
        Self::copy_chunk(cx, cy, &self.rivers, &mut dst, 3, 0, 1);
        dst
    }

    pub fn chunk_garden(&self, cx: u8, cy: u8) -> Vec<u8> {
        self.chunk_gardens
            .get(&(cx, cy))
            .cloned()
            .unwrap_or_default()
    }

    pub fn chunk_garden_excluding(
        &self,
        cx: u8,
        cy: u8,
        removed: &HashSet<(u16, u16)>,
    ) -> Vec<u8> {
        let Some(raw) = self.chunk_gardens.get(&(cx, cy)) else {
            return Vec::new();
        };
        if removed.is_empty() {
            return raw.clone();
        }
        let mut out = Vec::with_capacity(raw.len());
        for rec in raw.chunks_exact(6) {
            let x = u16::from_le_bytes([rec[0], rec[1]]);
            let y = u16::from_le_bytes([rec[2], rec[3]]);
            if !removed.contains(&(x, y)) {
                out.extend_from_slice(rec);
            }
        }
        out
    }

    pub fn natural_at(&self, tile_x: u16, tile_y: u16) -> Option<u16> {
        self.natural_tiles.get(&(tile_x, tile_y)).copied()
    }

    /// Returns `true` if `b` from `whole.ocean` is dry walkable/buildable land.
    ///
    /// Matches Durango's exact `WaterData::ByteToDepth` + `WaterData::CreateWaterMask` formula:
    /// - `0..=127`: ocean depth `b / 127.0` (`CreateWaterMask` ocean threshold `0.30` -> `b <= 38` is dry)
    /// - `128..=255`: inland lake depth `(b - 128) / 127.0` (`CreateWaterMask` lake threshold `0.10` -> `128..=140` is dry)
    #[inline]
    pub fn is_dry_ocean_corner(b: u8) -> bool {
        b <= 38 || (128..=140).contains(&b)
    }

    /// Returns `true` if all 4 corners of `(tile_x, tile_y)` are dry land.
    pub fn is_dry_land(&self, tile_x: u16, tile_y: u16) -> bool {
        let tx = usize::from(tile_x);
        let ty = usize::from(tile_y);
        if tx >= self.width || ty >= self.height {
            return false;
        }
        let stride = self.width + 1;
        let corners = [
            tx + ty * stride,
            (tx + 1) + ty * stride,
            tx + (ty + 1) * stride,
            (tx + 1) + (ty + 1) * stride,
        ];
        corners
            .iter()
            .all(|&idx| Self::is_dry_ocean_corner(self.ocean.get(idx).copied().unwrap_or(0)))
    }

    /// Returns `true` if `(tile_x, tile_y)` and its 4 cardinal neighbors are all dry land,
    /// keeping wildlife spawns and wandering paths safely inland away from deep-water edges.
    pub fn is_safe_inland_land(&self, tile_x: u16, tile_y: u16) -> bool {
        if tile_x < 2
            || tile_y < 2
            || usize::from(tile_x) + 2 >= self.width
            || usize::from(tile_y) + 2 >= self.height
        {
            return false;
        }
        self.is_dry_land(tile_x, tile_y)
            && self.is_dry_land(tile_x - 1, tile_y)
            && self.is_dry_land(tile_x + 1, tile_y)
            && self.is_dry_land(tile_x, tile_y - 1)
            && self.is_dry_land(tile_x, tile_y + 1)
    }

    /// Finds the nearest safe dry-land tile around `origin` (for example the harbor `entry_tile`),
    /// preferring `is_safe_inland_land` outside the exact dock tile so fresh arrivals do not spawn
    /// standing ankle-deep in coastal water or hovering over unlandable ocean waves.
    pub fn find_nearest_dry_tile(&self, origin: (u16, u16), max_radius: i32) -> (u16, u16) {
        let mut best_inland: Option<(i32, u16, u16)> = None;
        let mut best_dry: Option<(i32, u16, u16)> = None;
        let ox = i32::from(origin.0);
        let oy = i32::from(origin.1);
        for r in 0..=max_radius {
            for dy in -r..=r {
                for dx in -r..=r {
                    if dx.abs() != r && dy.abs() != r {
                        continue;
                    }
                    let tx = ox + dx;
                    let ty = oy + dy;
                    if tx < 1
                        || ty < 1
                        || tx >= self.width as i32 - 1
                        || ty >= self.height as i32 - 1
                    {
                        continue;
                    }
                    let ux = tx as u16;
                    let uy = ty as u16;
                    if (ux, uy) == origin {
                        continue;
                    }
                    let dist2 = dx * dx + dy * dy;
                    if self.is_safe_inland_land(ux, uy) {
                        if best_inland.is_none_or(|(bd, _, _)| dist2 < bd) {
                            best_inland = Some((dist2, ux, uy));
                        }
                    } else if self.is_dry_land(ux, uy) {
                        if best_dry.is_none_or(|(bd, _, _)| dist2 < bd) {
                            best_dry = Some((dist2, ux, uy));
                        }
                    }
                }
            }
            if let Some((_, bx, by)) = best_inland {
                return (bx, by);
            }
        }
        if let Some((_, bx, by)) = best_dry {
            return (bx, by);
        }
        origin
    }

    pub fn chunk_combined(&self, cx: i32, cy: i32) -> Vec<u8> {
        let b = self.chunk_biomes(cx, cy);
        let o = self.chunk_ocean(cx, cy);
        let r = self.chunk_rivers(cx, cy);
        let lm = if cx >= 0
            && cy >= 0
            && (cx as usize) < self.num_chunks_x()
            && (cy as usize) < self.num_chunks_y()
            && cx <= 255
            && cy <= 255
        {
            self.chunk_landmarks.get(&(cx as u8, cy as u8))
        } else {
            None
        };
        let extra = lm.map_or(0, Vec::len);
        let mut out = Vec::with_capacity(b.len() + o.len() + r.len() + extra);
        out.extend_from_slice(&b);
        out.extend_from_slice(&o);
        out.extend_from_slice(&r);
        if let Some(lm_bytes) = lm {
            out.extend_from_slice(lm_bytes);
        }
        out
    }
}

pub struct TerrainStore {
    terrains_dir: Option<PathBuf>,
    cache: RwLock<HashMap<String, Arc<LoadedTerrain>>>,
    fallback_shared: Arc<LoadedTerrain>,
    available_terrains: Vec<String>,
}

impl TerrainStore {
    pub fn new(terrains_dir: Option<PathBuf>) -> Self {
        let mut ids = Vec::new();
        if let Some(dir) = &terrains_dir {
            if let Ok(rd) = std::fs::read_dir(dir) {
                for entry in rd.flatten() {
                    let p = entry.path();
                    if p.extension().and_then(|e| e.to_str()) == Some("zip") {
                        if let Some(stem) = p.file_stem().and_then(|s| s.to_str()) {
                            ids.push(stem.to_string());
                        }
                    }
                }
            }
        }
        if ids.is_empty() {
            ids.push(DEFAULT_TERRAIN_ID.to_string());
            ids.push("pe10tr_1".to_string());
        }
        ids.sort();
        ids.dedup();
        Self {
            terrains_dir,
            cache: RwLock::new(HashMap::new()),
            fallback_shared: Arc::new(LoadedTerrain::fallback(DEFAULT_TERRAIN_ID)),
            available_terrains: ids,
        }
    }

    pub async fn get_or_load(&self, raw_id: &str) -> Arc<LoadedTerrain> {
        let resolved = crate::state::terrain_id_for_region(raw_id);
        let id = resolved.as_str();
        if let Some(hit) = self.cache.read().await.get(id) {
            return hit.clone();
        }
        let safe = !id.is_empty()
            && id.len() <= 64
            && id
                .chars()
                .all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-');
        if !safe {
            return self.fallback_shared.clone();
        }
        if let Some(dir) = &self.terrains_dir {
            let candidate = dir.join(format!("{id}.zip"));
            if candidate.is_file() {
                let id_owned = id.to_string();
                let loaded = tokio::task::spawn_blocking(move || {
                    LoadedTerrain::load_from_zip(&id_owned, &candidate)
                })
                .await;
                match loaded {
                    Ok(Ok(t)) => {
                        tracing::info!(
                            terrain = %t.id,
                            width = t.width,
                            height = t.height,
                            "loaded terrain archive"
                        );
                        let arc = Arc::new(t);
                        self.cache.write().await.insert(id.to_string(), arc.clone());
                        return arc;
                    }
                    Ok(Err(err)) => {
                        tracing::warn!(%id, %err, "terrain zip load failed; using shared fallback");
                    }
                    Err(err) => {
                        tracing::warn!(%id, %err, "terrain zip task panicked; using shared fallback");
                    }
                }
            }
        }
        // Do NOT insert fallback/nonexistent IDs into `cache` so untrusted strings cannot grow memory.
        self.fallback_shared.clone()
    }

    pub fn list_available_terrains(&self) -> Vec<String> {
        self.available_terrains.clone()
    }
}

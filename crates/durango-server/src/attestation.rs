use std::collections::HashMap;
use std::fs;
use std::io::Read;
use std::path::Path;
use std::sync::Mutex;

use serde::{Deserialize, Serialize};
use uuid::Uuid;

pub const COMPILED_RELEASE_ID: Option<&str> = option_env!("DURANGO_OFFICIAL_RELEASE_ID");
pub const COMPILED_BUILD_KEY: Option<&str> = option_env!("DURANGO_OFFICIAL_BUILD_KEY");
pub const COMPILED_GIT_COMMIT: Option<&str> = option_env!("DURANGO_GIT_COMMIT");

const SHA256_K: [u32; 64] = [
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
];

struct Sha256 {
    state: [u32; 8],
    bit_len: u64,
    buf: [u8; 64],
    buf_len: usize,
}

impl Sha256 {
    fn new() -> Self {
        Self {
            state: [
                0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab,
                0x5be0cd19,
            ],
            bit_len: 0,
            buf: [0u8; 64],
            buf_len: 0,
        }
    }

    fn compress(&mut self, block: &[u8; 64]) {
        let mut w = [0u32; 64];
        for i in 0..16 {
            w[i] = u32::from_be_bytes([
                block[i * 4],
                block[i * 4 + 1],
                block[i * 4 + 2],
                block[i * 4 + 3],
            ]);
        }
        for i in 16..64 {
            let s0 = w[i - 15].rotate_right(7) ^ w[i - 15].rotate_right(18) ^ (w[i - 15] >> 3);
            let s1 = w[i - 2].rotate_right(17) ^ w[i - 2].rotate_right(19) ^ (w[i - 2] >> 10);
            w[i] = w[i - 16]
                .wrapping_add(s0)
                .wrapping_add(w[i - 7])
                .wrapping_add(s1);
        }
        let [mut a, mut b, mut c, mut d, mut e, mut f, mut g, mut h] = self.state;
        for i in 0..64 {
            let s1 = e.rotate_right(6) ^ e.rotate_right(11) ^ e.rotate_right(25);
            let ch = (e & f) ^ ((!e) & g);
            let temp1 = h
                .wrapping_add(s1)
                .wrapping_add(ch)
                .wrapping_add(SHA256_K[i])
                .wrapping_add(w[i]);
            let s0 = a.rotate_right(2) ^ a.rotate_right(13) ^ a.rotate_right(22);
            let maj = (a & b) ^ (a & c) ^ (b & c);
            let temp2 = s0.wrapping_add(maj);

            h = g;
            g = f;
            f = e;
            e = d.wrapping_add(temp1);
            d = c;
            c = b;
            b = a;
            a = temp1.wrapping_add(temp2);
        }
        self.state[0] = self.state[0].wrapping_add(a);
        self.state[1] = self.state[1].wrapping_add(b);
        self.state[2] = self.state[2].wrapping_add(c);
        self.state[3] = self.state[3].wrapping_add(d);
        self.state[4] = self.state[4].wrapping_add(e);
        self.state[5] = self.state[5].wrapping_add(f);
        self.state[6] = self.state[6].wrapping_add(g);
        self.state[7] = self.state[7].wrapping_add(h);
    }

    fn update(&mut self, mut data: &[u8]) {
        self.bit_len = self.bit_len.wrapping_add((data.len() as u64).wrapping_mul(8));
        if self.buf_len > 0 {
            let need = 64 - self.buf_len;
            let take = data.len().min(need);
            self.buf[self.buf_len..self.buf_len + take].copy_from_slice(&data[..take]);
            self.buf_len += take;
            data = &data[take..];
            if self.buf_len == 64 {
                let block = self.buf;
                self.compress(&block);
                self.buf_len = 0;
            }
        }
        while data.len() >= 64 {
            let mut block = [0u8; 64];
            block.copy_from_slice(&data[..64]);
            self.compress(&block);
            data = &data[64..];
        }
        if !data.is_empty() {
            self.buf[..data.len()].copy_from_slice(data);
            self.buf_len = data.len();
        }
    }

    fn finalize(mut self) -> [u8; 32] {
        self.buf[self.buf_len] = 0x80;
        self.buf_len += 1;
        if self.buf_len > 56 {
            self.buf[self.buf_len..64].fill(0);
            let block = self.buf;
            self.compress(&block);
            self.buf_len = 0;
        }
        self.buf[self.buf_len..56].fill(0);
        self.buf[56..64].copy_from_slice(&self.bit_len.to_be_bytes());
        let block = self.buf;
        self.compress(&block);

        let mut out = [0u8; 32];
        for (i, word) in self.state.iter().enumerate() {
            out[i * 4..i * 4 + 4].copy_from_slice(&word.to_be_bytes());
        }
        out
    }
}

pub fn sha256_bytes(data: &[u8]) -> [u8; 32] {
    let mut h = Sha256::new();
    h.update(data);
    h.finalize()
}

pub fn hex_encode(bytes: &[u8]) -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut out = String::with_capacity(bytes.len() * 2);
    for &b in bytes {
        out.push(HEX[(b >> 4) as usize] as char);
        out.push(HEX[(b & 0x0f) as usize] as char);
    }
    out
}

pub fn sha256_hex(data: &[u8]) -> String {
    hex_encode(&sha256_bytes(data))
}

pub fn hmac_sha256(key: &[u8], msg: &[u8]) -> [u8; 32] {
    let mut key_block = [0u8; 64];
    if key.len() > 64 {
        let digest = sha256_bytes(key);
        key_block[..32].copy_from_slice(&digest);
    } else {
        key_block[..key.len()].copy_from_slice(key);
    }
    let mut ipad = [0x36u8; 64];
    let mut opad = [0x5cu8; 64];
    for i in 0..64 {
        ipad[i] ^= key_block[i];
        opad[i] ^= key_block[i];
    }
    let mut inner = Sha256::new();
    inner.update(&ipad);
    inner.update(msg);
    let inner_hash = inner.finalize();

    let mut outer = Sha256::new();
    outer.update(&opad);
    outer.update(&inner_hash);
    outer.finalize()
}

pub fn hmac_sha256_hex(key: &[u8], msg: &[u8]) -> String {
    hex_encode(&hmac_sha256(key, msg))
}

pub fn ct_eq_hex(a: &str, b: &str) -> bool {
    let ab = a.trim().as_bytes();
    let bb = b.trim().as_bytes();
    if ab.len() != bb.len() || ab.is_empty() {
        return false;
    }
    let mut diff = 0u8;
    for (&x, &y) in ab.iter().zip(bb.iter()) {
        diff |= x.to_ascii_lowercase() ^ y.to_ascii_lowercase();
    }
    diff == 0
}

pub fn compute_file_sha256(path: &Path) -> Option<String> {
    let mut file = fs::File::open(path).ok()?;
    let mut h = Sha256::new();
    let mut buf = [0u8; 16384];
    loop {
        let n = file.read(&mut buf).ok()?;
        if n == 0 {
            break;
        }
        h.update(&buf[..n]);
    }
    Some(hex_encode(&h.finalize()))
}

pub fn compute_self_exe_sha256() -> String {
    std::env::current_exe()
        .ok()
        .and_then(|p| compute_file_sha256(&p))
        .unwrap_or_else(|| "unknown".to_string())
}

/// Computes a deterministic SHA-256 digest over core authoritative gameplay balance assets
/// so tampering with drop tables, recipes, blueprints, or wildlife stats alters the digest.
pub fn compute_assets_digest(assets_dir: Option<&Path>) -> String {
    let Some(dir) = assets_dir else {
        return "no-assets".to_string();
    };
    const CORE_ASSET_FILES: &[&str] = &[
        "item/prototype_data.json",
        "item/recipes.json",
        "building/blueprints.json",
        "wildlife/animals.json",
        "skill/skills.json",
    ];
    let mut master = Sha256::new();
    let mut found = 0usize;
    for rel in CORE_ASSET_FILES {
        let p = dir.join(rel);
        if let Some(h) = compute_file_sha256(&p) {
            master.update(rel.as_bytes());
            master.update(b":");
            master.update(h.as_bytes());
            master.update(b"\n");
            found += 1;
        }
    }
    if found == 0 {
        return "no-assets".to_string();
    }
    hex_encode(&master.finalize())
}

/// Canonical heartbeat/directory registration signing message.
pub fn build_attestation_message(
    nonce: &str,
    server_id: &str,
    gateway: &str,
    exe_sha256: &str,
    assets_digest: &str,
    version: &str,
    ts: i64,
) -> String {
    format!(
        "durango-attest-v1|{}|{}|{}|{}|{}|{}|{}",
        nonce.trim(),
        server_id.trim(),
        gateway.trim().trim_end_matches('/'),
        exe_sha256.trim(),
        assets_digest.trim(),
        version.trim(),
        ts
    )
}

pub fn sign_attestation_claim(
    key: &str,
    nonce: &str,
    server_id: &str,
    gateway: &str,
    exe_sha256: &str,
    assets_digest: &str,
    version: &str,
    ts: i64,
) -> String {
    let msg = build_attestation_message(
        nonce,
        server_id,
        gateway,
        exe_sha256,
        assets_digest,
        version,
        ts,
    );
    hmac_sha256_hex(key.as_bytes(), msg.as_bytes())
}

/// Canonical live HTTP reverse-probe signing message (`/api/attestation?nonce=...`).
pub fn build_probe_message(nonce: &str, exe_sha256: &str, assets_digest: &str, version: &str) -> String {
    format!(
        "durango-probe-v1|{}|{}|{}|{}",
        nonce.trim(),
        exe_sha256.trim(),
        assets_digest.trim(),
        version.trim()
    )
}

pub fn sign_probe_response(
    key: &str,
    nonce: &str,
    exe_sha256: &str,
    assets_digest: &str,
    version: &str,
) -> String {
    let msg = build_probe_message(nonce, exe_sha256, assets_digest, version);
    hmac_sha256_hex(key.as_bytes(), msg.as_bytes())
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TrustedReleaseEntry {
    pub release_id: String,
    pub exe_sha256: String,
    #[serde(default)]
    pub assets_digest: Option<String>,
    pub hmac_key: String,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
pub struct CaAllowlist {
    #[serde(default)]
    pub require_attestation: bool,
    #[serde(default)]
    pub releases: Vec<TrustedReleaseEntry>,
}

impl CaAllowlist {
    pub fn load_from_file(path: &Path) -> Result<Self, String> {
        let raw = fs::read_to_string(path).map_err(|e| format!("{}: {e}", path.display()))?;
        serde_json::from_str::<Self>(&raw).map_err(|e| format!("{}: {e}", path.display()))
    }

    pub fn find_matching_release(&self, exe_sha256: &str, assets_digest: &str) -> Option<&TrustedReleaseEntry> {
        self.releases.iter().find(|r| {
            let exe_ok = r.exe_sha256 == "*" || ct_eq_hex(&r.exe_sha256, exe_sha256);
            let assets_ok = match r.assets_digest.as_deref() {
                None | Some("*") | Some("") => true,
                Some(expected) => ct_eq_hex(expected, assets_digest),
            };
            exe_ok && assets_ok
        })
    }
}

#[derive(Debug, Clone)]
struct PendingChallenge {
    server_id: String,
    gateway: String,
    issued_at: f64,
}

#[derive(Debug, Default)]
pub struct CaChallengeStore {
    challenges: Mutex<HashMap<String, PendingChallenge>>,
}

impl CaChallengeStore {
    pub fn issue(&self, server_id: &str, gateway: &str, now: f64) -> String {
        let nonce = Uuid::new_v4().simple().to_string();
        let mut map = self.challenges.lock().unwrap();
        map.retain(|_, v| now - v.issued_at <= 90.0);
        if map.len() >= 1024 {
            map.clear();
        }
        map.insert(
            nonce.clone(),
            PendingChallenge {
                server_id: server_id.trim().to_string(),
                gateway: gateway.trim().trim_end_matches('/').to_string(),
                issued_at: now,
            },
        );
        nonce
    }

    pub fn consume_and_verify(&self, nonce: &str, server_id: &str, gateway: &str, now: f64) -> bool {
        let mut map = self.challenges.lock().unwrap();
        map.retain(|_, v| now - v.issued_at <= 90.0);
        let Some(entry) = map.remove(nonce.trim()) else {
            return false;
        };
        let norm_gw = gateway.trim().trim_end_matches('/');
        entry.server_id == server_id.trim() && entry.gateway.eq_ignore_ascii_case(norm_gw)
    }
}

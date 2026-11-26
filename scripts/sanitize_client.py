#!/usr/bin/env python3
"""
Binary sanitizer & English/offline polish patcher for Assembly-CSharp.dll
(Durango OffServer client v3.3.6).

Patches applied deterministically with full pre/post verification:
1. Security / Privacy IL `ret` patches:
   - OffServerHardwareId::AddTo (no hardware/SMBIOS/MachineGuid fingerprinting)
   - TestBridge::StartListener (no unauthenticated local TCP remote-control listener)
   - RavenClient::{CaptureUntiyLog, Send, .cctor} (no Sentry crash uploads)
2. Black-screen prologue video fix:
   - FullScreenMovieGroupBase::Play: NOP the 7-byte `isEditor` branch guard so the
     built-in skip path runs immediately (invokes onFinished() -> Character Creation)
     instead of hanging on dead Nexon CloudFront URLs.
3. #US string heap patches (in-place, zero heap-offset drift):
   - Neutralize leaked Slack webhook, Sentry DSN, external GitHub servers.json, and
     dead Nexon CloudFront prologue movie URLs.
   - Replace hardcoded Thai character-creation UI strings with clean English labels.

    python3 scripts/sanitize_client.py IN.dll OUT.dll
"""
import hashlib
import json
import os
import struct
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
from dotnet_meta import DotNetPE  # noqa: E402

RET = 0x2A
NOP = 0x00

# (fully-qualified void method, why)
RET_PATCHES = [
    ("Durango.Logic.Clusters.OffServerHardwareId::AddTo",
     "stops registry MachineGuid + SMBIOS UUID + deviceUniqueIdentifier fingerprint ('hw' form field)"),
    ("Durango.Development.TestBridge::StartListener",
     "stops the unauthenticated 127.0.0.1 TCP remote-control server (testbridge=<port>)"),
    ("RavenClient::CaptureUntiyLog",
     "stops Unity log capture + gzip upload to Sentry"),
    ("RavenClient::Send",
     "stops Sentry HTTP POST"),
    ("RavenClient::.cctor",
     "stops parsing the hard-coded Sentry DSN"),
    ("Durango.Logic.Clusters.OffServerDiscordLink::Begin",
     "stops opening local Discord OAuth callback server or external browser auth"),
    ("OffServerRebornTitle::UseRebornLogo",
     "restores original Durango title logo instead of third-party DORANGU:RENEWAL emoji branding"),
    ("OffServerRebornTitle::UseRebornLoadingLogo",
     "restores original Durango loading logo instead of third-party DORANGU:RENEWAL emoji branding"),
    ("Durango.UI.OffServerTransition::OnGUI",
     "hides third-party shush-emoji DORANGU:RENEWAL scene transition overlay while preserving phase callbacks"),
    ("OffServerRunButtonUI::Attach",
     "replaces the floating orange Run circle icon with Durango-CE's clean bottom-center character state text"),
    ("OffServerMoveClose::CloseWindows",
     "prevents accidental WASD movement from closing open Bag/Character/Craft/Domain windows"),
]

# Exact IL prefix patches with optional wildcard token bytes (`??` matches any byte; `..` preserves existing byte):
# (method, expected_old_pattern, new_pattern, why)
IL_BYTE_PATCHES = [
    (
        "Durango.UI.FullScreenMovieGroupBase::Play",
        "28??????0a2c1a",
        "00000000000000",
        "always take built-in skip branch so dead CloudFront prologue video never black-screens before Character Creation",
    ),
    (
        "OffServerRunButton::get_HoldMode",
        "72??????701628??????2b0ade0526160ade00062a",
        "..........17..................17..........",
        "default PC movement sprint mode to Hold-Shift-to-Run (release Shift to walk) instead of toggle",
    ),
]

# Fixed-length #US string replacements (same char count, padded with `pad`).
# (label, matcher, replacement prefix, pad char, why)
STRING_PATCHES = [
    ("slack-webhook", lambda t: t.startswith("https://hooks.slack.com/services/"),
     "http://127.0.0.1:9/", "x", "Nexon Slack incoming-webhook URL (secret) -> dead loopback URL"),
    ("sentry-dsn", lambda t: "@app.getsentry.com/" in t and t.startswith("https://"),
     "http://k:k@127.0.0.1:9/", "1", "Nexon Sentry DSN incl. secret key -> dead loopback DSN"),
]

UPSTREAM_SERVERS_JSON = "https://raw.githubusercontent.com/ShuuuuShi/Durango-OffServer-Client/main/servers.json"
DEFAULT_SERVERS_JSON = "http://127.0.0.1:8190/servers.json"

# Exact #US in-place length-shrinking replacements (updates compressed length byte + zero-fills slack).
# Keeps every subsequent #US token offset 100% identical!
# (label, exact_old_text, new_text, why)
US_SHORTEN_PATCHES = [
    ("cloudfront-prologue-mp4",
     "https://d1skbslnewf3os.cloudfront.net/prologue_movie.mp4", "",
     "Remove dead Nexon CloudFront mobile prologue video URL"),
    ("cloudfront-prologue-pc-mp4",
     "https://d1skbslnewf3os.cloudfront.net/prologue_movie_pc.mp4", "",
     "Remove dead Nexon CloudFront PC prologue video URL"),
    ("window-title-dorangu", "Dorangu:Renewal v", "Durango-CE v",
     "Replace third-party Dorangu:Renewal window title with Durango-CE"),
    ("thai-costume-tab", "ชุด", "Set", "Character creation outfit sub-tab -> English"),
    ("thai-outfit-engineer", "ชุดช่าง", "Craft", "Starter outfit 0 (engineer) -> English"),
    ("thai-outfit-office", "ชุดสำนักงาน", "Office", "Starter outfit 1 (office worker) -> English"),
    ("thai-outfit-student", "ชุดนักเรียน", "Student", "Starter outfit 2 (student) -> English"),
    ("thai-outfit-farmer", "ชุดชาวไร่", "Farmer", "Starter outfit 3 (farmer) -> English"),
    ("thai-outfit-service", "ชุดพนักงานบริการ", "Service Staff", "Starter outfit 4 (waiter/service) -> English"),
    ("thai-outfit-soldier", "ชุดทหาร", "Soldier", "Starter outfit 5 (soldier) -> English"),
    ("thai-outfit-homewear", "ชุดอยู่บ้าน", "Homewear", "Starter outfit 6 (homeworker) -> English"),
    ("thai-outfit-hoodie", "เสื้อฮู้ด", "Hoodie", "Starter outfit 7 (hoodie/jobless) -> English"),
    ("thai-create-title", "เพศและรูปลักษณ์เริ่มต้น", "Gender & Appearance",
     "Character creation header -> English"),
    ("thai-preset-title", "เลือกรูปลักษณ์เริ่มต้น", "Choose Starter Look",
     "Character creation preset header -> English"),
    ("thai-preset-desc",
     "ชุดนี้เป็นรูปลักษณ์ของตัวละคร คุณจะเลือกอาชีพและรับสกิลเริ่มต้นเมื่อเดินทางถึงเกาะเทม",
     "Cosmetic starter outfit only. You will pick your job & skills on the Tamed Island.",
     "Character creation preset note -> English"),
]


def us_entries_full(pe):
    """Yield (hdr_file_off, hdr_len, blob_len, str_file_off, text) for every #US entry."""
    d, base = pe.us_data, pe.streams["#US"][0]
    p = 1
    while p < len(d):
        b0 = d[p]
        if b0 & 0x80 == 0:
            n, hp = b0, 1
        elif b0 & 0xC0 == 0x80:
            if p + 1 >= len(d):
                break
            n, hp = ((b0 & 0x3F) << 8) | d[p + 1], 2
        else:
            if p + 3 >= len(d):
                break
            n, hp = ((b0 & 0x1F) << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3], 4
        if p + hp + n > len(d):
            break
        if n > 1:
            yield base + p, hp, n, base + p + hp, d[p + hp: p + hp + n - 1].decode("utf-16le", "replace")
        p += hp + n


def us_entries(pe):
    for _, _, _, str_off, text in us_entries_full(pe):
        yield str_off, text


def encode_compressed_uint(val):
    if val <= 0x7F:
        return bytes([val])
    if val <= 0x3FFF:
        return bytes([(val >> 8) | 0x80, val & 0xFF])
    return bytes([
        ((val >> 24) & 0x1F) | 0xC0,
        (val >> 16) & 0xFF,
        (val >> 8) & 0xFF,
        val & 0xFF,
    ])


def blob(pe, idx):
    b = pe.blob_data
    b0 = b[idx]
    if b0 & 0x80 == 0:
        return b[idx + 1: idx + 1 + b0]
    if b0 & 0xC0 == 0x80:
        l = ((b0 & 0x3F) << 8) | b[idx + 1]
        return b[idx + 2: idx + 2 + l]
    l = ((b0 & 0x1F) << 24) | (b[idx + 1] << 16) | (b[idx + 2] << 8) | b[idx + 3]
    return b[idx + 4: idx + 4 + l]


def il_offset(pe, rva):
    off = pe.rva_to_offset(rva)
    b0 = pe.data[off]
    if b0 & 3 == 2:                       # tiny header
        return off + 1
    if b0 & 3 == 3:                       # fat header
        flags_size = struct.unpack_from("<H", pe.data, off)[0]
        return off + (flags_size >> 12) * 4
    raise ValueError("bad method header")


def find_method(pe, full):
    hits = [(mi, m) for mi, m in enumerate(pe.tables[6], 1)
            if f"{pe.method_to_type.get(mi)}::{m[3]}" == full]
    if len(hits) != 1:
        raise SystemExit(f"[abort] {full}: expected exactly 1 definition, found {len(hits)}")
    return hits[0][1]


def match_il_pattern(buf, pattern):
    if len(pattern) % 2 != 0 or len(buf) != len(pattern) // 2:
        raise SystemExit(f"[abort] invalid IL pattern length {len(pattern)} for {len(buf)} bytes")
    for i, b in enumerate(buf):
        tok = pattern[i * 2: i * 2 + 2]
        if tok not in ("??", "..") and b != int(tok, 16):
            return False
    return True


def apply_il_pattern(buf, pattern):
    out = bytearray(buf)
    for i in range(len(out)):
        tok = pattern[i * 2: i * 2 + 2]
        if tok != "..":
            out[i] = int(tok, 16)
    return bytes(out)


def main(src, dst, servers_url=DEFAULT_SERVERS_JSON, platform="pc"):
    pe = DotNetPE(src)
    data = bytearray(pe.data)
    report = {"input_sha256": hashlib.sha256(pe.data).hexdigest(), "platform": platform, "patches": []}

    ret_patches = [
        p for p in RET_PATCHES
        if platform == "pc" or p[0] != "OffServerRunButtonUI::Attach"
    ]
    il_byte_patches = [
        p for p in IL_BYTE_PATCHES
        if platform == "pc" or p[0] != "OffServerRunButton::get_HoldMode"
    ]

    for full, why in ret_patches:
        rva, _, _, _, sig, _ = find_method(pe, full)
        s = blob(pe, sig)
        if s[0] & 0x10 or s[2] != 0x01:
            raise SystemExit(f"[abort] {full}: not a non-generic void method (sig={s.hex()})")
        if rva == 0:
            raise SystemExit(f"[abort] {full}: no IL body")
        o = il_offset(pe, rva)
        status = "already-patched" if data[o] == RET else "patched"
        report["patches"].append({"method": full, "rva": hex(rva), "file_offset": hex(o),
                                  "old_byte": hex(data[o]), "status": status, "why": why})
        data[o] = RET

    for full, old_pat, new_pat, why in il_byte_patches:
        rva = find_method(pe, full)[0]
        if rva == 0:
            raise SystemExit(f"[abort] {full}: no IL body")
        o = il_offset(pe, rva)
        if len(old_pat) != len(new_pat) or len(old_pat) % 2 != 0:
            raise SystemExit(f"[abort] {full}: mismatched IL pattern length")
        nbytes = len(old_pat) // 2
        cur = bytes(data[o:o + nbytes])
        if match_il_pattern(cur, old_pat):
            data[o:o + nbytes] = apply_il_pattern(cur, new_pat)
            status = "patched"
        elif match_il_pattern(cur, new_pat):
            status = "already-patched"
        else:
            raise SystemExit(f"[abort] {full}: unexpected IL prefix {cur.hex()} (expected {old_pat})")
        report["patches"].append({"method": full, "rva": hex(rva), "file_offset": hex(o),
                                  "status": status, "why": why})

    for label, match, repl, pad, why in STRING_PATCHES:
        hits = [(o, t) for o, t in us_entries(pe) if match(t)]
        if len(hits) == 1:
            o, t = hits[0]
            new = (repl + pad * (len(t) - len(repl))).encode("utf-16le")
            if len(new) != len(t) * 2:
                raise SystemExit(f"[abort] {label}: replacement byte length mismatch")
            data[o:o + len(new)] = new
            status = "patched"
        elif not hits and any(t.startswith(repl) for _, t in us_entries(pe)):
            status = "already-patched"
        else:
            raise SystemExit(f"[abort] {label}: expected exactly 1 match, found {len(hits)}")
        report["patches"].append({"string": label, "status": status, "why": why})

    all_us = list(us_entries_full(pe))
    shorten_list = list(US_SHORTEN_PATCHES) + [
        (
            "upstream-servers-json",
            UPSTREAM_SERVERS_JSON,
            servers_url.strip() or DEFAULT_SERVERS_JSON,
            "Upstream external servers.json URL -> configured Durango-CE directory URL",
        ),
    ]
    us_base = pe.streams["#US"][0]
    for label, old_text, new_text, why in shorten_list:
        hits = [e for e in all_us if e[4] == old_text]
        if len(hits) == 1:
            hdr_off, old_hp, old_n, _, _ = hits[0]
            old_total = old_hp + old_n
            utf16 = new_text.encode("utf-16le")
            term = 0x01 if any(ord(c) > 0x7F for c in new_text) else 0x00
            new_n = len(utf16) + 1
            new_hdr = encode_compressed_uint(new_n)
            payload = new_hdr + utf16 + bytes([term])
            if len(payload) > old_total:
                raise SystemExit(
                    f"[abort] {label}: replacement {new_text!r} ({len(payload)}B) > slot ({old_total}B)"
                )
            # Zero-fill slack bytes (valid 0-length entries in #US, preserving all token offsets)
            padded = payload + b"\x00" * (old_total - len(payload))
            data[hdr_off:hdr_off + old_total] = padded
            status = "patched"
        elif not hits and (
            (new_text == "" and any(pe.us_data[e[0] - us_base + e[1]] == 0x00 and e[2] == 1 for e in all_us)
             or new_text == "" and pe.us_data[1:len(pe.us_data) - 1].count(b"\x01\x00") >= 1)
            or (new_text != "" and any(e[4] == new_text for e in all_us))
        ):
            status = "already-patched"
        else:
            raise SystemExit(f"[abort] {label}: expected 1 match for {old_text!r}, found {len(hits)}")
        report["patches"].append({"string": label, "status": status, "why": why})

    dst_dir = os.path.dirname(os.path.abspath(dst))
    tmp_dst = os.path.join(dst_dir, f".{os.path.basename(dst)}.tmp.{os.getpid()}")
    try:
        with open(tmp_dst, "wb") as f:
            f.write(data)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp_dst, dst)
    except Exception:
        if os.path.exists(tmp_dst):
            os.remove(tmp_dst)
        raise
    report["output_sha256"] = hashlib.sha256(bytes(data)).hexdigest()

    # Re-parse the output and verify every patch cleanly (works even under python3 -O)
    out = DotNetPE(dst)
    for full, _ in ret_patches:
        rva = find_method(out, full)[0]
        if out.data[il_offset(out, rva)] != RET:
            raise SystemExit(f"[verify failed] {full}: not patched to ret")
    for full, _, new_pat, _ in il_byte_patches:
        rva = find_method(out, full)[0]
        o = il_offset(out, rva)
        nbytes = len(new_pat) // 2
        if not match_il_pattern(out.data[o:o + nbytes], new_pat):
            raise SystemExit(f"[verify failed] {full}: IL pattern mismatch after write")
    for label, match, *_ in STRING_PATCHES:
        if any(match(t) for _, t in us_entries(out)):
            raise SystemExit(f"[verify failed] {label}: unpatched string still present")
    for label, old_text, new_text, _ in shorten_list:
        out_texts = [t for _, t in us_entries(out)]
        if any(t == old_text for t in out_texts):
            raise SystemExit(f"[verify failed] {label}: old string still present")
        if new_text != "" and not any(t == new_text for t in out_texts):
            raise SystemExit(f"[verify failed] {label}: replacement string missing after write")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("dst")
    ap.add_argument("--servers-url", default=DEFAULT_SERVERS_JSON)
    ap.add_argument("--platform", choices=["pc", "mobile"], default="pc")
    ns = ap.parse_args()
    main(ns.src, ns.dst, servers_url=ns.servers_url, platform=ns.platform)

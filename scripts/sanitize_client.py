#!/usr/bin/env python3
"""
Binary sanitizer for Assembly-CSharp.dll (Durango OffServer client v3.3.6).

There is no source for the injected OffServer* code, so the dangerous members are
neutralised directly in IL: the first instruction of each target (void) method is
replaced with `ret`, and two leaked third-party credentials are overwritten in the
#US string heap (same length - heap offsets must not move).

    python3 scripts/sanitize_client.py IN.dll OUT.dll

The input file is never modified. Every patch is verified (target found, returns
void, bytes match expectation) and the script aborts without writing on any doubt.
Re-running on an already-patched file is a no-op.
"""
import hashlib
import json
import struct
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib"))
from dotnet_meta import DotNetPE  # noqa: E402

RET = 0x2A

# (fully-qualified method, why)
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
]

# Leaked third-party secrets are located by a non-secret prefix/marker so that this
# script itself never contains the secrets it removes.
# (label, matcher, replacement template, pad char, why)
STRING_PATCHES = [
    ("slack-webhook", lambda t: t.startswith("https://hooks.slack.com/services/"),
     "http://127.0.0.1:9/", "x", "Nexon Slack incoming-webhook URL (secret) -> dead loopback URL"),
    ("sentry-dsn", lambda t: "@app.getsentry.com/" in t and t.startswith("https://"),
     "http://k:k@127.0.0.1:9/", "1", "Nexon Sentry DSN incl. secret key -> dead loopback DSN"),
]


def us_entries(pe):
    """Yield (file_offset, text) for every entry of the #US (user string) heap."""
    d, base = pe.us_data, pe.streams["#US"][0]
    p = 1
    while p < len(d):
        b0 = d[p]
        if b0 & 0x80 == 0:
            n, hp = b0, 1
        elif b0 & 0xC0 == 0x80:
            n, hp = ((b0 & 0x3F) << 8) | d[p + 1], 2
        else:
            n, hp = ((b0 & 0x1F) << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3], 4
        if n > 1:
            yield base + p + hp, d[p + hp: p + hp + n - 1].decode("utf-16le", "replace")
        p += hp + max(n, 1)


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


def main(src, dst):
    pe = DotNetPE(src)
    data = bytearray(pe.data)
    report = {"input_sha256": hashlib.sha256(pe.data).hexdigest(), "patches": []}

    for full, why in RET_PATCHES:
        rva, _, _, _, sig, _ = find_method(pe, full)
        s = blob(pe, sig)
        # [callconv][paramcount][rettype] ; generic methods have an extra byte - not expected here
        if s[0] & 0x10 or s[2] != 0x01:
            raise SystemExit(f"[abort] {full}: not a non-generic void method (sig={s.hex()})")
        if rva == 0:
            raise SystemExit(f"[abort] {full}: no IL body")
        o = il_offset(pe, rva)
        status = "already-patched" if data[o] == RET else "patched"
        report["patches"].append({"method": full, "rva": hex(rva), "file_offset": hex(o),
                                  "old_byte": hex(data[o]), "status": status, "why": why})
        data[o] = RET

    for label, match, repl, pad, why in STRING_PATCHES:
        hits = [(o, t) for o, t in us_entries(pe) if match(t)]
        if len(hits) == 1:
            o, t = hits[0]
            new = (repl + pad * (len(t) - len(repl))).encode("utf-16le")
            assert len(new) == len(t) * 2
            data[o:o + len(new)] = new
            status = "patched"
        elif not hits and any(t.startswith(repl) for _, t in us_entries(pe)):
            status = "already-patched"
        else:
            raise SystemExit(f"[abort] {label}: expected exactly 1 match, found {len(hits)}")
        report["patches"].append({"string": label, "status": status, "why": why})

    with open(dst, "wb") as f:
        f.write(data)
    report["output_sha256"] = hashlib.sha256(bytes(data)).hexdigest()

    # re-parse the output and verify
    out = DotNetPE(dst)
    for full, _ in RET_PATCHES:
        rva = find_method(out, full)[0]
        assert out.data[il_offset(out, rva)] == RET, full
    for label, match, *_ in STRING_PATCHES:
        assert not any(match(t) for _, t in us_entries(out)), label
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])

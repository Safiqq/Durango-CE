#!/usr/bin/env python3
import struct
import sys
import os

class DotNetPE:
    def __init__(self, path):
        self.path = path
        with open(path, 'rb') as f:
            self.data = f.read()
        self.sections = []
        self._parse_pe()
        self._parse_metadata()

    def _parse_pe(self):
        e_lfanew = struct.unpack_from('<I', self.data, 0x3c)[0]
        num_sections = struct.unpack_from('<H', self.data, e_lfanew + 6)[0]
        opt_hdr_size = struct.unpack_from('<H', self.data, e_lfanew + 20)[0]
        sec_offset = e_lfanew + 24 + opt_hdr_size
        for i in range(num_sections):
            s = self.data[sec_offset + i*40 : sec_offset + (i+1)*40]
            name = s[:8].rstrip(b'\x00').decode('ascii', errors='replace')
            vsize, rva, raw_size, raw_ptr = struct.unpack_from('<IIII', s, 8)
            self.sections.append((name, rva, vsize, raw_ptr, raw_size))

    def rva_to_offset(self, rva):
        if rva == 0:
            return 0
        for name, sec_rva, vsize, raw_ptr, raw_size in self.sections:
            if sec_rva <= rva < sec_rva + max(vsize, raw_size):
                return raw_ptr + (rva - sec_rva)
        return 0

    def _parse_metadata(self):
        idx = self.data.find(b'BSJB')
        if idx == -1:
            raise ValueError("No BSJB found")
        self.bsjb_offset = idx
        major, minor, reserved, ver_len = struct.unpack_from('<HHII', self.data, idx + 4)
        self.clr_version = self.data[idx+16:idx+16+ver_len].decode('utf-8', errors='replace').rstrip('\x00')
        pos = idx + 16 + ver_len
        flags, num_streams = struct.unpack_from('<HH', self.data, pos)
        pos += 4
        self.streams = {}
        for _ in range(num_streams):
            s_off, s_size = struct.unpack_from('<II', self.data, pos)
            pos += 8
            name_end = self.data.find(b'\x00', pos)
            s_name = self.data[pos:name_end].decode('ascii', errors='replace')
            pos = (name_end + 4) & ~3
            self.streams[s_name] = (idx + s_off, s_size)

        self.strings_data = self._get_stream('#Strings')
        self.us_data = self._get_stream('#US')
        self.blob_data = self._get_stream('#Blob')
        self.guid_data = self._get_stream('#GUID')
        self.tables_data = self._get_stream('#~') or self._get_stream('#-')
        self._parse_tables()

    def _get_stream(self, name):
        if name not in self.streams:
            return b''
        off, sz = self.streams[name]
        return self.data[off:off+sz]

    def get_string(self, idx):
        if idx >= len(self.strings_data):
            return ''
        end = self.strings_data.find(b'\x00', idx)
        if end == -1:
            end = len(self.strings_data)
        return self.strings_data[idx:end].decode('utf-8', errors='replace')

    def get_us(self, idx):
        if idx == 0 or idx >= len(self.us_data):
            return ''
        p = idx
        b0 = self.us_data[p]
        if (b0 & 0x80) == 0:
            length = b0
            p += 1
        elif (b0 & 0xc0) == 0x80:
            length = ((b0 & 0x3f) << 8) | self.us_data[p+1]
            p += 2
        elif (b0 & 0xe0) == 0xc0:
            length = ((b0 & 0x1f) << 24) | (self.us_data[p+1] << 16) | (self.us_data[p+2] << 8) | self.us_data[p+3]
            p += 4
        else:
            return ''
        if length <= 1:
            return ''
        return self.us_data[p:p+length-1].decode('utf-16le', errors='replace')

    def _parse_tables(self):
        td = self.tables_data
        if not td:
            return
        heap_sizes = td[6]
        str_idx_size = 4 if (heap_sizes & 1) else 2
        guid_idx_size = 4 if (heap_sizes & 2) else 2
        blob_idx_size = 4 if (heap_sizes & 4) else 2
        valid_mask = struct.unpack_from('<Q', td, 8)[0]
        sorted_mask = struct.unpack_from('<Q', td, 16)[0]
        pos = 24
        self.row_counts = [0] * 64
        for i in range(64):
            if (valid_mask >> i) & 1:
                self.row_counts[i] = struct.unpack_from('<I', td, pos)[0]
                pos += 4

        def tbl_idx_size(t):
            return 4 if self.row_counts[t] >= 65536 else 2

        def coded_idx_size(bits, tables):
            max_rows = max((self.row_counts[t] if t is not None and t < 64 else 0) for t in tables)
            return 4 if max_rows >= (1 << (16 - bits)) else 2

        self.str_sz = str_idx_size
        self.guid_sz = guid_idx_size
        self.blob_sz = blob_idx_size

        TypeDefOrRef = coded_idx_size(2, [2, 1, 27])
        HasConstant = coded_idx_size(2, [4, 8, 23])
        HasCustomAttribute = coded_idx_size(5, [6, 4, 1, 2, 8, 9, 10, 0, 14, 23, 20, 17, 26, 27, 32, 35, 38, 39, 40, 42, 44, 43])
        HasFieldMarshal = coded_idx_size(1, [4, 8])
        HasDeclSecurity = coded_idx_size(2, [2, 6, 32])
        MemberRefParent = coded_idx_size(3, [2, 1, 26, 6, 27])
        HasSemantics = coded_idx_size(1, [20, 23])
        MethodDefOrRef = coded_idx_size(1, [6, 10])
        MemberForwarded = coded_idx_size(1, [4, 6])
        Implementation = coded_idx_size(2, [38, 35, 39])
        CustomAttributeType = coded_idx_size(3, [None, None, 6, 10, None])
        ResolutionScope = coded_idx_size(2, [0, 26, 35, 1])
        TypeOrMethodDef = coded_idx_size(1, [2, 6])

        # Helper to read integers
        def r_int(p, sz):
            if sz == 2:
                return struct.unpack_from('<H', td, p)[0], p + 2
            else:
                return struct.unpack_from('<I', td, p)[0], p + 4

        self.tables = {}
        for t in range(64):
            cnt = self.row_counts[t]
            if cnt == 0:
                continue
            rows = []
            if t == 0x00: # Module
                for _ in range(cnt):
                    gen = struct.unpack_from('<H', td, pos)[0]; pos += 2
                    name, pos = r_int(pos, str_idx_size)
                    mvid, pos = r_int(pos, guid_idx_size)
                    encid, pos = r_int(pos, guid_idx_size)
                    encbaseid, pos = r_int(pos, guid_idx_size)
                    rows.append((gen, self.get_string(name)))
            elif t == 0x01: # TypeRef
                for _ in range(cnt):
                    scope, pos = r_int(pos, ResolutionScope)
                    name, pos = r_int(pos, str_idx_size)
                    ns, pos = r_int(pos, str_idx_size)
                    rows.append((scope, self.get_string(ns), self.get_string(name)))
            elif t == 0x02: # TypeDef
                f_sz = tbl_idx_size(4)
                m_sz = tbl_idx_size(6)
                for _ in range(cnt):
                    flags = struct.unpack_from('<I', td, pos)[0]; pos += 4
                    name, pos = r_int(pos, str_idx_size)
                    ns, pos = r_int(pos, str_idx_size)
                    ext, pos = r_int(pos, TypeDefOrRef)
                    flist, pos = r_int(pos, f_sz)
                    mlist, pos = r_int(pos, m_sz)
                    rows.append((flags, self.get_string(ns), self.get_string(name), ext, flist, mlist))
            elif t == 0x04: # Field
                for _ in range(cnt):
                    flags = struct.unpack_from('<H', td, pos)[0]; pos += 2
                    name, pos = r_int(pos, str_idx_size)
                    sig, pos = r_int(pos, blob_idx_size)
                    rows.append((flags, self.get_string(name), sig))
            elif t == 0x06: # MethodDef
                p_sz = tbl_idx_size(8)
                for _ in range(cnt):
                    rva, impl, flags = struct.unpack_from('<IHH', td, pos); pos += 8
                    name, pos = r_int(pos, str_idx_size)
                    sig, pos = r_int(pos, blob_idx_size)
                    plist, pos = r_int(pos, p_sz)
                    rows.append((rva, impl, flags, self.get_string(name), sig, plist))
            elif t == 0x08: # Param
                for _ in range(cnt):
                    flags, seq = struct.unpack_from('<HH', td, pos); pos += 4
                    name, pos = r_int(pos, str_idx_size)
                    rows.append((flags, seq, self.get_string(name)))
            elif t == 0x09: # InterfaceImpl
                c_sz = tbl_idx_size(2)
                for _ in range(cnt):
                    cls, pos = r_int(pos, c_sz)
                    iface, pos = r_int(pos, TypeDefOrRef)
                    rows.append((cls, iface))
            elif t == 0x0A: # MemberRef
                for _ in range(cnt):
                    parent, pos = r_int(pos, MemberRefParent)
                    name, pos = r_int(pos, str_idx_size)
                    sig, pos = r_int(pos, blob_idx_size)
                    rows.append((parent, self.get_string(name), sig))
            elif t == 0x0B: # Constant
                for _ in range(cnt):
                    ctype = struct.unpack_from('<H', td, pos)[0]; pos += 2
                    parent, pos = r_int(pos, HasConstant)
                    val, pos = r_int(pos, blob_idx_size)
                    rows.append((ctype, parent, val))
            elif t == 0x0C: # CustomAttribute
                for _ in range(cnt):
                    parent, pos = r_int(pos, HasCustomAttribute)
                    atype, pos = r_int(pos, CustomAttributeType)
                    val, pos = r_int(pos, blob_idx_size)
                    rows.append((parent, atype, val))
            elif t == 0x0D: # FieldMarshal
                for _ in range(cnt):
                    p, pos = r_int(pos, HasFieldMarshal)
                    nt, pos = r_int(pos, blob_idx_size)
            elif t == 0x0E: # DeclSecurity
                for _ in range(cnt):
                    act = struct.unpack_from('<H', td, pos)[0]; pos += 2
                    p, pos = r_int(pos, HasDeclSecurity)
                    ps, pos = r_int(pos, blob_idx_size)
            elif t == 0x0F: # ClassLayout
                c_sz = tbl_idx_size(2)
                for _ in range(cnt):
                    pos += 6
                    c, pos = r_int(pos, c_sz)
            elif t == 0x10: # FieldLayout
                f_sz = tbl_idx_size(4)
                for _ in range(cnt):
                    pos += 4
                    f, pos = r_int(pos, f_sz)
            elif t == 0x11: # StandAloneSig
                for _ in range(cnt):
                    s, pos = r_int(pos, blob_idx_size)
                    rows.append(s)
            elif t == 0x12: # EventMap
                c_sz = tbl_idx_size(2)
                e_sz = tbl_idx_size(20)
                for _ in range(cnt):
                    pos += c_sz + e_sz
            elif t == 0x14: # Event
                for _ in range(cnt):
                    pos += 2 + str_idx_size + TypeDefOrRef
            elif t == 0x15: # PropertyMap
                c_sz = tbl_idx_size(2)
                pr_sz = tbl_idx_size(23)
                for _ in range(cnt):
                    pos += c_sz + pr_sz
            elif t == 0x17: # Property
                for _ in range(cnt):
                    pos += 2 + str_idx_size + blob_idx_size
            elif t == 0x18: # MethodSemantics
                m_sz = tbl_idx_size(6)
                for _ in range(cnt):
                    pos += 2 + m_sz + HasSemantics
            elif t == 0x19: # MethodImpl
                c_sz = tbl_idx_size(2)
                for _ in range(cnt):
                    pos += c_sz + MethodDefOrRef * 2
            elif t == 0x1A: # ModuleRef
                for _ in range(cnt):
                    name, pos = r_int(pos, str_idx_size)
                    rows.append(self.get_string(name))
            elif t == 0x1B: # TypeSpec
                for _ in range(cnt):
                    s, pos = r_int(pos, blob_idx_size)
                    rows.append(s)
            elif t == 0x1C: # ImplMap (P/Invoke!)
                mr_sz = tbl_idx_size(26)
                for _ in range(cnt):
                    flags = struct.unpack_from('<H', td, pos)[0]; pos += 2
                    mf, pos = r_int(pos, MemberForwarded)
                    iname, pos = r_int(pos, str_idx_size)
                    scope, pos = r_int(pos, mr_sz)
                    rows.append((flags, mf, self.get_string(iname), scope))
            elif t == 0x1D: # FieldRVA
                f_sz = tbl_idx_size(4)
                for _ in range(cnt):
                    rva = struct.unpack_from('<I', td, pos)[0]; pos += 4
                    f, pos = r_int(pos, f_sz)
                    rows.append((rva, f))
            elif t == 0x20: # Assembly
                for _ in range(cnt):
                    pos += 16 + blob_idx_size + str_idx_size * 2
            elif t == 0x23: # AssemblyRef
                for _ in range(cnt):
                    v1, v2, v3, v4, flags = struct.unpack_from('<HHHHI', td, pos); pos += 12
                    pk, pos = r_int(pos, blob_idx_size)
                    name, pos = r_int(pos, str_idx_size)
                    cult, pos = r_int(pos, str_idx_size)
                    hv, pos = r_int(pos, blob_idx_size)
                    rows.append((f"{v1}.{v2}.{v3}.{v4}", self.get_string(name)))
            elif t == 0x26: # File
                for _ in range(cnt):
                    pos += 4 + str_idx_size + blob_idx_size
            elif t == 0x27: # ExportedType
                for _ in range(cnt):
                    pos += 8 + str_idx_size * 2 + Implementation
            elif t == 0x28: # ManifestResource
                for _ in range(cnt):
                    off, flags = struct.unpack_from('<II', td, pos); pos += 8
                    name, pos = r_int(pos, str_idx_size)
                    impl, pos = r_int(pos, Implementation)
                    rows.append((off, flags, self.get_string(name), impl))
            elif t == 0x29: # NestedClass
                c_sz = tbl_idx_size(2)
                for _ in range(cnt):
                    nc, pos = r_int(pos, c_sz)
                    ec, pos = r_int(pos, c_sz)
                    rows.append((nc, ec))
            elif t == 0x2A: # GenericParam
                for _ in range(cnt):
                    pos += 4 + TypeOrMethodDef + str_idx_size
            elif t == 0x2B: # MethodSpec
                for _ in range(cnt):
                    m, pos = r_int(pos, MethodDefOrRef)
                    inst, pos = r_int(pos, blob_idx_size)
                    rows.append((m, inst))
            elif t == 0x2C: # GenericParamConstraint
                gp_sz = tbl_idx_size(42)
                for _ in range(cnt):
                    pos += gp_sz + TypeDefOrRef
            else:
                # stop if unknown table encountered
                break
            self.tables[t] = rows

        # Build Method -> Type mapping
        self.method_to_type = {}
        self.field_to_type = {}
        tdefs = self.tables.get(0x02, [])
        mdefs = self.tables.get(0x06, [])
        fdefs = self.tables.get(0x04, [])
        nested = {nc: ec for nc, ec in self.tables.get(0x29, [])}

        def full_type_name(t_idx):
            if t_idx < 1 or t_idx > len(tdefs):
                return f"Type#{t_idx}"
            _, ns, name, _, _, _ = tdefs[t_idx - 1]
            if t_idx in nested:
                return full_type_name(nested[t_idx]) + "+" + name
            return f"{ns}.{name}" if ns else name

        self.type_names = [full_type_name(i+1) for i in range(len(tdefs))]

        for i, td_row in enumerate(tdefs):
            _, _, _, _, flist, mlist = td_row
            next_flist = tdefs[i+1][4] if i+1 < len(tdefs) else len(fdefs) + 1
            next_mlist = tdefs[i+1][5] if i+1 < len(tdefs) else len(mdefs) + 1
            tname = self.type_names[i]
            for f_idx in range(flist, next_flist):
                self.field_to_type[f_idx] = tname
            for m_idx in range(mlist, next_mlist):
                self.method_to_type[m_idx] = tname

    def resolve_token(self, tok):
        tbl = (tok >> 24) & 0xff
        rid = tok & 0x00ffffff
        if rid == 0:
            return f"0x{tok:08x}"
        if tbl == 0x70: # User string
            return repr(self.get_us(rid))
        if tbl == 0x01: # TypeRef
            rows = self.tables.get(0x01, [])
            if rid <= len(rows):
                _, ns, name = rows[rid - 1]
                return f"{ns}.{name}" if ns else name
        elif tbl == 0x02: # TypeDef
            if rid <= len(self.type_names):
                return self.type_names[rid - 1]
        elif tbl == 0x04: # Field
            rows = self.tables.get(0x04, [])
            if rid <= len(rows):
                _, name, _ = rows[rid - 1]
                return f"{self.field_to_type.get(rid, '')}::{name}"
        elif tbl == 0x06: # MethodDef
            rows = self.tables.get(0x06, [])
            if rid <= len(rows):
                _, _, _, name, _, _ = rows[rid - 1]
                return f"{self.method_to_type.get(rid, '')}::{name}"
        elif tbl == 0x0A: # MemberRef
            rows = self.tables.get(0x0A, [])
            if rid <= len(rows):
                parent, name, _ = rows[rid - 1]
                ptag = parent & 7
                prid = parent >> 3
                pname = ""
                if ptag == 0 and prid <= len(self.type_names):
                    pname = self.type_names[prid - 1]
                elif ptag == 1:
                    trefs = self.tables.get(0x01, [])
                    if prid <= len(trefs):
                        _, ns, tn = trefs[prid - 1]
                        pname = f"{ns}.{tn}" if ns else tn
                return f"{pname}::{name}"
        elif tbl == 0x2B: # MethodSpec
            rows = self.tables.get(0x2B, [])
            if rid <= len(rows):
                m, _ = rows[rid - 1]
                mtag = m & 1
                mrid = m >> 1
                return self.resolve_token(((0x0A if mtag else 0x06) << 24) | mrid)
        return f"Token(0x{tok:08x})"

    def get_method_il_bytes(self, rva):
        if rva == 0:
            return b''
        off = self.rva_to_offset(rva)
        if off == 0 or off >= len(self.data):
            return b''
        b0 = self.data[off]
        if (b0 & 3) == 2: # Tiny header
            code_size = b0 >> 2
            return self.data[off+1 : off+1+code_size]
        elif (b0 & 3) == 3: # Fat header
            flags_size = struct.unpack_from('<H', self.data, off)[0]
            hdr_size = (flags_size >> 12) * 4
            code_size = struct.unpack_from('<I', self.data, off + 4)[0]
            return self.data[off + hdr_size : off + hdr_size + code_size]
        return b''

    def scan_method_tokens(self, il):
        # Scan CIL instructions for tokens (calls, strings, fields, types)
        tokens = []
        p = 0
        n = len(il)
        # 1-byte opcodes operand lengths
        op1_len = {
            0x0e:1, 0x0f:1, 0x10:1, 0x11:1, 0x12:1, 0x13:1, 0x1f:1,
            0x2b:1, 0x2c:1, 0x2d:1, 0x2e:1, 0x2f:1, 0x30:1, 0x31:1, 0x32:1, 0x33:1, 0x34:1, 0x35:1, 0x36:1, 0x37:1, 0xde:1,
            0x20:4, 0x22:4,
            0x38:4, 0x39:4, 0x3a:4, 0x3b:4, 0x3c:4, 0x3d:4, 0x3e:4, 0x3f:4, 0x40:4, 0x41:4, 0x42:4, 0x43:4, 0x44:4, 0xdd:4,
            0x21:8, 0x23:8,
            # token instructions (4-byte metadata token):
            0x27:4, 0x28:4, 0x29:4, 0x6f:4, 0x70:4, 0x71:4, 0x72:4, 0x73:4, 0x74:4, 0x75:4,
            0x79:4, 0x7b:4, 0x7c:4, 0x7d:4, 0x7e:4, 0x7f:4, 0x80:4, 0x81:4, 0x8c:4, 0x8d:4,
            0x8f:4, 0xa3:4, 0xa4:4, 0xa5:4, 0xc2:4, 0xc6:4, 0xd0:4,
        }
        token_ops = {
            0x27:'jmp', 0x28:'call', 0x6f:'callvirt', 0x70:'cpobj', 0x71:'ldobj', 0x72:'ldstr',
            0x73:'newobj', 0x74:'castclass', 0x75:'isinst', 0x79:'unbox', 0x7b:'ldfld',
            0x7c:'ldflda', 0x7d:'stfld', 0x7e:'ldsfld', 0x7f:'ldsflda', 0x80:'stsfd',
            0x81:'stobj', 0x8c:'box', 0x8d:'newarr', 0x8f:'ldelema', 0xa3:'ldelem',
            0xa4:'stelem', 0xa5:'unbox.any', 0xc2:'refanyval', 0xc6:'mkrefany', 0xd0:'ldtoken'
        }
        op2_len = {
            0x06:4, 0x07:4, # ldftn, ldvirtftn (token)
            0x09:2, 0x0a:2, 0x0b:2, 0x0c:2, 0x0d:2, 0x0e:2,
            0x12:1, 0x13:4, 0x14:4, 0x15:4, 0x16:4, 0x18:4, 0x1a:2, 0x1c:4,
        }
        while p < n:
            b = il[p]
            p += 1
            if b == 0x45: # switch
                if p + 4 > n: break
                cnt = struct.unpack_from('<I', il, p)[0]
                p += 4 + cnt * 4
            elif b == 0xfe:
                if p >= n: break
                b2 = il[p]
                p += 1
                sz = op2_len.get(b2, 0)
                if b2 in (0x06, 0x07) and p + 4 <= n:
                    tok = struct.unpack_from('<I', il, p)[0]
                    tokens.append(('ldftn' if b2==0x06 else 'ldvirtftn', tok, self.resolve_token(tok)))
                p += sz
            else:
                sz = op1_len.get(b, 0)
                if b in token_ops and p + 4 <= n:
                    tok = struct.unpack_from('<I', il, p)[0]
                    tokens.append((token_ops[b], tok, self.resolve_token(tok)))
                p += sz
        return tokens

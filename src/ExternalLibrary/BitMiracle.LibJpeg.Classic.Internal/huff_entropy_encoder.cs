using System;

namespace BitMiracle.LibJpeg.Classic.Internal;

internal class huff_entropy_encoder : jpeg_entropy_encoder
{
	private class c_derived_tbl
	{
		public int[] ehufco = new int[256];

		public char[] ehufsi = new char[256];
	}

	private class savable_state
	{
		public int put_buffer;

		public int put_bits;

		public int[] last_dc_val = new int[4];

		public void ASSIGN_STATE(savable_state src)
		{
			put_buffer = src.put_buffer;
			put_bits = src.put_bits;
			for (int i = 0; i < last_dc_val.Length; i++)
			{
				last_dc_val[i] = src.last_dc_val[i];
			}
		}
	}

	private const int MAX_COEF_BITS = 10;

	private const int MAX_CORR_BITS = 1000;

	private savable_state m_saved = new savable_state();

	private int m_restarts_to_go;

	private int m_next_restart_num;

	private c_derived_tbl[] m_dc_derived_tbls = new c_derived_tbl[4];

	private c_derived_tbl[] m_ac_derived_tbls = new c_derived_tbl[4];

	private long[][] m_dc_count_ptrs = new long[4][];

	private long[][] m_ac_count_ptrs = new long[4][];

	private bool m_gather_statistics;

	private jpeg_compress_struct m_cinfo;

	private int ac_tbl_no;

	private uint EOBRUN;

	private uint BE;

	private char[] bit_buffer;

	public huff_entropy_encoder(jpeg_compress_struct cinfo)
	{
		m_cinfo = cinfo;
		for (int i = 0; i < 4; i++)
		{
			m_dc_derived_tbls[i] = (m_ac_derived_tbls[i] = null);
			m_dc_count_ptrs[i] = (m_ac_count_ptrs[i] = null);
		}
		if (m_cinfo.m_progressive_mode)
		{
			bit_buffer = null;
		}
	}

	public override void start_pass(bool gather_statistics)
	{
		m_gather_statistics = gather_statistics;
		if (gather_statistics)
		{
			finish_pass = finish_pass_gather;
		}
		else
		{
			finish_pass = finish_pass_huff;
		}
		if (m_cinfo.m_progressive_mode)
		{
			if (m_cinfo.m_Ah == 0)
			{
				if (m_cinfo.m_Ss == 0)
				{
					encode_mcu = encode_mcu_DC_first;
				}
				else
				{
					encode_mcu = encode_mcu_AC_first;
				}
			}
			else if (m_cinfo.m_Ss == 0)
			{
				encode_mcu = encode_mcu_DC_refine;
			}
			else
			{
				encode_mcu = encode_mcu_AC_refine;
				if (bit_buffer == null)
				{
					bit_buffer = new char[1000];
				}
			}
			ac_tbl_no = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[0]].Ac_tbl_no;
			EOBRUN = 0u;
			BE = 0u;
		}
		else if (gather_statistics)
		{
			encode_mcu = encode_mcu_gather;
		}
		else
		{
			encode_mcu = encode_mcu_huff;
		}
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
			if (m_cinfo.m_Ss == 0 && m_cinfo.m_Ah == 0)
			{
				int dc_tbl_no = jpeg_component_info.Dc_tbl_no;
				if (gather_statistics)
				{
					if (dc_tbl_no < 0 || dc_tbl_no >= 4)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, dc_tbl_no);
					}
					if (m_dc_count_ptrs[dc_tbl_no] == null)
					{
						m_dc_count_ptrs[dc_tbl_no] = new long[257];
					}
					else
					{
						Array.Clear(m_dc_count_ptrs[dc_tbl_no], 0, m_dc_count_ptrs[dc_tbl_no].Length);
					}
				}
				else
				{
					jpeg_make_c_derived_tbl(isDC: true, dc_tbl_no, ref m_dc_derived_tbls[dc_tbl_no]);
				}
				m_saved.last_dc_val[i] = 0;
			}
			if (m_cinfo.m_Se == 0)
			{
				continue;
			}
			int num = jpeg_component_info.Ac_tbl_no;
			if (gather_statistics)
			{
				if (num < 0 || num >= 4)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, num);
				}
				if (m_ac_count_ptrs[num] == null)
				{
					m_ac_count_ptrs[num] = new long[257];
				}
				else
				{
					Array.Clear(m_ac_count_ptrs[num], 0, m_ac_count_ptrs[num].Length);
				}
			}
			else
			{
				jpeg_make_c_derived_tbl(isDC: false, num, ref m_ac_derived_tbls[num]);
			}
		}
		m_saved.put_buffer = 0;
		m_saved.put_bits = 0;
		m_restarts_to_go = m_cinfo.m_restart_interval;
		m_next_restart_num = 0;
	}

	private bool encode_mcu_huff(JBLOCK[][] MCU_data)
	{
		savable_state savable_state = new savable_state();
		savable_state.ASSIGN_STATE(m_saved);
		if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0 && !emit_restart_s(savable_state, m_next_restart_num))
		{
			return false;
		}
		for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
		{
			int num = m_cinfo.m_MCU_membership[i];
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]];
			if (!encode_one_block(savable_state, MCU_data[i][0].data, savable_state.last_dc_val[num], m_dc_derived_tbls[jpeg_component_info.Dc_tbl_no], m_ac_derived_tbls[jpeg_component_info.Ac_tbl_no]))
			{
				return false;
			}
			savable_state.last_dc_val[num] = MCU_data[i][0][0];
		}
		m_saved.ASSIGN_STATE(savable_state);
		if (m_cinfo.m_restart_interval != 0)
		{
			if (m_restarts_to_go == 0)
			{
				m_restarts_to_go = m_cinfo.m_restart_interval;
				m_next_restart_num++;
				m_next_restart_num &= 7;
			}
			m_restarts_to_go--;
		}
		return true;
	}

	private void finish_pass_huff()
	{
		if (m_cinfo.m_progressive_mode)
		{
			emit_eobrun();
			flush_bits_e();
			return;
		}
		savable_state savable_state = new savable_state();
		savable_state.ASSIGN_STATE(m_saved);
		if (!flush_bits_s(savable_state))
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CANT_SUSPEND);
		}
		m_saved.ASSIGN_STATE(savable_state);
	}

	private bool encode_mcu_gather(JBLOCK[][] MCU_data)
	{
		if (m_cinfo.m_restart_interval != 0)
		{
			if (m_restarts_to_go == 0)
			{
				for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
				{
					m_saved.last_dc_val[i] = 0;
				}
				m_restarts_to_go = m_cinfo.m_restart_interval;
			}
			m_restarts_to_go--;
		}
		for (int j = 0; j < m_cinfo.m_blocks_in_MCU; j++)
		{
			int num = m_cinfo.m_MCU_membership[j];
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]];
			htest_one_block(MCU_data[j][0].data, m_saved.last_dc_val[num], m_dc_count_ptrs[jpeg_component_info.Dc_tbl_no], m_ac_count_ptrs[jpeg_component_info.Ac_tbl_no]);
			m_saved.last_dc_val[num] = MCU_data[j][0][0];
		}
		return true;
	}

	private void finish_pass_gather()
	{
		if (m_cinfo.m_progressive_mode)
		{
			emit_eobrun();
		}
		bool[] array = new bool[4];
		bool[] array2 = new bool[4];
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
			if (m_cinfo.m_Ss == 0 && m_cinfo.m_Ah == 0)
			{
				int dc_tbl_no = jpeg_component_info.Dc_tbl_no;
				if (!array[dc_tbl_no])
				{
					if (m_cinfo.m_dc_huff_tbl_ptrs[dc_tbl_no] == null)
					{
						m_cinfo.m_dc_huff_tbl_ptrs[dc_tbl_no] = new JHUFF_TBL();
					}
					jpeg_gen_optimal_table(m_cinfo.m_dc_huff_tbl_ptrs[dc_tbl_no], m_dc_count_ptrs[dc_tbl_no]);
					array[dc_tbl_no] = true;
				}
			}
			if (m_cinfo.m_Se == 0)
			{
				continue;
			}
			int num = jpeg_component_info.Ac_tbl_no;
			if (!array2[num])
			{
				if (m_cinfo.m_ac_huff_tbl_ptrs[num] == null)
				{
					m_cinfo.m_ac_huff_tbl_ptrs[num] = new JHUFF_TBL();
				}
				jpeg_gen_optimal_table(m_cinfo.m_ac_huff_tbl_ptrs[num], m_ac_count_ptrs[num]);
				array2[num] = true;
			}
		}
	}

	private bool encode_one_block(savable_state state, short[] block, int last_dc_val, c_derived_tbl dctbl, c_derived_tbl actbl)
	{
		int num = block[0] - last_dc_val;
		int num2 = num;
		if (num < 0)
		{
			num = -num;
			num2--;
		}
		int num3 = 0;
		while (num != 0)
		{
			num3++;
			num >>= 1;
		}
		if (num3 > 11)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
		}
		if (!emit_bits_s(state, dctbl.ehufco[num3], dctbl.ehufsi[num3]))
		{
			return false;
		}
		if (num3 != 0 && !emit_bits_s(state, num2, num3))
		{
			return false;
		}
		int num4 = 0;
		int[] natural_order = m_cinfo.natural_order;
		int lim_Se = m_cinfo.lim_Se;
		for (int i = 1; i <= lim_Se; i++)
		{
			num2 = block[natural_order[i]];
			if (num2 == 0)
			{
				num4++;
				continue;
			}
			while (num4 > 15)
			{
				if (!emit_bits_s(state, actbl.ehufco[240], actbl.ehufsi[240]))
				{
					return false;
				}
				num4 -= 16;
			}
			num = num2;
			if (num < 0)
			{
				num = -num;
				num2--;
			}
			num3 = 1;
			while ((num >>= 1) != 0)
			{
				num3++;
			}
			if (num3 > 10)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
			}
			num = (num4 << 4) + num3;
			if (!emit_bits_s(state, actbl.ehufco[num], actbl.ehufsi[num]))
			{
				return false;
			}
			if (!emit_bits_s(state, num2, num3))
			{
				return false;
			}
			num4 = 0;
		}
		if (num4 > 0 && !emit_bits_s(state, actbl.ehufco[0], actbl.ehufsi[0]))
		{
			return false;
		}
		return true;
	}

	private void htest_one_block(short[] block, int last_dc_val, long[] dc_counts, long[] ac_counts)
	{
		int num = block[0] - last_dc_val;
		if (num < 0)
		{
			num = -num;
		}
		int num2 = 0;
		while (num != 0)
		{
			num2++;
			num >>= 1;
		}
		if (num2 > 11)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
		}
		dc_counts[num2]++;
		int num3 = 0;
		int lim_Se = m_cinfo.lim_Se;
		int[] natural_order = m_cinfo.natural_order;
		for (int i = 1; i <= lim_Se; i++)
		{
			num = block[natural_order[i]];
			if (num == 0)
			{
				num3++;
				continue;
			}
			while (num3 > 15)
			{
				ac_counts[240]++;
				num3 -= 16;
			}
			if (num < 0)
			{
				num = -num;
			}
			num2 = 1;
			while ((num >>= 1) != 0)
			{
				num2++;
			}
			if (num2 > 10)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
			}
			ac_counts[(num3 << 4) + num2]++;
			num3 = 0;
		}
		if (num3 > 0)
		{
			ac_counts[0]++;
		}
	}

	private bool emit_byte_s(int val)
	{
		return m_cinfo.m_dest.emit_byte(val);
	}

	private void emit_byte_e(int val)
	{
		m_cinfo.m_dest.emit_byte(val);
	}

	private bool dump_buffer_s()
	{
		return true;
	}

	private bool dump_buffer_e()
	{
		return true;
	}

	private bool emit_bits_s(savable_state state, int code, int size)
	{
		if (size == 0)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);
		}
		int num = code & ((1 << size) - 1);
		int num2 = size + state.put_bits;
		num <<= 24 - num2;
		num |= state.put_buffer;
		while (num2 >= 8)
		{
			int num3 = (num >> 16) & 0xFF;
			if (!emit_byte_s(num3))
			{
				return false;
			}
			if (num3 == 255 && !emit_byte_s(0))
			{
				return false;
			}
			num <<= 8;
			num2 -= 8;
		}
		state.put_buffer = num;
		state.put_bits = num2;
		return true;
	}

	private void emit_bits_e(int code, int size)
	{
		if (size == 0)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);
		}
		if (m_gather_statistics)
		{
			return;
		}
		int num = code & ((1 << size) - 1);
		int num2 = size + m_saved.put_bits;
		num <<= 24 - num2;
		num |= m_saved.put_buffer;
		while (num2 >= 8)
		{
			int num3 = (num >> 16) & 0xFF;
			emit_byte_e(num3);
			if (num3 == 255)
			{
				emit_byte_e(0);
			}
			num <<= 8;
			num2 -= 8;
		}
		m_saved.put_buffer = num;
		m_saved.put_bits = num2;
	}

	private bool flush_bits_s(savable_state state)
	{
		if (!emit_bits_s(state, 127, 7))
		{
			return false;
		}
		state.put_buffer = 0;
		state.put_bits = 0;
		return true;
	}

	private void flush_bits_e()
	{
		emit_bits_e(127, 7);
		m_saved.put_buffer = 0;
		m_saved.put_bits = 0;
	}

	private void emit_dc_symbol(int tbl_no, int symbol)
	{
		if (m_gather_statistics)
		{
			m_dc_count_ptrs[tbl_no][symbol]++;
			return;
		}
		c_derived_tbl c_derived_tbl = m_dc_derived_tbls[tbl_no];
		emit_bits_e(c_derived_tbl.ehufco[symbol], c_derived_tbl.ehufsi[symbol]);
	}

	private void emit_ac_symbol(int tbl_no, int symbol)
	{
		if (m_gather_statistics)
		{
			m_ac_count_ptrs[tbl_no][symbol]++;
			return;
		}
		c_derived_tbl c_derived_tbl = m_ac_derived_tbls[tbl_no];
		emit_bits_e(c_derived_tbl.ehufco[symbol], c_derived_tbl.ehufsi[symbol]);
	}

	private void emit_buffered_bits(uint offset, uint nbits)
	{
		if (!m_gather_statistics)
		{
			for (int i = 0; i < nbits; i++)
			{
				emit_bits_e(bit_buffer[offset + i], 1);
			}
		}
	}

	private void emit_eobrun()
	{
		if (EOBRUN != 0)
		{
			uint num = EOBRUN;
			int num2 = 0;
			while ((num >>= 1) != 0)
			{
				num2++;
			}
			if (num2 > 14)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE);
			}
			emit_ac_symbol(ac_tbl_no, num2 << 4);
			if (num2 != 0)
			{
				emit_bits_e((int)EOBRUN, num2);
			}
			EOBRUN = 0u;
			emit_buffered_bits(0u, BE);
			BE = 0u;
		}
	}

	private bool emit_restart_s(savable_state state, int restart_num)
	{
		if (!flush_bits_s(state))
		{
			return false;
		}
		if (!emit_byte_s(255))
		{
			return false;
		}
		if (!emit_byte_s(208 + restart_num))
		{
			return false;
		}
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			state.last_dc_val[i] = 0;
		}
		return true;
	}

	private void emit_restart_e(int restart_num)
	{
		emit_eobrun();
		if (!m_gather_statistics)
		{
			flush_bits_e();
			emit_byte_e(255);
			emit_byte_e(208 + restart_num);
		}
		if (m_cinfo.m_Ss == 0)
		{
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				m_saved.last_dc_val[i] = 0;
			}
		}
		else
		{
			EOBRUN = 0u;
			BE = 0u;
		}
	}

	private static int IRIGHT_SHIFT(int x, int shft)
	{
		if (x < 0)
		{
			return (x >> shft) | (-1 << 16 - shft);
		}
		return x >> shft;
	}

	private bool encode_mcu_DC_first(JBLOCK[][] MCU_data)
	{
		if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
		{
			emit_restart_e(m_next_restart_num);
		}
		for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
		{
			int num = m_cinfo.m_MCU_membership[i];
			int dc_tbl_no = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[num]].Dc_tbl_no;
			int num2 = IRIGHT_SHIFT(MCU_data[i][0][0], m_cinfo.m_Al);
			int num3 = num2 - m_saved.last_dc_val[num];
			m_saved.last_dc_val[num] = num2;
			num2 = num3;
			if (num2 < 0)
			{
				num2 = -num2;
				num3--;
			}
			int num4 = 0;
			while (num2 != 0)
			{
				num4++;
				num2 >>= 1;
			}
			if (num4 > 11)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
			}
			emit_dc_symbol(dc_tbl_no, num4);
			if (num4 != 0)
			{
				emit_bits_e(num3, num4);
			}
		}
		if (m_cinfo.m_restart_interval != 0)
		{
			if (m_restarts_to_go == 0)
			{
				m_restarts_to_go = m_cinfo.m_restart_interval;
				m_next_restart_num++;
				m_next_restart_num &= 7;
			}
			m_restarts_to_go--;
		}
		return true;
	}

	private bool encode_mcu_AC_first(JBLOCK[][] MCU_data)
	{
		if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
		{
			emit_restart_e(m_next_restart_num);
		}
		int[] natural_order = m_cinfo.natural_order;
		int num = 0;
		for (int i = m_cinfo.m_Ss; i <= m_cinfo.m_Se; i++)
		{
			int num2 = MCU_data[0][0][natural_order[i]];
			if (num2 == 0)
			{
				num++;
				continue;
			}
			int code;
			if (num2 < 0)
			{
				num2 = -num2;
				num2 >>= m_cinfo.m_Al;
				code = ~num2;
			}
			else
			{
				num2 >>= m_cinfo.m_Al;
				code = num2;
			}
			if (num2 == 0)
			{
				num++;
				continue;
			}
			if (EOBRUN != 0)
			{
				emit_eobrun();
			}
			while (num > 15)
			{
				emit_ac_symbol(ac_tbl_no, 240);
				num -= 16;
			}
			int num3 = 1;
			while ((num2 >>= 1) != 0)
			{
				num3++;
			}
			if (num3 > 10)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCT_COEF);
			}
			emit_ac_symbol(ac_tbl_no, (num << 4) + num3);
			emit_bits_e(code, num3);
			num = 0;
		}
		if (num > 0)
		{
			EOBRUN++;
			if (EOBRUN == 32767)
			{
				emit_eobrun();
			}
		}
		if (m_cinfo.m_restart_interval != 0)
		{
			if (m_restarts_to_go == 0)
			{
				m_restarts_to_go = m_cinfo.m_restart_interval;
				m_next_restart_num++;
				m_next_restart_num &= 7;
			}
			m_restarts_to_go--;
		}
		return true;
	}

	private bool encode_mcu_DC_refine(JBLOCK[][] MCU_data)
	{
		if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
		{
			emit_restart_e(m_next_restart_num);
		}
		for (int i = 0; i < m_cinfo.m_blocks_in_MCU; i++)
		{
			int num = MCU_data[i][0][0];
			emit_bits_e(num >> m_cinfo.m_Al, 1);
		}
		if (m_cinfo.m_restart_interval != 0)
		{
			if (m_restarts_to_go == 0)
			{
				m_restarts_to_go = m_cinfo.m_restart_interval;
				m_next_restart_num++;
				m_next_restart_num &= 7;
			}
			m_restarts_to_go--;
		}
		return true;
	}

	private bool encode_mcu_AC_refine(JBLOCK[][] MCU_data)
	{
		if (m_cinfo.m_restart_interval != 0 && m_restarts_to_go == 0)
		{
			emit_restart_e(m_next_restart_num);
		}
		int num = 0;
		int[] natural_order = m_cinfo.natural_order;
		int[] array = new int[64];
		for (int i = m_cinfo.m_Ss; i <= m_cinfo.m_Se; i++)
		{
			int num2 = MCU_data[0][0][natural_order[i]];
			if (num2 < 0)
			{
				num2 = -num2;
			}
			if ((array[i] = num2 >> m_cinfo.m_Al) == 1)
			{
				num = i;
			}
		}
		int num3 = 0;
		uint num4 = 0u;
		uint num5 = BE;
		for (int j = m_cinfo.m_Ss; j <= m_cinfo.m_Se; j++)
		{
			int num6 = array[j];
			if (num6 == 0)
			{
				num3++;
				continue;
			}
			while (num3 > 15 && j <= num)
			{
				emit_eobrun();
				emit_ac_symbol(ac_tbl_no, 240);
				num3 -= 16;
				emit_buffered_bits(num5, num4);
				num5 = 0u;
				num4 = 0u;
			}
			if (num6 > 1)
			{
				bit_buffer[num5 + num4] = (char)(num6 & 1);
				num4++;
				continue;
			}
			emit_eobrun();
			emit_ac_symbol(ac_tbl_no, (num3 << 4) + 1);
			num6 = ((MCU_data[0][0][natural_order[j]] >= 0) ? 1 : 0);
			emit_bits_e(num6, 1);
			emit_buffered_bits(num5, num4);
			num5 = 0u;
			num4 = 0u;
			num3 = 0;
		}
		if (num3 > 0 || num4 != 0)
		{
			EOBRUN++;
			BE += num4;
			if (EOBRUN == 32767 || BE > 937)
			{
				emit_eobrun();
			}
		}
		if (m_cinfo.m_restart_interval != 0)
		{
			if (m_restarts_to_go == 0)
			{
				m_restarts_to_go = m_cinfo.m_restart_interval;
				m_next_restart_num++;
				m_next_restart_num &= 7;
			}
			m_restarts_to_go--;
		}
		return true;
	}

	private void jpeg_make_c_derived_tbl(bool isDC, int tblno, ref c_derived_tbl dtbl)
	{
		if (tblno < 0 || tblno >= 4)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tblno);
		}
		JHUFF_TBL jHUFF_TBL = ((!isDC) ? m_cinfo.m_ac_huff_tbl_ptrs[tblno] : m_cinfo.m_dc_huff_tbl_ptrs[tblno]);
		if (jHUFF_TBL == null)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, tblno);
		}
		if (dtbl == null)
		{
			dtbl = new c_derived_tbl();
		}
		int num = 0;
		char[] array = new char[257];
		for (int i = 1; i <= 16; i++)
		{
			int num2 = jHUFF_TBL.Bits[i];
			if (num + num2 > 256)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
			}
			while (num2-- != 0)
			{
				array[num++] = (char)i;
			}
		}
		array[num] = '\0';
		int num3 = num;
		int num4 = 0;
		int num5 = array[0];
		num = 0;
		int[] array2 = new int[257];
		while (array[num] != 0)
		{
			while (array[num] == num5)
			{
				array2[num++] = num4;
				num4++;
			}
			if (num4 >= 1 << num5)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
			}
			num4 <<= 1;
			num5++;
		}
		Array.Clear(dtbl.ehufsi, 0, dtbl.ehufsi.Length);
		int num6 = ((!isDC) ? 255 : 15);
		for (num = 0; num < num3; num++)
		{
			int num7 = jHUFF_TBL.Huffval[num];
			if (num7 > num6 || dtbl.ehufsi[num7] != 0)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE);
			}
			dtbl.ehufco[num7] = array2[num];
			dtbl.ehufsi[num7] = array[num];
		}
	}

	protected void jpeg_gen_optimal_table(JHUFF_TBL htbl, long[] freq)
	{
		byte[] array = new byte[33];
		int[] array2 = new int[257];
		int[] array3 = new int[257];
		int i;
		for (i = 0; i < 257; i++)
		{
			array3[i] = -1;
		}
		freq[256] = 1L;
		while (true)
		{
			int num = -1;
			long num2 = 1000000000L;
			for (i = 0; i <= 256; i++)
			{
				if (freq[i] != 0 && freq[i] <= num2)
				{
					num2 = freq[i];
					num = i;
				}
			}
			int num3 = -1;
			num2 = 1000000000L;
			for (i = 0; i <= 256; i++)
			{
				if (freq[i] != 0 && freq[i] <= num2 && i != num)
				{
					num2 = freq[i];
					num3 = i;
				}
			}
			if (num3 < 0)
			{
				break;
			}
			freq[num] += freq[num3];
			freq[num3] = 0L;
			array2[num]++;
			while (array3[num] >= 0)
			{
				num = array3[num];
				array2[num]++;
			}
			array3[num] = num3;
			array2[num3]++;
			while (array3[num3] >= 0)
			{
				num3 = array3[num3];
				array2[num3]++;
			}
		}
		for (i = 0; i <= 256; i++)
		{
			if (array2[i] != 0)
			{
				if (array2[i] > 32)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_HUFF_CLEN_OVERFLOW);
				}
				array[array2[i]]++;
			}
		}
		for (i = 32; i > 16; i--)
		{
			while (array[i] > 0)
			{
				int num4 = i - 2;
				while (array[num4] == 0)
				{
					num4--;
				}
				array[i] -= 2;
				array[i - 1]++;
				array[num4 + 1] += 2;
				array[num4]--;
			}
		}
		while (array[i] == 0)
		{
			i--;
		}
		array[i]--;
		Buffer.BlockCopy(array, 0, htbl.Bits, 0, htbl.Bits.Length);
		int num5 = 0;
		for (i = 1; i <= 32; i++)
		{
			for (int num4 = 0; num4 <= 255; num4++)
			{
				if (array2[num4] == i)
				{
					htbl.Huffval[num5] = (byte)num4;
					num5++;
				}
			}
		}
		htbl.Sent_table = false;
	}
}

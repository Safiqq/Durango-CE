namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_marker_writer
{
	private jpeg_compress_struct m_cinfo;

	private int m_last_restart_interval;

	public jpeg_marker_writer(jpeg_compress_struct cinfo)
	{
		m_cinfo = cinfo;
	}

	public void write_file_header()
	{
		emit_marker(JPEG_MARKER.SOI);
		m_last_restart_interval = 0;
		if (m_cinfo.m_write_JFIF_header)
		{
			emit_jfif_app0();
		}
		if (m_cinfo.m_write_Adobe_marker)
		{
			emit_adobe_app14();
		}
	}

	public void write_frame_header()
	{
		int num = 0;
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			num += emit_dqt(m_cinfo.Component_info[i].Quant_tbl_no);
		}
		bool flag;
		if (m_cinfo.arith_code || m_cinfo.m_progressive_mode || m_cinfo.m_data_precision != 8 || m_cinfo.block_size != 8)
		{
			flag = false;
		}
		else
		{
			flag = true;
			for (int j = 0; j < m_cinfo.m_num_components; j++)
			{
				if (m_cinfo.Component_info[j].Dc_tbl_no > 1 || m_cinfo.Component_info[j].Ac_tbl_no > 1)
				{
					flag = false;
				}
			}
			if (num != 0 && flag)
			{
				flag = false;
				m_cinfo.TRACEMS(0, J_MESSAGE_CODE.JTRC_16BIT_TABLES);
			}
		}
		if (m_cinfo.arith_code)
		{
			if (m_cinfo.m_progressive_mode)
			{
				emit_sof(JPEG_MARKER.SOF10);
			}
			else
			{
				emit_sof(JPEG_MARKER.SOF9);
			}
		}
		else if (m_cinfo.m_progressive_mode)
		{
			emit_sof(JPEG_MARKER.SOF2);
		}
		else if (flag)
		{
			emit_sof(JPEG_MARKER.SOF0);
		}
		else
		{
			emit_sof(JPEG_MARKER.SOF1);
		}
		if (m_cinfo.color_transform != J_COLOR_TRANSFORM.JCT_NONE)
		{
			emit_lse_ict();
		}
		if (m_cinfo.m_progressive_mode && m_cinfo.block_size != 8)
		{
			emit_pseudo_sos();
		}
	}

	public void write_scan_header()
	{
		if (m_cinfo.arith_code)
		{
			emit_dac();
			return;
		}
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
			if (m_cinfo.m_Ss == 0 && m_cinfo.m_Ah == 0)
			{
				emit_dht(jpeg_component_info.Dc_tbl_no, is_ac: false);
			}
			if (m_cinfo.m_Se != 0)
			{
				emit_dht(jpeg_component_info.Ac_tbl_no, is_ac: true);
			}
		}
		if (m_cinfo.m_restart_interval != m_last_restart_interval)
		{
			emit_dri();
			m_last_restart_interval = m_cinfo.m_restart_interval;
		}
		emit_sos();
	}

	public void write_file_trailer()
	{
		emit_marker(JPEG_MARKER.EOI);
	}

	public void write_tables_only()
	{
		emit_marker(JPEG_MARKER.SOI);
		for (int i = 0; i < 4; i++)
		{
			if (m_cinfo.m_quant_tbl_ptrs[i] != null)
			{
				emit_dqt(i);
			}
		}
		for (int j = 0; j < 4; j++)
		{
			if (m_cinfo.m_dc_huff_tbl_ptrs[j] != null)
			{
				emit_dht(j, is_ac: false);
			}
			if (m_cinfo.m_ac_huff_tbl_ptrs[j] != null)
			{
				emit_dht(j, is_ac: true);
			}
		}
		emit_marker(JPEG_MARKER.EOI);
	}

	public void write_marker_header(int marker, int datalen)
	{
		if (datalen > 65533)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_LENGTH);
		}
		emit_marker((JPEG_MARKER)marker);
		emit_2bytes(datalen + 2);
	}

	public void write_marker_byte(byte val)
	{
		emit_byte(val);
	}

	private void emit_sos()
	{
		emit_marker(JPEG_MARKER.SOS);
		emit_2bytes(2 * m_cinfo.m_comps_in_scan + 2 + 1 + 3);
		emit_byte(m_cinfo.m_comps_in_scan);
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			int num = m_cinfo.m_cur_comp_info[i];
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[num];
			emit_byte(jpeg_component_info.Component_id);
			int num2 = ((m_cinfo.m_Ss == 0 && m_cinfo.m_Ah == 0) ? jpeg_component_info.Dc_tbl_no : 0);
			int num3 = ((m_cinfo.m_Se != 0) ? jpeg_component_info.Ac_tbl_no : 0);
			emit_byte((num2 << 4) + num3);
		}
		emit_byte(m_cinfo.m_Ss);
		emit_byte(m_cinfo.m_Se);
		emit_byte((m_cinfo.m_Ah << 4) + m_cinfo.m_Al);
	}

	private void emit_lse_ict()
	{
		if (m_cinfo.color_transform != J_COLOR_TRANSFORM.JCT_SUBTRACT_GREEN || m_cinfo.Num_components < 3)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
		}
		emit_marker(JPEG_MARKER.JPG8);
		emit_2bytes(24);
		emit_byte(13);
		emit_2bytes(255);
		emit_byte(3);
		emit_byte(m_cinfo.Component_info[1].Component_id);
		emit_byte(m_cinfo.Component_info[0].Component_id);
		emit_byte(m_cinfo.Component_info[2].Component_id);
		emit_byte(128);
		emit_2bytes(0);
		emit_2bytes(0);
		emit_byte(0);
		emit_2bytes(1);
		emit_2bytes(0);
		emit_byte(0);
		emit_2bytes(1);
		emit_2bytes(0);
	}

	private void emit_sof(JPEG_MARKER code)
	{
		emit_marker(code);
		emit_2bytes(3 * m_cinfo.m_num_components + 2 + 5 + 1);
		if (m_cinfo.jpeg_height > 65535 || m_cinfo.jpeg_width > 65535)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG, 65535);
		}
		emit_byte(m_cinfo.m_data_precision);
		emit_2bytes(m_cinfo.jpeg_height);
		emit_2bytes(m_cinfo.jpeg_width);
		emit_byte(m_cinfo.m_num_components);
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[i];
			emit_byte(jpeg_component_info.Component_id);
			emit_byte((jpeg_component_info.H_samp_factor << 4) + jpeg_component_info.V_samp_factor);
			emit_byte(jpeg_component_info.Quant_tbl_no);
		}
	}

	private void emit_adobe_app14()
	{
		emit_marker(JPEG_MARKER.APP14);
		emit_2bytes(14);
		emit_byte(65);
		emit_byte(100);
		emit_byte(111);
		emit_byte(98);
		emit_byte(101);
		emit_2bytes(100);
		emit_2bytes(0);
		emit_2bytes(0);
		switch (m_cinfo.m_jpeg_color_space)
		{
		case J_COLOR_SPACE.JCS_YCbCr:
			emit_byte(1);
			break;
		case J_COLOR_SPACE.JCS_YCCK:
			emit_byte(2);
			break;
		default:
			emit_byte(0);
			break;
		}
	}

	private void emit_dri()
	{
		emit_marker(JPEG_MARKER.DRI);
		emit_2bytes(4);
		emit_2bytes(m_cinfo.m_restart_interval);
	}

	private void emit_dht(int index, bool is_ac)
	{
		JHUFF_TBL jHUFF_TBL = m_cinfo.m_dc_huff_tbl_ptrs[index];
		if (is_ac)
		{
			jHUFF_TBL = m_cinfo.m_ac_huff_tbl_ptrs[index];
			index += 16;
		}
		if (jHUFF_TBL == null)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_HUFF_TABLE, index);
		}
		if (!jHUFF_TBL.Sent_table)
		{
			emit_marker(JPEG_MARKER.DHT);
			int num = 0;
			for (int i = 1; i <= 16; i++)
			{
				num += jHUFF_TBL.Bits[i];
			}
			emit_2bytes(num + 2 + 1 + 16);
			emit_byte(index);
			for (int j = 1; j <= 16; j++)
			{
				emit_byte(jHUFF_TBL.Bits[j]);
			}
			for (int k = 0; k < num; k++)
			{
				emit_byte(jHUFF_TBL.Huffval[k]);
			}
			jHUFF_TBL.Sent_table = true;
		}
	}

	private int emit_dqt(int index)
	{
		JQUANT_TBL jQUANT_TBL = m_cinfo.m_quant_tbl_ptrs[index];
		if (jQUANT_TBL == null)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, index);
		}
		int num = 0;
		for (int i = 0; i <= m_cinfo.lim_Se; i++)
		{
			if (jQUANT_TBL.quantval[m_cinfo.natural_order[i]] > 255)
			{
				num = 1;
			}
		}
		if (!jQUANT_TBL.Sent_table)
		{
			emit_marker(JPEG_MARKER.DQT);
			emit_2bytes((num == 0) ? (m_cinfo.lim_Se + 1 + 1 + 2) : (m_cinfo.lim_Se * 2 + 2 + 1 + 2));
			emit_byte(index + (num << 4));
			for (int j = 0; j <= m_cinfo.lim_Se; j++)
			{
				int num2 = jQUANT_TBL.quantval[m_cinfo.natural_order[j]];
				if (num != 0)
				{
					emit_byte(num2 >> 8);
				}
				emit_byte(num2 & 0xFF);
			}
			jQUANT_TBL.Sent_table = true;
		}
		return num;
	}

	private void emit_dac()
	{
		byte[] array = new byte[16];
		byte[] array2 = new byte[16];
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[m_cinfo.m_cur_comp_info[i]];
			if (m_cinfo.m_Ss == 0 && m_cinfo.m_Ah == 0)
			{
				array[jpeg_component_info.Dc_tbl_no] = 1;
			}
			if (m_cinfo.m_Se != 0)
			{
				array2[jpeg_component_info.Ac_tbl_no] = 1;
			}
		}
		int num = 0;
		for (int j = 0; j < 16; j++)
		{
			num += array[j] + array2[j];
		}
		if (num == 0)
		{
			return;
		}
		emit_marker(JPEG_MARKER.DAC);
		emit_2bytes(num * 2 + 2);
		for (int k = 0; k < 16; k++)
		{
			if (array[k] != 0)
			{
				emit_byte(k);
				emit_byte(m_cinfo.arith_dc_L[k] + (m_cinfo.arith_dc_U[k] << 4));
			}
			if (array2[k] != 0)
			{
				emit_byte(k + 16);
				emit_byte(m_cinfo.arith_ac_K[k]);
			}
		}
	}

	private void emit_pseudo_sos()
	{
		emit_marker(JPEG_MARKER.SOS);
		emit_2bytes(6);
		emit_byte(0);
		emit_byte(0);
		emit_byte(m_cinfo.block_size * m_cinfo.block_size - 1);
		emit_byte(0);
	}

	private void emit_jfif_app0()
	{
		emit_marker(JPEG_MARKER.APP0);
		emit_2bytes(16);
		emit_byte(74);
		emit_byte(70);
		emit_byte(73);
		emit_byte(70);
		emit_byte(0);
		emit_byte(m_cinfo.m_JFIF_major_version);
		emit_byte(m_cinfo.m_JFIF_minor_version);
		emit_byte((int)m_cinfo.m_density_unit);
		emit_2bytes(m_cinfo.m_X_density);
		emit_2bytes(m_cinfo.m_Y_density);
		emit_byte(0);
		emit_byte(0);
	}

	private void emit_marker(JPEG_MARKER mark)
	{
		emit_byte(255);
		emit_byte((int)mark);
	}

	private void emit_2bytes(int value)
	{
		emit_byte((value >> 8) & 0xFF);
		emit_byte(value & 0xFF);
	}

	private void emit_byte(int val)
	{
		if (!m_cinfo.m_dest.emit_byte(val))
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CANT_SUSPEND);
		}
	}
}

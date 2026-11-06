using System;

namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_input_controller
{
	private jpeg_decompress_struct m_cinfo;

	private bool m_consumeData;

	private int m_inheaders;

	private bool m_has_multiple_scans;

	private bool m_eoi_reached;

	public jpeg_input_controller(jpeg_decompress_struct cinfo)
	{
		m_cinfo = cinfo;
		m_inheaders = 1;
	}

	public ReadResult consume_input()
	{
		if (m_consumeData)
		{
			return m_cinfo.m_coef.consume_data();
		}
		return consume_markers();
	}

	public void reset_input_controller()
	{
		m_consumeData = false;
		m_has_multiple_scans = false;
		m_eoi_reached = false;
		m_inheaders = 1;
		m_cinfo.m_err.reset_error_mgr();
		m_cinfo.m_marker.reset_marker_reader();
		m_cinfo.m_coef_bits = null;
	}

	public void start_input_pass()
	{
		per_scan_setup();
		latch_quant_tables();
		m_cinfo.m_entropy.start_pass();
		m_cinfo.m_coef.start_input_pass();
		m_consumeData = true;
	}

	public void finish_input_pass()
	{
		m_cinfo.m_entropy.finish_pass();
		m_consumeData = false;
	}

	public bool HasMultipleScans()
	{
		return m_has_multiple_scans;
	}

	public bool EOIReached()
	{
		return m_eoi_reached;
	}

	public void jpeg_core_output_dimensions()
	{
		if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_width, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_height, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 1;
			m_cinfo.min_DCT_v_scaled_size = 1;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 2)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 2L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 2L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 2;
			m_cinfo.min_DCT_v_scaled_size = 2;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 3)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 3L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 3L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 3;
			m_cinfo.min_DCT_v_scaled_size = 3;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 4)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 4L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 4L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 4;
			m_cinfo.min_DCT_v_scaled_size = 4;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 5)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 5L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 5L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 5;
			m_cinfo.min_DCT_v_scaled_size = 5;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 6)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 6L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 6L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 6;
			m_cinfo.min_DCT_v_scaled_size = 6;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 7)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 7L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 7L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 7;
			m_cinfo.min_DCT_v_scaled_size = 7;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 8)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 8L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 8L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 8;
			m_cinfo.min_DCT_v_scaled_size = 8;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 9)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 9L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 9L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 9;
			m_cinfo.min_DCT_v_scaled_size = 9;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 10)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 10L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 10L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 10;
			m_cinfo.min_DCT_v_scaled_size = 10;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 11)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 11L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 11L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 11;
			m_cinfo.min_DCT_v_scaled_size = 11;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 12)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 12L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 12L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 12;
			m_cinfo.min_DCT_v_scaled_size = 12;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 13)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 13L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 13L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 13;
			m_cinfo.min_DCT_v_scaled_size = 13;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 14)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 14L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 14L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 14;
			m_cinfo.min_DCT_v_scaled_size = 14;
		}
		else if (m_cinfo.m_scale_num * m_cinfo.block_size <= m_cinfo.m_scale_denom * 15)
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 15L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 15L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 15;
			m_cinfo.min_DCT_v_scaled_size = 15;
		}
		else
		{
			m_cinfo.m_output_width = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_width * 16L, m_cinfo.block_size);
			m_cinfo.m_output_height = (int)JpegUtils.jdiv_round_up((long)m_cinfo.m_image_height * 16L, m_cinfo.block_size);
			m_cinfo.min_DCT_h_scaled_size = 16;
			m_cinfo.min_DCT_v_scaled_size = 16;
		}
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[i];
			jpeg_component_info.DCT_h_scaled_size = m_cinfo.min_DCT_h_scaled_size;
			jpeg_component_info.DCT_v_scaled_size = m_cinfo.min_DCT_v_scaled_size;
		}
	}

	private ReadResult consume_markers()
	{
		if (m_eoi_reached)
		{
			return ReadResult.JPEG_REACHED_EOI;
		}
		while (true)
		{
			ReadResult readResult = m_cinfo.m_marker.read_markers();
			switch (readResult)
			{
			case ReadResult.JPEG_REACHED_SOS:
				if (m_inheaders != 0)
				{
					if (m_inheaders == 1)
					{
						initial_setup();
					}
					if (m_cinfo.m_comps_in_scan == 0)
					{
						m_inheaders = 2;
						break;
					}
					m_inheaders = 0;
				}
				else
				{
					if (!m_has_multiple_scans)
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_EOI_EXPECTED);
					}
					if (m_cinfo.m_comps_in_scan == 0)
					{
						break;
					}
					m_cinfo.m_inputctl.start_input_pass();
				}
				return readResult;
			case ReadResult.JPEG_REACHED_EOI:
				m_eoi_reached = true;
				if (m_inheaders != 0)
				{
					if (m_cinfo.m_marker.SawSOF())
					{
						m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_SOF_NO_SOS);
					}
				}
				else if (m_cinfo.m_output_scan_number > m_cinfo.m_input_scan_number)
				{
					m_cinfo.m_output_scan_number = m_cinfo.m_input_scan_number;
				}
				return readResult;
			default:
				return readResult;
			}
		}
	}

	private void initial_setup()
	{
		if (m_cinfo.m_image_height > 65500 || m_cinfo.m_image_width > 65500)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG, 65500);
		}
		if (m_cinfo.m_data_precision < 8 || m_cinfo.m_data_precision > 12)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_PRECISION, m_cinfo.m_data_precision);
		}
		if (m_cinfo.m_num_components > 10)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_num_components, 10);
		}
		m_cinfo.m_max_h_samp_factor = 1;
		m_cinfo.m_max_v_samp_factor = 1;
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			if (m_cinfo.Comp_info[i].H_samp_factor <= 0 || m_cinfo.Comp_info[i].H_samp_factor > 4 || m_cinfo.Comp_info[i].V_samp_factor <= 0 || m_cinfo.Comp_info[i].V_samp_factor > 4)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_SAMPLING);
			}
			m_cinfo.m_max_h_samp_factor = Math.Max(m_cinfo.m_max_h_samp_factor, m_cinfo.Comp_info[i].H_samp_factor);
			m_cinfo.m_max_v_samp_factor = Math.Max(m_cinfo.m_max_v_samp_factor, m_cinfo.Comp_info[i].V_samp_factor);
		}
		if (m_cinfo.is_baseline || (m_cinfo.m_progressive_mode && m_cinfo.m_comps_in_scan != 0))
		{
			m_cinfo.block_size = 8;
			m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
			m_cinfo.lim_Se = 63;
		}
		else
		{
			switch (m_cinfo.m_Se)
			{
			case 0:
				m_cinfo.block_size = 1;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 3:
				m_cinfo.block_size = 2;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order2;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 8:
				m_cinfo.block_size = 3;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order3;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 15:
				m_cinfo.block_size = 4;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order4;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 24:
				m_cinfo.block_size = 5;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order5;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 35:
				m_cinfo.block_size = 6;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order6;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 48:
				m_cinfo.block_size = 7;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order7;
				m_cinfo.lim_Se = m_cinfo.m_Se;
				break;
			case 63:
				m_cinfo.block_size = 8;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 80:
				m_cinfo.block_size = 9;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 99:
				m_cinfo.block_size = 10;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 120:
				m_cinfo.block_size = 11;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 143:
				m_cinfo.block_size = 12;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 168:
				m_cinfo.block_size = 13;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 195:
				m_cinfo.block_size = 14;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 224:
				m_cinfo.block_size = 15;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			case 255:
				m_cinfo.block_size = 16;
				m_cinfo.natural_order = JpegUtils.jpeg_natural_order;
				m_cinfo.lim_Se = 63;
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_PROGRESSION, m_cinfo.m_Ss, m_cinfo.m_Se, m_cinfo.m_Ah, m_cinfo.m_Al);
				break;
			}
		}
		m_cinfo.min_DCT_h_scaled_size = m_cinfo.block_size;
		m_cinfo.min_DCT_v_scaled_size = m_cinfo.block_size;
		for (int j = 0; j < m_cinfo.m_num_components; j++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[j];
			jpeg_component_info.DCT_h_scaled_size = m_cinfo.block_size;
			jpeg_component_info.DCT_v_scaled_size = m_cinfo.block_size;
			jpeg_component_info.Width_in_blocks = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_width * jpeg_component_info.H_samp_factor, m_cinfo.m_max_h_samp_factor * m_cinfo.block_size);
			jpeg_component_info.height_in_blocks = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_height * jpeg_component_info.V_samp_factor, m_cinfo.m_max_v_samp_factor * m_cinfo.block_size);
			jpeg_component_info.downsampled_width = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_width * jpeg_component_info.H_samp_factor, m_cinfo.m_max_h_samp_factor);
			jpeg_component_info.downsampled_height = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_height * jpeg_component_info.V_samp_factor, m_cinfo.m_max_v_samp_factor);
			jpeg_component_info.component_needed = true;
			jpeg_component_info.quant_table = null;
		}
		m_cinfo.m_total_iMCU_rows = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_height, m_cinfo.m_max_v_samp_factor * m_cinfo.block_size);
		if (m_cinfo.m_comps_in_scan < m_cinfo.m_num_components || m_cinfo.m_progressive_mode)
		{
			m_cinfo.m_inputctl.m_has_multiple_scans = true;
		}
		else
		{
			m_cinfo.m_inputctl.m_has_multiple_scans = false;
		}
	}

	private void latch_quant_tables()
	{
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]];
			if (jpeg_component_info.quant_table == null)
			{
				int quant_tbl_no = jpeg_component_info.Quant_tbl_no;
				if (quant_tbl_no < 0 || quant_tbl_no >= 4 || m_cinfo.m_quant_tbl_ptrs[quant_tbl_no] == null)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, quant_tbl_no);
				}
				JQUANT_TBL jQUANT_TBL = new JQUANT_TBL();
				Buffer.BlockCopy(m_cinfo.m_quant_tbl_ptrs[quant_tbl_no].quantval, 0, jQUANT_TBL.quantval, 0, jQUANT_TBL.quantval.Length * 2);
				jQUANT_TBL.Sent_table = m_cinfo.m_quant_tbl_ptrs[quant_tbl_no].Sent_table;
				jpeg_component_info.quant_table = jQUANT_TBL;
				m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]] = jpeg_component_info;
			}
		}
	}

	private void per_scan_setup()
	{
		if (m_cinfo.m_comps_in_scan == 1)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]];
			m_cinfo.m_MCUs_per_row = jpeg_component_info.Width_in_blocks;
			m_cinfo.m_MCU_rows_in_scan = jpeg_component_info.height_in_blocks;
			jpeg_component_info.MCU_width = 1;
			jpeg_component_info.MCU_height = 1;
			jpeg_component_info.MCU_blocks = 1;
			jpeg_component_info.MCU_sample_width = jpeg_component_info.DCT_h_scaled_size;
			jpeg_component_info.last_col_width = 1;
			int num = jpeg_component_info.height_in_blocks % jpeg_component_info.V_samp_factor;
			if (num == 0)
			{
				num = jpeg_component_info.V_samp_factor;
			}
			jpeg_component_info.last_row_height = num;
			m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[0]] = jpeg_component_info;
			m_cinfo.m_blocks_in_MCU = 1;
			m_cinfo.m_MCU_membership[0] = 0;
			return;
		}
		if (m_cinfo.m_comps_in_scan <= 0 || m_cinfo.m_comps_in_scan > 4)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_comps_in_scan, 4);
		}
		m_cinfo.m_MCUs_per_row = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_width, m_cinfo.m_max_h_samp_factor * m_cinfo.block_size);
		m_cinfo.m_MCU_rows_in_scan = (int)JpegUtils.jdiv_round_up(m_cinfo.m_image_height, m_cinfo.m_max_v_samp_factor * m_cinfo.block_size);
		m_cinfo.m_blocks_in_MCU = 0;
		for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
		{
			jpeg_component_info jpeg_component_info2 = m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]];
			jpeg_component_info2.MCU_width = jpeg_component_info2.H_samp_factor;
			jpeg_component_info2.MCU_height = jpeg_component_info2.V_samp_factor;
			jpeg_component_info2.MCU_blocks = jpeg_component_info2.MCU_width * jpeg_component_info2.MCU_height;
			jpeg_component_info2.MCU_sample_width = jpeg_component_info2.MCU_width * jpeg_component_info2.DCT_h_scaled_size;
			int num2 = jpeg_component_info2.Width_in_blocks % jpeg_component_info2.MCU_width;
			if (num2 == 0)
			{
				num2 = jpeg_component_info2.MCU_width;
			}
			jpeg_component_info2.last_col_width = num2;
			num2 = jpeg_component_info2.height_in_blocks % jpeg_component_info2.MCU_height;
			if (num2 == 0)
			{
				num2 = jpeg_component_info2.MCU_height;
			}
			jpeg_component_info2.last_row_height = num2;
			int mCU_blocks = jpeg_component_info2.MCU_blocks;
			if (m_cinfo.m_blocks_in_MCU + mCU_blocks > 10)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_MCU_SIZE);
			}
			m_cinfo.Comp_info[m_cinfo.m_cur_comp_info[i]] = jpeg_component_info2;
			while (mCU_blocks-- > 0)
			{
				m_cinfo.m_MCU_membership[m_cinfo.m_blocks_in_MCU++] = i;
			}
		}
	}
}

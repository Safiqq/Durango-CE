using System;

namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_comp_master
{
	private enum c_pass_type
	{
		main_pass,
		huff_opt_pass,
		output_pass
	}

	private jpeg_compress_struct m_cinfo;

	private bool m_call_pass_startup;

	private bool m_is_last_pass;

	private c_pass_type m_pass_type;

	private int m_pass_number;

	private int m_total_passes;

	private int m_scan_number;

	public jpeg_comp_master(jpeg_compress_struct cinfo, bool transcode_only)
	{
		m_cinfo = cinfo;
		if (transcode_only)
		{
			if (cinfo.m_optimize_coding)
			{
				m_pass_type = c_pass_type.huff_opt_pass;
			}
			else
			{
				m_pass_type = c_pass_type.output_pass;
			}
		}
		else
		{
			m_pass_type = c_pass_type.main_pass;
		}
		if (cinfo.m_optimize_coding)
		{
			m_total_passes = cinfo.m_num_scans * 2;
		}
		else
		{
			m_total_passes = cinfo.m_num_scans;
		}
	}

	public void prepare_for_pass()
	{
		switch (m_pass_type)
		{
		case c_pass_type.main_pass:
			prepare_for_main_pass();
			break;
		case c_pass_type.huff_opt_pass:
			if (prepare_for_huff_opt_pass())
			{
				prepare_for_output_pass();
			}
			break;
		case c_pass_type.output_pass:
			prepare_for_output_pass();
			break;
		default:
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
			break;
		}
		m_is_last_pass = m_pass_number == m_total_passes - 1;
		if (m_cinfo.m_progress != null)
		{
			m_cinfo.m_progress.Completed_passes = m_pass_number;
			m_cinfo.m_progress.Total_passes = m_total_passes;
		}
	}

	public void pass_startup()
	{
		m_cinfo.m_master.m_call_pass_startup = false;
		m_cinfo.m_marker.write_frame_header();
		m_cinfo.m_marker.write_scan_header();
	}

	public void finish_pass()
	{
		m_cinfo.m_entropy.finish_pass();
		switch (m_pass_type)
		{
		case c_pass_type.main_pass:
			m_pass_type = c_pass_type.output_pass;
			if (!m_cinfo.m_optimize_coding)
			{
				m_scan_number++;
			}
			break;
		case c_pass_type.huff_opt_pass:
			m_pass_type = c_pass_type.output_pass;
			break;
		case c_pass_type.output_pass:
			if (m_cinfo.m_optimize_coding)
			{
				m_pass_type = c_pass_type.huff_opt_pass;
			}
			m_scan_number++;
			break;
		}
		m_pass_number++;
	}

	public bool IsLastPass()
	{
		return m_is_last_pass;
	}

	public bool MustCallPassStartup()
	{
		return m_call_pass_startup;
	}

	private void prepare_for_main_pass()
	{
		select_scan_parameters();
		per_scan_setup();
		if (!m_cinfo.m_raw_data_in)
		{
			m_cinfo.m_cconvert.start_pass();
			m_cinfo.m_prep.start_pass(J_BUF_MODE.JBUF_PASS_THRU);
		}
		m_cinfo.m_fdct.start_pass();
		m_cinfo.m_entropy.start_pass(m_cinfo.m_optimize_coding);
		m_cinfo.m_coef.start_pass((m_total_passes > 1) ? J_BUF_MODE.JBUF_SAVE_AND_PASS : J_BUF_MODE.JBUF_PASS_THRU);
		m_cinfo.m_main.start_pass(J_BUF_MODE.JBUF_PASS_THRU);
		if (m_cinfo.m_optimize_coding)
		{
			m_call_pass_startup = false;
		}
		else
		{
			m_call_pass_startup = true;
		}
	}

	private bool prepare_for_huff_opt_pass()
	{
		select_scan_parameters();
		per_scan_setup();
		if (m_cinfo.m_Ss != 0 || m_cinfo.m_Ah == 0)
		{
			m_cinfo.m_entropy.start_pass(gather_statistics: true);
			m_cinfo.m_coef.start_pass(J_BUF_MODE.JBUF_CRANK_DEST);
			m_call_pass_startup = false;
			return false;
		}
		m_pass_type = c_pass_type.output_pass;
		m_pass_number++;
		return true;
	}

	private void prepare_for_output_pass()
	{
		if (!m_cinfo.m_optimize_coding)
		{
			select_scan_parameters();
			per_scan_setup();
		}
		m_cinfo.m_entropy.start_pass(gather_statistics: false);
		m_cinfo.m_coef.start_pass(J_BUF_MODE.JBUF_CRANK_DEST);
		if (m_scan_number == 0)
		{
			m_cinfo.m_marker.write_frame_header();
		}
		m_cinfo.m_marker.write_scan_header();
		m_call_pass_startup = false;
	}

	private void select_scan_parameters()
	{
		if (m_cinfo.m_scan_info != null)
		{
			jpeg_scan_info jpeg_scan_info2 = m_cinfo.m_scan_info[m_scan_number];
			m_cinfo.m_comps_in_scan = jpeg_scan_info2.comps_in_scan;
			for (int i = 0; i < jpeg_scan_info2.comps_in_scan; i++)
			{
				m_cinfo.m_cur_comp_info[i] = jpeg_scan_info2.component_index[i];
			}
			if (m_cinfo.m_progressive_mode)
			{
				m_cinfo.m_Ss = jpeg_scan_info2.Ss;
				m_cinfo.m_Se = jpeg_scan_info2.Se;
				m_cinfo.m_Ah = jpeg_scan_info2.Ah;
				m_cinfo.m_Al = jpeg_scan_info2.Al;
				return;
			}
		}
		else
		{
			if (m_cinfo.m_num_components > 4)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_num_components, 4);
			}
			m_cinfo.m_comps_in_scan = m_cinfo.m_num_components;
			for (int j = 0; j < m_cinfo.m_num_components; j++)
			{
				m_cinfo.m_cur_comp_info[j] = j;
			}
		}
		m_cinfo.m_Ss = 0;
		m_cinfo.m_Se = m_cinfo.block_size * m_cinfo.block_size - 1;
		m_cinfo.m_Ah = 0;
		m_cinfo.m_Al = 0;
	}

	private void per_scan_setup()
	{
		if (m_cinfo.m_comps_in_scan == 1)
		{
			int num = m_cinfo.m_cur_comp_info[0];
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[num];
			m_cinfo.m_MCUs_per_row = jpeg_component_info.Width_in_blocks;
			m_cinfo.m_MCU_rows_in_scan = jpeg_component_info.height_in_blocks;
			jpeg_component_info.MCU_width = 1;
			jpeg_component_info.MCU_height = 1;
			jpeg_component_info.MCU_blocks = 1;
			jpeg_component_info.MCU_sample_width = jpeg_component_info.DCT_h_scaled_size;
			jpeg_component_info.last_col_width = 1;
			int num2 = jpeg_component_info.height_in_blocks % jpeg_component_info.V_samp_factor;
			if (num2 == 0)
			{
				num2 = jpeg_component_info.V_samp_factor;
			}
			jpeg_component_info.last_row_height = num2;
			m_cinfo.m_blocks_in_MCU = 1;
			m_cinfo.m_MCU_membership[0] = 0;
		}
		else
		{
			if (m_cinfo.m_comps_in_scan <= 0 || m_cinfo.m_comps_in_scan > 4)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_COMPONENT_COUNT, m_cinfo.m_comps_in_scan, 4);
			}
			m_cinfo.m_MCUs_per_row = (int)JpegUtils.jdiv_round_up(m_cinfo.jpeg_width, m_cinfo.m_max_h_samp_factor * m_cinfo.block_size);
			m_cinfo.m_MCU_rows_in_scan = (int)JpegUtils.jdiv_round_up(m_cinfo.jpeg_height, m_cinfo.m_max_v_samp_factor * m_cinfo.block_size);
			m_cinfo.m_blocks_in_MCU = 0;
			for (int i = 0; i < m_cinfo.m_comps_in_scan; i++)
			{
				int num3 = m_cinfo.m_cur_comp_info[i];
				jpeg_component_info jpeg_component_info2 = m_cinfo.Component_info[num3];
				jpeg_component_info2.MCU_width = jpeg_component_info2.H_samp_factor;
				jpeg_component_info2.MCU_height = jpeg_component_info2.V_samp_factor;
				jpeg_component_info2.MCU_blocks = jpeg_component_info2.MCU_width * jpeg_component_info2.MCU_height;
				jpeg_component_info2.MCU_sample_width = jpeg_component_info2.MCU_width * jpeg_component_info2.DCT_h_scaled_size;
				int num4 = jpeg_component_info2.Width_in_blocks % jpeg_component_info2.MCU_width;
				if (num4 == 0)
				{
					num4 = jpeg_component_info2.MCU_width;
				}
				jpeg_component_info2.last_col_width = num4;
				num4 = jpeg_component_info2.height_in_blocks % jpeg_component_info2.MCU_height;
				if (num4 == 0)
				{
					num4 = jpeg_component_info2.MCU_height;
				}
				jpeg_component_info2.last_row_height = num4;
				int mCU_blocks = jpeg_component_info2.MCU_blocks;
				if (m_cinfo.m_blocks_in_MCU + mCU_blocks > 10)
				{
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_MCU_SIZE);
				}
				while (mCU_blocks-- > 0)
				{
					m_cinfo.m_MCU_membership[m_cinfo.m_blocks_in_MCU++] = i;
				}
			}
		}
		if (m_cinfo.m_restart_in_rows > 0)
		{
			int val = m_cinfo.m_restart_in_rows * m_cinfo.m_MCUs_per_row;
			m_cinfo.m_restart_interval = Math.Min(val, 65535);
		}
	}
}

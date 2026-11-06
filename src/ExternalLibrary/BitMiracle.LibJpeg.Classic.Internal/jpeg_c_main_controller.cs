namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_c_main_controller
{
	private jpeg_compress_struct m_cinfo;

	private int m_cur_iMCU_row;

	private int m_rowgroup_ctr;

	private bool m_suspended;

	private byte[][][] m_buffer = new byte[10][][];

	public jpeg_c_main_controller(jpeg_compress_struct cinfo)
	{
		m_cinfo = cinfo;
		for (int i = 0; i < cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = cinfo.Component_info[i];
			m_buffer[i] = jpeg_common_struct.AllocJpegSamples(jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size, jpeg_component_info.V_samp_factor * jpeg_component_info.DCT_v_scaled_size);
		}
	}

	public void start_pass(J_BUF_MODE pass_mode)
	{
		if (!m_cinfo.m_raw_data_in)
		{
			m_cur_iMCU_row = 0;
			m_rowgroup_ctr = 0;
			m_suspended = false;
			if (pass_mode != J_BUF_MODE.JBUF_PASS_THRU)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE);
			}
		}
	}

	public void process_data(byte[][] input_buf, ref int in_row_ctr, int in_rows_avail)
	{
		while (m_cur_iMCU_row < m_cinfo.m_total_iMCU_rows)
		{
			if (m_rowgroup_ctr < m_cinfo.min_DCT_v_scaled_size)
			{
				m_cinfo.m_prep.pre_process_data(input_buf, ref in_row_ctr, in_rows_avail, m_buffer, ref m_rowgroup_ctr, m_cinfo.min_DCT_v_scaled_size);
			}
			if (m_rowgroup_ctr != m_cinfo.min_DCT_v_scaled_size)
			{
				break;
			}
			if (!m_cinfo.m_coef.compress_data(m_buffer))
			{
				if (!m_suspended)
				{
					in_row_ctr--;
					m_suspended = true;
				}
				break;
			}
			if (m_suspended)
			{
				in_row_ctr++;
				m_suspended = false;
			}
			m_rowgroup_ctr = 0;
			m_cur_iMCU_row++;
		}
	}
}

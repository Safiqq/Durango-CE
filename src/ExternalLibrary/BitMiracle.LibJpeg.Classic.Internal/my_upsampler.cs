namespace BitMiracle.LibJpeg.Classic.Internal;

internal class my_upsampler : jpeg_upsampler
{
	private enum ComponentUpsampler
	{
		noop_upsampler,
		fullsize_upsampler,
		h2v1_upsampler,
		h2v2_upsampler,
		int_upsampler
	}

	private jpeg_decompress_struct m_cinfo;

	private ComponentBuffer[] m_color_buf = new ComponentBuffer[10];

	private int[] m_perComponentOffsets = new int[10];

	private ComponentUpsampler[] m_upsampleMethods = new ComponentUpsampler[10];

	private int m_currentComponent;

	private int m_upsampleRowOffset;

	private int m_next_row_out;

	private int m_rows_to_go;

	private int[] m_rowgroup_height = new int[10];

	private byte[] m_h_expand = new byte[10];

	private byte[] m_v_expand = new byte[10];

	public my_upsampler(jpeg_decompress_struct cinfo)
	{
		m_cinfo = cinfo;
		m_need_context_rows = false;
		if (cinfo.m_CCIR601_sampling)
		{
			cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL);
		}
		for (int i = 0; i < cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = cinfo.Comp_info[i];
			int num = jpeg_component_info.H_samp_factor * jpeg_component_info.DCT_h_scaled_size / cinfo.min_DCT_h_scaled_size;
			int num2 = jpeg_component_info.V_samp_factor * jpeg_component_info.DCT_v_scaled_size / cinfo.min_DCT_v_scaled_size;
			int max_h_samp_factor = cinfo.m_max_h_samp_factor;
			int max_v_samp_factor = cinfo.m_max_v_samp_factor;
			m_rowgroup_height[i] = num2;
			if (!jpeg_component_info.component_needed)
			{
				m_upsampleMethods[i] = ComponentUpsampler.noop_upsampler;
				continue;
			}
			if (num == max_h_samp_factor && num2 == max_v_samp_factor)
			{
				m_upsampleMethods[i] = ComponentUpsampler.fullsize_upsampler;
				continue;
			}
			if (num * 2 == max_h_samp_factor && num2 == max_v_samp_factor)
			{
				m_upsampleMethods[i] = ComponentUpsampler.h2v1_upsampler;
			}
			else if (num * 2 == max_h_samp_factor && num2 * 2 == max_v_samp_factor)
			{
				m_upsampleMethods[i] = ComponentUpsampler.h2v2_upsampler;
			}
			else if (max_h_samp_factor % num == 0 && max_v_samp_factor % num2 == 0)
			{
				m_upsampleMethods[i] = ComponentUpsampler.int_upsampler;
				m_h_expand[i] = (byte)(max_h_samp_factor / num);
				m_v_expand[i] = (byte)(max_v_samp_factor / num2);
			}
			else
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL);
			}
			ComponentBuffer componentBuffer = new ComponentBuffer();
			componentBuffer.SetBuffer(jpeg_common_struct.AllocJpegSamples(JpegUtils.jround_up(cinfo.m_output_width, cinfo.m_max_h_samp_factor), cinfo.m_max_v_samp_factor), null, 0);
			m_color_buf[i] = componentBuffer;
		}
	}

	public override void start_pass()
	{
		m_next_row_out = m_cinfo.m_max_v_samp_factor;
		m_rows_to_go = m_cinfo.m_output_height;
	}

	public override void upsample(ComponentBuffer[] input_buf, ref int in_row_group_ctr, int in_row_groups_avail, byte[][] output_buf, ref int out_row_ctr, int out_rows_avail)
	{
		if (m_next_row_out >= m_cinfo.m_max_v_samp_factor)
		{
			for (int i = 0; i < m_cinfo.m_num_components; i++)
			{
				m_perComponentOffsets[i] = 0;
				m_currentComponent = i;
				m_upsampleRowOffset = in_row_group_ctr * m_rowgroup_height[i];
				upsampleComponent(ref input_buf[i]);
			}
			m_next_row_out = 0;
		}
		int num = m_cinfo.m_max_v_samp_factor - m_next_row_out;
		if (num > m_rows_to_go)
		{
			num = m_rows_to_go;
		}
		out_rows_avail -= out_row_ctr;
		if (num > out_rows_avail)
		{
			num = out_rows_avail;
		}
		m_cinfo.m_cconvert.color_convert(m_color_buf, m_perComponentOffsets, m_next_row_out, output_buf, out_row_ctr, num);
		out_row_ctr += num;
		m_rows_to_go -= num;
		m_next_row_out += num;
		if (m_next_row_out >= m_cinfo.m_max_v_samp_factor)
		{
			in_row_group_ctr++;
		}
	}

	private void upsampleComponent(ref ComponentBuffer input_data)
	{
		switch (m_upsampleMethods[m_currentComponent])
		{
		case ComponentUpsampler.noop_upsampler:
			noop_upsample();
			break;
		case ComponentUpsampler.fullsize_upsampler:
			fullsize_upsample(ref input_data);
			break;
		case ComponentUpsampler.h2v1_upsampler:
			h2v1_upsample(ref input_data);
			break;
		case ComponentUpsampler.h2v2_upsampler:
			h2v2_upsample(ref input_data);
			break;
		case ComponentUpsampler.int_upsampler:
			int_upsample(ref input_data);
			break;
		default:
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
			break;
		}
	}

	private static void noop_upsample()
	{
	}

	private void fullsize_upsample(ref ComponentBuffer input_data)
	{
		m_color_buf[m_currentComponent] = input_data;
		m_perComponentOffsets[m_currentComponent] = m_upsampleRowOffset;
	}

	private void h2v1_upsample(ref ComponentBuffer input_data)
	{
		ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
		for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i++)
		{
			int i2 = m_upsampleRowOffset + i;
			int num = 0;
			int num2 = 0;
			while (num < m_cinfo.m_output_width)
			{
				byte b = input_data[i2][num2];
				componentBuffer[i][num] = b;
				num++;
				componentBuffer[i][num] = b;
				num++;
				num2++;
			}
		}
	}

	private void h2v2_upsample(ref ComponentBuffer input_data)
	{
		ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
		int num = 0;
		for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i += 2)
		{
			int i2 = m_upsampleRowOffset + num;
			int num2 = 0;
			int num3 = 0;
			while (num2 < m_cinfo.m_output_width)
			{
				byte b = input_data[i2][num3];
				componentBuffer[i][num2] = b;
				num2++;
				componentBuffer[i][num2] = b;
				num2++;
				num3++;
			}
			JpegUtils.jcopy_sample_rows(componentBuffer, i, componentBuffer, i + 1, 1, m_cinfo.m_output_width);
			num++;
		}
	}

	private void int_upsample(ref ComponentBuffer input_data)
	{
		ComponentBuffer componentBuffer = m_color_buf[m_currentComponent];
		int num = m_h_expand[m_currentComponent];
		int num2 = m_v_expand[m_currentComponent];
		int num3 = 0;
		for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i += num2)
		{
			int i2 = m_upsampleRowOffset + num3;
			for (int j = 0; j < m_cinfo.m_output_width; j++)
			{
				byte b = input_data[i2][j];
				int num4 = 0;
				for (int num5 = num; num5 > 0; num5--)
				{
					componentBuffer[i][num4] = b;
					num4++;
				}
			}
			if (num2 > 1)
			{
				JpegUtils.jcopy_sample_rows(componentBuffer, i, componentBuffer, i + 1, num2 - 1, m_cinfo.m_output_width);
			}
			num3++;
		}
	}
}

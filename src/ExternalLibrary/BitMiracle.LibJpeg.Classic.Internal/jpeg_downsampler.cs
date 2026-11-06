namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_downsampler
{
	private enum downSampleMethod
	{
		fullsize_smooth_downsampler,
		fullsize_downsampler,
		h2v1_downsampler,
		h2v2_smooth_downsampler,
		h2v2_downsampler,
		int_downsampler
	}

	private downSampleMethod[] m_downSamplers = new downSampleMethod[10];

	private int[] rowgroup_height = new int[10];

	private byte[] h_expand = new byte[10];

	private byte[] v_expand = new byte[10];

	private jpeg_compress_struct m_cinfo;

	private bool m_need_context_rows;

	public jpeg_downsampler(jpeg_compress_struct cinfo)
	{
		m_cinfo = cinfo;
		m_need_context_rows = false;
		if (cinfo.m_CCIR601_sampling)
		{
			cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL);
		}
		bool flag = true;
		for (int i = 0; i < cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = cinfo.Component_info[i];
			int num = jpeg_component_info.H_samp_factor * jpeg_component_info.DCT_h_scaled_size / m_cinfo.min_DCT_h_scaled_size;
			int num2 = jpeg_component_info.V_samp_factor * jpeg_component_info.DCT_v_scaled_size / m_cinfo.min_DCT_v_scaled_size;
			int max_h_samp_factor = m_cinfo.m_max_h_samp_factor;
			int max_v_samp_factor = m_cinfo.m_max_v_samp_factor;
			rowgroup_height[i] = num2;
			if (max_h_samp_factor == num && max_v_samp_factor == num2)
			{
				if (cinfo.m_smoothing_factor != 0)
				{
					m_downSamplers[i] = downSampleMethod.fullsize_smooth_downsampler;
					m_need_context_rows = true;
				}
				else
				{
					m_downSamplers[i] = downSampleMethod.fullsize_downsampler;
				}
			}
			else if (max_h_samp_factor == num * 2 && max_v_samp_factor == num2)
			{
				flag = false;
				m_downSamplers[i] = downSampleMethod.h2v1_downsampler;
			}
			else if (max_h_samp_factor == num * 2 && max_v_samp_factor == num2 * 2)
			{
				if (cinfo.m_smoothing_factor != 0)
				{
					m_downSamplers[i] = downSampleMethod.h2v2_smooth_downsampler;
					m_need_context_rows = true;
				}
				else
				{
					m_downSamplers[i] = downSampleMethod.h2v2_downsampler;
				}
			}
			else if (max_h_samp_factor % num == 0 && max_v_samp_factor % num2 == 0)
			{
				flag = false;
				m_downSamplers[i] = downSampleMethod.int_downsampler;
				h_expand[i] = (byte)(max_h_samp_factor / num);
				v_expand[i] = (byte)(max_v_samp_factor / num2);
			}
			else
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL);
			}
		}
		if (cinfo.m_smoothing_factor != 0 && !flag)
		{
			cinfo.TRACEMS(0, J_MESSAGE_CODE.JTRC_SMOOTH_NOTIMPL);
		}
	}

	public void downsample(byte[][][] input_buf, int in_row_index, byte[][][] output_buf, int out_row_group_index)
	{
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			int startOutRow = out_row_group_index * rowgroup_height[i];
			switch (m_downSamplers[i])
			{
			case downSampleMethod.fullsize_smooth_downsampler:
				fullsize_smooth_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
				break;
			case downSampleMethod.fullsize_downsampler:
				fullsize_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
				break;
			case downSampleMethod.h2v1_downsampler:
				h2v1_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
				break;
			case downSampleMethod.h2v2_smooth_downsampler:
				h2v2_smooth_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
				break;
			case downSampleMethod.h2v2_downsampler:
				h2v2_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
				break;
			case downSampleMethod.int_downsampler:
				int_downsample(i, input_buf[i], in_row_index, output_buf[i], startOutRow);
				break;
			}
		}
	}

	public bool NeedContextRows()
	{
		return m_need_context_rows;
	}

	private void int_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
	{
		jpeg_component_info jpeg_component_info = m_cinfo.Component_info[componentIndex];
		int num = jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size;
		int num2 = h_expand[jpeg_component_info.Component_index];
		expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, num * num2);
		int num3 = v_expand[jpeg_component_info.Component_index];
		int num4 = num2 * num3;
		int num5 = num4 / 2;
		int num6 = 0;
		int num7 = 0;
		while (num6 < m_cinfo.m_max_v_samp_factor)
		{
			int num8 = 0;
			int num9 = 0;
			while (num8 < num)
			{
				int num10 = 0;
				for (int i = 0; i < num3; i++)
				{
					for (int j = 0; j < num2; j++)
					{
						num10 += input_data[startInputRow + num6 + i][num9 + j];
					}
				}
				output_data[startOutRow + num7][num8] = (byte)((num10 + num5) / num4);
				num8++;
				num9 += num2;
			}
			num6 += num3;
			num7++;
		}
	}

	private void fullsize_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
	{
		JpegUtils.jcopy_sample_rows(input_data, startInputRow, output_data, startOutRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width);
		jpeg_component_info jpeg_component_info = m_cinfo.Component_info[componentIndex];
		expand_right_edge(output_data, startOutRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size);
	}

	private void h2v1_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
	{
		jpeg_component_info jpeg_component_info = m_cinfo.Component_info[componentIndex];
		int num = jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size;
		expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, num * 2);
		for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i++)
		{
			int num2 = 0;
			int num3 = 0;
			for (int j = 0; j < num; j++)
			{
				output_data[startOutRow + i][j] = (byte)(input_data[startInputRow + i][num3] + input_data[startInputRow + i][num3 + 1] + num2 >> 1);
				num2 ^= 1;
				num3 += 2;
			}
		}
	}

	private void h2v2_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
	{
		jpeg_component_info jpeg_component_info = m_cinfo.Component_info[componentIndex];
		int num = jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size;
		expand_right_edge(input_data, startInputRow, m_cinfo.m_max_v_samp_factor, m_cinfo.m_image_width, num * 2);
		int num2 = 0;
		int num3 = 0;
		while (num2 < m_cinfo.m_max_v_samp_factor)
		{
			int num4 = 1;
			int num5 = 0;
			for (int i = 0; i < num; i++)
			{
				output_data[startOutRow + num3][i] = (byte)(input_data[startInputRow + num2][num5] + input_data[startInputRow + num2][num5 + 1] + input_data[startInputRow + num2 + 1][num5] + input_data[startInputRow + num2 + 1][num5 + 1] + num4 >> 2);
				num4 ^= 3;
				num5 += 2;
			}
			num2 += 2;
			num3++;
		}
	}

	private void h2v2_smooth_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
	{
		jpeg_component_info jpeg_component_info = m_cinfo.Component_info[componentIndex];
		int num = jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size;
		expand_right_edge(input_data, startInputRow - 1, m_cinfo.m_max_v_samp_factor + 2, m_cinfo.m_image_width, num * 2);
		int num2 = 16384 - m_cinfo.m_smoothing_factor * 80;
		int num3 = m_cinfo.m_smoothing_factor * 16;
		int num4 = 0;
		int num5 = 0;
		while (num4 < m_cinfo.m_max_v_samp_factor)
		{
			int num6 = 0;
			int num7 = 0;
			int num8 = 0;
			int num9 = 0;
			int num10 = 0;
			int num11 = input_data[startInputRow + num4][num7] + input_data[startInputRow + num4][num7 + 1] + input_data[startInputRow + num4 + 1][num8] + input_data[startInputRow + num4 + 1][num8 + 1];
			int num12 = input_data[startInputRow + num4 - 1][num9] + input_data[startInputRow + num4 - 1][num9 + 1] + input_data[startInputRow + num4 + 2][num10] + input_data[startInputRow + num4 + 2][num10 + 1] + input_data[startInputRow + num4][num7] + input_data[startInputRow + num4][num7 + 2] + input_data[startInputRow + num4 + 1][num8] + input_data[startInputRow + num4 + 1][num8 + 2];
			num12 += num12;
			num12 += input_data[startInputRow + num4 - 1][num9] + input_data[startInputRow + num4 - 1][num9 + 2] + input_data[startInputRow + num4 + 2][num10] + input_data[startInputRow + num4 + 2][num10 + 2];
			num11 = num11 * num2 + num12 * num3;
			output_data[startOutRow + num5][num6] = (byte)(num11 + 32768 >> 16);
			num6++;
			num7 += 2;
			num8 += 2;
			num9 += 2;
			num10 += 2;
			for (int num13 = num - 2; num13 > 0; num13--)
			{
				num11 = input_data[startInputRow + num4][num7] + input_data[startInputRow + num4][num7 + 1] + input_data[startInputRow + num4 + 1][num8] + input_data[startInputRow + num4 + 1][num8 + 1];
				num12 = input_data[startInputRow + num4 - 1][num9] + input_data[startInputRow + num4 - 1][num9 + 1] + input_data[startInputRow + num4 + 2][num10] + input_data[startInputRow + num4 + 2][num10 + 1] + input_data[startInputRow + num4][num7 - 1] + input_data[startInputRow + num4][num7 + 2] + input_data[startInputRow + num4 + 1][num8 - 1] + input_data[startInputRow + num4 + 1][num8 + 2];
				num12 += num12;
				num12 += input_data[startInputRow + num4 - 1][num9 - 1] + input_data[startInputRow + num4 - 1][num9 + 2] + input_data[startInputRow + num4 + 2][num10 - 1] + input_data[startInputRow + num4 + 2][num10 + 2];
				num11 = num11 * num2 + num12 * num3;
				output_data[startOutRow + num5][num6] = (byte)(num11 + 32768 >> 16);
				num6++;
				num7 += 2;
				num8 += 2;
				num9 += 2;
				num10 += 2;
			}
			num11 = input_data[startInputRow + num4][num7] + input_data[startInputRow + num4][num7 + 1] + input_data[startInputRow + num4 + 1][num8] + input_data[startInputRow + num4 + 1][num8 + 1];
			num12 = input_data[startInputRow + num4 - 1][num9] + input_data[startInputRow + num4 - 1][num9 + 1] + input_data[startInputRow + num4 + 2][num10] + input_data[startInputRow + num4 + 2][num10 + 1] + input_data[startInputRow + num4][num7 - 1] + input_data[startInputRow + num4][num7 + 1] + input_data[startInputRow + num4 + 1][num8 - 1] + input_data[startInputRow + num4 + 1][num8 + 1];
			num12 += num12;
			num12 += input_data[startInputRow + num4 - 1][num9 - 1] + input_data[startInputRow + num4 - 1][num9 + 1] + input_data[startInputRow + num4 + 2][num10 - 1] + input_data[startInputRow + num4 + 2][num10 + 1];
			num11 = num11 * num2 + num12 * num3;
			output_data[startOutRow + num5][num6] = (byte)(num11 + 32768 >> 16);
			num4 += 2;
			num5++;
		}
	}

	private void fullsize_smooth_downsample(int componentIndex, byte[][] input_data, int startInputRow, byte[][] output_data, int startOutRow)
	{
		jpeg_component_info jpeg_component_info = m_cinfo.Component_info[componentIndex];
		int num = jpeg_component_info.Width_in_blocks * jpeg_component_info.DCT_h_scaled_size;
		expand_right_edge(input_data, startInputRow - 1, m_cinfo.m_max_v_samp_factor + 2, m_cinfo.m_image_width, num);
		int num2 = 65536 - m_cinfo.m_smoothing_factor * 512;
		int num3 = m_cinfo.m_smoothing_factor * 64;
		for (int i = 0; i < m_cinfo.m_max_v_samp_factor; i++)
		{
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			int num8 = input_data[startInputRow + i - 1][num6] + input_data[startInputRow + i + 1][num7] + input_data[startInputRow + i][num5];
			num6++;
			num7++;
			int num9 = input_data[startInputRow + i][num5];
			num5++;
			int num10 = input_data[startInputRow + i - 1][num6] + input_data[startInputRow + i + 1][num7] + input_data[startInputRow + i][num5];
			int num11 = num8 + (num8 - num9) + num10;
			num9 = num9 * num2 + num11 * num3;
			output_data[startOutRow + i][num4] = (byte)(num9 + 32768 >> 16);
			num4++;
			int num12 = num8;
			num8 = num10;
			for (int num13 = num - 2; num13 > 0; num13--)
			{
				num9 = input_data[startInputRow + i][num5];
				num5++;
				num6++;
				num7++;
				num10 = input_data[startInputRow + i - 1][num6] + input_data[startInputRow + i + 1][num7] + input_data[startInputRow + i][num5];
				num11 = num12 + (num8 - num9) + num10;
				num9 = num9 * num2 + num11 * num3;
				output_data[startOutRow + i][num4] = (byte)(num9 + 32768 >> 16);
				num4++;
				num12 = num8;
				num8 = num10;
			}
			num9 = input_data[startInputRow + i][num5];
			num11 = num12 + (num8 - num9) + num8;
			num9 = num9 * num2 + num11 * num3;
			output_data[startOutRow + i][num4] = (byte)(num9 + 32768 >> 16);
		}
	}

	private static void expand_right_edge(byte[][] image_data, int startInputRow, int num_rows, int input_cols, int output_cols)
	{
		int num = output_cols - input_cols;
		if (num <= 0)
		{
			return;
		}
		for (int i = startInputRow; i < startInputRow + num_rows; i++)
		{
			byte b = image_data[i][input_cols - 1];
			for (int j = 0; j < num; j++)
			{
				image_data[i][input_cols + j] = b;
			}
		}
	}
}

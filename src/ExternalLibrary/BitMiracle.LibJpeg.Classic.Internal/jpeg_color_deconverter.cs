namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_color_deconverter
{
	private delegate void color_convert_func(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows);

	private const int SCALEBITS = 16;

	private const int ONE_HALF = 32768;

	private const int R_Y_OFF = 0;

	private const int G_Y_OFF = 256;

	private const int B_Y_OFF = 512;

	private const int TABLE_SIZE = 768;

	private color_convert_func m_converter;

	private jpeg_decompress_struct m_cinfo;

	private int[] m_perComponentOffsets;

	private int[] m_Cr_r_tab;

	private int[] m_Cb_b_tab;

	private int[] m_Cr_g_tab;

	private int[] m_Cb_g_tab;

	private int[] rgb_y_tab;

	public jpeg_color_deconverter(jpeg_decompress_struct cinfo)
	{
		m_cinfo = cinfo;
		switch (cinfo.m_jpeg_color_space)
		{
		case J_COLOR_SPACE.JCS_GRAYSCALE:
			if (cinfo.m_num_components != 1)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
			}
			break;
		case J_COLOR_SPACE.JCS_RGB:
		case J_COLOR_SPACE.JCS_YCbCr:
		case J_COLOR_SPACE.JCS_BG_RGB:
		case J_COLOR_SPACE.JCS_BG_YCC:
			if (cinfo.m_num_components != 3)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
			}
			break;
		case J_COLOR_SPACE.JCS_CMYK:
		case J_COLOR_SPACE.JCS_YCCK:
			if (cinfo.m_num_components != 4)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
			}
			break;
		case J_COLOR_SPACE.JCS_NCHANNEL:
			if (cinfo.m_num_components < 1)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
			}
			break;
		default:
			if (cinfo.m_num_components < 1)
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE);
			}
			break;
		}
		if (cinfo.color_transform != J_COLOR_TRANSFORM.JCT_NONE && cinfo.m_jpeg_color_space != J_COLOR_SPACE.JCS_RGB && cinfo.m_jpeg_color_space != J_COLOR_SPACE.JCS_BG_RGB)
		{
			cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
		}
		switch (cinfo.m_out_color_space)
		{
		case J_COLOR_SPACE.JCS_GRAYSCALE:
			cinfo.m_out_color_components = 1;
			switch (cinfo.m_jpeg_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
			case J_COLOR_SPACE.JCS_YCbCr:
			case J_COLOR_SPACE.JCS_BG_YCC:
			{
				m_converter = grayscale_convert;
				for (int i = 1; i < cinfo.m_num_components; i++)
				{
					cinfo.Comp_info[i].component_needed = false;
				}
				break;
			}
			case J_COLOR_SPACE.JCS_RGB:
				switch (cinfo.color_transform)
				{
				case J_COLOR_TRANSFORM.JCT_NONE:
					m_converter = rgb_gray_convert;
					break;
				case J_COLOR_TRANSFORM.JCT_SUBTRACT_GREEN:
					m_converter = rgb1_gray_convert;
					break;
				default:
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
					break;
				}
				build_rgb_y_table();
				break;
			default:
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				break;
			}
			break;
		case J_COLOR_SPACE.JCS_RGB:
			cinfo.m_out_color_components = 3;
			switch (cinfo.m_jpeg_color_space)
			{
			case J_COLOR_SPACE.JCS_GRAYSCALE:
				m_converter = gray_rgb_convert;
				break;
			case J_COLOR_SPACE.JCS_YCbCr:
				m_converter = ycc_rgb_convert;
				build_ycc_rgb_table();
				break;
			case J_COLOR_SPACE.JCS_BG_YCC:
				m_converter = ycc_rgb_convert;
				build_bg_ycc_rgb_table();
				break;
			case J_COLOR_SPACE.JCS_RGB:
				switch (cinfo.color_transform)
				{
				case J_COLOR_TRANSFORM.JCT_NONE:
					m_converter = rgb_convert;
					break;
				case J_COLOR_TRANSFORM.JCT_SUBTRACT_GREEN:
					m_converter = rgb1_rgb_convert;
					break;
				default:
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
					break;
				}
				break;
			case J_COLOR_SPACE.JCS_CMYK:
				m_converter = cmyk_rgb_convert;
				break;
			case J_COLOR_SPACE.JCS_YCCK:
				m_converter = ycck_rgb_convert;
				build_ycc_rgb_table();
				break;
			default:
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				break;
			}
			break;
		case J_COLOR_SPACE.JCS_BG_RGB:
			cinfo.m_out_color_components = 3;
			if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_BG_RGB)
			{
				switch (cinfo.color_transform)
				{
				case J_COLOR_TRANSFORM.JCT_NONE:
					m_converter = rgb_convert;
					break;
				case J_COLOR_TRANSFORM.JCT_SUBTRACT_GREEN:
					m_converter = rgb1_rgb_convert;
					break;
				default:
					cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
					break;
				}
			}
			else
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
			}
			break;
		case J_COLOR_SPACE.JCS_CMYK:
			cinfo.m_out_color_components = 4;
			switch (cinfo.m_jpeg_color_space)
			{
			case J_COLOR_SPACE.JCS_YCCK:
				m_converter = ycck_cmyk_convert;
				build_ycc_rgb_table();
				break;
			case J_COLOR_SPACE.JCS_CMYK:
				m_converter = null_convert;
				break;
			default:
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
				break;
			}
			break;
		case J_COLOR_SPACE.JCS_NCHANNEL:
			if (cinfo.m_jpeg_color_space == J_COLOR_SPACE.JCS_NCHANNEL)
			{
				m_converter = null_convert;
			}
			else
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
			}
			break;
		default:
			if (cinfo.m_out_color_space == cinfo.m_jpeg_color_space)
			{
				cinfo.m_out_color_components = cinfo.m_num_components;
				m_converter = null_convert;
			}
			else
			{
				cinfo.ERREXIT(J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL);
			}
			break;
		}
		if (cinfo.m_quantize_colors)
		{
			cinfo.m_output_components = 1;
		}
		else
		{
			cinfo.m_output_components = cinfo.m_out_color_components;
		}
	}

	public void color_convert(ComponentBuffer[] input_buf, int[] perComponentOffsets, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		m_perComponentOffsets = perComponentOffsets;
		m_converter(input_buf, input_row, output_buf, output_row, num_rows);
	}

	private void build_ycc_rgb_table()
	{
		m_Cr_r_tab = new int[256];
		m_Cb_b_tab = new int[256];
		m_Cr_g_tab = new int[256];
		m_Cb_g_tab = new int[256];
		int num = 0;
		int num2 = -128;
		while (num <= 255)
		{
			m_Cr_r_tab[num] = JpegUtils.RIGHT_SHIFT(FIX(1.402) * num2 + 32768, 16);
			m_Cb_b_tab[num] = JpegUtils.RIGHT_SHIFT(FIX(1.772) * num2 + 32768, 16);
			m_Cr_g_tab[num] = -FIX(0.714136286) * num2;
			m_Cb_g_tab[num] = -FIX(0.344136286) * num2 + 32768;
			num++;
			num2++;
		}
	}

	private void build_bg_ycc_rgb_table()
	{
		m_Cr_r_tab = new int[256];
		m_Cb_b_tab = new int[256];
		m_Cr_g_tab = new int[256];
		m_Cb_g_tab = new int[256];
		int num = 0;
		int num2 = -128;
		while (num <= 255)
		{
			m_Cr_r_tab[num] = JpegUtils.RIGHT_SHIFT(FIX(2.804) * num2 + 32768, 16);
			m_Cb_b_tab[num] = JpegUtils.RIGHT_SHIFT(FIX(3.544) * num2 + 32768, 16);
			m_Cr_g_tab[num] = -FIX(1.428272572) * num2;
			m_Cb_g_tab[num] = -FIX(0.688272572) * num2 + 32768;
			num++;
			num2++;
		}
	}

	private void ycc_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
		for (int i = 0; i < num_rows; i++)
		{
			int num4 = 0;
			for (int j = 0; j < m_cinfo.m_output_width; j++)
			{
				int num5 = input_buf[0][input_row + num][j];
				int num6 = input_buf[1][input_row + num2][j];
				int num7 = input_buf[2][input_row + num3][j];
				output_buf[output_row + i][num4] = sample_range_limit[sampleRangeLimitOffset + num5 + m_Cr_r_tab[num7]];
				output_buf[output_row + i][num4 + 1] = sample_range_limit[sampleRangeLimitOffset + num5 + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num6] + m_Cr_g_tab[num7], 16)];
				output_buf[output_row + i][num4 + 2] = sample_range_limit[sampleRangeLimitOffset + num5 + m_Cb_b_tab[num6]];
				num4 += 3;
			}
			input_row++;
		}
	}

	private void build_rgb_y_table()
	{
		rgb_y_tab = new int[768];
		for (int i = 0; i <= 255; i++)
		{
			rgb_y_tab[i] = FIX(0.299) * i;
			rgb_y_tab[i + 256] = FIX(0.587) * i;
			rgb_y_tab[i + 512] = FIX(0.114) * i + 32768;
		}
	}

	private void rgb_gray_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num4 = 0;
			for (int j = 0; j < output_width; j++)
			{
				int num5 = input_buf[0][input_row + num][j];
				int num6 = input_buf[1][input_row + num2][j];
				int num7 = input_buf[2][input_row + num3][j];
				output_buf[output_row + i][num4++] = (byte)(rgb_y_tab[num5] + rgb_y_tab[num6 + 256] + rgb_y_tab[num7 + 512] >> 16);
			}
		}
	}

	private void rgb1_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num4 = 0;
			for (int j = 0; j < output_width; j++)
			{
				int num5 = input_buf[0][input_row + num][j];
				int num6 = input_buf[1][input_row + num2][j];
				int num7 = input_buf[2][input_row + num3][j];
				output_buf[output_row + i][num4] = (byte)((num5 + num6 - 128) & 0xFF);
				output_buf[output_row + i][num4 + 1] = (byte)num6;
				output_buf[output_row + i][num4 + 2] = (byte)((num7 + num6 - 128) & 0xFF);
				num4 += 3;
			}
		}
	}

	private void rgb1_gray_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num4 = 0;
			for (int j = 0; j < output_width; j++)
			{
				int num5 = input_buf[0][input_row + num][j];
				int num6 = input_buf[1][input_row + num2][j];
				int num7 = input_buf[2][input_row + num3][j];
				num5 = (num5 + num6 - 128) & 0xFF;
				num7 = (num7 + num6 - 128) & 0xFF;
				output_buf[output_row + i][num4++] = (byte)(rgb_y_tab[num5] + rgb_y_tab[num6 + 256] + rgb_y_tab[num7 + 512] >> 16);
			}
		}
	}

	private void rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num4 = 0;
			for (int j = 0; j < output_width; j++)
			{
				int num5 = input_buf[0][input_row + num][j];
				int num6 = input_buf[1][input_row + num2][j];
				int num7 = input_buf[2][input_row + num3][j];
				output_buf[output_row + i][num4] = (byte)num5;
				output_buf[output_row + i][num4 + 1] = (byte)num6;
				output_buf[output_row + i][num4 + 2] = (byte)num7;
				num4 += 3;
			}
		}
	}

	private void ycck_cmyk_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int num4 = m_perComponentOffsets[3];
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num5 = 0;
			for (int j = 0; j < output_width; j++)
			{
				int num6 = input_buf[0][input_row + num][j];
				int num7 = input_buf[1][input_row + num2][j];
				int num8 = input_buf[2][input_row + num3][j];
				output_buf[output_row + i][num5] = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + m_Cr_r_tab[num8])];
				output_buf[output_row + i][num5 + 1] = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num7] + m_Cr_g_tab[num8], 16))];
				output_buf[output_row + i][num5 + 2] = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + m_Cb_b_tab[num7])];
				output_buf[output_row + i][num5 + 3] = input_buf[3][input_row + num4][j];
				num5 += 4;
			}
			input_row++;
		}
	}

	private void gray_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num4 = 0;
			for (int j = 0; j < output_width; j++)
			{
				output_buf[output_row + i][num4] = input_buf[0][input_row + num][j];
				output_buf[output_row + i][num4 + 1] = input_buf[0][input_row + num2][j];
				output_buf[output_row + i][num4 + 2] = input_buf[0][input_row + num3][j];
				num4 += 3;
			}
			input_row++;
		}
	}

	private void grayscale_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		JpegUtils.jcopy_sample_rows(input_buf[0], input_row + m_perComponentOffsets[0], output_buf, output_row, num_rows, m_cinfo.m_output_width);
	}

	private void cmyk_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int num4 = m_perComponentOffsets[3];
		for (int i = 0; i < num_rows; i++)
		{
			int num5 = 0;
			for (int j = 0; j < m_cinfo.m_output_width; j++)
			{
				int num6 = input_buf[0][input_row + num][j];
				int num7 = input_buf[1][input_row + num2][j];
				int num8 = input_buf[2][input_row + num3][j];
				int num9 = input_buf[3][input_row + num4][j];
				output_buf[output_row + i][num5] = (byte)(num6 * num9 / 255);
				output_buf[output_row + i][num5 + 1] = (byte)(num7 * num9 / 255);
				output_buf[output_row + i][num5 + 2] = (byte)(num8 * num9 / 255);
				num5 += 3;
			}
			input_row++;
		}
	}

	private void ycck_rgb_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		int num = m_perComponentOffsets[0];
		int num2 = m_perComponentOffsets[1];
		int num3 = m_perComponentOffsets[2];
		int num4 = m_perComponentOffsets[3];
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int sampleRangeLimitOffset = m_cinfo.m_sampleRangeLimitOffset;
		int output_width = m_cinfo.m_output_width;
		for (int i = 0; i < num_rows; i++)
		{
			int num5 = 0;
			for (int j = 0; j < output_width; j++)
			{
				int num6 = input_buf[0][input_row + num][j];
				int num7 = input_buf[1][input_row + num2][j];
				int num8 = input_buf[2][input_row + num3][j];
				int num9 = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + m_Cr_r_tab[num8])];
				int num10 = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + JpegUtils.RIGHT_SHIFT(m_Cb_g_tab[num7] + m_Cr_g_tab[num8], 16))];
				int num11 = sample_range_limit[sampleRangeLimitOffset + 255 - (num6 + m_Cb_b_tab[num7])];
				int num12 = input_buf[3][input_row + num4][j];
				output_buf[output_row + i][num5] = (byte)(num9 * num12 / 255);
				output_buf[output_row + i][num5 + 1] = (byte)(num10 * num12 / 255);
				output_buf[output_row + i][num5 + 2] = (byte)(num11 * num12 / 255);
				num5 += 3;
			}
			input_row++;
		}
	}

	private void null_convert(ComponentBuffer[] input_buf, int input_row, byte[][] output_buf, int output_row, int num_rows)
	{
		for (int i = 0; i < num_rows; i++)
		{
			for (int j = 0; j < m_cinfo.m_num_components; j++)
			{
				int num = 0;
				int num2 = 0;
				int num3 = m_perComponentOffsets[j];
				for (int k = 0; k < m_cinfo.m_output_width; k++)
				{
					output_buf[output_row + i][j + num2] = input_buf[j][input_row + num3][num];
					num2 += m_cinfo.m_num_components;
					num++;
				}
			}
			input_row++;
		}
	}

	private static int FIX(double x)
	{
		return (int)(x * 65536.0 + 0.5);
	}
}

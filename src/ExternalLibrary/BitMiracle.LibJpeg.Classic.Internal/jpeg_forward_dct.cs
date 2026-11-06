namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_forward_dct
{
	private delegate void forward_DCT_method_ptr(int[] data, byte[][] sample_data, int start_row, int start_col);

	private delegate void float_DCT_method_ptr(float[] data, byte[][] sample_data, int start_row, int start_col);

	public delegate void forward_DCT_ptr(jpeg_component_info compptr, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks);

	private class divisor_table
	{
		public int[] int_array = new int[64];

		public float[] float_array = new float[64];
	}

	private const int FAST_INTEGER_CONST_BITS = 8;

	private const int FAST_INTEGER_FIX_0_382683433 = 98;

	private const int FAST_INTEGER_FIX_0_541196100 = 139;

	private const int FAST_INTEGER_FIX_0_707106781 = 181;

	private const int FAST_INTEGER_FIX_1_306562965 = 334;

	private const int SLOW_INTEGER_CONST_BITS = 13;

	private const int SLOW_INTEGER_PASS1_BITS = 2;

	private const int SLOW_INTEGER_FIX_0_298631336 = 2446;

	private const int SLOW_INTEGER_FIX_0_390180644 = 3196;

	private const int SLOW_INTEGER_FIX_0_541196100 = 4433;

	private const int SLOW_INTEGER_FIX_0_765366865 = 6270;

	private const int SLOW_INTEGER_FIX_0_899976223 = 7373;

	private const int SLOW_INTEGER_FIX_1_175875602 = 9633;

	private const int SLOW_INTEGER_FIX_1_501321110 = 12299;

	private const int SLOW_INTEGER_FIX_1_847759065 = 15137;

	private const int SLOW_INTEGER_FIX_1_961570560 = 16069;

	private const int SLOW_INTEGER_FIX_2_053119869 = 16819;

	private const int SLOW_INTEGER_FIX_2_562915447 = 20995;

	private const int SLOW_INTEGER_FIX_3_072711026 = 25172;

	private const int CONST_BITS = 14;

	private static readonly short[] aanscales = new short[64]
	{
		16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520, 22725, 31521,
		29692, 26722, 22725, 17855, 12299, 6270, 21407, 29692, 27969, 25172,
		21407, 16819, 11585, 5906, 19266, 26722, 25172, 22654, 19266, 15137,
		10426, 5315, 16384, 22725, 21407, 19266, 16384, 12873, 8867, 4520,
		12873, 17855, 16819, 15137, 12873, 10114, 6967, 3552, 8867, 12299,
		11585, 10426, 8867, 6967, 4799, 2446, 4520, 6270, 5906, 5315,
		4520, 3552, 2446, 1247
	};

	private static readonly double[] aanscalefactor = new double[8] { 1.0, 1.387039845, 1.306562965, 1.175875602, 1.0, 0.785694958, 0.5411961, 0.275899379 };

	private jpeg_compress_struct m_cinfo;

	private forward_DCT_method_ptr[] do_dct = new forward_DCT_method_ptr[10];

	private float_DCT_method_ptr[] do_float_dct = new float_DCT_method_ptr[10];

	public forward_DCT_ptr[] forward_DCT = new forward_DCT_ptr[10];

	private divisor_table[] m_dctTables;

	public jpeg_forward_dct(jpeg_compress_struct cinfo)
	{
		m_cinfo = cinfo;
		m_dctTables = new divisor_table[m_cinfo.m_num_components];
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			m_dctTables[i] = new divisor_table();
		}
	}

	public virtual void start_pass()
	{
		J_DCT_METHOD j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Component_info[i];
			switch ((jpeg_component_info.DCT_h_scaled_size << 8) + jpeg_component_info.DCT_v_scaled_size)
			{
			case 257:
				do_dct[i] = jpeg_fdct_1x1;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 514:
				do_dct[i] = jpeg_fdct_2x2;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 771:
				do_dct[i] = jpeg_fdct_3x3;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1028:
				do_dct[i] = jpeg_fdct_4x4;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1285:
				do_dct[i] = jpeg_fdct_5x5;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1542:
				do_dct[i] = jpeg_fdct_6x6;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1799:
				do_dct[i] = jpeg_fdct_7x7;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2313:
				do_dct[i] = jpeg_fdct_9x9;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2570:
				do_dct[i] = jpeg_fdct_10x10;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2827:
				do_dct[i] = jpeg_fdct_11x11;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 3084:
				do_dct[i] = jpeg_fdct_12x12;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 3341:
				do_dct[i] = jpeg_fdct_13x13;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 3598:
				do_dct[i] = jpeg_fdct_14x14;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 3855:
				do_dct[i] = jpeg_fdct_15x15;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 4112:
				do_dct[i] = jpeg_fdct_16x16;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 4104:
				do_dct[i] = jpeg_fdct_16x8;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 3591:
				do_dct[i] = jpeg_fdct_14x7;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 3078:
				do_dct[i] = jpeg_fdct_12x6;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2565:
				do_dct[i] = jpeg_fdct_10x5;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2052:
				do_dct[i] = jpeg_fdct_8x4;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1539:
				do_dct[i] = jpeg_fdct_6x3;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1026:
				do_dct[i] = jpeg_fdct_4x2;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 513:
				do_dct[i] = jpeg_fdct_2x1;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2064:
				do_dct[i] = jpeg_fdct_8x16;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1806:
				do_dct[i] = jpeg_fdct_7x14;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1548:
				do_dct[i] = jpeg_fdct_6x12;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1290:
				do_dct[i] = jpeg_fdct_5x10;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 1032:
				do_dct[i] = jpeg_fdct_4x8;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 774:
				do_dct[i] = jpeg_fdct_3x6;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 516:
				do_dct[i] = jpeg_fdct_2x4;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 258:
				do_dct[i] = jpeg_fdct_1x2;
				j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
				break;
			case 2056:
				switch (m_cinfo.m_dct_method)
				{
				case J_DCT_METHOD.JDCT_ISLOW:
					do_dct[i] = jpeg_fdct_islow;
					j_DCT_METHOD = J_DCT_METHOD.JDCT_ISLOW;
					break;
				case J_DCT_METHOD.JDCT_IFAST:
					do_dct[i] = jpeg_fdct_ifast;
					j_DCT_METHOD = J_DCT_METHOD.JDCT_IFAST;
					break;
				case J_DCT_METHOD.JDCT_FLOAT:
					do_float_dct[i] = jpeg_fdct_float;
					j_DCT_METHOD = J_DCT_METHOD.JDCT_FLOAT;
					break;
				default:
					m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
					break;
				}
				break;
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_BAD_DCTSIZE, jpeg_component_info.DCT_h_scaled_size, jpeg_component_info.DCT_v_scaled_size);
				break;
			}
			int quant_tbl_no = m_cinfo.Component_info[i].Quant_tbl_no;
			if (quant_tbl_no < 0 || quant_tbl_no >= 4 || m_cinfo.m_quant_tbl_ptrs[quant_tbl_no] == null)
			{
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NO_QUANT_TABLE, quant_tbl_no);
			}
			JQUANT_TBL jQUANT_TBL = m_cinfo.m_quant_tbl_ptrs[quant_tbl_no];
			int num = 0;
			switch (j_DCT_METHOD)
			{
			case J_DCT_METHOD.JDCT_ISLOW:
			{
				int[] int_array = m_dctTables[i].int_array;
				for (num = 0; num < 64; num++)
				{
					int_array[num] = jQUANT_TBL.quantval[num] << ((!jpeg_component_info.component_needed) ? 3 : 4);
				}
				forward_DCT[i] = forwardDCTImpl;
				break;
			}
			case J_DCT_METHOD.JDCT_IFAST:
			{
				int[] int_array = m_dctTables[i].int_array;
				for (num = 0; num < 64; num++)
				{
					int_array[num] = JpegUtils.DESCALE(jQUANT_TBL.quantval[num] * aanscales[num], (!jpeg_component_info.component_needed) ? 11 : 10);
				}
				forward_DCT[i] = forwardDCTImpl;
				break;
			}
			case J_DCT_METHOD.JDCT_FLOAT:
			{
				float[] float_array = m_dctTables[i].float_array;
				num = 0;
				for (int j = 0; j < 8; j++)
				{
					for (int k = 0; k < 8; k++)
					{
						float_array[num] = (float)(1.0 / ((double)jQUANT_TBL.quantval[num] * aanscalefactor[j] * aanscalefactor[k] * ((!jpeg_component_info.component_needed) ? 8.0 : 16.0)));
						num++;
					}
				}
				forward_DCT[i] = forwardDCTFloatImpl;
				break;
			}
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
				break;
			}
		}
	}

	private void forwardDCTImpl(jpeg_component_info compptr, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
	{
		forward_DCT_method_ptr forward_DCT_method_ptr = do_dct[compptr.Component_index];
		int[] int_array = m_dctTables[compptr.Component_index].int_array;
		int[] array = new int[64];
		int num = 0;
		while (num < num_blocks)
		{
			forward_DCT_method_ptr(array, sample_data, start_row, start_col);
			for (int i = 0; i < 64; i++)
			{
				int num2 = int_array[i];
				int num3 = array[i];
				if (num3 < 0)
				{
					num3 = -num3;
					num3 += num2 >> 1;
					num3 = ((num3 >= num2) ? (num3 / num2) : 0);
					num3 = -num3;
				}
				else
				{
					num3 += num2 >> 1;
					num3 = ((num3 >= num2) ? (num3 / num2) : 0);
				}
				coef_blocks[num][i] = (short)num3;
			}
			num++;
			start_col += compptr.DCT_h_scaled_size;
		}
	}

	private void forwardDCTFloatImpl(jpeg_component_info compptr, byte[][] sample_data, JBLOCK[] coef_blocks, int start_row, int start_col, int num_blocks)
	{
		float_DCT_method_ptr float_DCT_method_ptr = do_float_dct[compptr.Component_index];
		float[] float_array = m_dctTables[compptr.Component_index].float_array;
		float[] array = new float[64];
		int num = 0;
		while (num < num_blocks)
		{
			float_DCT_method_ptr(array, sample_data, start_row, start_col);
			for (int i = 0; i < 64; i++)
			{
				float num2 = array[i] * float_array[i];
				coef_blocks[num][i] = (short)((int)(num2 + 16384.5f) - 16384);
			}
			num++;
			start_col += compptr.DCT_h_scaled_size;
		}
	}

	private static void jpeg_fdct_float(float[] data, byte[][] sample_data, int start_row, int start_col)
	{
		int num = 0;
		for (int i = 0; i < 8; i++)
		{
			byte[] array = sample_data[start_row + i];
			float num2 = array[start_col] + array[start_col + 7];
			float num3 = array[start_col] - array[start_col + 7];
			float num4 = array[start_col + 1] + array[start_col + 6];
			float num5 = array[start_col + 1] - array[start_col + 6];
			float num6 = array[start_col + 2] + array[start_col + 5];
			float num7 = array[start_col + 2] - array[start_col + 5];
			float num8 = array[start_col + 3] + array[start_col + 4];
			float num9 = array[start_col + 3] - array[start_col + 4];
			float num10 = num2 + num8;
			float num11 = num2 - num8;
			float num12 = num4 + num6;
			float num13 = num4 - num6;
			data[num] = num10 + num12 - 1024f;
			data[num + 4] = num10 - num12;
			float num14 = (num13 + num11) * 0.70710677f;
			data[num + 2] = num11 + num14;
			data[num + 6] = num11 - num14;
			num10 = num9 + num7;
			num12 = num7 + num5;
			num13 = num5 + num3;
			float num15 = (num10 - num13) * 0.38268343f;
			float num16 = 0.5411961f * num10 + num15;
			float num17 = 1.306563f * num13 + num15;
			float num18 = num12 * 0.70710677f;
			float num19 = num3 + num18;
			float num20 = num3 - num18;
			data[num + 5] = num20 + num16;
			data[num + 3] = num20 - num16;
			data[num + 1] = num19 + num17;
			data[num + 7] = num19 - num17;
			num += 8;
		}
		num = 0;
		for (int num21 = 7; num21 >= 0; num21--)
		{
			float num22 = data[num] + data[num + 56];
			float num23 = data[num] - data[num + 56];
			float num24 = data[num + 8] + data[num + 48];
			float num25 = data[num + 8] - data[num + 48];
			float num26 = data[num + 16] + data[num + 40];
			float num27 = data[num + 16] - data[num + 40];
			float num28 = data[num + 24] + data[num + 32];
			float num29 = data[num + 24] - data[num + 32];
			float num30 = num22 + num28;
			float num31 = num22 - num28;
			float num32 = num24 + num26;
			float num33 = num24 - num26;
			data[num] = num30 + num32;
			data[num + 32] = num30 - num32;
			float num34 = (num33 + num31) * 0.70710677f;
			data[num + 16] = num31 + num34;
			data[num + 48] = num31 - num34;
			num30 = num29 + num27;
			num32 = num27 + num25;
			num33 = num25 + num23;
			float num35 = (num30 - num33) * 0.38268343f;
			float num36 = 0.5411961f * num30 + num35;
			float num37 = 1.306563f * num33 + num35;
			float num38 = num32 * 0.70710677f;
			float num39 = num23 + num38;
			float num40 = num23 - num38;
			data[num + 40] = num40 + num36;
			data[num + 24] = num40 - num36;
			data[num + 8] = num39 + num37;
			data[num + 56] = num39 - num37;
			num++;
		}
	}

	private static void jpeg_fdct_ifast(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		int num = 0;
		for (int i = 0; i < 8; i++)
		{
			byte[] array = sample_data[start_row + i];
			int num2 = array[start_col] + array[start_col + 7];
			int num3 = array[start_col] - array[start_col + 7];
			int num4 = array[start_col + 1] + array[start_col + 6];
			int num5 = array[start_col + 1] - array[start_col + 6];
			int num6 = array[start_col + 2] + array[start_col + 5];
			int num7 = array[start_col + 2] - array[start_col + 5];
			int num8 = array[start_col + 3] + array[start_col + 4];
			int num9 = array[start_col + 3] - array[start_col + 4];
			int num10 = num2 + num8;
			int num11 = num2 - num8;
			int num12 = num4 + num6;
			int num13 = num4 - num6;
			data[num] = num10 + num12 - 1024;
			data[num + 4] = num10 - num12;
			int num14 = FAST_INTEGER_MULTIPLY(num13 + num11, 181);
			data[num + 2] = num11 + num14;
			data[num + 6] = num11 - num14;
			num10 = num9 + num7;
			num12 = num7 + num5;
			num13 = num5 + num3;
			int num15 = FAST_INTEGER_MULTIPLY(num10 - num13, 98);
			int num16 = FAST_INTEGER_MULTIPLY(num10, 139) + num15;
			int num17 = FAST_INTEGER_MULTIPLY(num13, 334) + num15;
			int num18 = FAST_INTEGER_MULTIPLY(num12, 181);
			int num19 = num3 + num18;
			int num20 = num3 - num18;
			data[num + 5] = num20 + num16;
			data[num + 3] = num20 - num16;
			data[num + 1] = num19 + num17;
			data[num + 7] = num19 - num17;
			num += 8;
		}
		num = 0;
		for (int num21 = 7; num21 >= 0; num21--)
		{
			int num22 = data[num] + data[num + 56];
			int num23 = data[num] - data[num + 56];
			int num24 = data[num + 8] + data[num + 48];
			int num25 = data[num + 8] - data[num + 48];
			int num26 = data[num + 16] + data[num + 40];
			int num27 = data[num + 16] - data[num + 40];
			int num28 = data[num + 24] + data[num + 32];
			int num29 = data[num + 24] - data[num + 32];
			int num30 = num22 + num28;
			int num31 = num22 - num28;
			int num32 = num24 + num26;
			int num33 = num24 - num26;
			data[num] = num30 + num32;
			data[num + 32] = num30 - num32;
			int num34 = FAST_INTEGER_MULTIPLY(num33 + num31, 181);
			data[num + 16] = num31 + num34;
			data[num + 48] = num31 - num34;
			num30 = num29 + num27;
			num32 = num27 + num25;
			num33 = num25 + num23;
			int num35 = FAST_INTEGER_MULTIPLY(num30 - num33, 98);
			int num36 = FAST_INTEGER_MULTIPLY(num30, 139) + num35;
			int num37 = FAST_INTEGER_MULTIPLY(num33, 334) + num35;
			int num38 = FAST_INTEGER_MULTIPLY(num32, 181);
			int num39 = num23 + num38;
			int num40 = num23 - num38;
			data[num + 40] = num40 + num36;
			data[num + 24] = num40 - num36;
			data[num + 8] = num39 + num37;
			data[num + 56] = num39 - num37;
			num++;
		}
	}

	private static void jpeg_fdct_islow(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		int num = 0;
		for (int i = 0; i < 8; i++)
		{
			byte[] array = sample_data[start_row + i];
			int num2 = array[start_col] + array[start_col + 7];
			int num3 = array[start_col + 1] + array[start_col + 6];
			int num4 = array[start_col + 2] + array[start_col + 5];
			int num5 = array[start_col + 3] + array[start_col + 4];
			int num6 = num2 + num5;
			int num7 = num2 - num5;
			int num8 = num3 + num4;
			int num9 = num3 - num4;
			num2 = array[start_col] - array[start_col + 7];
			num3 = array[start_col + 1] - array[start_col + 6];
			num4 = array[start_col + 2] - array[start_col + 5];
			num5 = array[start_col + 3] - array[start_col + 4];
			data[num] = num6 + num8 - 1024 << 2;
			data[num + 4] = num6 - num8 << 2;
			int num10 = (num7 + num9) * 4433;
			num10 += 1024;
			data[num + 2] = JpegUtils.RIGHT_SHIFT(num10 + num7 * 6270, 11);
			data[num + 6] = JpegUtils.DESCALE(num10 - num9 * 15137, 11);
			num7 = num2 + num4;
			num9 = num3 + num5;
			num10 = (num7 + num9) * 9633;
			num10 += 1024;
			num7 *= -3196;
			num9 *= -16069;
			num7 += num10;
			num9 += num10;
			num10 = (num2 + num5) * -7373;
			num2 *= 12299;
			num5 *= 2446;
			num2 += num10 + num7;
			num5 += num10 + num9;
			num10 = (num3 + num4) * -20995;
			num3 *= 25172;
			num4 *= 16819;
			num3 += num10 + num9;
			num4 += num10 + num7;
			data[num + 1] = JpegUtils.RIGHT_SHIFT(num2, 11);
			data[num + 3] = JpegUtils.RIGHT_SHIFT(num3, 11);
			data[num + 5] = JpegUtils.RIGHT_SHIFT(num4, 11);
			data[num + 7] = JpegUtils.RIGHT_SHIFT(num5, 11);
			num += 8;
		}
		num = 0;
		for (int num11 = 7; num11 >= 0; num11--)
		{
			int num12 = data[num] + data[num + 56];
			int num13 = data[num + 8] + data[num + 48];
			int num14 = data[num + 16] + data[num + 40];
			int num15 = data[num + 24] + data[num + 32];
			int num16 = num12 + num15 + 2;
			int num17 = num12 - num15;
			int num18 = num13 + num14;
			int num19 = num13 - num14;
			num12 = data[num] - data[num + 56];
			num13 = data[num + 8] - data[num + 48];
			num14 = data[num + 16] - data[num + 40];
			num15 = data[num + 24] - data[num + 32];
			data[num] = JpegUtils.RIGHT_SHIFT(num16 + num18, 2);
			data[num + 32] = JpegUtils.RIGHT_SHIFT(num16 - num18, 2);
			int num20 = (num17 + num19) * 4433;
			num20 += 16384;
			data[num + 16] = JpegUtils.RIGHT_SHIFT(num20 + num17 * 6270, 15);
			data[num + 48] = JpegUtils.RIGHT_SHIFT(num20 - num19 * 15137, 15);
			num17 = num12 + num14;
			num19 = num13 + num15;
			num20 = (num17 + num19) * 9633;
			num20 += 16384;
			num17 *= -3196;
			num19 *= -16069;
			num17 += num20;
			num19 += num20;
			num20 = (num12 + num15) * -7373;
			num12 *= 12299;
			num15 *= 2446;
			num12 += num20 + num17;
			num15 += num20 + num19;
			num20 = (num13 + num14) * -20995;
			num13 *= 25172;
			num14 *= 16819;
			num13 += num20 + num19;
			num14 += num20 + num17;
			data[num + 8] = JpegUtils.RIGHT_SHIFT(num12, 15);
			data[num + 24] = JpegUtils.RIGHT_SHIFT(num13, 15);
			data[num + 40] = JpegUtils.RIGHT_SHIFT(num14, 15);
			data[num + 56] = JpegUtils.RIGHT_SHIFT(num15, 15);
			num++;
		}
	}

	private static int FAST_INTEGER_MULTIPLY(int var, int c)
	{
		return JpegUtils.RIGHT_SHIFT(var * c, 8);
	}

	private static int SLOW_INTEGER_FIX(double x)
	{
		return (int)(x * 8192.0 + 0.5);
	}

	private void jpeg_fdct_1x1(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_2x2(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_3x3(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_4x4(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_5x5(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_6x6(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_7x7(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_9x9(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_10x10(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_11x11(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_12x12(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_13x13(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_14x14(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_15x15(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_16x16(int[] data1, byte[][] sample_data, int start_row, int start_col)
	{
		int[] array = new int[64];
		int num = 0;
		int[] array2 = data1;
		int num2 = 0;
		while (true)
		{
			byte[] array3 = sample_data[start_row + num2];
			int num3 = array3[start_col] + array3[start_col + 15];
			int num4 = array3[start_col + 1] + array3[start_col + 14];
			int num5 = array3[start_col + 2] + array3[start_col + 13];
			int num6 = array3[start_col + 3] + array3[start_col + 12];
			int num7 = array3[start_col + 4] + array3[start_col + 11];
			int num8 = array3[start_col + 5] + array3[start_col + 10];
			int num9 = array3[start_col + 6] + array3[start_col + 9];
			int num10 = array3[start_col + 7] + array3[start_col + 8];
			int num11 = num3 + num10;
			int num12 = num3 - num10;
			int num13 = num4 + num9;
			int num14 = num4 - num9;
			int num15 = num5 + num8;
			int num16 = num5 - num8;
			int num17 = num6 + num7;
			int num18 = num6 - num7;
			num3 = array3[start_col] - array3[start_col + 15];
			num4 = array3[start_col + 1] - array3[start_col + 14];
			num5 = array3[start_col + 2] - array3[start_col + 13];
			num6 = array3[start_col + 3] - array3[start_col + 12];
			num7 = array3[start_col + 4] - array3[start_col + 11];
			num8 = array3[start_col + 5] - array3[start_col + 10];
			num9 = array3[start_col + 6] - array3[start_col + 9];
			num10 = array3[start_col + 7] - array3[start_col + 8];
			array2[num] = num11 + num13 + num15 + num17 - 2048 << 2;
			array2[num + 4] = JpegUtils.DESCALE((num11 - num17) * SLOW_INTEGER_FIX(1.306562965) + (num13 - num15) * 4433, 11);
			num11 = (num18 - num14) * SLOW_INTEGER_FIX(0.275899379) + (num12 - num16) * SLOW_INTEGER_FIX(1.387039845);
			array2[num + 2] = JpegUtils.DESCALE(num11 + num14 * SLOW_INTEGER_FIX(1.451774982) + num16 * SLOW_INTEGER_FIX(2.172734804), 11);
			array2[num + 6] = JpegUtils.DESCALE(num11 - num12 * SLOW_INTEGER_FIX(0.211164243) - num18 * SLOW_INTEGER_FIX(1.061594338), 11);
			num13 = (num3 + num4) * SLOW_INTEGER_FIX(1.353318001) + (num9 - num10) * SLOW_INTEGER_FIX(0.410524528);
			num15 = (num3 + num5) * SLOW_INTEGER_FIX(1.247225013) + (num8 + num10) * SLOW_INTEGER_FIX(0.666655658);
			num17 = (num3 + num6) * SLOW_INTEGER_FIX(1.093201867) + (num7 - num10) * SLOW_INTEGER_FIX(0.897167586);
			num12 = (num4 + num5) * SLOW_INTEGER_FIX(0.138617169) + (num9 - num8) * SLOW_INTEGER_FIX(1.407403738);
			num14 = (num4 + num6) * -SLOW_INTEGER_FIX(0.666655658) + (num7 + num9) * -SLOW_INTEGER_FIX(1.247225013);
			num16 = (num5 + num6) * -SLOW_INTEGER_FIX(1.353318001) + (num8 - num7) * SLOW_INTEGER_FIX(0.410524528);
			num11 = num13 + num15 + num17 - num3 * SLOW_INTEGER_FIX(2.286341144) + num10 * SLOW_INTEGER_FIX(0.779653625);
			num13 += num12 + num14 + num4 * SLOW_INTEGER_FIX(0.071888074) - num9 * SLOW_INTEGER_FIX(1.663905119);
			num15 += num12 + num16 - num5 * SLOW_INTEGER_FIX(1.125726048) + num8 * SLOW_INTEGER_FIX(1.227391138);
			num17 += num14 + num16 + num6 * SLOW_INTEGER_FIX(1.065388962) + num7 * SLOW_INTEGER_FIX(2.167985692);
			array2[num + 1] = JpegUtils.DESCALE(num11, 11);
			array2[num + 3] = JpegUtils.DESCALE(num13, 11);
			array2[num + 5] = JpegUtils.DESCALE(num15, 11);
			array2[num + 7] = JpegUtils.DESCALE(num17, 11);
			num2++;
			switch (num2)
			{
			default:
				num += 8;
				break;
			case 8:
				array2 = array;
				num = 0;
				break;
			case 16:
			{
				array2 = data1;
				num = 0;
				int num19 = 0;
				for (num2 = 7; num2 >= 0; num2--)
				{
					int num20 = array2[num] + array[num19 + 56];
					int num21 = array2[num + 8] + array[num19 + 48];
					int num22 = array2[num + 16] + array[num19 + 40];
					int num23 = array2[num + 24] + array[num19 + 32];
					int num24 = array2[num + 32] + array[num19 + 24];
					int num25 = array2[num + 40] + array[num19 + 16];
					int num26 = array2[num + 48] + array[num19 + 8];
					int num27 = array2[num + 56] + array[num19];
					int num28 = num20 + num27;
					int num29 = num20 - num27;
					int num30 = num21 + num26;
					int num31 = num21 - num26;
					int num32 = num22 + num25;
					int num33 = num22 - num25;
					int num34 = num23 + num24;
					int num35 = num23 - num24;
					num20 = array2[num] - array[num19 + 56];
					num21 = array2[num + 8] - array[num19 + 48];
					num22 = array2[num + 16] - array[num19 + 40];
					num23 = array2[num + 24] - array[num19 + 32];
					num24 = array2[num + 32] - array[num19 + 24];
					num25 = array2[num + 40] - array[num19 + 16];
					num26 = array2[num + 48] - array[num19 + 8];
					num27 = array2[num + 56] - array[num19];
					array2[num] = JpegUtils.DESCALE(num28 + num30 + num32 + num34, 4);
					array2[num + 32] = JpegUtils.DESCALE((num28 - num34) * SLOW_INTEGER_FIX(1.306562965) + (num30 - num32) * 4433, 17);
					num28 = (num35 - num31) * SLOW_INTEGER_FIX(0.275899379) + (num29 - num33) * SLOW_INTEGER_FIX(1.387039845);
					array2[num + 16] = JpegUtils.DESCALE(num28 + num31 * SLOW_INTEGER_FIX(1.451774982) + num33 * SLOW_INTEGER_FIX(2.172734804), 17);
					array2[num + 48] = JpegUtils.DESCALE(num28 - num29 * SLOW_INTEGER_FIX(0.211164243) - num35 * SLOW_INTEGER_FIX(1.061594338), 17);
					num30 = (num20 + num21) * SLOW_INTEGER_FIX(1.353318001) + (num26 - num27) * SLOW_INTEGER_FIX(0.410524528);
					num32 = (num20 + num22) * SLOW_INTEGER_FIX(1.247225013) + (num25 + num27) * SLOW_INTEGER_FIX(0.666655658);
					num34 = (num20 + num23) * SLOW_INTEGER_FIX(1.093201867) + (num24 - num27) * SLOW_INTEGER_FIX(0.897167586);
					num29 = (num21 + num22) * SLOW_INTEGER_FIX(0.138617169) + (num26 - num25) * SLOW_INTEGER_FIX(1.407403738);
					num31 = (num21 + num23) * -SLOW_INTEGER_FIX(0.666655658) + (num24 + num26) * -SLOW_INTEGER_FIX(1.247225013);
					num33 = (num22 + num23) * -SLOW_INTEGER_FIX(1.353318001) + (num25 - num24) * SLOW_INTEGER_FIX(0.410524528);
					num28 = num30 + num32 + num34 - num20 * SLOW_INTEGER_FIX(2.286341144) + num27 * SLOW_INTEGER_FIX(0.779653625);
					num30 += num29 + num31 + num21 * SLOW_INTEGER_FIX(0.071888074) - num26 * SLOW_INTEGER_FIX(1.663905119);
					num32 += num29 + num33 - num22 * SLOW_INTEGER_FIX(1.125726048) + num25 * SLOW_INTEGER_FIX(1.227391138);
					num34 += num31 + num33 + num23 * SLOW_INTEGER_FIX(1.065388962) + num24 * SLOW_INTEGER_FIX(2.167985692);
					array2[num + 8] = JpegUtils.DESCALE(num28, 17);
					array2[num + 24] = JpegUtils.DESCALE(num30, 17);
					array2[num + 40] = JpegUtils.DESCALE(num32, 17);
					array2[num + 56] = JpegUtils.DESCALE(num34, 17);
					num++;
					num19++;
				}
				return;
			}
			}
		}
	}

	private void jpeg_fdct_16x8(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_14x7(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_12x6(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_10x5(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_8x4(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_6x3(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_4x2(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_2x1(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_8x16(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_7x14(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_6x12(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_5x10(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_4x8(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_3x6(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_2x4(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_fdct_1x2(int[] data, byte[][] sample_data, int start_row, int start_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}
}

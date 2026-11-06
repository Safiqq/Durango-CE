namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_inverse_dct
{
	private delegate void inverse_method(int component_index, short[] coef_block, int output_row, int output_col);

	private class multiplier_table
	{
		public int[] int_array = new int[64];

		public float[] float_array = new float[64];
	}

	private const int IFAST_SCALE_BITS = 2;

	private const int RANGE_MASK = 1023;

	private const int RANGE_CENTER = 512;

	private const int RANGE_SUBSET = 384;

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

	private const int FAST_INTEGER_CONST_BITS = 8;

	private const int FAST_INTEGER_PASS1_BITS = 2;

	private const int FAST_INTEGER_FIX_1_082392200 = 277;

	private const int FAST_INTEGER_FIX_1_414213562 = 362;

	private const int FAST_INTEGER_FIX_1_847759065 = 473;

	private const int FAST_INTEGER_FIX_2_613125930 = 669;

	private const int REDUCED_CONST_BITS = 13;

	private const int REDUCED_PASS1_BITS = 2;

	private const int REDUCED_FIX_0_211164243 = 1730;

	private const int REDUCED_FIX_0_509795579 = 4176;

	private const int REDUCED_FIX_0_601344887 = 4926;

	private const int REDUCED_FIX_0_720959822 = 5906;

	private const int REDUCED_FIX_0_765366865 = 6270;

	private const int REDUCED_FIX_0_850430095 = 6967;

	private const int REDUCED_FIX_0_899976223 = 7373;

	private const int REDUCED_FIX_1_061594337 = 8697;

	private const int REDUCED_FIX_1_272758580 = 10426;

	private const int REDUCED_FIX_1_451774981 = 11893;

	private const int REDUCED_FIX_1_847759065 = 15137;

	private const int REDUCED_FIX_2_172734803 = 17799;

	private const int REDUCED_FIX_2_562915447 = 20995;

	private const int REDUCED_FIX_3_624509785 = 29692;

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

	private const int CONST_BITS = 14;

	private static readonly double[] aanscalefactor = new double[8] { 1.0, 1.387039845, 1.306562965, 1.175875602, 1.0, 0.785694958, 0.5411961, 0.275899379 };

	private inverse_method[] m_inverse_DCT_method = new inverse_method[10];

	private multiplier_table[] m_dctTables;

	private jpeg_decompress_struct m_cinfo;

	private int[] m_cur_method = new int[10];

	private ComponentBuffer m_componentBuffer;

	public jpeg_inverse_dct(jpeg_decompress_struct cinfo)
	{
		m_cinfo = cinfo;
		m_dctTables = new multiplier_table[cinfo.m_num_components];
		for (int i = 0; i < cinfo.m_num_components; i++)
		{
			m_dctTables[i] = new multiplier_table();
			m_cur_method[i] = -1;
		}
	}

	public void start_pass()
	{
		for (int i = 0; i < m_cinfo.m_num_components; i++)
		{
			jpeg_component_info jpeg_component_info = m_cinfo.Comp_info[i];
			inverse_method inverse_method = null;
			int num = 0;
			switch ((jpeg_component_info.DCT_h_scaled_size << 8) + jpeg_component_info.DCT_v_scaled_size)
			{
			case 257:
				inverse_method = jpeg_idct_1x1;
				num = 0;
				break;
			case 514:
				inverse_method = jpeg_idct_2x2;
				num = 0;
				break;
			case 771:
				inverse_method = jpeg_idct_3x3;
				num = 0;
				break;
			case 1028:
				inverse_method = jpeg_idct_4x4;
				num = 0;
				break;
			case 1285:
				inverse_method = jpeg_idct_5x5;
				num = 0;
				break;
			case 1542:
				inverse_method = jpeg_idct_6x6;
				num = 0;
				break;
			case 1799:
				inverse_method = jpeg_idct_7x7;
				num = 0;
				break;
			case 2313:
				inverse_method = jpeg_idct_9x9;
				num = 0;
				break;
			case 2570:
				inverse_method = jpeg_idct_10x10;
				num = 0;
				break;
			case 2827:
				inverse_method = jpeg_idct_11x11;
				num = 0;
				break;
			case 3084:
				inverse_method = jpeg_idct_12x12;
				num = 0;
				break;
			case 3341:
				inverse_method = jpeg_idct_13x13;
				num = 0;
				break;
			case 3598:
				inverse_method = jpeg_idct_14x14;
				num = 0;
				break;
			case 3855:
				inverse_method = jpeg_idct_15x15;
				num = 0;
				break;
			case 4112:
				inverse_method = jpeg_idct_16x16;
				num = 0;
				break;
			case 4104:
				inverse_method = jpeg_idct_16x8;
				num = 0;
				break;
			case 3591:
				inverse_method = jpeg_idct_14x7;
				num = 0;
				break;
			case 3078:
				inverse_method = jpeg_idct_12x6;
				num = 0;
				break;
			case 2565:
				inverse_method = jpeg_idct_10x5;
				num = 0;
				break;
			case 2052:
				inverse_method = jpeg_idct_8x4;
				num = 0;
				break;
			case 1539:
				inverse_method = jpeg_idct_6x3;
				num = 0;
				break;
			case 1026:
				inverse_method = jpeg_idct_4x2;
				num = 0;
				break;
			case 513:
				inverse_method = jpeg_idct_2x1;
				num = 0;
				break;
			case 2064:
				inverse_method = jpeg_idct_8x16;
				num = 0;
				break;
			case 1806:
				inverse_method = jpeg_idct_7x14;
				num = 0;
				break;
			case 1548:
				inverse_method = jpeg_idct_6x12;
				num = 0;
				break;
			case 1290:
				inverse_method = jpeg_idct_5x10;
				num = 0;
				break;
			case 1032:
				inverse_method = jpeg_idct_4x8;
				num = 0;
				break;
			case 774:
				inverse_method = jpeg_idct_3x6;
				num = 0;
				break;
			case 516:
				inverse_method = jpeg_idct_2x4;
				num = 0;
				break;
			case 258:
				inverse_method = jpeg_idct_1x2;
				num = 0;
				break;
			case 2056:
				switch (m_cinfo.m_dct_method)
				{
				case J_DCT_METHOD.JDCT_ISLOW:
					inverse_method = jpeg_idct_islow;
					num = 0;
					break;
				case J_DCT_METHOD.JDCT_IFAST:
					inverse_method = jpeg_idct_ifast;
					num = 1;
					break;
				case J_DCT_METHOD.JDCT_FLOAT:
					inverse_method = jpeg_idct_float;
					num = 2;
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
			m_inverse_DCT_method[i] = inverse_method;
			if (!jpeg_component_info.component_needed || m_cur_method[i] == num || jpeg_component_info.quant_table == null)
			{
				continue;
			}
			m_cur_method[i] = num;
			switch ((J_DCT_METHOD)num)
			{
			case J_DCT_METHOD.JDCT_ISLOW:
			{
				int[] int_array = m_dctTables[i].int_array;
				for (int l = 0; l < 64; l++)
				{
					int_array[l] = jpeg_component_info.quant_table.quantval[l];
				}
				break;
			}
			case J_DCT_METHOD.JDCT_IFAST:
			{
				int[] int_array2 = m_dctTables[i].int_array;
				for (int m = 0; m < 64; m++)
				{
					int_array2[m] = JpegUtils.DESCALE(jpeg_component_info.quant_table.quantval[m] * aanscales[m], 12);
				}
				break;
			}
			case J_DCT_METHOD.JDCT_FLOAT:
			{
				float[] float_array = m_dctTables[i].float_array;
				int num2 = 0;
				for (int j = 0; j < 8; j++)
				{
					for (int k = 0; k < 8; k++)
					{
						float_array[num2] = (float)((double)jpeg_component_info.quant_table.quantval[num2] * aanscalefactor[j] * aanscalefactor[k] * 0.125);
						num2++;
					}
				}
				break;
			}
			default:
				m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
				break;
			}
		}
	}

	public void inverse(int component_index, short[] coef_block, ComponentBuffer output_buf, int output_row, int output_col)
	{
		m_componentBuffer = output_buf;
		inverse_method inverse_method = m_inverse_DCT_method[component_index];
		if (inverse_method == null)
		{
			m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOT_COMPILED);
		}
		else
		{
			inverse_method(component_index, coef_block, output_row, output_col);
		}
	}

	private void jpeg_idct_islow(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[64];
		int num = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num2 = 0;
		int num3 = 0;
		for (int num4 = 8; num4 > 0; num4--)
		{
			if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
			{
				array[num3 + 56] = (array[num3 + 48] = (array[num3 + 40] = (array[num3 + 32] = (array[num3 + 24] = (array[num3 + 16] = (array[num3 + 8] = (array[num3] = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]) << 2)))))));
				num++;
				num2++;
				num3++;
			}
			else
			{
				int num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]);
				int num6 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num2 + 32]);
				num5 <<= 13;
				num6 <<= 13;
				num5 += 1024;
				int num7 = num5 + num6;
				int num8 = num5 - num6;
				num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num2 + 16]);
				num6 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num2 + 48]);
				int num9 = (num5 + num6) * 4433;
				int num10 = num9 + num5 * 6270;
				int num11 = num9 - num6 * 15137;
				int num12 = num7 + num10;
				int num13 = num7 - num10;
				int num14 = num8 + num11;
				int num15 = num8 - num11;
				num7 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num2 + 56]);
				num8 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num2 + 40]);
				num10 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num2 + 24]);
				num11 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num2 + 8]);
				num5 = num7 + num10;
				num6 = num8 + num11;
				num9 = (num5 + num6) * 9633;
				num5 *= -16069;
				num6 *= -3196;
				num5 += num9;
				num6 += num9;
				num9 = (num7 + num11) * -7373;
				num7 *= 2446;
				num11 *= 12299;
				num7 += num9 + num5;
				num11 += num9 + num6;
				num9 = (num8 + num10) * -20995;
				num8 *= 16819;
				num10 *= 25172;
				num8 += num9 + num6;
				num10 += num9 + num5;
				array[num3] = JpegUtils.RIGHT_SHIFT(num12 + num11, 11);
				array[num3 + 56] = JpegUtils.RIGHT_SHIFT(num12 - num11, 11);
				array[num3 + 8] = JpegUtils.RIGHT_SHIFT(num14 + num10, 11);
				array[num3 + 48] = JpegUtils.RIGHT_SHIFT(num14 - num10, 11);
				array[num3 + 16] = JpegUtils.RIGHT_SHIFT(num15 + num8, 11);
				array[num3 + 40] = JpegUtils.RIGHT_SHIFT(num15 - num8, 11);
				array[num3 + 24] = JpegUtils.RIGHT_SHIFT(num13 + num7, 11);
				array[num3 + 32] = JpegUtils.RIGHT_SHIFT(num13 - num7, 11);
				num++;
				num2++;
				num3++;
			}
		}
		num3 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num16 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int i = 0; i < 8; i++)
		{
			int num17 = array[num3] + 16384 + 16;
			int i2 = output_row + i;
			if (array[num3 + 1] == 0 && array[num3 + 2] == 0 && array[num3 + 3] == 0 && array[num3 + 4] == 0 && array[num3 + 5] == 0 && array[num3 + 6] == 0 && array[num3 + 7] == 0)
			{
				byte b = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num17, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col] = b;
				m_componentBuffer[i2][output_col + 1] = b;
				m_componentBuffer[i2][output_col + 2] = b;
				m_componentBuffer[i2][output_col + 3] = b;
				m_componentBuffer[i2][output_col + 4] = b;
				m_componentBuffer[i2][output_col + 5] = b;
				m_componentBuffer[i2][output_col + 6] = b;
				m_componentBuffer[i2][output_col + 7] = b;
				num3 += 8;
				continue;
			}
			int num18 = array[num3 + 4];
			int num19 = num17 + num18 << 13;
			int num20 = num17 - num18 << 13;
			num17 = array[num3 + 2];
			num18 = array[num3 + 6];
			int num21 = (num17 + num18) * 4433;
			int num22 = num21 + num17 * 6270;
			int num23 = num21 - num18 * 15137;
			int num24 = num19 + num22;
			int num25 = num19 - num22;
			int num26 = num20 + num23;
			int num27 = num20 - num23;
			num19 = array[num3 + 7];
			num20 = array[num3 + 5];
			num22 = array[num3 + 3];
			num23 = array[num3 + 1];
			num17 = num19 + num22;
			num18 = num20 + num23;
			num21 = (num17 + num18) * 9633;
			num17 *= -16069;
			num18 *= -3196;
			num17 += num21;
			num18 += num21;
			num21 = (num19 + num23) * -7373;
			num19 *= 2446;
			num23 *= 12299;
			num19 += num21 + num17;
			num23 += num21 + num18;
			num21 = (num20 + num22) * -20995;
			num20 *= 16819;
			num22 *= 25172;
			num20 += num21 + num18;
			num22 += num21 + num17;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num24 + num23, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num24 - num23, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num26 + num22, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num26 - num22, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num27 + num20, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num27 - num20, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num25 + num19, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num25 - num19, 18)) & 0x3FF];
			num3 += 8;
		}
	}

	private static int SLOW_INTEGER_DEQUANTIZE(int coef, int quantval)
	{
		return coef * quantval;
	}

	private static int SLOW_INTEGER_FIX(double x)
	{
		return (int)(x * 8192.0 + 0.5);
	}

	private void jpeg_idct_ifast(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[64];
		int num = 0;
		int num2 = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num3 = 0;
		for (int num4 = 8; num4 > 0; num4--)
		{
			if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
			{
				array[num2 + 56] = (array[num2 + 48] = (array[num2 + 40] = (array[num2 + 32] = (array[num2 + 24] = (array[num2 + 16] = (array[num2 + 8] = (array[num2] = FAST_INTEGER_DEQUANTIZE(coef_block[num], int_array[num3]))))))));
				num++;
				num3++;
				num2++;
			}
			else
			{
				int num5 = FAST_INTEGER_DEQUANTIZE(coef_block[num], int_array[num3]);
				int num6 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num3 + 16]);
				int num7 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num3 + 32]);
				int num8 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num3 + 48]);
				int num9 = num5 + num7;
				int num10 = num5 - num7;
				int num11 = num6 + num8;
				int num12 = FAST_INTEGER_MULTIPLY(num6 - num8, 362) - num11;
				num5 = num9 + num11;
				num8 = num9 - num11;
				num6 = num10 + num12;
				num7 = num10 - num12;
				int num13 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num3 + 8]);
				int num14 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num3 + 24]);
				int num15 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num3 + 40]);
				int num16 = FAST_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num3 + 56]);
				int num17 = num15 + num14;
				int num18 = num15 - num14;
				int num19 = num13 + num16;
				int num20 = num13 - num16;
				num16 = num19 + num17;
				num10 = FAST_INTEGER_MULTIPLY(num19 - num17, 362);
				int num21 = FAST_INTEGER_MULTIPLY(num18 + num20, 473);
				num9 = num21 - FAST_INTEGER_MULTIPLY(num20, 277);
				num12 = num21 - FAST_INTEGER_MULTIPLY(num18, 669);
				num15 = num12 - num16;
				num14 = num10 - num15;
				num13 = num9 - num14;
				array[num2] = num5 + num16;
				array[num2 + 56] = num5 - num16;
				array[num2 + 8] = num6 + num15;
				array[num2 + 48] = num6 - num15;
				array[num2 + 16] = num7 + num14;
				array[num2 + 40] = num7 - num14;
				array[num2 + 24] = num8 + num13;
				array[num2 + 32] = num8 - num13;
				num++;
				num3++;
				num2++;
			}
		}
		num2 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num22 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int i = 0; i < 8; i++)
		{
			int i2 = output_row + i;
			int num23 = array[num2] + 16400;
			if (array[num2 + 1] == 0 && array[num2 + 2] == 0 && array[num2 + 3] == 0 && array[num2 + 4] == 0 && array[num2 + 5] == 0 && array[num2 + 6] == 0 && array[num2 + 7] == 0)
			{
				byte b = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num23, 5)) & 0x3FF];
				m_componentBuffer[i2][output_col] = b;
				m_componentBuffer[i2][output_col + 1] = b;
				m_componentBuffer[i2][output_col + 2] = b;
				m_componentBuffer[i2][output_col + 3] = b;
				m_componentBuffer[i2][output_col + 4] = b;
				m_componentBuffer[i2][output_col + 5] = b;
				m_componentBuffer[i2][output_col + 6] = b;
				m_componentBuffer[i2][output_col + 7] = b;
				num2 += 8;
				continue;
			}
			int num24 = num23 + array[num2 + 4];
			int num25 = num23 - array[num2 + 4];
			int num26 = array[num2 + 2] + array[num2 + 6];
			int num27 = FAST_INTEGER_MULTIPLY(array[num2 + 2] - array[num2 + 6], 362) - num26;
			int num28 = num24 + num26;
			int num29 = num24 - num26;
			int num30 = num25 + num27;
			int num31 = num25 - num27;
			int num32 = array[num2 + 5] + array[num2 + 3];
			int num33 = array[num2 + 5] - array[num2 + 3];
			int num34 = array[num2 + 1] + array[num2 + 7];
			int num35 = array[num2 + 1] - array[num2 + 7];
			int num36 = num34 + num32;
			num25 = FAST_INTEGER_MULTIPLY(num34 - num32, 362);
			num23 = FAST_INTEGER_MULTIPLY(num33 + num35, 473);
			num24 = num23 - FAST_INTEGER_MULTIPLY(num35, 277);
			num27 = num23 - FAST_INTEGER_MULTIPLY(num33, 669);
			int num37 = num27 - num36;
			int num38 = num25 - num37;
			int num39 = num24 - num38;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num28 + num36, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num28 - num36, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num30 + num37, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num30 - num37, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num31 + num38, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num31 - num38, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num29 + num39, 5)) & 0x3FF];
			m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num22 + FAST_INTEGER_IRIGHT_SHIFT(num29 - num39, 5)) & 0x3FF];
			num2 += 8;
		}
	}

	private static int FAST_INTEGER_MULTIPLY(int var, int c)
	{
		return JpegUtils.RIGHT_SHIFT(var * c, 8);
	}

	private static int FAST_INTEGER_DEQUANTIZE(short coef, int quantval)
	{
		return coef * quantval;
	}

	private static int FAST_INTEGER_IRIGHT_SHIFT(int x, int shft)
	{
		return x >> shft;
	}

	private static int FAST_INTEGER_IDESCALE(int x, int n)
	{
		return FAST_INTEGER_IRIGHT_SHIFT(x, n);
	}

	private void jpeg_idct_float(int component_index, short[] coef_block, int output_row, int output_col)
	{
		float[] array = new float[64];
		int num = 0;
		int num2 = 0;
		float[] float_array = m_dctTables[component_index].float_array;
		int num3 = 0;
		for (int num4 = 8; num4 > 0; num4--)
		{
			if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
			{
				array[num2 + 56] = (array[num2 + 48] = (array[num2 + 40] = (array[num2 + 32] = (array[num2 + 24] = (array[num2 + 16] = (array[num2 + 8] = (array[num2] = FLOAT_DEQUANTIZE(coef_block[num], float_array[num3]))))))));
				num++;
				num3++;
				num2++;
			}
			else
			{
				float num5 = FLOAT_DEQUANTIZE(coef_block[num], float_array[num3]);
				float num6 = FLOAT_DEQUANTIZE(coef_block[num + 16], float_array[num3 + 16]);
				float num7 = FLOAT_DEQUANTIZE(coef_block[num + 32], float_array[num3 + 32]);
				float num8 = FLOAT_DEQUANTIZE(coef_block[num + 48], float_array[num3 + 48]);
				float num9 = num5 + num7;
				float num10 = num5 - num7;
				float num11 = num6 + num8;
				float num12 = (num6 - num8) * 1.4142135f - num11;
				num5 = num9 + num11;
				num8 = num9 - num11;
				num6 = num10 + num12;
				num7 = num10 - num12;
				float num13 = FLOAT_DEQUANTIZE(coef_block[num + 8], float_array[num3 + 8]);
				float num14 = FLOAT_DEQUANTIZE(coef_block[num + 24], float_array[num3 + 24]);
				float num15 = FLOAT_DEQUANTIZE(coef_block[num + 40], float_array[num3 + 40]);
				float num16 = FLOAT_DEQUANTIZE(coef_block[num + 56], float_array[num3 + 56]);
				float num17 = num15 + num14;
				float num18 = num15 - num14;
				float num19 = num13 + num16;
				float num20 = num13 - num16;
				num16 = num19 + num17;
				num10 = (num19 - num17) * 1.4142135f;
				float num21 = (num18 + num20) * 1.847759f;
				num9 = num21 - num20 * 1.0823922f;
				num12 = num21 - num18 * 2.613126f;
				num15 = num12 - num16;
				num14 = num10 - num15;
				num13 = num9 - num14;
				array[num2] = num5 + num16;
				array[num2 + 56] = num5 - num16;
				array[num2 + 8] = num6 + num15;
				array[num2 + 48] = num6 - num15;
				array[num2 + 16] = num7 + num14;
				array[num2 + 40] = num7 - num14;
				array[num2 + 24] = num8 + num13;
				array[num2 + 32] = num8 - num13;
				num++;
				num3++;
				num2++;
			}
		}
		num2 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num22 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int i = 0; i < 8; i++)
		{
			float num23 = array[num2] + 512.5f;
			float num24 = num23 + array[num2 + 4];
			float num25 = num23 - array[num2 + 4];
			float num26 = array[num2 + 2] + array[num2 + 6];
			float num27 = (array[num2 + 2] - array[num2 + 6]) * 1.4142135f - num26;
			float num28 = num24 + num26;
			float num29 = num24 - num26;
			float num30 = num25 + num27;
			float num31 = num25 - num27;
			float num32 = array[num2 + 5] + array[num2 + 3];
			float num33 = array[num2 + 5] - array[num2 + 3];
			float num34 = array[num2 + 1] + array[num2 + 7];
			float num35 = array[num2 + 1] - array[num2 + 7];
			float num36 = num34 + num32;
			num25 = (num34 - num32) * 1.4142135f;
			num23 = (num33 + num35) * 1.847759f;
			num24 = num23 - num35 * 1.0823922f;
			num27 = num23 - num33 * 2.613126f;
			float num37 = num27 - num36;
			float num38 = num25 - num37;
			float num39 = num24 - num38;
			int i2 = output_row + i;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num22 + (int)(num28 + num36)) & 0x3FF];
			m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num22 + (int)(num28 - num36)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num22 + (int)(num30 + num37)) & 0x3FF];
			m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num22 + (int)(num30 - num37)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num22 + (int)(num31 + num38)) & 0x3FF];
			m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num22 + (int)(num31 - num38)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num22 + (int)(num29 + num39)) & 0x3FF];
			m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num22 + (int)(num29 - num39)) & 0x3FF];
			num2 += 8;
		}
	}

	private static float FLOAT_DEQUANTIZE(short coef, float quantval)
	{
		return (float)coef * quantval;
	}

	private void jpeg_idct_4x4(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[32];
		int num = 0;
		int num2 = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num3 = 0;
		for (int num4 = 8; num4 > 0; num4--)
		{
			if (num4 != 4)
			{
				if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
				{
					array[num2 + 24] = (array[num2 + 16] = (array[num2 + 8] = (array[num2] = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]) << 2)));
				}
				else
				{
					int num5 = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]);
					num5 <<= 14;
					int num6 = REDUCED_DEQUANTIZE(coef_block[num + 16], int_array[num3 + 16]);
					int num7 = REDUCED_DEQUANTIZE(coef_block[num + 48], int_array[num3 + 48]);
					int num8 = num6 * 15137 + num7 * -6270;
					int num9 = num5 + num8;
					int num10 = num5 - num8;
					int num11 = REDUCED_DEQUANTIZE(coef_block[num + 56], int_array[num3 + 56]);
					num6 = REDUCED_DEQUANTIZE(coef_block[num + 40], int_array[num3 + 40]);
					num7 = REDUCED_DEQUANTIZE(coef_block[num + 24], int_array[num3 + 24]);
					int num12 = REDUCED_DEQUANTIZE(coef_block[num + 8], int_array[num3 + 8]);
					num5 = num11 * -1730 + num6 * 11893 + num7 * -17799 + num12 * 8697;
					num8 = num11 * -4176 + num6 * -4926 + num7 * 7373 + num12 * 20995;
					array[num2] = JpegUtils.DESCALE(num9 + num8, 12);
					array[num2 + 24] = JpegUtils.DESCALE(num9 - num8, 12);
					array[num2 + 8] = JpegUtils.DESCALE(num10 + num5, 12);
					array[num2 + 16] = JpegUtils.DESCALE(num10 - num5, 12);
				}
			}
			num++;
			num3++;
			num2++;
		}
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num13 = m_cinfo.m_sampleRangeLimitOffset - 384;
		num2 = 0;
		for (int i = 0; i < 4; i++)
		{
			int i2 = output_row + i;
			if (array[num2 + 1] == 0 && array[num2 + 2] == 0 && array[num2 + 3] == 0 && array[num2 + 5] == 0 && array[num2 + 6] == 0 && array[num2 + 7] == 0)
			{
				byte b = sample_range_limit[(num13 + JpegUtils.DESCALE(array[num2], 5)) & 0x3FF];
				m_componentBuffer[i2][output_col] = b;
				m_componentBuffer[i2][output_col + 1] = b;
				m_componentBuffer[i2][output_col + 2] = b;
				m_componentBuffer[i2][output_col + 3] = b;
				num2 += 8;
				continue;
			}
			int num14 = array[num2] << 14;
			int num15 = array[num2 + 2] * 15137 + array[num2 + 6] * -6270;
			int num16 = num14 + num15;
			int num17 = num14 - num15;
			int num18 = array[num2 + 7];
			int num19 = array[num2 + 5];
			int num20 = array[num2 + 3];
			int num21 = array[num2 + 1];
			num14 = num18 * -1730 + num19 * 11893 + num20 * -17799 + num21 * 8697;
			num15 = num18 * -4176 + num19 * -4926 + num20 * 7373 + num21 * 20995;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num13 + JpegUtils.DESCALE(num16 + num15, 19)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num13 + JpegUtils.DESCALE(num16 - num15, 19)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num13 + JpegUtils.DESCALE(num17 + num14, 19)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num13 + JpegUtils.DESCALE(num17 - num14, 19)) & 0x3FF];
			num2 += 8;
		}
	}

	private void jpeg_idct_2x2(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[16];
		int num = 0;
		int num2 = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num3 = 0;
		for (int num4 = 8; num4 > 0; num4--)
		{
			if (num4 != 6 && num4 != 4 && num4 != 2)
			{
				if (coef_block[num + 8] == 0 && coef_block[num + 24] == 0 && coef_block[num + 40] == 0 && coef_block[num + 56] == 0)
				{
					array[num2 + 8] = (array[num2] = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]) << 2);
				}
				else
				{
					int num5 = REDUCED_DEQUANTIZE(coef_block[num], int_array[num3]);
					int num6 = num5 << 15;
					num5 = REDUCED_DEQUANTIZE(coef_block[num + 56], int_array[num3 + 56]);
					int num7 = num5 * -5906;
					num5 = REDUCED_DEQUANTIZE(coef_block[num + 40], int_array[num3 + 40]);
					num7 += num5 * 6967;
					num5 = REDUCED_DEQUANTIZE(coef_block[num + 24], int_array[num3 + 24]);
					num7 += num5 * -10426;
					num5 = REDUCED_DEQUANTIZE(coef_block[num + 8], int_array[num3 + 8]);
					num7 += num5 * 29692;
					array[num2] = JpegUtils.DESCALE(num6 + num7, 13);
					array[num2 + 8] = JpegUtils.DESCALE(num6 - num7, 13);
				}
			}
			num++;
			num3++;
			num2++;
		}
		num2 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num8 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int i = 0; i < 2; i++)
		{
			int i2 = output_row + i;
			if (array[num2 + 1] == 0 && array[num2 + 3] == 0 && array[num2 + 5] == 0 && array[num2 + 7] == 0)
			{
				byte b = sample_range_limit[(num8 + JpegUtils.DESCALE(array[num2], 5)) & 0x3FF];
				m_componentBuffer[i2][output_col] = b;
				m_componentBuffer[i2][output_col + 1] = b;
				num2 += 8;
			}
			else
			{
				int num9 = array[num2] << 15;
				int num10 = array[num2 + 7] * -5906 + array[num2 + 5] * 6967 + array[num2 + 3] * -10426 + array[num2 + 1] * 29692;
				m_componentBuffer[i2][output_col] = sample_range_limit[(num8 + JpegUtils.DESCALE(num9 + num10, 20)) & 0x3FF];
				m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num8 + JpegUtils.DESCALE(num9 - num10, 20)) & 0x3FF];
				num2 += 8;
			}
		}
	}

	private void jpeg_idct_1x1(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] int_array = m_dctTables[component_index].int_array;
		int x = REDUCED_DEQUANTIZE(coef_block[0], int_array[0]);
		x = JpegUtils.DESCALE(x, 3);
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num = m_cinfo.m_sampleRangeLimitOffset - 384;
		m_componentBuffer[output_row][output_col] = sample_range_limit[(num + x) & 0x3FF];
	}

	private static int REDUCED_DEQUANTIZE(short coef, int quantval)
	{
		return coef * quantval;
	}

	private void jpeg_idct_3x3(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_5x5(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_6x6(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_7x7(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_9x9(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_10x10(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_11x11(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_12x12(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_13x13(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_14x14(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_15x15(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_16x16(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[128];
		int num = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < 8; i++)
		{
			int num4 = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]);
			num4 <<= 13;
			num4 += 1024;
			int num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num2 + 32]);
			int num6 = num5 * SLOW_INTEGER_FIX(1.306562965);
			int num7 = num5 * 4433;
			int num8 = num4 + num6;
			int num9 = num4 - num6;
			int num10 = num4 + num7;
			int num11 = num4 - num7;
			num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num2 + 16]);
			int num12 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num2 + 48]);
			int num13 = num5 - num12;
			int num14 = num13 * SLOW_INTEGER_FIX(0.275899379);
			num13 *= SLOW_INTEGER_FIX(1.387039845);
			num4 = num13 + num12 * 20995;
			num6 = num14 + num5 * 7373;
			num7 = num13 - num5 * SLOW_INTEGER_FIX(0.601344887);
			int num15 = num14 - num12 * SLOW_INTEGER_FIX(0.509795579);
			int num16 = num8 + num4;
			int num17 = num8 - num4;
			int num18 = num10 + num6;
			int num19 = num10 - num6;
			int num20 = num11 + num7;
			int num21 = num11 - num7;
			int num22 = num9 + num15;
			int num23 = num9 - num15;
			num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num2 + 8]);
			num12 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num2 + 24]);
			num13 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num2 + 40]);
			num14 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num2 + 56]);
			num9 = num5 + num13;
			num6 = (num5 + num12) * SLOW_INTEGER_FIX(1.353318001);
			num7 = num9 * SLOW_INTEGER_FIX(1.247225013);
			num15 = (num5 + num14) * SLOW_INTEGER_FIX(1.093201867);
			num8 = (num5 - num14) * SLOW_INTEGER_FIX(0.897167586);
			num9 *= SLOW_INTEGER_FIX(0.666655658);
			num10 = (num5 - num12) * SLOW_INTEGER_FIX(0.410524528);
			num4 = num6 + num7 + num15 - num5 * SLOW_INTEGER_FIX(2.286341144);
			num11 = num8 + num9 + num10 - num5 * SLOW_INTEGER_FIX(1.835730603);
			num5 = (num12 + num13) * SLOW_INTEGER_FIX(0.138617169);
			num6 += num5 + num12 * SLOW_INTEGER_FIX(0.071888074);
			num7 += num5 - num13 * SLOW_INTEGER_FIX(1.125726048);
			num5 = (num13 - num12) * SLOW_INTEGER_FIX(1.407403738);
			num9 += num5 - num13 * SLOW_INTEGER_FIX(0.766367282);
			num10 += num5 + num12 * SLOW_INTEGER_FIX(1.971951411);
			num12 += num14;
			num5 = num12 * -SLOW_INTEGER_FIX(0.666655658);
			num6 += num5;
			num15 += num5 + num14 * SLOW_INTEGER_FIX(1.065388962);
			num12 *= -SLOW_INTEGER_FIX(1.247225013);
			num8 += num12 + num14 * SLOW_INTEGER_FIX(3.141271809);
			num10 += num12;
			num12 = (num13 + num14) * -SLOW_INTEGER_FIX(1.353318001);
			num7 += num12;
			num15 += num12;
			num12 = (num14 - num13) * SLOW_INTEGER_FIX(0.410524528);
			num8 += num12;
			num9 += num12;
			array[num3] = JpegUtils.RIGHT_SHIFT(num16 + num4, 11);
			array[num3 + 120] = JpegUtils.RIGHT_SHIFT(num16 - num4, 11);
			array[num3 + 8] = JpegUtils.RIGHT_SHIFT(num18 + num6, 11);
			array[num3 + 112] = JpegUtils.RIGHT_SHIFT(num18 - num6, 11);
			array[num3 + 16] = JpegUtils.RIGHT_SHIFT(num20 + num7, 11);
			array[num3 + 104] = JpegUtils.RIGHT_SHIFT(num20 - num7, 11);
			array[num3 + 24] = JpegUtils.RIGHT_SHIFT(num22 + num15, 11);
			array[num3 + 96] = JpegUtils.RIGHT_SHIFT(num22 - num15, 11);
			array[num3 + 32] = JpegUtils.RIGHT_SHIFT(num23 + num8, 11);
			array[num3 + 88] = JpegUtils.RIGHT_SHIFT(num23 - num8, 11);
			array[num3 + 40] = JpegUtils.RIGHT_SHIFT(num21 + num9, 11);
			array[num3 + 80] = JpegUtils.RIGHT_SHIFT(num21 - num9, 11);
			array[num3 + 48] = JpegUtils.RIGHT_SHIFT(num19 + num10, 11);
			array[num3 + 72] = JpegUtils.RIGHT_SHIFT(num19 - num10, 11);
			array[num3 + 56] = JpegUtils.RIGHT_SHIFT(num17 + num11, 11);
			array[num3 + 64] = JpegUtils.RIGHT_SHIFT(num17 - num11, 11);
			num++;
			num2++;
			num3++;
		}
		num3 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num24 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int j = 0; j < 16; j++)
		{
			int num25 = array[num3] + 16384 + 16;
			num25 <<= 13;
			int num26 = array[num3 + 4];
			int num27 = num26 * SLOW_INTEGER_FIX(1.306562965);
			int num28 = num26 * 4433;
			int num29 = num25 + num27;
			int num30 = num25 - num27;
			int num31 = num25 + num28;
			int num32 = num25 - num28;
			num26 = array[num3 + 2];
			int num33 = array[num3 + 6];
			int num34 = num26 - num33;
			int num35 = num34 * SLOW_INTEGER_FIX(0.275899379);
			num34 *= SLOW_INTEGER_FIX(1.387039845);
			num25 = num34 + num33 * 20995;
			num27 = num35 + num26 * 7373;
			num28 = num34 - num26 * SLOW_INTEGER_FIX(0.601344887);
			int num36 = num35 - num33 * SLOW_INTEGER_FIX(0.509795579);
			int num37 = num29 + num25;
			int num38 = num29 - num25;
			int num39 = num31 + num27;
			int num40 = num31 - num27;
			int num41 = num32 + num28;
			int num42 = num32 - num28;
			int num43 = num30 + num36;
			int num44 = num30 - num36;
			num26 = array[num3 + 1];
			num33 = array[num3 + 3];
			num34 = array[num3 + 5];
			num35 = array[num3 + 7];
			num30 = num26 + num34;
			num27 = (num26 + num33) * SLOW_INTEGER_FIX(1.353318001);
			num28 = num30 * SLOW_INTEGER_FIX(1.247225013);
			num36 = (num26 + num35) * SLOW_INTEGER_FIX(1.093201867);
			num29 = (num26 - num35) * SLOW_INTEGER_FIX(0.897167586);
			num30 *= SLOW_INTEGER_FIX(0.666655658);
			num31 = (num26 - num33) * SLOW_INTEGER_FIX(0.410524528);
			num25 = num27 + num28 + num36 - num26 * SLOW_INTEGER_FIX(2.286341144);
			num32 = num29 + num30 + num31 - num26 * SLOW_INTEGER_FIX(1.835730603);
			num26 = (num33 + num34) * SLOW_INTEGER_FIX(0.138617169);
			num27 += num26 + num33 * SLOW_INTEGER_FIX(0.071888074);
			num28 += num26 - num34 * SLOW_INTEGER_FIX(1.125726048);
			num26 = (num34 - num33) * SLOW_INTEGER_FIX(1.407403738);
			num30 += num26 - num34 * SLOW_INTEGER_FIX(0.766367282);
			num31 += num26 + num33 * SLOW_INTEGER_FIX(1.971951411);
			num33 += num35;
			num26 = num33 * -SLOW_INTEGER_FIX(0.666655658);
			num27 += num26;
			num36 += num26 + num35 * SLOW_INTEGER_FIX(1.065388962);
			num33 *= -SLOW_INTEGER_FIX(1.247225013);
			num29 += num33 + num35 * SLOW_INTEGER_FIX(3.141271809);
			num31 += num33;
			num33 = (num34 + num35) * -SLOW_INTEGER_FIX(1.353318001);
			num28 += num33;
			num36 += num33;
			num33 = (num35 - num34) * SLOW_INTEGER_FIX(0.410524528);
			num29 += num33;
			num30 += num33;
			int i2 = output_row + j;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num37 + num25, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 15] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num37 - num25, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num39 + num27, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 14] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num39 - num27, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num41 + num28, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 13] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num41 - num28, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num43 + num36, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 12] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num43 - num36, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num44 + num29, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 11] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num44 - num29, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num42 + num30, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 10] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num42 - num30, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num40 + num31, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 9] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num40 - num31, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num38 + num32, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 8] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num38 - num32, 18)) & 0x3FF];
			num3 += 8;
		}
	}

	private void jpeg_idct_16x8(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[64];
		int num = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num2 = 0;
		int num3 = 0;
		for (int num4 = 8; num4 > 0; num4--)
		{
			if (coef_block[num + 8] == 0 && coef_block[num + 16] == 0 && coef_block[num + 24] == 0 && coef_block[num + 32] == 0 && coef_block[num + 40] == 0 && coef_block[num + 48] == 0 && coef_block[num + 56] == 0)
			{
				array[num3 + 56] = (array[num3 + 48] = (array[num3 + 40] = (array[num3 + 32] = (array[num3 + 24] = (array[num3 + 16] = (array[num3 + 8] = (array[num3] = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]) << 2)))))));
				num++;
				num2++;
				num3++;
			}
			else
			{
				int num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num2 + 16]);
				int num6 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num2 + 48]);
				int num7 = (num5 + num6) * 4433;
				int num8 = num7 + num5 * 6270;
				int num9 = num7 - num6 * 15137;
				num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]);
				num6 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num2 + 32]);
				num5 <<= 13;
				num6 <<= 13;
				num5 += 1024;
				int num10 = num5 + num6;
				int num11 = num5 - num6;
				int num12 = num10 + num8;
				int num13 = num10 - num8;
				int num14 = num11 + num9;
				int num15 = num11 - num9;
				num10 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num2 + 56]);
				num11 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num2 + 40]);
				num8 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num2 + 24]);
				num9 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num2 + 8]);
				num5 = num10 + num8;
				num6 = num11 + num9;
				num7 = (num5 + num6) * 9633;
				num5 *= -16069;
				num6 *= -3196;
				num5 += num7;
				num6 += num7;
				num7 = (num10 + num9) * -7373;
				num10 *= 2446;
				num9 *= 12299;
				num10 += num7 + num5;
				num9 += num7 + num6;
				num7 = (num11 + num8) * -20995;
				num11 *= 16819;
				num8 *= 25172;
				num11 += num7 + num6;
				num8 += num7 + num5;
				array[num3] = JpegUtils.RIGHT_SHIFT(num12 + num9, 11);
				array[num3 + 56] = JpegUtils.RIGHT_SHIFT(num12 - num9, 11);
				array[num3 + 8] = JpegUtils.RIGHT_SHIFT(num14 + num8, 11);
				array[num3 + 48] = JpegUtils.RIGHT_SHIFT(num14 - num8, 11);
				array[num3 + 16] = JpegUtils.RIGHT_SHIFT(num15 + num11, 11);
				array[num3 + 40] = JpegUtils.RIGHT_SHIFT(num15 - num11, 11);
				array[num3 + 24] = JpegUtils.RIGHT_SHIFT(num13 + num10, 11);
				array[num3 + 32] = JpegUtils.RIGHT_SHIFT(num13 - num10, 11);
				num++;
				num2++;
				num3++;
			}
		}
		num3 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num16 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int i = 0; i < 8; i++)
		{
			int num17 = array[num3] + 16384 + 16;
			num17 <<= 13;
			int num18 = array[num3 + 4];
			int num19 = num18 * SLOW_INTEGER_FIX(1.306562965);
			int num20 = num18 * 4433;
			int num21 = num17 + num19;
			int num22 = num17 - num19;
			int num23 = num17 + num20;
			int num24 = num17 - num20;
			num18 = array[num3 + 2];
			int num25 = array[num3 + 6];
			int num26 = num18 - num25;
			int num27 = num26 * SLOW_INTEGER_FIX(0.275899379);
			num26 *= SLOW_INTEGER_FIX(1.387039845);
			num17 = num26 + num25 * 20995;
			num19 = num27 + num18 * 7373;
			num20 = num26 - num18 * SLOW_INTEGER_FIX(0.601344887);
			int num28 = num27 - num25 * SLOW_INTEGER_FIX(0.509795579);
			int num29 = num21 + num17;
			int num30 = num21 - num17;
			int num31 = num23 + num19;
			int num32 = num23 - num19;
			int num33 = num24 + num20;
			int num34 = num24 - num20;
			int num35 = num22 + num28;
			int num36 = num22 - num28;
			num18 = array[num3 + 1];
			num25 = array[num3 + 3];
			num26 = array[num3 + 5];
			num27 = array[num3 + 7];
			num22 = num18 + num26;
			num19 = (num18 + num25) * SLOW_INTEGER_FIX(1.353318001);
			num20 = num22 * SLOW_INTEGER_FIX(1.247225013);
			num28 = (num18 + num27) * SLOW_INTEGER_FIX(1.093201867);
			num21 = (num18 - num27) * SLOW_INTEGER_FIX(0.897167586);
			num22 *= SLOW_INTEGER_FIX(0.666655658);
			num23 = (num18 - num25) * SLOW_INTEGER_FIX(0.410524528);
			num17 = num19 + num20 + num28 - num18 * SLOW_INTEGER_FIX(2.286341144);
			num24 = num21 + num22 + num23 - num18 * SLOW_INTEGER_FIX(1.835730603);
			num18 = (num25 + num26) * SLOW_INTEGER_FIX(0.138617169);
			num19 += num18 + num25 * SLOW_INTEGER_FIX(0.071888074);
			num20 += num18 - num26 * SLOW_INTEGER_FIX(1.125726048);
			num18 = (num26 - num25) * SLOW_INTEGER_FIX(1.407403738);
			num22 += num18 - num26 * SLOW_INTEGER_FIX(0.766367282);
			num23 += num18 + num25 * SLOW_INTEGER_FIX(1.971951411);
			num25 += num27;
			num18 = num25 * -SLOW_INTEGER_FIX(0.666655658);
			num19 += num18;
			num28 += num18 + num27 * SLOW_INTEGER_FIX(1.065388962);
			num25 *= -SLOW_INTEGER_FIX(1.247225013);
			num21 += num25 + num27 * SLOW_INTEGER_FIX(3.141271809);
			num23 += num25;
			num25 = (num26 + num27) * -SLOW_INTEGER_FIX(1.353318001);
			num20 += num25;
			num28 += num25;
			num25 = (num27 - num26) * SLOW_INTEGER_FIX(0.410524528);
			num21 += num25;
			num22 += num25;
			int i2 = output_row + i;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num29 + num17, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 15] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num29 - num17, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num31 + num19, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 14] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num31 - num19, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num33 + num20, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 13] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num33 - num20, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num35 + num28, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 12] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num35 - num28, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num36 + num21, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 11] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num36 - num21, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num34 + num22, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 10] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num34 - num22, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num32 + num23, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 9] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num32 - num23, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num30 + num24, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 8] = sample_range_limit[(num16 + JpegUtils.RIGHT_SHIFT(num30 - num24, 18)) & 0x3FF];
			num3 += 8;
		}
	}

	private void jpeg_idct_14x7(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_12x6(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_10x5(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_8x4(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_6x3(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_4x2(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_2x1(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_8x16(int component_index, short[] coef_block, int output_row, int output_col)
	{
		int[] array = new int[128];
		int num = 0;
		int[] int_array = m_dctTables[component_index].int_array;
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < 8; i++)
		{
			int num4 = SLOW_INTEGER_DEQUANTIZE(coef_block[num], int_array[num2]);
			num4 <<= 13;
			num4 += 1024;
			int num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 32], int_array[num2 + 32]);
			int num6 = num5 * SLOW_INTEGER_FIX(1.306562965);
			int num7 = num5 * 4433;
			int num8 = num4 + num6;
			int num9 = num4 - num6;
			int num10 = num4 + num7;
			int num11 = num4 - num7;
			num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 16], int_array[num2 + 16]);
			int num12 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 48], int_array[num2 + 48]);
			int num13 = num5 - num12;
			int num14 = num13 * SLOW_INTEGER_FIX(0.275899379);
			num13 *= SLOW_INTEGER_FIX(1.387039845);
			num4 = num13 + num12 * 20995;
			num6 = num14 + num5 * 7373;
			num7 = num13 - num5 * SLOW_INTEGER_FIX(0.601344887);
			int num15 = num14 - num12 * SLOW_INTEGER_FIX(0.509795579);
			int num16 = num8 + num4;
			int num17 = num8 - num4;
			int num18 = num10 + num6;
			int num19 = num10 - num6;
			int num20 = num11 + num7;
			int num21 = num11 - num7;
			int num22 = num9 + num15;
			int num23 = num9 - num15;
			num5 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 8], int_array[num2 + 8]);
			num12 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 24], int_array[num2 + 24]);
			num13 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 40], int_array[num2 + 40]);
			num14 = SLOW_INTEGER_DEQUANTIZE(coef_block[num + 56], int_array[num2 + 56]);
			num9 = num5 + num13;
			num6 = (num5 + num12) * SLOW_INTEGER_FIX(1.353318001);
			num7 = num9 * SLOW_INTEGER_FIX(1.247225013);
			num15 = (num5 + num14) * SLOW_INTEGER_FIX(1.093201867);
			num8 = (num5 - num14) * SLOW_INTEGER_FIX(0.897167586);
			num9 *= SLOW_INTEGER_FIX(0.666655658);
			num10 = (num5 - num12) * SLOW_INTEGER_FIX(0.410524528);
			num4 = num6 + num7 + num15 - num5 * SLOW_INTEGER_FIX(2.286341144);
			num11 = num8 + num9 + num10 - num5 * SLOW_INTEGER_FIX(1.835730603);
			num5 = (num12 + num13) * SLOW_INTEGER_FIX(0.138617169);
			num6 += num5 + num12 * SLOW_INTEGER_FIX(0.071888074);
			num7 += num5 - num13 * SLOW_INTEGER_FIX(1.125726048);
			num5 = (num13 - num12) * SLOW_INTEGER_FIX(1.407403738);
			num9 += num5 - num13 * SLOW_INTEGER_FIX(0.766367282);
			num10 += num5 + num12 * SLOW_INTEGER_FIX(1.971951411);
			num12 += num14;
			num5 = num12 * -SLOW_INTEGER_FIX(0.666655658);
			num6 += num5;
			num15 += num5 + num14 * SLOW_INTEGER_FIX(1.065388962);
			num12 *= -SLOW_INTEGER_FIX(1.247225013);
			num8 += num12 + num14 * SLOW_INTEGER_FIX(3.141271809);
			num10 += num12;
			num12 = (num13 + num14) * -SLOW_INTEGER_FIX(1.353318001);
			num7 += num12;
			num15 += num12;
			num12 = (num14 - num13) * SLOW_INTEGER_FIX(0.410524528);
			num8 += num12;
			num9 += num12;
			array[num3] = JpegUtils.RIGHT_SHIFT(num16 + num4, 11);
			array[num3 + 120] = JpegUtils.RIGHT_SHIFT(num16 - num4, 11);
			array[num3 + 8] = JpegUtils.RIGHT_SHIFT(num18 + num6, 11);
			array[num3 + 112] = JpegUtils.RIGHT_SHIFT(num18 - num6, 11);
			array[num3 + 16] = JpegUtils.RIGHT_SHIFT(num20 + num7, 11);
			array[num3 + 104] = JpegUtils.RIGHT_SHIFT(num20 - num7, 11);
			array[num3 + 24] = JpegUtils.RIGHT_SHIFT(num22 + num15, 11);
			array[num3 + 96] = JpegUtils.RIGHT_SHIFT(num22 - num15, 11);
			array[num3 + 32] = JpegUtils.RIGHT_SHIFT(num23 + num8, 11);
			array[num3 + 88] = JpegUtils.RIGHT_SHIFT(num23 - num8, 11);
			array[num3 + 40] = JpegUtils.RIGHT_SHIFT(num21 + num9, 11);
			array[num3 + 80] = JpegUtils.RIGHT_SHIFT(num21 - num9, 11);
			array[num3 + 48] = JpegUtils.RIGHT_SHIFT(num19 + num10, 11);
			array[num3 + 72] = JpegUtils.RIGHT_SHIFT(num19 - num10, 11);
			array[num3 + 56] = JpegUtils.RIGHT_SHIFT(num17 + num11, 11);
			array[num3 + 64] = JpegUtils.RIGHT_SHIFT(num17 - num11, 11);
			num++;
			num2++;
			num3++;
		}
		num3 = 0;
		byte[] sample_range_limit = m_cinfo.m_sample_range_limit;
		int num24 = m_cinfo.m_sampleRangeLimitOffset - 384;
		for (int j = 0; j < 16; j++)
		{
			int num25 = array[num3] + 16384 + 16;
			int num26 = array[num3 + 4];
			int num27 = num25 + num26 << 13;
			int num28 = num25 - num26 << 13;
			num25 = array[num3 + 2];
			num26 = array[num3 + 6];
			int num29 = (num25 + num26) * 4433;
			int num30 = num29 + num25 * 6270;
			int num31 = num29 - num26 * 15137;
			int num32 = num27 + num30;
			int num33 = num27 - num30;
			int num34 = num28 + num31;
			int num35 = num28 - num31;
			num27 = array[num3 + 7];
			num28 = array[num3 + 5];
			num30 = array[num3 + 3];
			num31 = array[num3 + 1];
			num25 = num27 + num30;
			num26 = num28 + num31;
			num29 = (num25 + num26) * 9633;
			num25 *= -16069;
			num26 *= -3196;
			num25 += num29;
			num26 += num29;
			num29 = (num27 + num31) * -7373;
			num27 *= 2446;
			num31 *= 12299;
			num27 += num29 + num25;
			num31 += num29 + num26;
			num29 = (num28 + num30) * -20995;
			num28 *= 16819;
			num30 *= 25172;
			num28 += num29 + num26;
			num30 += num29 + num25;
			int i2 = output_row + j;
			m_componentBuffer[i2][output_col] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num32 + num31, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 7] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num32 - num31, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 1] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num34 + num30, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 6] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num34 - num30, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 2] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num35 + num28, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 5] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num35 - num28, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 3] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num33 + num27, 18)) & 0x3FF];
			m_componentBuffer[i2][output_col + 4] = sample_range_limit[(num24 + JpegUtils.RIGHT_SHIFT(num33 - num27, 18)) & 0x3FF];
			num3 += 8;
		}
	}

	private void jpeg_idct_7x14(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_6x12(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_5x10(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_4x8(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_3x6(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_2x4(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}

	private void jpeg_idct_1x2(int component_index, short[] coef_block, int output_row, int output_col)
	{
		m_cinfo.ERREXIT(J_MESSAGE_CODE.JERR_NOTIMPL);
	}
}

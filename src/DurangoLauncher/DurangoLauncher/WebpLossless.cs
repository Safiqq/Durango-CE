using System;
using System.Collections.Generic;
using System.IO;

namespace DurangoLauncher;

public static class WebpLossless
{
	public sealed record Image(int Width, int Height, uint[] Argb);

	private sealed class BitReader
	{
		private readonly byte[] _b;

		private long _bit;

		public BitReader(byte[] b)
		{
			_b = b;
		}

		public uint Read(int n)
		{
			uint num = 0u;
			for (int i = 0; i < n; i++)
			{
				long num2 = _bit >> 3;
				if (num2 >= _b.Length)
				{
					throw new InvalidDataException("VP8L truncated");
				}
				num |= (uint)(((_b[num2] >> (int)(_bit & 7)) & 1) << i);
				_bit++;
			}
			return num;
		}
	}

	private sealed class Prefix
	{
		private readonly int[] _count = new int[16];

		private readonly int[] _sym;

		private readonly int _single = -1;

		public Prefix(int[] lengths)
		{
			int num = 0;
			int single = 0;
			for (int i = 0; i < lengths.Length; i++)
			{
				if (lengths[i] < 0 || lengths[i] > 15)
				{
					throw new InvalidDataException("VP8L code length");
				}
				if (lengths[i] > 0)
				{
					_count[lengths[i]]++;
					num++;
					single = i;
				}
			}
			if (num == 0)
			{
				throw new InvalidDataException("VP8L empty code");
			}
			_sym = new int[num];
			if (num == 1)
			{
				_single = single;
				return;
			}
			int[] array = new int[16];
			for (int j = 1; j < 15; j++)
			{
				array[j + 1] = array[j] + _count[j];
			}
			for (int k = 0; k < lengths.Length; k++)
			{
				if (lengths[k] > 0)
				{
					_sym[array[lengths[k]]++] = k;
				}
			}
		}

		public int Read(BitReader br)
		{
			if (_single >= 0)
			{
				return _single;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			for (int i = 1; i <= 15; i++)
			{
				num |= (int)br.Read(1);
				int num4 = _count[i];
				if (num - num2 < num4)
				{
					return _sym[num3 + num - num2];
				}
				num3 += num4;
				num2 += num4;
				num2 <<= 1;
				num <<= 1;
			}
			throw new InvalidDataException("VP8L bad code");
		}
	}

	private abstract class Transform
	{
		public int XSize;

		public abstract uint[] Inverse(uint[] pix, int height);
	}

	private sealed class PredictorTransform : Transform
	{
		public int Bits;

		public uint[] Data = Array.Empty<uint>();

		public override uint[] Inverse(uint[] p, int height)
		{
			int xSize = XSize;
			int num = DivRound(xSize, Bits);
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < xSize; j++)
				{
					int num2 = i * xSize + j;
					uint b;
					if (i == 0)
					{
						b = ((j == 0) ? 4278190080u : p[num2 - 1]);
					}
					else if (j == 0)
					{
						b = p[num2 - xSize];
					}
					else
					{
						uint num3 = p[num2 - 1];
						uint num4 = p[num2 - xSize];
						uint num5 = p[num2 - xSize - 1];
						uint num6 = p[num2 - xSize + 1];
						b = ((Data[(i >> Bits) * num + (j >> Bits)] >> 8) & 0xF) switch
						{
							1u => num3, 
							2u => num4, 
							3u => num6, 
							4u => num5, 
							5u => Avg(Avg(num3, num6), num4), 
							6u => Avg(num3, num5), 
							7u => Avg(num3, num4), 
							8u => Avg(num5, num4), 
							9u => Avg(num4, num6), 
							10u => Avg(Avg(num3, num5), Avg(num4, num6)), 
							11u => Select(num3, num4, num5), 
							12u => ClampFull(num3, num4, num5), 
							13u => ClampHalf(Avg(num3, num4), num5), 
							_ => 4278190080u, 
						};
					}
					p[num2] = Add(p[num2], b);
				}
			}
			return p;
		}
	}

	private sealed class ColorTransform : Transform
	{
		public int Bits;

		public uint[] Data = Array.Empty<uint>();

		private static int Delta(int t, int c)
		{
			return (sbyte)t * (sbyte)c >> 5;
		}

		public override uint[] Inverse(uint[] p, int height)
		{
			int xSize = XSize;
			int num = DivRound(xSize, Bits);
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < xSize; j++)
				{
					uint num2 = Data[(i >> Bits) * num + (j >> Bits)];
					int t = (int)(num2 & 0xFF);
					int t2 = (int)((num2 >> 8) & 0xFF);
					int t3 = (int)((num2 >> 16) & 0xFF);
					int num3 = i * xSize + j;
					uint num4 = p[num3];
					int c = (int)((num4 >> 8) & 0xFF);
					int num5 = ((int)((num4 >> 16) & 0xFF) + Delta(t, c)) & 0xFF;
					int num6 = ((int)(num4 & 0xFF) + Delta(t2, c) + Delta(t3, num5)) & 0xFF;
					p[num3] = (num4 & 0xFF00FF00u) | (uint)(num5 << 16) | (uint)num6;
				}
			}
			return p;
		}
	}

	private sealed class SubtractGreen : Transform
	{
		public override uint[] Inverse(uint[] p, int height)
		{
			for (int i = 0; i < p.Length; i++)
			{
				uint num = p[i];
				uint num2 = (num >> 8) & 0xFF;
				p[i] = (num & 0xFF00FF00u) | ((((num >> 16) + num2) & 0xFF) << 16) | (((num & 0xFF) + num2) & 0xFF);
			}
			return p;
		}
	}

	private sealed class ColorIndexing : Transform
	{
		public int Bits;

		public uint[] Palette = Array.Empty<uint>();

		public override uint[] Inverse(uint[] p, int height)
		{
			int xSize = XSize;
			int num = DivRound(xSize, Bits);
			uint[] array = new uint[xSize * height];
			int num2 = 8 >> Bits;
			int num3 = (1 << num2) - 1;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < xSize; j++)
				{
					int num4 = ((Bits == 0) ? ((int)((p[i * num + j] >> 8) & 0xFF)) : ((int)(p[i * num + (j >> Bits)] >> 8 >> (j & ((1 << Bits) - 1)) * num2) & num3));
					array[i * xSize + j] = ((num4 < Palette.Length) ? Palette[num4] : 0u);
				}
			}
			return array;
		}
	}

	private const int MaxSize = 4096;

	private static readonly int[] CodeLengthOrder = new int[19]
	{
		17, 18, 0, 1, 2, 3, 4, 5, 16, 6,
		7, 8, 9, 10, 11, 12, 13, 14, 15
	};

	private static readonly byte[] CodeToPlane = new byte[120]
	{
		24, 7, 23, 25, 40, 6, 39, 41, 22, 26,
		38, 42, 56, 5, 55, 57, 21, 27, 54, 58,
		37, 43, 72, 4, 71, 73, 20, 28, 53, 59,
		70, 74, 36, 44, 88, 69, 75, 52, 60, 3,
		87, 89, 19, 29, 86, 90, 35, 45, 68, 76,
		85, 91, 51, 61, 104, 2, 103, 105, 18, 30,
		102, 106, 34, 46, 84, 92, 67, 77, 101, 107,
		50, 62, 120, 1, 119, 121, 83, 93, 17, 31,
		100, 108, 66, 78, 118, 122, 33, 47, 117, 123,
		49, 63, 99, 109, 82, 94, 0, 116, 124, 65,
		79, 16, 32, 98, 110, 48, 115, 125, 81, 95,
		64, 114, 126, 97, 111, 80, 113, 127, 96, 112
	};

	public static Image? Decode(ReadOnlySpan<byte> file)
	{
		if (file.Length < 20 || file[0] != 82 || file[1] != 73 || file[2] != 70 || file[3] != 70 || file[8] != 87 || file[9] != 69 || file[10] != 66 || file[11] != 80)
		{
			return null;
		}
		int num = 12;
		while (num + 8 <= file.Length)
		{
			uint num2 = (uint)(file[num + 4] | (file[num + 5] << 8) | (file[num + 6] << 16) | (file[num + 7] << 24));
			bool num3 = file[num] == 86 && file[num + 1] == 80 && file[num + 2] == 56 && file[num + 3] == 76;
			int num4 = num + 8;
			if (num2 > (uint)(file.Length - num4))
			{
				num2 = (uint)(file.Length - num4);
			}
			if (num3)
			{
				return DecodeVp8l(file.Slice(num4, (int)num2));
			}
			num = num4 + (int)num2 + (int)(num2 & 1);
		}
		return null;
	}

	public static Image DecodeVp8l(ReadOnlySpan<byte> data)
	{
		BitReader bitReader = new BitReader(data.ToArray());
		if (bitReader.Read(8) != 47)
		{
			throw new InvalidDataException("VP8L signature");
		}
		int num = (int)(bitReader.Read(14) + 1);
		int num2 = (int)(bitReader.Read(14) + 1);
		bitReader.Read(1);
		if (bitReader.Read(3) != 0)
		{
			throw new InvalidDataException("VP8L version");
		}
		if (num > 4096 || num2 > 4096)
		{
			throw new InvalidDataException("VP8L too large");
		}
		List<Transform> transforms;
		uint[] array = DecodeStream(bitReader, num, num2, top: true, out transforms);
		for (int num3 = transforms.Count - 1; num3 >= 0; num3--)
		{
			array = transforms[num3].Inverse(array, num2);
		}
		return new Image(num, num2, array);
	}

	private static Prefix ReadCode(BitReader br, int alphabet)
	{
		int[] array = new int[alphabet];
		if (br.Read(1) == 1)
		{
			uint num = br.Read(1) + 1;
			int num2 = (int)br.Read((br.Read(1) == 0) ? 1 : 8);
			if (num2 >= alphabet)
			{
				throw new InvalidDataException("VP8L symbol");
			}
			array[num2] = 1;
			if (num == 2)
			{
				int num3 = (int)br.Read(8);
				if (num3 >= alphabet)
				{
					throw new InvalidDataException("VP8L symbol");
				}
				array[num3] = 1;
			}
			return new Prefix(array);
		}
		int[] array2 = new int[19];
		int num4 = (int)(br.Read(4) + 4);
		if (num4 > 19)
		{
			throw new InvalidDataException("VP8L code-length count");
		}
		for (int i = 0; i < num4; i++)
		{
			array2[CodeLengthOrder[i]] = (int)br.Read(3);
		}
		Prefix prefix = new Prefix(array2);
		int num5 = alphabet;
		if (br.Read(1) == 1)
		{
			int n = (int)(2 + 2 * br.Read(3));
			num5 = (int)(2 + br.Read(n));
			if (num5 > alphabet)
			{
				throw new InvalidDataException("VP8L max_symbol");
			}
		}
		int num6 = 8;
		int num7 = 0;
		while (num7 < alphabet && num5-- != 0)
		{
			int num8 = prefix.Read(br);
			if (num8 < 16)
			{
				array[num7++] = num8;
				if (num8 != 0)
				{
					num6 = num8;
				}
				continue;
			}
			int num9 = num8 - 16;
			int num10 = (int)br.Read(num9 switch
			{
				1 => 3, 
				0 => 2, 
				_ => 7, 
			}) + ((num9 == 2) ? 11 : 3);
			if (num7 + num10 > alphabet)
			{
				throw new InvalidDataException("VP8L repeat");
			}
			int num11 = ((num8 == 16) ? num6 : 0);
			while (num10-- > 0)
			{
				array[num7++] = num11;
			}
		}
		return new Prefix(array);
	}

	private static int DivRound(int n, int bits)
	{
		return n + (1 << bits) - 1 >> bits;
	}

	private static uint Add(uint a, uint b)
	{
		return (((a & 0xFF00FF00u) + (b & 0xFF00FF00u)) & 0xFF00FF00u) | (((a & 0xFF00FF) + (b & 0xFF00FF)) & 0xFF00FF);
	}

	private static uint Avg(uint a, uint b)
	{
		return (((a ^ b) & 0xFEFEFEFEu) >> 1) + (a & b);
	}

	private static int Ch(uint p, int s)
	{
		return (int)((p >> s) & 0xFF);
	}

	private static uint Select(uint l, uint t, uint tl)
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 32; i += 8)
		{
			num += Math.Abs(Ch(t, i) - Ch(tl, i));
			num2 += Math.Abs(Ch(l, i) - Ch(tl, i));
		}
		if (num >= num2)
		{
			return t;
		}
		return l;
	}

	private static uint ClampFull(uint a, uint b, uint c)
	{
		uint num = 0u;
		for (int i = 0; i < 32; i += 8)
		{
			num |= (uint)(Math.Clamp(Ch(a, i) + Ch(b, i) - Ch(c, i), 0, 255) << i);
		}
		return num;
	}

	private static uint ClampHalf(uint a, uint b)
	{
		uint num = 0u;
		for (int i = 0; i < 32; i += 8)
		{
			int num2 = Ch(a, i);
			num |= (uint)(Math.Clamp(num2 + (num2 - Ch(b, i)) / 2, 0, 255) << i);
		}
		return num;
	}

	private static uint[] DecodeStream(BitReader br, int xsize, int ysize, bool top, out List<Transform> transforms)
	{
		transforms = new List<Transform>();
		List<Transform> transforms2;
		if (top)
		{
			bool[] array = new bool[4];
			while (br.Read(1) == 1)
			{
				int num = (int)br.Read(2);
				if (array[num])
				{
					throw new InvalidDataException("VP8L duplicate transform");
				}
				array[num] = true;
				switch (num)
				{
				case 0:
				case 1:
				{
					int bits = (int)(br.Read(3) + 2);
					uint[] data = DecodeStream(br, DivRound(xsize, bits), DivRound(ysize, bits), top: false, out transforms2);
					transforms.Add((num == 0) ? ((Transform)new PredictorTransform
					{
						XSize = xsize,
						Bits = bits,
						Data = data
					}) : ((Transform)new ColorTransform
					{
						XSize = xsize,
						Bits = bits,
						Data = data
					}));
					continue;
				}
				case 2:
					transforms.Add(new SubtractGreen
					{
						XSize = xsize
					});
					continue;
				}
				int num2 = (int)(br.Read(8) + 1);
				int bits2 = ((num2 <= 16) ? ((num2 > 4) ? 1 : ((num2 > 2) ? 2 : 3)) : 0);
				uint[] array2 = DecodeStream(br, num2, 1, top: false, out transforms2);
				for (int i = 1; i < array2.Length; i++)
				{
					array2[i] = Add(array2[i], array2[i - 1]);
				}
				transforms.Add(new ColorIndexing
				{
					XSize = xsize,
					Bits = bits2,
					Palette = array2
				});
				xsize = DivRound(xsize, bits2);
			}
		}
		int num3 = 0;
		if (br.Read(1) == 1)
		{
			num3 = (int)br.Read(4);
			if (num3 < 1 || num3 > 11)
			{
				throw new InvalidDataException("VP8L color cache bits");
			}
		}
		int num4 = 0;
		int num5 = 0;
		uint[] array3 = null;
		int num6 = 1;
		if (top && br.Read(1) == 1)
		{
			num4 = (int)(br.Read(3) + 2);
			num5 = DivRound(xsize, num4);
			array3 = DecodeStream(br, num5, DivRound(ysize, num4), top: false, out transforms2);
			for (int j = 0; j < array3.Length; j++)
			{
				array3[j] = (array3[j] >> 8) & 0xFFFF;
				num6 = Math.Max(num6, (int)(array3[j] + 1));
			}
		}
		int num7 = ((num3 > 0) ? (1 << num3) : 0);
		Prefix[][] array4 = new Prefix[num6][];
		for (int k = 0; k < num6; k++)
		{
			array4[k] = new Prefix[5]
			{
				ReadCode(br, 280 + num7),
				ReadCode(br, 256),
				ReadCode(br, 256),
				ReadCode(br, 256),
				ReadCode(br, 40)
			};
		}
		uint[] array5 = new uint[xsize * ysize];
		uint[] array6 = ((num7 > 0) ? new uint[num7] : null);
		int num8 = 0;
		int num9 = 0;
		int num10 = array5.Length;
		while (num8 < num10)
		{
			Prefix[] array7 = ((array3 == null) ? array4[0] : array4[array3[(num8 / xsize >> num4) * num5 + (num8 % xsize >> num4)]]);
			int num11 = array7[0].Read(br);
			if (num11 < 256)
			{
				int num12 = array7[1].Read(br);
				int num13 = array7[2].Read(br);
				int num14 = array7[3].Read(br);
				array5[num8++] = (uint)((num14 << 24) | (num12 << 16) | (num11 << 8) | num13);
			}
			else if (num11 < 280)
			{
				int num15 = CopyLength(br, num11 - 256);
				int num16 = Distance(xsize, CopyLength(br, array7[4].Read(br)));
				if (num16 > num8 || num8 + num15 > num10)
				{
					throw new InvalidDataException("VP8L back-reference");
				}
				int num17 = 0;
				while (num17 < num15)
				{
					array5[num8] = array5[num8 - num16];
					num17++;
					num8++;
				}
			}
			else
			{
				if (array6 == null)
				{
					throw new InvalidDataException("VP8L cache code without cache");
				}
				while (num9 < num8)
				{
					Insert(array6, num3, array5[num9++]);
				}
				array5[num8++] = array6[num11 - 280];
			}
			if (array6 != null)
			{
				while (num9 < num8)
				{
					Insert(array6, num3, array5[num9++]);
				}
			}
		}
		return array5;
	}

	private static void Insert(uint[] cache, int bits, uint argb)
	{
		cache[506832829 * argb >> 32 - bits] = argb;
	}

	private static int CopyLength(BitReader br, int prefix)
	{
		if (prefix < 4)
		{
			return prefix + 1;
		}
		int num = prefix - 2 >> 1;
		return (2 + (prefix & 1) << num) + (int)br.Read(num) + 1;
	}

	private static int Distance(int xsize, int code)
	{
		if (code > 120)
		{
			return code - 120;
		}
		int num = CodeToPlane[code - 1];
		int num2 = (num >> 4) * xsize + (8 - (num & 0xF));
		if (num2 < 1)
		{
			return 1;
		}
		return num2;
	}
}

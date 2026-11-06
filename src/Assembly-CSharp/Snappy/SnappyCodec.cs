using System;
using System.IO;

namespace Snappy;

public static class SnappyCodec
{
	private const int BlockSize = 65536;

	private const int HashBits = 14;

	public static int GetMaxCompressedLength(int inLength)
	{
		return 32 + inLength + inLength / 6;
	}

	public static byte[] Compress(byte[] input)
	{
		return CompressToArray(input, 0, input.Length);
	}

	public static int Compress(byte[] input, int offset, int length, byte[] output, int outOffset)
	{
		CheckRange(input, offset, length);
		byte[] array = CompressToArray(input, offset, length);
		if (output == null || outOffset < 0 || outOffset + array.Length > output.Length)
		{
			throw new ArgumentOutOfRangeException("output", "Snappy: output buffer too small");
		}
		Buffer.BlockCopy(array, 0, output, outOffset, array.Length);
		return array.Length;
	}

	public static byte[] Uncompress(byte[] input)
	{
		byte[] array = new byte[GetUncompressedLength(input, 0, input.Length)];
		Uncompress(input, 0, input.Length, array, 0);
		return array;
	}

	public static int Uncompress(byte[] input, int offset, int length, byte[] output, int outOffset)
	{
		CheckRange(input, offset, length);
		int p = offset;
		int num = offset + length;
		int num2 = ReadLength(input, ref p, num);
		if (output == null || outOffset < 0 || outOffset + num2 > output.Length)
		{
			throw new ArgumentOutOfRangeException("output", "Snappy: output buffer too small");
		}
		int num3 = outOffset;
		int num4 = outOffset + num2;
		while (p < num)
		{
			byte b = input[p++];
			int num5;
			int num6;
			switch (b & 3)
			{
			case 0:
			{
				int num7 = b >> 2;
				if (num7 >= 60)
				{
					int num8 = num7 - 59;
					if (p + num8 > num)
					{
						throw new InvalidDataException("Snappy: truncated literal length");
					}
					num7 = 0;
					for (int i = 0; i < num8; i++)
					{
						num7 |= input[p++] << 8 * i;
					}
				}
				num5 = num7 + 1;
				if (num5 <= 0 || p + num5 > num || num3 + num5 > num4)
				{
					throw new InvalidDataException("Snappy: literal out of range");
				}
				Buffer.BlockCopy(input, p, output, num3, num5);
				p += num5;
				num3 += num5;
				continue;
			}
			case 1:
				if (p >= num)
				{
					throw new InvalidDataException("Snappy: truncated copy1");
				}
				num5 = ((b >> 2) & 7) + 4;
				num6 = (b >> 5 << 8) | input[p++];
				break;
			case 2:
				if (p + 2 > num)
				{
					throw new InvalidDataException("Snappy: truncated copy2");
				}
				num5 = (b >> 2) + 1;
				num6 = input[p] | (input[p + 1] << 8);
				p += 2;
				break;
			default:
				if (p + 4 > num)
				{
					throw new InvalidDataException("Snappy: truncated copy4");
				}
				num5 = (b >> 2) + 1;
				num6 = input[p] | (input[p + 1] << 8) | (input[p + 2] << 16) | (input[p + 3] << 24);
				p += 4;
				break;
			}
			if (num6 <= 0 || num6 > num3 - outOffset || num3 + num5 > num4)
			{
				throw new InvalidDataException("Snappy: copy out of range");
			}
			int num9 = num3 - num6;
			for (int j = 0; j < num5; j++)
			{
				output[num3 + j] = output[num9 + j];
			}
			num3 += num5;
		}
		if (num3 != num4)
		{
			throw new InvalidDataException("Snappy: got " + (num3 - outOffset) + " bytes, header says " + num2);
		}
		return num2;
	}

	public static int GetUncompressedLength(byte[] input)
	{
		return GetUncompressedLength(input, 0, input.Length);
	}

	public static int GetUncompressedLength(byte[] input, int offset, int length)
	{
		CheckRange(input, offset, length);
		int p = offset;
		return ReadLength(input, ref p, offset + length);
	}

	public static bool Validate(byte[] input)
	{
		return Validate(input, 0, input.Length);
	}

	public static bool Validate(byte[] input, int offset, int length)
	{
		try
		{
			byte[] output = new byte[GetUncompressedLength(input, offset, length)];
			Uncompress(input, offset, length, output, 0);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void CheckRange(byte[] input, int offset, int length)
	{
		if (input == null)
		{
			throw new ArgumentNullException("input");
		}
		if (offset < 0 || length < 0 || offset + length > input.Length)
		{
			throw new ArgumentOutOfRangeException("offset", "Snappy: input range out of bounds");
		}
	}

	private static int ReadLength(byte[] input, ref int p, int end)
	{
		ulong num = 0uL;
		int num2 = 0;
		while (true)
		{
			if (p >= end || num2 > 35)
			{
				throw new InvalidDataException("Snappy: bad length header");
			}
			byte b = input[p++];
			num |= (ulong)((long)(b & 0x7F) << num2);
			if (b < 128)
			{
				break;
			}
			num2 += 7;
		}
		if (num > int.MaxValue)
		{
			throw new InvalidDataException("Snappy: length too large");
		}
		return (int)num;
	}

	private static byte[] CompressToArray(byte[] src, int offset, int length)
	{
		MemoryStream memoryStream = new MemoryStream(GetMaxCompressedLength(length));
		uint num;
		for (num = (uint)length; num >= 128; num >>= 7)
		{
			memoryStream.WriteByte((byte)(num | 0x80));
		}
		memoryStream.WriteByte((byte)num);
		int[] table = new int[16384];
		for (int i = 0; i < length; i += 65536)
		{
			CompressBlock(src, offset + i, Math.Min(65536, length - i), table, memoryStream);
		}
		return memoryStream.ToArray();
	}

	private static uint Load32(byte[] s, int i)
	{
		return (uint)(s[i] | (s[i + 1] << 8) | (s[i + 2] << 16) | (s[i + 3] << 24));
	}

	private static void CompressBlock(byte[] s, int b, int n, int[] table, MemoryStream o)
	{
		if (n < 15)
		{
			EmitLiteral(s, b, n, o);
			return;
		}
		for (int i = 0; i < table.Length; i++)
		{
			table[i] = -1;
		}
		int num = 0;
		int num2 = 0;
		int num3 = n - 4;
		while (num2 <= num3)
		{
			uint num4 = Load32(s, b + num2);
			int num5 = (int)(num4 * 506832829 >> 18);
			int num6 = table[num5];
			table[num5] = num2;
			if (num6 >= 0 && Load32(s, b + num6) == num4)
			{
				if (num2 > num)
				{
					EmitLiteral(s, b + num, num2 - num, o);
				}
				int j;
				for (j = 4; num2 + j < n && s[b + num6 + j] == s[b + num2 + j]; j++)
				{
				}
				EmitCopy(num2 - num6, j, o);
				num2 += j;
				num = num2;
			}
			else
			{
				num2++;
			}
		}
		if (n > num)
		{
			EmitLiteral(s, b + num, n - num, o);
		}
	}

	private static void EmitLiteral(byte[] s, int at, int count, MemoryStream o)
	{
		int num = count - 1;
		if (num < 60)
		{
			o.WriteByte((byte)(num << 2));
		}
		else if (num < 256)
		{
			o.WriteByte(240);
			o.WriteByte((byte)num);
		}
		else if (num < 65536)
		{
			o.WriteByte(244);
			o.WriteByte((byte)num);
			o.WriteByte((byte)(num >> 8));
		}
		else if (num < 16777216)
		{
			o.WriteByte(248);
			o.WriteByte((byte)num);
			o.WriteByte((byte)(num >> 8));
			o.WriteByte((byte)(num >> 16));
		}
		else
		{
			o.WriteByte(252);
			o.WriteByte((byte)num);
			o.WriteByte((byte)(num >> 8));
			o.WriteByte((byte)(num >> 16));
			o.WriteByte((byte)(num >> 24));
		}
		o.Write(s, at, count);
	}

	private static void EmitCopy(int offset, int len, MemoryStream o)
	{
		while (len >= 68)
		{
			EmitCopy2(offset, 64, o);
			len -= 64;
		}
		if (len > 64)
		{
			EmitCopy2(offset, 60, o);
			len -= 60;
		}
		if (len <= 11 && offset < 2048)
		{
			o.WriteByte((byte)(1 | (len - 4 << 2) | (offset >> 8 << 5)));
			o.WriteByte((byte)offset);
		}
		else
		{
			EmitCopy2(offset, len, o);
		}
	}

	private static void EmitCopy2(int offset, int len, MemoryStream o)
	{
		o.WriteByte((byte)(2 | (len - 1 << 2)));
		o.WriteByte((byte)offset);
		o.WriteByte((byte)(offset >> 8));
	}
}

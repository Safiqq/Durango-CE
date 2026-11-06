using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace DurangoLauncher;

public static class Cdc
{
	public delegate void ChunkHandler(long offset, ReadOnlySpan<byte> chunk);

	public const int Min = 16384;

	public const int Avg = 65536;

	public const int Max = 262144;

	private const ulong MaskS = 18446673704965373952uL;

	private const ulong MaskL = 18445618173802708992uL;

	private static readonly ulong[] Gear = MakeGear();

	private static ulong[] MakeGear()
	{
		ulong[] array = new ulong[256];
		ulong num = 4919422092728026179uL;
		for (int i = 0; i < 256; i++)
		{
			num += 11400714819323198485uL;
			ulong num2 = num;
			num2 = (num2 ^ (num2 >> 30)) * 13787848793156543929uL;
			num2 = (num2 ^ (num2 >> 27)) * 10723151780598845931uL;
			array[i] = num2 ^ (num2 >> 31);
		}
		return array;
	}

	public static int Cut(ReadOnlySpan<byte> data)
	{
		int num = data.Length;
		if (num <= 16384)
		{
			return num;
		}
		if (num > 262144)
		{
			num = 262144;
		}
		ulong num2 = 0uL;
		int i = 16384;
		for (int num3 = Math.Min(65536, num); i < num3; i++)
		{
			num2 = (num2 << 1) + Gear[data[i]];
			if ((num2 & 0xFFFFC00000000000uL) == 0L)
			{
				return i + 1;
			}
		}
		for (; i < num; i++)
		{
			num2 = (num2 << 1) + Gear[data[i]];
			if ((num2 & 0xFFFC000000000000uL) == 0L)
			{
				return i + 1;
			}
		}
		return num;
	}

	public static void Split(Stream s, ChunkHandler onChunk, CancellationToken ct = default(CancellationToken))
	{
		byte[] array = new byte[1048576];
		int num = 0;
		int i = 0;
		long num2 = 0L;
		bool flag = false;
		while (true)
		{
			if (!flag && i - num < 262144)
			{
				if (num > 0)
				{
					Buffer.BlockCopy(array, num, array, 0, i - num);
					i -= num;
					num = 0;
				}
				int num3;
				for (; i < array.Length; i += num3)
				{
					num3 = s.Read(array, i, array.Length - i);
					if (num3 <= 0)
					{
						flag = true;
						break;
					}
				}
			}
			if (i == num)
			{
				break;
			}
			ct.ThrowIfCancellationRequested();
			int num4 = Cut(new ReadOnlySpan<byte>(array, num, i - num));
			onChunk(num2, new ReadOnlySpan<byte>(array, num, num4));
			num += num4;
			num2 += num4;
		}
	}

	public static string H(ReadOnlySpan<byte> data)
	{
		Span<byte> destination = stackalloc byte[32];
		SHA256.HashData(data, destination);
		return Convert.ToHexString(destination.Slice(0, 16)).ToLowerInvariant();
	}
}

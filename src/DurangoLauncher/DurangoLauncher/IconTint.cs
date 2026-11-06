using System;
using System.Collections.Generic;

namespace DurangoLauncher;

public static class IconTint
{
	public const string Mono = "mono";

	public const string Rgb = "rgb";

	public const string None = "none";

	public static string Normalize(string? tint)
	{
		if (!(tint == "mono"))
		{
			if (tint == "rgb")
			{
				return "rgb";
			}
			return "none";
		}
		return "mono";
	}

	public static bool TryHex(string? s, out uint rgb)
	{
		rgb = 0u;
		if (s == null)
		{
			return false;
		}
		ReadOnlySpan<char> readOnlySpan = s.AsSpan().Trim();
		if (readOnlySpan.Length > 0 && readOnlySpan[0] == '#')
		{
			readOnlySpan = readOnlySpan.Slice(1, readOnlySpan.Length - 1);
		}
		if (readOnlySpan.Length != 6)
		{
			return false;
		}
		ReadOnlySpan<char> readOnlySpan2 = readOnlySpan;
		for (int i = 0; i < readOnlySpan2.Length; i++)
		{
			char c = readOnlySpan2[i];
			int num = ((c >= '0' && c <= '9') ? (c - 48) : ((c >= 'a' && c <= 'f') ? (c - 97 + 10) : ((c >= 'A' && c <= 'F') ? (c - 65 + 10) : (-1))));
			if (num < 0)
			{
				rgb = 0u;
				return false;
			}
			rgb = (rgb << 4) | (uint)num;
		}
		return true;
	}

	public static uint[]? ParseColors(IReadOnlyList<string?>? hex)
	{
		if (hex == null || hex.Count != 3)
		{
			return null;
		}
		uint[] array = new uint[3];
		for (int i = 0; i < 3; i++)
		{
			if (!TryHex(hex[i], out array[i]))
			{
				return null;
			}
		}
		return array;
	}

	public static string Effective(string? tint, uint[]? colors)
	{
		if (colors == null || colors.Length != 3)
		{
			return "none";
		}
		return Normalize(tint);
	}

	public static string Key(string icon, string? tint, uint[]? colors)
	{
		string text = Effective(tint, colors);
		if (!(text == "none"))
		{
			return $"{icon}|{text}|{colors[0]:x6}{colors[1]:x6}{colors[2]:x6}";
		}
		return icon;
	}

	public static uint[] Apply(uint[] argb, string? tint, uint[]? colors)
	{
		string text = Effective(tint, colors);
		if (text == "none")
		{
			return argb;
		}
		uint[] array = new uint[argb.Length];
		if (text == "mono")
		{
			uint num = (uint)((Q((float)((Ch(colors[0], 16) + Ch(colors[1], 16) + Ch(colors[2], 16)) / 3.0)) << 16) | (Q((float)((Ch(colors[0], 8) + Ch(colors[1], 8) + Ch(colors[2], 8)) / 3.0)) << 8) | Q((float)((Ch(colors[0], 0) + Ch(colors[1], 0) + Ch(colors[2], 0)) / 3.0)));
			for (int i = 0; i < argb.Length; i++)
			{
				float num2 = (float)(argb[i] >> 24) / 255f;
				array[i] = (uint)(Q(num2 * num2) << 24) | num;
			}
			return array;
		}
		float num3 = (float)Ch(colors[0], 16);
		float num4 = (float)Ch(colors[0], 8);
		float num5 = (float)Ch(colors[0], 0);
		float num6 = (float)Ch(colors[1], 16);
		float num7 = (float)Ch(colors[1], 8);
		float num8 = (float)Ch(colors[1], 0);
		float num9 = (float)Ch(colors[2], 16);
		float num10 = (float)Ch(colors[2], 8);
		float num11 = (float)Ch(colors[2], 0);
		for (int j = 0; j < argb.Length; j++)
		{
			uint num12 = argb[j];
			float num13 = (float)((num12 >> 16) & 0xFF) / 255f;
			float num14 = (float)((num12 >> 8) & 0xFF) / 255f;
			float num15 = (float)(num12 & 0xFF) / 255f;
			float v = (float)(num12 >> 24) / 255f;
			array[j] = (uint)((Q(v) << 24) | (Q(Clamp01(num13 * num3 + num14 * num6 + num15 * num9)) << 16) | (Q(Clamp01(num13 * num4 + num14 * num7 + num15 * num10)) << 8) | Q(Clamp01(num13 * num5 + num14 * num8 + num15 * num11)));
		}
		return array;
	}

	private static double Ch(uint rgb, int shift)
	{
		return (double)((rgb >> shift) & 0xFF) / 255.0;
	}

	private static float Clamp01(float v)
	{
		if (!(v < 0f))
		{
			if (!(v > 1f))
			{
				return v;
			}
			return 1f;
		}
		return 0f;
	}

	private static byte Q(float v)
	{
		return (byte)(int)(v * 255f + 0.5f);
	}
}

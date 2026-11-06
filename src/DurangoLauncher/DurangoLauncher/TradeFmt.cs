using System;
using System.Globalization;
using System.Linq;

namespace DurangoLauncher;

public static class TradeFmt
{
	public static string N(long n)
	{
		return n.ToString("N0", CultureInfo.InvariantCulture);
	}

	public static string T(long n)
	{
		return N(n) + " T";
	}

	public static string Span(long seconds)
	{
		if (seconds < 60)
		{
			return L.T("ไม\u0e48ถ\u0e36งนาท\u0e35", "under a minute");
		}
		if (seconds < 3600)
		{
			return L.T($"{seconds / 60} นาท\u0e35", $"{seconds / 60} min");
		}
		if (seconds < 172800)
		{
			return L.T($"{seconds / 3600} ชม.", $"{seconds / 3600} hr");
		}
		return L.T($"{seconds / 86400} ว\u0e31น", $"{seconds / 86400} days");
	}

	public static string ExpiresIn(long expiresAt, long now)
	{
		long num = expiresAt - now;
		if (num > 0)
		{
			return L.T("หมดอาย\u0e38ใน ", "Expires in ") + Span(num);
		}
		return L.T("หมดอาย\u0e38แล\u0e49ว", "Expired");
	}

	public static string Left(long expiresAt, long now)
	{
		if (expiresAt > 0)
		{
			if (expiresAt - now > 0)
			{
				return Span(expiresAt - now);
			}
			return L.T("หมดเวลา", "Ended");
		}
		return "—";
	}

	public static string Presence(TradeCharacter c, long now)
	{
		if (c.Online)
		{
			return L.T("ออนไลน\u0e4c", "Online") + ((c.IslandName.Length > 0) ? (" · " + c.IslandName) : "");
		}
		if (c.DisconnectedAt > 0 && now > c.DisconnectedAt)
		{
			return L.T("ออฟไลน\u0e4c ", "Offline ") + Span(now - c.DisconnectedAt);
		}
		return L.T("ออฟไลน\u0e4c", "Offline");
	}

	public static string Clock(long unix, long now)
	{
		if (unix <= 0)
		{
			return "";
		}
		DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
		DateTimeOffset dateTimeOffset2 = DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime();
		string text = dateTimeOffset.ToString("HH:mm", CultureInfo.InvariantCulture);
		if (dateTimeOffset.Date == dateTimeOffset2.Date)
		{
			return text;
		}
		if (dateTimeOffset.Date == dateTimeOffset2.Date.AddDays(-1.0))
		{
			return L.T("เม\u0e37\u0e48อวาน ", "Yesterday ") + text;
		}
		string[] array = new string[12]
		{
			"ม.ค.", "ก.พ.", "ม\u0e35.ค.", "เม.ย.", "พ.ค.", "ม\u0e34.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.",
			"พ.ย.", "ธ.ค."
		};
		string[] array2 = new string[12]
		{
			"Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct",
			"Nov", "Dec"
		};
		return L.T($"{dateTimeOffset.Day} {array[dateTimeOffset.Month - 1]} {text}", $"{array2[dateTimeOffset.Month - 1]} {dateTimeOffset.Day} {text}");
	}

	public static long NowUnix()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	public static string Pct(double rate)
	{
		return (rate * 100.0).ToString((rate * 100.0 % 1.0 == 0.0) ? "0" : "0.#", CultureInfo.InvariantCulture) + "%";
	}

	public static long ParseT(string? s)
	{
		string text = new string((s ?? "").Where(char.IsDigit).ToArray());
		if (text.Length != 0)
		{
			if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			{
				return 0L;
			}
			return Math.Min(result, 999999999999L);
		}
		return 0L;
	}
}

using System;
using System.Globalization;

namespace DurangoLauncher;

public static class CalFmt
{
	private static readonly string[] ThMonths = new string[12]
	{
		"มกราคม", "ก\u0e38มภาพ\u0e31นธ\u0e4c", "ม\u0e35นาคม", "เมษายน", "พฤษภาคม", "ม\u0e34ถ\u0e38นายน", "กรกฎาคม", "ส\u0e34งหาคม", "ก\u0e31นยายน", "ต\u0e38ลาคม",
		"พฤศจ\u0e34กายน", "ธ\u0e31นวาคม"
	};

	private static readonly string[] EnMonths = new string[12]
	{
		"January", "February", "March", "April", "May", "June", "July", "August", "September", "October",
		"November", "December"
	};

	public static readonly string[] ThDays = new string[7] { "จ\u0e31นทร\u0e4c", "อ\u0e31งคาร", "พ\u0e38ธ", "พฤห\u0e31สบด\u0e35", "ศ\u0e38กร\u0e4c", "เสาร\u0e4c", "อาท\u0e34ตย\u0e4c" };

	public static readonly string[] EnDays = new string[7] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

	public static string Month(MonthGrid g)
	{
		return L.T($"{ThMonths[g.Month - 1]} {g.Year}", $"{EnMonths[g.Month - 1]} {g.Year}");
	}

	public static string Weekday(int i)
	{
		if (!L.En)
		{
			return ThDays[i];
		}
		return EnDays[i];
	}

	public static string Span(double seconds)
	{
		long num = (long)Math.Max(0.0, Math.Floor(seconds));
		long num2 = num / 3600;
		long num3 = num2 / 24;
		if (num3 >= 2)
		{
			return L.T($"{num3} ว\u0e31น {num2 % 24} ชม.", $"{num3} days {num2 % 24} hr");
		}
		if (num2 >= 1)
		{
			return L.T($"{num2} ชม. {num % 3600 / 60} นาท\u0e35", $"{num2} hr {num % 3600 / 60} min");
		}
		return L.T($"{Math.Max(1L, num / 60)} นาท\u0e35", $"{Math.Max(1L, num / 60)} min");
	}

	public static string Status(CalStatus s)
	{
		return s switch
		{
			CalStatus.Upcoming => L.T("ย\u0e31งไม\u0e48เร\u0e34\u0e48ม", "Not started"), 
			CalStatus.Live => L.T("กำล\u0e31งจ\u0e31ด", "Live"), 
			_ => L.T("จบแล\u0e49ว", "Ended"), 
		};
	}

	public static string Countdown(CalEvent e, double now, TimeSpan off)
	{
		return e.StatusAt(now) switch
		{
			CalStatus.Upcoming => L.T("เร\u0e34\u0e48มใน ", "Starts in ") + Span((double)e.Start - now), 
			CalStatus.Live => L.T("เหล\u0e37ออ\u0e35ก ", "Ends in ") + Span((double)e.End - now), 
			_ => L.T("จบเม\u0e37\u0e48อ ", "Ended ") + Dm(e.End, off) + " " + Hm(e.End, off), 
		};
	}

	public static string StatusLine(CalEvent e, double now, TimeSpan off)
	{
		CalStatus calStatus = e.StatusAt(now);
		if (calStatus != CalStatus.Ended)
		{
			return Status(calStatus) + " · " + Countdown(e, now, off);
		}
		return Status(calStatus);
	}

	private static DateTimeOffset At(long unix, TimeSpan off)
	{
		return DateTimeOffset.FromUnixTimeSeconds(unix).ToOffset(off);
	}

	public static string Hm(long unix, TimeSpan off)
	{
		return At(unix, off).ToString("HH:mm", CultureInfo.InvariantCulture);
	}

	public static string Hms(double unix, TimeSpan off)
	{
		return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(unix * 1000.0)).ToOffset(off).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
	}

	public static string Dm(long unix, TimeSpan off)
	{
		DateTimeOffset dateTimeOffset = At(unix, off);
		return $"{dateTimeOffset.Day}/{dateTimeOffset.Month:00}";
	}

	public static string Dmy(MonthGrid g, int day)
	{
		return $"{day}/{g.Month:00}/{g.Year}";
	}

	public static string When(CalEvent e, TimeSpan off)
	{
		if (!e.AllDay)
		{
			return $"{Dm(e.Start, off)} {Hm(e.Start, off)} – {Dm(e.End, off)} {Hm(e.End, off)}";
		}
		return Dm(e.Start, off) + " – " + Dm(Math.Max(e.Start, e.End - 1), off) + " · " + L.T("ท\u0e31\u0e49งว\u0e31น", "all day");
	}

	public static string ListTime(CalEvent e, TimeSpan off)
	{
		if (!e.AllDay)
		{
			return Hm(e.Start, off);
		}
		return L.T("ท\u0e31\u0e49งว\u0e31น", "All day");
	}

	public static string Tz(TimeSpan off)
	{
		if (!(off == TimeSpan.Zero))
		{
			return "UTC" + ((off < TimeSpan.Zero) ? "-" : "+") + Math.Abs(off.Hours) + ((off.Minutes != 0) ? (":" + Math.Abs(off.Minutes).ToString("00")) : "");
		}
		return "UTC";
	}

	public static string Count(int n)
	{
		return L.T($"{n} อ\u0e35เวนต\u0e4c", (n == 1) ? "1 event" : $"{n} events");
	}
}

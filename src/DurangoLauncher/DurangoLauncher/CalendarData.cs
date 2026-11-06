using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record CalendarData(double Now, TimeSpan Offset, long From, long Until, int? ResetHour, CalEvent[] Events)
{
	public static readonly TimeSpan DefaultOffset = TimeSpan.FromHours(7);

	public static CalendarData? Parse(JsonElement r)
	{
		if (r.ValueKind != JsonValueKind.Object)
		{
			return null;
		}
		if (r.TryGetProperty("ok", out var value) && value.ValueKind == JsonValueKind.False)
		{
			return null;
		}
		if (!r.TryGetProperty("now", out var value2) || value2.ValueKind != JsonValueKind.Number || !value2.TryGetDouble(out var value3) || !double.IsFinite(value3) || value3 <= 0.0)
		{
			return null;
		}
		if (!r.TryGetProperty("events", out var value4) || value4.ValueKind != JsonValueKind.Array)
		{
			return null;
		}
		TimeSpan offset = DefaultOffset;
		if (r.TryGetProperty("timezone", out var value5) && value5.ValueKind == JsonValueKind.Number && value5.TryGetDouble(out var value6) && double.IsFinite(value6) && Math.Abs(value6) <= 14.0)
		{
			offset = TimeSpan.FromMinutes(Math.Round(value6 * 60.0));
		}
		int? resetHour = null;
		if (r.TryGetProperty("dailyReset", out var value7) && value7.ValueKind == JsonValueKind.Object)
		{
			int? num = J.IntN(value7, "hour");
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				switch (valueOrDefault)
				{
				case 0:
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				case 13:
				case 14:
				case 15:
				case 16:
				case 17:
				case 18:
				case 19:
				case 20:
				case 21:
				case 22:
				case 23:
					resetHour = valueOrDefault;
					break;
				}
			}
		}
		CalEvent[] events = (from x in (from x in value4.EnumerateArray()
				where x.ValueKind == JsonValueKind.Object
				select x).Select(CalEvent.Parse)
			orderby x.Start
			select x).ToArray();
		return new CalendarData(value3, offset, CalEvent.Sec(r, "from"), CalEvent.Sec(r, "until"), resetHour, events);
	}

	public DateTimeOffset Local(double unix)
	{
		return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(unix * 1000.0)).ToOffset(Offset);
	}

	public MonthGrid Grid()
	{
		DateTimeOffset dateTimeOffset = Local((From > 0) ? ((double)From) : Now);
		return new MonthGrid(dateTimeOffset.Year, dateTimeOffset.Month, Offset);
	}

	public int LiveCount(double now)
	{
		return Events.Count((CalEvent e) => e.StatusAt(now) == CalStatus.Live);
	}

	[CompilerGenerated]
	private CalendarData(CalendarData original)
	{
		Now = original.Now;
		Offset = original.Offset;
		From = original.From;
		Until = original.Until;
		ResetHour = original.ResetHour;
		Events = original.Events;
	}
}

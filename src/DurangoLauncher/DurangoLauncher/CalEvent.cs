using System;
using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record CalEvent(string Id, string Name, string Icon, string Place, string Description, long Start, long End, bool Manual, bool AllDay, string[] Guide)
{
	public static CalEvent Parse(JsonElement e)
	{
		return new CalEvent(J.Str(e, "id"), J.Str(e, "name"), J.Str(e, "icon"), J.Str(e, "place"), J.Str(e, "description"), Sec(e, "start"), Sec(e, "end"), J.Bool(e, "manual"), J.Bool(e, "allDay"), J.StrArr(e, "guide"));
	}

	internal static long Sec(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var value2) || !double.IsFinite(value2))
		{
			return J.Long(e, k, 0L);
		}
		return (long)Math.Round(value2);
	}

	public bool OnDay(long dayStart)
	{
		if (Start < dayStart + 86400)
		{
			return End > dayStart;
		}
		return false;
	}

	public CalStatus StatusAt(double now)
	{
		if (!(now < (double)Start))
		{
			if (!(now < (double)End))
			{
				return CalStatus.Ended;
			}
			return CalStatus.Live;
		}
		return CalStatus.Upcoming;
	}

	public CalGuideStep[] Steps()
	{
		return (from g in Guide
			select (from s in g.Replace("\r\n", "\n").Split('\n')
				select s.Trim() into s
				where s.Length > 0
				select s).ToArray() into p
			where p.Length != 0
			select new CalGuideStep(p[0], p[1..])).ToArray();
	}
}

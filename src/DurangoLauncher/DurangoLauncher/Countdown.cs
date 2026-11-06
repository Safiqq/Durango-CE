using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DurangoLauncher;

public sealed record Countdown(DateTimeOffset At, string Label, string LabelEn, string Done, string DoneEn, double HideAfterHours, string Tag = "", string TagEn = "", string Title = "", string TitleEn = "", string TitleDone = "", string TitleDoneEn = "")
{
	public enum Phase
	{
		Counting,
		Done,
		Hidden
	}

	public const double DefaultHideAfterHours = 72.0;

	private static readonly TimeSpan Thailand = TimeSpan.FromHours(7);

	private static readonly Regex HasZone = new Regex("(?:[zZ]|[+-]\\d{2}:?\\d{2})\\s*$");

	private static readonly string[] ThaiDays = new string[7] { "อา.", "จ.", "อ.", "พ.", "พฤ.", "ศ.", "ส." };

	private static readonly string[] ThaiMonths = new string[12]
	{
		"ม.ค.", "ก.พ.", "ม\u0e35.ค.", "เม.ย.", "พ.ค.", "ม\u0e34.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.",
		"พ.ย.", "ธ.ค."
	};

	public Phase PhaseAt(DateTimeOffset now)
	{
		if (!(now < At))
		{
			if (!(now < At + TimeSpan.FromHours(Math.Max(0.0, HideAfterHours))))
			{
				return Phase.Hidden;
			}
			return Phase.Done;
		}
		return Phase.Counting;
	}

	public TimeSpan Left(DateTimeOffset now)
	{
		if (!(At > now))
		{
			return TimeSpan.Zero;
		}
		return At - now;
	}

	public string LabelText(bool en)
	{
		return Pick(en, Label, LabelEn);
	}

	public string DoneText(bool en)
	{
		return Pick(en, Done, DoneEn);
	}

	public string TagText(bool en)
	{
		return Pick(en, Tag, TagEn);
	}

	public string TitleText(bool en, bool done)
	{
		if (!done)
		{
			return Pick(en, Title, TitleEn);
		}
		string text = Pick(en, TitleDone, TitleDoneEn);
		if (text == null || text.Length <= 0)
		{
			return Pick(en, Title, TitleEn);
		}
		return text;
	}

	private static string Pick(bool en, string th, string enText)
	{
		if (!en || enText.Length <= 0)
		{
			return th;
		}
		return enText;
	}

	public static Countdown? Parse(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("countdown", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return null;
		}
		if (!TryParseAt(Str(value, "at"), out var at))
		{
			return null;
		}
		double hideAfterHours = 72.0;
		if (value.TryGetProperty("hide_after_hours", out var value2) && value2.ValueKind == JsonValueKind.Number && value2.TryGetDouble(out var value3))
		{
			hideAfterHours = Math.Max(0.0, value3);
		}
		string text = Str(value, "label");
		string text2 = Str(value, "done");
		DateTimeOffset at2 = at;
		string label = ((text.Length > 0) ? text : "น\u0e31บถอยหล\u0e31ง");
		string text3 = Str(value, "label_en");
		string labelEn = ((text3 != null && text3.Length > 0) ? text3 : ((text.Length > 0) ? "" : "Countdown"));
		string done = ((text2.Length > 0) ? text2 : "เป\u0e34ดแล\u0e49ว");
		string text4 = Str(value, "done_en");
		return new Countdown(at2, label, labelEn, done, (text4 != null && text4.Length > 0) ? text4 : ((text2.Length > 0) ? "" : "Live now"), hideAfterHours, Str(value, "tag"), Str(value, "tag_en"), Str(value, "title"), Str(value, "title_en"), Str(value, "title_done"), Str(value, "title_done_en"));
	}

	public static bool TryParseAt(string s, out DateTimeOffset at)
	{
		at = default(DateTimeOffset);
		s = s.Trim();
		if (s.Length == 0)
		{
			return false;
		}
		if (HasZone.IsMatch(s))
		{
			return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out at);
		}
		if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			return false;
		}
		at = new DateTimeOffset(DateTime.SpecifyKind(result, DateTimeKind.Unspecified), Thailand);
		return true;
	}

	public static (int Days, int Hours, int Minutes, int Seconds) Split(TimeSpan left)
	{
		long num = Math.Max(0L, (long)Math.Floor(left.TotalSeconds));
		return (Days: (int)(num / 86400), Hours: (int)(num % 86400 / 3600), Minutes: (int)(num % 3600 / 60), Seconds: (int)(num % 60));
	}

	public static string Short(TimeSpan left, bool en)
	{
		var (num, value, value2, value3) = Split(left);
		if (num > 0)
		{
			if (en)
			{
				return $"{num}d {value}h";
			}
			return $"{num} ว\u0e31น {value} ชม.";
		}
		return $"{value:00}:{value2:00}:{value3:00}";
	}

	public string WhenText(bool en)
	{
		DateTimeOffset at = At;
		string text = Zone(at.Offset);
		if (en)
		{
			return $"{at.ToString("ddd, MMM d, yyyy", CultureInfo.InvariantCulture)} · {at:HH:mm} ({text})";
		}
		string text2 = $"{ThaiDays[(int)at.DayOfWeek]} {at.Day} {ThaiMonths[at.Month - 1]} {at.Year} · {at:HH:mm} น.";
		if (!(at.Offset == Thailand))
		{
			return text2 + " (" + text + ")";
		}
		return text2;
	}

	private static string Zone(TimeSpan off)
	{
		if (off == TimeSpan.Zero)
		{
			return "UTC";
		}
		string value = ((off < TimeSpan.Zero) ? "-" : "+");
		TimeSpan timeSpan = off.Duration();
		if (timeSpan.Minutes == 0)
		{
			return $"UTC{value}{timeSpan.Hours}";
		}
		return $"UTC{value}{timeSpan.Hours}:{timeSpan.Minutes:00}";
	}

	private static string Str(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return (value.GetString() ?? "").Trim();
	}

	[CompilerGenerated]
	private Countdown(Countdown original)
	{
		At = original.At;
		Label = original.Label;
		LabelEn = original.LabelEn;
		Done = original.Done;
		DoneEn = original.DoneEn;
		HideAfterHours = original.HideAfterHours;
		Tag = original.Tag;
		TagEn = original.TagEn;
		Title = original.Title;
		TitleEn = original.TitleEn;
		TitleDone = original.TitleDone;
		TitleDoneEn = original.TitleDoneEn;
	}
}

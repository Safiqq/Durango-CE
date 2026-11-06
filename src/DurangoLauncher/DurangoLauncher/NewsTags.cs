using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace DurangoLauncher;

public static class NewsTags
{
	private static readonly (NewsKind Kind, string[] Words)[] Known = new(NewsKind, string[])[4]
	{
		(NewsKind.Update, new string[7] { "อ\u0e31ปเดต", "อ\u0e31พเดต", "อ\u0e31พเดท", "อ\u0e31ปเดท", "update", "patch", "แพตช\u0e4c" }),
		(NewsKind.System, new string[4] { "ระบบ", "system", "feature", "ฟ\u0e35เจอร\u0e4c" }),
		(NewsKind.Balance, new string[3] { "สมด\u0e38ล", "ปร\u0e31บสมด\u0e38ล", "balance" }),
		(NewsKind.Event, new string[4] { "ก\u0e34จกรรม", "อ\u0e35เวนต\u0e4c", "อ\u0e35เว\u0e49นท\u0e4c", "event" })
	};

	private static readonly Regex Bracket = new Regex("^\\s*[\\[【(]\\s*([^\\]】)]{1,20}?)\\s*[\\]】)]\\s*[—–:\\-·|]*\\s*");

	private static readonly Regex Prefix = new Regex("^\\s*([^\\s:—–\\-·|]{1,20})\\s*[:—–\\-·|]+\\s*");

	private static readonly Regex Version = new Regex("^\\s*(?:client\\s+)?v(\\d+(?:\\.\\d+)*)\\s*[—–:\\-·]*\\s*", RegexOptions.IgnoreCase);

	private static readonly Regex FirstWord = new Regex("^\\s*(\\S{1,20})\\s");

	public static NewsKind KindOf(string word)
	{
		string w = word.Trim();
		(NewsKind, string[])[] known = Known;
		for (int i = 0; i < known.Length; i++)
		{
			(NewsKind, string[]) tuple = known[i];
			var (result, _) = tuple;
			if (tuple.Item2.Any((string x) => string.Equals(x, w, StringComparison.OrdinalIgnoreCase)))
			{
				return result;
			}
		}
		return NewsKind.Other;
	}

	public static string Chip(NewsKind kind, bool en)
	{
		return kind switch
		{
			NewsKind.Update => en ? "Update" : "อ\u0e31ปเดต", 
			NewsKind.System => en ? "System" : "ระบบ", 
			NewsKind.Balance => en ? "Balance" : "สมด\u0e38ล", 
			NewsKind.Event => en ? "Event" : "ก\u0e34จกรรม", 
			_ => en ? "News" : "ข\u0e48าวสาร", 
		};
	}

	public static string Icon(NewsKind kind)
	{
		return kind switch
		{
			NewsKind.Update => "download", 
			NewsKind.System => "settings", 
			NewsKind.Balance => "scale", 
			NewsKind.Event => "calendar", 
			_ => "news", 
		};
	}

	public static (NewsKind Kind, string Chip, string Title) Classify(string title, string tag, bool en)
	{
		title = title.Trim();
		if (tag.Trim().Length > 0)
		{
			NewsKind newsKind = KindOf(tag);
			string item = Classify(title, "", en).Title;
			return (Kind: newsKind, Chip: (newsKind == NewsKind.Other) ? tag.Trim() : Chip(newsKind, en), Title: item);
		}
		Match match = Bracket.Match(title);
		if (match.Success)
		{
			NewsKind newsKind2 = KindOf(match.Groups[1].Value);
			if (newsKind2 != NewsKind.Other)
			{
				return (Kind: newsKind2, Chip: Chip(newsKind2, en), Title: Rest(title, match));
			}
		}
		match = Version.Match(title);
		if (match.Success)
		{
			string text = Rest(title, match);
			return (Kind: NewsKind.Update, Chip: Chip(NewsKind.Update, en), Title: (text.Length > 0 && text != title) ? (match.Groups[1].Value + " · " + text) : title);
		}
		match = Prefix.Match(title);
		if (match.Success)
		{
			NewsKind newsKind3 = KindOf(match.Groups[1].Value);
			if (newsKind3 != NewsKind.Other)
			{
				return (Kind: newsKind3, Chip: Chip(newsKind3, en), Title: Rest(title, match));
			}
		}
		match = FirstWord.Match(title);
		if (match.Success)
		{
			NewsKind newsKind4 = KindOf(match.Groups[1].Value);
			if (newsKind4 != NewsKind.Other)
			{
				return (Kind: newsKind4, Chip: Chip(newsKind4, en), Title: title);
			}
		}
		return (Kind: NewsKind.Other, Chip: Chip(NewsKind.Other, en), Title: title);
	}

	private static string Rest(string title, Match m)
	{
		int length = m.Length;
		string text = title.Substring(length, title.Length - length).Trim();
		if (text.Length <= 0)
		{
			return title;
		}
		return text;
	}
}

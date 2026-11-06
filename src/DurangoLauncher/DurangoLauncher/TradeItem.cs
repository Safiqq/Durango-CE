using System;
using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record TradeItem(string Id, string Proto, string NameTh, string NameEn, string Icon, int Count, int? Level, int? Durability, int? MaxDurability, string Group, string[] Tags, string? Held, bool Tradable, long? ListedPrice = null, string IconTint = "none", uint[]? Colors = null)
{
	public string Tint => DurangoLauncher.IconTint.Effective(IconTint, Colors);

	public string Name
	{
		get
		{
			if (!L.En)
			{
				return First(NameTh, NameEn, Proto);
			}
			return First(NameEn, NameTh, Proto);
		}
	}

	public bool IsEquip
	{
		get
		{
			if (!Level.HasValue)
			{
				return MaxDurability.HasValue;
			}
			return true;
		}
	}

	public double? DurabilityFrac
	{
		get
		{
			int? durability = Durability;
			if (durability.HasValue)
			{
				int valueOrDefault = durability.GetValueOrDefault();
				durability = MaxDurability;
				if (durability.HasValue)
				{
					int valueOrDefault2 = durability.GetValueOrDefault();
					if (valueOrDefault2 > 0)
					{
						return Math.Clamp((double)valueOrDefault / (double)valueOrDefault2, 0.0, 1.0);
					}
				}
			}
			return null;
		}
	}

	public string TagLine => string.Join(" · ", Tags);

	private static string First(string a, string b, string c)
	{
		if (a.Length <= 0)
		{
			if (b.Length <= 0)
			{
				return c;
			}
			return b;
		}
		return a;
	}

	public static TradeItem Parse(JsonElement e)
	{
		(string Th, string En) tuple = J.Name(e, "name");
		string item = tuple.Th;
		string item2 = tuple.En;
		string id = J.Str(e, "id");
		string proto = J.Str(e, "proto");
		string icon = J.Str(e, "icon");
		int count = J.Int(e, "count", 1);
		int? level = J.IntN(e, "level");
		int? durability = J.IntN(e, "durability");
		int? maxDurability = J.IntN(e, "max_durability");
		string text = J.Str(e, "group", "other");
		string[] tags = J.StrArr(e, "tags");
		string text2 = J.Str(e, "held");
		string held = ((text2 != null && text2.Length > 0) ? text2 : null);
		JsonElement value;
		bool tradable = !e.TryGetProperty("tradable", out value) || value.ValueKind != JsonValueKind.False;
		long? num = J.LongN(e, "listed_price");
		long? listedPrice;
		if (num.HasValue)
		{
			long valueOrDefault = num.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				listedPrice = valueOrDefault;
				goto IL_010b;
			}
		}
		listedPrice = null;
		goto IL_010b;
		IL_010b:
		return new TradeItem(id, proto, item, item2, icon, count, level, durability, maxDurability, text, tags, held, tradable, listedPrice, DurangoLauncher.IconTint.Normalize(J.Str(e, "icon_tint")), ColorsOf(e));
	}

	private static uint[]? ColorsOf(JsonElement e)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("colors", out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
		{
			return null;
		}
		return DurangoLauncher.IconTint.ParseColors((from x in value.EnumerateArray()
			select (x.ValueKind != JsonValueKind.String) ? null : x.GetString()).ToArray());
	}

	public static TradeItem[] ParseList(JsonElement e, string key)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return Array.Empty<TradeItem>();
		}
		return (from x in value.EnumerateArray()
			where x.ValueKind == JsonValueKind.Object
			select x).Select(Parse).ToArray();
	}
}

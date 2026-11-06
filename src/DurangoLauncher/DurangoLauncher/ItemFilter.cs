using System;
using System.Collections.Generic;
using System.Linq;

namespace DurangoLauncher;

public static class ItemFilter
{
	public static readonly string[] Chips = new string[6] { "all", "weapon", "clothing", "material", "food", "other" };

	public static string ChipText(string chip)
	{
		return chip switch
		{
			"weapon" => L.T("อาว\u0e38ธ", "Weapons"), 
			"clothing" => L.T("เส\u0e37\u0e49อผ\u0e49า", "Clothing"), 
			"material" => L.T("ว\u0e31สด\u0e38", "Materials"), 
			"food" => L.T("อาหาร", "Food"), 
			"other" => L.T("อ\u0e37\u0e48น ๆ", "Other"), 
			_ => L.T("ท\u0e31\u0e49งหมด", "All"), 
		};
	}

	public static string? GroupParam(string chip)
	{
		if (!(chip == "all"))
		{
			return chip;
		}
		return null;
	}

	public static bool InChip(TradeItem it, string chip)
	{
		if (!(chip == "all"))
		{
			if (chip == "clothing")
			{
				string text = it.Group;
				return (text == "clothing" || text == "accessory") ? true : false;
			}
			return it.Group == chip;
		}
		return true;
	}

	public static TradeItem[] Apply(IEnumerable<TradeItem> items, string chip, string? query)
	{
		string q = (query ?? "").Trim();
		return items.Where((TradeItem i) => InChip(i, chip) && (q.Length == 0 || Match(i, q))).ToArray();
	}

	private static bool Match(TradeItem i, string q)
	{
		if (!i.NameTh.Contains(q, StringComparison.OrdinalIgnoreCase) && !i.NameEn.Contains(q, StringComparison.OrdinalIgnoreCase) && !i.Proto.Contains(q, StringComparison.OrdinalIgnoreCase))
		{
			return i.Tags.Any((string t) => t.Contains(q, StringComparison.OrdinalIgnoreCase));
		}
		return true;
	}
}

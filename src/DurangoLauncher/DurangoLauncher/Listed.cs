using System.Collections.Generic;

namespace DurangoLauncher;

public static class Listed
{
	public static bool NeedMine(TradeItem it)
	{
		if (it.Held == "market")
		{
			return !it.ListedPrice.HasValue;
		}
		return false;
	}

	public static long? Price(TradeItem it, IReadOnlyDictionary<string, long>? fromMine)
	{
		if (!(it.Held != "market"))
		{
			long? listedPrice = it.ListedPrice;
			if (!listedPrice.HasValue)
			{
				if (fromMine == null || !fromMine.TryGetValue(it.Id, out var value) || value <= 0)
				{
					return null;
				}
				return value;
			}
			return listedPrice;
		}
		return null;
	}

	public static string Label(TradeItem it, IReadOnlyDictionary<string, long>? fromMine)
	{
		string text = L.T("ลงตลาดอย\u0e39\u0e48 (จากในเกม)", "Listed (in game)");
		long? num = Price(it, fromMine);
		object obj;
		if (num.HasValue)
		{
			long valueOrDefault = num.GetValueOrDefault();
			obj = " · " + TradeFmt.T(valueOrDefault);
		}
		else
		{
			obj = "";
		}
		return text + (string?)obj;
	}
}

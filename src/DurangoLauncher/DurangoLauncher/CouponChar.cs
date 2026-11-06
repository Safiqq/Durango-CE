using System.Collections.Generic;
using System.Linq;

namespace DurangoLauncher;

public static class CouponChar
{
	public static TradeCharacter? Default(IReadOnlyList<TradeCharacter> chars)
	{
		object obj;
		if (chars.Count != 0)
		{
			obj = chars.FirstOrDefault((TradeCharacter c) => c.Online);
			if (obj == null)
			{
				return chars.OrderByDescending((TradeCharacter c) => c.DisconnectedAt).First();
			}
		}
		else
		{
			obj = null;
		}
		return (TradeCharacter?)obj;
	}

	public static TradeCharacter? Pick(IReadOnlyList<TradeCharacter> chars, string? current)
	{
		if (current != null)
		{
			TradeCharacter tradeCharacter = chars.FirstOrDefault((TradeCharacter c) => c.EntityId == current);
			if ((object)tradeCharacter != null)
			{
				return tradeCharacter;
			}
		}
		return Default(chars);
	}
}

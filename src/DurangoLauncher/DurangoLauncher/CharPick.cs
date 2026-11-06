using System.Collections.Generic;
using System.Linq;

namespace DurangoLauncher;

public static class CharPick
{
	public static TradeCharacter? Pick(IReadOnlyList<TradeCharacter> chars, string? preferredId)
	{
		if (chars.Count == 0)
		{
			return null;
		}
		if (preferredId != null)
		{
			TradeCharacter tradeCharacter = chars.FirstOrDefault((TradeCharacter c) => c.EntityId == preferredId);
			if ((object)tradeCharacter != null)
			{
				return tradeCharacter;
			}
		}
		return chars.FirstOrDefault((TradeCharacter c) => c.Online) ?? chars[0];
	}
}

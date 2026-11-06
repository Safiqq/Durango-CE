using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record CharactersResult(long Coin, TradeCharacter[] Characters)
{
	public static CharactersResult Parse(JsonElement r)
	{
		return new CharactersResult(J.Long(r, "coin", 0L), J.Objs(r, "characters").Select(TradeCharacter.Parse).ToArray());
	}
}

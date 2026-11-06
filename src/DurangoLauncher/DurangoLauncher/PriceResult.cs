using System.Text.Json;

namespace DurangoLauncher;

public sealed record PriceResult(long MinPrice, int Count)
{
	public static PriceResult Parse(JsonElement r)
	{
		return new PriceResult(J.Long(r, "min_price", 0L), J.Int(r, "count"));
	}
}

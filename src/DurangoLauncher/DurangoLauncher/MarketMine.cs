using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record MarketMine(MarketProduct[] Selling, SoldItem[] Sold, int CollectCount, long CollectTotal, long CollectGross, long CollectTax)
{
	public static MarketMine Parse(JsonElement r)
	{
		JsonElement value;
		JsonElement e = ((r.TryGetProperty("collectable", out value) && value.ValueKind == JsonValueKind.Object) ? value : default(JsonElement));
		return new MarketMine(J.Objs(r, "selling").Select(MarketProduct.Parse).ToArray(), J.Objs(r, "sold").Select(SoldItem.Parse).ToArray(), J.Int(e, "count"), J.Long(e, "total", 0L), J.Long(e, "gross", 0L), J.Long(e, "tax", 0L));
	}
}

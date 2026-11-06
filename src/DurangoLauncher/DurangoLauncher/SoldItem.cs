using System;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record SoldItem(string ProductId, TradeItem Item, long Price, long Tax, long SoldAt)
{
	public static SoldItem Parse(JsonElement e)
	{
		JsonElement value;
		return new SoldItem(J.Str(e, "product_id"), (e.TryGetProperty("item", out value) && value.ValueKind == JsonValueKind.Object) ? TradeItem.Parse(value) : new TradeItem("", "", "", "", "", 1, null, null, null, "other", Array.Empty<string>(), null, Tradable: true), J.Long(e, "price", 0L), J.Long(e, "tax", 0L), J.Long(e, "sold_at", 0L));
	}
}

using System;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record MarketProduct(string ProductId, string SellerName, TradeItem Item, long Price, string Currency, int Count, int? Level, int? Durability, long ListedAt, long ExpiresAt, bool Favorite, bool Mine, string State)
{
	public static MarketProduct Parse(JsonElement e)
	{
		JsonElement value;
		TradeItem tradeItem = ((e.TryGetProperty("item", out value) && value.ValueKind == JsonValueKind.Object) ? TradeItem.Parse(value) : new TradeItem("", J.Str(e, "proto"), "", "", J.Str(e, "icon"), J.Int(e, "count", 1), J.IntN(e, "level"), J.IntN(e, "durability"), null, "other", Array.Empty<string>(), null, Tradable: true));
		return new MarketProduct(J.Str(e, "product_id", J.Str(e, "id")), J.Str(e, "seller_name", J.Str(e, "seller")), tradeItem, J.Long(e, "price", 0L), J.Str(e, "currency", "tstone"), J.Int(e, "count", tradeItem.Count), J.IntN(e, "level") ?? tradeItem.Level, J.IntN(e, "durability") ?? tradeItem.Durability, J.Long(e, "listed_at", 0L), J.Long(e, "expires_at", 0L), J.Bool(e, "favorite"), J.Bool(e, "mine"), J.Str(e, "state"));
	}
}

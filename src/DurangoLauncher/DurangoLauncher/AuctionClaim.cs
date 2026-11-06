using System.Text.Json;

namespace DurangoLauncher;

public sealed record AuctionClaim(string Id, string Text, long Tstone, long Gross, long Tax, int Count, TradeItem? Item)
{
	public static AuctionClaim Parse(JsonElement e)
	{
		JsonElement value;
		return new AuctionClaim(J.Str(e, "id", J.Str(e, "claim_id")), J.Str(e, "text"), J.Long(e, "tstone", 0L), J.Long(e, "gross", 0L), J.Long(e, "tax", 0L), J.Int(e, "count", 1), (e.TryGetProperty("item", out value) && value.ValueKind == JsonValueKind.Object) ? TradeItem.Parse(value) : null);
	}
}

using System;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record AuctionLot(string Id, string SellerName, string Proto, int Level, int Count, TradeItem Item, long StartPrice, long Current, long MinBid, int BidCount, long EndsAt, int RemainSec, string State, bool Mine, bool Leading, bool Bidded, string TopName)
{
	public static AuctionLot Parse(JsonElement e)
	{
		JsonElement value;
		TradeItem tradeItem = ((e.TryGetProperty("item", out value) && value.ValueKind == JsonValueKind.Object) ? TradeItem.Parse(value) : new TradeItem("", J.Str(e, "proto"), "", "", J.Str(e, "icon"), J.Int(e, "count", 1), J.IntN(e, "level"), null, null, "other", Array.Empty<string>(), null, Tradable: true));
		return new AuctionLot(J.Str(e, "id", J.Str(e, "lot_id")), J.Str(e, "sellerName", J.Str(e, "seller_name")), J.Str(e, "proto", tradeItem.Proto), J.Int(e, "level", tradeItem.Level.GetValueOrDefault()), J.Int(e, "count", tradeItem.Count), tradeItem, J.Long(e, "startPrice", J.Long(e, "start_price", 0L)), J.Long(e, "current", 0L), J.Long(e, "minBid", J.Long(e, "min_bid", 0L)), J.Int(e, "bidCount", J.Int(e, "bid_count")), J.Long(e, "endsAt", J.Long(e, "ends_at", 0L)), J.Int(e, "remainSec", J.Int(e, "remain_sec")), J.Str(e, "state"), J.Bool(e, "mine"), J.Bool(e, "leading"), J.Bool(e, "bidded"), J.Str(e, "topName", J.Str(e, "top_name")));
	}
}

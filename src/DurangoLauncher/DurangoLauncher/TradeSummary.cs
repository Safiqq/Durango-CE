using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record TradeSummary(string Server, int ChatUnread, int OffersIn, int OffersChanged, int MarketCollectable, long MarketCollectableTotal, TradeSummaryChar[] Characters, bool? Supporter = null, bool? TradeAllowed = null)
{
	public static TradeSummary Parse(JsonElement r)
	{
		int marketCollectable = 0;
		long marketCollectableTotal = 0L;
		if (r.TryGetProperty("market_collectable", out var value))
		{
			int value2;
			if (value.ValueKind == JsonValueKind.Object)
			{
				marketCollectable = J.Int(value, "count");
				marketCollectableTotal = J.Long(value, "total", 0L);
			}
			else if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out value2))
			{
				marketCollectable = value2;
			}
		}
		return new TradeSummary(J.Str(r, "server"), J.Int(r, "chat_unread"), J.Int(r, "offers_in"), J.Int(r, "offers_changed"), marketCollectable, marketCollectableTotal, (from c in J.Objs(r, "characters")
			select new TradeSummaryChar(J.Str(c, "entity_id"), J.Str(c, "name"), J.Int(c, "level"), J.Bool(c, "online"))).ToArray(), J.BoolN(r, "supporter"), J.BoolN(r, "trade_allowed"));
	}
}

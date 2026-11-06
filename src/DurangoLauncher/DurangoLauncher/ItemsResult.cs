using System.Text.Json;

namespace DurangoLauncher;

public sealed record ItemsResult(long SyncedAt, TradeItem[] Bag, TradeItem[] Equipped, int BagMax)
{
	public static ItemsResult Parse(JsonElement r)
	{
		return new ItemsResult(J.Long(r, "synced_at", 0L), TradeItem.ParseList(r, "bag"), TradeItem.ParseList(r, "equipped"), J.Int(r, "bag_max"));
	}
}

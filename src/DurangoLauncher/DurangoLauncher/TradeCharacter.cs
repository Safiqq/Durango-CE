using System.Text.Json;

namespace DurangoLauncher;

public sealed record TradeCharacter(string EntityId, string Name, int Level, bool Online, long DisconnectedAt, string IslandName, long Tstone, int BagUsed, int BagMax, bool Supporter)
{
	public static TradeCharacter Parse(JsonElement e)
	{
		return new TradeCharacter(J.Str(e, "entity_id"), J.Str(e, "name"), J.Int(e, "level"), J.Bool(e, "online"), J.Long(e, "disconnected_at", 0L), J.Str(e, "island_name"), J.Long(e, "tstone", 0L), J.Int(e, "bag_used"), J.Int(e, "bag_max"), J.Bool(e, "supporter"));
	}
}

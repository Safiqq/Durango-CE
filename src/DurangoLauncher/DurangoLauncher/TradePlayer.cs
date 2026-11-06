using System.Text.Json;

namespace DurangoLauncher;

public sealed record TradePlayer(string EntityId, string Name, int Level, bool Online, bool Eligible, string? Why = null)
{
	public static TradePlayer Parse(JsonElement e)
	{
		string entityId = J.Str(e, "entity_id");
		string name = J.Str(e, "name");
		int level = J.Int(e, "level");
		bool online = J.Bool(e, "online");
		JsonElement value;
		bool eligible = !e.TryGetProperty("eligible", out value) || value.ValueKind != JsonValueKind.False;
		string text = J.Str(e, "why");
		return new TradePlayer(entityId, name, level, online, eligible, (text != null && text.Length > 0) ? text : null);
	}
}

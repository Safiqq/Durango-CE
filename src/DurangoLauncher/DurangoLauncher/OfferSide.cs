using System.Text.Json;

namespace DurangoLauncher;

public sealed record OfferSide(string EntityId, string Name, int Level)
{
	public static OfferSide Parse(JsonElement e)
	{
		return new OfferSide(J.Str(e, "entity_id"), J.Str(e, "name"), J.Int(e, "level"));
	}
}

using System.Text.Json;

namespace DurangoLauncher;

public sealed record OfferChatMsg(long Id, long T, string EntityId, string Name, string Text)
{
	public static OfferChatMsg Parse(JsonElement e)
	{
		return new OfferChatMsg(J.Long(e, "id", 0L), J.Long(e, "t", 0L), J.Str(e, "entity_id"), J.Str(e, "name"), J.Str(e, "text"));
	}
}

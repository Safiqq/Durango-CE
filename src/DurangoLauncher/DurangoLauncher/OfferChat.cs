using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record OfferChat(bool Closed, OfferChatMsg[] Messages)
{
	public static OfferChat Parse(JsonElement r)
	{
		return new OfferChat(J.Bool(r, "closed"), J.Objs(r, "messages").Select(OfferChatMsg.Parse).ToArray());
	}
}

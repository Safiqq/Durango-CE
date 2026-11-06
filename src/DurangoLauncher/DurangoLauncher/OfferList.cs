using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record OfferList(TradeOffer[] Offers, long Now)
{
	public static OfferList Parse(JsonElement r)
	{
		return new OfferList(J.Objs(r, "offers").Select(TradeOffer.Parse).ToArray(), J.Long(r, "now", 0L));
	}
}

using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record AuctionResult(AuctionLot[] Lots, AuctionClaim[] Claims, int Total)
{
	public static AuctionResult Parse(JsonElement r)
	{
		AuctionLot[] array = J.Objs(r, "lots").Concat(J.Objs(r, "products")).Select(AuctionLot.Parse)
			.ToArray();
		if (array.Length == 0 && r.TryGetProperty("lot", out var value) && value.ValueKind == JsonValueKind.Object)
		{
			array = new AuctionLot[1] { AuctionLot.Parse(value) };
		}
		return new AuctionResult(array, J.Objs(r, "claims").Select(AuctionClaim.Parse).ToArray(), J.Int(r, "total", array.Length));
	}
}

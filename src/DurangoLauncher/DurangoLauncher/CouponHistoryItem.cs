using System.Text.Json;

namespace DurangoLauncher;

public sealed record CouponHistoryItem(string Kind, string Code, string Name, string ToName, long CoinAdded, long At, string Src)
{
	public bool IsCoin => Kind == "coin";

	public static CouponHistoryItem Parse(JsonElement e)
	{
		return new CouponHistoryItem(J.Str(e, "kind", "reward"), J.Str(e, "code"), J.Str(e, "name"), J.Str(e, "to_name"), J.Long(e, "coin_added", J.Long(e, "coin", 0L)), J.Long(e, "at", 0L), J.Str(e, "src", "launcher"));
	}
}

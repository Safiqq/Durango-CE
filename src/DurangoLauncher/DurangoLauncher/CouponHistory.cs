using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record CouponHistory(CouponHistoryItem[] Items)
{
	public const int Max = 10;

	public static CouponHistory Parse(JsonElement r)
	{
		return new CouponHistory((from i in J.Objs(r, "items").Select(CouponHistoryItem.Parse)
			orderby i.At descending
			select i).Take(10).ToArray());
	}

	[CompilerGenerated]
	private CouponHistory(CouponHistory original)
	{
		Items = original.Items;
	}
}

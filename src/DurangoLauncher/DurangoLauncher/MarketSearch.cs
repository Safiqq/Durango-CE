using System;
using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record MarketSearch(int Total, int PageSize, MarketProduct[] Products)
{
	public static MarketSearch Parse(JsonElement r)
	{
		MarketProduct[] array = J.Objs(r, "products").Select(MarketProduct.Parse).ToArray();
		return new MarketSearch(J.Int(r, "total", array.Length), J.Int(r, "page_size", Math.Max(1, array.Length)), array);
	}
}

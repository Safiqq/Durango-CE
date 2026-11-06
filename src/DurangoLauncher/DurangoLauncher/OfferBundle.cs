using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record OfferBundle(TradeItem[] Items, long Tstone)
{
	public bool IsEmpty
	{
		get
		{
			if (Items.Length == 0)
			{
				return Tstone <= 0;
			}
			return false;
		}
	}

	public static readonly OfferBundle Empty = new OfferBundle(Array.Empty<TradeItem>(), 0L);

	public static OfferBundle Parse(JsonElement e)
	{
		if (e.ValueKind != JsonValueKind.Object)
		{
			return Empty;
		}
		return new OfferBundle(TradeItem.ParseList(e, "items"), J.Long(e, "tstone", 0L));
	}

	[CompilerGenerated]
	private OfferBundle(OfferBundle original)
	{
		Items = original.Items;
		Tstone = original.Tstone;
	}
}

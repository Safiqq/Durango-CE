namespace DurangoLauncher;

public sealed class BuyFlow
{
	public string? ProductId { get; private set; }

	public long Expect { get; private set; }

	public long? NewPrice { get; private set; }

	public bool Busy { get; private set; }

	public void Start(MarketProduct p)
	{
		ProductId = p.ProductId;
		Expect = p.Price;
		NewPrice = null;
		Busy = true;
	}

	public bool Fail(ApiError e)
	{
		Busy = false;
		if (e.Code == "price_changed")
		{
			long? price = e.Price;
			if (price.HasValue)
			{
				long valueOrDefault = price.GetValueOrDefault();
				if (valueOrDefault > 0)
				{
					NewPrice = valueOrDefault;
					Expect = valueOrDefault;
					return true;
				}
			}
		}
		return false;
	}

	public void Done()
	{
		Busy = false;
		NewPrice = null;
		ProductId = null;
	}
}

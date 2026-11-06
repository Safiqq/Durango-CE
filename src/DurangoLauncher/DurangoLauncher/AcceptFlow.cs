namespace DurangoLauncher;

public sealed class AcceptFlow
{
	public string? OfferId { get; private set; }

	public int SentVersion { get; private set; }

	public bool NeedReload { get; private set; }

	public bool Busy { get; private set; }

	public void Start(TradeOffer o)
	{
		OfferId = o.Id;
		SentVersion = o.Version;
		NeedReload = false;
		Busy = true;
	}

	public bool Fail(ApiError e)
	{
		Busy = false;
		NeedReload = e.Code == "stale";
		return NeedReload;
	}

	public void Done()
	{
		Busy = false;
		NeedReload = false;
		OfferId = null;
	}

	public bool Reloaded(TradeOffer? fresh)
	{
		NeedReload = false;
		if (fresh != null && fresh.Open)
		{
			return fresh.Version != SentVersion;
		}
		return false;
	}
}

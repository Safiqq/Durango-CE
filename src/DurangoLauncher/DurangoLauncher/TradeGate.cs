namespace DurangoLauncher;

public sealed class TradeGate
{
	public bool? Allowed { get; private set; }

	public bool Locked => Allowed == false;

	public bool Star => Allowed == true;

	public bool CanPoll => !Locked;

	public void FromSummary(TradeSummary s)
	{
		Allowed = s.TradeAllowed;
	}

	public void DeniedMe()
	{
		Allowed = false;
	}

	public void Reset()
	{
		Allowed = null;
	}
}

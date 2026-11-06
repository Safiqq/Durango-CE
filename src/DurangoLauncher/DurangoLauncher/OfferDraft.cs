namespace DurangoLauncher;

public sealed record OfferDraft(string ToEntityId, string[] GiveItemIds, long GiveTstone, string[] WantItemIds, long WantTstone, string Message)
{
	public bool IsEmpty
	{
		get
		{
			if (GiveItemIds.Length == 0 && GiveTstone <= 0 && WantItemIds.Length == 0)
			{
				return WantTstone <= 0;
			}
			return false;
		}
	}
}

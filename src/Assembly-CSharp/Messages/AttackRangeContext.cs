namespace Messages;

public sealed class AttackRangeContext
{
	public string OwnerId;

	public string WindowId;

	public double AttackStartAt;

	public double RevisionAt;

	public bool Cancel;

	public bool IsValid()
	{
		if (!string.IsNullOrEmpty(OwnerId) && OwnerId.Length <= 256 && WindowId != null && WindowId.Length <= 64 && AttackStartAt > 0.0 && !double.IsInfinity(AttackStartAt) && !double.IsNaN(RevisionAt) && !double.IsInfinity(RevisionAt))
		{
			return RevisionAt >= AttackStartAt;
		}
		return false;
	}
}

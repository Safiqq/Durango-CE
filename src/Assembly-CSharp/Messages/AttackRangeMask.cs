namespace Messages;

public sealed class AttackRangeMask
{
	public double ActiveUntil;

	public float X;

	public float Z;

	public float Cell;

	public int Width;

	public int Height;

	public int[] Runs;

	public bool IsValid()
	{
		if (Width < 1 || Width > 512 || Height < 1 || Height > 512 || !(Cell > 0f) || float.IsInfinity(Cell) || float.IsNaN(X) || float.IsInfinity(X) || float.IsNaN(Z) || float.IsInfinity(Z) || double.IsNaN(ActiveUntil) || double.IsInfinity(ActiveUntil) || Runs == null || Runs.Length % 3 != 0 || Runs.Length > Width * Height * 3)
		{
			return false;
		}
		int num = -1;
		int num2 = -1;
		for (int i = 0; i < Runs.Length; i += 3)
		{
			int num3 = Runs[i];
			int num4 = Runs[i + 1];
			int num5 = Runs[i + 2];
			if (num3 < 0 || num3 >= Height || num4 < 0 || num5 < num4 || num5 >= Width || num3 < num || (num3 == num && num4 <= num2))
			{
				return false;
			}
			num = num3;
			num2 = num5;
		}
		return true;
	}
}

namespace DurangoLauncher;

public static class LevelChip
{
	public static readonly (int? Min, int? Max)[] Ranges = new(int?, int?)[4]
	{
		(null, null),
		(1, 20),
		(21, 40),
		(41, 60)
	};

	public static string Text(int idx)
	{
		(int?, int?) tuple = Ranges[idx];
		var (num, _) = tuple;
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			int? item = tuple.Item2;
			if (item.HasValue)
			{
				int valueOrDefault2 = item.GetValueOrDefault();
				return $"Lv {valueOrDefault}–{valueOrDefault2}";
			}
		}
		return L.T("Lv ท\u0e31\u0e49งหมด", "All levels");
	}

	public static int Next(int idx)
	{
		return (idx + 1) % Ranges.Length;
	}
}

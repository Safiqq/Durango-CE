using System;

namespace DurangoLauncher;

public sealed record MonthGrid(int Year, int Month, TimeSpan Offset)
{
	public int Days => DateTime.DaysInMonth(Year, Month);

	public int Lead => (int)(new DateTime(Year, Month, 1).DayOfWeek + 6) % 7;

	public int Rows => (Lead + Days + 6) / 7;

	public int Cells => Rows * 7;

	public int DayAt(int cell)
	{
		int num = cell - Lead + 1;
		if (num < 1 || num > Days)
		{
			return 0;
		}
		return num;
	}

	public int CellOf(int day)
	{
		return Lead + day - 1;
	}

	public long DayStart(int day)
	{
		return new DateTimeOffset(Year, Month, day, 0, 0, 0, Offset).ToUnixTimeSeconds();
	}

	public int DayOf(double unix)
	{
		DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(unix * 1000.0)).ToOffset(Offset);
		if (dateTimeOffset.Year != Year || dateTimeOffset.Month != Month)
		{
			return 0;
		}
		return dateTimeOffset.Day;
	}
}

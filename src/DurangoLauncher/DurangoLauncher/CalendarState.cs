using System;
using System.Diagnostics;
using System.Linq;

namespace DurangoLauncher;

public sealed class CalendarState
{
	public const int OpenMs = 15000;

	public const int IdleMs = 120000;

	public const int MaxMs = 300000;

	private (int Y, int M) _selMonth;

	public CalendarData? Data { get; private set; }

	public long RecvTick { get; private set; }

	public bool Known { get; private set; }

	public bool Hidden { get; private set; }

	public int Failures { get; private set; }

	public string LastError { get; private set; } = "";

	public int Sel { get; private set; }

	public int SelEvent { get; set; }

	public bool MenuVisible
	{
		get
		{
			if (Known)
			{
				return !Hidden;
			}
			return false;
		}
	}

	public bool ShowError
	{
		get
		{
			if (Data == null && Failures > 0)
			{
				return !Hidden;
			}
			return false;
		}
	}

	public bool Stale
	{
		get
		{
			if (Data != null)
			{
				return Failures > 0;
			}
			return false;
		}
	}

	public MonthGrid? Grid => Data?.Grid();

	public void Apply(CalFetchResult r, long tick)
	{
		Known = true;
		switch (r.Kind)
		{
		case CalFetchKind.Ok:
			Data = r.Data;
			RecvTick = tick;
			Hidden = false;
			Failures = 0;
			LastError = "";
			EnsureSelection(tick);
			break;
		case CalFetchKind.NotFound:
			Hidden = true;
			Data = null;
			Failures = 0;
			LastError = r.Detail;
			break;
		default:
			Hidden = false;
			Failures++;
			LastError = r.Detail;
			break;
		}
	}

	public double ServerNow(long tick)
	{
		if (!(Data == null))
		{
			return Data.Now + (double)Math.Max(0L, tick - RecvTick) / (double)Stopwatch.Frequency;
		}
		return 0.0;
	}

	public int Today(long tick)
	{
		if (!(Data == null))
		{
			return Data.Grid().DayOf(ServerNow(tick));
		}
		return 0;
	}

	public CalEvent[] EventsOn(int day)
	{
		if (Data == null || day <= 0)
		{
			return Array.Empty<CalEvent>();
		}
		MonthGrid monthGrid = Data.Grid();
		if (day > monthGrid.Days)
		{
			return Array.Empty<CalEvent>();
		}
		long ds = monthGrid.DayStart(day);
		return Data.Events.Where((CalEvent e) => e.OnDay(ds)).ToArray();
	}

	public int LiveCount(long tick)
	{
		return Data?.LiveCount(ServerNow(tick)) ?? 0;
	}

	public void EnsureSelection(long tick)
	{
		MonthGrid grid = Grid;
		if (grid == null)
		{
			return;
		}
		if (Sel >= 1 && Sel <= grid.Days)
		{
			(int, int) selMonth = _selMonth;
			int year = grid.Year;
			int month = grid.Month;
			if (selMonth.Item1 == year && selMonth.Item2 == month)
			{
				return;
			}
		}
		SelectToday(tick);
	}

	public void SelectToday(long tick)
	{
		if (!(Grid == null))
		{
			int num = Today(tick);
			Select((num <= 0) ? 1 : num);
		}
	}

	public void Select(int day)
	{
		MonthGrid grid = Grid;
		if (grid == null)
		{
			return;
		}
		int num = Math.Clamp(day, 1, grid.Days);
		if (num == Sel)
		{
			(int, int) selMonth = _selMonth;
			int year = grid.Year;
			int month = grid.Month;
			if (selMonth.Item1 == year && selMonth.Item2 == month)
			{
				goto IL_0058;
			}
		}
		SelEvent = 0;
		goto IL_0058;
		IL_0058:
		Sel = num;
		_selMonth = (Y: grid.Year, M: grid.Month);
	}

	public void Step(int delta)
	{
		Select(Sel + delta);
	}

	public CalEvent? SelectedEvent()
	{
		CalEvent[] array = EventsOn(Sel);
		if (array.Length == 0)
		{
			return null;
		}
		return array[Math.Clamp(SelEvent, 0, array.Length - 1)];
	}

	public int NextDelayMs(bool pageOpen)
	{
		if (Hidden)
		{
			return 300000;
		}
		int num = (pageOpen ? 15000 : 120000);
		if (Failures <= 0)
		{
			return num;
		}
		long val = (long)num << Math.Min(Failures, 10);
		return (int)Math.Min(300000L, val);
	}
}

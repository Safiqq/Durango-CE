using System;
using System.Collections.Generic;

namespace DurangoLauncher;

public static class TradeMenu
{
	public static bool ShowSection(bool loggedIn, LauncherFeatures f)
	{
		if (loggedIn)
		{
			return f.AnyMenu;
		}
		return false;
	}

	public static bool ShowSection(bool loggedIn, LauncherFeatures f, bool calendar)
	{
		if (!calendar)
		{
			return ShowSection(loggedIn, f);
		}
		return true;
	}

	public static bool ShowChars(bool loggedIn, LauncherFeatures f)
	{
		if (loggedIn)
		{
			return f.Items;
		}
		return false;
	}

	public static bool ShowChat(bool loggedIn, LauncherFeatures f)
	{
		if (loggedIn)
		{
			return f.Chat;
		}
		return false;
	}

	public static bool ShowMarket(bool loggedIn, LauncherFeatures f)
	{
		if (loggedIn)
		{
			return f.MarketBuy;
		}
		return false;
	}

	public static bool ShowTrade(bool loggedIn, LauncherFeatures f)
	{
		if (loggedIn)
		{
			return f.Trade;
		}
		return false;
	}

	public static bool ShowCoupon(bool loggedIn, LauncherFeatures f)
	{
		if (loggedIn)
		{
			return f.Coupon;
		}
		return false;
	}

	public static bool ShowCalendar(CalendarState? c)
	{
		return c?.MenuVisible ?? false;
	}

	public static string[] Order(bool loggedIn, LauncherFeatures f, bool calendar)
	{
		List<string> list = new List<string>();
		if (ShowChars(loggedIn, f))
		{
			list.Add("chars");
		}
		if (ShowChat(loggedIn, f))
		{
			list.Add("chat");
		}
		if (calendar)
		{
			list.Add("calendar");
		}
		if (ShowMarket(loggedIn, f))
		{
			list.Add("market");
		}
		if (ShowTrade(loggedIn, f))
		{
			list.Add("trade");
		}
		if (ShowCoupon(loggedIn, f))
		{
			list.Add("coupon");
		}
		return list.ToArray();
	}

	public static (int ItemH, int Gap) Fit(int top, int limit, int count, int itemH, int gap, int minH)
	{
		if (count <= 0)
		{
			return (ItemH: itemH, Gap: gap);
		}
		int num = limit - top;
		if (count * (itemH + gap) <= num)
		{
			return (ItemH: itemH, Gap: gap);
		}
		int num2 = Math.Max(minH, num / count - gap);
		if (count * (num2 + gap) <= num)
		{
			return (ItemH: num2, Gap: gap);
		}
		int num3 = Math.Max(2, num / count - num2);
		if (count * (num2 + num3) <= num)
		{
			return (ItemH: num2, Gap: num3);
		}
		return (ItemH: Math.Max(Math.Min(30, num2), num / count - num3), Gap: num3);
	}

	public static (int Chat, int Market, int Trade) Badges(TradeSummary? s)
	{
		if (!(s == null))
		{
			return (Chat: s.ChatUnread, Market: s.MarketCollectable, Trade: s.OffersIn);
		}
		return (Chat: 0, Market: 0, Trade: 0);
	}

	public static (int Chat, int Market, int Trade) Badges(TradeSummary? s, TradeGate gate)
	{
		(int, int, int) result = Badges(s);
		if (!gate.Locked)
		{
			return result;
		}
		return (Chat: result.Item1, Market: result.Item2, Trade: 0);
	}
}

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record LauncherFeatures(bool Items, bool Chat, bool MarketBuy, bool Trade, int Api = 0, bool Coupon = false)
{
	public bool Any
	{
		get
		{
			if (!Items && !Chat && !MarketBuy)
			{
				return Trade;
			}
			return true;
		}
	}

	public bool AnyMenu
	{
		get
		{
			if (!Any)
			{
				return Coupon;
			}
			return true;
		}
	}

	public static readonly LauncherFeatures Off = new LauncherFeatures(Items: false, Chat: false, MarketBuy: false, Trade: false);

	public static LauncherFeatures Parse(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("launcher", out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return Off;
		}
		return new LauncherFeatures(J.Bool(value, "items"), J.Bool(value, "chat"), J.Bool(value, "market_buy"), J.Bool(value, "trade"), J.Int(value, "api"), J.Bool(value, "coupon"));
	}

	public LauncherFeatures Without(string feature)
	{
		switch (feature)
		{
		case "items":
			return this with
			{
				Items = false
			};
		case "chat":
			return this with
			{
				Chat = false
			};
		case "market_buy":
		case "market":
			return this with
			{
				MarketBuy = false
			};
		case "trade":
			return this with
			{
				Trade = false
			};
		case "coupon":
			return this with
			{
				Coupon = false
			};
		default:
			return this;
		}
	}

	[CompilerGenerated]
	private LauncherFeatures(LauncherFeatures original)
	{
		Items = original.Items;
		Chat = original.Chat;
		MarketBuy = original.MarketBuy;
		Trade = original.Trade;
		Api = original.Api;
		Coupon = original.Coupon;
	}
}

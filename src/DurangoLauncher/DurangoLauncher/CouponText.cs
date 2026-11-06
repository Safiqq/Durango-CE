using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DurangoLauncher;

public static class CouponText
{
	public static string MoveTitle => L.T("น\u0e35\u0e48ค\u0e37อรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง", "This is a device transfer code");

	public static string MoveBody => L.T("ใช\u0e49ในเกม: ต\u0e31\u0e49งค\u0e48า ▸ บ\u0e31ญช\u0e35 ▸ กรอกรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง · หน\u0e49าน\u0e35\u0e49ร\u0e31บเฉพาะค\u0e39ปองและโค\u0e49ดเต\u0e34ม Coin", "Use it in game: Settings ▸ Account ▸ Enter transfer code · this page only takes coupons and Coin codes");

	public static string KindLabel(CouponCode.Kind k)
	{
		switch (k)
		{
		case CouponCode.Kind.Reward:
			return L.T("ค\u0e39ปองของรางว\u0e31ล", "Reward coupon");
		case CouponCode.Kind.Coin:
		case CouponCode.Kind.CoinBad:
			return L.T("โค\u0e49ดเต\u0e34ม Coin", "Coin top-up code");
		case CouponCode.Kind.Move:
			return L.T("รห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง", "Device transfer code");
		default:
			return "";
		}
	}

	public static string? InputHint(CouponCode.Result r)
	{
		switch (r.Kind)
		{
		case CouponCode.Kind.CoinBad:
			return L.T("โค\u0e49ดเต\u0e34ม Coin ไม\u0e48ถ\u0e39กต\u0e49อง — ร\u0e39ป DC-XXXX-XXXX ไม\u0e48ม\u0e35 0/O/1/I", "Invalid Coin code — format DC-XXXX-XXXX, no 0/O/1/I");
		case CouponCode.Kind.Invalid:
			if (r.Reason == "short")
			{
				return L.T("พ\u0e34มพ\u0e4cต\u0e48อ — ค\u0e39ปองยาว 8–16 ต\u0e31ว", "Keep typing — coupons are 8–16 characters");
			}
			if (r.Reason == "chars")
			{
				return L.T("ร\u0e39ปโค\u0e49ดไม\u0e48ถ\u0e39กต\u0e49อง — ใช\u0e49ได\u0e49เฉพาะ A–Z 0–9 และ -", "Invalid code — only A–Z, 0–9 and - are allowed");
			}
			return L.T("โค\u0e49ดยาวเก\u0e34นไป — ค\u0e39ปองยาว 8–16 ต\u0e31ว", "Code is too long — coupons are 8–16 characters");
		default:
			return null;
		}
	}

	public static string OkTitle(CouponResult r)
	{
		if (!r.IsCoin)
		{
			return L.T("ใช\u0e49ค\u0e39ปอง " + r.Code + " แล\u0e49ว", "Coupon " + r.Code + " redeemed") + ((r.Name.Length > 0 && !r.Name.Equals(r.Code, StringComparison.OrdinalIgnoreCase)) ? (" · " + r.Name) : "");
		}
		return L.T("เต\u0e34ม Coin สำเร\u0e47จ +" + TradeFmt.N(r.CoinAdded) + " Coin", "Coin added +" + TradeFmt.N(r.CoinAdded) + " Coin");
	}

	public static string OkBody(CouponResult r, string toNameFallback)
	{
		if (r.IsCoin)
		{
			return L.T("Coin ในบ\u0e31ญช\u0e35ตอนน\u0e35\u0e49 " + TradeFmt.N(r.Coin) + " · ใช\u0e49ได\u0e49ท\u0e38กต\u0e31วละคร", "Coin on this account now " + TradeFmt.N(r.Coin) + " · usable by every character");
		}
		string text = ((r.ToName.Length > 0) ? r.ToName : toNameFallback);
		string text2 = ((r.MailDays > 0) ? L.T($" (เก\u0e47บได\u0e49 {r.MailDays} ว\u0e31น)", $" (kept {r.MailDays} days)") : "");
		return L.T("ส\u0e48งเข\u0e49ากล\u0e48องจดหมายของ " + text + " · เป\u0e34ดจดหมายในเกม", "Sent to " + text + "'s mailbox · open your mail in game") + text2;
	}

	public static (string Icon, string Name, int Count)[] Gifts(CouponResult r)
	{
		List<(string, string, int)> list = new List<(string, string, int)>();
		if (r.Tstone > 0)
		{
			list.Add(("gem", "T Stone", (int)Math.Min(2147483647L, r.Tstone)));
		}
		list.AddRange(r.Items.Select((CouponGift i) => ("gift", Name: i.Name, Count: i.Count)));
		list.AddRange(r.Vouchers.Select((CouponGift v) => ("ticket", Name: v.Name, Count: v.Count)));
		return list.ToArray();
	}

	public static string? Fallback(string code)
	{
		return code switch
		{
			"not_found" => L.T("ไม\u0e48พบค\u0e39ปองน\u0e35\u0e49", "Coupon not found"), 
			"disabled" => L.T("ค\u0e39ปองน\u0e35\u0e49ป\u0e34ดใช\u0e49งานแล\u0e49ว", "This coupon is disabled"), 
			"not_yet" => L.T("ค\u0e39ปองน\u0e35\u0e49ย\u0e31งไม\u0e48เป\u0e34ดให\u0e49ใช\u0e49", "This coupon is not open yet"), 
			"expired" => L.T("ค\u0e39ปองน\u0e35\u0e49หมดอาย\u0e38แล\u0e49ว", "This coupon has expired"), 
			"hours" => L.T("ค\u0e39ปองน\u0e35\u0e49ใส\u0e48ได\u0e49เฉพาะบางช\u0e48วงเวลา", "This coupon only works at certain hours"), 
			"used" => L.T("บ\u0e31ญช\u0e35น\u0e35\u0e49ใช\u0e49ค\u0e39ปองน\u0e35\u0e49ไปแล\u0e49ว", "This account already used this coupon"), 
			"voucher_invalid" => L.T("โค\u0e49ดเต\u0e34ม Coin ไม\u0e48ถ\u0e39กต\u0e49อง", "Invalid Coin code"), 
			"voucher_not_found" => L.T("ไม\u0e48พบโค\u0e49ดเต\u0e34ม Coin น\u0e35\u0e49", "Coin code not found"), 
			"voucher_used" => L.T("โค\u0e49ดเต\u0e34ม Coin น\u0e35\u0e49ถ\u0e39กใช\u0e49ไปแล\u0e49ว", "This Coin code was already used"), 
			"voucher_used_self" => L.T("บ\u0e31ญช\u0e35น\u0e35\u0e49ใช\u0e49โค\u0e49ดเต\u0e34ม Coin น\u0e35\u0e49ไปแล\u0e49ว", "This account already used this Coin code"), 
			"voucher_voided" => L.T("โค\u0e49ดเต\u0e34ม Coin น\u0e35\u0e49ถ\u0e39กยกเล\u0e34กแล\u0e49ว", "This Coin code was voided"), 
			"coin_disabled" => L.T("ระบบเต\u0e34ม Coin ป\u0e34ดอย\u0e39\u0e48", "Coin top-up is turned off"), 
			"move_code" => MoveTitle, 
			"empty" => L.T("ย\u0e31งไม\u0e48ได\u0e49ใส\u0e48โค\u0e49ด", "No code entered"), 
			"rate_limited" => L.T("ใส\u0e48โค\u0e49ดถ\u0e35\u0e48เก\u0e34นไป — รอส\u0e31กคร\u0e39\u0e48", "Too many tries — please wait"), 
			"unavailable" => L.T("เซ\u0e34ร\u0e4cฟเกมไม\u0e48ตอบ — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "The game server is not responding — try again"), 
			_ => null, 
		};
	}

	public static string? ErrHint(ApiError e, int retryLeftSec = 0)
	{
		switch (e.Code)
		{
		case "not_found":
		case "voucher_not_found":
			return L.T("ตรวจต\u0e31วสะกดอ\u0e35กคร\u0e31\u0e49ง · ต\u0e31วเล\u0e47ก/ใหญ\u0e48ไม\u0e48ม\u0e35ผล · ช\u0e48องว\u0e48างต\u0e31ดให\u0e49เอง", "Check the spelling · case does not matter · spaces are trimmed");
		case "used":
		{
			long? usedAt = e.UsedAt;
			if (usedAt.HasValue)
			{
				long valueOrDefault = usedAt.GetValueOrDefault();
				if (valueOrDefault > 0)
				{
					return L.T("ใช\u0e49ไปเม\u0e37\u0e48อ ", "Used on ") + When(valueOrDefault);
				}
			}
			break;
		}
		case "hours":
		{
			string hoursText = e.HoursText;
			if (hoursText != null && hoursText.Length > 0)
			{
				return L.T("ช\u0e48วงท\u0e35\u0e48ใส\u0e48ได\u0e49 " + hoursText + " น.", "Open hours " + hoursText);
			}
			break;
		}
		case "not_yet":
		{
			long? usedAt = e.Since;
			if (usedAt.HasValue)
			{
				long valueOrDefault2 = usedAt.GetValueOrDefault();
				if (valueOrDefault2 > 0)
				{
					return L.T("เป\u0e34ดให\u0e49ใช\u0e49 ", "Opens ") + When(valueOrDefault2);
				}
			}
			break;
		}
		case "voucher_invalid":
			return L.T("ร\u0e39ป DC-XXXX-XXXX · ไม\u0e48ม\u0e35 0/O/1/I", "Format DC-XXXX-XXXX · no 0/O/1/I");
		case "rate_limited":
			return (retryLeftSec > 0) ? L.T($"ลองใหม\u0e48ได\u0e49ใน {retryLeftSec} ว\u0e34 — ก\u0e31นการส\u0e38\u0e48มโค\u0e49ด", $"Try again in {retryLeftSec} s — this stops code guessing") : L.T("ก\u0e31นการส\u0e38\u0e48มโค\u0e49ด — ลองใหม\u0e48ได\u0e49แล\u0e49ว", "This stops code guessing — you can try again now");
		case "not_your_character":
			return L.T("รายช\u0e37\u0e48อต\u0e31วละครโหลดใหม\u0e48แล\u0e49ว — เล\u0e37อกต\u0e31วอ\u0e35กคร\u0e31\u0e49ง", "The character list was reloaded — pick a character again");
		}
		return null;
	}

	public static string When(long unix)
	{
		if (unix > 0)
		{
			return DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
		}
		return "";
	}

	public static string HistoryLine(CouponHistoryItem h)
	{
		if (!h.IsCoin)
		{
			return L.T("ค\u0e39ปองของรางว\u0e31ล", "Reward coupon") + ((h.ToName.Length > 0) ? (" → " + h.ToName) : "") + " · " + When(h.At);
		}
		return "+" + TradeFmt.N(h.CoinAdded) + " Coin · " + When(h.At);
	}
}

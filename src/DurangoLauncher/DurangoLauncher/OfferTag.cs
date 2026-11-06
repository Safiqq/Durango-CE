using System.Collections.Generic;
using System.Linq;

namespace DurangoLauncher;

public static class OfferTag
{
	public enum Kind
	{
		Yellow,
		Gray,
		Green,
		Red
	}

	public static (string Text, Kind Kind) Of(TradeOffer o, string me)
	{
		return o.State switch
		{
			"pending" => o.IsIncoming(me) ? (Text: L.T("รอค\u0e38ณตอบ", "Your reply"), Kind: Kind.Yellow) : (Text: L.T("รออ\u0e35กฝ\u0e48าย", "Waiting"), Kind: Kind.Gray), 
			"accepted" => (Text: L.T("สำเร\u0e47จ", "Done"), Kind: Kind.Green), 
			"declined" => (Text: L.T("ปฏ\u0e34เสธ", "Declined"), Kind: Kind.Red), 
			"cancelled" => (Text: L.T("ยกเล\u0e34ก", "Cancelled"), Kind: Kind.Red), 
			"expired" => (Text: L.T("หมดอาย\u0e38", "Expired"), Kind: Kind.Red), 
			"countered" => (Text: L.T("โต\u0e49กล\u0e31บแล\u0e49ว", "Countered"), Kind: Kind.Gray), 
			_ => (Text: o.State, Kind: Kind.Gray), 
		};
	}

	public static string? EditedChip(TradeOffer o, string me)
	{
		if (!o.Open || !o.Edited || !o.IsIncoming(me))
		{
			return null;
		}
		return L.T("ข\u0e49อเสนอถ\u0e39กแก\u0e49ไข", "Offer edited");
	}

	public static string EditedLine(TradeOffer o, long now)
	{
		if (!o.Edited)
		{
			return "";
		}
		return L.T("แก\u0e49ไขเม\u0e37\u0e48อ ", "Edited ") + TradeFmt.Clock(o.EditedAt, now);
	}

	public static string GiveLine(OfferBundle b)
	{
		List<string> list = b.Items.Select((TradeItem i) => i.Name + ((i.Count > 1) ? $" ×{i.Count}" : "")).ToList();
		if (b.Tstone > 0)
		{
			list.Add(TradeFmt.N(b.Tstone) + " T");
		}
		if (list.Count != 0)
		{
			return L.T("ให\u0e49 ", "Gives ") + string.Join(", ", list);
		}
		return L.T("ไม\u0e48ให\u0e49อะไร", "Nothing");
	}
}

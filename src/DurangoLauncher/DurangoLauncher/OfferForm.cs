using System;
using System.Collections.Generic;
using System.Linq;

namespace DurangoLauncher;

public sealed class OfferForm
{
	public const int MaxSlots = 5;

	public readonly List<string> GiveIds = new List<string>();

	public readonly List<string> WantIds = new List<string>();

	public OfferMode Mode { get; }

	public string Me { get; }

	public TradeOffer? Base { get; private set; }

	public TradeOffer? Origin { get; set; }

	public OfferSide? FixedTo { get; }

	public TradePlayer? To { get; set; }

	public long GiveT { get; set; }

	public long WantT { get; set; }

	public string Message { get; set; } = "";

	public string ToId => FixedTo?.EntityId ?? To?.EntityId ?? "";

	public string ToName => FixedTo?.Name ?? To?.Name ?? "";

	public int ToLevel => FixedTo?.Level ?? To?.Level ?? 0;

	public int Version => Base?.Version ?? 0;

	public bool WantItemsAllowed
	{
		get
		{
			if (Mode != OfferMode.Counter)
			{
				if (Mode == OfferMode.Edit)
				{
					return Base?.ReplyTo != null;
				}
				return false;
			}
			return true;
		}
	}

	public bool Changed
	{
		get
		{
			if (Mode != OfferMode.Edit || Base == null)
			{
				return true;
			}
			if (Same(GiveIds, Base.Give.Items.Select((TradeItem i) => i.Id)) && GiveT == Base.Give.Tstone && WantT == Base.Want.Tstone && (!WantItemsAllowed || Same(WantIds, Base.Want.Items.Select((TradeItem i) => i.Id))))
			{
				return Message.Trim() != Base.Message.Trim();
			}
			return true;
			static bool Same(IEnumerable<string> a, IEnumerable<string> b)
			{
				return a.ToHashSet().SetEquals(b);
			}
		}
	}

	public long ExtraTstone
	{
		get
		{
			if (Mode != OfferMode.Edit || !(Base != null))
			{
				return GiveT;
			}
			return Math.Max(0L, GiveT - Base.Give.Tstone);
		}
	}

	public static string WantHint => L.T("อยากได\u0e49ไอเทมอะไร ค\u0e38ยในห\u0e49องแชทข\u0e49อเสนอ — อ\u0e35กฝ\u0e48ายใส\u0e48ของเองตอนโต\u0e49กล\u0e31บ", "Want an item? Ask in the offer's chat — they add their items when they counter");

	public OfferForm(OfferMode mode, TradeOffer? b, string me, TradeItem? withItem = null)
	{
		Mode = mode;
		Me = me;
		Base = ((mode == OfferMode.New) ? null : b);
		if (Base != null)
		{
			FixedTo = Base.Other(me);
			Load(Base);
		}
		if (withItem != null && !GiveIds.Contains(withItem.Id) && GiveIds.Count < 5)
		{
			GiveIds.Add(withItem.Id);
		}
	}

	private void Load(TradeOffer b)
	{
		var (offerBundle, offerBundle2) = b.Sides(Me);
		GiveIds.Clear();
		WantIds.Clear();
		GiveIds.AddRange(offerBundle2.Items.Select((TradeItem i) => i.Id).Distinct().Take(5));
		GiveT = offerBundle2.Tstone;
		WantT = offerBundle.Tstone;
		if (WantItemsAllowed)
		{
			WantIds.AddRange(offerBundle.Items.Select((TradeItem i) => i.Id).Distinct());
		}
		Message = ((Mode != OfferMode.Counter && b.From.EntityId == Me) ? b.Message : "");
	}

	public TradeItem[] WantChoices()
	{
		if (!WantItemsAllowed || Base == null)
		{
			return Array.Empty<TradeItem>();
		}
		if (Mode == OfferMode.Counter)
		{
			return (from i in Base.Give.Items
				group i by i.Id into g
				select g.First()).ToArray();
		}
		TradeItem[] second = ((Origin != null && Origin.Id == Base.ReplyTo) ? Origin.Give.Items : Array.Empty<TradeItem>());
		return (from i in Base.Want.Items.Concat(second)
			group i by i.Id into g
			select g.First()).ToArray();
	}

	public TradeItem[] GiveChoices(IEnumerable<TradeItem>? bag)
	{
		IEnumerable<TradeItem> enumerable = (bag ?? Enumerable.Empty<TradeItem>()).Where((TradeItem i) => i.Held == null && i.Tradable);
		if (Mode != OfferMode.Edit || Base == null)
		{
			return (from i in enumerable
				group i by i.Id into g
				select g.First()).ToArray();
		}
		return (from i in Base.Give.Items.Select((TradeItem i) => i with
			{
				Held = null
			}).Concat(enumerable)
			group i by i.Id into g
			select g.First()).ToArray();
	}

	public bool IsKept(string id)
	{
		if (Mode == OfferMode.Edit && Base != null)
		{
			return Base.Give.Items.Any((TradeItem i) => i.Id == id);
		}
		return false;
	}

	public void Prune(IEnumerable<TradeItem>? bag)
	{
		if (bag != null)
		{
			HashSet<string> ok = (from i in GiveChoices(bag)
				select i.Id).ToHashSet();
			GiveIds.RemoveAll((string id) => !ok.Contains(id));
		}
		if (!WantItemsAllowed)
		{
			WantIds.Clear();
		}
		else if (Mode == OfferMode.Counter || Origin != null)
		{
			HashSet<string> w = (from i in WantChoices()
				select i.Id).ToHashSet();
			WantIds.RemoveAll((string id) => !w.Contains(id));
		}
	}

	public string? ToggleGive(string id)
	{
		if (GiveIds.Remove(id))
		{
			return null;
		}
		if (GiveIds.Count >= 5)
		{
			return L.T($"ใส\u0e48ได\u0e49ส\u0e39งส\u0e38ด {5} ช\u0e48อง", $"Up to {5} slots");
		}
		GiveIds.Add(id);
		return null;
	}

	public void ToggleWant(string id)
	{
		if (!WantIds.Remove(id) && WantItemsAllowed && WantChoices().Any((TradeItem i) => i.Id == id) && WantIds.Count < 5)
		{
			WantIds.Add(id);
		}
	}

	public OfferDraft Draft()
	{
		return new OfferDraft(ToId, GiveIds.ToArray(), GiveT, WantItemsAllowed ? WantIds.ToArray() : Array.Empty<string>(), WantT, Message.Trim());
	}

	public (int Added, int Removed, long GiveTDelta) Delta()
	{
		if (Mode != OfferMode.Edit || Base == null)
		{
			return (Added: GiveIds.Count, Removed: 0, GiveTDelta: GiveT);
		}
		HashSet<string> old = Base.Give.Items.Select((TradeItem i) => i.Id).ToHashSet();
		return (Added: GiveIds.Count((string id) => !old.Contains(id)), Removed: old.Count((string id) => !GiveIds.Contains(id)), GiveTDelta: GiveT - Base.Give.Tstone);
	}

	public string? Check(long? haveT)
	{
		OfferDraft offerDraft = Draft();
		if (offerDraft.ToEntityId.Length == 0)
		{
			return L.T("เล\u0e37อกผ\u0e39\u0e49เล\u0e48นท\u0e35\u0e48จะส\u0e48งข\u0e49อเสนอก\u0e48อน", "Choose who to send the offer to");
		}
		if (offerDraft.IsEmpty)
		{
			return L.T("ย\u0e31งไม\u0e48ได\u0e49ใส\u0e48ของหร\u0e37อ T ท\u0e31\u0e49งสองฝ\u0e31\u0e48ง", "Add something to give or ask for");
		}
		if (!Changed)
		{
			return L.T("ย\u0e31งไม\u0e48ได\u0e49แก\u0e49อะไร", "Nothing changed yet");
		}
		if (haveT.HasValue)
		{
			long valueOrDefault = haveT.GetValueOrDefault();
			if (ExtraTstone > valueOrDefault)
			{
				return L.T("T ไม\u0e48พอ — ม\u0e35 " + TradeFmt.T(valueOrDefault), "Not enough T — you have " + TradeFmt.T(valueOrDefault));
			}
		}
		return null;
	}

	public string Summary()
	{
		OfferDraft offerDraft = Draft();
		string value = (WantItemsAllowed ? L.T($"{offerDraft.WantItemIds.Length} ช\u0e34\u0e49น + ", $"{offerDraft.WantItemIds.Length} item(s) + ") : "");
		return L.T($"ค\u0e38ณให\u0e49 {offerDraft.GiveItemIds.Length} ช\u0e34\u0e49น + {TradeFmt.T(offerDraft.GiveTstone)} ⇄ ขอ {value}{TradeFmt.T(offerDraft.WantTstone)}", $"You give {offerDraft.GiveItemIds.Length} item(s) + {TradeFmt.T(offerDraft.GiveTstone)} ⇄ ask {value}{TradeFmt.T(offerDraft.WantTstone)}");
	}

	public bool Rebase(TradeOffer fresh)
	{
		if (Base == null || fresh.Id != Base.Id)
		{
			return false;
		}
		Base = fresh;
		Load(fresh);
		if (fresh.Open)
		{
			return fresh.From.EntityId == Me;
		}
		return false;
	}
}

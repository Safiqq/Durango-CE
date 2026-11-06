using System;
using System.Collections.Generic;
using System.Linq;
using Durango.Logic.Item;
using Durango.Network;
using Durango.UI;
using Newtonsoft.Json.Linq;

public static class CommunityAuctionPreview
{
	private static readonly Dictionary<string, ItemData> Items = new Dictionary<string, ItemData>();

	public static bool Active { get; private set; }

	public static bool Begin(CommunityAuctionUI.Tab tab, bool listing = false)
	{
		if (CommunityCareerUI.BlocksOtherMenus || CommunityTradeUI.BlocksOtherMenus || UIManager.MessageBox.IsShow)
		{
			return false;
		}
		InventorySystem inventorySystem = GameSystem<InventorySystem>.Instance();
		if (inventorySystem == null || inventorySystem.PlayerItemList == null)
		{
			return false;
		}
		MarketGroup marketGroup = UIManager.FindScript<MarketGroup>();
		if (marketGroup == null)
		{
			return false;
		}
		if (!marketGroup.IsOpened)
		{
			marketGroup.Open();
		}
		if (!marketGroup.IsOpened)
		{
			return false;
		}
		Items.Clear();
		foreach (ItemData item in (from x in inventorySystem.PlayerItemList
			where x != null && !string.IsNullOrEmpty(x.Id)
			group x by x.PrototypeId into x
			select x.First()).Take(4))
		{
			Items[item.Id] = item;
		}
		Active = true;
		if (listing)
		{
			marketGroup.SelectTabForTest(4);
		}
		else
		{
			marketGroup.OpenAuctionTab(tab);
		}
		return true;
	}

	public static void End()
	{
		Active = false;
		Items.Clear();
	}

	public static ItemData ResolveItem(JToken row)
	{
		if (Active && row is JObject && Items.TryGetValue(CommunityExchange.Str((JObject)row, "id"), out var value))
		{
			return value;
		}
		return CommunityExchange.ItemOf(row);
	}

	public static JObject Response(CommunityAuctionUI.Tab tab)
	{
		JArray jArray = new JArray();
		JArray jArray2 = new JArray();
		double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
		int num = 0;
		foreach (ItemData value in Items.Values)
		{
			JObject jObject = new JObject { ["id"] = value.Id };
			if (string.IsNullOrEmpty(CommunityAuctionUI.Query) || tab != CommunityAuctionUI.Tab.Browse || value.Name.IndexOf(CommunityAuctionUI.Query, StringComparison.CurrentCultureIgnoreCase) >= 0)
			{
				jArray.Add(new JObject
				{
					["id"] = "preview-lot-" + num,
					["item"] = jObject,
					["count"] = num + 1,
					["state"] = 1,
					["startPrice"] = 100 + num * 25,
					["current"] = 100 + num * 25,
					["minBid"] = 110 + num * 25,
					["bidCount"] = ((tab != CommunityAuctionUI.Tab.Selling) ? num : 0),
					["mine"] = tab == CommunityAuctionUI.Tab.Selling,
					["bidded"] = tab == CommunityAuctionUI.Tab.Bidding,
					["leading"] = tab == CommunityAuctionUI.Tab.Bidding && num == 1,
					["endsAt"] = predictedServerTime + (double)(3600 * (num + 1))
				});
			}
			if (num < 2)
			{
				jArray2.Add(new JObject
				{
					["id"] = "preview-claim-" + num,
					["item"] = jObject.DeepClone(),
					["count"] = num + 1,
					["at"] = predictedServerTime,
					["text"] = "ต\u0e31วอย\u0e48างไอเทมรอร\u0e31บ"
				});
			}
			num++;
		}
		jArray2.Add(new JObject
		{
			["id"] = "preview-money",
			["tstone"] = 1234,
			["count"] = 0
		});
		return new JObject
		{
			["ok"] = true,
			["lots"] = jArray,
			["claims"] = jArray2
		};
	}
}

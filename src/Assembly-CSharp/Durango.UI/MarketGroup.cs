using System;
using Durango.Logic.Clusters;
using Durango.Logic.Explore;
using Durango.Logic.Item;
using Durango.Logic.Market;
using Durango.Logic.Notification;
using Durango.UI.Control;
using Durango.Utils;
using Durango.Utils.Extensions;
using L10N;
using Messages;
using NestedPrefab;
using Shared.Economy;
using Shared.Market;
using UnityEngine;

namespace Durango.UI;

[Uri("Market")]
public class MarketGroup : UIBase, INotificationable
{
	public enum Menu
	{
		[T.EnumName("구입")]
		Buy,
		[T.EnumName("내 장터")]
		MyMarket,
		[T.EnumName("판매")]
		Sell,
		[EnumIcon("icon_market_info")]
		Auction,
		[EnumIcon("icon_market_sell")]
		AuctionList
	}

	[SerializeField]
	private UITitle _titleWidget;

	[SerializeField]
	private NestedPrefabLinker _menuLinker;

	[SerializeField]
	private CommodityListWidget _goodsList;

	[SerializeField]
	private MarketHistoryWidget _historyList;

	[SerializeField]
	private SellItemWidget _sellItemWidget;

	private readonly Toggle _notification = new Toggle(Durango.Logic.Notification.Type.Important);

	private IconTabList _menuList;

	private Menu[] _menus;

	private Menu _selectedTab;

	private bool _onExpiredNotification;

	private bool _onPaymentPendingNotification;

	private StackableAlarm<string, ProductSold> _productSoldAlarm;

	public Notification Notification => _notification;

	public bool OffServerAuctionOpen
	{
		get
		{
			if (_historyList != null)
			{
				return _historyList.OffServerAuctionMode;
			}
			return false;
		}
	}

	private void Start()
	{
		_openCloseSound = UISound.GroupType.Default;
		_menuList = _menuLinker.Object.GetComponent<IconTabList>();
		if (GameManager.ClusterMode == Mode.Online)
		{
			_menus = ((!OffServerLink.Active) ? new Menu[3]
			{
				Menu.Buy,
				Menu.MyMarket,
				Menu.Sell
			} : new Menu[4]
			{
				Menu.Buy,
				Menu.Auction,
				Menu.MyMarket,
				Menu.Sell
			});
			_menuList.BeginLoad();
			for (int i = 0; i < _menus.Length; i++)
			{
				Menu menu = _menus[i];
				_menuList.Add(IconMap.Get(menu), menu switch
				{
					Menu.AuctionList => OffServerAuctionUI.Text("menu_list"),
					Menu.Auction => OffServerAuctionUI.Text("menu_auction"),
					_ => menu.GetName(),
				});
			}
			_menuList.EndLoad();
			_menuList.Clicked += MenuSelected;
		}
		else
		{
			_menus = new Menu[1];
			_menuLinker.gameObject.SetActive(value: false);
			_menuList.gameObject.SetActive(value: false);
		}
		Observable<bool> hasExpired = GameSystem<MarketSystem>.Instance().HasExpired;
		hasExpired.Changed = (Action<bool>)Delegate.Combine(hasExpired.Changed, (Action<bool>)delegate(bool value)
		{
			_onExpiredNotification = value;
			UpdateNotifiactionMarkers();
		});
		GameSystem<MarketSystem>.Instance().OnProductCollectiblePaymentExists += OnProductCollectiblePaymentExists;
		GameSystem<MarketSystem>.Instance().OnProductSold += OnProductSold;
		GameSystem<MarketSystem>.Instance().OnProductPaymentReceived += OnProductPaymentReceived;
		GameSystem<MarketSystem>.Instance().OnProductStateUpdated += OnProductStateUpdated;
		OffServerExchange.Changed += UpdateNotifiactionMarkers;
		_productSoldAlarm = new StackableAlarm<string, ProductSold>("ProductSold", (ProductSold sold) => sold.Item.Id, (ProductSold sold, int count) => (count <= 1) ? T._("<em>{0}</em> 아이템이 팔렸습니다.", sold.Item.Name) : T._("<em>{0}</em> 외 {1}개 아이템이 팔렸습니다.", sold.Item.Name, count - 1), "alarm_market", majorAlarm: true, 1.8f, delegate
		{
			if (!base.IsOpened)
			{
				Open();
			}
			SelectMenuTab(Menu.MyMarket);
		});
		SetChildrenActive(activated: false);
	}

	private void OnDestroy()
	{
		OffServerExchange.Changed -= UpdateNotifiactionMarkers;
	}

	public void OpenAndSearch(OrTagFilter tagFilter, OrTagFilter material, int level = 0)
	{
		Open();
		_goodsList.Open(tagFilter, material, level, instant: true);
	}

	public void OpenAndSearch(string prototype)
	{
		Open();
		_goodsList.Open(prototype, instant: true);
	}

	public void OpenAndSearch(string prototype, int prototypeLevel, string itemTag)
	{
		Open();
		_goodsList.Open(prototype, prototypeLevel, itemTag, instant: true);
	}

	public void SelectTabForTest(int menu)
	{
		SelectMenuTab((Menu)menu);
	}

	public void OpenAuctionTab(OffServerAuctionUI.Tab tab)
	{
		if (!base.IsOpened)
		{
			Open();
		}
		if (base.IsOpened && OffServerAuctionUI.Available)
		{
			_menuList.Select(_menus.IndexOf(Menu.Auction));
			_goodsList.Close();
			_sellItemWidget.Close();
			_historyList.OpenAuction(tab);
			_selectedTab = Menu.Auction;
			_titleWidget.Object.SetTitle(OffServerAuctionPreview.Active ? "ประม\u0e39ล · ต\u0e31วอย\u0e48าง UI" : OffServerAuctionUI.Text("menu_auction"));
			HasBack.Value = false;
		}
	}

	public override bool Close()
	{
		bool num = base.Close();
		if (num)
		{
			OffServerAuctionPreview.End();
		}
		return num;
	}

	private void ShowMainPage(bool reset)
	{
		if (reset)
		{
			if (!OptionSystem.IsMarketEnabled())
			{
				UIManager.SystemMsg(T._("현재 섬 장터를 점검하고 있습니다. 이용에 불편을 드려 죄송합니다."));
			}
			_goodsList.Open(instant: true);
			_historyList.Close(instant: true);
			_sellItemWidget.Close(instant: true);
			_menuList.Select(_menus.IndexOf(Menu.Buy));
			_selectedTab = Menu.Buy;
		}
		Durango.Logic.Explore.Region region = GameManager.Region;
		_titleWidget.Object.SetTitle(T._("{0} 섬", region.Name));
	}

	protected override bool TryOpen()
	{
		if (!base.TryOpen())
		{
			return false;
		}
		ShowMainPage(reset: true);
		_onExpiredNotification = GameSystem<MarketSystem>.Instance().HasExpired.Value;
		_onPaymentPendingNotification = GameSystem<MarketSystem>.Instance().HasCollectiblePayment;
		UpdateNotifiactionMarkers();
		return true;
	}

	protected override bool TryClose()
	{
		if (!_goodsList.Back())
		{
			return false;
		}
		base.TryClose();
		return true;
	}

	private void OpenGoodsList()
	{
		_goodsList.Open(instant: false);
		_historyList.Close();
		_sellItemWidget.Close();
	}

	private void OpenHistoryList(ProductType type)
	{
		_goodsList.Close();
		_historyList.Open(type);
		_sellItemWidget.Close();
	}

	private void OpenSellItem()
	{
		_goodsList.Close();
		_historyList.Close();
		_sellItemWidget.SetAuctionListed(delegate
		{
			OpenAuctionTab(OffServerAuctionUI.Tab.Selling);
		});
		_sellItemWidget.Open();
	}

	private void MenuSelected(int index)
	{
		Menu menu = _menus[index];
		SelectMenuTab(menu);
	}

	private void SelectMenuTab(Menu menu)
	{
		bool value = false;
		switch (menu)
		{
		case Menu.Buy:
			if (_selectedTab == Menu.Buy)
			{
				ShowMainPage(reset: true);
				break;
			}
			_menuList.Select(_menus.IndexOf(Menu.Buy));
			OpenGoodsList();
			value = true;
			break;
		case Menu.MyMarket:
			_menuList.Select(_menus.IndexOf(Menu.MyMarket));
			OpenHistoryList(ProductType.Sold);
			break;
		case Menu.Sell:
			_menuList.Select(_menus.IndexOf(Menu.Sell));
			OpenSellItem();
			break;
		case Menu.Auction:
		case Menu.AuctionList:
			if (!OffServerAuctionUI.Available)
			{
				UIManager.SystemMsg(OffServerAuctionUI.Text(OffServerExchange.ConfigReceived ? "auction_off" : "auction_wait"));
				_menuList.Select(_menus.IndexOf(_selectedTab));
				return;
			}
			_goodsList.Close();
			if (menu == Menu.Auction)
			{
				_menuList.Select(_menus.IndexOf(Menu.Auction));
				_sellItemWidget.Close();
				_historyList.OpenAuction(OffServerAuctionUI.Tab.Browse);
				break;
			}
			_menuList.Select(_menus.IndexOf(Menu.Sell));
			_historyList.Close();
			_sellItemWidget.OpenAuction(delegate
			{
				OpenAuctionTab(OffServerAuctionUI.Tab.Selling);
			});
			menu = Menu.Sell;
			break;
		default:
			throw new ArgumentOutOfRangeException();
		}
		_selectedTab = menu;
		_titleWidget.Object.SetTitle((menu != Menu.Auction) ? T._("{0} 섬", GameManager.Region.Name) : (OffServerAuctionPreview.Active ? "ประม\u0e39ล · ต\u0e31วอย\u0e48าง UI" : OffServerAuctionUI.Text("menu_auction")));
		HasBack.Value = value;
	}

	private void OnProductCollectiblePaymentExists(bool hasCollectablePayment)
	{
		_onPaymentPendingNotification = hasCollectablePayment;
		UpdateNotifiactionMarkers();
		if (base.IsOpened && _historyList.IsOpenedInSoldTab)
		{
			_historyList.RefreshReceiveButtonBar();
		}
	}

	private void OnProductSold(ProductSold sold)
	{
		_productSoldAlarm.Add(sold);
		string chatText = T._("<em>{0}</em> 아이템이 <em>{1}</em>에 팔렸습니다.", sold.Item.Name, Durango.Logic.Item.Inventory.CurrencyFormat(sold.Price, Currency.TStone));
		GameSystem<SocialSystem>.Instance().AddSystemChat(chatText, string.Empty, remainColor: true);
	}

	private void OnProductPaymentReceived(MarketPaymentReceived received)
	{
		string text = ((received.ItemCount <= 1) ? T._("(수수료 제외) <em>{0}</em> 아이템을 팔아 <em>{1}</em>을 받았습니다.", received.FirstItemName, Durango.Logic.Item.Inventory.CurrencyFormat(received.TotalPrice, Currency.TStone)) : T._("(수수료 제외) <em>{0}</em> 외 {1}개의 아이템을 팔아 <em>{2}</em>을 받았습니다.", received.FirstItemName, received.ItemCount - 1, Durango.Logic.Item.Inventory.CurrencyFormat(received.TotalPrice, Currency.TStone)));
		UIManager.SystemMsg(text);
		GameSystem<SocialSystem>.Instance().AddSystemChat(text, string.Empty, remainColor: true);
	}

	private void OnProductStateUpdated(ProductStateUpdated updated)
	{
		if (updated.State == ProductState.PaymentReceived && base.IsOpened && _historyList.IsOpenedInSoldTab)
		{
			_historyList.PaymentReceived(updated.ProductId);
		}
	}

	private void UpdateNotifiactionMarkers()
	{
		bool flag = OffServerExchange.PendingClaims > 0;
		_notification.On = _onExpiredNotification || _onPaymentPendingNotification || flag;
		if (!(_menuList == null) && _menus != null)
		{
			_menuList.SetNotification(_menus.IndexOf(Menu.MyMarket), _onExpiredNotification || _onPaymentPendingNotification, _notification.Type);
			if (_menus.IndexOf(Menu.Auction) >= 0)
			{
				_menuList.SetNotification(_menus.IndexOf(Menu.Auction), flag, _notification.Type);
			}
			_historyList.SetNotification(ProductType.Expired, _onExpiredNotification, _notification.Type);
			_historyList.SetNotification(ProductType.Sold, _onPaymentPendingNotification, _notification.Type);
		}
	}
}

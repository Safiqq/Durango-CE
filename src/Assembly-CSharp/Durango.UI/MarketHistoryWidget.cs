using System;
using Durango.Logic.Item;
using Durango.Logic.Market;
using Durango.Logic.Notification;
using Durango.UI.Control;
using Durango.Utils.Extensions;
using L10N;
using Messages;
using NestedPrefab;
using Shared.Economy;
using UnityEngine;

namespace Durango.UI;

public class MarketHistoryWidget : AnimationWidget, IUIInitializable, IScreenResizeReceiver
{
	private static readonly ProductType[] ProductTabsOrder = new ProductType[5]
	{
		ProductType.Sold,
		ProductType.Purchased,
		ProductType.Registered,
		ProductType.Expired,
		ProductType.Favorites
	};

	[SerializeField]
	private NestedPrefabLinker _tabLinker;

	[SerializeField]
	private CommodityList _commodityList;

	[SerializeField]
	private AnimationWidget _buttonContainer;

	[SerializeField]
	private SelectableButton _actionButton;

	[SerializeField]
	private SelectableButton _searchButton;

	[SerializeField]
	private MarketFavoritesButton _favoritesButton;

	[SerializeField]
	private GameObject _receiveButtonBar;

	[SerializeField]
	private SelectableButton _receiveAllButton;

	[SerializeField]
	private SelectableButton _searchButtonInReceiveBar;

	[SerializeField]
	private RectLayout _layout;

	private ProductType _currentTab;

	private HorizontalTabList _tabList;

	private readonly Durango.Logic.Market.Commodities _commodities = new Durango.Logic.Market.Commodities();

	private bool _isOpen;

	private bool _offServerAuction;

	private OffServerAuctionUI.Tab _auctionTab;

	private string _receiveBarText;

	private string _receiveAllText;

	public bool OffServerAuctionMode
	{
		get
		{
			if (_isOpen)
			{
				return _offServerAuction;
			}
			return false;
		}
	}

	public bool IsOpenedInSoldTab
	{
		get
		{
			if (_isOpen)
			{
				return _currentTab == ProductType.Sold;
			}
			return false;
		}
	}

	void IUIInitializable.Init()
	{
		_commodityList.Init();
		_commodityList.SetScrollEndPadding(_buttonContainer.Widget.height);
		_commodityList.CommoditySelected += OnCommoditySelected;
		_commodities.GoodsListUpdated += OnUpdateGoodsList;
		_commodities.Request.Condition = _commodityList.GetSortCondition();
		SelectableButton actionButton = _actionButton;
		actionButton.Clicked = (Action)Delegate.Combine(actionButton.Clicked, new Action(OnActionButtonClicked));
		SelectableButton searchButton = _searchButton;
		searchButton.Clicked = (Action)Delegate.Combine(searchButton.Clicked, new Action(OnSearchButtonClicked));
		SelectableButton searchButtonInReceiveBar = _searchButtonInReceiveBar;
		searchButtonInReceiveBar.Clicked = (Action)Delegate.Combine(searchButtonInReceiveBar.Clicked, new Action(OnSearchButtonClicked));
		SelectableButton receiveAllButton = _receiveAllButton;
		receiveAllButton.Clicked = (Action)Delegate.Combine(receiveAllButton.Clicked, new Action(OnReceiveAllButtonClicked));
		_tabList = _tabLinker.Object.GetComponent<HorizontalTabList>();
		LoadMarketTabs();
		_tabList.Clicked += delegate(int index)
		{
			if (_offServerAuction)
			{
				if (index >= 0 && index < OffServerAuctionUI.Tabs.Length)
				{
					SelectAuctionTab(OffServerAuctionUI.Tabs[index]);
				}
			}
			else
			{
				SelectTab(ProductTabsOrder[index]);
			}
		};
		OffServerAuctionUI.Updated += OnAuctionUpdated;
	}

	private void OnDestroy()
	{
		OffServerAuctionUI.Updated -= OnAuctionUpdated;
	}

	private void LoadMarketTabs()
	{
		_tabList.BeginLoad();
		for (int i = 0; i < ProductTabsOrder.Length; i++)
		{
			ProductType productType = ProductTabsOrder[i];
			string text = productType switch
			{
				ProductType.Registered => T._("등록된 물품"),
				ProductType.Purchased => T._("구매 내역"),
				ProductType.Sold => T._("판매 내역"),
				ProductType.Expired => T._("만료된 물품"),
				ProductType.Favorites => T._("찜 목록"),
				_ => productType.ToString(),
			};
			_tabList.AddText(text);
		}
		_tabList.EndLoadByFixedSize(150);
	}

	public void OpenAuction(OffServerAuctionUI.Tab tab)
	{
		_isOpen = true;
		SetAlpha(1f);
		base.gameObject.SetActive(value: true);
		if (!_offServerAuction)
		{
			_offServerAuction = true;
			_tabList.BeginLoad();
			for (int i = 0; i < OffServerAuctionUI.Tabs.Length; i++)
			{
				_tabList.AddText(OffServerAuctionUI.TabName(OffServerAuctionUI.Tabs[i]));
			}
			_tabList.EndLoadByFixedSize(150);
			UISpriteLabel componentInChildren = _receiveButtonBar.GetComponentInChildren<UISpriteLabel>(includeInactive: true);
			_receiveBarText = ((componentInChildren != null) ? componentInChildren.text : null);
			_receiveAllText = _receiveAllButton.Text;
			_receiveAllButton.Text = OffServerAuctionUI.Text("claim_all");
		}
		SelectAuctionTab(tab);
	}

	private void LeaveAuction()
	{
		if (_offServerAuction)
		{
			_offServerAuction = false;
			LoadMarketTabs();
			UISpriteLabel componentInChildren = _receiveButtonBar.GetComponentInChildren<UISpriteLabel>(includeInactive: true);
			if (componentInChildren != null && _receiveBarText != null)
			{
				componentInChildren.text = _receiveBarText;
			}
			if (_receiveAllText != null)
			{
				_receiveAllButton.Text = _receiveAllText;
			}
			_actionButton.gameObject.SetActive(value: true);
			_actionButton.Disabled = false;
			_searchButton.gameObject.SetActive(value: true);
		}
	}

	private void SelectAuctionTab(OffServerAuctionUI.Tab tab)
	{
		_auctionTab = tab;
		_tabList.Select(Array.IndexOf(OffServerAuctionUI.Tabs, tab));
		RefreshReceiveButtonBar();
		_commodityList.SetLoading();
		_commodityList.SetAuctionProductType(OffServerAuctionUI.ProductTypeOf(tab));
		OffServerAuctionUI.Load(tab);
	}

	public void ReloadAuction()
	{
		if (OffServerAuctionMode)
		{
			OffServerAuctionUI.Load(_auctionTab);
		}
	}

	private void OnAuctionUpdated()
	{
		if (!(this == null) && _isOpen && _offServerAuction)
		{
			if (OffServerAuctionUI.IsLoading)
			{
				_commodityList.SetLoading();
			}
			else
			{
				_commodityList.SetOffServer(OffServerAuctionUI.Data);
			}
			OnCommoditySelected(_commodityList.Selected);
			RefreshReceiveButtonBar();
			_tabList.SetNotification(Array.IndexOf(OffServerAuctionUI.Tabs, OffServerAuctionUI.Tab.Claims), OffServerAuctionUI.MoneyWaiting > 0 || OffServerAuctionUI.ItemClaims > 0, Durango.Logic.Notification.Type.Important);
		}
	}

	public void Open(ProductType type)
	{
		LeaveAuction();
		_isOpen = true;
		SetAlpha(1f);
		base.gameObject.SetActive(value: true);
		SelectTab(type);
	}

	public void Close(bool instant = false)
	{
		if (instant || _isOpen)
		{
			_isOpen = false;
			if (instant)
			{
				base.gameObject.SetActive(value: false);
				SetAlpha(0f, useTween: false);
			}
			else
			{
				base.Delay = 0f;
				base.Alpha = 0f;
			}
		}
	}

	private void SelectTab(ProductType tabType)
	{
		_currentTab = tabType;
		RefreshReceiveButtonBar();
		_tabList.Select(ProductTabsOrder.IndexOf(tabType));
		_commodities.Request.Type = tabType;
		_commodityList.SetLoading();
		_commodityList.SetProductType(_commodities.Request.Type);
		_commodities.Get(reset: true);
	}

	public void RefreshReceiveButtonBar()
	{
		if (_offServerAuction)
		{
			bool flag = _auctionTab == OffServerAuctionUI.Tab.Claims;
			if (flag != _receiveButtonBar.activeSelf)
			{
				_receiveButtonBar.SetActive(flag);
				_layout.UpdateLayout();
				UIUtility.UpdateAnchors(base.transform);
			}
			if (flag)
			{
				UISpriteLabel componentInChildren = _receiveButtonBar.GetComponentInChildren<UISpriteLabel>(includeInactive: true);
				if (componentInChildren != null)
				{
					componentInChildren.text = OffServerAuctionUI.Text("money_wait", OffServerExchange.TStoneText(OffServerAuctionUI.MoneyWaiting), OffServerAuctionUI.ItemClaims);
				}
				_receiveAllButton.Disabled = OffServerAuctionUI.IsLoading || OffServerAuctionUI.IsBusy || (OffServerAuctionUI.MoneyWaiting <= 0 && OffServerAuctionUI.ItemClaims <= 0);
				_searchButtonInReceiveBar.Disabled = true;
			}
		}
		else
		{
			bool flag2 = _currentTab == ProductType.Sold;
			if (flag2 != _receiveButtonBar.activeSelf)
			{
				_receiveButtonBar.SetActive(flag2);
				_layout.UpdateLayout();
				UIUtility.UpdateAnchors(base.transform);
			}
			if (flag2)
			{
				_receiveAllButton.Disabled = !GameSystem<MarketSystem>.Instance().HasCollectiblePayment;
			}
		}
	}

	public void SetNotification(ProductType type, bool on, Durango.Logic.Notification.Type notificationType)
	{
		if (!_offServerAuction)
		{
			int num = ProductTabsOrder.IndexOf(type);
			if (num != -1)
			{
				_tabList.SetNotification(num, on, notificationType);
			}
		}
	}

	public void PaymentReceived(string productId = null)
	{
		_commodityList.PaymentReceived(productId);
		RefreshReceiveButtonBar();
	}

	private void OnUpdateGoodsList()
	{
		if (_isOpen && !_offServerAuction)
		{
			_commodityList.Set(_commodities);
			OnCommoditySelected(_commodityList.Selected);
		}
	}

	private void OnCommoditySelected(Commodity commodity)
	{
		if (_offServerAuction)
		{
			string text = (OffServerAuctionUI.IsLoading ? null : OffServerAuctionUI.ActionText(commodity));
			_actionButton.Disabled = OffServerAuctionUI.IsLoading || OffServerAuctionUI.IsBusy;
			ShowButton(_auctionTab != OffServerAuctionUI.Tab.Claims);
			_actionButton.gameObject.SetActive(text != null);
			if (text != null)
			{
				_actionButton.Text = text;
			}
			_searchButton.gameObject.SetActive(_auctionTab == OffServerAuctionUI.Tab.Browse);
			_favoritesButton.Set(null, _commodityList.UpdateCommodity);
			return;
		}
		if (commodity == null)
		{
			ShowButton(show: false);
			_searchButtonInReceiveBar.Disabled = true;
			return;
		}
		ProductType currentTab = _currentTab;
		if (currentTab == ProductType.Sold)
		{
			_searchButtonInReceiveBar.Disabled = false;
			return;
		}
		ShowButton(show: true);
		bool flag = currentTab == ProductType.Registered || currentTab == ProductType.Expired || currentTab == ProductType.Favorites;
		_actionButton.gameObject.SetActive(flag);
		if (flag)
		{
			switch (currentTab)
			{
			case ProductType.Registered:
				_actionButton.Text = T._("등록 취소");
				break;
			case ProductType.Expired:
				_actionButton.Text = T._("받기");
				break;
			case ProductType.Favorites:
				_actionButton.Text = T._("<t_stone> {0} 구매", commodity.Price.ToString("N0", T.Culture));
				break;
			}
		}
		_favoritesButton.Set((_currentTab != ProductType.Favorites) ? null : commodity, _commodityList.UpdateCommodity);
	}

	private void ShowButton(bool show)
	{
		if (show)
		{
			_buttonContainer.gameObject.SetActive(value: true);
			_buttonContainer.Alpha = 1f;
		}
		else
		{
			_buttonContainer.Alpha = 0f;
		}
	}

	private void OnActionButtonClicked()
	{
		if (_offServerAuction)
		{
			OffServerAuctionUI.DoAction(_commodityList.Selected);
			return;
		}
		Commodity selected = _commodityList.Selected;
		if (selected == null)
		{
			return;
		}
		switch (_currentTab)
		{
		case ProductType.Registered:
			_commodities.Unregister(selected);
			break;
		case ProductType.Expired:
			_commodities.Withdraw(selected);
			break;
		case ProductType.Favorites:
		{
			ItemData item = selected.GetItem();
			if (item != null)
			{
				UIManager.MessageBox.Show(T._("{0:으로} <em>{1}</em>{1:-을} 구매합니다", Durango.Logic.Item.Inventory.CurrencyFormat(selected.Price, Currency.TStone), item.Name), (Action)delegate
				{
					_commodities.Buy(selected);
				}, (string)null);
			}
			break;
		}
		case ProductType.Purchased:
		case ProductType.Sold:
			break;
		}
	}

	private void OnSearchButtonClicked()
	{
		if (_offServerAuction)
		{
			OffServerAuctionUI.AskSearch(_searchButton.gameObject);
			return;
		}
		Commodity selected = _commodityList.Selected;
		if (selected != null)
		{
			ItemData item = selected.GetItem();
			if (item != null)
			{
				MarketGroup marketGroup = UIManager.FindScript<MarketGroup>();
				marketGroup.Close();
				marketGroup.OpenAndSearch(item.PrototypeId);
			}
		}
	}

	private void OnReceiveAllButtonClicked()
	{
		if (_offServerAuction)
		{
			OffServerAuctionUI.ClaimAll();
			return;
		}
		MarketSystem.Send(default(MarketCollectAllPayments)).On<Products>(delegate
		{
			PaymentReceived();
		});
	}

	void IScreenResizeReceiver.OnChangeScreenSize()
	{
		_layout.UpdateLayout();
	}
}

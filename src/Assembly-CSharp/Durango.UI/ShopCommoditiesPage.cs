using System;
using System.Collections.Generic;
using System.Linq;
using Durango.Logic.Clusters;
using Durango.Logic.Shop;
using Durango.UI.Control;
using L10N;
using UnityEngine;
using Yaml;

namespace Durango.UI;

public class ShopCommoditiesPage : MonoBehaviour, IUIInitializable, RectLayout.ICompatible
{
	public Action<List<Durango.Logic.Shop.Commodity>> ListChanged;

	public Action<ShopCategory> CategorySelected;

	public Action<string> Selected;

	public Action CoinTransferClicked;

	[SerializeField]
	private SelectableButton _coinTransferButton;

	private readonly Dictionary<string, ShopCommodityListBase> _pages = new Dictionary<string, ShopCommodityListBase>();

	private ShopCommodityListBase _currentPage;

	private SelectableButton _treasuryButton;

	private string _lastKey = string.Empty;

	void IUIInitializable.Init()
	{
		Transform transform = base.transform;
		for (int i = 0; i < transform.childCount; i++)
		{
			ShopCommodityListBase component = transform.GetChild(i).GetComponent<ShopCommodityListBase>();
			if (!(component == null))
			{
				component.Selected = (Action<string>)Delegate.Combine(component.Selected, new Action<string>(OnSelectCommodity));
				component.CategorySelected = (Action<ShopCategory>)Delegate.Combine(component.CategorySelected, new Action<ShopCategory>(OnSelectCategory));
				_pages.Add(component.name, component);
			}
		}
		_coinTransferButton.Text = T._("코인 전송");
		_coinTransferButton.ToPreferredSize();
		SelectableButton coinTransferButton = _coinTransferButton;
		coinTransferButton.Clicked = (Action)Delegate.Combine(coinTransferButton.Clicked, (Action)delegate
		{
			if (CoinTransferClicked != null)
			{
				CoinTransferClicked();
			}
		});
		try
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(_coinTransferButton.gameObject, _coinTransferButton.transform.parent);
			gameObject.name = "OffServerTreasuryButton";
			_treasuryButton = gameObject.GetComponent<SelectableButton>();
			_treasuryButton.Text = OffServerTreasury.Text("button");
			_treasuryButton.ToPreferredSize();
			_treasuryButton.Clicked = OffServerTreasuryUI.Open;
			gameObject.SetActive(value: false);
			OffServerTreasury.Changed += RefreshTreasuryButton;
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
			_treasuryButton = null;
		}
	}

	private void OnDestroy()
	{
		OffServerTreasury.Changed -= RefreshTreasuryButton;
	}

	private void RefreshTreasuryButton()
	{
		if (!(_treasuryButton == null))
		{
			bool flag = OffServerTreasury.Active && (_lastKey == "durangoCoin" || _lastKey == "gem" || _lastKey == "r_piece");
			_treasuryButton.gameObject.SetActive(flag);
			if (flag)
			{
				_treasuryButton.Text = OffServerTreasury.Text("button");
				_treasuryButton.ToPreferredSize();
				PlaceTreasuryButton();
			}
		}
	}

	private void PlaceTreasuryButton()
	{
		if (!(_treasuryButton == null) && _treasuryButton.gameObject.activeSelf)
		{
			Vector3 pos = GetComponent<UIWidget>().localCorners[2];
			if (_coinTransferButton.gameObject.activeSelf)
			{
				pos.x -= _coinTransferButton.Widget.width + 12;
			}
			_treasuryButton.Widget.SetPosition(pos, 1f, 1f);
		}
	}

	public void Set(ShopCategory category, List<Durango.Logic.Shop.Commodity> commodities, bool reset)
	{
		string text = category.ViewType;
		if (string.IsNullOrEmpty(text) || !_pages.ContainsKey(text))
		{
			text = _pages.Keys.First();
		}
		ShopCommodityListBase shopCommodityListBase = null;
		foreach (KeyValuePair<string, ShopCommodityListBase> page in _pages)
		{
			if (page.Key == text)
			{
				shopCommodityListBase = page.Value;
				page.Value.gameObject.SetActive(value: true);
			}
			else
			{
				page.Value.gameObject.SetActive(value: false);
			}
		}
		SetCoinTransferButton(category.Key);
		if (!(shopCommodityListBase == null))
		{
			shopCommodityListBase.SetList(commodities, reset);
			_currentPage = shopCommodityListBase;
			_currentPage.RefreshCategoryNotification();
			base.gameObject.SetActive(value: true);
			if (ListChanged != null)
			{
				ListChanged(shopCommodityListBase.CurrentList);
			}
		}
	}

	public void SelectAndMoveTo(string id)
	{
		if (!(_currentPage == null))
		{
			_currentPage.SelectAndMoveTo(id);
		}
	}

	public void SetSubCategories(List<ShopCategory> categories, ShopCategory selected)
	{
		_currentPage.SetSubCategories(categories, selected);
		RefreshCategoryNotification();
	}

	public void RefreshCategoryNotification()
	{
		if (_currentPage != null)
		{
			_currentPage.RefreshCategoryNotification();
		}
	}

	private void SetCoinTransferButton(string key)
	{
		bool flag = OptionSystem.GetBool("cashshop.coin_transfer_enabled");
		flag &= key == "gem" || key == "r_piece";
		_coinTransferButton.gameObject.SetActive(flag);
		_lastKey = key ?? string.Empty;
		RefreshTreasuryButton();
		OffServerTreasury.FetchIfStale();
	}

	private void OnSelectCommodity(string commodity)
	{
		if (Selected != null)
		{
			Selected(commodity);
		}
	}

	private void OnSelectCategory(ShopCategory category)
	{
		if (CategorySelected != null)
		{
			CategorySelected(category);
		}
	}

	Vector2 RectLayout.ICompatible.UpdateLayout(float? x, float? y)
	{
		UIWidget component = GetComponent<UIWidget>();
		Point2 point = new Point2(x.HasValue ? ((int)x.Value) : component.width, y.HasValue ? ((int)y.Value) : component.height);
		component.SetDimensions(point.x, point.y);
		if (_currentPage != null)
		{
			_currentPage.UpdateLayout();
		}
		if (_coinTransferButton.gameObject.activeSelf)
		{
			_coinTransferButton.Widget.SetPosition(component.localCorners[2], 1f, 1f);
		}
		PlaceTreasuryButton();
		return point.ToVector2();
	}
}

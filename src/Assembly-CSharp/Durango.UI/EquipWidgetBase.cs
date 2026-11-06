using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Item;
using Durango.Logic.Shop;
using Durango.UI.Control;
using Durango.UI.Popup;
using L10N;
using NestedPrefab;
using Shared.Item;
using UnityEngine;

namespace Durango.UI;

public class EquipWidgetBase : UIWidget, IUIInitializable
{
	[SerializeField]
	private Selectable _tabForEquipPreset;

	[SerializeField]
	private Selectable _tabForAvatarEquip;

	[SerializeField]
	private GameObject _equipPane;

	[SerializeField]
	private EquipPresetTabListWidget _equipPresetTabListWidget;

	[SerializeField]
	protected GameObject _titleSelector;

	[SerializeField]
	protected UILabel _titleLabel;

	[SerializeField]
	private GameObject _slotContainer;

	[SerializeField]
	private GameObject _infoAvatarEquip;

	[SerializeField]
	private GameObject _infoLockedPreset;

	[SerializeField]
	private SelectableButton _buyButton;

	[SerializeField]
	protected EquipSlotsWidget _equipSlotWidget;

	[SerializeField]
	protected SelectableButton _equipButton;

	[SerializeField]
	private NestedPrefabLinker _itemListLinker;

	[SerializeField]
	private GameObject _emptyItemList;

	protected ItemList _itemList;

	private bool _equipChanged;

	private bool _waitForChangingPreset;

	public EquipSlotType SelectedEquipPreset { get; private set; }

	public EquipSystem.Slot SelectedSlot => _equipSlotWidget.SelectedSlot;

	public ItemData LastSelected { get; protected set; }

	public Transform GetSlotTransform(EquipSystem.Slot slot)
	{
		EquipSlotBase slot2 = _equipSlotWidget.GetSlot(slot);
		if (slot2 != null)
		{
			return slot2.transform;
		}
		return null;
	}

	public Transform GetItemTransform(TagEvaluator evaluator)
	{
		ItemIconWidget itemIconWidget = _itemList.FindIcon(evaluator);
		if (itemIconWidget != null)
		{
			return itemIconWidget.transform;
		}
		return null;
	}

	public Transform GetEquipButtonTransform()
	{
		return _equipButton.transform;
	}

	public virtual void Init()
	{
		Selectable tabForEquipPreset = _tabForEquipPreset;
		tabForEquipPreset.Clicked = (Action)Delegate.Combine(tabForEquipPreset.Clicked, new Action(TabForEquipPreset_Clicked));
		Selectable tabForAvatarEquip = _tabForAvatarEquip;
		tabForAvatarEquip.Clicked = (Action)Delegate.Combine(tabForAvatarEquip.Clicked, new Action(TabForAvatarEquip_Clicked));
		_equipPresetTabListWidget.TabClicked += EquipPresetTabListWidget_TabClicked;
		UIEventListener uIEventListener = UIEventListener.Get(_titleSelector);
		uIEventListener.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(uIEventListener.onClick, (UIEventListener.VoidDelegate)delegate
		{
			CharacterInfoGroup.ShowTitleSelector(delegate(string id)
			{
				GameSystem<StatisticsSystem>.Instance().SelectTitle(id);
				SetTitle(GameSystem<StatisticsSystem>.Instance().GetTitle(id)?.Name);
			});
		});
		SelectableButton buyButton = _buyButton;
		buyButton.Clicked = (Action)Delegate.Combine(buyButton.Clicked, new Action(BuyButton_Clicked));
		_equipSlotWidget.SlotClicked += EquipSlotWidget_SlotClicked;
		SelectableButton equipButton = _equipButton;
		equipButton.Clicked = (Action)Delegate.Combine(equipButton.Clicked, new Action(ToggleEquipLastSelectedItem));
		_itemList = _itemListLinker.Object.GetComponent<ItemList>();
		_itemList.SelectableCount = 1;
		_itemList.FixedIconSize = true;
		_itemList.EquipmentsSelectable = true;
		_itemList.OnUpdateSelectItem = ItemList_OnUpdateSelectItem;
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (Application.isPlaying)
		{
			GameSystem<EquipSystem>.Instance().EquipmentsUpdated += EquipSystem_EquipmentsUpdated;
			GameSystem<InventorySystem>.Instance().PlayerInventoryUpdated += InventorySystem_PlayerInventoryUpdated;
			ShowLoadingRingInEquipPane(show: false);
			_buyButton.gameObject.SetActive(GameSystem<MenuSystem>.Instance().IsEnabled(MenuType.Shop));
			_equipChanged = false;
			if (PlayerBehavior.LocalPlayer != null)
			{
				SetTitle(PlayerBehavior.LocalPlayer.Title._Title);
			}
			SelectEquipPreset(GameSystem<EquipSystem>.Instance().CurrentEquipPreset);
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (Application.isPlaying && !GameManager.IsSceneClosing)
		{
			GameSystem<EquipSystem>.Instance().EquipmentsUpdated -= EquipSystem_EquipmentsUpdated;
			GameSystem<InventorySystem>.Instance().PlayerInventoryUpdated -= InventorySystem_PlayerInventoryUpdated;
			if (_equipChanged)
			{
				PlayerController.MotionUpdater.Motion("Avatar_Dress");
				_equipChanged = false;
			}
		}
	}

	protected virtual void SetTitle(string title)
	{
		if (string.IsNullOrEmpty(title))
		{
			_titleLabel.text = T._("칭호 없음");
			_titleLabel.color = new Color(1f, 1f, 1f, 0.5f);
		}
		else
		{
			_titleLabel.text = title;
			_titleLabel.color = PresetColor.UIYellow;
		}
	}

	protected virtual void SelectEquipPreset(EquipSlotType presetType)
	{
		CommunityNinePresets.SelectPreset(this, presetType);
	}

	protected void SelectSlot(EquipSystem.Slot slot)
	{
		_equipSlotWidget.SelectSlot(SelectedEquipPreset, slot);
		RefreshItemList();
		LastSelected = null;
		RefreshEquipButton();
	}

	protected void ToggleEquipLastSelectedItem()
	{
		if ((!(Selectable.Current != null) || !Selectable.Current.Disabled) && SelectedSlot != EquipSystem.Slot.Invalid)
		{
			ItemData item = ((LastSelected != null && !LastSelected.IsEquipments) ? LastSelected : null);
			GameSystem<EquipSystem>.Instance().EquipItem(SelectedEquipPreset, SelectedSlot.ToString().ToLower(), item, delegate
			{
				_equipButton.ShowLoadingRing(show: false);
			});
			_itemList.DeselectAllItems(sendEvent: false);
			_equipChanged = true;
			LastSelected = null;
			_equipButton.Disabled = true;
			_equipButton.ShowLoadingRing(show: true);
		}
	}

	private void RefreshEquipOrAvatarTabs()
	{
		CommunityNinePresets.RefreshModes(this);
	}

	protected virtual void RefreshEquipSlotContainer()
	{
		CommunityNinePresets.RefreshBase(this);
	}

	protected void RefreshItemList()
	{
		CommunityNinePresets.RefreshList(this);
	}

	protected void RefreshEquipButton()
	{
		_equipButton.ShowLoadingRing(show: false);
		if (UsableEquipPresetSelected())
		{
			if (LastSelected != null)
			{
				_equipButton.Disabled = false;
				_equipButton.Text = ((!LastSelected.IsEquipments) ? T._("장착") : T._("해제"));
				return;
			}
			EquipSlotBase slot = _equipSlotWidget.GetSlot(SelectedSlot);
			if (slot != null && slot.Item != null)
			{
				_equipButton.Disabled = slot.Disabled;
				_equipButton.Text = T._("해제");
				return;
			}
		}
		_equipButton.Disabled = true;
		_equipButton.Text = T._("장착");
	}

	private bool UsableEquipPresetSelected()
	{
		if (SelectedEquipPreset != EquipSlotType.Invalid && SelectedSlot != EquipSystem.Slot.Invalid && !_waitForChangingPreset)
		{
			return !GameSystem<EquipSystem>.Instance().IsLockedPreset(SelectedEquipPreset);
		}
		return false;
	}

	private void ShowLoadingRingInEquipPane(bool show)
	{
		LoadingRingWidget loadingRing = UIManager.Popup.LoadingRing;
		if (show)
		{
			loadingRing.AttachToWidget(_equipPane);
			loadingRing.ShowInstantly();
		}
		else
		{
			loadingRing.DetachFromWidget(_equipPane);
		}
		_waitForChangingPreset = show;
	}

	protected void TabForEquipPreset_Clicked()
	{
		CommunityNinePresets.ClickGearTab(this);
	}

	protected void TabForAvatarEquip_Clicked()
	{
		CommunityNinePresets.ClickFashionTab(this);
	}

	private void EquipPresetTabListWidget_TabClicked(EquipSlotType presetType)
	{
		if (!GameSystem<EquipSystem>.Instance().IsLockedPreset(presetType))
		{
			ShowLoadingRingInEquipPane(show: true);
			GameSystem<EquipSystem>.Instance().ChangePreset(presetType, delegate
			{
				SetTitle(GameSystem<StatisticsSystem>.Instance().GetTitle(GameSystem<EquipSystem>.Instance().GetCurrentTitleId())?.Name);
				ShowLoadingRingInEquipPane(show: false);
				RefreshEquipSlotContainer();
				RefreshItemList();
				RefreshEquipButton();
			});
		}
		SelectEquipPreset(presetType);
	}

	private void BuyButton_Clicked()
	{
		if (!GameSystem<MenuSystem>.Instance().IsEnabled(MenuType.Shop))
		{
			return;
		}
		EquipSlotType slot = SelectedEquipPreset;
		GameSystem<ShopSystem>.Instance().GetPurchasableCommodities(delegate(List<Commodity> list)
		{
			int num = list.FindIndex(delegate(Commodity x)
			{
				EquipSlotType? slotType = x.Data.SlotType;
				return slotType.GetValueOrDefault() == slot && slotType.HasValue;
			});
			if (num != -1)
			{
				UIManager.FindScript<ShopGroup>().Open(list[num].Id, select: true);
			}
		});
	}

	protected virtual void EquipSlotWidget_SlotClicked(EquipSystem.Slot slot)
	{
		SelectSlot(slot);
	}

	protected virtual void ItemList_OnUpdateSelectItem()
	{
	}

	protected virtual void OnItemIconRightClick()
	{
	}

	private void EquipSystem_EquipmentsUpdated()
	{
		RefreshEquipSlotContainer();
	}

	private void InventorySystem_PlayerInventoryUpdated()
	{
		RefreshItemList();
	}
}

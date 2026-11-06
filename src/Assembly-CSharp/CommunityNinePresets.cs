using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Durango.Development;
using Durango.Logic.Item;
using Durango.Network;
using Durango.UI;
using Durango.UI.Control;
using L10N;
using Messages;
using MsgPack;
using Shared.Item;
using UnityEngine;

public static class CommunityNinePresets
{
	public static EquipSlotType CurrentFashion;

	public static EquipSlotType[] FashionTypes => new EquipSlotType[1].Concat(from n in Enumerable.Range(10, 8)
		select (EquipSlotType)n).ToArray();

	public static bool IsFashion(EquipSlotType p)
	{
		if (p != EquipSlotType.Avatar)
		{
			if (p >= EquipSlotType.Avatar2)
			{
				return p <= EquipSlotType.Avatar9;
			}
			return false;
		}
		return true;
	}

	public static IEnumerable<EquipSlotType> Enumerate(bool includeAvatar)
	{
		if (!includeAvatar)
		{
			return from n in Enumerable.Range(1, 9)
				select (EquipSlotType)n;
		}
		return (from n in Enumerable.Range(0, 18)
			select (EquipSlotType)n).Concat(new EquipSlotType[1] { EquipSlotType.Season2 });
	}

	private static FieldInfo Field(object o, string name)
	{
		Type type = o.GetType();
		while (type != null)
		{
			FieldInfo field = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				return field;
			}
			type = type.BaseType;
		}
		throw new MissingFieldException(o.GetType().FullName, name);
	}

	public static T Get<T>(object o, string name)
	{
		return (T)Field(o, name).GetValue(o);
	}

	private static void SetProperty(object o, string name, object value)
	{
		Type type = o.GetType();
		while (type != null)
		{
			PropertyInfo property = type.GetProperty(name, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				property.GetSetMethod(nonPublic: true).Invoke(o, new object[1] { value });
				return;
			}
			type = type.BaseType;
		}
		throw new MissingMemberException(name);
	}

	private static void Call(object o, string name, params object[] args)
	{
		Type type = o.GetType();
		while (type != null)
		{
			MethodInfo methodInfo = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((MethodInfo m) => m.Name == name && m.GetParameters().Length == args.Length);
			if (methodInfo != null)
			{
				methodInfo.Invoke(o, args);
				return;
			}
			type = type.BaseType;
		}
		throw new MissingMethodException(name);
	}

	public static Equipments ReadEquipments(Unpacker u)
	{
		int num = u.LastReadData.AsInt32();
		u.Read();
		int currentType = u.LastReadData.AsInt32();
		Equipments result = new Equipments
		{
			CurrentType = (EquipSlotType)currentType,
			Presets = new Dictionary<EquipSlotType, EquipmentSlot>()
		};
		u.Read();
		int num2 = u.LastReadData.AsInt32();
		for (int i = 0; i < num2; i++)
		{
			u.Read();
			EquipSlotType key = (EquipSlotType)u.LastReadData.AsInt32();
			u.Read();
			EquipmentSlot value = EquipmentSlot.Unpack(u);
			result.Presets[key] = value;
		}
		CurrentFashion = EquipSlotType.Avatar;
		if (num >= 3)
		{
			u.Read();
			EquipSlotType equipSlotType = (EquipSlotType)u.LastReadData.AsInt32();
			if (IsFashion(equipSlotType))
			{
				CurrentFashion = equipSlotType;
			}
		}
		return result;
	}

	public static void EquipItem(EquipSystem system, ItemData item)
	{
		if (item != null)
		{
			string stringAttribute = item.GetStringAttribute("slot");
			if (!string.IsNullOrEmpty(stringAttribute))
			{
				system.EquipItem(item.HasTag("equipment_avatar") ? CurrentFashion : system.CurrentEquipPreset, stringAttribute, item.IsEquipments ? null : item);
			}
		}
	}

	public static bool IsEquippedItem(EquipSystem system, ItemData item)
	{
		if (item == null)
		{
			return false;
		}
		return system.GetEquipPreset(item.HasTag("equipment_avatar") ? CurrentFashion : system.CurrentEquipPreset)?.SlotItems.Values.Contains(item.Id) ?? false;
	}

	public static EquipSlotType ActiveFashion()
	{
		return CurrentFashion;
	}

	public static void ChangePreset(EquipSystem system, EquipSlotType preset, Action onReply)
	{
		Connections.Frontend.Send(new ChangeEquipSlotType
		{
			SlotType = preset
		}).On<OK>(delegate
		{
			if (!IsFashion(preset))
			{
				SetProperty(system, "CurrentEquipPreset", preset);
			}
			GameSystem<InventorySystem>.Instance().UpdateEquipments(system.CurrentEquipPreset);
			SoundManager.PlayEvent("ui_menu_equip_item_equip_on");
			if (onReply != null)
			{
				onReply();
			}
			if (Field(system, "ChangePresetSucceeded").GetValue(system) is Action action)
			{
				action();
			}
		}).Rest(delegate
		{
			if (onReply != null)
			{
				onReply();
			}
		});
	}

	public static void SelectPreset(EquipWidgetBase widget, EquipSlotType preset)
	{
		SetProperty(widget, "SelectedEquipPreset", preset);
		ConfigureTabs(widget, preset);
		Get<EquipPresetTabListWidget>(widget, "_equipPresetTabListWidget").SelectTab(preset);
		RefreshModes(widget);
		Call(widget, "RefreshEquipSlotContainer");
	}

	public static void RefreshModes(EquipWidgetBase widget)
	{
		bool flag = IsFashion(widget.SelectedEquipPreset);
		Get<Selectable>(widget, "_tabForEquipPreset").Selected = !flag;
		Get<Selectable>(widget, "_tabForAvatarEquip").Selected = flag;
	}

	public static void ConfigureTabs(EquipWidgetBase widget, EquipSlotType preset)
	{
		EquipPresetTabListWidget equipPresetTabListWidget = Get<EquipPresetTabListWidget>(widget, "_equipPresetTabListWidget");
		CommunityNinePresetTabs communityNinePresetTabs = equipPresetTabListWidget.GetComponent<CommunityNinePresetTabs>();
		if (!communityNinePresetTabs)
		{
			communityNinePresetTabs = equipPresetTabListWidget.gameObject.AddComponent<CommunityNinePresetTabs>();
		}
		communityNinePresetTabs.Bind(equipPresetTabListWidget, widget);
		communityNinePresetTabs.Rebuild(preset);
	}

	public static void InitTabs(EquipPresetTabListWidget tabs)
	{
		CommunityNinePresetTabs communityNinePresetTabs = tabs.GetComponent<CommunityNinePresetTabs>();
		if (!communityNinePresetTabs)
		{
			communityNinePresetTabs = tabs.gameObject.AddComponent<CommunityNinePresetTabs>();
		}
		communityNinePresetTabs.Bind(tabs, tabs.GetComponentInParent<EquipWidgetBase>());
	}

	public static void RefreshBase(EquipWidgetBase widget)
	{
		EquipSlotType selectedEquipPreset = widget.SelectedEquipPreset;
		bool flag = selectedEquipPreset != EquipSlotType.Invalid;
		bool flag2 = IsFashion(selectedEquipPreset);
		bool flag3 = GameSystem<EquipSystem>.Instance().IsLockedPreset(selectedEquipPreset);
		bool flag4 = Get<bool>(widget, "_waitForChangingPreset");
		EquipPresetTabListWidget equipPresetTabListWidget = Get<EquipPresetTabListWidget>(widget, "_equipPresetTabListWidget");
		equipPresetTabListWidget.gameObject.SetActive(flag);
		Get<GameObject>(widget, "_titleSelector").SetActive(flag && !flag2 && !flag3 && !flag4);
		GameObject gameObject = Get<GameObject>(widget, "_slotContainer");
		gameObject.SetActive(flag && (flag2 || (!flag3 && !flag4)));
		Get<GameObject>(widget, "_infoAvatarEquip").SetActive(flag && flag2);
		Get<GameObject>(widget, "_infoLockedPreset").SetActive(flag && !flag2 && flag3);
		if (flag)
		{
			ConfigureTabs(widget, selectedEquipPreset);
			IEnumerable<EquipSlotType> enumerable;
			if (!IsFashion(selectedEquipPreset))
			{
				enumerable = Enumerate(includeAvatar: false);
			}
			else
			{
				IEnumerable<EquipSlotType> fashionTypes = FashionTypes;
				enumerable = fashionTypes;
			}
			foreach (EquipSlotType item in enumerable)
			{
				equipPresetTabListWidget.Refresh(item);
			}
			equipPresetTabListWidget.SelectTab(selectedEquipPreset);
		}
		if (gameObject.activeSelf)
		{
			Get<EquipSlotsWidget>(widget, "_equipSlotWidget").RefreshSlots(selectedEquipPreset);
		}
	}

	public static void RefreshPC(EquipWidget_PC widget)
	{
		RefreshBase(widget);
		bool flag = widget.SelectedEquipPreset != EquipSlotType.Invalid;
		bool flag2 = IsFashion(widget.SelectedEquipPreset);
		if (flag)
		{
			Get<GameObject>(widget, "_characterAbilityWidget").SetActive(flag2 || !GameSystem<EquipSystem>.Instance().IsLockedPreset(widget.SelectedEquipPreset));
		}
		Get<GameObject>(widget, "_abilityList").SetActive(!flag || !flag2);
		Get<GameObject>(widget, "_detailShowButton").SetActive(!flag || !flag2);
	}

	public static void RefreshList(EquipWidgetBase widget)
	{
		ItemList itemList = Get<ItemList>(widget, "_itemList");
		EquipSlotType selectedEquipPreset = widget.SelectedEquipPreset;
		EquipSystem.Slot slot = widget.SelectedSlot;
		if (selectedEquipPreset == EquipSlotType.Invalid || slot == EquipSystem.Slot.Invalid || Get<bool>(widget, "_waitForChangingPreset") || GameSystem<EquipSystem>.Instance().IsLockedPreset(selectedEquipPreset))
		{
			itemList.gameObject.SetActive(value: false);
			return;
		}
		bool fashion = IsFashion(selectedEquipPreset);
		string slotName = slot.ToString().ToLowerInvariant();
		itemList.DeselectAllItems(sendEvent: false);
		itemList.SetItemList(GameSystem<InventorySystem>.Instance().PlayerItemList, delegate(ItemData item)
		{
			if (fashion != item.HasTag("equipment_avatar"))
			{
				return false;
			}
			return item.HasAttribute("slot", slotName) || (slot == EquipSystem.Slot.Main && item.HasAttribute("slot", "both")) || (slot == EquipSystem.Slot.Body && item.HasAttribute("slot", "hoody"));
		});
		Get<GameObject>(widget, "_emptyItemList").SetActive(itemList.UsableCount == 0);
		itemList.gameObject.SetActive(itemList.UsableCount > 0);
		itemList.Reposition(resetPosition: false, tween: false);
	}

	public static void ClickGearTab(EquipWidgetBase widget)
	{
		if (IsFashion(widget.SelectedEquipPreset))
		{
			Call(widget, "SelectEquipPreset", GameSystem<EquipSystem>.Instance().CurrentEquipPreset);
		}
	}

	public static void ClickFashionTab(EquipWidgetBase widget)
	{
		EquipSystem.EquipPreset equipPreset = GameSystem<EquipSystem>.Instance().GetEquipPreset(CurrentFashion);
		if (equipPreset == null || equipPreset.IsHidden)
		{
			UIManager.SystemMsg(T._("현재 섬에서는 외형 장비를 착용할 수 없습니다."));
		}
		else if (!IsFashion(widget.SelectedEquipPreset))
		{
			Call(widget, "SelectEquipPreset", CurrentFashion);
		}
	}

	public static bool Handles(string[] args)
	{
		if (args.Length > 1)
		{
			return args[1] == "presets";
		}
		return false;
	}

	public static IEnumerator UiCommand(string[] args, TestBridge.Result result)
	{
		EquipWidgetBase owner = UnityEngine.Object.FindObjectsByType<EquipWidgetBase>(FindObjectsSortMode.None).FirstOrDefault((EquipWidgetBase w) => w.isActiveAndEnabled);
		if (!owner)
		{
			result.Fail("Open the native character equipment page first");
			yield break;
		}
		EquipPresetTabListWidget equipPresetTabListWidget = Get<EquipPresetTabListWidget>(owner, "_equipPresetTabListWidget");
		List<EquipSlotType> list = Get<List<EquipSlotType>>(equipPresetTabListWidget, "_presetTypes");
		if (args.Length > 3 && args[2] == "scroll")
		{
			if (!int.TryParse(args[3], out var result2) || result2 < 1 || result2 > list.Count)
			{
				result.Fail("Preset index outside the current native tab list");
				yield break;
			}
			equipPresetTabListWidget.ScrollView.MoveToVisibleArea(result2 - 1, instant: false);
			yield return null;
		}
		result.Set("gear", (int)GameSystem<EquipSystem>.Instance().CurrentEquipPreset).Set("fashion", (int)CurrentFashion).Set("selected", (int)owner.SelectedEquipPreset)
			.Set("fashionPage", IsFashion(owner.SelectedEquipPreset))
			.Set("tabs", list.Select((EquipSlotType t) => (int)t).ToArray())
			.Set("tabCount", list.Count);
	}
}

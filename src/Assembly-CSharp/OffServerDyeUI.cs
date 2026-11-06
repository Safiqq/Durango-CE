using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Crafting;
using Durango.Logic.Item;
using Durango.UI;
using Durango.UI.Control;
using Messages;
using Shared.Item;

public static class OffServerDyeUI
{
	private sealed class State
	{
		public readonly DyeSelection Selection = new DyeSelection();

		public bool Syncing;
	}

	private static readonly Dictionary<DyeGroup, State> States = new Dictionary<DyeGroup, State>();

	private static readonly Dictionary<ItemList, State> Lists = new Dictionary<ItemList, State>();

	private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static T Get<T>(object obj, string name)
	{
		return (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(obj);
	}

	private static State StateOf(DyeGroup group)
	{
		if (!States.TryGetValue(group, out var state))
		{
			state = new State();
			States.Add(group, state);
			group.OnCloseSucceed += delegate
			{
				state.Selection.SetTarget(null);
			};
		}
		return state;
	}

	public static void TargetChanged(DyeGroup group)
	{
		State state = StateOf(group);
		ItemData lastSelectedItem = Get<ItemList>(group, "_targetItemList").LastSelectedItem;
		state.Selection.SetTarget(lastSelectedItem?.Id);
		if (lastSelectedItem != null)
		{
			Get<DyePartsWidget>(group, "_dyePartsWidget").Set(lastSelectedItem);
			Get<DyeResultWidget>(group, "_dyeResultWidget").SetModel(lastSelectedItem);
		}
		Refresh(group);
	}

	public static void DyeChanged(DyeGroup group)
	{
		State state = StateOf(group);
		if (state.Syncing)
		{
			return;
		}
		ItemList itemList = Get<ItemList>(group, "_dyeItemList");
		ItemData lastSelectedItem = Get<ItemList>(group, "_targetItemList").LastSelectedItem;
		int selectedPart = Get<DyePartsWidget>(group, "_dyePartsWidget").SelectedPart;
		if (lastSelectedItem == null || !lastSelectedItem.IsDyeable((ColorChannel)selectedPart))
		{
			return;
		}
		state.Selection.SelectChannel(selectedPart);
		if (itemList.LastClickedItem != null)
		{
			state.Selection.Assign(itemList.LastClickedItem.Id);
		}
		else
		{
			string[] array = state.Selection.Snapshot();
			foreach (string id in array)
			{
				if (id != null && !itemList.SelectedList.Any((ItemData x) => x.Id == id))
				{
					state.Selection.Remove(id);
				}
			}
		}
		Refresh(group);
	}

	public static void Refresh(DyeGroup group)
	{
		State state = StateOf(group);
		ItemData lastSelectedItem = Get<ItemList>(group, "_targetItemList").LastSelectedItem;
		ItemList itemList = Get<ItemList>(group, "_dyeItemList");
		DyePartsWidget parts = Get<DyePartsWidget>(group, "_dyePartsWidget");
		DyeResultWidget result = Get<DyeResultWidget>(group, "_dyeResultWidget");
		SelectableButton selectableButton = Get<SelectableButton>(group, "_applyButton");
		state.Selection.SetTarget(lastSelectedItem?.Id);
		Lists[itemList] = state;
		itemList.SelectableCount = -1;
		itemList.MultiIconMode = Durango.UI.ItemIconWidget.MultiIconMode.Index;
		state.Syncing = true;
		itemList.SelectedList.Clear();
		string[] array = state.Selection.Snapshot();
		foreach (string id in array)
		{
			if (id != null)
			{
				ItemData itemData = itemList.FirstOrDefault((ItemData x) => x.Id == id);
				if (itemData == null)
				{
					state.Selection.Remove(id);
				}
				else
				{
					itemList.SelectedList.Add(itemData);
				}
			}
		}
		itemList.UpdateSelectedItems(isScrollToItem: false);
		state.Syncing = false;
		selectableButton.Disabled = lastSelectedItem == null || state.Selection.Count == 0 || OffServerDyeBatch.Active;
		if (lastSelectedItem == null)
		{
			parts.Reset();
			result.SetUnknownModel();
			return;
		}
		ItemColor colors = lastSelectedItem.Colors;
		for (int num = 0; num < 3; num++)
		{
			string id2 = state.Selection[num];
			if (id2 != null)
			{
				ItemData itemData2 = itemList.First((ItemData x) => x.Id == id2);
				if (itemData2.HasTag("decolorizer"))
				{
					colors.Bleaching(num, (GameSystem<RecipeSystem>.Instance().GetBleachingRecipe((ColorChannel)num) as RecipeModify)?.AddColorRate ?? 0f);
				}
				else
				{
					colors.Dyeing(num, itemData2.Colors[0], 0.8f);
				}
			}
		}
		result.SetColor(colors);
		int selected = parts.SelectedPart;
		ItemData itemData3 = ((selected >= 0 && selected < 3) ? itemList.FirstOrDefault((ItemData x) => x.Id == state.Selection[selected]) : null);
		if (itemData3 == null)
		{
			result.ResetEstimate();
			return;
		}
		string targetId = lastSelectedItem.Id;
		string dyeId = itemData3.Id;
		CraftSystem.DyeingEstimate(Get<Artifact>(group, "_workbench"), lastSelectedItem, itemData3, (ColorChannel)selected, delegate(CraftEstimation? estimate)
		{
			if (group.IsOpened && state.Selection.TargetId == targetId && parts.SelectedPart == selected && state.Selection[selected] == dyeId)
			{
				result.SetEstimate(estimate);
			}
		});
	}

	public static void Badge(Durango.UI.ItemIconWidget icon)
	{
		ItemList componentInParent = icon.GetComponentInParent<ItemList>();
		if (!(componentInParent == null) && icon.Item != null && Lists.TryGetValue(componentInParent, out var value))
		{
			int num = value.Selection.ChannelOf(icon.Item.Id);
			if (num >= 0)
			{
				Get<UILabel>(Get<object>(icon, "_multiSelector"), "IndexLabel").text = (num + 1).ToString();
			}
		}
	}

	public static void Apply(DyeGroup group)
	{
		State state = StateOf(group);
		ItemData target = Get<ItemList>(group, "_targetItemList").LastSelectedItem;
		Artifact workbench = Get<Artifact>(group, "_workbench");
		if (target == null || workbench == null || state.Selection.Count == 0 || OffServerDyeBatch.Active)
		{
			return;
		}
		string[] plan = state.Selection.Snapshot();
		Durango.Logic.Item.Inventory playerInventory = GameSystem<InventorySystem>.Instance().PlayerInventory;
		List<ItemData> list = new List<ItemData> { target };
		string[] array = plan;
		foreach (string text in array)
		{
			if (text != null)
			{
				ItemData itemData = playerInventory.Find(text);
				if (itemData == null)
				{
					Refresh(group);
					return;
				}
				list.Add(itemData);
			}
		}
		UIManager.MessageBox.ShowLockConfirm(list.ToArray(), delegate
		{
			Action action = delegate
			{
				OffServerDyeBatch.Begin(workbench, target.Id, plan);
				group.ForceClose();
			};
			if (PlayerBehavior.LocalPlayer.IsRiding)
			{
				VehicleBase.RequestUnmountIfRiding(immediately: true, action);
			}
			else
			{
				action();
			}
		});
	}
}

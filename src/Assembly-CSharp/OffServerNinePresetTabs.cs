using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Durango.UI;
using Shared.Item;
using UnityEngine;

public sealed class OffServerNinePresetTabs : MonoBehaviour
{
	private EquipPresetTabListWidget tabs;

	private EquipWidgetBase owner;

	private EquipSystem system;

	private bool bound;

	public void Bind(EquipPresetTabListWidget target, EquipWidgetBase parent)
	{
		tabs = target;
		owner = parent;
		if (!bound)
		{
			bound = true;
			system = GameSystem<EquipSystem>.Instance();
			tabs.Clicked += Clicked;
			system.EquipmentsUpdated += Updated;
		}
	}

	private void OnDestroy()
	{
		if (bound && system != null)
		{
			system.EquipmentsUpdated -= Updated;
		}
		if ((bool)tabs)
		{
			tabs.Clicked -= Clicked;
		}
	}

	private void Clicked(int index)
	{
		typeof(EquipPresetTabListWidget).GetMethod("EquipSlotTabsWidget_Clicked", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tabs, new object[1] { index });
	}

	private void Updated()
	{
		if ((bool)owner)
		{
			Rebuild(owner.SelectedEquipPreset);
		}
	}

	public void Rebuild(EquipSlotType current)
	{
		List<EquipSlotType> list = OffServerNinePresets.Get<List<EquipSlotType>>(tabs, "_presetTypes");
		IEnumerable<EquipSlotType> source;
		if (!OffServerNinePresets.IsFashion(current))
		{
			source = OffServerNinePresets.Enumerate(includeAvatar: false);
		}
		else
		{
			IEnumerable<EquipSlotType> fashionTypes = OffServerNinePresets.FashionTypes;
			source = fashionTypes;
		}
		EquipSlotType[] array = source.Where((EquipSlotType t) => system.GetEquipPreset(t) != null && !system.GetEquipPreset(t).IsHidden).ToArray();
		if (!list.SequenceEqual(array))
		{
			list.Clear();
			tabs.BeginLoad();
			for (int num = 0; num < array.Length; num++)
			{
				list.Add(array[num]);
				tabs.Add(null, (num + 1).ToString());
			}
			tabs.EndLoad();
		}
	}
}

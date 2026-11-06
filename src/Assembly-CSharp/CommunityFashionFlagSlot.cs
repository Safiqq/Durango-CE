using System;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using Shared.Item;
using UnityEngine;

public sealed class CommunityFashionFlagSlot : MonoBehaviour
{
	private EquipSlotsWidget slots;

	private EquipSlotType preset;

	private GameObject source;

	private GameObject cell;

	private UILabel sourceLabel;

	private UILabel label;

	private UIWidget cellWidget;

	private SelectableWidget sourceSelectable;

	private SelectableWidget cellSelectable;

	private string lastAccessory = "\0";

	public static void Attach(EquipSlotsWidget widget, EquipSlotType presetType)
	{
		if ((bool)widget)
		{
			CommunityFashionFlagSlot communityFashionFlagSlot = widget.GetComponent<CommunityFashionFlagSlot>();
			if (!communityFashionFlagSlot)
			{
				communityFashionFlagSlot = widget.gameObject.AddComponent<CommunityFashionFlagSlot>();
			}
			communityFashionFlagSlot.slots = widget;
			communityFashionFlagSlot.preset = presetType;
			communityFashionFlagSlot.Refresh();
		}
	}

	private void LateUpdate()
	{
		Refresh();
	}

	private void Refresh()
	{
		if (!slots)
		{
			return;
		}
		if (!CommunityNinePresets.IsFashion(preset))
		{
			if ((bool)cell)
			{
				cell.SetActive(value: false);
			}
			return;
		}
		if (!source)
		{
			CharacterInfoGroup componentInParent = slots.GetComponentInParent<CharacterInfoGroup>();
			if (!componentInParent)
			{
				return;
			}
			Transform transform = componentInParent.transform.Find("Container/InfoWidget/CharacterInfo/FlagSlot");
			if (!transform)
			{
				return;
			}
			source = transform.gameObject;
			sourceLabel = source.GetComponentInChildren<UILabel>(includeInactive: true);
			sourceSelectable = source.GetComponent<SelectableWidget>();
		}
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		string text = ((localPlayer != null) ? (localPlayer.Display.Accessory ?? "") : "");
		if (text != lastAccessory)
		{
			lastAccessory = text;
			CharacterInfoWidget componentInParent2 = source.GetComponentInParent<CharacterInfoWidget>();
			if ((bool)componentInParent2)
			{
				typeof(CharacterInfoWidget).GetMethod("SetHonorFlag", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(componentInParent2, new object[1] { text });
			}
		}
		EquipSlotBase slot = slots.GetSlot(EquipSystem.Slot.Head);
		if (!slot)
		{
			return;
		}
		if (!cell)
		{
			cell = UnityEngine.Object.Instantiate(source, slots.transform, worldPositionStays: false);
			cell.name = "FashionFlag";
			cellWidget = cell.GetComponent<UIWidget>();
			label = cell.GetComponentInChildren<UILabel>(includeInactive: true);
			cellSelectable = cell.GetComponent<SelectableWidget>();
			if (!cellWidget)
			{
				throw new InvalidOperationException("Native flag selector has no widget");
			}
			int num = slot.Widget.depth - cellWidget.depth;
			UIWidget[] componentsInChildren = cell.GetComponentsInChildren<UIWidget>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].depth += num;
			}
			cellWidget.SetAnchor((Transform)null);
			cellWidget.pivot = UIWidget.Pivot.Center;
			if ((bool)label)
			{
				label.overflowMethod = UILabel.Overflow.ShrinkContent;
				label.alignment = NGUIText.Alignment.Center;
				label.maxLineCount = 3;
			}
		}
		if (!cell.activeSelf)
		{
			cell.SetActive(value: true);
		}
		cell.transform.localScale = Vector3.one;
		cell.transform.localRotation = Quaternion.identity;
		cell.transform.localPosition = slot.transform.localPosition + Vector3.right * slot.Widget.width;
		cellWidget.width = slot.Widget.width;
		cellWidget.height = slot.Widget.height;
		if ((bool)label && (bool)sourceLabel)
		{
			label.text = sourceLabel.text;
		}
		if ((bool)cellSelectable && (bool)sourceSelectable)
		{
			cellSelectable.Selected = sourceSelectable.Selected;
		}
		UIEventListener uIEventListener = UIEventListener.Get(source);
		UIEventListener.Get(cell).onClick = uIEventListener.onClick;
		if ((bool)cellSelectable)
		{
			cellSelectable.Clicked = null;
		}
		BoxCollider component = cell.GetComponent<BoxCollider>();
		if ((bool)component)
		{
			component.center = Vector3.zero;
			component.size = new Vector3(cellWidget.width, cellWidget.height, 1f);
		}
		cellWidget.UpdateAnchors();
	}
}

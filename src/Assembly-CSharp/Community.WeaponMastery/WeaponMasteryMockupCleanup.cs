using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.UI.Popup;
using UnityEngine;

namespace Community.WeaponMastery;

public sealed class WeaponMasteryMockupCleanup : MonoBehaviour
{
	public FarmingEncyclopediaPopup Owner;

	public string OriginalTitle;

	public readonly List<Action> Restores = new List<Action>();

	private void OnDisable()
	{
		if (Owner == null)
		{
			return;
		}
		WeaponMasteryPreview.HideTooltip();
		for (int num = Restores.Count - 1; num >= 0; num--)
		{
			Restores[num]();
		}
		Restores.Clear();
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
		if (OriginalTitle != null)
		{
			((UILabel)typeof(FarmingEncyclopediaPopup).GetField("_titleLabel", bindingAttr).GetValue(Owner)).text = OriginalTitle;
		}
		Action<int, int> value = (Action<int, int>)Delegate.CreateDelegate(typeof(Action<int, int>), Owner, typeof(FarmingEncyclopediaPopup).GetMethod("OnSelectMastery", bindingAttr));
		FarmingMasteryWidget[] componentsInChildren = Owner.GetComponentsInChildren<FarmingMasteryWidget>(includeInactive: true);
		foreach (FarmingMasteryWidget obj in componentsInChildren)
		{
			typeof(FarmingMasteryWidget).GetField("MasterySelected", bindingAttr).SetValue(obj, value);
		}
		UITexture[] componentsInChildren2 = Owner.GetComponentsInChildren<UITexture>(includeInactive: true);
		foreach (UITexture uITexture in componentsInChildren2)
		{
			if (uITexture.gameObject.name == "Weapon mastery concept icon")
			{
				uITexture.gameObject.SetActive(value: false);
				UnityEngine.Object.Destroy(uITexture.gameObject);
			}
		}
	}
}

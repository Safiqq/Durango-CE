using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Durango.UI;
using UnityEngine;

namespace Community.WeaponMastery;

public static class WeaponMasteryActionIcons
{
	private static readonly Dictionary<string, string> Skills = new Dictionary<string, string>
	{
		{ "onehand_mastery_focus", "sword_onehand_10" },
		{ "onehand_mastery_flurry", "sword_onehand_11" },
		{ "twohand_mastery_cleave", "sword_twohand_10" },
		{ "twohand_mastery_moon", "sword_twohand_11" },
		{ "axe_mastery_break", "axe_onehand_10" },
		{ "axe_mastery_whirl", "axe_onehand_11" }
	};

	private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

	private static float next;

	public static void Tick()
	{
		if (Time.unscaledTime < next)
		{
			return;
		}
		next = Time.unscaledTime + 0.2f;
		BattleActionButton[] array = Resources.FindObjectsOfTypeAll<BattleActionButton>();
		foreach (BattleActionButton battleActionButton in array)
		{
			if (!battleActionButton.gameObject.activeInHierarchy)
			{
				continue;
			}
			UISprite uISprite = (UISprite)typeof(BattleActionButton).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battleActionButton);
			Transform transform = uISprite.transform.Find("Mastery skill drawing");
			if (!Skills.TryGetValue(battleActionButton.Id ?? "", out var skill))
			{
				if (transform != null)
				{
					transform.gameObject.SetActive(value: false);
				}
				continue;
			}
			WeaponMasteryPreview.Skill skill2 = WeaponMasteryPreview.Weapons.SelectMany((WeaponMasteryPreview.Weapon w) => w.skills).First((WeaponMasteryPreview.Skill x) => x.id == skill);
			if (!Textures.TryGetValue(skill, out var value))
			{
				value = Resources.Load<Texture2D>("CommunityMastery/" + skill2.image);
				if (value == null)
				{
					continue;
				}
				Textures[skill] = value;
			}
			if (transform == null)
			{
				GameObject gameObject = new GameObject("Mastery skill drawing");
				gameObject.layer = uISprite.gameObject.layer;
				gameObject.transform.SetParent(uISprite.transform, worldPositionStays: false);
				transform = gameObject.transform;
				gameObject.AddComponent<UITexture>();
			}
			UITexture component = transform.GetComponent<UITexture>();
			component.mainTexture = value;
			component.width = uISprite.width;
			component.height = uISprite.height;
			component.pivot = uISprite.pivot;
			component.depth = uISprite.depth;
			component.color = uISprite.color;
			uISprite.spriteName = null;
			transform.gameObject.SetActive(!CommunityHudLayout.IsHidden(battleActionButton.transform));
		}
	}
}

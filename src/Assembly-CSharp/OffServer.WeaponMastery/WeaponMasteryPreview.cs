using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Durango.UI.Control;
using Durango.UI.Popup;
using Newtonsoft.Json;
using UnityEngine;

namespace OffServer.WeaponMastery;

public static class WeaponMasteryPreview
{
	public sealed class Skill
	{
		public string id;

		public string name;

		public string summary;

		public string effect;

		public string type;

		public string icon;

		public string image;

		public int unlock;

		public int cost;

		public int cooldown;
	}

	public sealed class Weapon
	{
		public string tag;

		public string name;

		public string role;

		public string icon;

		public Skill[] skills;
	}

	private sealed class Design
	{
		public Weapon[] weapons;

		public int maxLevel = 20;
	}

	private sealed class Choice
	{
		public Skill Data;

		public FarmingMasterySelectWidget Native;

		public UISprite Glyph;

		public int Index;
	}

	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private static Design design;

	private static int pageLevel;

	private static bool livePage;

	private static string pageWeaponName;

	private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, Dictionary<int, int>> selections = new Dictionary<string, Dictionary<int, int>>();

	private static WidgetTooltipControl tooltip;

	private static GameObject tooltipOwner;

	private static bool stickyTooltip;

	private static float stickyUntil;

	private static string clickedId;

	private static int clickFrame = -1;

	public static Weapon[] Weapons
	{
		get
		{
			if (design == null)
			{
				design = JsonConvert.DeserializeObject<Design>(Resources.Load<TextAsset>("OffServerMastery/catalog").text);
			}
			return design.weapons;
		}
	}

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, Private).GetValue(o);
	}

	public static void HideTooltip()
	{
		if (tooltip != null)
		{
			tooltip.Hide();
		}
		tooltipOwner = null;
		stickyTooltip = false;
	}

	private static string PathOf(Transform t)
	{
		string text = t.name;
		while (t.parent != null)
		{
			t = t.parent;
			text = t.name + "/" + text;
		}
		return text;
	}

	private static void ShowTooltip(Choice choice, KScrollView scroll, bool sticky)
	{
		Skill data = choice.Data;
		if (sticky)
		{
			scroll.MoveToVisibleArea(choice.Index / 2, instant: true, 0f, 35f);
		}
		tooltip = UIManager.Popup.Tooltip<WidgetTooltipControl>();
		tooltipOwner = choice.Native.gameObject;
		stickyTooltip = sticky;
		stickyUntil = (sticky ? (Time.unscaledTime + 12f) : 0f);
		string text = "Lv. " + data.unlock + " • " + ((data.type == "active") ? "ท\u0e48าพ\u0e34เศษกดใช\u0e49" : "พาสซ\u0e35ฟ");
		if (data.unlock > pageLevel)
		{
			text += " • ย\u0e31งไม\u0e48ปลดล\u0e47อก";
		}
		text = text + "\n\n" + data.effect;
		if (data.type == "active")
		{
			text = text + "\n\nสตาม\u0e34นา " + data.cost + " • ค\u0e39ลดาวน\u0e4c " + data.cooldown + " ว\u0e34นาท\u0e35";
		}
		text += (livePage ? ("\n\nม\u0e35ผลเม\u0e37\u0e48อถ\u0e37อ" + pageWeaponName + " • เล\u0e37อกได\u0e49 1 สก\u0e34ลต\u0e48อช\u0e48วงเลเวล") : "\n\nต\u0e31วอย\u0e48างความสามารถ • ย\u0e31งไม\u0e48ม\u0e35ผลการต\u0e48อส\u0e39\u0e49");
		tooltip.Set(data.name, text, 460, 300);
		tooltip.AutoPosition = false;
		tooltip.Show(sticky ? 12f : 0f);
		tooltip.SetPosition(choice.Glyph, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 12f));
	}

	private static void Refresh(IEnumerable<Choice> choices, Dictionary<int, int> selected)
	{
		foreach (Choice choice in choices)
		{
			int value;
			bool flag = selected.TryGetValue(choice.Data.unlock, out value);
			choice.Native.SetState((choice.Data.unlock > pageLevel) ? FarmingMasterySelectWidget.State.Locked : (flag ? ((value != choice.Index) ? FarmingMasterySelectWidget.State.Unselected : FarmingMasterySelectWidget.State.Acquired) : FarmingMasterySelectWidget.State.Selectable));
		}
	}

	public static void Open(string tag)
	{
		if (!SwordMasteryClient.Live(tag))
		{
			UIManager.SystemMsg("เซ\u0e34ร\u0e4cฟเวอร\u0e4cย\u0e31งไม\u0e48เป\u0e34ดระบบสก\u0e34ลพ\u0e34เศษ");
			return;
		}
		SwordMasteryClient.Open(tag, delegate
		{
			OpenPrepared(tag);
		});
	}

	private static void OpenPrepared(string tag)
	{
		livePage = SwordMasteryClient.Live(tag);
		pageLevel = SwordMasteryClient.Level;
		Weapon weapon = Weapons.First((Weapon w) => w.tag == tag);
		pageWeaponName = weapon.name;
		if (weapon.skills.Length != 8)
		{
			throw new InvalidOperationException("Expected 8 mastery choices");
		}
		FarmingEncyclopediaPopup farmingEncyclopediaPopup = WeaponMasteryMockup.Prepare();
		WeaponMasteryMockupCleanup component = farmingEncyclopediaPopup.GetComponent<WeaponMasteryMockupCleanup>();
		Field<UILabel>(farmingEncyclopediaPopup, "_titleLabel").text = (livePage ? "ความชำนาญอาว\u0e38ธ" : "ความชำนาญอาว\u0e38ธ • ต\u0e31วอย\u0e48าง");
		Field<UILabel>(farmingEncyclopediaPopup, "_nameLabel").text = "<em>" + weapon.name + "</em>  <weak>Lv. " + pageLevel + " / " + design.maxLevel + "</weak>";
		Field<UILabel>(farmingEncyclopediaPopup, "_infoLabel").text = "อ\u0e34งเลเวลสก\u0e34ลต\u0e48อส\u0e39\u0e49ระยะประช\u0e34ด • ปลดล\u0e47อก Lv.5 / 10 / 15 / 20";
		Field<UILabel>(farmingEncyclopediaPopup, "_descriptionLabel").text = "เล\u0e37อก 1 ความสามารถต\u0e48อช\u0e48วงเลเวล • แยกความชำนาญตามอาว\u0e38ธท\u0e31\u0e49ง 3 ชน\u0e34ด";
		Field<ItemIconTex>(farmingEncyclopediaPopup, "_iconTexture").SetIcon((tag == "sword_onehand") ? "weapon_sword_onehand_bone" : ((tag == "lance_twohand") ? "weapon_lance_bamboo" : weapon.icon));
		float num = Mathf.Clamp01((float)pageLevel / 20f);
		typeof(FarmingEncyclopediaPopup).GetField("_expRatio", Private).SetValue(farmingEncyclopediaPopup, num);
		Field<UISprite>(farmingEncyclopediaPopup, "_progressSprite").fillAmount = num;
		if (!selections.TryGetValue(tag, out var selected))
		{
			selections[tag] = (selected = new Dictionary<int, int>());
			for (int num2 = 0; num2 < 4; num2++)
			{
				if (weapon.skills[num2 * 2].unlock <= pageLevel)
				{
					selected[weapon.skills[num2 * 2].unlock] = num2 * 2 + num2 % 2;
				}
			}
		}
		if (livePage)
		{
			selected.Clear();
			for (int num3 = 0; num3 < 8; num3++)
			{
				if ((SwordMasteryClient.Mask & (1 << num3)) != 0)
				{
					selected[weapon.skills[num3].unlock] = num3;
				}
			}
		}
		List<Choice> choices = new List<Choice>();
		KScrollView scroll = Field<KScrollView>(farmingEncyclopediaPopup, "_masteries");
		scroll.Nodes.BeginLoad();
		for (int num4 = 0; num4 < 4; num4++)
		{
			int unlock = weapon.skills[num4 * 2].unlock;
			if (weapon.skills[num4 * 2 + 1].unlock != unlock)
			{
				throw new InvalidOperationException("Each native row requires two choices at the same level");
			}
			FarmingMasteryWidget component2 = scroll.Nodes.GetNext().GetComponent<FarmingMasteryWidget>();
			typeof(FarmingMasteryWidget).GetField("MasterySelected", Private).SetValue(component2, null);
			typeof(FarmingMasteryWidget).GetField("_level", Private).SetValue(component2, unlock);
			Field<UISprite>(component2, "_activateBorder").gameObject.SetActive(unlock <= pageLevel);
			Field<UILabel>(component2, "_levelLabel").text = "Lv. " + unlock;
			FarmingMasterySelectWidget[] array = Field<FarmingMasterySelectWidget[]>(component2, "_masteryWidgets");
			for (int num5 = 0; num5 < 2; num5++)
			{
				int index = num4 * 2 + num5;
				Skill skill = weapon.skills[index];
				FarmingMasterySelectWidget farmingMasterySelectWidget = array[num5];
				GameObject go = farmingMasterySelectWidget.gameObject;
				string originalName = go.name;
				farmingMasterySelectWidget.Set(num5, new KeyValuePair<string, float>("", 0f));
				go.SetActive(value: true);
				go.name = "Weapon mastery choice " + index;
				component.Restores.Add(delegate
				{
					go.name = originalName;
				});
				Field<UILabel>(farmingMasterySelectWidget, "_nameLabel").text = skill.name;
				Field<UILabel>(farmingMasterySelectWidget, "_effectLabel").text = skill.summary;
				UISprite uISprite = Field<UISprite>(farmingMasterySelectWidget, "_iconSprite");
				if (!string.IsNullOrEmpty(skill.image))
				{
					string image = skill.image;
					if (!textures.TryGetValue(image, out var value))
					{
						value = Resources.Load<Texture2D>("OffServerMastery/" + image);
						if (value == null)
						{
							throw new IOException("Missing mastery icon");
						}
						textures[image] = value;
					}
					uISprite.spriteName = null;
					GameObject gameObject = new GameObject("Weapon mastery concept icon");
					gameObject.layer = go.layer;
					gameObject.transform.SetParent(uISprite.transform, worldPositionStays: false);
					UITexture uITexture = gameObject.AddComponent<UITexture>();
					uITexture.mainTexture = value;
					uITexture.width = uISprite.width;
					uITexture.height = uISprite.height;
					uITexture.pivot = uISprite.pivot;
					uITexture.depth = uISprite.depth + 1;
				}
				else
				{
					uISprite.spriteName = skill.icon;
				}
				Choice choice = new Choice
				{
					Data = skill,
					Native = farmingMasterySelectWidget,
					Glyph = uISprite,
					Index = index
				};
				choices.Add(choice);
				UIEventListener listener = UIEventListener.Get(go);
				UIEventListener.VoidDelegate oldClick = listener.onClick;
				UIEventListener.BoolDelegate oldTooltip = listener.onTooltip;
				UIEventListener.BoolDelegate oldHover = listener.onHover;
				component.Restores.Add(delegate
				{
					listener.onClick = oldClick;
					listener.onTooltip = oldTooltip;
					listener.onHover = oldHover;
				});
				listener.onClick = delegate
				{
					if (clickFrame != Time.frameCount || !(clickedId == skill.id))
					{
						clickFrame = Time.frameCount;
						clickedId = skill.id;
						if (livePage && skill.unlock <= pageLevel)
						{
							SwordMasteryClient.Choose(index);
						}
						else if (skill.unlock <= pageLevel)
						{
							selected[skill.unlock] = index;
							Refresh(choices, selected);
						}
						ShowTooltip(choice, scroll, sticky: true);
					}
				};
				listener.onTooltip = delegate(GameObject _, bool show)
				{
					if (show && (!stickyTooltip || Time.unscaledTime >= stickyUntil))
					{
						ShowTooltip(choice, scroll, sticky: false);
					}
					else if (!show && tooltipOwner == go && !stickyTooltip)
					{
						HideTooltip();
					}
				};
				listener.onHover = delegate(GameObject _, bool over)
				{
					if (!over && tooltipOwner == go && !stickyTooltip)
					{
						HideTooltip();
					}
				};
				WeaponMasteryChoiceInput input = go.AddComponent<WeaponMasteryChoiceInput>();
				input.Clicked = delegate
				{
					listener.onClick(go);
				};
				component.Restores.Add(delegate
				{
					input.Clicked = null;
					UnityEngine.Object.Destroy(input);
				});
			}
		}
		if (livePage)
		{
			Action updated = delegate
			{
				selected.Clear();
				for (int i = 0; i < 8; i++)
				{
					if ((SwordMasteryClient.Mask & (1 << i)) != 0)
					{
						selected[weapon.skills[i].unlock] = i;
					}
				}
				Refresh(choices, selected);
			};
			SwordMasteryClient.Changed = updated;
			component.Restores.Add(delegate
			{
				if (SwordMasteryClient.Changed == updated)
				{
					SwordMasteryClient.Changed = null;
				}
			});
			updated();
		}
		Refresh(choices, selected);
		scroll.Nodes.EndLoad();
		farmingEncyclopediaPopup.Show();
		farmingEncyclopediaPopup.transform.localPosition = Vector3.zero;
	}
}

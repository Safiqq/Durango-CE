using System;
using System.Collections.Generic;
using Durango.Logic.Combat;
using Durango.Logic.Item;
using Durango.UI.Control;
using UnityEngine;
using Yaml;

namespace Durango.UI;

internal sealed class CombatLoadoutEditor : MonoBehaviour
{
	private struct Choice
	{
		public string binding;

		public string name;

		public string sprite;

		public ItemData item;
	}

	private SkillGroup owner;

	private UITitleWidget title;

	private UIWidget sp;

	private KWidgetScrollView scroll;

	private UILabel font;

	private GameObject entry;

	private GameObject panel;

	private string[] draft;

	private string profile;

	private int selected;

	private int page;

	private bool items;

	private readonly List<Choice> choices = new List<Choice>();

	private readonly Dictionary<string, Choice> lookup = new Dictionary<string, Choice>();

	private static readonly Color Gold = new Color(1f, 0.85f, 0.48f);

	internal static void Attach(SkillGroup owner, UITitleWidget title, KWidgetScrollView scroll, UILabel font)
	{
		if (title == null || owner.GetComponent<CombatLoadoutEditor>() != null)
		{
			return;
		}
		CombatLoadoutEditor combatLoadoutEditor = owner.gameObject.AddComponent<CombatLoadoutEditor>();
		combatLoadoutEditor.owner = owner;
		combatLoadoutEditor.title = title;
		combatLoadoutEditor.scroll = scroll;
		combatLoadoutEditor.font = font;
		UISprite[] componentsInChildren = title.GetComponentsInChildren<UISprite>(includeInactive: true);
		foreach (UISprite uISprite in componentsInChildren)
		{
			if (uISprite.spriteName == "icon_sp")
			{
				combatLoadoutEditor.sp = uISprite;
				break;
			}
		}
		combatLoadoutEditor.entry = title.gameObject.AddChild();
		combatLoadoutEditor.entry.name = "CombatLoadoutEntry";
		combatLoadoutEditor.Sprite(combatLoadoutEditor.entry, "icon_mainhud_config", -4, 2, 27, 27, 50);
		combatLoadoutEditor.Sprite(combatLoadoutEditor.entry, "action_attack_sword", 7, -4, 21, 24, 51);
		combatLoadoutEditor.Hit(combatLoadoutEditor.entry, "OpenCombatLoadout", 0, 0, 50, 46, combatLoadoutEditor.Toggle);
		owner.OnCloseSucceed += combatLoadoutEditor.CloseEditor;
	}

	private void LateUpdate()
	{
		if (!owner.IsOpened)
		{
			return;
		}
		Vector3[] array = ((sp != null && sp.gameObject.activeInHierarchy) ? sp.worldCorners : title.worldCorners);
		Vector3 vector = ((sp != null && sp.gameObject.activeInHierarchy) ? (title.transform.InverseTransformPoint((array[0] + array[1]) * 0.5f) + Vector3.left * 26f) : (title.transform.InverseTransformPoint((array[2] + array[3]) * 0.5f) + Vector3.left * 174f));
		entry.transform.localPosition = new Vector3(vector.x, vector.y, 0f);
		if (!(panel == null))
		{
			if (profile != CombatLoadout.ProfileKey)
			{
				CloseEditor();
				return;
			}
			Vector3[] worldCorners = scroll.Panel.worldCorners;
			Transform parent = panel.transform.parent;
			Vector3 vector2 = parent.InverseTransformPoint(worldCorners[0]);
			Vector3 vector3 = parent.InverseTransformPoint(worldCorners[2]);
			panel.transform.localPosition = (vector2 + vector3) * 0.5f;
			float num = Mathf.Min(Mathf.Abs(vector3.x - vector2.x) / 1220f, Mathf.Abs(vector3.y - vector2.y) / 620f);
			panel.transform.localScale = Vector3.one * num;
		}
	}

	private void OnDestroy()
	{
		if (owner != null)
		{
			owner.OnCloseSucceed -= CloseEditor;
		}
	}

	private void CloseEditor()
	{
		if (!(panel == null))
		{
			panel.SetActive(value: false);
			UnityEngine.Object.Destroy(panel);
			panel = null;
			scroll.gameObject.SetActive(value: true);
		}
	}

	private void Toggle()
	{
		if (panel != null)
		{
			CloseEditor();
			return;
		}
		draft = CombatLoadout.Draft();
		profile = CombatLoadout.ProfileKey;
		selected = Array.FindIndex(draft, string.IsNullOrEmpty);
		if (selected < 0)
		{
			selected = 0;
		}
		items = false;
		page = 0;
		panel = scroll.transform.parent.gameObject.AddChild();
		panel.name = "CombatLoadout";
		scroll.gameObject.SetActive(value: false);
		Draw();
		LateUpdate();
	}

	private UISprite Sprite(GameObject parent, string id, int x, int y, int w, int h, int depth)
	{
		UISprite uISprite = parent.AddSprite(id, depth);
		if (id == "select_btn_big" || id == "bg_roundbox2")
		{
			uISprite.type = UIBasicSprite.Type.Sliced;
		}
		uISprite.width = w;
		uISprite.height = h;
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		return uISprite;
	}

	private void Text(GameObject parent, string text, int x, int y, int w, int size, bool left = false, bool gold = false)
	{
		UILabel uILabel = parent.AddWidget<UILabel>(45);
		uILabel.bitmapFont = font.bitmapFont;
		if (font.trueTypeFont != null)
		{
			uILabel.trueTypeFont = font.trueTypeFont;
		}
		uILabel.fontSize = size;
		uILabel.width = w;
		uILabel.height = 52;
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.pivot = (left ? UIWidget.Pivot.Left : UIWidget.Pivot.Center);
		uILabel.alignment = (left ? NGUIText.Alignment.Left : NGUIText.Alignment.Center);
		uILabel.color = (gold ? Gold : Color.white);
		uILabel.text = text;
		uILabel.transform.localPosition = new Vector3(x, y, 0f);
	}

	private void Hit(GameObject parent, string name, int x, int y, int w, int h, Action action)
	{
		UIWidget uIWidget = parent.AddWidget<UIWidget>(70);
		uIWidget.name = name;
		uIWidget.width = w;
		uIWidget.height = h;
		uIWidget.transform.localPosition = new Vector3(x, y, 0f);
		NGUITools.AddWidgetCollider(uIWidget.gameObject);
		UIEventListener.Get(uIWidget.gameObject).onClick = delegate
		{
			action();
		};
	}

	private void Button(string id, string label, int x, int y, int w, Action action, bool active = false)
	{
		Sprite(panel, "select_btn_big", x, y, w, 48, 36);
		Text(panel, label, x, y, w - 10, 22, left: false, active);
		Hit(panel, id, x, y, w, 49, action);
	}

	private void Add(Choice c, bool isItem)
	{
		if (!lookup.ContainsKey(c.binding))
		{
			lookup.Add(c.binding, c);
			if (items == isItem)
			{
				choices.Add(c);
			}
		}
	}

	private void Collect()
	{
		choices.Clear();
		lookup.Clear();
		BattleAction[] actionSlots = GameSystem<CombatSystem>.Instance().ActionSlots;
		int num = 0;
		while (actionSlots != null && num < Math.Min(actionSlots.Length, 6))
		{
			if (actionSlots[num] != null)
			{
				PlayerActionMeta meta = actionSlots[num].Data.Meta;
				Add(new Choice
				{
					binding = CombatLoadout.Skill(num),
					name = meta.Name.ToString(),
					sprite = meta.Icon
				}, isItem: false);
			}
			num++;
		}
		foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
		{
			if (CombatLoadout.Consumable(playerItem))
			{
				Add(new Choice
				{
					binding = "i:" + playerItem.PrototypeId,
					name = playerItem.Name,
					item = playerItem
				}, isItem: true);
			}
		}
	}

	private void Icon(Choice c, int x, int y, int size)
	{
		if (c.item != null)
		{
			ItemIconTex itemIconTex = panel.AddWidget<ItemIconTex>(43);
			int width = (itemIconTex.height = size);
			itemIconTex.width = width;
			itemIconTex.transform.localPosition = new Vector3(x, y, 0f);
			itemIconTex.SetIcon(c.item);
		}
		else if (!string.IsNullOrEmpty(c.sprite))
		{
			Sprite(panel, c.sprite, x, y, size, size, 43);
		}
	}

	private void Draw()
	{
		for (int num = panel.transform.childCount - 1; num >= 0; num--)
		{
			GameObject obj = panel.transform.GetChild(num).gameObject;
			obj.SetActive(value: false);
			UnityEngine.Object.Destroy(obj);
		}
		Collect();
		Color color = new Color(0.12f, 0.12f, 0.1f, 0.76f);
		Sprite(panel, "bg_roundbox2", -249, 0, 705, 610, 30).color = color;
		Sprite(panel, "bg_roundbox2", 360, 0, 485, 610, 30).color = color;
		Text(panel, "จ\u0e31ดช\u0e48องต\u0e48อส\u0e39\u0e49", -570, 257, 540, 37, left: true);
		EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
		ItemData itemData = equipSystem.FindEquippedItem(equipSystem.CurrentEquipPreset, "main", "both");
		Text(panel, (itemData == null) ? "ม\u0e37อเปล\u0e48า" : itemData.Name, 145, 257, 420, 33, left: true);
		Button("LoadoutSkills", "ท\u0e48าโจมต\u0e35", -473, 180, 182, delegate
		{
			items = false;
			page = 0;
			Draw();
		}, !items);
		Button("LoadoutItems", "ไอเทมในกระเป\u0e4bา", -244, 180, 238, delegate
		{
			items = true;
			page = 0;
			Draw();
		}, items);
		int pages = Math.Max(1, (choices.Count + 3) / 4);
		page = Math.Min(page, pages - 1);
		for (int num2 = 0; num2 < 4 && page * 4 + num2 < choices.Count; num2++)
		{
			Choice c = choices[page * 4 + num2];
			int y = 99 - 71 * num2;
			Icon(c, -535, y, 47);
			Text(panel, c.name, -480, y, 470, 25, left: true);
			Sprite(panel, "bg_button_arrow", 55, y, 15, 24, 40);
			Hit(panel, "Assign_" + c.binding, -250, y, 675, 65, delegate
			{
				Assign(c.binding);
			});
		}
		if (choices.Count == 0)
		{
			Text(panel, "ไม\u0e48ม\u0e35รายการท\u0e35\u0e48ใช\u0e49ได\u0e49", -240, 45, 630, 24);
		}
		if (pages > 1)
		{
			Button("LoadoutPrev", "‹", -363, -191, 58, delegate
			{
				if (page > 0)
				{
					page--;
				}
				Draw();
			});
			Text(panel, page + 1 + " / " + pages, -247, -191, 120, 20);
			Button("LoadoutNext", "›", -132, -191, 58, delegate
			{
				if (page + 1 < pages)
				{
					page++;
				}
				Draw();
			});
		}
		Text(panel, "เล\u0e37อกช\u0e48อง แล\u0e49วแตะรายการทางซ\u0e49าย", 144, 188, 440, 22, left: true);
		for (int num3 = 0; num3 < 6; num3++)
		{
			int n = num3;
			int num4 = 204 + num3 % 3 * 149;
			int num5 = 82 - num3 / 3 * 147;
			Sprite(panel, "hexagon_grunge_bg", num4, num5, 104, 96, 37);
			Sprite(panel, "hexagon_action_outline", num4, num5, 104, 96, 39).color = ((num3 == selected) ? Gold : Color.white);
			string text = "ว\u0e48าง";
			if (!string.IsNullOrEmpty(draft[num3]))
			{
				if (lookup.TryGetValue(draft[num3], out var value))
				{
					Icon(value, num4, num5, 48);
					text = value.name;
				}
				else
				{
					text = (CombatLoadout.IsItem(draft[num3]) ? "ของหมด" : "ท\u0e48าไม\u0e48พร\u0e49อม");
				}
			}
			else
			{
				Text(panel, "+", num4, num5, 70, 32);
			}
			Text(panel, (num3 + 1).ToString(), num4 + 49, num5 + 34, 38, 18);
			Text(panel, text, num4, num5 - 63, 144, 19, left: false, num3 == selected);
			Hit(panel, "LoadoutSlot" + (num3 + 1), num4, num5 - 7, 142, 134, delegate
			{
				selected = n;
				Draw();
			});
		}
		Text(panel, "กำล\u0e31งจ\u0e31ดช\u0e48อง " + (selected + 1), 145, -192, 250, 23, left: true, gold: true);
		if (!string.IsNullOrEmpty(draft[selected]))
		{
			Button("LoadoutClear", "เอาออก", 508, -192, 144, delegate
			{
				draft[selected] = null;
				Draw();
			});
		}
		Button("LoadoutBack", "กล\u0e31บหน\u0e49าสก\u0e34ล", -473, -260, 202, CloseEditor);
		Button("LoadoutSave", "บ\u0e31นท\u0e36กช\u0e38ด", 496, -260, 170, delegate
		{
			if (profile == CombatLoadout.ProfileKey)
			{
				CombatLoadout.Save(draft);
			}
			CloseEditor();
		}, active: true);
	}

	private void Assign(string value)
	{
		int num = Array.IndexOf(draft, value);
		if (num >= 0)
		{
			draft[num] = draft[selected];
		}
		draft[selected] = value;
		Draw();
	}
}

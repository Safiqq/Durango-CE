using System;
using System.Collections.Generic;
using System.Reflection;
using Converter;
using Durango.UI;
using Durango.UI.Control;
using Newtonsoft.Json;
using UnityEngine;
using Yaml;

namespace Community.WeaponMastery;

public static class MasteryRuntime
{
	private static SelectableButton entry;

	private static SkillGroup owner;

	private static int clickFrame = -1;

	public static void InstallActions(Dictionary<string, PlayerAction> actions)
	{
		TextAsset textAsset = Resources.Load<TextAsset>("CommunityMastery/actions");
		if (textAsset == null)
		{
			return;
		}
		foreach (KeyValuePair<string, PlayerAction> item in JsonConvert.DeserializeObject<Dictionary<string, PlayerAction>>(textAsset.text, Converters.Setting))
		{
			item.Value.Id = item.Key;
			actions[item.Key] = item.Value;
		}
	}

	public static void Tick(CombatSystem combat)
	{
		SwordMasteryClient.Tick(combat);
		NativeComboAnimation.Ensure(PlayerBehavior.LocalPlayer);
		WeaponMasteryActionIcons.Tick();
		MasteryBloodFx.Tick();
	}

	public static void TickEntry(SkillGroup screen)
	{
		if (!screen.IsOpened || !SwordMasteryClient.Live("sword_onehand"))
		{
			if (entry != null)
			{
				entry.gameObject.SetActive(value: false);
			}
			return;
		}
		if (entry == null || owner != screen)
		{
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
			SelectableButton selectableButton = (SelectableButton)typeof(MessageBox).GetField("_baseButton", bindingAttr).GetValue(UIManager.MessageBox);
			if (selectableButton == null)
			{
				return;
			}
			UITitle uITitle = (UITitle)typeof(SkillGroup).GetField("_titleWidget", bindingAttr).GetValue(screen);
			if (uITitle == null || uITitle.Object == null)
			{
				return;
			}
			bool flag = !(uITitle.Object is UITitleWidget_PC);
			Transform learningGuideButton = screen.LearningGuideButton;
			if (entry != null)
			{
				UnityEngine.Object.Destroy(entry.gameObject);
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(selectableButton.gameObject);
			gameObject.SetActive(value: false);
			gameObject.name = "Weapon special skills";
			gameObject.transform.SetParent(learningGuideButton.parent, worldPositionStays: false);
			gameObject.transform.localScale = Vector3.one;
			entry = gameObject.GetComponent<SelectableButton>();
			owner = screen;
			UIWidget widget = entry.Widget;
			UIRect.AnchorPoint leftAnchor = widget.leftAnchor;
			UIRect.AnchorPoint rightAnchor = widget.rightAnchor;
			UIRect.AnchorPoint topAnchor = widget.topAnchor;
			Transform transform = (widget.bottomAnchor.target = null);
			Transform transform3 = (topAnchor.target = transform);
			Transform target = (rightAnchor.target = transform3);
			leftAnchor.target = target;
			entry.SetStyle(PresetButton.Style.Solid);
			entry.SetDimensions(flag ? 240 : 300, flag ? 56 : 64);
			entry.Text = "สก\u0e34ลพ\u0e34เศษ";
			if (flag)
			{
				widget.pivot = UIWidget.Pivot.Center;
				uITitle.Object.SetTitleNext(gameObject.transform, new Vector2(24f + (float)widget.width * 0.5f, 0f));
				gameObject.transform.localScale = Vector3.one;
			}
			else
			{
				gameObject.transform.localPosition = learningGuideButton.localPosition + new Vector3(-330f, 0f, 0f);
			}
			entry.ShortcutCommand = InputCommand.None;
			UIButton component = gameObject.GetComponent<UIButton>();
			if (component != null)
			{
				component.onClick.Clear();
			}
			Action open = delegate
			{
				if (screen.IsOpened && SwordMasteryClient.Live("sword_onehand") && clickFrame != Time.frameCount)
				{
					clickFrame = Time.frameCount;
					UIManager.MessageBox.Show("สก\u0e34ลพ\u0e34เศษ", "เล\u0e37อกอาว\u0e38ธ • ปลดล\u0e47อกตามเลเวลสก\u0e34ลต\u0e48อส\u0e39\u0e49ระยะประช\u0e34ด 5 / 10 / 15 / 20", delegate(int index)
					{
						if (index >= 0 && index < 3)
						{
							WeaponMasteryPreview.Open((new string[3] { "sword_onehand", "sword_twohand", "axe_onehand" })[index]);
						}
					}, new MessageBox.Button("ดาบม\u0e37อเด\u0e35ยว"), new MessageBox.Button("ดาบสองม\u0e37อ"), new MessageBox.Button("ขวานม\u0e37อเด\u0e35ยว"), new MessageBox.Button("ป\u0e34ด", PresetButton.Style.Border));
				}
			};
			entry.Clicked = open;
			UIEventListener.Get(gameObject).onClick = delegate
			{
				open();
			};
		}
		entry.gameObject.SetActive(value: true);
	}
}

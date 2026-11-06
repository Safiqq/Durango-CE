using System;
using System.Collections.Generic;
using System.Linq;
using Durango.Logic.Item;
using Durango.Logic.Skill;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using Shared.Player;
using Shared.Skill;
using UnityEngine;
using Yaml;
using Yaml.Util;

public class CommunityCareerUI : MonoBehaviour
{
	private const int W = 1100;

	private const int H = 680;

	private const int RowWidth = 300;

	private const int RowHeight = 50;

	private const int RowStride = 53;

	private static readonly Color Gold = new Color(1f, 0.82f, 0.36f);

	private static readonly string[] Names = new string[8] { "Engineer", "Office Worker", "Student", "Farmer", "Attendant", "Soldier", "Homemaker", "Job Seeker" };

	private static CommunityCareerUI _window;

	private static SelectableButton _buttonTemplate;

	private static UILabel _labelTemplate;

	private static Durango.UI.Control.ItemIconWidget _iconTemplate;

	private static SelectableWidget _rowTemplate;

	private static SelectionMarker _markerTemplate;

	private readonly List<SelectableWidget> _rows = new List<SelectableWidget>();

	private readonly List<GameObject> _items = new List<GameObject>();

	private JObject _state;

	private JObject _job;

	private GameObject _content;

	private UILabel _name;

	private UILabel _skill;

	private UILabel _description;

	private UILabel _status;

	private SelectionMarker _marker;

	private SelectableButton _confirm;

	private bool _confirming;

	private int _generation;

	private int _itemRevision;

	private ItemInfoTooltip _itemTooltip;

	private GameObject _itemDetailsLayer;

	private readonly Dictionary<string, ItemData> _itemCache = new Dictionary<string, ItemData>();

	private static bool _opening;

	private const int MessageBoxDepth = 7100;

	private readonly List<KeyValuePair<UIPanel, int>> _raisedPanels = new List<KeyValuePair<UIPanel, int>>();

	public static bool BlocksOtherMenus
	{
		get
		{
			if (!_opening)
			{
				return IsOpen;
			}
			return true;
		}
	}

	public static bool IsOpen
	{
		get
		{
			if (_window != null)
			{
				return _window.gameObject.activeSelf;
			}
			return false;
		}
	}

	public static bool ShowWindow(JObject state)
	{
		if (!(state["jobs"] is JArray { Count: not 0 } jArray))
		{
			return false;
		}
		try
		{
			_opening = true;
			int num = 0;
			while (UIBase.CurrentUI != null && num < 32)
			{
				UIBase currentUI = UIBase.CurrentUI;
				currentUI.ForceClose();
				if (UIBase.CurrentUI == currentUI)
				{
					return false;
				}
				num++;
			}
			if (UIBase.CurrentUI != null)
			{
				return false;
			}
			MenuListGroupBase menuListGroupBase = UIManager.FindScript<MenuListGroupBase>();
			if (menuListGroupBase != null && menuListGroupBase.IsOpened)
			{
				menuListGroupBase.Close();
			}
			TooltipBase.HideNonModal(null);
			if (_window == null)
			{
				if (!FindTemplates())
				{
					return false;
				}
				GameObject obj = Durango.Utils.Singleton<UIManager>.Instance().UIRoot.gameObject.AddChild();
				obj.name = "CommunityCareer";
				_window = obj.AddComponent<CommunityCareerUI>();
				_window.Build();
				NGUITools.SetLayer(obj, LayerHelper.UIOverLayer);
			}
			_window._state = state;
			_window._generation = CommunityCareer.Generation;
			_window.gameObject.SetActive(value: true);
			if (Durango.Utils.Singleton<PlayerController>.HasInstance())
			{
				Durango.Utils.Singleton<PlayerController>.Instance().StopMove();
			}
			DialogueGroupBase dialogueGroupBase = UIManager.FindScript<DialogueGroupBase>();
			if (dialogueGroupBase != null)
			{
				dialogueGroupBase.SetVisible(visible: false, "Career");
			}
			_window.Select(((int?)jArray[0]["id"]).GetValueOrDefault());
			_window.Fit();
			return true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			if (_window != null)
			{
				UnityEngine.Object.Destroy(_window.gameObject);
			}
			_window = null;
			return false;
		}
		finally
		{
			_opening = false;
		}
	}

	public static void CloseWindow()
	{
		if (_window != null && _window.gameObject.activeSelf)
		{
			_window.HideItemDetails();
			_window._itemRevision++;
			if (_window._confirming)
			{
				_window._confirming = false;
				UIManager.MessageBox.Hide();
			}
			_window.RestoreMessageBox();
			_window.gameObject.SetActive(value: false);
			DialogueGroupBase dialogueGroupBase = UIManager.FindScript<DialogueGroupBase>();
			if (dialogueGroupBase != null)
			{
				dialogueGroupBase.SetVisible(visible: true, "Career");
			}
		}
	}

	public static void Back()
	{
		if (IsOpen && !CommunityCareer.Busy)
		{
			if (_window._itemTooltip != null && _window._itemTooltip.IsVisible)
			{
				_window.HideItemDetails();
			}
			else if (_window._confirming)
			{
				UIManager.MessageBox.Hide(byBackButton: true);
			}
		}
	}

	public static void Result(string text, bool success)
	{
		if (IsOpen)
		{
			if (success)
			{
				CloseWindow();
				return;
			}
			_window._confirming = false;
			_window._status.text = text;
			_window.RefreshButtons();
		}
	}

	public static bool Click(string key)
	{
		if (!IsOpen)
		{
			return false;
		}
		if (int.TryParse(key, out var result) && result >= 0 && result < _window._rows.Count)
		{
			if (CommunityCareer.Busy)
			{
				return false;
			}
			_window.Select(result);
			return true;
		}
		if (key == "back")
		{
			Back();
			return true;
		}
		SelectableButton selectableButton = ((key == "confirm") ? _window._confirm : null);
		if (selectableButton == null || selectableButton.Disabled)
		{
			return false;
		}
		selectableButton.Clicked();
		return true;
	}

	private static bool FindTemplates()
	{
		MarketGroup marketGroup = UIManager.FindScript<MarketGroup>();
		EditPlayerDisplayGroup editPlayerDisplayGroup = UIManager.FindScript<EditPlayerDisplayGroup>();
		if (marketGroup == null || editPlayerDisplayGroup == null)
		{
			return false;
		}
		_buttonTemplate = marketGroup.GetComponentsInChildren<SelectableButton>(includeInactive: true).FirstOrDefault((SelectableButton x) => x.name == "BuyButton");
		_labelTemplate = marketGroup.GetComponentsInChildren<UILabel>(includeInactive: true).FirstOrDefault((UILabel x) => !(x is UISpriteLabel) && x.fontSize == 26 && (x.trueTypeFont != null || x.bitmapFont != null));
		FactionGroup factionGroup = UIManager.FindScript<FactionGroup>();
		if (factionGroup == null)
		{
			return false;
		}
		_iconTemplate = (from x in factionGroup.GetComponentsInChildren<Durango.UI.Control.ItemIconWidget>(includeInactive: true)
			where x.GetType() == typeof(Durango.UI.Control.ItemIconWidget)
			orderby x.width descending
			select x).FirstOrDefault();
		EditPlayerPresetPage componentInChildren = editPlayerDisplayGroup.GetComponentInChildren<EditPlayerPresetPage>(includeInactive: true);
		if (componentInChildren == null)
		{
			return false;
		}
		_rowTemplate = componentInChildren.GetComponentsInChildren<SelectableWidget>(includeInactive: true).FirstOrDefault((SelectableWidget x) => x.transform.Find("Text") != null);
		_markerTemplate = componentInChildren.GetComponentInChildren<SelectionMarker>(includeInactive: true);
		if (_buttonTemplate != null && _labelTemplate != null && _iconTemplate != null && _rowTemplate != null)
		{
			return _markerTemplate != null;
		}
		return false;
	}

	private void Build()
	{
		base.gameObject.AddComponent<UIPanel>().depth = 5800;
		UISprite uISprite = Sprite(base.gameObject, "bg_white", 0, 0, 6000, 6000, 0);
		uISprite.color = new Color(0f, 0f, 0f, 0.18f);
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		_content = base.gameObject.AddChild();
		_itemDetailsLayer = base.gameObject.AddChild();
		_itemDetailsLayer.name = "CareerItemDetailsLayer";
		_itemDetailsLayer.AddComponent<UIPanel>().depth = 7000;
		Sprite(_content, "bg_roundbox2_space", 0, 0, 1100, 680, 1);
		Sprite(_content, "bg_box_grunge", 0, 0, 1100, 680, 2);
		Sprite(_content, "bg_roundbox2_half", 0, 296, 1092, 80, 3);
		Label("เล\u0e37อกอาช\u0e35พ", -510, 300, 32, Color.white, 850);
		Label("เล\u0e37อกความถน\u0e31ดเร\u0e34\u0e48มต\u0e49นของค\u0e38ณ", -510, 253, 21, new Color(0.72f, 0.72f, 0.72f), 850);
		Sprite(_content, "bg_dotline_white2", 0, 225, 1044, 2, 4);
		UIWidget[] componentsInChildren3;
		for (int i = 0; i < Names.Length; i++)
		{
			int index = i;
			GameObject gameObject = UnityEngine.Object.Instantiate(_rowTemplate.gameObject, _content.transform);
			gameObject.name = "Career_" + i;
			gameObject.transform.localScale = Vector3.one;
			UIRect[] componentsInChildren = gameObject.GetComponentsInChildren<UIRect>(includeInactive: true);
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				componentsInChildren[j].SetAnchor((Transform)null);
			}
			UIDragScrollView[] componentsInChildren2 = gameObject.GetComponentsInChildren<UIDragScrollView>(includeInactive: true);
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				UnityEngine.Object.Destroy(componentsInChildren2[j]);
			}
			componentsInChildren3 = gameObject.GetComponentsInChildren<UIWidget>(includeInactive: true);
			for (int j = 0; j < componentsInChildren3.Length; j++)
			{
				componentsInChildren3[j].depth = 12;
			}
			SelectableWidget component = gameObject.GetComponent<SelectableWidget>();
			component.Widget.pivot = UIWidget.Pivot.Center;
			component.Widget.SetDimensions(300, 50);
			UISprite[] componentsInChildren4 = gameObject.GetComponentsInChildren<UISprite>(includeInactive: true);
			foreach (UISprite obj in componentsInChildren4)
			{
				bool flag = obj.name == "dotline";
				obj.pivot = UIWidget.Pivot.Center;
				obj.SetDimensions(300, flag ? 2 : 50);
				obj.transform.localScale = Vector3.one;
				obj.transform.localPosition = new Vector3(0f, flag ? (-26f) : 0f, 0f);
			}
			component.Clicked = delegate
			{
				Select(index);
			};
			UILabel component2 = gameObject.transform.Find("Text").GetComponent<UILabel>();
			component2.text = Names[i];
			component2.fontSize = 25;
			component2.pivot = UIWidget.Pivot.Left;
			component2.alignment = NGUIText.Alignment.Left;
			component2.SetDimensions(260, 44);
			component2.overflowMethod = UILabel.Overflow.ShrinkContent;
			component2.transform.localScale = Vector3.one;
			component2.transform.localPosition = new Vector3(-130f, 0f, 0f);
			component2.depth = 14;
			gameObject.transform.localPosition = new Vector3(-370f, 190 - 53 * i, 0f);
			gameObject.SetActive(value: true);
			BoxCollider component3 = gameObject.GetComponent<BoxCollider>();
			if (component3 != null)
			{
				component3.center = Vector3.zero;
				component3.size = new Vector3(300f, 50f, 0f);
			}
			_rows.Add(component);
		}
		_marker = UnityEngine.Object.Instantiate(_markerTemplate.gameObject, _content.transform).GetComponent<SelectionMarker>();
		componentsInChildren3 = _marker.GetComponentsInChildren<UIWidget>(includeInactive: true);
		for (int j = 0; j < componentsInChildren3.Length; j++)
		{
			componentsInChildren3[j].depth = 18;
		}
		_name = Label("", -155, 184, 36, Gold, 650);
		_skill = Label("", -155, 126, 28, Color.white, 650);
		_description = Label("", -155, 48, 23, new Color(0.82f, 0.82f, 0.82f), 650);
		_description.height = 108;
		_description.overflowMethod = UILabel.Overflow.ShrinkContent;
		Label("อ\u0e38ปกรณ\u0e4cเร\u0e34\u0e48มต\u0e49น", -155, -35, 23, Color.white, 650);
		Label("แตะไอเทมเพ\u0e37\u0e48อด\u0e39รายละเอ\u0e35ยด • ร\u0e31บของเม\u0e37\u0e48อย\u0e37นย\u0e31นอาช\u0e35พ", -155, -200, 21, new Color(0.7f, 0.7f, 0.7f), 650);
		Sprite(_content, "bg_dotline_white2", 0, -230, 1044, 2, 4);
		_status = Label("", 0, -253, 21, Gold, 1030, UIWidget.Pivot.Center);
		_confirm = Button("confirm", "เล\u0e37อกอาช\u0e35พน\u0e35\u0e49", 390, -302, 280, 66, Confirm, solid: true);
		Label("กร\u0e38ณาเล\u0e37อกอาช\u0e35พเพ\u0e37\u0e48อเร\u0e34\u0e48มต\u0e49นช\u0e35ว\u0e34ตบนเกาะ", -510, -302, 20, new Color(0.65f, 0.65f, 0.65f), 410);
	}

	private void Select(int id)
	{
		if (CommunityCareer.Busy || _confirming || _state == null)
		{
			return;
		}
		_job = (_state["jobs"] as JArray)?.OfType<JObject>().FirstOrDefault((JObject x) => (int?)x["id"] == id);
		if (_job == null)
		{
			return;
		}
		HideItemDetails();
		int revision = ++_itemRevision;
		_confirming = false;
		for (int num = 0; num < _rows.Count; num++)
		{
			_rows[num].Selected = num == id;
		}
		_marker.Set(_rows[id].Widget);
		_name.text = Names[id];
		_skill.text = string.Join(" / ", ((_job["categories"] as JArray) ?? new JArray()).Select((JToken x) => "Lv. " + (int)x["level"] + "  " + Durango.Logic.Skill.Util.CategoryLocalizeName((Shared.Skill.Category)(int)x["id"])));
		Yaml.Job job = SingletonDict<Shared.Player.Job, Yaml.Job>.Get((Shared.Player.Job)id);
		_description.text = ((job != null) ? ((string)job.description) : "");
		foreach (GameObject item in _items)
		{
			item.SetActive(value: false);
			UnityEngine.Object.Destroy(item);
		}
		_items.Clear();
		List<IGrouping<string, JToken>> list = (from x in (_job["items"] as JArray) ?? new JArray()
			group x by (string)x["proto"] + ":" + (int)x["level"]).ToList();
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			JToken jToken = list[num2].First();
			string proto = (string)jToken["proto"];
			int level = (int)jToken["level"];
			int count = list[num2].Count();
			Durango.UI.Control.ItemIconWidget icon = UnityEngine.Object.Instantiate(_iconTemplate.gameObject, _content.transform).GetComponent<Durango.UI.Control.ItemIconWidget>();
			GameObject go = icon.gameObject;
			go.name = "StartingItem_" + proto;
			icon.SetAnchor((Transform)null);
			icon.transform.localScale = Vector3.one * (96f / (float)icon.width);
			icon.transform.localPosition = new Vector3(-107 + num2 * 110, -115f, 0f);
			int num3 = go.GetComponentsInChildren<UIWidget>(includeInactive: true).Min((UIWidget x) => x.depth);
			UIWidget[] componentsInChildren = go.GetComponentsInChildren<UIWidget>(includeInactive: true);
			for (int num4 = 0; num4 < componentsInChildren.Length; num4++)
			{
				componentsInChildren[num4].depth += 8 - num3;
			}
			Transform transform = go.transform.Find("bag");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
			}
			NGUITools.AddWidgetCollider(go);
			go.SetActive(value: false);
			_items.Add(go);
			string cacheKey = proto + ":" + level;
			Action<ItemData> fill = delegate(ItemData data)
			{
				if (!(this == null) && revision == _itemRevision && !(icon == null))
				{
					icon.Set(data, count, alwaysShowCount: true, delegate
					{
						ShowItemDetails(icon, proto, level);
					});
					Transform transform2 = go.transform.Find("Lv");
					if (transform2 != null)
					{
						transform2.gameObject.SetActive(value: true);
					}
					go.SetActive(value: true);
					NGUITools.SetLayer(go, LayerHelper.UIOverLayer);
				}
			};
			if (_itemCache.TryGetValue(cacheKey, out var value))
			{
				fill(value);
				continue;
			}
			PrototypePreset.Request(proto, level, delegate(PrototypePreset preset)
			{
				if (!(this == null) && revision == _itemRevision)
				{
					if (preset == null)
					{
						_status.text = "โหลดรายละเอ\u0e35ยดไอเทมไม\u0e48สำเร\u0e47จ ลองเล\u0e37อกอาช\u0e35พน\u0e35\u0e49อ\u0e35กคร\u0e31\u0e49ง";
					}
					else
					{
						ItemData itemData = preset.ToItem();
						_itemCache[cacheKey] = itemData;
						fill(itemData);
					}
				}
			});
		}
		_status.text = "เล\u0e37อกได\u0e49คร\u0e31\u0e49งเด\u0e35ยว แต\u0e48เร\u0e35ยนท\u0e31กษะสายอ\u0e37\u0e48นได\u0e49";
		NGUITools.SetLayer(base.gameObject, LayerHelper.UIOverLayer);
		RefreshButtons();
	}

	private void Confirm()
	{
		if (_job == null || CommunityCareer.Busy || _confirming || UIManager.MessageBox.IsShow || _generation != CommunityCareer.Generation)
		{
			return;
		}
		HideItemDetails();
		int jobId = (int)_job["id"];
		int generation = _generation;
		_confirming = true;
		RefreshButtons();
		RaiseMessageBox();
		UIManager.MessageBox.Show("ย\u0e37นย\u0e31นเล\u0e37อกอาช\u0e35พ <em>" + Names[jobId] + "</em> หร\u0e37อไม\u0e48?", "<alert_icon/> หล\u0e31งย\u0e37นย\u0e31นจะเปล\u0e35\u0e48ยนอาช\u0e35พไม\u0e48ได\u0e49\nค\u0e38ณจะได\u0e49ร\u0e31บสก\u0e34ลและอ\u0e38ปกรณ\u0e4cเร\u0e34\u0e48มต\u0e49นของอาช\u0e35พน\u0e35\u0e49", delegate(bool ok)
		{
			if (!(this == null))
			{
				_confirming = false;
				RestoreMessageBox();
				if (ok && IsOpen && _window == this && generation == CommunityCareer.Generation && _job != null && (int)_job["id"] == jobId)
				{
					CommunityCareer.Choose(jobId);
				}
				RefreshButtons();
			}
		}, "ย\u0e37นย\u0e31นอาช\u0e35พ", "ยกเล\u0e34ก");
	}

	private void RaiseMessageBox()
	{
		RestoreMessageBox();
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox == null)
		{
			return;
		}
		UIPanel[] componentsInChildren = messageBox.GetComponentsInChildren<UIPanel>(includeInactive: true);
		if (componentsInChildren.Length == 0)
		{
			return;
		}
		int num = 7100 - componentsInChildren.Min((UIPanel x) => x.depth);
		if (num > 0)
		{
			UIPanel[] array = componentsInChildren;
			foreach (UIPanel uIPanel in array)
			{
				_raisedPanels.Add(new KeyValuePair<UIPanel, int>(uIPanel, uIPanel.depth));
				uIPanel.depth += num;
			}
		}
	}

	private void RestoreMessageBox()
	{
		foreach (KeyValuePair<UIPanel, int> raisedPanel in _raisedPanels)
		{
			if (raisedPanel.Key != null)
			{
				raisedPanel.Key.depth = raisedPanel.Value;
			}
		}
		_raisedPanels.Clear();
	}

	private void ShowItemDetails(UIWidget slot, string proto, int level)
	{
		if (!IsOpen || CommunityCareer.Busy)
		{
			return;
		}
		HideItemDetails();
		_itemTooltip = UIManager.Popup.Tooltip<ItemInfoTooltip>();
		ItemInfoTooltip tooltip = _itemTooltip;
		int originalLayer = tooltip.gameObject.layer;
		Transform originalParent = tooltip.transform.parent;
		Vector3 originalPosition = tooltip.transform.localPosition;
		Vector3 originalScale = tooltip.transform.localScale;
		tooltip.transform.SetParent(_itemDetailsLayer.transform, worldPositionStays: false);
		tooltip.transform.localScale = Vector3.one;
		tooltip.Widget.ParentHasChanged();
		tooltip.AddOnFinished(delegate
		{
			if (!(tooltip == null))
			{
				tooltip.transform.SetParent(originalParent, worldPositionStays: false);
				tooltip.transform.localPosition = originalPosition;
				tooltip.transform.localScale = originalScale;
				NGUITools.SetLayer(tooltip.gameObject, originalLayer);
				tooltip.Widget.ParentHasChanged();
			}
		});
		tooltip.Set(proto, level);
		tooltip.Direction = TooltipBase.TooltipDirection.Vertical;
		tooltip.Show(slot, Vector2.zero);
		NGUITools.SetLayer(tooltip.gameObject, LayerHelper.UIOverLayer);
	}

	private void HideItemDetails()
	{
		if (_itemTooltip != null)
		{
			_itemTooltip.Hide();
		}
		_itemTooltip = null;
	}

	private void RefreshButtons()
	{
		bool busy = CommunityCareer.Busy;
		_confirm.Text = (busy ? "กำล\u0e31งย\u0e37นย\u0e31น..." : "เล\u0e37อกอาช\u0e35พน\u0e35\u0e49");
		_confirm.Disabled = busy || _confirming || _job == null;
		foreach (SelectableWidget row in _rows)
		{
			row.Disabled = busy || _confirming;
		}
	}

	private void Update()
	{
		Fit();
	}

	private void Fit()
	{
		float num = Durango.Utils.Singleton<UIManager>.Instance().UIRoot.activeHeight;
		float num2 = num * (float)Screen.width / (float)Math.Max(1, Screen.height);
		float num3 = Mathf.Min(1f, (num - 32f) / 680f, (num2 - 32f) / 1100f);
		_content.transform.localScale = Vector3.one * num3;
	}

	private static UISprite Sprite(GameObject parent, string asset, int x, int y, int w, int h, int depth)
	{
		UISprite uISprite = parent.AddSprite(asset, depth);
		uISprite.SetDimensions(w, h);
		uISprite.color = asset switch
		{
			"bg_roundbox2" => new Color(0f, 0f, 0f, 20f / 51f),
			"bg_roundbox2_space" => new Color(0f, 0f, 0f, 0.47058824f),
			"bg_roundbox2_half" => new Color(0f, 0f, 0f, 28f / 51f),
			"bg_box_grunge" => Color.black,
			"bg_dotline_white2" => new Color(0.44313726f, 0.44313726f, 0.41960785f, 20f / 51f),
			_ => Color.white,
		};
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		return uISprite;
	}

	private UILabel Label(string text, int x, int y, int size, Color color, int width, UIWidget.Pivot pivot = UIWidget.Pivot.Left, GameObject parent = null)
	{
		UILabel uILabel = (parent ?? _content).AddChild().AddComponent<UILabel>();
		uILabel.bitmapFont = _labelTemplate.bitmapFont;
		if (_labelTemplate.trueTypeFont != null)
		{
			uILabel.trueTypeFont = _labelTemplate.trueTypeFont;
		}
		uILabel.fontStyle = _labelTemplate.fontStyle;
		uILabel.effectStyle = _labelTemplate.effectStyle;
		uILabel.effectColor = _labelTemplate.effectColor;
		uILabel.transform.localScale = Vector3.one;
		uILabel.transform.localPosition = new Vector3(x, y, 0f);
		uILabel.pivot = pivot;
		uILabel.alignment = ((pivot == UIWidget.Pivot.Left) ? NGUIText.Alignment.Left : NGUIText.Alignment.Center);
		uILabel.width = width;
		uILabel.height = size + 14;
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.depth = 20;
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.text = text;
		uILabel.transform.localPosition = new Vector3(x, y, 0f);
		uILabel.gameObject.SetActive(value: true);
		return uILabel;
	}

	private SelectableButton Button(string key, string text, int x, int y, int w, int h, Action click, bool solid)
	{
		SelectableButton component = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, _content.transform).GetComponent<SelectableButton>();
		component.name = "Career_" + key;
		component.transform.localScale = Vector3.one;
		component.transform.localPosition = new Vector3(x, y, 0f);
		UIRect[] componentsInChildren = component.GetComponentsInChildren<UIRect>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].SetAnchor((Transform)null);
		}
		UIWidget[] componentsInChildren2 = component.GetComponentsInChildren<UIWidget>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			componentsInChildren2[i].depth += 10;
		}
		component.SetDimensions(w, h);
		component.Text = text;
		component.SetStyle((!solid) ? PresetButton.Style.Border : PresetButton.Style.Solid);
		component.Clicked = click;
		component.Disabled = false;
		component.gameObject.SetActive(value: true);
		return component;
	}
}

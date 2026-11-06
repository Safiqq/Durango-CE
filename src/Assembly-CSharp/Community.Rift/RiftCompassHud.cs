using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Durango.Logic.Item;
using Durango.Render.Camera;
using Durango.Terrain;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Community.Rift;

public sealed class RiftCompassHud : MonoBehaviour
{
	private static RiftCompassHud instance;

	private UIWidget canvas;

	private UIPanel panel;

	private MenuListGroupBase menu;

	private VisibleController visibility;

	private UISprite background;

	private UISprite face;

	private UISprite rose;

	private UISprite tip;

	private UISprite tail;

	private UILabel title;

	private UILabel distance;

	private UILabel status;

	private UILabel snapshot;

	private SelectableButton scan;

	private SelectableButton fold;

	private Transform needle;

	private GameObject body;

	private string terrain;

	private bool found;

	private bool storm;

	private bool collapsed;

	private bool pending;

	private int tx;

	private int tz;

	private int tiles;

	private float expires;

	private float requestUntil;

	private float angle;

	private double sourceAt;

	public static float NeedleAngle(Vector2 delta)
	{
		return Mathf.Atan2(delta.y, delta.x) * 57.29578f - 90f;
	}

	public static bool ValidTile(int x, int z)
	{
		if (x >= 0 && z >= 0 && x <= 4095)
		{
			return z <= 4095;
		}
		return false;
	}

	public static void Receive(JObject json)
	{
		if (!Singleton<UIManager>.HasInstance() || PlayerBehavior.LocalPlayer == null || GameManager.IsTitleScene)
		{
			return;
		}
		string text = (string)json["terrain"];
		if (string.IsNullOrEmpty(text) || text != GameManager.Region.Id)
		{
			return;
		}
		bool flag = json["found"] != null && json["found"].Type == JTokenType.Boolean && (bool)json["found"];
		int result = 0;
		int result2 = 0;
		if (!flag || (json["tx"] != null && json["tz"] != null && json["tx"].Type == JTokenType.Integer && json["tz"].Type == JTokenType.Integer && int.TryParse(json["tx"].ToString(), out result) && int.TryParse(json["tz"].ToString(), out result2) && ValidTile(result, result2)))
		{
			double num = ((json["ttl"] == null) ? 300.0 : json["ttl"].Value<double>());
			if (!double.IsNaN(num) && !double.IsInfinity(num) && !(num <= 0.0) && (!(instance == null) || Build()))
			{
				instance.terrain = text;
				instance.found = flag;
				instance.tx = result;
				instance.tz = result2;
				instance.storm = json["storm"] != null && json["storm"].Type == JTokenType.Boolean && (bool)json["storm"];
				instance.sourceAt = ((json["at"] == null) ? 0.0 : json["at"].Value<double>());
				instance.expires = Time.unscaledTime + (float)Math.Min(num, 3600.0);
				instance.pending = false;
				instance.collapsed = false;
				instance.Refresh();
				UnityEngine.Debug.Log("[Community][RIFT-COMPASS] authoritative snapshot " + text + " found=" + flag + " tile=" + result + "," + result2);
			}
		}
	}

	private static bool Build()
	{
		QuestGroup questGroup = UIManager.FindScript<QuestGroup>();
		if (questGroup == null)
		{
			return false;
		}
		QuestNodeWidget componentInChildren = questGroup.GetComponentInChildren<QuestNodeWidget>(includeInactive: true);
		if (componentInChildren == null)
		{
			return false;
		}
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
		UILabel uILabel = (UILabel)typeof(QuestNodeWidget).GetField("_titleLabel", bindingAttr).GetValue(componentInChildren);
		SelectableButton selectableButton = (SelectableButton)typeof(QuestNodeWidget).GetField("_receiveButton", bindingAttr).GetValue(componentInChildren);
		if (uILabel == null || selectableButton == null)
		{
			return false;
		}
		GameObject gameObject = new GameObject("RiftCompassHud");
		gameObject.SetActive(value: false);
		gameObject.transform.SetParent(Singleton<UIManager>.Instance().UIRoot.transform, worldPositionStays: false);
		RiftCompassHud hud = gameObject.AddComponent<RiftCompassHud>();
		instance = hud;
		hud.panel = gameObject.AddComponent<UIPanel>();
		hud.panel.depth = 2550;
		hud.menu = UIManager.FindScript<MenuListGroupBase>();
		hud.visibility = gameObject.AddComponent<VisibleController>();
		typeof(VisibleController).GetField("_flag", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud.visibility, VisibleType.Base | VisibleType.HideOnRightSide | VisibleType.HideOnLeftMenu | VisibleType.HideOnWorldMap);
		GameObject gameObject2 = new GameObject("NativeCompass", typeof(UIWidget));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		hud.canvas = gameObject2.GetComponent<UIWidget>();
		hud.canvas.width = 320;
		hud.canvas.height = 250;
		UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(UIBase.AnchorType.FullscreenMobileOnly);
		hud.canvas.SetAnchor(rootAnchor.gameObject, 0f, 20, 0f, 110, 0f, 340, 0f, 360);
		hud.background = hud.Sprite("Background", "bg_roundbox2_space", gameObject2.transform, 0, 0, 320, 250, new Color(0.09f, 0.12f, 0.13f, 0.94f), 0);
		hud.title = hud.Label(uILabel, "Target", gameObject2.transform, 14, 10, 240, 30, 20);
		hud.fold = hud.Button(selectableButton, "Fold", gameObject2.transform, 254, 8, 52, 30, delegate
		{
			hud.collapsed = !hud.collapsed;
			hud.Refresh();
		});
		hud.body = new GameObject("Body");
		hud.body.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		hud.face = hud.Sprite("DialFace", "bg_circle_100x100", hud.body.transform, 12, 49, 144, 144, new Color(0.74f, 0.75f, 0.68f, 1f), 10);
		hud.Sprite("DialRing", "img_slide_circle_outside", hud.body.transform, 10, 47, 148, 148, Inventory.RiftCoinColor, 12);
		hud.rose = hud.Sprite("CompassRose", "explore_map_compass", hud.body.transform, 20, 57, 128, 128, Color.white, 13);
		hud.rose.pivot = UIWidget.Pivot.Center;
		hud.rose.transform.localPosition = new Vector3(-76f, 4f, 0f);
		GameObject gameObject3 = new GameObject("Needle");
		gameObject3.transform.SetParent(hud.body.transform, worldPositionStays: false);
		hud.needle = gameObject3.transform;
		hud.needle.localPosition = new Vector3(-76f, 4f, 0f);
		hud.tip = hud.NeedlePart("GoldTip", gameObject3.transform, new Color(1f, 0.83f, 0.32f, 1f), 0f);
		hud.tail = hud.NeedlePart("Counterweight", gameObject3.transform, new Color(0.85f, 0.86f, 0.78f, 1f), 180f);
		hud.Sprite("NeedlePin", "bg_circle_100x100", hud.body.transform, 79, 116, 10, 10, PresetColor.UIYellow, 21);
		hud.distance = hud.Label(uILabel, "Distance", hud.body.transform, 174, 70, 130, 43, 30);
		hud.status = hud.Label(uILabel, "Direction", hud.body.transform, 174, 116, 130, 54, 18);
		hud.status.maxLineCount = 2;
		hud.snapshot = hud.Label(uILabel, "Snapshot", hud.body.transform, 152, 205, 152, 34, 13);
		hud.snapshot.maxLineCount = 2;
		hud.scan = hud.Button(selectableButton, "Scan", hud.body.transform, 14, 202, 128, 36, delegate
		{
			hud.Scan();
		});
		hud.scan.Text = RiftText.T("ค\u0e49นหาใหม\u0e48", "Search again");
		NGUITools.SetLayer(gameObject, questGroup.gameObject.layer);
		gameObject.SetActive(value: true);
		hud.Refresh();
		return true;
	}

	private UISprite NeedlePart(string name, Transform parent, Color color, float turn)
	{
		UISprite uISprite = Sprite(name, "guide_arrow", parent, 0, 0, 56, 16, color, 20);
		uISprite.pivot = UIWidget.Pivot.Center;
		uISprite.transform.localPosition = new Vector3(0f, (turn == 0f) ? 22 : (-22), 0f);
		uISprite.transform.localRotation = Quaternion.Euler(0f, 0f, 90f + turn);
		return uISprite;
	}

	private UISprite Sprite(string name, string key, Transform parent, int x, int y, int width, int height, Color color, int depth)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.SetActive(value: false);
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		UISprite uISprite = gameObject.AddComponent<UISprite>();
		uISprite.spriteName = key;
		uISprite.color = color;
		uISprite.depth = depth;
		Place(uISprite, x, y, width, height);
		gameObject.SetActive(value: true);
		return uISprite;
	}

	private UILabel Label(UILabel source, string name, Transform parent, int x, int y, int width, int height, int size)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(source.gameObject, parent, worldPositionStays: false);
		gameObject.name = name;
		Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		UILabel component = gameObject.GetComponent<UILabel>();
		component.overflowMethod = UILabel.Overflow.ClampContent;
		component.maxLineCount = 1;
		component.fontSize = size;
		component.spacingY = 2;
		component.supportEncoding = false;
		component.text = "";
		component.color = Color.white;
		component.alignment = NGUIText.Alignment.Left;
		component.depth = 30;
		Place(component, x, y, width, height);
		gameObject.SetActive(value: true);
		return component;
	}

	private SelectableButton Button(SelectableButton source, string name, Transform parent, int x, int y, int width, int height, Action click)
	{
		GameObject obj = UnityEngine.Object.Instantiate(source.gameObject, parent, worldPositionStays: false);
		obj.name = name;
		SelectableButton component = obj.GetComponent<SelectableButton>();
		Place(component.Widget, x, y, width, height);
		component.ShortcutCommand = InputCommand.None;
		component.Clicked = click;
		component.SubClicked = null;
		component.Disabled = false;
		component.ClearEffect();
		obj.SetActive(value: true);
		UILabel[] componentsInChildren = obj.GetComponentsInChildren<UILabel>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].fontSize = 18;
		}
		return component;
	}

	private static void Place(UIWidget widget, int x, int y, int width, int height)
	{
		widget.SetAnchor((Transform)null);
		widget.pivot = UIWidget.Pivot.TopLeft;
		widget.transform.localPosition = new Vector3(x - 160, 125 - y, 0f);
		widget.transform.localScale = Vector3.one;
		widget.width = width;
		widget.height = height;
	}

	private void Scan()
	{
		if (!pending && !(PlayerBehavior.LocalPlayer == null) && !(GameManager.Region.Id != terrain))
		{
			ItemData itemData = GameSystem<InventorySystem>.Instance().PlayerItemList.FirstOrDefault((ItemData i) => i.HasTag("dgs02_compass"));
			if (itemData == null)
			{
				status.text = RiftText.T("ไม\u0e48ม\u0e35เข\u0e47มท\u0e34ศในกระเป\u0e4bา", "No compass in bag");
				return;
			}
			pending = true;
			requestUntil = Time.unscaledTime + 6f;
			GameSystem<InventorySystem>.Instance().UseItem(itemData, playerAccepted: true, delegate
			{
			});
			Refresh();
		}
	}

	private void Update()
	{
		if (PlayerBehavior.LocalPlayer == null || GameManager.IsTitleScene || GameManager.Region.Id != terrain || !PlayerBehavior.LocalPlayer.IsAlive)
		{
			Clear();
			return;
		}
		if (Time.unscaledTime >= expires)
		{
			Clear();
			return;
		}
		if (pending && Time.unscaledTime >= requestUntil)
		{
			pending = false;
		}
		bool flag = !UIManager.IsLoadingCurtain && UIBase.CurrentUI == null && (menu == null || !menu.IsOpened) && visibility.Visible && !TooltipBase.HasModal() && (UIManager.MessageBox == null || !UIManager.MessageBox.IsShow);
		panel.alpha = (flag ? 1 : 0);
		if (canvas.gameObject.activeSelf != flag)
		{
			canvas.gameObject.SetActive(flag);
		}
		Refresh();
	}

	private void Refresh()
	{
		if (!(canvas == null))
		{
			body.SetActive(!collapsed);
			background.height = (collapsed ? 48 : 250);
			fold.Text = (collapsed ? RiftText.T("ขยาย", "Open") : RiftText.T("ย\u0e48อ", "Fold"));
			title.text = (storm ? RiftText.T("เข\u0e47มท\u0e34ศ · สายแร\u0e48พาย\u0e38", "Compass · storm vein") : RiftText.T("เข\u0e47มท\u0e34ศ · แร\u0e48ผล\u0e36ก", "Compass · crystal ore"));
			bool flag = found && Time.unscaledTime < expires;
			tip.gameObject.SetActive(flag);
			tail.gameObject.SetActive(flag);
			scan.Disabled = pending;
			if (!flag)
			{
				distance.text = "—";
				status.text = (found ? RiftText.T("ผลค\u0e49นหาหมดอาย\u0e38", "Search expired") : RiftText.T("ไม\u0e48พบแร\u0e48ใกล\u0e49ต\u0e31ว", "No ore nearby"));
			}
			else if (PlayerBehavior.LocalPlayer != null && Singleton<MainCamera>.HasInstance())
			{
				PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
				Vector3 world = Durango.Terrain.Util.TilePositionToClientPosition(new Point2(tx, tz), tileCenter: true);
				Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(localPlayer.CurrentPosition);
				tiles = RiftScope.TileDistance(new Vector2(tx, tz), vector);
				distance.text = tiles + RiftText.T(" ไทล\u0e4c", " tiles");
				Vector3 vector2 = MainCamera.WorldToNGUIPos(localPlayer.CurrentPosition);
				Vector3 vector3 = MainCamera.WorldToNGUIPos(world);
				angle = NeedleAngle(vector3 - vector2);
				needle.localRotation = Quaternion.RotateTowards(needle.localRotation, Quaternion.Euler(0f, 0f, angle), Time.unscaledDeltaTime * 420f);
				Vector3 vector4 = MainCamera.WorldToNGUIPos(Durango.Terrain.Util.TilePositionToClientPosition(vector + Vector2.up, tileCenter: true)) - vector2;
				rose.transform.localRotation = Quaternion.Euler(0f, 0f, NeedleAngle(vector4));
				status.text = ((tiles == 0) ? RiftText.T("ถ\u0e36งตำแหน\u0e48งแล\u0e49ว", "At target") : RiftText.T("ปลายทองช\u0e35\u0e49แร\u0e48", "Gold tip points to ore"));
			}
			if (pending)
			{
				status.text = RiftText.T("กำล\u0e31งค\u0e49นหา…", "Searching…");
			}
			snapshot.text = RiftText.T("ผลค\u0e49นหาล\u0e48าส\u0e38ด\nเหล\u0e37อ {0} ว\u0e34นาท\u0e35", "Last search\n{0}s remaining", Mathf.Max(0, Mathf.CeilToInt(expires - Time.unscaledTime)));
		}
	}

	public static void Clear()
	{
		if (!(instance == null))
		{
			instance.enabled = false;
			UnityEngine.Object.Destroy(instance.gameObject);
			instance = null;
		}
	}

	public static Dictionary<string, object> DebugCommand(string action)
	{
		if (action == "close")
		{
			Clear();
		}
		else if (instance != null && action == "fold")
		{
			instance.collapsed = !instance.collapsed;
			instance.Refresh();
		}
		else if (instance != null && action == "scan")
		{
			instance.Scan();
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object> {
		{
			"open",
			instance != null
		} };
		if (instance == null)
		{
			return dictionary;
		}
		dictionary["terrain"] = instance.terrain;
		dictionary["found"] = instance.found;
		dictionary["targetTx"] = instance.tx;
		dictionary["targetTz"] = instance.tz;
		dictionary["sourceAt"] = instance.sourceAt;
		dictionary["remaining"] = Mathf.Max(0f, instance.expires - Time.unscaledTime);
		dictionary["distance"] = instance.tiles;
		dictionary["angle"] = instance.angle;
		dictionary["needleAngle"] = instance.needle.localEulerAngles.z;
		dictionary["folded"] = instance.collapsed;
		dictionary["visible"] = instance.panel.alpha > 0f && instance.canvas.gameObject.activeInHierarchy;
		dictionary["menuOpen"] = instance.menu != null && instance.menu.IsOpened;
		dictionary["roseSprite"] = instance.rose.spriteName;
		dictionary["roseValid"] = instance.rose.isValid;
		dictionary["needleSprite"] = instance.tip.spriteName;
		dictionary["needleValid"] = instance.tip.isValid;
		dictionary["status"] = instance.status.text;
		dictionary["nativeComponents"] = new string[4] { "UISprite", "UILabel", "SelectableButton", "UIPanel" };
		return dictionary;
	}
}

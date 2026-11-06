using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Logic.Clusters;
using Durango.System;
using Durango.UI;
using Durango.UI.Control;
using UnityEngine;

namespace Community;

public sealed class SeasonLevelPage : MonoBehaviour
{
	private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private static readonly Color Muted = new Color(0.65f, 0.66f, 0.63f, 1f);

	private WarpRushRewards owner;

	private UIWidget host;

	private GameObject page;

	private GameObject columns;

	private GameObject template;

	private UILabel font;

	private UILabel title;

	private UILabel end;

	private UILabel level;

	private UILabel description;

	private UILabel progress;

	private UILabel remaining;

	private UILabel quota;

	private UILabel quotaNote;

	private UILabel claimNote;

	private UILabel checkpoint5;

	private UILabel checkpoint10;

	private UISprite levelFill;

	private UISprite quotaFill;

	private SelectableButton claim;

	private SelectableButton buttonTemplate;

	private UIPanel clip;

	private UIScrollView scroll;

	private readonly List<WarpRushRewardPhaseWidget> nodes = new List<WarpRushRewardPhaseWidget>();

	private readonly Dictionary<GameObject, bool> legacy = new Dictionary<GameObject, bool>();

	private CommunitySeasonPass.Status shown;

	private float next;

	private bool custom;

	private bool columnsDirty = true;

	public static bool Supports(CommunitySeasonPass.Status status)
	{
		if (SeasonLevelHud.UsesDedicatedHud)
		{
			if (status != null && status.Active)
			{
				return status.ExpPerPoint > 0;
			}
			return true;
		}
		return false;
	}

	public static SeasonLevelPage Attach(WarpRushRewards owner, GameObject tierTemplate, UILabel style, SelectableButton button)
	{
		SeasonLevelPage seasonLevelPage = owner.gameObject.AddComponent<SeasonLevelPage>();
		seasonLevelPage.owner = owner;
		seasonLevelPage.host = owner.GetComponent<UIWidget>();
		seasonLevelPage.template = tierTemplate;
		seasonLevelPage.font = style;
		seasonLevelPage.buttonTemplate = button;
		return seasonLevelPage;
	}

	public bool Present(CommunitySeasonPass.Status status)
	{
		bool flag = Supports(status) && WarpRushGroup.SeasonPassMode;
		if (flag && page == null)
		{
			Build();
		}
		if (page == null)
		{
			return false;
		}
		if (custom != flag)
		{
			custom = flag;
			foreach (KeyValuePair<GameObject, bool> item in legacy)
			{
				if (item.Key != null)
				{
					item.Key.SetActive(!flag && item.Value);
				}
			}
			page.SetActive(flag);
			shown = null;
			columnsDirty = true;
		}
		if (flag)
		{
			Render(status);
		}
		return flag;
	}

	private void OnDisable()
	{
		shown = null;
		columnsDirty = true;
	}

	private void Update()
	{
		if (!(Time.unscaledTime < next))
		{
			next = Time.unscaledTime + 0.25f;
			Present(CommunitySeasonPass.Current);
		}
	}

	private void LateUpdate()
	{
		if (custom && !(page == null) && !(host == null))
		{
			float b = Mathf.Min((float)host.width / 1106f, (float)host.height / 630f);
			page.transform.localScale = Vector3.one * Mathf.Max(0.1f, b);
		}
	}

	private void Build()
	{
		foreach (Transform item in owner.transform)
		{
			legacy[item.gameObject] = item.gameObject.activeSelf;
		}
		page = owner.gameObject.AddChild();
		page.name = "SeasonLevelPage";
		page.AddComponent<UIWidget>().SetDimensions(1106, 630);
		title = Label("SeasonName", -537f, 296f, 740, 32, 23, PresetColor.UIYellow);
		end = Label("End", 180f, 296f, 355, 32, 16, Muted, NGUIText.Alignment.Right);
		Box("Summary", 0f, 205f, 1106, 124);
		Sprite("icon_mainhud_warprush", -513f, 227f, 36, 36, PresetColor.UIYellow);
		level = Label("Level", -478f, 242f, 520, 40, 29, PresetColor.UIYellow);
		description = Label("Source", -478f, 207f, 520, 27, 17, Color.white);
		progress = Label("LevelExp", -478f, 179f, 290, 27, 18, Color.white);
		remaining = Label("Remaining", -164f, 179f, 220, 27, 15, Muted, NGUIText.Alignment.Right);
		levelFill = Gauge("LevelProgress", -211f, 153f, 534);
		Sprite("bg_dotline_white2_height", 90f, 205f, 1, 104, new Color(1f, 1f, 1f, 0.15f));
		Label("QuotaTitle", 120f, 242f, 410, 30, 20, Color.white).text = SeasonLevelPresentation.T("EXP ซ\u0e35ซ\u0e31\u0e48นว\u0e31นน\u0e35\u0e49", "Season EXP today");
		quota = Label("Quota", 120f, 207f, 410, 34, 25, PresetColor.UIYellow);
		quotaNote = Label("QuotaNote", 120f, 179f, 410, 27, 16, Muted);
		quotaFill = Gauge("DailyQuota", 325f, 153f, 410);
		Box("Rewards", 0f, -20f, 1106, 310);
		Label("TrackTitle", -537f, 105f, 120, 30, 16, Color.white).text = "Season Level";
		Label("BoxLabel", -537f, 38f, 120, 28, 17, Color.white).text = SeasonLevelPresentation.T("กล\u0e48องเสบ\u0e35ยง", "Supply box");
		Sprite("icon_warprush_reward", -480f, -8f, 46, 38, Color.white);
		Label("SpecialLabel", -537f, -63f, 120, 28, 17, PresetColor.UIYellow).text = SeasonLevelPresentation.T("รางว\u0e31ลพ\u0e34เศษ", "Special reward");
		Sprite("icon_warprush_levelreward", -480f, -108f, 30, 30, PresetColor.UIYellow);
		GameObject gameObject = page.AddChild();
		gameObject.name = "RewardScroll";
		gameObject.transform.localPosition = new Vector3(65f, -20f, 0f);
		clip = gameObject.AddComponent<UIPanel>();
		clip.clipping = UIDrawCall.Clipping.SoftClip;
		clip.baseClipRegion = new Vector4(0f, 0f, 946f, 306f);
		clip.clipSoftness = Vector2.one;
		UIPanel uIPanel = NGUITools.FindInParents<UIPanel>(page);
		clip.depth = ((uIPanel == null) ? 1 : (uIPanel.depth + 1));
		scroll = gameObject.AddComponent<UIScrollView>();
		scroll.movement = UIScrollView.Movement.Horizontal;
		scroll.disableDragIfFits = true;
		scroll.contentPivot = UIWidget.Pivot.TopLeft;
		columns = gameObject.AddChild();
		columns.name = "NativeRewardColumns";
		Box("Checkpoints", 0f, -214f, 1106, 52);
		Sprite("icon_sailing_volcanic", -518f, -214f, 30, 30, Color.white);
		checkpoint5 = Label("Checkpoint5", -483f, -214f, 500, 43, 17, PresetColor.UIYellow);
		Sprite("icon_sailing_snowfield", 185f, -214f, 28, 28, Color.white);
		checkpoint10 = Label("Checkpoint10", 218f, -214f, 315, 43, 17, Color.white);
		Box("Footer", 0f, -283f, 1106, 57);
		Sprite("icon_warprush_reward", -522f, -281f, 30, 26, Color.white);
		claimNote = Label("Claimable", -495f, -280f, 690, 36, 20, Color.white);
		GameObject gameObject2 = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, page.transform, worldPositionStays: false);
		gameObject2.name = "ClaimAllSeasonRewards";
		claim = gameObject2.GetComponent<SelectableButton>();
		claim.Widget.SetAnchor((Transform)null);
		typeof(SelectableButton).GetField("_toPreferredSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(claim, false);
		typeof(SelectableButton).GetField("_fontSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(claim, 21);
		FieldInfo field = typeof(SelectableButton).GetField("_padding", BindingFlags.Instance | BindingFlags.NonPublic);
		object value = field.GetValue(claim);
		FieldInfo[] fields = value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (fieldInfo.FieldType == typeof(int))
			{
				fieldInfo.SetValue(value, 0);
			}
		}
		field.SetValue(claim, value);
		claim.Widget.depth = 30;
		claim.SetStyle(PresetButton.Style.Border);
		claim.SetStyle(PresetButton.Style.Solid);
		claim.SetDimensions(286, 46);
		gameObject2.transform.localPosition = new Vector3(397f, -283f, 0f);
		claim.Clicked = delegate
		{
			if (CanClaim(CommunitySeasonPass.Current))
			{
				CommunitySeasonPass.Claim(0);
			}
		};
		page.SetActive(value: false);
	}

	public static bool CanClaim(CommunitySeasonPass.Status s)
	{
		if (SeasonLevelPresentation.IsLevelSeason(s) && SeasonLevelPresentation.Fresh(s) && s.Claimable > 0)
		{
			return !CommunitySeasonPass.Busy;
		}
		return false;
	}

	private void Render(CommunitySeasonPass.Status s)
	{
		bool flag = SeasonLevelPresentation.IsLevelSeason(s);
		title.text = (flag ? s.SeasonName : SeasonLevelPresentation.T("ซ\u0e35ซ\u0e31\u0e48น", "Season"));
		end.text = SeasonLevelPresentation.End(s);
		level.text = (flag ? SeasonLevelPresentation.Level(s) : ((s == null) ? SeasonLevelPresentation.T("กำล\u0e31งโหลดซ\u0e35ซ\u0e31\u0e48น…", "Loading season…") : SeasonLevelPresentation.T("ย\u0e31งไม\u0e48ม\u0e35ซ\u0e35ซ\u0e31\u0e48นเป\u0e34ด", "No active season")));
		description.text = (flag ? SeasonLevelPresentation.T("EXP ส\u0e48วนเก\u0e34นหล\u0e31งเลเวลต\u0e31วละคร 60", "Overflow EXP after character level 60") : "");
		progress.text = SeasonLevelPresentation.Progress(s);
		remaining.text = SeasonLevelPresentation.Remaining(s);
		levelFill.fillAmount = (flag ? CommunitySeasonPass.SeasonRatio(s) : 0f);
		quota.text = SeasonLevelPresentation.Quota(s);
		quotaNote.text = SeasonLevelPresentation.QuotaNote(s);
		quotaFill.fillAmount = SeasonLevelPresentation.QuotaRatio(s);
		claim.Disabled = !CanClaim(s);
		claim.Text = (flag ? string.Format(SeasonLevelPresentation.T("ร\u0e31บรางว\u0e31ลท\u0e31\u0e49งหมด ({0})", "Claim all ({0})"), s.Claimable) : SeasonLevelPresentation.T("ไม\u0e48ม\u0e35รางว\u0e31ลให\u0e49ร\u0e31บ", "No rewards to claim"));
		claimNote.text = (flag ? string.Format(SeasonLevelPresentation.T("ม\u0e35รางว\u0e31ลค\u0e49างร\u0e31บ {0} เลเวล", "Unclaimed rewards: {0} levels"), s.Claimable) : SeasonLevelPresentation.Progress(s));
		bool flag2 = flag && string.Equals(s.SeasonId, "s02", StringComparison.OrdinalIgnoreCase) && s.MaxTier >= 10;
		checkpoint5.text = (flag2 ? SeasonLevelPresentation.T("เป\u0e49าหมาย · Season Lv.5\nพ\u0e37\u0e49นท\u0e35\u0e48ซ\u0e35ซ\u0e31\u0e48น S02-5", "Milestone · Season Lv.5\nS02-5 season area") : "");
		checkpoint10.text = (flag2 ? SeasonLevelPresentation.T("เป\u0e49าหมาย · Season Lv.10\nพ\u0e37\u0e49นท\u0e35\u0e48ซ\u0e35ซ\u0e31\u0e48น S02-10", "Milestone · Season Lv.10\nS02-10 season area") : "");
		if (shown != s || columnsDirty)
		{
			shown = s;
			columnsDirty = false;
			FillColumns(s);
		}
		bool flag3 = flag && SeasonLevelPresentation.Fresh(s) && !CommunitySeasonPass.Busy;
		foreach (WarpRushRewardPhaseWidget node in nodes)
		{
			Collider[] componentsInChildren = node.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = flag3;
			}
		}
	}

	private void FillColumns(CommunitySeasonPass.Status s)
	{
		int num = ((s != null && s.Active) ? s.Tiers.Count : 0);
		while (nodes.Count < num)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(template, columns.transform, worldPositionStays: false);
			gameObject.name = "SeasonLevelColumn";
			UIRect[] componentsInChildren = gameObject.GetComponentsInChildren<UIRect>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].SetAnchor((Transform)null);
			}
			WarpRushRewardPhaseWidget component = gameObject.GetComponent<WarpRushRewardPhaseWidget>();
			component.Init(scroll);
			nodes.Add(component);
		}
		float num2 = ((Platform.Instance.UsePCUI && num <= 10) ? 94.6f : 118f);
		for (int j = 0; j < nodes.Count; j++)
		{
			WarpRushRewardPhaseWidget warpRushRewardPhaseWidget = nodes[j];
			warpRushRewardPhaseWidget.gameObject.SetActive(j < num);
			if (j < num)
			{
				warpRushRewardPhaseWidget.transform.localPosition = new Vector3(-473f + num2 * ((float)j + 0.5f), 155f, 0f);
				warpRushRewardPhaseWidget.SetSeasonPass(s.Tiers[j], s.FirstClaimable);
				Arrange(warpRushRewardPhaseWidget.transform, "Progress", 0f, -40f);
				Arrange(warpRushRewardPhaseWidget.transform, "LevelReward", 0f, -140f);
				Arrange(warpRushRewardPhaseWidget.transform, "CashReward", 0f, -240f);
				Arrange(warpRushRewardPhaseWidget.transform, "bg_line", num2 * 0.5f, -181f);
				warpRushRewardPhaseWidget.transform.Find("bg_line").GetComponent<UIWidget>().height = 220;
				UILabel component2 = warpRushRewardPhaseWidget.transform.Find("Progress/Level").GetComponent<UILabel>();
				component2.width = (int)num2;
				component2.fontSize = 18;
				component2.text = "Lv." + s.Tiers[j].Number;
				Transform obj = warpRushRewardPhaseWidget.transform.Find("Progress/Gauge");
				obj.localPosition = new Vector3((0f - num2) * 0.5f, 18f, 0f);
				UIWidget[] componentsInChildren2 = obj.GetComponentsInChildren<UIWidget>(includeInactive: true);
				for (int i = 0; i < componentsInChildren2.Length; i++)
				{
					componentsInChildren2[i].width = Mathf.Max(1, (int)num2 - 24);
				}
			}
		}
		scroll.InvalidateBounds();
	}

	private static void Arrange(Transform root, string name, float x, float y)
	{
		Transform transform = root.Find(name);
		if (transform != null)
		{
			transform.localPosition = new Vector3(x, y, 0f);
		}
	}

	public Dictionary<string, object> Diagnostics()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>
		{
			{ "custom", custom },
			{
				"columns",
				nodes.FindAll((WarpRushRewardPhaseWidget n) => n.gameObject.activeSelf).Count
			}
		};
		if (page == null)
		{
			return dictionary;
		}
		dictionary["visible"] = page.activeInHierarchy;
		dictionary["level"] = level.text;
		dictionary["quota"] = quota.text;
		dictionary["progress"] = progress.text;
		dictionary["claimDisabled"] = claim.Disabled;
		dictionary["scale"] = page.transform.localScale.x;
		dictionary["spritesValid"] = Array.TrueForAll(page.GetComponentsInChildren<UISprite>(includeInactive: true), (UISprite s) => s.isValid);
		return dictionary;
	}

	private void Box(string name, float x, float y, int w, int h)
	{
		Sprite("bg_roundbox2", x, y, w, h, new Color(0f, 0f, 0f, 0.35f)).name = name;
		Sprite("bg_box_grunge", x, y, w, h, new Color(0f, 0f, 0f, 0.8f));
	}

	private UISprite Gauge(string name, float x, float y, int w)
	{
		Sprite(Platform.Instance.UsePCUI ? "party_bar_slot_pc" : "bg_white", x, y, w, 5, Platform.Instance.UsePCUI ? new Color(1f, 1f, 1f, 0.6f) : new Color(0f, 0f, 0f, 0.47f));
		UISprite uISprite = Sprite(Platform.Instance.UsePCUI ? "party_bar_pc" : "bg_white", x, y, w, 5, PresetColor.UIYellow);
		uISprite.name = name;
		uISprite.type = UIBasicSprite.Type.Filled;
		uISprite.fillDirection = UIBasicSprite.FillDirection.Horizontal;
		return uISprite;
	}

	private UISprite Sprite(string sprite, float x, float y, int w, int h, Color color)
	{
		UISprite uISprite = page.AddSprite(sprite, 5);
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		uISprite.width = w;
		uISprite.height = h;
		uISprite.color = color;
		return uISprite;
	}

	private UILabel Label(string name, float x, float y, int w, int h, int size, Color color, NGUIText.Alignment align = NGUIText.Alignment.Left)
	{
		GameObject gameObject = page.AddChild();
		gameObject.name = name;
		UILabel uILabel = gameObject.AddComponent<UILabel>();
		uILabel.bitmapFont = font.bitmapFont;
		uILabel.trueTypeFont = font.trueTypeFont;
		uILabel.fontSize = size;
		uILabel.width = w;
		uILabel.height = h;
		uILabel.pivot = UIWidget.Pivot.Left;
		uILabel.alignment = align;
		uILabel.color = color;
		uILabel.depth = 20;
		uILabel.supportEncoding = false;
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		gameObject.transform.localPosition = new Vector3(x, y, 0f);
		return uILabel;
	}
}

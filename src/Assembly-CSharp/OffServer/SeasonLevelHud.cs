using Durango.Logic.Clusters;
using Durango.System;
using Durango.UI;
using Durango.Utils;
using UnityEngine;

namespace OffServer;

public sealed class SeasonLevelHud : MonoBehaviour
{
	private ExpGaugeBase owner;

	private UIWidget surface;

	private UILabel level;

	private UILabel percent;

	private UISprite fill;

	private float next;

	public static bool UsesDedicatedHud => true;

	public static void Attach(ExpGaugeBase gauge)
	{
		if (UsesDedicatedHud && !(gauge.GetComponent<SeasonLevelHud>() != null))
		{
			gauge.gameObject.AddComponent<SeasonLevelHud>().owner = gauge;
		}
	}

	private void OnDisable()
	{
		if (surface != null)
		{
			surface.gameObject.SetActive(value: false);
		}
	}

	private void OnDestroy()
	{
		if (surface != null)
		{
			Object.Destroy(surface.gameObject);
		}
	}

	private void Update()
	{
		if (!(Time.unscaledTime < next))
		{
			next = Time.unscaledTime + 0.25f;
			bool flag = GameManager.IsReady && !GameManager.IsTitleScene && Singleton<UIManager>.HasInstance() && !UIManager.IsLoadingCurtain && owner != null && owner.isActiveAndEnabled && UsesDedicatedHud && OffServerSeasonPass.UseSeasonHud;
			if (surface != null)
			{
				surface.gameObject.SetActive(flag);
			}
			if (flag && (!(surface == null) || Build()))
			{
				OffServerSeasonPass.Status current = OffServerSeasonPass.Current;
				float num = OffServerSeasonPass.SeasonRatio(current);
				level.text = "Season Lv." + current.SeasonLevel;
				percent.text = (SeasonLevelPresentation.Max(current) ? "MAX" : (Mathf.RoundToInt(num * 100f) + "%"));
				fill.fillAmount = num;
			}
		}
	}

	private bool Build()
	{
		UILabel componentInChildren = owner.GetComponentInChildren<UILabel>(includeInactive: true);
		UIWidget component = owner.GetComponent<UIWidget>();
		if (componentInChildren == null || component == null)
		{
			return false;
		}
		GameObject gameObject = owner.gameObject.AddChild();
		gameObject.name = "SeasonLevelHud";
		surface = gameObject.AddComponent<UIWidget>();
		surface.width = 222;
		surface.height = 30;
		surface.SetAnchor(component.gameObject, 0.5f, -111, 1f, 12, 0.5f, 111, 1f, 42);
		int num = componentInChildren.depth + 10;
		UISprite uISprite = gameObject.AddSprite("icon_mainhud_warprush", num);
		uISprite.width = 19;
		uISprite.height = 19;
		uISprite.transform.localPosition = new Vector3(-101f, 3f, 0f);
		uISprite.color = PresetColor.UIYellow;
		level = Label(gameObject, componentInChildren, "SeasonLevel", -85f, 3f, 151, 21, 17, NGUIText.Alignment.Left, num);
		level.color = PresetColor.UIYellow;
		percent = Label(gameObject, componentInChildren, "Progress", 62f, 3f, 49, 21, 15, NGUIText.Alignment.Right, num);
		UISprite uISprite2 = gameObject.AddSprite(Platform.Instance.UsePCUI ? "party_bar_slot_pc" : "bg_white", num);
		uISprite2.width = 222;
		uISprite2.height = 4;
		uISprite2.color = (Platform.Instance.UsePCUI ? Color.white : new Color(0f, 0f, 0f, 0.47f));
		uISprite2.transform.localPosition = new Vector3(0f, -12f, 0f);
		fill = gameObject.AddSprite(Platform.Instance.UsePCUI ? "party_bar_pc" : "bg_white", num + 1);
		fill.width = 222;
		fill.height = 4;
		fill.transform.localPosition = new Vector3(0f, -12f, 0f);
		fill.type = UIBasicSprite.Type.Filled;
		fill.fillDirection = UIBasicSprite.FillDirection.Horizontal;
		fill.color = PresetColor.UIYellow;
		BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
		boxCollider.isTrigger = true;
		boxCollider.size = new Vector3(222f, Platform.Instance.UsePCUI ? 30 : 48, 1f);
		boxCollider.center = new Vector3(0f, (!Platform.Instance.UsePCUI) ? 10 : 0, 0f);
		UIEventListener.Get(gameObject).onClick = delegate
		{
			if (OffServerSeasonPass.UseSeasonHud)
			{
				WarpRushGroup warpRushGroup = UIManager.FindScript<WarpRushGroup>();
				if (warpRushGroup != null)
				{
					WarpRushGroup.SeasonPassMode = true;
					warpRushGroup.Open();
				}
			}
		};
		return true;
	}

	private static UILabel Label(GameObject parent, UILabel style, string name, float x, float y, int w, int h, int size, NGUIText.Alignment align, int depth)
	{
		GameObject gameObject = parent.AddChild();
		gameObject.name = name;
		UILabel uILabel = gameObject.AddComponent<UILabel>();
		uILabel.bitmapFont = style.bitmapFont;
		uILabel.trueTypeFont = style.trueTypeFont;
		uILabel.fontSize = size;
		uILabel.supportEncoding = false;
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.pivot = UIWidget.Pivot.Left;
		uILabel.alignment = align;
		uILabel.width = w;
		uILabel.height = h;
		uILabel.depth = depth;
		uILabel.color = Color.white;
		uILabel.effectStyle = UILabel.Effect.Shadow;
		uILabel.effectColor = Color.black;
		gameObject.transform.localPosition = new Vector3(x, y, 0f);
		return uILabel;
	}
}

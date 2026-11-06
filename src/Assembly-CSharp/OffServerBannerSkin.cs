using System;
using Durango.UI;
using UnityEngine;
using Yaml;

public class OffServerBannerSkin : MonoBehaviour
{
	private static readonly Color Dark = new Color(0.1f, 0.07f, 0.04f, 1f);

	private static readonly Color Cream = new Color(0.91f, 0.82f, 0.6f, 1f);

	private static readonly Color TagNew = new Color(0.78f, 0.19f, 0.16f, 1f);

	private static readonly Color TagTime = new Color(0.08f, 0.08f, 0.08f, 0.82f);

	private const float TickSec = 30f;

	private const string RootName = "OffServerBannerSkin";

	private UIWidget _banner;

	private UISprite _background;

	private UITexture _texture;

	private UILabel _hud;

	private bool _hudHidden;

	private PromotionLink _link;

	private GameObject _root;

	private UISprite _shadow;

	private UISprite _gradLeft;

	private UISprite _gradBottom;

	private UISprite _grunge;

	private UISprite _line;

	private UISprite _rimInner;

	private UISprite _rimOuter;

	private UISprite _circle;

	private UISprite _icon;

	private UISprite _tagBg;

	private UILabel _title;

	private UILabel _tagText;

	private int _w;

	private int _h;

	private float _nextTick = float.MaxValue;

	public static int Count { get; private set; }

	public static void Apply(PromotionBannerWidget widget, UISprite background, UITexture texture, UILabel hud, PromotionLink link)
	{
		try
		{
			if (!(widget == null) && !(background == null))
			{
				OffServerBannerStyle.Fetch(force: false);
				OffServerBannerSkin offServerBannerSkin = widget.GetComponent<OffServerBannerSkin>();
				if (offServerBannerSkin == null)
				{
					offServerBannerSkin = widget.gameObject.AddComponent<OffServerBannerSkin>();
				}
				if (offServerBannerSkin._background != background)
				{
					offServerBannerSkin.Init(background, texture, hud);
				}
				offServerBannerSkin._link = link;
				offServerBannerSkin.Sync();
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private void Init(UISprite background, UITexture texture, UILabel hud)
	{
		_background = background;
		_texture = texture;
		_hud = hud;
		_banner = ((background.transform.parent != null) ? background.transform.parent.GetComponent<UIWidget>() : null);
		if (_banner == null)
		{
			_banner = background;
		}
		_root = null;
		_hudHidden = false;
		Transform transform = _banner.transform.Find("OffServerBannerSkin");
		if (transform != null)
		{
			if (_hud != null && !_hud.enabled)
			{
				_hudHidden = true;
			}
			transform.gameObject.SetActive(value: false);
			transform.name = "OffServerBannerSkin-copy";
			UnityEngine.Object.Destroy(transform.gameObject);
		}
	}

	private void Awake()
	{
		Count++;
		OffServerBannerStyle.Changed += OnStyleChanged;
		base.enabled = false;
	}

	private void OnDestroy()
	{
		Count--;
		OffServerBannerStyle.Changed -= OnStyleChanged;
	}

	private void OnStyleChanged()
	{
		if (this != null)
		{
			Sync();
		}
	}

	private void Sync()
	{
		if (!(_background == null) && !(_banner == null))
		{
			if (OffServerBannerStyle.Style >= 1 && _link != null)
			{
				Show();
			}
			else
			{
				Hide();
			}
		}
	}

	private void Show()
	{
		if (_root == null)
		{
			Build();
		}
		_root.SetActive(value: true);
		if (_hud != null && _hud.enabled)
		{
			_hud.enabled = false;
			_hudHidden = true;
		}
		Refresh();
		base.enabled = true;
	}

	private void Hide()
	{
		if (_root != null)
		{
			_root.SetActive(value: false);
		}
		if (_hudHidden && _hud != null)
		{
			_hud.enabled = true;
		}
		_hudHidden = false;
		base.enabled = false;
	}

	private void Update()
	{
		if (!(_banner == null) && !(_root == null))
		{
			if (_banner.width != _w || _banner.height != _h)
			{
				Layout();
			}
			if (Time.realtimeSinceStartup >= _nextTick)
			{
				Refresh();
			}
		}
	}

	private void Build()
	{
		_root = _banner.gameObject.AddChild();
		_root.name = "OffServerBannerSkin";
		int depth = _background.depth;
		int num = Mathf.Max(depth, (_texture != null) ? _texture.depth : depth) + 1;
		int num2 = Mathf.Max(num + 6, (_hud != null) ? _hud.depth : 0);
		_shadow = Spr("glow_box", depth - 1, new Color(0f, 0f, 0f, 0.6f));
		_gradLeft = Spr("bg_popup_gradient", num, new Color(0f, 0f, 0f, 0.62f));
		_gradLeft.type = UIBasicSprite.Type.Simple;
		_gradLeft.rotate = UIBasicSprite.Rotate.Radial270;
		_gradBottom = Spr("bg_popup_gradient", num, new Color(0f, 0f, 0f, 0.55f));
		_gradBottom.type = UIBasicSprite.Type.Simple;
		_gradBottom.flip = UIBasicSprite.Flip.Vertically;
		_grunge = Spr("bg_box_grunge", num + 1, new Color(Cream.r, Cream.g, Cream.b, 0.28f));
		_line = Spr("estate_line_box_line_bg", num + 2, new Color(Cream.r, Cream.g, Cream.b, 0.9f));
		_rimInner = Spr("estate_line_box_line_bg", num + 3, Dark);
		_rimOuter = Spr("estate_line_box_line_bg", num + 3, Dark);
		_line.centerType = UIBasicSprite.AdvancedType.Invisible;
		_rimInner.centerType = UIBasicSprite.AdvancedType.Invisible;
		_rimOuter.centerType = UIBasicSprite.AdvancedType.Invisible;
		_circle = Spr("bg_circle_01", num + 4, Color.white);
		_circle.type = UIBasicSprite.Type.Simple;
		_icon = Spr("bg_white", num + 5, Color.white);
		_icon.type = UIBasicSprite.Type.Simple;
		_title = Lbl(num2 + 1, 18, Color.white);
		_title.pivot = UIWidget.Pivot.Left;
		_title.alignment = NGUIText.Alignment.Left;
		_title.overflowMethod = UILabel.Overflow.ShrinkContent;
		_title.maxLineCount = 1;
		_title.effectStyle = UILabel.Effect.OutlineShadow;
		_title.effectColor = new Color(0.06f, 0.04f, 0.02f, 1f);
		_title.effectDistance = new Vector2(1f, 1f);
		_tagBg = Spr("bg_roundbox_small", num2 + 2, TagNew);
		_tagBg.pivot = UIWidget.Pivot.TopRight;
		_tagText = Lbl(num2 + 3, 12, Color.white);
		_tagText.pivot = UIWidget.Pivot.Center;
		_tagText.overflowMethod = UILabel.Overflow.ResizeFreely;
		_tagText.effectStyle = UILabel.Effect.Outline;
		_tagText.effectColor = new Color(0f, 0f, 0f, 0.7f);
	}

	private UISprite Spr(string sprite, int depth, Color color)
	{
		UISprite uISprite = _root.AddSprite(sprite, depth);
		uISprite.pivot = UIWidget.Pivot.Center;
		uISprite.color = color;
		return uISprite;
	}

	private UILabel Lbl(int depth, int size, Color color)
	{
		UILabel uILabel = _root.AddWidget<UILabel>(depth);
		if (_hud != null)
		{
			uILabel.bitmapFont = _hud.bitmapFont;
			if (_hud.trueTypeFont != null)
			{
				uILabel.trueTypeFont = _hud.trueTypeFont;
			}
		}
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.supportEncoding = false;
		return uILabel;
	}

	private void Refresh()
	{
		if (!(_root == null))
		{
			OffServerBannerStyle.Entry entry = OffServerBannerStyle.For(_link);
			_title.text = OffServerBannerStyle.TitleFor(_link, entry);
			string text = OffServerBannerStyle.IconFor(_link, entry);
			UIAtlas atlas;
			UISpriteData spriteData;
			bool flag = !string.IsNullOrEmpty(text) && ResourceSingleton<UISpriteManager>.Instance().TryGet(text, out atlas, out spriteData) && spriteData != null;
			_circle.enabled = flag;
			_icon.enabled = flag;
			if (flag)
			{
				_icon.spriteName = text;
			}
			bool countdown;
			string text2 = OffServerBannerStyle.TagFor(_link, entry, OffServerBannerStyle.Now(), out countdown);
			bool flag2 = !string.IsNullOrEmpty(text2);
			_tagBg.enabled = flag2;
			_tagText.enabled = flag2;
			if (flag2)
			{
				_tagText.text = text2;
				_tagBg.color = (countdown ? TagTime : TagNew);
			}
			_nextTick = (countdown ? (Time.realtimeSinceStartup + 30f) : float.MaxValue);
			Layout();
		}
	}

	private void Layout()
	{
		_w = _banner.width;
		_h = _banner.height;
		Vector2 pivotOffset = _banner.pivotOffset;
		float num = (0f - pivotOffset.x) * (float)_w;
		float num2 = (0f - pivotOffset.y) * (float)_h;
		float x = num + (float)_w * 0.5f;
		float num3 = num2 + (float)_h * 0.5f;
		float num4 = num + (float)_w;
		float num5 = num2 + (float)_h;
		Place(_shadow, x, num3 - 3f, _w + 10, _h + 10);
		int num6 = Mathf.RoundToInt((float)_w * 0.55f);
		Place(_gradLeft, num + (float)num6 * 0.5f, num3, num6, _h);
		int num7 = Mathf.RoundToInt((float)_h * 0.72f);
		Place(_gradBottom, x, num2 + (float)num7 * 0.5f, _w, num7);
		Place(_grunge, x, num3, _w - 4, _h - 4);
		Place(_line, x, num3, _w - 2, _h - 2);
		Place(_rimInner, x, num3, _w + 2, _h + 2);
		Place(_rimOuter, x, num3, _w + 4, _h + 4);
		int num8 = Mathf.Clamp(_h / 2 - 2, 20, 28);
		float x2 = num + 4f + (float)num8 * 0.5f;
		float y = num5 - 4f - (float)num8 * 0.5f;
		Place(_circle, x2, y, num8, num8);
		if (_icon.enabled)
		{
			UISpriteData atlasSprite = _icon.GetAtlasSprite();
			float num9 = (float)num8 * 0.6f;
			float num10 = ((atlasSprite != null) ? Mathf.Max(1f, Mathf.Max(atlasSprite.width, atlasSprite.height)) : num9);
			int width = ((atlasSprite != null) ? Mathf.Max(2, Mathf.RoundToInt((float)atlasSprite.width * num9 / num10)) : Mathf.RoundToInt(num9));
			int height = ((atlasSprite != null) ? Mathf.Max(2, Mathf.RoundToInt((float)atlasSprite.height * num9 / num10)) : Mathf.RoundToInt(num9));
			Place(_icon, x2, y, width, height);
		}
		_title.width = Mathf.Max(20, _w - 18);
		_title.height = 26;
		_title.transform.localPosition = new Vector3(Mathf.Round(num + 9f), Mathf.Round(num2 + 17f), 0f);
		if (_tagText.enabled)
		{
			int b = Mathf.RoundToInt(_tagText.printedSize.x) + 12;
			_tagBg.width = Mathf.Max(24, b);
			_tagBg.height = 18;
			_tagBg.transform.localPosition = new Vector3(Mathf.Round(num4 - 5f), Mathf.Round(num5 - 5f), 0f);
			_tagText.transform.localPosition = new Vector3(Mathf.Round(num4 - 5f - (float)_tagBg.width * 0.5f), Mathf.Round(num5 - 5f - 9f), 0f);
		}
	}

	private static void Place(UIWidget widget, float x, float y, int width, int height)
	{
		widget.width = Mathf.Max(2, width);
		widget.height = Mathf.Max(2, height);
		widget.transform.localPosition = new Vector3(Mathf.Round(x), Mathf.Round(y), 0f);
	}
}

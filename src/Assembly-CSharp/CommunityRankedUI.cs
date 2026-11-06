using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Durango.Logic;
using Durango.Logic.Item;
using Durango.Logic.Party;
using Durango.System;
using Durango.UI;
using Durango.UI.Control;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Yaml;

public class CommunityRankedUI : MonoBehaviour
{
	private sealed class Emblem
	{
		public GameObject Go;

		public UISprite Shield;

		public UISprite Mark;

		public UISprite WingL;

		public UISprite WingR;

		public UISprite Crown;

		public UILabel Unknown;

		public CommunityRankedAnim.Anim In;
	}

	private sealed class Bar
	{
		public GameObject Go;

		public UISprite Light;

		public UISprite Fill;

		public UILabel Label;
	}

	private sealed class Tile
	{
		public GameObject Go;

		public UISprite Bg;

		public UISprite Select;

		public ItemIconTex Icon;

		public UILabel Name;

		public string Proto;

		public string ItemId;

		public int Level;

		public CommunityRankedAnim.Anim Sel;
	}

	private sealed class Card
	{
		public GameObject Go;

		public UISprite Bg;

		public UISprite Grunge;

		public UILabel Header;

		public readonly Emblem[] Emblems = new Emblem[3];

		public readonly UILabel[] Names = new UILabel[3];

		public readonly UILabel[] Tiers = new UILabel[3];

		public readonly UISprite[] Lines = new UISprite[2];

		public GameObject Body;

		public CommunityRankedAnim.Anim In;
	}

	private sealed class RoundRow
	{
		public GameObject Go;

		public UISprite Dot;

		public UILabel Round;

		public UISprite Weather;

		public UILabel WeatherName;

		public UILabel Outcome;

		public UISprite TimeIcon;

		public UILabel Time;

		public UILabel Note;
	}

	private sealed class Chip
	{
		public GameObject Go;

		public UILabel Number;

		public UISprite Crown;

		public UILabel Name;

		public UILabel Tier;
	}

	private static readonly Color Yellow = new Color(1f, 0.847f, 0.357f);

	private static readonly Color Red = new Color(0.949f, 0.208f, 0.016f);

	private static readonly Color FoeText = new Color(1f, 0.478f, 0.361f);

	private static readonly Color Grey = new Color(0.573f, 0.573f, 0.573f);

	private const int PlacementGames = 5;

	private static CommunityRankedUI _inst;

	private UILabel _font;

	private SelectableButton _solidTpl;

	private SelectableButton _borderTpl;

	private int _hudDepth = 10;

	private int _modalDepth = 140;

	private int _overDepth = 150;

	private int _overLayer = -1;

	private CommunityRankedWindow _lobbyWin;

	private bool _lobbyOpen;

	private string _lobbyMode = "1v1";

	private Transform _lCenter;

	private GameObject _lAdd;

	private UITitle _lUITitle;

	private RectLayoutComponent _lLayout;

	private UILabel _lTitle;

	private UILabel _lDesc;

	private SelectableButton _lGuide;

	private readonly UILabel[] _lScores = new UILabel[4];

	private GameObject _lHighlight;

	private UILabel _lQueue;

	private SelectableButton _lEnter;

	private readonly UISprite[] _tabBg = new UISprite[2];

	private readonly UISprite[] _tabLine = new UISprite[2];

	private readonly UILabel[] _tabText = new UILabel[2];

	private Emblem _lEmblem;

	private UISprite _lGlow;

	private UILabel _lTier;

	private UILabel _lPoints;

	private Bar _lBar;

	private UILabel _lPlaceTitle;

	private UILabel _lPlaceCount;

	private UILabel _lPlaceNote;

	private UISprite _lSeasonIcon;

	private UILabel _lSeason;

	private UILabel _lPartyHead;

	private readonly Chip[] _lChips = new Chip[3];

	private UISprite _lInviteIcon;

	private UILabel _lInvite;

	private readonly List<KeyValuePair<Transform, Vector3>> _lPlaced = new List<KeyValuePair<Transform, Vector3>>();

	private GameObject _found;

	private UILabel _fTitle;

	private UILabel _fSub;

	private readonly Card[] _fCards = new Card[2];

	private UISprite _fCircle;

	private UISprite _fCross;

	private UILabel _fNote;

	private GameObject _hud;

	private GameObject _hBox;

	private UILabel _hMe;

	private UILabel _hFoe;

	private readonly UISprite[] _pips = new UISprite[4];

	private UISprite _hTimer;

	private UILabel _hClock;

	private UISprite _hWeather;

	private UILabel _hRound;

	private UISprite _hStatusBg;

	private UILabel _hStatus;

	private readonly Bar[] _hHp = new Bar[6];

	private readonly UILabel[] _hHpName = new UILabel[6];

	private GameObject _count;

	private UILabel _cNumber;

	private UILabel _cSub;

	private GameObject _ringWarn;

	private UILabel _rwTitle;

	private UILabel _rwSub;

	private GameObject _potion;

	private UILabel _potionText;

	private ItemIconTex _potionIcon;

	private GameObject _break;

	private UILabel _bTitle;

	private UILabel _bClock;

	private UILabel _bScore;

	private readonly Tile[] _tiles = new Tile[9];

	private UILabel _bSideNote;

	private UILabel _bPickNote;

	private UILabel _bNoWeapon;

	private string _pickId;

	private float _pickUntil;

	private SelectableButton _side0;

	private SelectableButton _side1;

	private UISprite _arrow0;

	private UISprite _arrow1;

	private UILabel _bRecover;

	private bool _breakHidden;

	private CommunityRankedWindow _resultWin;

	private bool _resultShown;

	private UILabel _rTitle;

	private UISprite _rLine;

	private GameObject _rHighlight;

	private UILabel _rRankLabel;

	private SelectableButton _rExit;

	private GameObject _rAdd;

	private Emblem _rEmblem;

	private UILabel _rTier;

	private UILabel _rPoints;

	private Bar _rBar;

	private UISprite _rBannerBg;

	private UILabel _rBanner;

	private UILabel _rHead;

	private readonly RoundRow[] _rRows = new RoundRow[3];

	private UILabel _rDc;

	private UILabel _rFoot;

	private UISprite _rCoinIcon;

	private UILabel _rCoin;

	private UILabel _rCoinNote;

	private GameObject _banner;

	private GameObject _bannerAt;

	private float _holdUntil;

	private string _animMatch;

	private string _animPhase = string.Empty;

	private JObject _animResult;

	private string _animMode;

	private bool _queuePulse;

	private GameObject _lTierGo;

	private GameObject _lPartyGo;

	private CommunityRankedAnim.Anim _aTier;

	private CommunityRankedAnim.Anim _aParty;

	private CommunityRankedAnim.Anim _aQueue;

	private readonly CommunityRankedAnim.Anim[] _aTabs = new CommunityRankedAnim.Anim[2];

	private GameObject _fVs;

	private CommunityRankedAnim.Anim _aFound;

	private CommunityRankedAnim.Anim _aStrip;

	private CommunityRankedAnim.Anim _aVs;

	private CommunityRankedAnim.Anim _aNote;

	private CommunityRankedAnim.Anim _aHud;

	private readonly CommunityRankedAnim.Anim[] _aPips = new CommunityRankedAnim.Anim[4];

	private readonly bool[] _pipOn = new bool[4];

	private GameObject _hStatusGo;

	private CommunityRankedAnim.Anim _aStatus;

	private string _lastStatus;

	private GameObject _cBand;

	private UISprite _cBg;

	private CommunityRankedAnim.Anim _aCount;

	private CommunityRankedAnim.Anim _aCountNum;

	private string _lastCount;

	private CommunityRankedAnim.Anim _aBreak;

	private GameObject _rDetails;

	private CommunityRankedAnim.Anim _aRTitle;

	private CommunityRankedAnim.Anim _aRLine;

	private CommunityRankedAnim.Anim _aRDetails;

	private CommunityRankedAnim.Anim _aRCoin;

	private bool _coinShown;

	private bool _wasFound;

	private bool _wasHud;

	private bool _wasCount;

	private bool _wasBreak;

	private float _nextTick;

	private static readonly string[] UIBaseFields = new string[8] { "_background", "_softOpen", "_gameBlur", "_closeWhenPlayerMoveStart", "_lockScreenOrientation", "_uiType", "_anchorType", "_avatarMotion" };

	private bool _wasInMatch;

	private int _lastRound = -1;

	private float _closeMapUntil;

	private static string PotionProto
	{
		get
		{
			string text = CommunityRanked.Str(CommunityRanked.Lobby?["potion"]);
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return "hp_medicine_store";
		}
	}

	private bool Holding => Time.realtimeSinceStartup < _holdUntil;

	private static bool PcUi
	{
		get
		{
			try
			{
				return Platform.Instance.UsePCUI;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static void Ensure()
	{
		if (_inst != null)
		{
			return;
		}
		UIRoot uIRoot = (Singleton<UIManager>.HasInstance() ? Singleton<UIManager>.Instance().UIRoot : null);
		if (uIRoot == null || UIManager.FindScript<PvpIslandGroup>() == null)
		{
			return;
		}
		GameObject obj = uIRoot.gameObject.AddChild();
		obj.name = "CommunityRanked";
		_inst = obj.AddComponent<CommunityRankedUI>();
		try
		{
			_inst.Build();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static void OpenLobby()
	{
		OpenLobby(null);
	}

	public static void OpenLobby(string mode, bool ask = true)
	{
		CommunityRankedPages.Hide();
		Ensure();
		if (!(_inst == null) && !(_inst._lobbyWin == null))
		{
			if (!string.IsNullOrEmpty(mode))
			{
				_inst._lobbyMode = mode;
			}
			if (CommunityRanked.Queued)
			{
				_inst._lobbyMode = CommunityRanked.QueuedMode;
			}
			_inst._lobbyOpen = true;
			if (ask)
			{
				CommunityRanked.AskState();
			}
			_inst.Refresh();
		}
	}

	public static void CloseAll()
	{
		CommunityRankedPages.Hide();
		if (!(_inst == null))
		{
			_inst._lobbyOpen = false;
			CommunityRanked.CloseResult();
			_inst.Refresh();
		}
	}

	public static void MenusChanged()
	{
		try
		{
			GameSystem<MenuSystem>.Instance().EnableMenu(MenuType.PvpIsland, CommunityRanked.ReplacesBrawl ? CommunityRanked.Available : WarpRushSystem.CommunityBrawlMenuEnabled(OptionSystem.IsWarpRushShutdown()));
			MenuListGroupBase menuListGroupBase = UIManager.FindScript<MenuListGroupBase>();
			if (menuListGroupBase != null)
			{
				menuListGroupBase.CommunityRefreshMenuList();
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static void SeasonTitles(string names)
	{
		try
		{
			ChapterGroup chapterGroup = UIManager.FindScript<ChapterGroup>();
			string text = CommunityRanked.OneLanguage(names);
			string[] array = (names ?? string.Empty).Split('\n');
			if (array.Length > 1 && CommunityRanked.Thai)
			{
				text = string.Join("  ·  ", array);
			}
			if (chapterGroup != null)
			{
				chapterGroup.Show(CommunityRanked.T("จบฤด\u0e39 · ได\u0e49ฉายาใหม\u0e48", "Season over · new title"), text);
			}
			else
			{
				UIManager.SystemMsg(text, 4f);
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static string Describe()
	{
		if (_inst == null)
		{
			return "ui none";
		}
		return "ui lobby " + (_inst._lobbyWin != null && _inst._lobbyWin.IsOpened) + " found " + Active(_inst._found) + " hud " + Active(_inst._hud) + " count " + Active(_inst._count) + " potion " + Active(_inst._potion) + " break " + Active(_inst._break) + " result " + (_inst._resultWin != null && _inst._resultWin.IsOpened) + " ringwarn " + Active(_inst._ringWarn);
	}

	private static bool Active(GameObject go)
	{
		if (go != null)
		{
			return go.activeSelf;
		}
		return false;
	}

	public static void DecorateBanner(UITexture texture)
	{
		if (!(texture == null))
		{
			texture.enabled = false;
			Transform transform = texture.transform.Find("CommunityRankedArt");
			if (transform == null)
			{
				UISprite uISprite = texture.gameObject.AddSprite("s01_banner_img", texture.depth);
				uISprite.name = "CommunityRankedArt";
				uISprite.type = UIBasicSprite.Type.Simple;
				uISprite.pivot = texture.pivot;
				uISprite.width = texture.width;
				uISprite.height = texture.height;
				uISprite.transform.localPosition = Vector3.zero;
			}
			else
			{
				transform.gameObject.SetActive(value: true);
			}
		}
	}

	public static void UndecorateBanner(UITexture texture)
	{
		if (!(texture == null))
		{
			Transform transform = texture.transform.Find("CommunityRankedArt");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
				texture.enabled = true;
			}
		}
	}

	private void Build()
	{
		base.gameObject.AddComponent<UIPanel>().depth = 10;
		PvpIslandGroup src = UIManager.FindScript<PvpIslandGroup>();
		PvpIslandResultGroup pvpIslandResultGroup = UIManager.FindScript<PvpIslandResultGroup>();
		ChapterGroup chapterGroup = UIManager.FindScript<ChapterGroup>();
		_hudDepth = PanelDepth(FindHudPanel(), 10);
		_modalDepth = PanelDepth((pvpIslandResultGroup != null) ? pvpIslandResultGroup.GetComponent<UIPanel>() : null, 140);
		_overDepth = PanelDepth((chapterGroup != null) ? chapterGroup.GetComponent<UIPanel>() : null, _modalDepth + 5);
		_overLayer = ((chapterGroup != null) ? chapterGroup.gameObject.layer : (-1));
		BuildLobby(src);
		BuildResult(pvpIslandResultGroup);
		BuildFound(chapterGroup);
		BuildHud();
		BuildCountdown();
		BuildRingWarn();
		BuildPotion();
		BuildBreak();
		Step(BuildAnim);
		CommunityRanked.Changed += Refresh;
		Refresh();
		CommunityRankedPages.Attach(this);
	}

	private void OnDestroy()
	{
		CommunityRanked.Changed -= Refresh;
		if (_inst == this)
		{
			_inst = null;
		}
		if (_lobbyWin != null)
		{
			UnityEngine.Object.Destroy(_lobbyWin.gameObject);
		}
		if (_resultWin != null)
		{
			UnityEngine.Object.Destroy(_resultWin.gameObject);
		}
	}

	private static UIPanel FindHudPanel()
	{
		try
		{
			MinimapGroupBase minimapGroupBase = UIManager.FindScript<MinimapGroupBase>();
			if (minimapGroupBase != null)
			{
				return minimapGroupBase.GetComponent<UIPanel>();
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static int PanelDepth(UIPanel panel, int fallback)
	{
		if (!(panel != null))
		{
			return fallback;
		}
		return panel.depth;
	}

	private static CommunityRankedWindow CloneWindow(UIBase src, string name)
	{
		GameObject gameObject = new GameObject("CommunityRankedHolder");
		gameObject.SetActive(value: false);
		try
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(src.gameObject, gameObject.transform, worldPositionStays: false);
			gameObject2.name = name;
			Component component = gameObject2.GetComponent(src.GetType());
			CommunityRankedWindow communityRankedWindow = gameObject2.AddComponent<CommunityRankedWindow>();
			string[] uIBaseFields = UIBaseFields;
			foreach (string text in uIBaseFields)
			{
				FieldInfo field = typeof(UIBase).GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
				if (!(field == null))
				{
					object obj = field.GetValue(src);
					Component component2 = obj as Component;
					if (component2 != null)
					{
						obj = Remap(component2, src.transform, gameObject2.transform);
					}
					field.SetValue(communityRankedWindow, obj);
				}
			}
			if (component != null)
			{
				UnityEngine.Object.DestroyImmediate(component);
			}
			gameObject2.transform.SetParent(src.transform.parent, worldPositionStays: false);
			gameObject2.transform.localPosition = src.transform.localPosition;
			gameObject2.transform.localScale = src.transform.localScale;
			gameObject2.layer = src.gameObject.layer;
			gameObject2.SetActive(value: true);
			communityRankedWindow.Init(UIRootAnchor.GetRootAnchor(communityRankedWindow.Anchor));
			communityRankedWindow.HideChildren();
			return communityRankedWindow;
		}
		finally
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private static Component Remap(Component c, Transform srcRoot, Transform dstRoot)
	{
		string text = RelPath(c.transform, srcRoot);
		if (text == null)
		{
			return c;
		}
		Transform transform = ((text.Length == 0) ? dstRoot : dstRoot.Find(text));
		if (!(transform == null))
		{
			return transform.GetComponent(c.GetType());
		}
		return null;
	}

	private static string RelPath(Transform t, Transform root)
	{
		string text = string.Empty;
		while (t != null && t != root)
		{
			text = ((text.Length == 0) ? t.name : (t.name + "/" + text));
			t = t.parent;
		}
		if (!(t == root))
		{
			return null;
		}
		return text;
	}

	private static GameObject CloneStatic(GameObject src, Transform parent, bool keepTweens = false)
	{
		GameObject gameObject = new GameObject("CommunityRankedHolder");
		gameObject.SetActive(value: false);
		try
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(src, gameObject.transform, worldPositionStays: false);
			MonoBehaviour[] componentsInChildren = gameObject2.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
			foreach (MonoBehaviour monoBehaviour in componentsInChildren)
			{
				if (!(monoBehaviour is UIRect) && (!keepTweens || !(monoBehaviour is UITweener)))
				{
					UnityEngine.Object.DestroyImmediate(monoBehaviour);
				}
			}
			Animation[] componentsInChildren2 = gameObject2.GetComponentsInChildren<Animation>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				UnityEngine.Object.DestroyImmediate(componentsInChildren2[i]);
			}
			Animator[] componentsInChildren3 = gameObject2.GetComponentsInChildren<Animator>(includeInactive: true);
			for (int i = 0; i < componentsInChildren3.Length; i++)
			{
				UnityEngine.Object.DestroyImmediate(componentsInChildren3[i]);
			}
			UIRect[] componentsInChildren4 = gameObject2.GetComponentsInChildren<UIRect>(includeInactive: true);
			foreach (UIRect obj in componentsInChildren4)
			{
				obj.alpha = 1f;
				obj.gameObject.SetActive(value: true);
			}
			gameObject2.transform.SetParent(parent, worldPositionStays: false);
			gameObject2.transform.localScale = Vector3.one;
			gameObject2.SetActive(value: true);
			return gameObject2;
		}
		finally
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private SelectableButton CloneButton(SelectableButton tpl, GameObject parent, float x, float y, int w, int h, int depth, PresetButton.Style style, string text, Action onClick)
	{
		if (tpl == null)
		{
			return null;
		}
		GameObject gameObject = new GameObject("CommunityRankedHolder");
		gameObject.SetActive(value: false);
		try
		{
			GameObject obj = UnityEngine.Object.Instantiate(tpl.gameObject, gameObject.transform, worldPositionStays: false);
			obj.name = "Button";
			SelectableButton component = obj.GetComponent<SelectableButton>();
			UIWidget component2 = obj.GetComponent<UIWidget>();
			if (component2 != null)
			{
				component2.leftAnchor.target = null;
				component2.rightAnchor.target = null;
				component2.bottomAnchor.target = null;
				component2.topAnchor.target = null;
				component2.depth = depth;
				component2.pivot = UIWidget.Pivot.Center;
			}
			obj.transform.SetParent(parent.transform, worldPositionStays: false);
			obj.transform.localPosition = new Vector3(x, y, 0f);
			obj.transform.localScale = Vector3.one;
			NGUITools.SetLayer(obj, parent.layer);
			obj.SetActive(value: true);
			component.SetStyle(style);
			if (component2 != null)
			{
				component2.ResetAnchors();
				component2.transform.localPosition = new Vector3(x, y, 0f);
			}
			component.SetDimensions(w, h);
			component.Text = text;
			component.Clicked = delegate
			{
				Safe(onClick);
			};
			return component;
		}
		finally
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private static void Safe(Action a)
	{
		try
		{
			a?.Invoke();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static T Find<T>(Transform root, string path) where T : Component
	{
		Transform transform = ((root != null) ? root.Find(path) : null);
		if (!(transform != null))
		{
			return null;
		}
		return transform.GetComponent<T>();
	}

	private GameObject Layer(string name, int depth, int layer = -1)
	{
		GameObject gameObject = base.gameObject.AddChild();
		gameObject.name = name;
		if (layer >= 0)
		{
			gameObject.layer = layer;
		}
		gameObject.AddComponent<UIPanel>().depth = depth;
		gameObject.SetActive(value: false);
		return gameObject;
	}

	private static GameObject Anchored(GameObject parent, UIBase.AnchorType type, float fx, float fy, int ox, int oy, int w, int h)
	{
		UIWidget uIWidget = parent.AddWidget<UIWidget>(0);
		uIWidget.name = "Anchor";
		UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(type);
		if (rootAnchor != null)
		{
			uIWidget.SetAnchor(rootAnchor.gameObject, fx, ox - w / 2, fy, oy - h / 2, fx, ox + w / 2, fy, oy + h / 2);
		}
		else
		{
			uIWidget.width = w;
			uIWidget.height = h;
		}
		return uIWidget.gameObject;
	}

	private static void Stretch(UIWidget w, UIBase.AnchorType type = UIBase.AnchorType.Base)
	{
		UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(type);
		if (rootAnchor != null)
		{
			w.SetAnchor(rootAnchor.gameObject, 0f, -2, 0f, -2, 1f, 2, 1f, 2);
			return;
		}
		w.width = 1280;
		w.height = 720;
	}

	private static UISprite Spr(GameObject parent, string sprite, float x, float y, float w, float h, Color color, int depth, UIWidget.Pivot pivot = UIWidget.Pivot.Center, bool simple = false)
	{
		UISprite uISprite = parent.AddSprite(sprite, depth);
		if (simple)
		{
			uISprite.type = UIBasicSprite.Type.Simple;
		}
		uISprite.pivot = pivot;
		uISprite.width = Mathf.Max(2, Mathf.RoundToInt(w));
		uISprite.height = Mathf.Max(2, Mathf.RoundToInt(h));
		uISprite.color = color;
		uISprite.transform.localPosition = new Vector3(Mathf.Round(x), Mathf.Round(y), 0f);
		return uISprite;
	}

	private static Color K(float a)
	{
		return new Color(0f, 0f, 0f, a);
	}

	private static Color W(float a)
	{
		return new Color(1f, 1f, 1f, a);
	}

	private UILabel Lbl(GameObject parent, float x, float y, int w, int h, int size, Color color, int depth, NGUIText.Alignment align = NGUIText.Alignment.Center, bool shadow = false)
	{
		UILabel uILabel = parent.AddWidget<UILabel>(depth);
		if (_font != null)
		{
			uILabel.bitmapFont = _font.bitmapFont;
			if (_font.trueTypeFont != null)
			{
				uILabel.trueTypeFont = _font.trueTypeFont;
			}
		}
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.alignment = align;
		uILabel.pivot = align switch
		{
			NGUIText.Alignment.Right => UIWidget.Pivot.Right,
			NGUIText.Alignment.Left => UIWidget.Pivot.Left,
			_ => UIWidget.Pivot.Center,
		};
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.width = w;
		uILabel.height = h;
		uILabel.effectStyle = (shadow ? UILabel.Effect.Shadow : UILabel.Effect.None);
		uILabel.effectColor = K(0.6f);
		uILabel.transform.localPosition = new Vector3(Mathf.Round(x), Mathf.Round(y), 0f);
		return uILabel;
	}

	private static void Click(UIWidget w, Action onClick)
	{
		NGUITools.AddWidgetCollider(w.gameObject);
		UIEventListener.Get(w.gameObject).onClick = delegate
		{
			Safe(onClick);
		};
	}

	private Emblem MakeEmblem(GameObject parent, float cx, float cy, float h, int depth)
	{
		float num = h / 63f;
		float num2 = 51f * num;
		Emblem emblem = new Emblem();
		emblem.Go = parent.AddChild();
		emblem.Go.name = "Emblem";
		emblem.Go.transform.localPosition = new Vector3(Mathf.Round(cx), Mathf.Round(cy), 0f);
		UIWidget uIWidget = emblem.Go.AddComponent<UIWidget>();
		uIWidget.width = Mathf.Max(2, Mathf.RoundToInt(num2 * 2f));
		uIWidget.height = Mathf.Max(2, Mathf.RoundToInt(h));
		uIWidget.depth = depth;
		emblem.WingL = Spr(emblem.Go, "tribalwar_img_wing_left", (0f - num2) * 0.62f, 4f * num, 32.2f * num, 49.45f * num, Color.white, depth, UIWidget.Pivot.Center, simple: true);
		emblem.WingR = Spr(emblem.Go, "tribalwar_img_wing", num2 * 0.62f, 4f * num, 32.2f * num, 49.45f * num, Color.white, depth, UIWidget.Pivot.Center, simple: true);
		emblem.Shield = Spr(emblem.Go, "tribalwar_img_shield", 0f, 0f, num2, h, Color.white, depth + 1);
		emblem.Mark = Spr(emblem.Go, "Icon_battle_emblem", 0f, -2f * num, 26f * num, 26f * num, new Color(0.08f, 0.07f, 0.06f, 0.85f), depth + 2);
		emblem.Crown = Spr(emblem.Go, "crown", 0f, h / 2f + 6f * num, 22f * num, 22f * num, Color.white, depth + 3);
		emblem.Unknown = Lbl(emblem.Go, 0f, 2f * num, Mathf.RoundToInt(num2), Mathf.RoundToInt(h), Mathf.RoundToInt(30f * num), W(0.7f), depth + 2);
		emblem.Unknown.text = "?";
		return emblem;
	}

	private static void SetEmblem(Emblem e, string tier)
	{
		if (e != null)
		{
			bool flag = string.IsNullOrEmpty(tier);
			Color color = (flag ? W(0.22f) : TierColor(tier));
			e.Shield.color = color;
			bool active = tier == "diamond" || tier == "master";
			e.WingL.gameObject.SetActive(active);
			e.WingR.gameObject.SetActive(active);
			CommunityRankedAnim.Tint(e.WingL, color);
			CommunityRankedAnim.Tint(e.WingR, color);
			e.Crown.gameObject.SetActive(tier == "master");
			e.Crown.color = color;
			e.Mark.gameObject.SetActive(!flag);
			e.Unknown.gameObject.SetActive(flag);
		}
	}

	private static Color TierColor(string tier)
	{
		return tier switch
		{
			"copper" => new Color(0.8f, 0.5f, 0.28f),
			"silver" => new Color(0.8f, 0.82f, 0.84f),
			"gold" => Yellow,
			"platinum" => new Color(0.55f, 0.86f, 0.8f),
			"diamond" => new Color(0.58f, 0.76f, 1f),
			"master" => new Color(0.93f, 0.47f, 0.3f),
			_ => W(0.7f),
		};
	}

	private static string Hex(Color c)
	{
		return ((int)(c.r * 255f)).ToString("X2") + ((int)(c.g * 255f)).ToString("X2") + ((int)(c.b * 255f)).ToString("X2");
	}

	private static string TierText(string tier)
	{
		if (string.IsNullOrEmpty(tier))
		{
			return CommunityRanked.T("ย\u0e31งไม\u0e48ม\u0e35ข\u0e31\u0e49น", "Unranked");
		}
		return "[" + Hex(TierColor(tier)) + "]" + CommunityRanked.TierName(tier) + "[-]";
	}

	private Bar MakeBar(GameObject parent, float left, float cy, int w, int depth, int fontSize)
	{
		Bar bar = new Bar();
		bar.Go = parent.AddChild();
		bar.Go.name = "Progress";
		bar.Go.transform.localPosition = new Vector3(Mathf.Round(left + (float)w / 2f), Mathf.Round(cy), 0f);
		Spr(bar.Go, "gauge_s02_box", 0f, 0f, w, 8f, K(0.65f), depth, UIWidget.Pivot.Center, simple: true);
		bar.Light = Spr(bar.Go, "gauge_s02_box", 0f, 0f, w, 8f, new Color(1f, 0.97f, 0.8f), depth + 1, UIWidget.Pivot.Center, simple: true);
		bar.Light.type = UIBasicSprite.Type.Filled;
		bar.Light.fillDirection = UIBasicSprite.FillDirection.Horizontal;
		bar.Fill = Spr(bar.Go, "gauge_s02_box", 0f, 0f, w, 8f, Yellow, depth + 2, UIWidget.Pivot.Center, simple: true);
		bar.Fill.type = UIBasicSprite.Type.Filled;
		bar.Fill.fillDirection = UIBasicSprite.FillDirection.Horizontal;
		bar.Label = Lbl(bar.Go, 0f, -19f, w + 60, 24, fontSize, Color.white, depth + 3);
		return bar;
	}

	private static void SetBar(Bar bar, double before, double after, string text)
	{
		float num = Mathf.Clamp01((float)before);
		float num2 = Mathf.Clamp01((float)after);
		bar.Fill.fillAmount = Mathf.Min(num, num2);
		bar.Light.fillAmount = Mathf.Max(num, num2);
		bar.Light.color = ((num2 >= num) ? new Color(1f, 0.97f, 0.8f) : new Color(0.95f, 0.21f, 0.02f, 0.6f));
		bar.Label.text = text ?? string.Empty;
	}

	private static string ToNext(string next, string delta = null)
	{
		string text = "[FFFFFF8C]" + CommunityRanked.T("ถ\u0e36งข\u0e31\u0e49น", "to") + "[-] " + (string.IsNullOrEmpty(next) ? "-" : TierText(next));
		if (!string.IsNullOrEmpty(delta))
		{
			text = text + "   " + delta;
		}
		return text;
	}

	private static string Num(int n)
	{
		return n.ToString("N0", CultureInfo.InvariantCulture);
	}

	private void BuildLobby(PvpIslandGroup src)
	{
		if (src == null)
		{
			return;
		}
		_lobbyWin = CloneWindow(src, "CommunityRankedLobby");
		Transform transform = _lobbyWin.transform;
		_lCenter = transform.Find("Container/Contents/Center");
		_lUITitle = transform.GetComponentInChildren<UITitle>(includeInactive: true);
		Transform transform2 = transform.Find("Container");
		_lLayout = ((transform2 != null) ? transform2.GetComponent<RectLayoutComponent>() : null);
		_lTitle = Find<UILabel>(transform, "Container/Contents/Center/Top/Title");
		_lDesc = Find<UILabel>(transform, "Container/Contents/Center/Top/Description");
		_lGuide = Find<SelectableButton>(transform, "Container/Contents/Center/Top/Button");
		string[] array = new string[4] { "WinCount", "PlayCount", "TotalKill", "AverageKill" };
		for (int i = 0; i < 4; i++)
		{
			_lScores[i] = Find<UILabel>(transform, "Container/Contents/Center/Bottom/" + array[i]);
		}
		Transform transform3 = transform.Find("Container/Contents/Center/Bottom/WinCount/Highlight");
		_lHighlight = ((transform3 != null) ? transform3.gameObject : null);
		_lQueue = Find<UILabel>(transform, "Container/Contents/Bottom/QueueCount");
		_lEnter = Find<SelectableButton>(transform, "Container/Contents/Bottom/Button");
		_font = _lDesc ?? _lTitle ?? FindStyle();
		_solidTpl = _lEnter;
		_borderTpl = _lGuide;
		if (_lUITitle != null)
		{
			FieldInfo field = typeof(UITitle).GetField("_titleName", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
			{
				field.SetValue(_lUITitle, CommunityRanked.T("ดวลจ\u0e31ดอ\u0e31นด\u0e31บ", "Ranked duel"));
			}
		}
		if (_lGuide != null)
		{
			_lGuide.Clicked = ShowGuide;
		}
		if (_lEnter != null)
		{
			_lEnter.Clicked = delegate
			{
				Safe(delegate
				{
					if (CommunityRanked.Queued)
					{
						CommunityRanked.Dequeue();
					}
					else
					{
						CommunityRanked.Queue(_lobbyMode);
					}
				});
			};
		}
		_lobbyWin.OnCloseSucceed += delegate
		{
			_lobbyOpen = false;
		};
		_lobbyWin.OnOpenSucceed += delegate
		{
			if (_lLayout != null)
			{
				_lLayout.UpdateLayout();
			}
			if (_lUITitle != null && _lUITitle.Object != null)
			{
				_lUITitle.Object.SetTitle(CommunityRanked.T("ดวลจ\u0e31ดอ\u0e31นด\u0e31บ", "Ranked duel"));
			}
		};
		if (_lCenter == null)
		{
			return;
		}
		_lAdd = _lCenter.gameObject.AddChild();
		_lAdd.name = "CommunityRanked";
		GameObject lAdd = _lAdd;
		int num = 30;
		for (int num2 = 0; num2 < 2; num2++)
		{
			GameObject gameObject = lAdd.AddChild();
			gameObject.name = "Tab" + num2;
			PlaceR(gameObject.transform, 742 + num2 * 262 + 129, 129.5f);
			_tabBg[num2] = Spr(gameObject, "bg_roundbox2", 0f, 0f, 258f, 59f, new Color(0.216f, 0.216f, 0.216f, 0.8f), num);
			Spr(gameObject, "bg_box_grunge", 0f, 0f, 260f, 60f, K(1f), num + 1);
			_tabLine[num2] = Spr(gameObject, "line_grunge_10px", 0f, -22.5f, 256f, 10f, Yellow, num + 2);
			_tabText[num2] = Lbl(gameObject, 0f, 0f, 240, 50, 24, Color.white, num + 3);
			string mode = ((num2 == 0) ? "1v1" : "3v3");
			Click(_tabBg[num2], delegate
			{
				if (!CommunityRanked.Queued)
				{
					_lobbyMode = mode;
					Refresh();
				}
			});
		}
		GameObject gameObject2 = lAdd.AddChild();
		gameObject2.name = "Tier";
		PlaceR(gameObject2.transform, 1015f, 295f);
		gameObject2.AddComponent<UIWidget>().depth = num;
		_lTierGo = gameObject2;
		_lGlow = Spr(gameObject2, "bg_gradition", 0f, 0f, 190f, 190f, new Color(1f, 0.847f, 0.357f, 0.25f), num, UIWidget.Pivot.Center, simple: true);
		_lEmblem = MakeEmblem(gameObject2, 0f, 3f, 126f, num + 1);
		_lTier = Lbl(gameObject2, 0f, -97f, 320, 46, 38, Color.white, num + 5);
		_lPoints = Lbl(gameObject2, 0f, -137f, 320, 32, 28, Color.white, num + 5);
		_lBar = MakeBar(gameObject2, -130f, -133f, 260, num + 5, 17);
		_lPlaceTitle = Lbl(gameObject2, 0f, -95f, 320, 44, 30, Color.white, num + 5);
		_lPlaceCount = Lbl(gameObject2, 0f, -133f, 320, 32, 26, Color.white, num + 5);
		_lPlaceNote = Lbl(gameObject2, 0f, -167f, 340, 30, 18, W(0.7f), num + 5);
		GameObject gameObject3 = lAdd.AddChild();
		gameObject3.name = "Season";
		PlaceR(gameObject3.transform, 930f, 497f);
		_lSeasonIcon = Spr(gameObject3, "icon_skill_time", -15f, 0f, 20f, 20f, W(0.7f), num + 5);
		_lSeason = Lbl(gameObject3, 0f, 0f, 300, 26, 19, W(0.75f), num + 5, NGUIText.Alignment.Left);
		GameObject gameObject4 = lAdd.AddChild();
		gameObject4.name = "Party";
		PlaceL(gameObject4.transform, 61f, 296f);
		gameObject4.AddComponent<UIWidget>().depth = num;
		_lPartyGo = gameObject4;
		_lPartyHead = Lbl(gameObject4, 0f, -15f, 640, 30, 20, W(0.7f), num + 5, NGUIText.Alignment.Left);
		for (int num3 = 0; num3 < 3; num3++)
		{
			Chip chip = new Chip();
			chip.Go = gameObject4.AddChild();
			chip.Go.transform.localPosition = new Vector3(num3 * 200, -56f, 0f);
			Spr(chip.Go, "bg_round_box", 92f, 0f, 184f, 44f, new Color(0.016f, 0.016f, 0.016f, 0.549f), num);
			Spr(chip.Go, "img_slide_circle_inside", 22f, 0f, 40f, 40f, K(0.36f), num + 1, UIWidget.Pivot.Center, simple: true);
			Spr(chip.Go, "circle_32", 22f, 0f, 20f, 20f, K(1f), num + 2);
			chip.Number = Lbl(chip.Go, 22f, 0f, 20, 20, 16, Yellow, num + 3);
			chip.Number.text = (num3 + 1).ToString();
			chip.Crown = Spr(chip.Go, "crown_outline", 22f, 12f, 18f, 18f, Yellow, num + 3);
			chip.Name = Lbl(chip.Go, 48f, 9f, 130, 22, 16, Color.white, num + 3, NGUIText.Alignment.Left);
			chip.Tier = Lbl(chip.Go, 48f, -11f, 130, 18, 14, new Color(0.765f, 0.765f, 0.765f), num + 3, NGUIText.Alignment.Left);
			_lChips[num3] = chip;
		}
		_lInviteIcon = Spr(gameObject4, "icon_mainhud_party", 19f, -87f, 30f, 28f, W(0.7f), num + 3);
		_lInvite = Lbl(gameObject4, 39f, -87f, 300, 30, 20, Yellow, num + 3, NGUIText.Alignment.Left);
		_lInvite.text = CommunityRanked.T("ชวนเข\u0e49าปาร\u0e4cต\u0e35\u0e49", "Invite to party");
		Click(_lInvite, delegate
		{
			UIBase.CloseAllUI();
			MenuHelper.Open(MenuType.Party);
		});
	}

	private void PlaceL(Transform t, float mx, float my)
	{
		_lPlaced.Add(new KeyValuePair<Transform, Vector3>(t, new Vector3(mx, my, 0f)));
	}

	private void PlaceR(Transform t, float mx, float my)
	{
		_lPlaced.Add(new KeyValuePair<Transform, Vector3>(t, new Vector3(mx, my, 1f)));
	}

	private void RelayoutLobby()
	{
		UIWidget uIWidget = ((_lCenter != null) ? _lCenter.GetComponent<UIWidget>() : null);
		float num = ((uIWidget != null && uIWidget.width > 100) ? ((float)uIWidget.width) : 1260f);
		float num2 = ((uIWidget != null && uIWidget.height > 100) ? ((float)uIWidget.height) : 524f);
		Vector2 vector = ((uIWidget != null) ? new Vector2((0.5f - uIWidget.pivotOffset.x) * num, (0.5f - uIWidget.pivotOffset.y) * num2) : Vector2.zero);
		if (_lAdd != null)
		{
			_lAdd.transform.localPosition = new Vector3(Mathf.Round(vector.x), Mathf.Round(vector.y), 0f);
		}
		foreach (KeyValuePair<Transform, Vector3> item in _lPlaced)
		{
			float f = ((item.Value.z > 0.5f) ? (num / 2f - (1270f - item.Value.x)) : ((0f - num) / 2f + (item.Value.x - 10f)));
			float f2 = num2 / 2f - (item.Value.y - 86f);
			item.Key.localPosition = new Vector3(Mathf.Round(f), Mathf.Round(f2), 0f);
		}
	}

	private static void FixTitle(UITitle title, string text)
	{
		UITitleWidget uITitleWidget = ((title != null) ? title.Object : null);
		if (uITitleWidget == null)
		{
			return;
		}
		FieldInfo field = typeof(UITitleWidget).GetField("TitleLabel", BindingFlags.Instance | BindingFlags.NonPublic);
		UILabel uILabel = ((field != null) ? (field.GetValue(uITitleWidget) as UILabel) : null);
		if (uILabel == null)
		{
			uITitleWidget.SetTitle(text);
			return;
		}
		if (uILabel.text != text)
		{
			uITitleWidget.SetTitle(text);
		}
		if (uILabel.overflowMethod != UILabel.Overflow.ResizeFreely)
		{
			uILabel.rightAnchor.target = null;
			uILabel.overflowMethod = UILabel.Overflow.ResizeFreely;
			uILabel.ResetAnchors();
			uILabel.MarkAsChanged();
		}
	}

	private void ShowGuide()
	{
		if (!(UIManager.MessageBox == null))
		{
			if (CommunityRanked.OwnGear)
			{
				int num = CommunityRanked.Int(CommunityRanked.Lobby?["potionsPerRound"], 3);
				UIManager.MessageBox.Show(CommunityRanked.T("ดวล 3 ยก — ชนะ 2 ใน 3 ยก · ยกละ 120 ว\u0e34นาท\u0e35\nใช\u0e49ต\u0e31วละครจร\u0e34ง: เลเวล สก\u0e34ล อาว\u0e38ธ เกราะของต\u0e31วเอง · จ\u0e31บค\u0e39\u0e48ก\u0e31บคนเลเวลใกล\u0e49ก\u0e31น\nแจกแค\u0e48ยาฟ\u0e37\u0e49นเล\u0e37อด " + num + " ขวด/ยก (ของก\u0e34น/ยาของต\u0e31วเองใช\u0e49ไม\u0e48ได\u0e49) · อาว\u0e38ธ/เกราะไม\u0e48เส\u0e35ยความทน\nเล\u0e37อดหมด = แพ\u0e49ยก (ไม\u0e48ตาย ไม\u0e48ม\u0e35ของตก) · ย\u0e37นน\u0e34\u0e48ง 20 ว\u0e34นาท\u0e35 = แพ\u0e49ยก\nพ\u0e31กยก: เปล\u0e35\u0e48ยนอาว\u0e38ธ/เกราะได\u0e49 · ท\u0e35มท\u0e35\u0e48แพ\u0e49ยกเล\u0e37อกฝ\u0e31\u0e48งเก\u0e34ด\nออกก\u0e48อนจบหร\u0e37อหล\u0e38ดเก\u0e34น 30 ว\u0e34นาท\u0e35 = แพ\u0e49ท\u0e31\u0e49งแมตช\u0e4c\nแมตช\u0e4cว\u0e31ดระด\u0e31บ 5 แมตช\u0e4cแรก แล\u0e49วได\u0e49ข\u0e31\u0e49นของฤด\u0e39", "Best of 3 — win 2 of 3 rounds · 120 s per round\nYour own character: your level, skills, weapons and armour · matched with players of a similar level\nOnly " + num + " healing potions per round are given (your own food/potions can't be used) · gear takes no durability loss\nOut of health = round lost (no death, no drops) · idle 20 s = round lost\nBreak: change weapons/armour · the team that lost picks the spawn side\nLeaving or disconnecting over 30 s = match lost\n5 placement matches, then your season rank"));
			}
			else
			{
				UIManager.MessageBox.Show(CommunityRanked.T("ดวล 3 ยก — ชนะ 2 ใน 3 ยก · ยกละ 120 ว\u0e34นาท\u0e35\nท\u0e38กคนเป\u0e47น Lv60 สก\u0e34ลเต\u0e47ม ช\u0e38ดมาตรฐานเท\u0e48าก\u0e31น · ของจร\u0e34งในกระเป\u0e4bาไม\u0e48ถ\u0e39กแตะ\nเล\u0e37อดหมด = แพ\u0e49ยก (ไม\u0e48ตาย ไม\u0e48ม\u0e35ของตก) · ย\u0e37นน\u0e34\u0e48ง 20 ว\u0e34นาท\u0e35 = แพ\u0e49ยก\nพ\u0e31กยก: เล\u0e37อกอาว\u0e38ธ · ท\u0e35มท\u0e35\u0e48แพ\u0e49ยกเล\u0e37อกฝ\u0e31\u0e48งเก\u0e34ด · ยาฟ\u0e37\u0e49นเล\u0e37อด 3 ขวด/ยก\nออกก\u0e48อนจบหร\u0e37อหล\u0e38ดเก\u0e34น 30 ว\u0e34นาท\u0e35 = แพ\u0e49ท\u0e31\u0e49งแมตช\u0e4c\nแมตช\u0e4cว\u0e31ดระด\u0e31บ 5 แมตช\u0e4cแรก แล\u0e49วได\u0e49ข\u0e31\u0e49นของฤด\u0e39", "Best of 3 — win 2 of 3 rounds · 120 s per round\nEveryone is Lv60 with every skill and the same kit · your real bag is untouched\nOut of health = round lost (no death, no drops) · idle 20 s = round lost\nBreak: pick a weapon · the team that lost picks the spawn side · 3 potions per round\nLeaving or disconnecting over 30 s = match lost\n5 placement matches, then your season rank"));
			}
		}
	}

	private static string ScoreText(string title, string value, bool em)
	{
		if (em)
		{
			value = "<em>" + value + "</em>";
		}
		return "[FFFFFFB4]" + title + "<br>18</br>[-][size=32][b]" + value + "[/b][/size]<br>18</br>[size=18][preset=round_box? " + CommunityRanked.T("ฤด\u0e39น\u0e35\u0e49", "season") + " ][/size]";
	}

	private void RefreshLobby()
	{
		if (_lobbyWin == null)
		{
			return;
		}
		if (!_lobbyOpen || CommunityRanked.Lobby == null || CommunityRanked.InMatch || CommunityRanked.Found != null)
		{
			if (_lobbyWin.IsOpened)
			{
				_lobbyWin.ForceClose();
			}
			_lobbyOpen = _lobbyOpen && !CommunityRanked.InMatch && CommunityRanked.Found == null;
			return;
		}
		bool flag = !_lobbyWin.IsOpened;
		if (!_lobbyWin.IsOpened && !_lobbyWin.Open())
		{
			_lobbyOpen = false;
			return;
		}
		RelayoutLobby();
		FixTitle(_lUITitle, CommunityRanked.T("ดวลจ\u0e31ดอ\u0e31นด\u0e31บ", "Ranked duel"));
		JObject lobby = CommunityRanked.Lobby;
		bool queued = CommunityRanked.Queued;
		if (queued)
		{
			_lobbyMode = CommunityRanked.QueuedMode;
		}
		bool flag2 = _lobbyMode == "3v3";
		for (int i = 0; i < 2; i++)
		{
			bool flag3 = i == 1 == flag2;
			_tabLine[i].gameObject.SetActive(flag3);
			_tabText[i].text = ((i == 0) ? CommunityRanked.T("ดวล 1v1", "Duel 1v1") : CommunityRanked.T("ท\u0e35ม 3v3", "Team 3v3"));
			_tabText[i].color = (flag3 ? Color.white : Grey);
		}
		if (_lTitle != null)
		{
			_lTitle.text = (flag2 ? CommunityRanked.T("ดวล 3 ยก · ท\u0e35ม 3v3", "Best of 3 · Team 3v3") : CommunityRanked.T("ดวล 3 ยก · 1v1", "Best of 3 · 1v1"));
		}
		if (_lDesc != null)
		{
			_lDesc.text = (flag2 ? CommunityRanked.T("ท\u0e35มละ 3 คน ตายหมดท\u0e35มแพ\u0e49ยก · ", "3 per team; a wiped team loses the round · ") : string.Empty) + (CommunityRanked.OwnGear ? CommunityRanked.T("ชนะ 2 ใน 3 ยก · ยกละ 120 ว\u0e34นาท\u0e35 · ใช\u0e49ต\u0e31วละครจร\u0e34ง (เลเวล สก\u0e34ล อาว\u0e38ธ เกราะของต\u0e31วเอง) · แจกแค\u0e48ยา · เปล\u0e35\u0e48ยนอาว\u0e38ธ/เกราะได\u0e49ตอนพ\u0e31กยก · ไม\u0e48ม\u0e35ของตก ไม\u0e48ได\u0e49 exp", "Win 2 of 3 rounds · 120 s per round · your own character (level, skills, weapons, armour) · only potions are given · change gear between rounds · no drops, no exp") : CommunityRanked.T("ชนะ 2 ใน 3 ยก · ยกละ 120 ว\u0e34นาท\u0e35 · ท\u0e38กคน Lv60 สก\u0e34ลเต\u0e47ม ช\u0e38ดเกราะเด\u0e35ยวก\u0e31น · เล\u0e37อกอาว\u0e38ธได\u0e49ตอนพ\u0e31กยก · ไม\u0e48ม\u0e35ของตก ไม\u0e48ได\u0e49 exp", "Win 2 of 3 rounds · 120 s per round · everyone Lv60, all skills maxed, same armour · pick a weapon between rounds · no drops, no exp"));
		}
		if (_lGuide != null)
		{
			_lGuide.gameObject.SetActive(!flag2);
			_lGuide.Text = CommunityRanked.T("ค\u0e39\u0e48ม\u0e37อ", "Guide");
		}
		JObject jObject = lobby["modes"]?[_lobbyMode] as JObject;
		string text = CommunityRanked.Str(jObject?["tier"]);
		int num = CommunityRanked.Int(jObject?["placement"]);
		bool flag4 = string.IsNullOrEmpty(text);
		SetEmblem(_lEmblem, text);
		_lGlow.gameObject.SetActive(!flag4);
		_lTier.gameObject.SetActive(!flag4);
		_lPlaceTitle.gameObject.SetActive(flag4);
		_lPlaceCount.gameObject.SetActive(flag4);
		_lPlaceNote.gameObject.SetActive(flag4);
		double? num2 = CommunityRanked.NumOrNull(jObject?["rating"]);
		_lPoints.gameObject.SetActive(!flag4 && num2.HasValue);
		_lBar.Go.SetActive(!flag4 && !num2.HasValue);
		if (flag4)
		{
			int num3 = Mathf.Clamp(5 - num, 0, 5);
			_lPlaceTitle.text = CommunityRanked.T("แมตช\u0e4cว\u0e31ดระด\u0e31บ", "Placement");
			_lPlaceCount.text = "[FFD85B]" + num3 + "[-][FFFFFF80] / [-]" + 5;
			_lPlaceNote.text = CommunityRanked.T("เล\u0e48นอ\u0e35ก " + num + " แมตช\u0e4c เพ\u0e37\u0e48อร\u0e31บข\u0e31\u0e49นแรกของฤด\u0e39", "Play " + num + " more to get your season rank");
		}
		else
		{
			_lTier.text = TierText(text);
			if (num2.HasValue)
			{
				_lPoints.text = Num((int)num2.Value) + " [FFFFFF99][size=20]" + CommunityRanked.T("แต\u0e49ม", "pts") + "[/size][-]";
			}
			else
			{
				double num4 = CommunityRanked.Num(jObject?["progress"]);
				SetBar(_lBar, num4, num4, ToNext(CommunityRanked.Str(jObject?["next"])));
			}
		}
		double num5 = CommunityRanked.Num(lobby["seasonEndsAt"]);
		int num6 = Mathf.Max(0, Mathf.CeilToInt((float)((num5 - CommunityRanked.Now) / 86400.0)));
		_lSeason.text = CommunityRanked.T("ฤด\u0e39 " + CommunityRanked.Str(lobby["season"]) + " · เหล\u0e37อ " + num6 + " ว\u0e31น", "Season " + CommunityRanked.Str(lobby["season"]) + " · " + num6 + " days left");
		int num7 = CommunityRanked.Int(jObject?["wins"]);
		int num8 = CommunityRanked.Int(jObject?["losses"]);
		string value = ((num7 + num8 > 0) ? (Mathf.RoundToInt(100f * (float)num7 / (float)(num7 + num8)) + "%") : "-");
		string[] array = new string[4]
		{
			ScoreText(CommunityRanked.T("ชนะ", "Wins"), Num(num7), em: true),
			ScoreText(CommunityRanked.T("แพ\u0e49", "Losses"), Num(num8), em: false),
			ScoreText(CommunityRanked.T("อ\u0e31ตราชนะ", "Win rate"), value, em: false),
			ScoreText(CommunityRanked.T("ว\u0e31ดระด\u0e31บ", "Placement"), Mathf.Clamp(5 - num, 0, 5) + "/" + 5, em: false)
		};
		for (int j = 0; j < 4; j++)
		{
			if (_lScores[j] != null)
			{
				_lScores[j].gameObject.SetActive(value: true);
				_lScores[j].text = array[j];
			}
		}
		if (_lHighlight != null)
		{
			_lHighlight.SetActive(value: true);
		}
		RefreshParty(flag2);
		if (_lQueue != null)
		{
			_lQueue.gameObject.SetActive(value: true);
			if (queued)
			{
				double num9 = CommunityRanked.Num(lobby["queue"]?["since"], CommunityRanked.Now);
				_lQueue.text = CommunityRanked.T("รอ", "Waiting") + "  [FFFFFF3C]|[-]  <em>" + CommunityRanked.Clock(CommunityRanked.Now - num9) + "</em>        " + CommunityRanked.T("ในค\u0e34ว", "In queue") + "  [FFFFFF3C]|[-]  <em>" + CommunityRanked.Int(lobby["queue"]?["count"]) + "</em>      [FFFFFF8C][size=19]" + CommunityRanked.T("(รอเก\u0e34น 60 ว\u0e34 ขยายช\u0e48วงท\u0e35ละข\u0e31\u0e49น)", "(widens every step after 60 s)") + "[/size][-]";
			}
			else
			{
				_lQueue.text = "[FFFFFF99]" + CommunityRanked.T("เป\u0e34ด 24 ชม. · เข\u0e49าได\u0e49ต\u0e31\u0e49งแต\u0e48 Lv", "Open 24 h · Lv") + CommunityRanked.Int(lobby["levelMin"], 30) + CommunityRanked.T("", "+") + " · " + (flag2 ? CommunityRanked.T("แต\u0e49มค\u0e34ดรายคน", "points are per player") : CommunityRanked.T("ค\u0e39\u0e48เด\u0e34มน\u0e31บผลไม\u0e48เก\u0e34น 3 แมตช\u0e4c/ว\u0e31น", "same opponent counts ≤3 matches/day")) + "[-]";
			}
		}
		if (_lEnter != null)
		{
			_lEnter.SetStyle(queued ? PresetButton.Style.Flat : PresetButton.Style.Solid);
			_lEnter.Text = (queued ? CommunityRanked.T("ยกเล\u0e34ก", "Cancel") : (flag2 ? CommunityRanked.T("เข\u0e49าค\u0e34ว 3v3", "Queue 3v3") : CommunityRanked.T("เข\u0e49าค\u0e34ว 1v1", "Queue 1v1")));
		}
		if (flag || _animMode != _lobbyMode)
		{
			if (!flag)
			{
				Play(_aTier);
				Play(_aParty);
				Play(_aTabs[flag2 ? 1u : 0u]);
			}
			Play(_lEmblem?.In);
			_animMode = _lobbyMode;
		}
		if (queued != _queuePulse)
		{
			_queuePulse = queued;
			if (queued)
			{
				Play(_aQueue);
			}
			else
			{
				_aQueue?.Finish();
			}
		}
	}

	private void RefreshParty(bool m3)
	{
		_lPartyHead.transform.parent.gameObject.SetActive(m3);
		if (!m3)
		{
			return;
		}
		PartySystem partySystem = null;
		try
		{
			partySystem = GameSystem<PartySystem>.Instance();
		}
		catch (Exception)
		{
		}
		bool flag = partySystem != null && !partySystem.NotInParty && partySystem.MemberCount > 1;
		_lInviteIcon.gameObject.SetActive(!flag);
		_lInvite.gameObject.SetActive(!flag);
		if (!flag)
		{
			_lPartyHead.text = CommunityRanked.T("เข\u0e49าเด\u0e35\u0e48ยว — ระบบเต\u0e34มเพ\u0e37\u0e48อนร\u0e48วมท\u0e35มให\u0e49ครบ 3 คน\nชวนปาร\u0e4cต\u0e35\u0e49 2–3 คน เพ\u0e37\u0e48อเข\u0e49าค\u0e34วพร\u0e49อมก\u0e31น (กล\u0e38\u0e48มเจอกล\u0e38\u0e48มก\u0e48อน)", "Solo — the system fills your team to 3\nInvite a party of 2–3 to queue together (groups meet groups first)");
			_lPartyHead.height = 60;
			_lPartyHead.transform.localPosition = new Vector3(0f, -30f, 0f);
			for (int i = 0; i < 3; i++)
			{
				_lChips[i].Go.SetActive(value: false);
			}
			return;
		}
		_lPartyHead.height = 30;
		_lPartyHead.transform.localPosition = new Vector3(0f, -15f, 0f);
		int memberCount = partySystem.MemberCount;
		_lPartyHead.text = CommunityRanked.T("ปาร\u0e4cต\u0e35\u0e49  [" + memberCount + "/3]  · ", "Party  [" + memberCount + "/3]  · ") + (partySystem.IsLeader ? CommunityRanked.T("ค\u0e38ณเป\u0e47นห\u0e31วหน\u0e49า กดเข\u0e49าค\u0e34วได\u0e49", "you lead — you can queue") : CommunityRanked.T("ห\u0e31วหน\u0e49าปาร\u0e4cต\u0e35\u0e49เป\u0e47นคนกดเข\u0e49าค\u0e34ว", "the leader queues"));
		for (int j = 0; j < 3; j++)
		{
			Member member = null;
			try
			{
				member = ((j < memberCount) ? partySystem.GetMember(j) : null);
			}
			catch (Exception)
			{
			}
			Chip chip = _lChips[j];
			chip.Go.SetActive(member != null);
			if (member != null)
			{
				chip.Crown.gameObject.SetActive(member.IsLeader);
				chip.Name.text = Plain((member.PlayerInfo != null) ? member.PlayerInfo.Name : member.EntityId);
				chip.Tier.text = (member.IsAccepted ? string.Empty : CommunityRanked.T("รอตอบร\u0e31บ", "invited"));
			}
		}
	}

	private void BuildFound(ChapterGroup chapter)
	{
		_found = Layer("Found", _overDepth, _overLayer);
		UISprite uISprite = _found.AddSprite("bg_white", 0);
		uISprite.name = "Dim";
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.color = K(0.6f);
		Stretch(uISprite);
		GameObject gameObject = Anchored(_found, UIBase.AnchorType.Base, 0.5f, 0.5f, 0, 0, 1280, 720);
		Transform transform = ((chapter != null) ? chapter.transform.Find("ChapterEffect00") : null);
		if (transform != null)
		{
			GameObject gameObject2 = CloneStatic(transform.gameObject, gameObject.transform, keepTweens: true);
			gameObject2.name = "Strip";
			gameObject2.transform.localPosition = new Vector3(0f, 210f, 0f);
			_aStrip = CommunityRankedAnim.Own(gameObject2);
			_fTitle = Find<UILabel>(gameObject2.transform, "Title");
			_fSub = Find<UILabel>(gameObject2.transform, "Subtitle");
			if (_fSub != null)
			{
				_fSub.width = 700;
				_fSub.height = 30;
			}
		}
		if (_fTitle == null)
		{
			Spr(gameObject, "bg_white", 0f, 210f, 1280f, 208f, K(1f), 0, UIWidget.Pivot.Center, simple: true);
			Spr(gameObject, "bg_white", 0f, 191f, 600f, 2f, new Color(0.941f, 0.8f, 0.337f, 0.15f), 2, UIWidget.Pivot.Center, simple: true);
			_fTitle = Lbl(gameObject, 0f, 235f, 600, 60, 50, Yellow, 3);
			_fSub = Lbl(gameObject, 0f, 160f, 700, 30, 24, Color.white, 3);
		}
		for (int i = 0; i < 2; i++)
		{
			Card card = new Card();
			card.Go = gameObject.AddChild();
			card.Go.name = ((i == 0) ? "Mine" : "Foes");
			card.Go.transform.localPosition = new Vector3((i == 0) ? (-270) : 270, 0f, 0f);
			card.Body = card.Go.AddWidget<UIWidget>(10).gameObject;
			card.Body.name = "Body";
			GameObject body = card.Body;
			card.Bg = Spr(body, "bg_roundbox2", 0f, 0f, 440f, 150f, K(0.55f), 10, UIWidget.Pivot.Top);
			card.Grunge = Spr(body, "bg_box_grunge", 0f, 1f, 442f, 152f, K(1f), 11, UIWidget.Pivot.Top);
			card.Header = Lbl(body, (i == 0) ? (-200) : 200, -24f, 400, 32, 20, (i == 0) ? Yellow : FoeText, 13, (i == 0) ? NGUIText.Alignment.Left : NGUIText.Alignment.Right);
			for (int j = 0; j < 3; j++)
			{
				float num = -84 - j * 84;
				card.Emblems[j] = MakeEmblem(body, (i == 0) ? (-162) : 162, num, 64f, 12);
				card.Names[j] = Lbl(body, (i == 0) ? (-116) : 116, num + 13f, 320, 38, 28, Color.white, 13, (i == 0) ? NGUIText.Alignment.Left : NGUIText.Alignment.Right);
				card.Tiers[j] = Lbl(body, (i == 0) ? (-116) : 116, num - 18f, 320, 28, 20, Color.white, 13, (i == 0) ? NGUIText.Alignment.Left : NGUIText.Alignment.Right);
				if (j < 2)
				{
					card.Lines[j] = Spr(body, "bg_dotline_white2", 0f, num - 44f, 408f, 2f, W(0.12f), 12);
					card.Lines[j].type = UIBasicSprite.Type.Tiled;
				}
			}
			_fCards[i] = card;
		}
		_fVs = gameObject.AddWidget<UIWidget>(14).gameObject;
		_fVs.name = "Vs";
		_fVs.transform.localPosition = new Vector3(0f, -5f, 0f);
		_fCircle = Spr(_fVs, "bg_circle_100x100", 0f, 0f, 92f, 92f, K(0.6f), 14);
		_fCross = Spr(_fVs, "quest_category_pvp", 0f, 0f, 64f, 57f, Yellow, 15);
		_fNote = Lbl(gameObject, 0f, -140f, 900, 30, 18, W(0.6f), 15);
	}

	private void RefreshFound()
	{
		JObject found = CommunityRanked.Found;
		double num = CommunityRanked.Num(found?["departAt"]);
		bool flag = found != null && !CommunityRanked.InMatch && (CommunityRanked.Now < num + 3.0 || Time.realtimeSinceStartup - CommunityRanked.FoundAt < 8f);
		_found.SetActive(flag);
		if (!flag)
		{
			return;
		}
		bool flag2 = CommunityRanked.Str(found["mode"]) == "3v3";
		_fTitle.text = CommunityRanked.T("พบค\u0e39\u0e48แข\u0e48ง!", "Opponent found!");
		int num2 = Mathf.Max(0, Mathf.CeilToInt((float)(num - CommunityRanked.Now)));
		_fSub.text = (flag2 ? CommunityRanked.T("ท\u0e35ม 3v3", "Team 3v3") : CommunityRanked.T("ดวล 1v1", "Duel 1v1")) + CommunityRanked.T(" · ชนะ 2 ใน 3 ยก · วาร\u0e4cปใน ", " · best of 3 · warping in ") + "[FFD85B]" + num2 + "[-]" + CommunityRanked.T(" ว\u0e34", " s");
		int num3 = Mathf.Clamp(CommunityRanked.Int(found["myTeam"]), 0, 1);
		JArray jArray = found["teams"] as JArray;
		int num4 = (flag2 ? 300 : 150);
		_ = (float)num4 / 2f;
		for (int i = 0; i < 2; i++)
		{
			Card card = _fCards[i];
			card.Go.transform.localPosition = new Vector3((i == 0) ? (-270) : 270, 70f, 0f);
			card.Bg.height = num4;
			card.Grunge.height = num4 + 2;
			card.Header.text = ((i == 0) ? CommunityRanked.T("ท\u0e35มเรา", "Your side") : CommunityRanked.T("ฝ\u0e48ายตรงข\u0e49าม", "Opponents"));
			int num5 = ((i == 0) ? num3 : (1 - num3));
			JArray jArray2 = ((jArray != null && jArray.Count > num5) ? (jArray[num5] as JArray) : null);
			int num6 = ((jArray2 != null) ? Mathf.Min(3, jArray2.Count) : 0);
			for (int j = 0; j < 3; j++)
			{
				JToken jToken = ((j < num6) ? jArray2[j] : null);
				card.Emblems[j].Go.SetActive(jToken != null);
				card.Names[j].gameObject.SetActive(jToken != null);
				card.Tiers[j].gameObject.SetActive(jToken != null);
				if (j < 2)
				{
					card.Lines[j].gameObject.SetActive(j < num6 - 1);
				}
				if (jToken != null)
				{
					string text = CommunityRanked.Str(jToken["tier"]);
					SetEmblem(card.Emblems[j], text);
					card.Names[j].text = Plain(CommunityRanked.Str(jToken["name"]));
					double? num7 = CommunityRanked.NumOrNull(jToken["rating"]);
					string text2 = ((text == "master" && num7.HasValue) ? ("[FFFFFF99]" + Num((int)num7.Value) + " " + CommunityRanked.T("แต\u0e49ม", "pts") + "[-]") : string.Empty);
					card.Tiers[j].text = ((i == 0) ? (TierText(text) + "  " + text2) : (text2 + "  " + TierText(text)));
				}
			}
		}
		float y = (flag2 ? (-80f) : (-5f));
		_fVs.transform.localPosition = new Vector3(0f, y, 0f);
		_fNote.transform.localPosition = new Vector3(0f, flag2 ? (-290f) : (-140f), 0f);
		bool flag3 = CommunityRanked.Flag(found["rated"]) || found["rated"] == null;
		if (CommunityRanked.OwnGear)
		{
			_fNote.text = (flag3 ? CommunityRanked.T("ใช\u0e49ต\u0e31วละครจร\u0e34ง · แจกแค\u0e48ยา · อาว\u0e38ธ/เกราะไม\u0e48เส\u0e35ยความทน", "Your own character · only potions are given · gear takes no durability loss") : CommunityRanked.T("แมตช\u0e4cฝ\u0e36ก — ไม\u0e48น\u0e31บแต\u0e49ม · ใช\u0e49ต\u0e31วละครจร\u0e34ง", "Practice match — not rated · your own character"));
		}
		else
		{
			_fNote.text = (flag3 ? CommunityRanked.T("ท\u0e38กคนเป\u0e47น Lv60 สก\u0e34ลเต\u0e47ม · ของในกระเป\u0e4bาจร\u0e34งไม\u0e48ถ\u0e39กแตะ (ใช\u0e49ช\u0e38ดมาตรฐาน)", "Everyone becomes Lv60 with all skills · your real bag is untouched (standard kit)") : CommunityRanked.T("แมตช\u0e4cฝ\u0e36ก — ไม\u0e48น\u0e31บแต\u0e49ม · ของในกระเป\u0e4bาจร\u0e34งไม\u0e48ถ\u0e39กแตะ", "Practice match — not rated · your real bag is untouched"));
		}
	}

	private void BuildHud()
	{
		_hud = Layer("Hud", _hudDepth + 1);
		_hBox = Anchored(_hud, UIBase.AnchorType.Base, 0.5f, 1f, 0, -84, 400, 74);
		GameObject hBox = _hBox;
		Spr(hBox, "bg_roundbox2", 0f, 0f, 400f, 74f, K(0.62f), 1);
		Spr(hBox, "bg_box_grunge", 0f, 0f, 402f, 76f, K(1f), 2);
		_hMe = Lbl(hBox, -124f, 15f, 128, 28, 19, Yellow, 4);
		_hFoe = Lbl(hBox, 124f, 15f, 128, 28, 19, FoeText, 4);
		for (int i = 0; i < 4; i++)
		{
			float x = ((i < 2) ? (-124f) : 124f) + ((i % 2 == 0) ? (-11f) : 11f);
			_pips[i] = Spr(hBox, "circle_32", x, -19f, 14f, 14f, Yellow, 4);
		}
		_hTimer = Spr(hBox, "img_battle_timer", -48f, 13f, 24f, 24f, Color.white, 4);
		_hClock = Lbl(hBox, -30f, 13f, 90, 40, 32, Color.white, 4, NGUIText.Alignment.Left);
		_hWeather = Spr(hBox, "icon_se_pvp_rain", -48f, -19f, 22f, 17f, Color.white, 4, UIWidget.Pivot.Center, simple: true);
		_hRound = Lbl(hBox, -30f, -20f, 140, 26, 16, W(0.8f), 4, NGUIText.Alignment.Left);
		_hStatusGo = hBox.AddWidget<UIWidget>(1).gameObject;
		_hStatusGo.name = "Status";
		_hStatusBg = Spr(_hStatusGo, "bg_round_box", 0f, -58f, 300f, 30f, new Color(0.016f, 0.016f, 0.016f, 0.62f), 1);
		_hStatus = Lbl(_hStatusGo, 0f, -58f, 290, 30, 17, Color.white, 4);
		for (int j = 0; j < 6; j++)
		{
			bool flag = j < 3;
			float y = -87 - j % 3 * 20;
			_hHp[j] = new Bar();
			_hHp[j].Go = hBox.AddChild();
			_hHp[j].Go.transform.localPosition = new Vector3(flag ? (-258) : 258, y, 0f);
			Spr(_hHp[j].Go, "bg_white", 0f, 0f, 104f, 12f, K(0.7f), 1, UIWidget.Pivot.Center, simple: true);
			_hHp[j].Fill = Spr(_hHp[j].Go, "bg_white", -50f, 0f, 100f, 8f, new Color(0.769f, 0.302f, 0.302f), 2, UIWidget.Pivot.Left, simple: true);
			_hHpName[j] = Lbl(_hHp[j].Go, flag ? (-58) : 58, 0f, 144, 22, 14, Color.white, 3, (!flag) ? NGUIText.Alignment.Left : NGUIText.Alignment.Right, shadow: true);
		}
	}

	private void RefreshHud()
	{
		JObject match = CommunityRanked.Match;
		bool flag = CommunityRanked.InMatch && match != null && CommunityRanked.Result == null && !_break.activeSelf;
		_hud.SetActive(flag);
		if (!flag)
		{
			return;
		}
		HideSurvivorCount();
		int myTeam = CommunityRanked.MyTeam;
		JArray jArray = match["score"] as JArray;
		int num = ((jArray != null && myTeam >= 0 && jArray.Count == 2) ? CommunityRanked.Int(jArray[myTeam]) : 0);
		int num2 = ((jArray != null && myTeam >= 0 && jArray.Count == 2) ? CommunityRanked.Int(jArray[1 - myTeam]) : 0);
		for (int i = 0; i < 4; i++)
		{
			bool flag2 = i < 2;
			bool flag3 = i % 2 < (flag2 ? num : num2);
			_pips[i].spriteName = (flag3 ? "circle_32" : "img_slide_circle_outside");
			_pips[i].width = (flag3 ? 14 : 15);
			_pips[i].height = (flag3 ? 14 : 15);
			CommunityRankedAnim.Tint(_pips[i], (!flag3) ? W(0.45f) : (flag2 ? Yellow : Red));
			if (flag3 && !_pipOn[i] && _wasHud)
			{
				Play(_aPips[i]);
			}
			_pipOn[i] = flag3;
		}
		bool flag4 = CommunityRanked.Str(match["mode"]) == "3v3";
		JArray jArray2 = match["teams"] as JArray;
		_hMe.text = (flag4 ? CommunityRanked.T("ท\u0e35มเรา", "Your team") : TeamName(jArray2, myTeam));
		_hFoe.text = (flag4 ? CommunityRanked.T("ท\u0e35มศ\u0e31ตร\u0e39", "Enemy") : TeamName(jArray2, 1 - myTeam));
		string weather = CommunityRanked.Str(match["weather"]);
		_hWeather.spriteName = CommunityRanked.WeatherIcon(weather);
		_hRound.text = CommunityRanked.T("ยก ", "Rd ") + CommunityRanked.Int(match["round"], 1) + " · " + CommunityRanked.WeatherName(weather);
		for (int j = 0; j < 6; j++)
		{
			int num3 = ((j < 3) ? myTeam : (1 - myTeam));
			JToken jToken = ((flag4 && jArray2 != null && myTeam >= 0 && jArray2.Count > num3 && jArray2[num3] is JArray jArray3 && jArray3.Count > j % 3) ? jArray3[j % 3] : null);
			_hHp[j].Go.SetActive(jToken != null);
			if (jToken != null)
			{
				double num4 = Math.Max(1.0, CommunityRanked.Num(jToken["max"], 1.0));
				float num5 = Mathf.Clamp01((float)((CommunityRanked.Flag(jToken["out"]) ? 0.0 : CommunityRanked.Num(jToken["hp"])) / num4));
				_hHp[j].Fill.width = Mathf.Max(2, Mathf.RoundToInt(100f * num5));
				_hHp[j].Fill.enabled = num5 > 0.001f;
				_hHpName[j].text = Plain(CommunityRanked.Str(jToken["name"]));
				_hHpName[j].color = ((num5 > 0.001f) ? Color.white : W(0.35f));
			}
		}
		TickHud();
	}

	private static string TeamName(JArray teams, int team)
	{
		JArray jArray = ((teams != null && team >= 0 && teams.Count > team) ? (teams[team] as JArray) : null);
		if (jArray == null || jArray.Count <= 0)
		{
			return string.Empty;
		}
		return Plain(CommunityRanked.Str(jArray[0]["name"]));
	}

	private void TickHud()
	{
		if (_hud.activeSelf && CommunityRanked.Match != null)
		{
			string phase = CommunityRanked.Phase;
			bool flag = phase == "Overtime";
			_hClock.text = CommunityRanked.Clock(CommunityRanked.PhaseLeft);
			_hClock.color = ((flag || (phase == "Live" && CommunityRanked.PhaseLeft < 15.0)) ? new Color(1f, 0.353f, 0.235f) : Color.white);
			_hTimer.color = (flag ? Red : Color.white);
			string text = null;
			Color color = Color.white;
			if (CommunityRanked.Flag(CommunityRanked.Match["out"]) && (phase == "Live" || flag))
			{
				text = CommunityRanked.T("หล\u0e38ดยกน\u0e35\u0e49 — รอยกถ\u0e31ดไป", "Out this round — wait for the next");
				color = W(0.75f);
			}
			else if (flag)
			{
				text = CommunityRanked.T("ต\u0e48อเวลา · ใครโดนต\u0e35ก\u0e48อนแพ\u0e49", "Overtime · first hit taken loses");
				color = FoeText;
			}
			else if (phase == "Live")
			{
				bool num = CommunityRanked.Str(CommunityRanked.Match["weather"]).StartsWith("pvp_storm");
				text = (num ? CommunityRanked.T("พาย\u0e38วงหด · อย\u0e39\u0e48ในวง", "Storm closing · stay inside") : CommunityRanked.T("ส\u0e39\u0e49!", "Fight!"));
				color = (num ? new Color(0.624f, 0.816f, 1f) : Color.white);
			}
			else if (phase == "Arriving")
			{
				text = CommunityRanked.T("รอผ\u0e39\u0e49เล\u0e48นเข\u0e49าสนาม", "Waiting for players");
				color = W(0.75f);
			}
			_hStatusBg.gameObject.SetActive(text != null);
			_hStatus.gameObject.SetActive(text != null);
			_hStatus.text = text ?? string.Empty;
			_hStatus.color = color;
			if (text != null && text != _lastStatus && _wasHud)
			{
				Play(_aStatus);
			}
			_lastStatus = text;
		}
	}

	private void BuildCountdown()
	{
		_count = Layer("Countdown", _hudDepth + 2);
		GameObject gameObject = (_cBand = Anchored(_count, UIBase.AnchorType.Base, 0.5f, 0.5f, 0, 0, 320, 130));
		_cBg = gameObject.AddSprite(PcUi ? "bg_popup_pc" : "bg_todo", 1);
		_cBg.name = "Bg";
		_cBg.color = (PcUi ? new Color(0.7058824f, 0.6901961f, 0.6901961f, 2f / 3f) : new Color(1f, 1f, 1f, 40f / 51f));
		_cBg.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
		_cBg.width = 640;
		_cBg.height = 260;
		_cNumber = Lbl(gameObject, 0f, 18f, 600, 80, 72, Yellow, 3);
		_cSub = Lbl(gameObject, 0f, -36f, 700, 30, 22, Color.white, 3);
	}

	private void RefreshCountdown()
	{
		bool flag = _hud.activeSelf && CommunityRanked.Phase == "Countdown";
		_count.SetActive(flag);
		if (flag)
		{
			JObject match = CommunityRanked.Match;
			_cNumber.text = Mathf.Max(1, Mathf.CeilToInt((float)CommunityRanked.PhaseLeft)).ToString();
			if (_cNumber.text != _lastCount)
			{
				_lastCount = _cNumber.text;
				Play(_aCountNum);
			}
			_cSub.text = CommunityRanked.T("ยก ", "Round ") + CommunityRanked.Int(match["round"], 1) + " · [FFFFFFCC]" + CommunityRanked.WeatherName(CommunityRanked.Str(match["weather"])) + "[-]";
			_cBg.width = 2 * Mathf.Max(260, Mathf.RoundToInt(_cSub.printedSize.x) + 96);
		}
	}

	private void BuildRingWarn()
	{
		_ringWarn = Layer("RingWarn", _hudDepth + 2);
		GameObject gameObject = Anchored(_ringWarn, UIBase.AnchorType.Base, 0.5f, 0.5f, 0, 150, 440, 68);
		Spr(gameObject, "bg_roundbox2", 0f, 0f, 440f, 68f, new Color(0.263f, 0f, 0.047f, 0.86f), 1);
		Spr(gameObject, "bg_box_grunge", 0f, 0f, 442f, 70f, K(1f), 2);
		Spr(gameObject, "icon_caution", -186f, 1f, 33f, 32f, Yellow, 3, UIWidget.Pivot.Center, simple: true);
		_rwTitle = Lbl(gameObject, 16f, 12f, 360, 32, 24, Yellow, 4, NGUIText.Alignment.Center, shadow: true);
		_rwSub = Lbl(gameObject, 16f, -17f, 360, 22, 16, W(0.9f), 4);
		TweenAlpha tweenAlpha = TweenAlpha.Begin(gameObject, 0.55f, 0.62f);
		tweenAlpha.from = 1f;
		tweenAlpha.style = UITweener.Style.PingPong;
	}

	private void RefreshRingWarn()
	{
		bool flag = _hud.activeSelf && CommunityRankedRing.Outside;
		if (_ringWarn.activeSelf != flag)
		{
			_ringWarn.SetActive(flag);
		}
		if (flag)
		{
			_rwTitle.text = CommunityRanked.T("ออกนอกสนาม! กล\u0e31บเข\u0e49าวง", "Out of the ring! Get back inside");
			double damageRatio = CommunityRankedRing.DamageRatio;
			_rwSub.text = ((damageRatio > 0.0) ? CommunityRanked.T("เส\u0e35ยเล\u0e37อด " + Math.Round(damageRatio * 100.0, 1) + "% ต\u0e48อว\u0e34นาท\u0e35 จนกว\u0e48าจะกล\u0e31บเข\u0e49าวง", "Losing " + Math.Round(damageRatio * 100.0, 1) + "% HP per second until you are back") : CommunityRanked.T("อย\u0e39\u0e48นอกวงจะเส\u0e35ยเล\u0e37อดท\u0e38กว\u0e34นาท\u0e35", "You lose HP every second outside the ring"));
		}
	}

	private void HideSurvivorCount()
	{
		try
		{
			PvpIslandSurvivorCountWidget[] array = UnityEngine.Object.FindObjectsByType<PvpIslandSurvivorCountWidget>();
			foreach (PvpIslandSurvivorCountWidget pvpIslandSurvivorCountWidget in array)
			{
				if (pvpIslandSurvivorCountWidget.gameObject.activeSelf)
				{
					pvpIslandSurvivorCountWidget.gameObject.SetActive(value: false);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private void BuildPotion()
	{
		_potion = Layer("Potion", _hudDepth + 1);
		bool pcUi = PcUi;
		int num = (pcUi ? 520 : 234);
		int oy = (pcUi ? 97 : 84);
		GameObject gameObject = Anchored(_potion, UIBase.AnchorType.Base, 1f, 0f, -num, oy, 64, 64);
		UISprite w = Spr(gameObject, "bg_circle_100x100", 0f, 0f, 64f, 64f, K(0.5f), 1);
		Spr(gameObject, "grunge_circle_outline", 0f, 0f, 64f, 64f, W(0.6f), 2);
		_potionIcon = gameObject.AddWidget<ItemIconTex>(3);
		_potionIcon.width = 40;
		_potionIcon.height = 40;
		_potionIcon.transform.localPosition = new Vector3(0f, 2f, 0f);
		Spr(gameObject, "bg_round_box", 28f, -25f, 40f, 22f, K(0.8f), 4);
		_potionText = Lbl(gameObject, 28f, -25f, 40, 22, 15, Color.white, 5);
		Click(w, UsePotion);
	}

	private void UsePotion()
	{
		InventorySystem inventorySystem = GameSystem<InventorySystem>.Instance();
		foreach (ItemData playerItem in inventorySystem.PlayerItemList)
		{
			if (playerItem != null && playerItem.PrototypeId == PotionProto)
			{
				inventorySystem.UseItem(playerItem, playerAccepted: true);
				return;
			}
		}
		UIManager.SystemMsg(CommunityRanked.T("ยาฟ\u0e37\u0e49นเล\u0e37อดหมดแล\u0e49ว", "No potions left"), 2f);
	}

	private static void SetItemIcon(ItemIconTex icon, string proto)
	{
		try
		{
			int[] array = new int[4] { 1, 50, 60, 0 };
			foreach (int level in array)
			{
				if (PrototypeYaml.GetItemPrototype(proto, level) != null)
				{
					icon.SetIcon(proto, level);
					return;
				}
			}
			icon.SetIcon(proto, 1);
		}
		catch (Exception)
		{
		}
	}

	private void RefreshPotion()
	{
		bool flag = _hud.activeSelf && CommunityRanked.Phase != "Arriving";
		_potion.SetActive(flag);
		if (flag)
		{
			if (_potionIcon.Icon == null)
			{
				SetItemIcon(_potionIcon, PotionProto);
			}
			int num = CommunityRanked.Int(CommunityRanked.Match?["potions"]);
			int num2 = CommunityRanked.Int(CommunityRanked.Lobby?["potionsPerRound"], 3);
			_potionText.text = num + "/" + num2;
			_potionIcon.alpha = ((num > 0) ? 1f : 0.4f);
		}
	}

	private void BuildBreak()
	{
		_break = Layer("Break", _modalDepth);
		UISprite uISprite = _break.AddSprite("bg_white", 0);
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.color = K(0.647f);
		Stretch(uISprite);
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		GameObject parent = Anchored(_break, UIBase.AnchorType.Base, 0.5f, 0.5f, 0, 0, 1000, 580);
		Spr(parent, "bg_roundbox2", 0f, 0f, 1000f, 580f, new Color(0.154f, 0.14f, 0.107f, 0.941f), 1);
		Spr(parent, "estate_line_box_line_bg", 0f, 0f, 1000f, 580f, W(0.039f), 2);
		Spr(parent, "bg_roundbox2", 0f, 264f, 1000f, 52f, new Color(0.627f, 0.529f, 0.278f), 3);
		Spr(parent, "bg_tooltip_grunge2", -378f, 264f, 244f, 52f, Color.white, 4, UIWidget.Pivot.Center, simple: true);
		Spr(parent, "bg_tooltip_grunge", 378f, 264f, 244f, 52f, Color.white, 4, UIWidget.Pivot.Center, simple: true);
		Spr(parent, "bg_dotline_white2", 0f, 239f, 1000f, 2f, K(1f), 4).type = UIBasicSprite.Type.Tiled;
		_bTitle = Lbl(parent, -476f, 264f, 700, 52, 26, Color.black, 6, NGUIText.Alignment.Left);
		Spr(parent, "img_battle_timer", 390f, 264f, 24f, 24f, K(1f), 6);
		_bClock = Lbl(parent, 406f, 264f, 80, 52, 28, Color.black, 6, NGUIText.Alignment.Left);
		_bScore = Lbl(parent, 0f, 206f, 952, 40, 22, Color.white, 6);
		_bPickNote = Lbl(parent, 0f, 167f, 952, 30, 20, W(0.7f), 6);
		_bNoWeapon = Lbl(parent, 0f, 96f, 900, 40, 22, W(0.55f), 6);
		for (int i = 0; i < 9; i++)
		{
			Tile tile = new Tile();
			tile.Go = parent.AddChild();
			tile.Go.name = "Weapon" + i;
			tile.Go.transform.localPosition = new Vector3(-416 + i * 104, 96f, 0f);
			tile.Bg = Spr(tile.Go, "bg_white", 0f, 0f, 96f, 96f, new Color(1f, 0.847f, 0.357f, 0.078f), 6, UIWidget.Pivot.Center, simple: true);
			tile.Icon = tile.Go.AddWidget<ItemIconTex>(7);
			tile.Icon.width = 72;
			tile.Icon.height = 72;
			tile.Select = Spr(tile.Go, "target_crosshair", 0f, 0f, 104f, 104f, new Color(0.996f, 0.839f, 0.357f), 8);
			tile.Name = Lbl(tile.Go, 0f, -72f, 104, 40, 15, W(0.8f), 8);
			Tile captured = tile;
			Click(tile.Bg, delegate
			{
				if (!string.IsNullOrEmpty(captured.ItemId))
				{
					CommunityRanked.PickWeaponItem(captured.ItemId, captured.Proto);
					_pickId = captured.ItemId;
					_pickUntil = Time.realtimeSinceStartup + 4f;
					Step(RefreshBreak);
				}
				else if (!string.IsNullOrEmpty(captured.Proto))
				{
					CommunityRanked.PickWeapon(captured.Proto);
				}
			});
			_tiles[i] = tile;
		}
		Spr(parent, "bg_dotline_white2", 0f, -10f, 952f, 2f, W(0.118f), 5).type = UIBasicSprite.Type.Tiled;
		_bSideNote = Lbl(parent, 0f, -39f, 952, 34, 22, Yellow, 6);
		_side0 = CloneButton(_solidTpl, parent, -160f, -97f, 300, 62, 10, PresetButton.Style.Solid, CommunityRanked.SideName(0), delegate
		{
			CommunityRanked.PickSide(0);
		});
		_side1 = CloneButton(_solidTpl, parent, 160f, -97f, 300, 62, 10, PresetButton.Style.Border, CommunityRanked.SideName(1), delegate
		{
			CommunityRanked.PickSide(1);
		});
		_arrow0 = Spr(parent, "img_arrow_weapon_left", -280f, -97f, 14f, 22f, K(1f), 40, UIWidget.Pivot.Center, simple: true);
		_arrow1 = Spr(parent, "img_arrow_weapon_right", 280f, -97f, 14f, 22f, new Color(0.831f, 0.808f, 0.745f, 0.745f), 40, UIWidget.Pivot.Center, simple: true);
		_bRecover = Lbl(parent, 0f, -157f, 952, 30, 19, W(0.65f), 6);
		Spr(parent, "bg_roundbox2", 0f, -245f, 1000f, 90f, new Color(0.074f, 0.061f, 0.044f), 3);
		Spr(parent, "bg_dotline_white2", 0f, -199f, 1000f, 2f, K(0.522f), 4).type = UIBasicSprite.Type.Tiled;
		Spr(parent, "estate_line_box_line_bg", 0f, -245f, 1000f, 90f, W(0.039f), 4);
		CloneButton(_solidTpl, parent, 0f, -245f, 260, 62, 10, PresetButton.Style.Solid, CommunityRanked.T("พร\u0e49อม", "Ready"), delegate
		{
			_breakHidden = true;
			Refresh();
		});
	}

	private void RefreshBreak()
	{
		JObject match = CommunityRanked.Match;
		string phase = CommunityRanked.Phase;
		if (phase != "Break")
		{
			_breakHidden = false;
		}
		bool flag = CommunityRanked.InMatch && phase == "Break" && !_breakHidden && CommunityRanked.Result == null && !Holding;
		_break.SetActive(flag);
		if (!flag)
		{
			return;
		}
		int num = CommunityRanked.Int(match["round"], 1) + 1;
		string weather = ((num <= 1) ? "pvp_fog" : ((num == 2) ? "pvp_rain" : "pvp_storm_01"));
		_bTitle.text = CommunityRanked.T("พ\u0e31กยก · ต\u0e48อไป ยก " + num + " · ", "Break · next: Round " + num + " · ") + CommunityRanked.WeatherName(weather);
		int myTeam = CommunityRanked.MyTeam;
		JArray jArray = match["score"] as JArray;
		int num2 = ((jArray != null && myTeam >= 0 && jArray.Count == 2) ? CommunityRanked.Int(jArray[myTeam]) : 0);
		int num3 = ((jArray != null && myTeam >= 0 && jArray.Count == 2) ? CommunityRanked.Int(jArray[1 - myTeam]) : 0);
		bool num4 = CommunityRanked.Str(match["mode"]) == "3v3";
		JArray teams = match["teams"] as JArray;
		string text = (num4 ? CommunityRanked.T("ท\u0e35มเรา", "Your team") : TeamName(teams, myTeam));
		string text2 = (num4 ? CommunityRanked.T("ท\u0e35มศ\u0e31ตร\u0e39", "Enemy") : TeamName(teams, 1 - myTeam));
		_bScore.text = "[FFD85B]" + text + "[-]   [size=30][b]" + num2 + " : " + num3 + "[/b][/size]   [FF7A5C]" + text2 + "[-]";
		bool flag2 = CommunityRanked.Flag(match["ownGear"]);
		_bPickNote.text = (flag2 ? CommunityRanked.T("อาว\u0e38ธในกระเป\u0e4bาของค\u0e38ณ — กดเพ\u0e37\u0e48อสวม", "Your weapons — tap to equip") : CommunityRanked.T("เล\u0e37อกอาว\u0e38ธ — ค\u0e48าพล\u0e31งเท\u0e48าก\u0e31นท\u0e38กแบบ", "Pick a weapon — all have equal stats"));
		_bNoWeapon.gameObject.SetActive(value: false);
		if (flag2)
		{
			RefreshOwnWeapons(match);
		}
		JArray jArray2 = (flag2 ? null : (match["weapons"] as JArray));
		string text3 = CommunityRanked.Str(match["weapon"]);
		for (int i = 0; i < 9; i++)
		{
			if (flag2)
			{
				break;
			}
			Tile tile = _tiles[i];
			string text4 = ((jArray2 != null && i < jArray2.Count) ? CommunityRanked.Str(jArray2[i]) : null);
			tile.Go.SetActive(!string.IsNullOrEmpty(text4));
			if (!string.IsNullOrEmpty(text4))
			{
				if (tile.Proto != text4 || tile.ItemId != null)
				{
					tile.Proto = text4;
					tile.ItemId = null;
					SetItemIcon(tile.Icon, text4);
				}
				bool flag3 = text3 == text4 || (string.IsNullOrEmpty(text3) && i == 0);
				tile.Name.text = CommunityRanked.WeaponName(text4);
				tile.Name.color = (flag3 ? Yellow : W(0.8f));
				SelectTile(tile, flag3);
				tile.Bg.color = new Color(1f, 0.847f, 0.357f, flag3 ? 0.22f : 0.078f);
			}
		}
		bool flag4 = CommunityRanked.Int(match["loser"], -1) == myTeam && myTeam >= 0;
		int num5 = ((match["sideOf"] is JArray jArray3 && myTeam >= 0 && jArray3.Count == 2) ? CommunityRanked.Int(jArray3[myTeam]) : 0);
		_bSideNote.color = (flag4 ? Yellow : W(0.75f));
		_bSideNote.text = (flag4 ? CommunityRanked.T("ค\u0e38ณแพ\u0e49ยกน\u0e35\u0e49 — เล\u0e37อกฝ\u0e31\u0e48งเก\u0e34ดของยกถ\u0e31ดไป", "You lost this round — choose your spawn side") : (CommunityRanked.T("ค\u0e38ณชนะยกน\u0e35\u0e49 · อ\u0e35กฝ\u0e48ายกำล\u0e31งเล\u0e37อกฝ\u0e31\u0e48งเก\u0e34ด… (ตอนน\u0e35\u0e49: ", "You won this round · the opponent is choosing a spawn side… (now: ") + CommunityRanked.SideName(num5) + ")"));
		_bSideNote.transform.localPosition = new Vector3(0f, flag4 ? (-39f) : (-72f), 0f);
		if (_side0 != null && _side1 != null)
		{
			_side0.gameObject.SetActive(flag4);
			_side1.gameObject.SetActive(flag4);
			_side0.SetStyle((num5 != 0) ? PresetButton.Style.Border : PresetButton.Style.Solid);
			_side1.SetStyle((num5 != 1) ? PresetButton.Style.Border : PresetButton.Style.Solid);
			_side0.Text = CommunityRanked.SideName(0);
			_side1.Text = CommunityRanked.SideName(1);
		}
		_arrow0.gameObject.SetActive(flag4);
		_arrow1.gameObject.SetActive(flag4);
		_arrow0.color = ((num5 == 0) ? K(1f) : new Color(0.831f, 0.808f, 0.745f, 0.745f));
		_arrow1.color = ((num5 == 1) ? K(1f) : new Color(0.831f, 0.808f, 0.745f, 0.745f));
		int num6 = CommunityRanked.Int(CommunityRanked.Lobby?["potionsPerRound"], 3);
		_bRecover.text = CommunityRanked.T("เล\u0e37อด/สตาม\u0e34นาเต\u0e47ม", "HP/stamina full") + "     ·     " + CommunityRanked.T("ค\u0e39ลดาวน\u0e4cร\u0e35เซ\u0e47ต", "cooldowns reset") + "     ·     " + CommunityRanked.T("ยาฟ\u0e37\u0e49นเล\u0e37อด ", "potions ") + num6 + "/" + num6;
	}

	private void RefreshOwnWeapons(JObject match)
	{
		List<JObject> list = new List<JObject>();
		if (match["weaponItems"] is JArray jArray)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item is JObject jObject && !string.IsNullOrEmpty(CommunityRanked.Str(jObject["id"])))
				{
					list.Add(jObject);
				}
			}
		}
		string held = CommunityRanked.Str(match["weapon"]);
		InventorySystem inventory = (GameSystem<InventorySystem>.HasInstance() ? GameSystem<InventorySystem>.Instance() : null);
		Func<JObject, bool> equipped = delegate(JObject o)
		{
			try
			{
				return ((inventory != null) ? inventory.FindItem(CommunityRanked.Str(o["id"])) : null)?.IsEquipments ?? false;
			}
			catch (Exception)
			{
				return false;
			}
		};
		list.Sort(delegate(JObject a, JObject b)
		{
			int num4 = CommunityRanked.Int(b["level"]).CompareTo(CommunityRanked.Int(a["level"]));
			if (num4 == 0)
			{
				num4 = string.CompareOrdinal(CommunityRanked.Str(a["proto"]), CommunityRanked.Str(b["proto"]));
			}
			return (num4 == 0) ? string.CompareOrdinal(CommunityRanked.Str(a["id"]), CommunityRanked.Str(b["id"])) : num4;
		});
		JObject jObject2 = ((!string.IsNullOrEmpty(_pickId) && Time.realtimeSinceStartup < _pickUntil) ? list.Find((JObject o) => CommunityRanked.Str(o["id"]) == _pickId) : null);
		if (jObject2 != null && equipped(jObject2))
		{
			_pickId = null;
		}
		JObject jObject3 = jObject2 ?? list.Find((JObject o) => equipped(o) && CommunityRanked.Str(o["slot"]) != "sub") ?? list.Find((JObject o) => equipped(o)) ?? list.Find((JObject o) => CommunityRanked.Str(o["proto"]) == held);
		int num = ((jObject3 != null) ? list.IndexOf(jObject3) : (-1));
		if (num >= 9)
		{
			list.RemoveAt(num);
			list.Insert(8, jObject3);
		}
		for (int num2 = 0; num2 < 9; num2++)
		{
			Tile tile = _tiles[num2];
			JObject jObject4 = ((num2 < list.Count) ? list[num2] : null);
			tile.Go.SetActive(jObject4 != null);
			if (jObject4 != null)
			{
				string text = CommunityRanked.Str(jObject4["id"]);
				string text2 = CommunityRanked.Str(jObject4["proto"]);
				int num3 = Math.Max(1, CommunityRanked.Int(jObject4["level"], 1));
				if (tile.ItemId != text || tile.Proto != text2 || tile.Level != num3)
				{
					tile.ItemId = text;
					tile.Proto = text2;
					tile.Level = num3;
					SetItemIcon(tile.Icon, text2, num3);
				}
				bool flag = jObject4 == jObject3;
				tile.Name.text = ItemName(text2, num3) + "\n" + LocalizeUtil.FormatLevel(num3);
				tile.Name.color = (flag ? Yellow : W(0.8f));
				SelectTile(tile, flag);
				tile.Bg.color = new Color(1f, 0.847f, 0.357f, flag ? 0.22f : 0.078f);
			}
		}
		_bNoWeapon.gameObject.SetActive(list.Count == 0);
		_bNoWeapon.text = CommunityRanked.T("ไม\u0e48ม\u0e35อาว\u0e38ธในกระเป\u0e4bา", "No weapons in your bag");
	}

	private static void SetItemIcon(ItemIconTex icon, string proto, int level)
	{
		try
		{
			if (PrototypeYaml.GetItemPrototype(proto, level) != null)
			{
				icon.SetIcon(proto, level);
				return;
			}
		}
		catch (Exception)
		{
		}
		SetItemIcon(icon, proto);
	}

	private static string ItemName(string proto, int level)
	{
		try
		{
			Prototype prototype = PrototypeYaml.GetItemPrototype(proto, level) ?? PrototypeYaml.GetItemPrototype(proto);
			if (prototype != null && (string)prototype.Name != null)
			{
				string text = prototype.Name.ToString();
				if (!string.IsNullOrEmpty(text))
				{
					return text;
				}
			}
		}
		catch (Exception)
		{
		}
		return CommunityRanked.WeaponName(proto);
	}

	private void BuildResult(PvpIslandResultGroup src)
	{
		if (src == null)
		{
			return;
		}
		_resultWin = CloneWindow(src, "CommunityRankedResult");
		FieldInfo field = typeof(UIBase).GetField("_gameBlur", BindingFlags.Instance | BindingFlags.NonPublic);
		if (field != null)
		{
			field.SetValue(_resultWin, false);
		}
		Transform transform = _resultWin.transform;
		_rTitle = Find<UILabel>(transform, "Container/Title/Title");
		_rLine = Find<UISprite>(transform, "Container/separator/line");
		Transform transform2 = transform.Find("Container/Center/RankingBox/highlight");
		_rHighlight = ((transform2 != null) ? transform2.gameObject : null);
		_rRankLabel = Find<UILabel>(transform, "Container/Center/RankingBox/Ranking");
		_rExit = Find<SelectableButton>(transform, "Container/Center/Button");
		if (_rTitle != null)
		{
			_rTitle.alignment = NGUIText.Alignment.Center;
		}
		if (_rRankLabel != null)
		{
			_rRankLabel.alignment = NGUIText.Alignment.Center;
		}
		UIWidget uIWidget = ((_rHighlight != null) ? _rHighlight.GetComponent<UIWidget>() : null);
		if (uIWidget != null)
		{
			uIWidget.leftAnchor.target = null;
			uIWidget.rightAnchor.target = null;
			uIWidget.bottomAnchor.target = null;
			uIWidget.topAnchor.target = null;
			uIWidget.ResetAnchors();
			uIWidget.pivot = UIWidget.Pivot.Center;
			uIWidget.width = 250;
			uIWidget.height = 250;
			uIWidget.transform.localPosition = new Vector3(-10f, 161f, 0f);
		}
		if (_rExit != null)
		{
			_rExit.Clicked = delegate
			{
				Safe(delegate
				{
					CommunityRanked.Leave();
					CommunityRanked.CloseResult();
				});
			};
		}
		_resultWin.OnCloseSucceed += delegate
		{
			if (_resultShown)
			{
				_resultShown = false;
				_resultWin.VisibleController.HideExceptForMe(hide: false, "RankedResult");
			}
		};
		Transform transform3 = transform.Find("Container");
		if (!(transform3 == null))
		{
			_rAdd = transform3.gameObject.AddChild();
			_rAdd.name = "CommunityRanked";
			GameObject rAdd = _rAdd;
			int num = 30;
			_rEmblem = MakeEmblem(rAdd, -330f, 60f, 112f, num);
			_rDetails = rAdd.AddWidget<UIWidget>(num).gameObject;
			_rDetails.name = "Details";
			rAdd = _rDetails;
			_rTier = Lbl(rAdd, -330f, -24f, 300, 44, 34, Color.white, num + 5);
			_rPoints = Lbl(rAdd, -330f, -64f, 300, 32, 26, Color.white, num + 5);
			_rBar = MakeBar(rAdd, -450f, -56f, 240, num + 5, 17);
			_rBannerBg = Spr(rAdd, "bg_round_box", -330f, -112f, 300f, 40f, new Color(0.3f, 0.24f, 0.06f, 0.9f), num + 4);
			_rBanner = Lbl(rAdd, -330f, -112f, 290, 48, 18, Color.white, num + 5);
			_rCoinIcon = Spr(rAdd, PcUi ? "durango_coin_pc" : "durango_coin", -372f, -162f, 38f, 38f, Color.white, num + 5, UIWidget.Pivot.Center, simple: true);
			_rCoin = Lbl(rAdd, -346f, -162f, 220, 44, 30, Yellow, num + 5, NGUIText.Alignment.Left, shadow: true);
			_rCoinNote = Lbl(rAdd, -330f, -196f, 330, 28, 16, W(0.7f), num + 5);
			_rHead = Lbl(rAdd, -80f, 139f, 560, 30, 24, W(0.627f), num + 5, NGUIText.Alignment.Left);
			for (int num2 = 0; num2 < 3; num2++)
			{
				RoundRow roundRow = new RoundRow();
				roundRow.Go = rAdd.AddChild();
				roundRow.Go.name = "Round" + (num2 + 1);
				roundRow.Go.transform.localPosition = new Vector3(200f, 89 - num2 * 62, 0f);
				Spr(roundRow.Go, "bg_roundbox2", 0f, 0f, 560f, 54f, K(0.45f), num);
				roundRow.Dot = Spr(roundRow.Go, "circle_32", -252f, 0f, 16f, 16f, Yellow, num + 1);
				roundRow.Round = Lbl(roundRow.Go, -234f, 0f, 110, 54, 22, Color.white, num + 1, NGUIText.Alignment.Left);
				roundRow.Weather = Spr(roundRow.Go, "icon_se_pvp_fog", -110f, 0f, 22f, 17f, Color.white, num + 1, UIWidget.Pivot.Center, simple: true);
				roundRow.WeatherName = Lbl(roundRow.Go, -94f, 0f, 110, 54, 17, W(0.7f), num + 1, NGUIText.Alignment.Left);
				roundRow.Outcome = Lbl(roundRow.Go, 10f, 0f, 90, 54, 22, Yellow, num + 1, NGUIText.Alignment.Left);
				roundRow.TimeIcon = Spr(roundRow.Go, "icon_skill_time", 106f, 0f, 18f, 18f, W(0.6f), num + 1);
				roundRow.Time = Lbl(roundRow.Go, 118f, 0f, 60, 54, 18, Color.white, num + 1, NGUIText.Alignment.Left);
				roundRow.Note = Lbl(roundRow.Go, 270f, 0f, 150, 54, 15, W(0.6f), num + 1, NGUIText.Alignment.Right);
				_rRows[num2] = roundRow;
			}
			_rDc = Lbl(rAdd, -80f, -54f, 560, 60, 18, FoeText, num + 5, NGUIText.Alignment.Left);
			_rFoot = Lbl(rAdd, -80f, -95f, 560, 30, 17, W(0.6f), num + 5, NGUIText.Alignment.Left);
		}
	}

	private void RefreshResult()
	{
		if (_resultWin == null)
		{
			return;
		}
		JObject result = CommunityRanked.Result;
		if (result == null)
		{
			if (_resultWin.IsOpened)
			{
				_resultWin.ForceClose();
			}
			return;
		}
		bool flag = false;
		if (!_resultWin.IsOpened)
		{
			if (ResultBanner(result) || Holding)
			{
				return;
			}
			UIBase.CloseAllUI();
			if (!_resultWin.Open())
			{
				return;
			}
			flag = true;
			_resultShown = true;
			_resultWin.VisibleController.HideExceptForMe(hide: true, "RankedResult");
			UIWidget backgroundWidget = _resultWin.BackgroundWidget;
			if (backgroundWidget != null)
			{
				backgroundWidget.alpha = 1f;
				backgroundWidget.color = K(0.647f);
				backgroundWidget.leftAnchor.SetScreen(0f, 0f);
				backgroundWidget.bottomAnchor.SetScreen(0f, 0f);
				backgroundWidget.rightAnchor.SetScreen(1f, 0f);
				backgroundWidget.topAnchor.SetScreen(1f, 0f);
				backgroundWidget.ResetAndUpdateAnchors();
			}
		}
		int num = Mathf.Clamp(CommunityRanked.Int(result["myTeam"]), 0, 1);
		int num2 = CommunityRanked.Int(result["winner"], -1);
		bool flag2 = num2 == num;
		if (_rTitle != null)
		{
			_rTitle.text = ((num2 < 0) ? CommunityRanked.T("แมตช\u0e4cยกเล\u0e34ก", "Match cancelled") : (flag2 ? CommunityRanked.T("ชนะการดวล", "Duel won") : CommunityRanked.T("แพ\u0e49การดวล", "Duel lost")));
			CommunityRankedAnim.Tint(_rTitle, flag2 ? Yellow : new Color(0.91f, 0.886f, 0.831f));
		}
		if (_rLine != null)
		{
			_rLine.color = (flag2 ? Yellow : new Color(0.75f, 0.72f, 0.66f));
		}
		if (_rHighlight != null)
		{
			_rHighlight.SetActive(flag2);
		}
		if (_rRankLabel != null)
		{
			_rRankLabel.text = "[FFFFFFA0]" + CommunityRanked.T("ข\u0e31\u0e49น", "Rank") + "[-]";
		}
		if (_rExit != null)
		{
			_rExit.Text = CommunityRanked.T("ออก", "Exit");
			_rExit.Disabled = false;
		}
		JObject jObject = result["change"] as JObject;
		string text = CommunityRanked.Str(jObject?["tierAfter"]);
		string text2 = CommunityRanked.Str(jObject?["tierBefore"]);
		SetEmblem(_rEmblem, text);
		_rEmblem.Go.SetActive(jObject != null);
		if (jObject == null)
		{
			string text3 = CommunityRanked.Str(result["mode"]);
			if (string.IsNullOrEmpty(text3))
			{
				text3 = CommunityRanked.Str(CommunityRanked.Match?["mode"]);
			}
			if (string.IsNullOrEmpty(text3))
			{
				text3 = "1v1";
			}
			text = CommunityRanked.Str(CommunityRanked.Lobby?["modes"]?[text3]?["tier"]);
			SetEmblem(_rEmblem, text);
			_rEmblem.Go.SetActive(value: true);
		}
		_rTier.text = TierText(text);
		double? num3 = CommunityRanked.NumOrNull(jObject?["ratingAfter"]);
		_rPoints.gameObject.SetActive(jObject != null && num3.HasValue);
		_rBar.Go.SetActive(jObject != null && !num3.HasValue && !string.IsNullOrEmpty(text));
		if (num3.HasValue)
		{
			int num4 = CommunityRanked.Int(jObject["delta"]);
			_rPoints.text = Num((int)num3.Value) + " [FFFFFF99][size=18]" + CommunityRanked.T("แต\u0e49ม", "pts") + "[/size][-]  [" + ((num4 >= 0) ? "FFD85B]+" : "FF7A5C]") + num4 + "[-]";
		}
		else if (jObject != null && !string.IsNullOrEmpty(text))
		{
			double num5 = CommunityRanked.Num(jObject["progressAfter"]);
			double before = ((text == text2) ? CommunityRanked.Num(jObject["progressBefore"], num5) : (IsHigher(text, text2) ? 0.0 : 1.0));
			SetBar(_rBar, before, num5, ToNext(CommunityRanked.Str(jObject["next"])));
		}
		string text4 = null;
		Color color = new Color(0.3f, 0.24f, 0.06f, 0.9f);
		if (jObject != null && !CommunityRanked.Flag(jObject["counted"]) && jObject["counted"] != null)
		{
			text4 = CommunityRanked.T("ค\u0e39\u0e48เด\u0e34มเก\u0e34น 3 แมตช\u0e4cว\u0e31นน\u0e35\u0e49 — ไม\u0e48น\u0e31บแต\u0e49ม", "Same opponent over 3 times today — not rated");
		}
		else if (jObject != null && !string.IsNullOrEmpty(text) && string.IsNullOrEmpty(text2))
		{
			text4 = "[FFD85B]" + CommunityRanked.T("ครบแมตช\u0e4cว\u0e31ดระด\u0e31บ " + 5 + "/" + 5 + "\nได\u0e49ข\u0e31\u0e49นแรกของฤด\u0e39 " + CommunityRanked.Str(CommunityRanked.Lobby?["season"]), "Placement " + 5 + "/" + 5 + " done\nFirst rank of season " + CommunityRanked.Str(CommunityRanked.Lobby?["season"])) + "[-]";
			color = new Color(0f, 0f, 0f, 0f);
		}
		else if (jObject != null && text != text2 && !string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text))
		{
			bool flag3 = IsHigher(text, text2);
			text4 = CommunityRanked.TierName(text2) + "  [FFD85B]→[-]  " + CommunityRanked.TierName(text) + "   " + (flag3 ? ("[FFD85B]" + CommunityRanked.T("เล\u0e37\u0e48อนข\u0e31\u0e49น!", "Promoted!") + "[-]") : ("[FF7A5C]" + CommunityRanked.T("ตกข\u0e31\u0e49น", "Demoted") + "[-]"));
			color = (flag3 ? new Color(0.3f, 0.24f, 0.06f, 0.9f) : new Color(0.3f, 0.06f, 0.04f, 0.9f));
		}
		else if (jObject == null)
		{
			text4 = ((!CommunityRanked.Flag(result["rated"]) && result["rated"] != null) ? CommunityRanked.T("แมตช\u0e4cฝ\u0e36ก — ไม\u0e48น\u0e31บแต\u0e49ม", "Practice — not rated") : CommunityRanked.T("ไม\u0e48น\u0e31บแต\u0e49ม", "Not rated"));
			color = new Color(0f, 0f, 0f, 0.6f);
		}
		else if (jObject != null && string.IsNullOrEmpty(text))
		{
			int num6 = CommunityRanked.Int(jObject["placementLeft"]);
			text4 = CommunityRanked.T("แมตช\u0e4cว\u0e31ดระด\u0e31บเหล\u0e37อ " + num6, num6 + " placement matches left");
			color = new Color(0f, 0f, 0f, 0f);
		}
		_rBannerBg.gameObject.SetActive(text4 != null && color.a > 0f);
		_rBannerBg.color = color;
		_rBanner.gameObject.SetActive(text4 != null);
		_rBanner.text = text4 ?? string.Empty;
		_rHead.text = CommunityRanked.T("ผลรายยก", "Rounds");
		JArray jArray = result["rounds"] as JArray;
		int num7 = ((jArray != null) ? Mathf.Min(3, jArray.Count) : 0);
		for (int i = 0; i < 3; i++)
		{
			RoundRow roundRow = _rRows[i];
			JToken jToken = ((i < num7) ? jArray[i] : null);
			roundRow.Go.SetActive(jToken != null);
			if (jToken != null)
			{
				int num8 = CommunityRanked.Int(jToken["round"], i + 1);
				bool flag4 = CommunityRanked.Int(jToken["winner"], -1) == num;
				string text5 = CommunityRanked.Str(jToken["weather"]);
				if (string.IsNullOrEmpty(text5))
				{
					text5 = ((num8 <= 1) ? "pvp_fog" : ((num8 == 2) ? "pvp_rain" : "pvp_storm_01"));
				}
				roundRow.Dot.color = (flag4 ? Yellow : Red);
				roundRow.Round.text = CommunityRanked.T("ยก ", "Round ") + num8;
				roundRow.Weather.spriteName = CommunityRanked.WeatherIcon(text5);
				roundRow.WeatherName.text = CommunityRanked.WeatherName(text5);
				roundRow.Outcome.text = (flag4 ? CommunityRanked.T("ชนะ", "Won") : CommunityRanked.T("แพ\u0e49", "Lost"));
				roundRow.Outcome.color = (flag4 ? Yellow : FoeText);
				double? num9 = CommunityRanked.NumOrNull(jToken["secs"]);
				roundRow.TimeIcon.gameObject.SetActive(num9.HasValue);
				roundRow.Time.gameObject.SetActive(num9.HasValue);
				roundRow.Time.text = (num9.HasValue ? CommunityRanked.Clock(num9.Value) : string.Empty);
				string text6 = CommunityRanked.Str(jToken["reason"]);
				JArray jArray2 = jToken["hp"] as JArray;
				int num10 = ((jArray2 != null && jArray2.Count == 2) ? Mathf.RoundToInt(100f * (float)CommunityRanked.Num(jArray2[num])) : (-1));
				int num11 = ((jArray2 != null && jArray2.Count == 2) ? Mathf.RoundToInt(100f * (float)CommunityRanked.Num(jArray2[1 - num])) : (-1));
				roundRow.Note.text = ((!(text6 == "ko") && !string.IsNullOrEmpty(text6)) ? ((text6 == "time") ? (num10 + "% : " + num11 + "%") : CommunityRanked.ReasonName(text6)) : ((num10 >= 0) ? (num10 + "%") : string.Empty));
			}
		}
		string text7 = CommunityRanked.Str(result["reason"]);
		bool active = text7 == "leave" || text7 == "disconnect" || text7 == "noshow";
		_rDc.gameObject.SetActive(active);
		_rDc.transform.localPosition = new Vector3(-80f, 89 - num7 * 62 - 10, 0f);
		_rDc.text = CommunityRanked.T("หล\u0e38ดเกม 30 ว\u0e34 หร\u0e37อกดออกเอง = แพ\u0e49ท\u0e31\u0e49งแมตช\u0e4c", "Disconnecting 30 s or leaving = match loss");
		_rFoot.text = CommunityRanked.T("ไม\u0e48ได\u0e49 exp · ไม\u0e48ม\u0e35ของตก", "No exp · no drops");
		if (flag)
		{
			Play(_aRTitle);
			Play(_aRLine);
			Play(_rEmblem?.In);
			Play(_aRDetails);
		}
		RefreshResultCoin(result);
	}

	private void RefreshResultCoin(JObject result)
	{
		if (_rCoin == null)
		{
			return;
		}
		JObject jObject = CommunityRanked.CoinFor(result);
		bool flag = jObject != null;
		_rCoinIcon.gameObject.SetActive(flag);
		_rCoin.gameObject.SetActive(flag);
		_rCoinNote.gameObject.SetActive(flag);
		if (flag)
		{
			bool flag2 = CommunityRanked.Flag(jObject["granted"]);
			int val = CommunityRanked.Int(jObject["amount"]);
			CommunityRankedAnim.Tint(_rCoinIcon, flag2 ? Color.white : W(0.35f));
			_rCoin.text = "+" + Math.Max(0, val) + " [FFFFFFB4][size=20]Coin[/size][-]";
			CommunityRankedAnim.Tint(_rCoin, flag2 ? Yellow : W(0.45f));
			if (flag2 && !_coinShown)
			{
				_coinShown = true;
				Play(_aRCoin);
			}
			string text = CommunityRanked.OneLanguage(CommunityRanked.Str(jObject["text"]));
			int num = CommunityRanked.Int(jObject["remaining"], -1);
			int num2 = CommunityRanked.Int(jObject["cap"]);
			if (flag2)
			{
				_rCoinNote.text = ((num >= 0 && num2 > 0) ? CommunityRanked.T("ว\u0e31นน\u0e35\u0e49ร\u0e31บ Coin ได\u0e49อ\u0e35ก " + num + " แมตช\u0e4c", num + " more coin matches today") : string.Empty);
			}
			else
			{
				_rCoinNote.text = ((!string.IsNullOrEmpty(text)) ? text : CommunityRanked.T("แมตช\u0e4cน\u0e35\u0e49ไม\u0e48ได\u0e49 Coin", "No Coin from this match"));
			}
		}
	}

	private void KeepArenaClear()
	{
		bool inMatch = CommunityRanked.InMatch;
		int num = (inMatch ? CommunityRanked.Int(CommunityRanked.Match?["round"]) : (-1));
		if ((inMatch && !_wasInMatch) || (inMatch && num != _lastRound))
		{
			_closeMapUntil = Time.realtimeSinceStartup + 8f;
		}
		_wasInMatch = inMatch;
		_lastRound = num;
		if (inMatch && Time.realtimeSinceStartup < _closeMapUntil)
		{
			WorldMapGroup worldMapGroup = UIManager.FindScript<WorldMapGroup>();
			if (worldMapGroup != null && worldMapGroup.IsOpened)
			{
				worldMapGroup.ForceClose();
			}
		}
		if (_resultWin != null && _resultWin.IsOpened)
		{
			if (UIBase.CurrentUI != null && UIBase.CurrentUI != _resultWin)
			{
				UIBase.CloseAllUI();
			}
			CharacterInfoGroup characterInfoGroup = UIManager.FindScript<CharacterInfoGroup>();
			if (characterInfoGroup != null && characterInfoGroup.IsOpened)
			{
				characterInfoGroup.ForceClose();
			}
		}
	}

	private static bool IsHigher(string a, string b)
	{
		return Array.IndexOf(CommunityRanked.TierIds, a) > Array.IndexOf(CommunityRanked.TierIds, b);
	}

	private void Refresh()
	{
		Step(AnimPhase);
		Step(RefreshResult);
		Step(RefreshBreak);
		Step(RefreshLobby);
		Step(RefreshFound);
		Step(RefreshHud);
		Step(RefreshCountdown);
		Step(RefreshRingWarn);
		Step(RefreshPotion);
		Step(AnimAppear);
	}

	private static void Step(Action a)
	{
		try
		{
			a();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private void Update()
	{
		if (Time.realtimeSinceStartup < _nextTick)
		{
			return;
		}
		_nextTick = Time.realtimeSinceStartup + 0.25f;
		Step(KeepArenaClear);
		Step(TickHud);
		Step(RefreshCountdown);
		Step(RefreshRingWarn);
		if (_lobbyWin != null && _lobbyWin.IsOpened && CommunityRanked.Queued)
		{
			Step(RefreshLobby);
		}
		if (_lobbyOpen && _lobbyWin != null && !_lobbyWin.IsOpened)
		{
			_lobbyOpen = false;
		}
		if (_found.activeSelf || CommunityRanked.Found != null)
		{
			Step(RefreshFound);
		}
		if (_break.activeSelf)
		{
			_bClock.text = CommunityRanked.Clock(CommunityRanked.PhaseLeft);
			if (CommunityRanked.Flag(CommunityRanked.Match?["ownGear"]))
			{
				Step(RefreshBreak);
			}
		}
		if ((CommunityRanked.InMatch && CommunityRanked.Match != null && CommunityRanked.Result == null) != (_hud.activeSelf || _break.activeSelf))
		{
			Refresh();
		}
		if (_holdUntil > 0f && Time.realtimeSinceStartup >= _holdUntil)
		{
			_holdUntil = 0f;
			Refresh();
		}
		Step(AnimAppear);
	}

	private static void Play(CommunityRankedAnim.Anim anim)
	{
		anim?.Play();
	}

	private void BuildAnim()
	{
		_banner = Layer("Banner", _overDepth + 2, _overLayer);
		_bannerAt = Anchored(_banner, UIBase.AnchorType.Base, 0.5f, 0.5f, 0, 100, 478, 178);
		_banner.SetActive(value: true);
		Transform shield = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Victory, "img_shield");
		Transform wingL = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Victory, "wing_left");
		Transform wingR = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Victory, "wing_right");
		Transform src = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Victory, "Label_Subject");
		Transform src2 = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Victory, "Label_Comment");
		Transform src3 = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Start, "Label_Subject");
		Transform src4 = CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Start, "Label_Comment");
		Func<Emblem, CommunityRankedAnim.Anim> func = (Emblem e) => (e != null) ? CommunityRankedAnim.Copy(shield, e.Go).With(CommunityRankedAnim.Copy(wingL, e.WingL.gameObject)).With(CommunityRankedAnim.Copy(wingR, e.WingR.gameObject)) : null;
		if (_lEmblem != null)
		{
			_lEmblem.In = func(_lEmblem);
		}
		_aTier = CommunityRankedAnim.SoftOpen(_lTierGo);
		_aParty = CommunityRankedAnim.SoftOpen(_lPartyGo);
		for (int num = 0; num < 2; num++)
		{
			_aTabs[num] = ((_tabLine[num] != null) ? CommunityRankedAnim.Selector(_tabLine[num].gameObject) : null);
		}
		_aQueue = ((_lQueue != null) ? CommunityRankedAnim.Pulse(_lQueue.gameObject) : null);
		_aFound = CommunityRankedAnim.SoftOpen(_found);
		for (int num2 = 0; num2 < 2; num2++)
		{
			Card card = _fCards[num2];
			if (card != null && !(card.Body == null))
			{
				card.In = CommunityRankedAnim.Copy(CommunityRankedAnim.WarPart(CommunityRankedAnim.War.Start, (num2 == 0) ? "img_sword_left" : "img_sword_right"), card.Body);
			}
		}
		_aVs = CommunityRankedAnim.Copy(src3, _fVs);
		_aNote = ((_fNote != null) ? CommunityRankedAnim.Copy(src4, _fNote.gameObject) : null);
		_aCount = CommunityRankedAnim.Copy(CommunityRankedAnim.ChapterPart("ChapterEffect01/Bg"), _cBand);
		_aCountNum = CommunityRankedAnim.Copy(src3, _cNumber.gameObject);
		_aHud = CommunityRankedAnim.SoftOpen(_hud);
		for (int num3 = 0; num3 < 4; num3++)
		{
			_aPips[num3] = CommunityRankedAnim.Copy(src3, _pips[num3].gameObject);
		}
		_aStatus = CommunityRankedAnim.Copy(src4, _hStatusGo);
		_aBreak = CommunityRankedAnim.SoftOpen(_break);
		Tile[] tiles = _tiles;
		foreach (Tile obj in tiles)
		{
			obj.Sel = CommunityRankedAnim.Selector(obj.Select.gameObject);
		}
		if (_rEmblem != null)
		{
			_rEmblem.In = func(_rEmblem);
		}
		_aRTitle = ((_rTitle != null) ? CommunityRankedAnim.Copy(src, _rTitle.gameObject) : null);
		_aRLine = ((_rLine != null) ? CommunityRankedAnim.Copy(CommunityRankedAnim.ChapterPart("ChapterEffect00/separator"), _rLine.gameObject, scaleOnly: true) : null);
		_aRDetails = CommunityRankedAnim.Copy(src2, _rDetails);
		_aRCoin = ((_rCoin != null) ? CommunityRankedAnim.Copy(src, _rCoin.gameObject).With(CommunityRankedAnim.Copy(src, _rCoinIcon.gameObject)) : null);
	}

	private float Banner(CommunityRankedAnim.War war, string subject, string comment)
	{
		return CommunityRankedAnim.ShowWar(war, _bannerAt, subject, comment);
	}

	private void Hold(float seconds)
	{
		if (seconds > 0f)
		{
			_holdUntil = Mathf.Max(_holdUntil, Time.realtimeSinceStartup + seconds);
		}
	}

	private static string ScoreLine(JArray score, int myTeam)
	{
		if (score == null || score.Count != 2 || myTeam < 0 || myTeam > 1)
		{
			return string.Empty;
		}
		return "[size=44][b][FFD85B]" + CommunityRanked.Int(score[myTeam]) + "[-]  :  [FF7A5C]" + CommunityRanked.Int(score[1 - myTeam]) + "[-][/b][/size]";
	}

	private void AnimPhase()
	{
		JObject match = CommunityRanked.Match;
		string text = ((CommunityRanked.InMatch && match != null) ? CommunityRanked.Str(match["matchId"]) : null);
		string text2 = ((text != null) ? CommunityRanked.Phase : string.Empty);
		if (text != null && text == _animMatch && text2 != _animPhase && CommunityRanked.Result == null)
		{
			int num = CommunityRanked.Int(match["round"], 1);
			if (text2 == "Live" && _animPhase == "Countdown")
			{
				Banner(CommunityRankedAnim.War.Start, CommunityRanked.T("ยก " + num + " · ส\u0e39\u0e49!", "Round " + num + " · Fight!"), CommunityRanked.WeatherName(CommunityRanked.Str(match["weather"])));
			}
			else if (text2 == "Overtime" && _animPhase == "Live")
			{
				Banner(CommunityRankedAnim.War.Start, CommunityRanked.T("ต\u0e48อเวลา!", "Overtime!"), CommunityRanked.T("ใครโดนต\u0e35ก\u0e48อนแพ\u0e49", "first hit taken loses"));
			}
			else if (text2 == "Break" && (_animPhase == "Live" || _animPhase == "Overtime"))
			{
				int myTeam = CommunityRanked.MyTeam;
				bool flag = myTeam >= 0 && CommunityRanked.Int(match["loser"], -1) == myTeam;
				Hold(Banner(flag ? CommunityRankedAnim.War.Defeat : CommunityRankedAnim.War.Victory, flag ? CommunityRanked.T("แพ\u0e49ยก " + num, "Round " + num + " lost") : CommunityRanked.T("ชนะยก " + num, "Round " + num + " won"), ScoreLine(match["score"] as JArray, myTeam)));
			}
		}
		_animMatch = text;
		_animPhase = text2;
	}

	private bool ResultBanner(JObject result)
	{
		if (result != _animResult)
		{
			_animResult = result;
			_coinShown = false;
			int num = Mathf.Clamp(CommunityRanked.Int(result["myTeam"]), 0, 1);
			int num2 = CommunityRanked.Int(result["winner"], -1);
			if (num2 >= 0)
			{
				bool flag = num2 == num;
				Hold(Banner((!flag) ? CommunityRankedAnim.War.Defeat : CommunityRankedAnim.War.Victory, flag ? CommunityRanked.T("ชนะการดวล!", "Duel won!") : CommunityRanked.T("แพ\u0e49การดวล", "Duel lost"), ScoreLine(result["score"] as JArray, num)));
			}
		}
		return Holding;
	}

	private void AnimAppear()
	{
		bool flag = Active(_found);
		bool flag2 = Active(_hud);
		bool flag3 = Active(_count);
		bool flag4 = Active(_break);
		if (flag && !_wasFound)
		{
			Play(_aFound);
			Play(_aStrip);
			Card[] fCards = _fCards;
			for (int i = 0; i < fCards.Length; i++)
			{
				Play(fCards[i]?.In);
			}
			Play(_aVs);
			Play(_aNote);
			CommunityRankedAnim.PlayChapterSound();
		}
		if (flag2 && !_wasHud)
		{
			Play(_aHud);
		}
		if (flag3 && !_wasCount)
		{
			Play(_aCount);
		}
		if (flag4 && !_wasBreak)
		{
			Play(_aBreak);
		}
		if (!flag3)
		{
			_lastCount = null;
		}
		if (!flag2)
		{
			_lastStatus = null;
		}
		_wasFound = flag;
		_wasHud = flag2;
		_wasCount = flag3;
		_wasBreak = flag4;
	}

	private void SelectTile(Tile tile, bool on)
	{
		bool flag = tile.Select.enabled;
		tile.Select.enabled = on;
		if (on && !flag && _wasBreak)
		{
			Play(tile.Sel);
		}
	}

	public static string AnimCommand(string[] args)
	{
		Ensure();
		if (_inst == null)
		{
			return "ui none";
		}
		string text = ((args.Length > 2) ? args[2].ToLowerInvariant() : string.Empty);
		float num = 0f;
		string comment = ScoreLine(new JArray(1, 0), 0);
		switch (text)
		{
		case "sound":
			CommunityRankedAnim.Sound = args.Length < 4 || args[3].ToLowerInvariant() != "off";
			break;
		case "start":
			num = _inst.Banner(CommunityRankedAnim.War.Start, CommunityRanked.T("ยก 2 · ส\u0e39\u0e49!", "Round 2 · Fight!"), CommunityRanked.WeatherName("pvp_rain"));
			break;
		case "ot":
			num = _inst.Banner(CommunityRankedAnim.War.Start, CommunityRanked.T("ต\u0e48อเวลา!", "Overtime!"), CommunityRanked.T("ใครโดนต\u0e35ก\u0e48อนแพ\u0e49", "first hit taken loses"));
			break;
		case "wonround":
			num = _inst.Banner(CommunityRankedAnim.War.Victory, CommunityRanked.T("ชนะยก 1", "Round 1 won"), comment);
			break;
		case "lostround":
			num = _inst.Banner(CommunityRankedAnim.War.Defeat, CommunityRanked.T("แพ\u0e49ยก 1", "Round 1 lost"), ScoreLine(new JArray(0, 1), 0));
			break;
		case "won":
			num = _inst.Banner(CommunityRankedAnim.War.Victory, CommunityRanked.T("ชนะการดวล!", "Duel won!"), ScoreLine(new JArray(2, 1), 0));
			break;
		case "lost":
			num = _inst.Banner(CommunityRankedAnim.War.Defeat, CommunityRanked.T("แพ\u0e49การดวล", "Duel lost"), ScoreLine(new JArray(1, 2), 0));
			break;
		case "off":
			CommunityRankedAnim.HideWar();
			break;
		default:
			return "ranked anim start|ot|wonround|lostround|won|lost|off|sound on|off · " + CommunityRankedAnim.Describe();
		}
		return "anim " + text + " " + num.ToString("0.0") + " s · " + CommunityRankedAnim.Describe();
	}

	private static string Plain(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return string.Empty;
		}
		return name.Replace("[", "(").Replace("]", ")");
	}

	private static UILabel FindStyle()
	{
		try
		{
			if (UIManager.MessageBox != null)
			{
				UILabel[] componentsInChildren = UIManager.MessageBox.GetComponentsInChildren<UILabel>(includeInactive: true);
				foreach (UILabel uILabel in componentsInChildren)
				{
					if (uILabel.trueTypeFont != null || uILabel.bitmapFont != null)
					{
						return uILabel;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}
}

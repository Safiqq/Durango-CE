using System;
using System.Text.RegularExpressions;
using BestHTTP;
using Durango.Logic.Clusters;
using Durango.Logic.Explore;
using Durango.Network;
using Durango.System;
using Durango.System.Config;
using Durango.Utils;
using UnityEngine;

namespace Durango.UI;

public class CommunityFpsCounter : MonoBehaviour
{
	public const string SettingKey = "show_fps";

	private const float Window = 0.5f;

	private const float OnlineInterval = 15f;

	private const float RootRetry = 0.5f;

	private const int PanelDepth = 5500;

	private const int TopGap = 4;

	private const int BarHeight = 34;

	private const int FontSize = 18;

	private const int Pad = 14;

	private const int Gap = 6;

	private const int SepGap = 10;

	private const int SepHeight = 18;

	private const int DotSize = 10;

	private const int PingGoodMs = 80;

	private const int PingBadMs = 150;

	private const float BgScale = 0.5f;

	private const string PcBgSprite = "bg_popup_pc";

	private const string MobileBgSprite = "bg_todo";

	private const string SepSprite = "bg_grunge_line";

	private static readonly Color PcBgColor = new Color(0.7058824f, 0.6901961f, 0.6901961f, 2f / 3f);

	private static readonly Color MobileBgColor = new Color(1f, 1f, 1f, 40f / 51f);

	private static readonly Color TextColor = new Color(0.91f, 0.9f, 0.87f, 1f);

	private static readonly Color IconColor = new Color(0.85f, 0.84f, 0.81f, 1f);

	private static readonly Color SepColor = new Color(0.61960787f, 0.61960787f, 0.61960787f, 28f / 51f);

	private static readonly Color PingGood = new Color(0.36f, 0.82f, 0.36f, 1f);

	private static readonly Color PingMid = new Color(0.95f, 0.78f, 0.2f, 1f);

	private static readonly Color PingBad = new Color(0.9f, 0.3f, 0.25f, 1f);

	private static readonly Color PingNone = new Color(0.55f, 0.55f, 0.55f, 1f);

	private static CommunityFpsCounter s_instance;

	private float _elapsed;

	private int _frames;

	private int _fps = -1;

	private int _online = -1;

	private int _island = -1;

	private string _islandId;

	private float _nextOnlineAt;

	private bool _fetching;

	private string _onlineGateway;

	private float _nextRootAt;

	private UIRoot _root;

	private GameObject _barGo;

	private bool _builtPc;

	private UIWidget _box;

	private UISprite _bg;

	private UISprite _peopleIcon;

	private UILabel _peopleLabel;

	private UISprite _sep1;

	private UISprite _islandIcon;

	private UILabel _islandLabel;

	private UISprite _sepIsland;

	private UISprite _fpsIcon;

	private UILabel _fpsLabel;

	private UISprite _sep2;

	private UISprite _dot;

	private UILabel _pingLabel;

	private UILabel _transportLabel;

	private bool _styled;

	private string _shownKey;

	private UIWidget _anchoredTo;

	private int _anchoredWidth;

	public static string Label => CommunityDiscordLink.Lang switch
	{
		"th" => "แสดงผ\u0e39\u0e49เล\u0e48นออนไลน\u0e4c · คนบนเกาะ · FPS · Ping",
		"id" => "Tampilkan pemain online · di pulau · FPS · Ping",
		"ko" => "접속자 · 섬 인원 · FPS · 핑 표시",
		_ => "Show players online · on island · FPS · Ping",
	};

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

	public static void SetVisible(bool visible)
	{
		if (visible)
		{
			if (s_instance == null)
			{
				GameObject obj = new GameObject("CommunityFpsCounter");
				UnityEngine.Object.DontDestroyOnLoad(obj);
				s_instance = obj.AddComponent<CommunityFpsCounter>();
			}
			s_instance.enabled = true;
		}
		else if (s_instance != null)
		{
			s_instance.enabled = false;
		}
	}

	public static void DebugCommand(string[] args)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "state");
		switch (text)
		{
		case "on":
		case "off":
			ConfigInstance.ChangeValue("show_fps", text == "on");
			break;
		case "fetch":
			if (s_instance != null)
			{
				s_instance._nextOnlineAt = 0f;
			}
			break;
		}
	}

	public static string Describe()
	{
		CommunityFpsCounter communityFpsCounter = s_instance;
		if (communityFpsCounter == null)
		{
			return "hud none (switch never turned on)";
		}
		string text = "enabled " + communityFpsCounter.enabled + " · pc " + PcUi + " · fps " + communityFpsCounter._fps + " · online " + communityFpsCounter._online + " · island " + (communityFpsCounter._islandId ?? "-") + "=" + communityFpsCounter._island + " · ping " + PingMs() + " · gateway " + (communityFpsCounter._onlineGateway ?? "-");
		if (communityFpsCounter._barGo == null)
		{
			return text + " · bar none";
		}
		UIWidget box = communityFpsCounter._box;
		Vector3 vector = ((UICamera.mainCamera != null) ? UICamera.mainCamera.WorldToScreenPoint(box.transform.position) : Vector3.zero);
		return text + " · bar active " + communityFpsCounter._barGo.activeInHierarchy + " · styled " + communityFpsCounter._styled + " · anchored " + (communityFpsCounter._anchoredTo != null) + " · texts [" + communityFpsCounter._peopleLabel.text + " | " + communityFpsCounter._fpsLabel.text + " | " + communityFpsCounter._pingLabel.text + " | " + communityFpsCounter._transportLabel.text + "] · box " + box.width + "x" + box.height + " · bg " + communityFpsCounter._bg.spriteName + " " + communityFpsCounter._bg.width + "x" + communityFpsCounter._bg.height + " ×" + 0.5f + " · screen centre " + Mathf.RoundToInt(vector.x) + "," + Mathf.RoundToInt(vector.y) + " of " + Screen.width + "x" + Screen.height;
	}

	private void OnEnable()
	{
		CommunityUdp.TransportChanged -= OnTransportChanged;
		CommunityUdp.TransportChanged += OnTransportChanged;
		_elapsed = 0f;
		_frames = 0;
		_fps = -1;
		_nextOnlineAt = 0f;
		_nextRootAt = 0f;
		_shownKey = null;
		if (_barGo != null)
		{
			_barGo.SetActive(value: true);
		}
		Refresh();
	}

	private void OnDisable()
	{
		CommunityUdp.TransportChanged -= OnTransportChanged;
		if (_barGo != null)
		{
			_barGo.SetActive(value: false);
		}
	}

	private void OnTransportChanged()
	{
		_shownKey = null;
		Refresh();
	}

	private void Update()
	{
		_elapsed += Time.unscaledDeltaTime;
		_frames++;
		if (_elapsed >= 0.5f)
		{
			_fps = Mathf.RoundToInt((float)_frames / _elapsed);
			_elapsed = 0f;
			_frames = 0;
			Refresh();
		}
		PollOnline();
	}

	private static int PingMs()
	{
		try
		{
			Connection frontendIfAny = Connections.FrontendIfAny;
			if (frontendIfAny == null || !frontendIfAny.Connected() || frontendIfAny.Ping <= 0f)
			{
				return -1;
			}
			return Mathf.RoundToInt(frontendIfAny.Ping * 1000f);
		}
		catch (Exception)
		{
			return -1;
		}
	}

	private static string CurrentGateway()
	{
		try
		{
			Cluster connectCluster = GameManager.ConnectCluster;
			if (connectCluster != null && !string.IsNullOrEmpty(connectCluster.GatewayUrlRoot))
			{
				return connectCluster.GatewayUrlRoot;
			}
		}
		catch (Exception)
		{
		}
		try
		{
			return CommunityLink.Gateway;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static string CurrentIsland()
	{
		try
		{
			Connection frontendIfAny = Connections.FrontendIfAny;
			if (frontendIfAny == null || !frontendIfAny.Connected())
			{
				return null;
			}
			Region region = GameManager.Region;
			return (region != null && !string.IsNullOrEmpty(region.Id)) ? region.Id : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private void PollOnline()
	{
		string island = CurrentIsland();
		if (island != _islandId)
		{
			_islandId = island;
			_island = -1;
			_nextOnlineAt = 0f;
			Refresh();
		}
		if (_fetching || Time.unscaledTime < _nextOnlineAt)
		{
			return;
		}
		_nextOnlineAt = Time.unscaledTime + 15f;
		string gateway = CurrentGateway();
		if (!CommunityLink.SameGateway(gateway, _onlineGateway))
		{
			_online = -1;
			_onlineGateway = gateway;
		}
		if (string.IsNullOrEmpty(gateway))
		{
			return;
		}
		_fetching = true;
		FetchStatus(gateway, island, delegate(bool ok, int online, int onIsland)
		{
			_fetching = false;
			if (!(this == null) && CommunityLink.SameGateway(gateway, _onlineGateway))
			{
				_online = (ok ? online : (-1));
				if (island == _islandId)
				{
					_island = ((ok && island != null) ? onIsland : (-1));
				}
				Refresh();
			}
		});
	}

	private static void FetchStatus(string gateway, string island, Action<bool, int, int> callback)
	{
		try
		{
			HTTPRequest hTTPRequest = new HTTPRequest(new Uri(gateway.TrimEnd('/') + "/status" + (string.IsNullOrEmpty(island) ? string.Empty : ("?island=" + Uri.EscapeDataString(island)))), HTTPMethods.Get, delegate(HTTPRequest req, HTTPResponse resp)
			{
				bool arg = false;
				int arg2 = -1;
				int arg3 = -1;
				try
				{
					if (resp != null && resp.IsSuccess)
					{
						string text = resp.DataAsText ?? string.Empty;
						arg = text.Contains("\"ok\":true") || text.Contains("\"ok\": true");
						arg2 = JsonInt(text, "online");
						arg3 = JsonInt(text, "island");
					}
				}
				catch (Exception)
				{
				}
				callback(arg, arg2, arg3);
			});
			hTTPRequest.Timeout = TimeSpan.FromSeconds(5.0);
			hTTPRequest.DisableCache = true;
			hTTPRequest.Send();
		}
		catch (Exception)
		{
			callback(arg1: false, -1, -1);
		}
	}

	private static int JsonInt(string json, string key)
	{
		Match match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)");
		if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
		{
			return -1;
		}
		return result;
	}

	private void Refresh()
	{
		if (base.enabled && EnsureBar())
		{
			if (!_styled)
			{
				ApplyStyle();
			}
			bool pcUi = PcUi;
			int num = PingMs();
			string text = ((_online >= 0) ? _online.ToString() : "--");
			string text2 = ((_fps >= 0) ? _fps.ToString() : "--");
			string text3 = ((num >= 0) ? num.ToString() : "--");
			string text4 = (pcUi ? ("FPS " + text2) : text2);
			string text5 = (pcUi ? ("Ping " + text3 + " ms") : (text3 + " ms"));
			string text6 = ((_island >= 0) ? _island.ToString() : "--");
			string text7 = CommunityUdp.BadgeText();
			bool flag = text7 == "TCP" && CommunityUdp.RecentFallback;
			string text8 = text + "|" + text6 + "|" + text4 + "|" + text5 + "|" + text7 + (flag ? "~" : "") + "|" + _styled;
			if (!(text8 == _shownKey))
			{
				_shownKey = text8;
				Color color = ((num < 0) ? PingNone : ((num < 80) ? PingGood : ((num <= 150) ? PingMid : PingBad)));
				_peopleLabel.text = text;
				_islandLabel.text = text6;
				_fpsLabel.text = text4;
				_pingLabel.text = text5;
				_pingLabel.color = color;
				_dot.color = color;
				_transportLabel.text = text7;
				_transportLabel.color = (flag ? PingNone : color);
				Layout();
			}
		}
	}

	private bool EnsureBar()
	{
		if (Time.unscaledTime < _nextRootAt)
		{
			return _barGo != null;
		}
		_nextRootAt = Time.unscaledTime + 0.5f;
		UIRoot uIRoot = FindRoot();
		if (_barGo != null)
		{
			if (uIRoot == null || (uIRoot == _root && _builtPc == PcUi))
			{
				return true;
			}
			UnityEngine.Object.Destroy(_barGo);
			_barGo = null;
		}
		if (uIRoot == null)
		{
			return false;
		}
		Build(uIRoot);
		return _barGo != null;
	}

	private static UIRoot FindRoot()
	{
		try
		{
			if (Singleton<UIManager>.HasInstance())
			{
				UIRoot uIRoot = Singleton<UIManager>.Instance().UIRoot;
				if (uIRoot != null)
				{
					return uIRoot;
				}
			}
		}
		catch (Exception)
		{
		}
		foreach (UIRoot item in UIRoot.list)
		{
			if (item != null && item.gameObject.activeInHierarchy)
			{
				return item;
			}
		}
		return null;
	}

	private void Build(UIRoot root)
	{
		_root = root;
		_builtPc = PcUi;
		_barGo = root.gameObject.AddChild();
		_barGo.name = "CommunityHudStats";
		_barGo.AddComponent<UIPanel>().depth = 5500;
		_box = _barGo.AddWidget<UIWidget>(0);
		_box.name = "Box";
		_box.height = 34;
		GameObject gameObject = _box.gameObject;
		_bg = gameObject.AddSprite(_builtPc ? "bg_popup_pc" : "bg_todo", 0);
		_bg.name = "Bg";
		_bg.color = (_builtPc ? PcBgColor : MobileBgColor);
		_bg.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
		_peopleIcon = Icon(gameObject, "icon_person", 22, 18);
		_peopleLabel = Text(gameObject, "Online");
		_sep1 = Separator(gameObject);
		_islandIcon = Icon(gameObject, "icon_popup_player_island", 22, 19);
		_islandLabel = Text(gameObject, "Island");
		_sepIsland = Separator(gameObject);
		_fpsIcon = Icon(gameObject, "icon_option_fps", 24, 24);
		_fpsLabel = Text(gameObject, "Fps");
		_sep2 = Separator(gameObject);
		_dot = gameObject.AddSprite("dot_12", 2);
		_dot.name = "PingDot";
		_dot.type = UIBasicSprite.Type.Simple;
		_dot.pivot = UIWidget.Pivot.Left;
		_dot.width = 10;
		_dot.height = 10;
		_pingLabel = Text(gameObject, "Ping");
		_transportLabel = Text(gameObject, "Transport");
		_styled = false;
		_shownKey = null;
		_anchoredTo = null;
		_anchoredWidth = 0;
		_barGo.SetActive(base.enabled);
	}

	private static UISprite Icon(GameObject parent, string sprite, int width, int height)
	{
		UISprite uISprite = parent.AddSprite(sprite, 2);
		uISprite.name = sprite;
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.pivot = UIWidget.Pivot.Left;
		uISprite.width = width;
		uISprite.height = height;
		uISprite.color = IconColor;
		return uISprite;
	}

	private static UISprite Separator(GameObject parent)
	{
		UISprite uISprite = parent.AddSprite("bg_grunge_line", 1);
		uISprite.name = "Sep";
		uISprite.type = UIBasicSprite.Type.Simple;
		uISprite.pivot = UIWidget.Pivot.Left;
		uISprite.width = 2;
		uISprite.height = 18;
		uISprite.color = SepColor;
		return uISprite;
	}

	private static UILabel Text(GameObject parent, string name)
	{
		UILabel uILabel = parent.AddWidget<UILabel>(3);
		uILabel.name = name;
		uILabel.fontSize = 18;
		uILabel.color = TextColor;
		uILabel.pivot = UIWidget.Pivot.Left;
		uILabel.alignment = NGUIText.Alignment.Left;
		uILabel.overflowMethod = UILabel.Overflow.ResizeFreely;
		uILabel.supportEncoding = false;
		uILabel.height = 24;
		return uILabel;
	}

	private void ApplyStyle()
	{
		UILabel uILabel = FindStyle();
		if (uILabel == null)
		{
			return;
		}
		UILabel[] array = new UILabel[5] { _peopleLabel, _islandLabel, _fpsLabel, _pingLabel, _transportLabel };
		foreach (UILabel uILabel2 in array)
		{
			if (uILabel.bitmapFont != null)
			{
				uILabel2.bitmapFont = uILabel.bitmapFont;
			}
			else
			{
				uILabel2.trueTypeFont = uILabel.trueTypeFont;
			}
			uILabel2.fontSize = 18;
		}
		_styled = true;
	}

	private static UILabel FindStyle()
	{
		try
		{
			if (Singleton<UIManager>.HasInstance() && UIManager.MessageBox != null)
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
		UILabel uILabel2 = null;
		try
		{
			UILabel[] componentsInChildren = UnityEngine.Object.FindObjectsByType<UILabel>(FindObjectsSortMode.None);
			foreach (UILabel uILabel3 in componentsInChildren)
			{
				if (uILabel3.trueTypeFont != null)
				{
					return uILabel3;
				}
				if (uILabel2 == null && uILabel3.bitmapFont != null)
				{
					uILabel2 = uILabel3;
				}
			}
		}
		catch (Exception)
		{
		}
		return uILabel2;
	}

	private void Layout()
	{
		int x = 14;
		x = Place(_peopleIcon, x, _peopleIcon.width) + 6;
		x = Place(_peopleLabel, x, LabelWidth(_peopleLabel)) + 10;
		x = Place(_sep1, x, _sep1.width) + 10;
		x = Place(_islandIcon, x, _islandIcon.width) + 6;
		x = Place(_islandLabel, x, LabelWidth(_islandLabel)) + 10;
		x = Place(_sepIsland, x, _sepIsland.width) + 10;
		x = Place(_fpsIcon, x, _fpsIcon.width) + 6;
		x = Place(_fpsLabel, x, LabelWidth(_fpsLabel)) + 10;
		x = Place(_sep2, x, _sep2.width) + 10;
		x = Place(_dot, x, 10) + 6;
		x = Place(_pingLabel, x, LabelWidth(_pingLabel));
		int num = LabelWidth(_transportLabel);
		x = Place(_transportLabel, x + ((num > 0) ? 6 : 0), num) + 14;
		int num2 = x;
		int num3 = num2 / 2;
		Transform[] array = new Transform[12]
		{
			_peopleIcon.transform, _peopleLabel.transform, _sep1.transform, _islandIcon.transform, _islandLabel.transform, _sepIsland.transform, _fpsIcon.transform, _fpsLabel.transform, _sep2.transform, _dot.transform,
			_pingLabel.transform, _transportLabel.transform
		};
		foreach (Transform obj in array)
		{
			obj.localPosition = new Vector3(obj.localPosition.x - (float)num3, 0f, 0f);
		}
		_bg.width = Mathf.RoundToInt((float)num2 / 0.5f);
		_bg.height = Mathf.RoundToInt(68f);
		_bg.transform.localPosition = Vector3.zero;
		UIWidget uIWidget = ExistingRootAnchor();
		if (uIWidget != null)
		{
			if (uIWidget != _anchoredTo || num2 != _anchoredWidth)
			{
				_box.SetAnchor(uIWidget.gameObject, 0.5f, -num3, 1f, -38, 0.5f, num2 - num3, 1f, -4);
				_box.ResetAndUpdateAnchors();
				_anchoredTo = uIWidget;
				_anchoredWidth = num2;
			}
			return;
		}
		if (_anchoredTo != null)
		{
			_box.SetAnchor((Transform)null);
			_anchoredTo = null;
		}
		_anchoredWidth = num2;
		_box.width = num2;
		_box.height = 34;
		UIRoot uIRoot = NGUITools.FindInParents<UIRoot>(_barGo);
		float num4 = ((uIRoot != null) ? ((float)uIRoot.activeHeight) : 720f);
		_box.transform.localPosition = new Vector3(0f, num4 * 0.5f - 4f - 17f, 0f);
	}

	private static UIWidget ExistingRootAnchor()
	{
		try
		{
			if (!Singleton<UIManager>.HasInstance() || !Singleton<UIRootAnchor>.HasInstance())
			{
				return null;
			}
			return UIRootAnchor.GetRootAnchor(UIBase.AnchorType.Base);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static int Place(UIWidget widget, int x, int width)
	{
		widget.transform.localPosition = new Vector3(x, 0f, 0f);
		return x + width;
	}

	private static int LabelWidth(UILabel label)
	{
		if (label.bitmapFont == null && label.trueTypeFont == null)
		{
			return 0;
		}
		return Mathf.CeilToInt(label.printedSize.x);
	}
}

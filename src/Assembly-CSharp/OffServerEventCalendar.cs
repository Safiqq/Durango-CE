using System;
using System.Collections.Generic;
using System.Globalization;
using BestHTTP;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OffServerEventCalendar : MonoBehaviour
{
	private sealed class CachedSnapshot
	{
		public JObject Data;

		public float ReceivedAt;
	}

	private sealed class Notice
	{
		public string Title;

		public string Text;

		public string Icon;

		public Entry Entry;
	}

	private sealed class Entry
	{
		public string Id;

		public string Name;

		public string Icon;

		public string Place;

		public string Description;

		public double Start;

		public double End;

		public bool Manual;

		public bool AllDay;

		public string[] Guide;
	}

	private sealed class WidthItem
	{
		public Transform Target;

		public Vector3 Position;

		public UIWidget Widget;

		public SelectableButton Button;

		public int Width;

		public int Height;

		public bool Stretch;
	}

	private const int W = 1160;

	private const int H = 810;

	private static OffServerEventCalendar _instance;

	private static bool _pendingLogin;

	private static readonly Dictionary<string, CachedSnapshot> Cache = new Dictionary<string, CachedSnapshot>();

	private static HTTPRequest _prefetch;

	private static string _prefetchUrl = "";

	private static readonly HashSet<string> Announced = new HashSet<string>();

	public static int LoginOpens;

	public static string TestSource = "";

	private readonly Queue<Notice> _notices = new Queue<Notice>();

	private readonly Dictionary<string, float> _recentNotices = new Dictionary<string, float>();

	private Notice _currentNotice;

	private readonly List<Entry> _events = new List<Entry>();

	private readonly List<Entry> _visible = new List<Entry>();

	private readonly Dictionary<string, SelectableButton> _buttons = new Dictionary<string, SelectableButton>();

	private readonly List<UILabel> _dayLabels = new List<UILabel>();

	private readonly List<UILabel> _dayCounts = new List<UILabel>();

	private readonly List<UISprite[]> _dayIcons = new List<UISprite[]>();

	private readonly List<UILabel> _dayOverflow = new List<UILabel>();

	private readonly List<UISprite> _dayBackgrounds = new List<UISprite>();

	private readonly Dictionary<string, Action> _cellClicks = new Dictionary<string, Action>();

	private readonly List<GameObject> _rows = new List<GameObject>();

	private readonly List<UILabel> _rowTexts = new List<UILabel>();

	private readonly List<UISprite> _rowIcons = new List<UISprite>();

	private GameObject _calendar;

	private GameObject _notice;

	private UIWidget _host;

	private readonly Dictionary<Transform, WidthItem> _widthItems = new Dictionary<Transform, WidthItem>();

	private float _calendarWidthRatio = 1f;

	private SelectableButton _buttonTemplate;

	private UILabel _timezone;

	private UILabel _resetTime;

	private UILabel _fontTemplate;

	private UILabel _range;

	private UILabel _dayTitle;

	private UILabel _summary;

	private UILabel _detailTitle;

	private UILabel _detailTime;

	private UILabel _detailPlace;

	private UILabel _detailText;

	private UILabel _detailState;

	private UILabel _noticeTitle;

	private UILabel _noticeText;

	private UISprite _detailIcon;

	private UISprite _noticeIcon;

	private UISprite _detailInfo;

	private UISprite _noticeProgressLeft;

	private UISprite _noticeProgressRight;

	private UIPanel _noticePanel;

	private Entry _selected;

	private string _viewSource = "";

	private string _dailyResetText = "ร\u0e35เซ\u0e47ตล\u0e34ม\u0e34ต: รอข\u0e49อม\u0e39ลเซ\u0e34ร\u0e4cฟเวอร\u0e4c";

	private string _status = "กำล\u0e31งอ\u0e48านข\u0e49อม\u0e39ลอ\u0e35เวนต\u0e4c";

	private double _from;

	private double _until;

	private double _day;

	private double _offset = 7.0;

	private double _clockDelta;

	private double _lastNow;

	private float _pollAt;

	private float _drawAt;

	private float _noticeUntil;

	private float _loginAt;

	private int _page;

	private bool _built;

	private bool _hasSnapshot;

	private bool _loggedIn;

	private static readonly Color Gold = new Color32(byte.MaxValue, 216, 91, byte.MaxValue);

	private static readonly Color Grey = new Color32(205, 205, 195, byte.MaxValue);

	public static bool Prefetching => _prefetch != null;

	public static bool HasCachedData => Cache.ContainsKey(SourceUrl());

	public static bool IsOpen
	{
		get
		{
			if (_instance != null && _instance._calendar != null)
			{
				return _instance._calendar.activeSelf;
			}
			return false;
		}
	}

	public static string State
	{
		get
		{
			if (!(_instance == null))
			{
				return _instance._status;
			}
			return "not ready";
		}
	}

	public static int EventCount
	{
		get
		{
			if (!(_instance == null))
			{
				return _instance._events.Count;
			}
			return 0;
		}
	}

	public static int PendingNotices
	{
		get
		{
			if (!(_instance == null))
			{
				return _instance._notices.Count;
			}
			return 0;
		}
	}

	public static bool NoticeVisible
	{
		get
		{
			if (_instance != null && _instance._notice != null)
			{
				return _instance._notice.activeInHierarchy;
			}
			return false;
		}
	}

	public static string NoticeTitle
	{
		get
		{
			if (!(_instance == null) && _instance._currentNotice != null)
			{
				return _instance._currentNotice.Title;
			}
			return "";
		}
	}

	public static float NoticeProgress
	{
		get
		{
			if (NoticeVisible)
			{
				return Mathf.Clamp01((12f - (_instance._noticeUntil - Time.realtimeSinceStartup)) / 11f);
			}
			return 0f;
		}
	}

	public static float NoticeAlpha
	{
		get
		{
			if (NoticeVisible && !(_instance._noticePanel == null))
			{
				return _instance._noticePanel.alpha;
			}
			return 0f;
		}
	}

	private bool EventsVisible
	{
		get
		{
			EventGroup eventGroup = UIManager.FindScript<EventGroup>();
			if (eventGroup != null)
			{
				return eventGroup.IsOpened;
			}
			return false;
		}
	}

	private double Now
	{
		get
		{
			if (!_hasSnapshot && Connections.Frontend != null && GameManager.IsReady)
			{
				return Connections.Frontend.GetPredictedServerTime();
			}
			return (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _clockDelta;
		}
	}

	public static void Authenticated(bool reconnect, bool emigrating)
	{
		if (OffServerLink.Active && !reconnect && !emigrating)
		{
			_pendingLogin = true;
		}
		Prepare();
	}

	public static void Install(GameObject owner)
	{
		if (_instance == null)
		{
			_instance = owner.AddComponent<OffServerEventCalendar>();
		}
		_instance.UseCachedSnapshot();
	}

	public static bool Open()
	{
		if (_instance != null)
		{
			return _instance.Show();
		}
		return false;
	}

	public static void Close()
	{
		EventGroup eventGroup = UIManager.FindScript<EventGroup>();
		if (eventGroup != null)
		{
			eventGroup.Close();
		}
		HideEmbedded();
	}

	public static void Fetch()
	{
		if (_instance != null)
		{
			_instance.Load();
		}
		else
		{
			Prepare(force: true);
		}
	}

	public static bool Click(string key)
	{
		if (_instance != null && IsOpen && _instance._cellClicks.TryGetValue(key, out var value))
		{
			value();
			return true;
		}
		if (_instance == null || !_instance._buttons.TryGetValue(key, out var value2) || value2 == null || !value2.gameObject.activeInHierarchy || value2.Disabled)
		{
			return false;
		}
		if (value2.Clicked != null)
		{
			value2.Clicked();
		}
		return true;
	}

	public static bool Notify(AlarmNotify msg)
	{
		if (!OffServerLink.Active || _instance == null)
		{
			return false;
		}
		bool num = msg.Uri == "ui://Event?calendar=1";
		bool flag = string.IsNullOrEmpty(msg.Uri) && ((msg.Icon == "icon_mainhud_shop" && IsEventText(msg.Text)) || IsWarText(msg.Text));
		if (!num && !flag)
		{
			return false;
		}
		return _instance.Toast(msg.Text, msg.Icon, null);
	}

	public static bool NotifyAnnouncement(string text)
	{
		if (!OffServerLink.Active || _instance == null || (!IsWarText(text) && !IsEventText(text)))
		{
			return false;
		}
		return _instance.Toast(text, IsWarText(text) ? "Icon_battle_emblem" : "icon_mainhud_shop", null);
	}

	private static bool IsWarText(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			if (!text.Contains("วอร\u0e4cฐานแคลน") || (!text.Contains("เร\u0e34\u0e48ม") && !text.Contains("จบแล\u0e49ว")))
			{
				return text.Contains("สงครามเร\u0e34\u0e48มข\u0e36\u0e49นแล\u0e49ว");
			}
			return true;
		}
		return false;
	}

	private static bool IsEventText(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		string text2 = text.ToLowerInvariant();
		if (!text.Contains("อ\u0e35เวนต\u0e4c") || (!text.Contains("เร\u0e34\u0e48ม") && !text.Contains("จบ")))
		{
			if (text2.Contains("event"))
			{
				if (!text2.Contains("start") && !text2.Contains("live"))
				{
					return text2.Contains("ended");
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public static bool TestNotification()
	{
		if (_instance != null)
		{
			return _instance.Toast("ทดสอบแจ\u0e49งเต\u0e37อนอ\u0e35เวนต\u0e4c · DEV เท\u0e48าน\u0e31\u0e49น", "Icon_battle_emblem", null);
		}
		return false;
	}

	private DateTime Local(double unix)
	{
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix + _offset * 3600.0);
	}

	private string TimeRange(Entry e)
	{
		if (!e.AllDay)
		{
			if (!e.Manual)
			{
				return Local(e.Start).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) + " – " + Local(e.End).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
			}
			return "เป\u0e34ดอย\u0e39\u0e48 · ไม\u0e48กำหนดเวลาป\u0e34ด";
		}
		return "เป\u0e34ดตลอดว\u0e31น";
	}

	private string StatusOf(Entry e)
	{
		if (!e.AllDay)
		{
			if (!(Now >= e.End))
			{
				if (!(Now >= e.Start))
				{
					return "ย\u0e31งไม\u0e48เร\u0e34\u0e48ม";
				}
				return "กำล\u0e31งดำเน\u0e34น";
			}
			return "จบแล\u0e49ว";
		}
		return "เป\u0e34ดตลอดว\u0e31น";
	}

	private static bool Blocked()
	{
		if (!UIManager.IsLoadingCurtain && !UIManager.MessageBox.IsShow && !OffServerCareerUI.BlocksOtherMenus)
		{
			return OffServerTradeUI.BlocksOtherMenus;
		}
		return true;
	}

	private void Update()
	{
		if (!GameManager.IsMainScene || !GameManager.IsReady || GameManager.IsSceneClosing)
		{
			return;
		}
		if (!_loggedIn)
		{
			_loggedIn = true;
			_loginAt = Time.realtimeSinceStartup + 2f;
		}
		if (!_built && !Build())
		{
			return;
		}
		Fit();
		if (_pendingLogin && Time.realtimeSinceStartup >= _loginAt && !Blocked() && Show())
		{
			_pendingLogin = false;
			LoginOpens++;
		}
		if (Time.realtimeSinceStartup >= _pollAt)
		{
			Load();
		}
		if (Time.realtimeSinceStartup >= _drawAt)
		{
			_drawAt = Time.realtimeSinceStartup + 1f;
			if (IsOpen)
			{
				Redraw();
			}
			double now = Now;
			if (_hasSnapshot && _lastNow > 0.0)
			{
				foreach (Entry @event in _events)
				{
					if ((@event.Id.StartsWith("war:") || @event.Id.StartsWith("season:")) && !@event.Manual && @event.Start > _lastNow && @event.Start <= now && @event.End > now && now - @event.Start < 90.0 && Announced.Add(@event.Id.StartsWith("war:") ? ("war:" + @event.Start) : @event.Id))
					{
						Toast(@event.Name + "เร\u0e34\u0e48มข\u0e36\u0e49นแล\u0e49ว", @event.Icon, @event);
					}
				}
			}
			_lastNow = now;
		}
		UpdateNotices();
		if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
		{
			Close();
		}
	}

	private bool Show()
	{
		if (!GameManager.IsReady || Blocked() || (!_built && !Build()))
		{
			return false;
		}
		EventGroup eventGroup = UIManager.FindScript<EventGroup>();
		if (eventGroup != null)
		{
			return eventGroup.OpenEventCalendar();
		}
		return false;
	}

	public static void ShowEmbedded(UIWidget host)
	{
		if (!(_instance == null) && (_instance._built || _instance.Build()))
		{
			OffServerEventCalendar instance = _instance;
			instance.UseCachedSnapshot();
			instance._host = host;
			instance._calendar.SetActive(value: true);
			if (instance._day == 0.0)
			{
				instance._day = Math.Floor((instance.Now + instance._offset * 3600.0) / 86400.0) * 86400.0 - instance._offset * 3600.0;
			}
			instance.Fit();
			instance.Redraw();
			UnityEngine.Debug.Log("[EventCalendar] opened entries=" + instance._events.Count + " cached=" + HasCachedData);
			if (!instance._hasSnapshot)
			{
				instance.Load();
			}
		}
	}

	public static void HideEmbedded()
	{
		if (_instance != null && _instance._calendar != null)
		{
			_instance._calendar.SetActive(value: false);
		}
	}

	private static string SourceUrl()
	{
		if (TestSource.Length > 0)
		{
			return TestSource;
		}
		string text = (string.IsNullOrEmpty(GameManager.GatewayUrl) ? OffServerLink.Gateway : GameManager.GatewayUrl);
		if (!string.IsNullOrEmpty(text))
		{
			return text.TrimEnd('/') + "/events/calendar";
		}
		return "";
	}

	public static void Prepare(bool force = false)
	{
		if (!OffServerLink.Active)
		{
			return;
		}
		string url = SourceUrl();
		if (!Uri.TryCreate(url, UriKind.Absolute, out var result) || (result.Scheme != "http" && result.Scheme != "https") || (!force && Cache.TryGetValue(url, out var value) && Time.realtimeSinceStartup - value.ReceivedAt < 30f))
		{
			return;
		}
		if (_prefetch != null)
		{
			bool flag = _prefetch.State == HTTPRequestStates.Initial || _prefetch.State == HTTPRequestStates.Queued || _prefetch.State == HTTPRequestStates.Processing;
			if (_prefetchUrl == url && flag)
			{
				return;
			}
			HTTPRequest prefetch = _prefetch;
			_prefetch = null;
			if (flag)
			{
				prefetch.Abort();
			}
		}
		_prefetchUrl = url;
		_prefetch = new HTTPRequest(result, HTTPMethods.Get, delegate(HTTPRequest req, HTTPResponse resp)
		{
			if (_prefetch == req && !(_prefetchUrl != url))
			{
				_prefetch = null;
				try
				{
					if (resp == null || resp.StatusCode != 200)
					{
						throw new InvalidOperationException((resp != null && resp.StatusCode == 404) ? "เซ\u0e34ร\u0e4cฟเวอร\u0e4cย\u0e31งไม\u0e48รองร\u0e31บปฏ\u0e34ท\u0e34นอ\u0e35เวนต\u0e4c" : "อ\u0e48านข\u0e49อม\u0e39ลอ\u0e35เวนต\u0e4cไม\u0e48ได\u0e49 · จะลองใหม\u0e48อ\u0e31ตโนม\u0e31ต\u0e34");
					}
					JObject jObject = JObject.Parse(resp.DataAsText);
					if (!B(jObject, "ok") || !(jObject["events"] is JArray))
					{
						throw new InvalidOperationException("ข\u0e49อม\u0e39ลอ\u0e35เวนต\u0e4cย\u0e31งไม\u0e48พร\u0e49อม");
					}
					CachedSnapshot cachedSnapshot = new CachedSnapshot
					{
						Data = jObject,
						ReceivedAt = Time.realtimeSinceStartup
					};
					if (N(jObject, "now") <= 0.0 || N(jObject, "until") <= N(jObject, "from") || Math.Abs(N(jObject, "timezone")) > 14.0)
					{
						throw new InvalidOperationException("ข\u0e49อม\u0e39ลปฏ\u0e34ท\u0e34นไม\u0e48ถ\u0e39กต\u0e49อง");
					}
					foreach (JToken item in (IEnumerable<JToken>)(JArray)jObject["events"])
					{
						if (!(item is JObject o) || S(o, "id").Length == 0 || N(o, "start") <= 0.0 || N(o, "end") <= N(o, "start"))
						{
							throw new InvalidOperationException("ข\u0e49อม\u0e39ลเวลาอ\u0e35เวนต\u0e4cไม\u0e48ถ\u0e39กต\u0e49อง");
						}
					}
					if (_instance != null && SourceUrl() == url)
					{
						_instance.ApplySnapshot(jObject, cachedSnapshot.ReceivedAt, cached: false);
					}
					Cache[url] = cachedSnapshot;
					UnityEngine.Debug.Log("[EventCalendar] prefetched " + ((JArray)jObject["events"]).Count + " entries scene=" + SceneManager.GetActiveScene().name);
				}
				catch (Exception ex)
				{
					if (_instance != null && SourceUrl() == url)
					{
						_instance._status = ex.Message + (_instance._hasSnapshot ? " · แสดงข\u0e49อม\u0e39ลล\u0e48าส\u0e38ดท\u0e35\u0e48อ\u0e48านได\u0e49" : "");
					}
				}
				if (_instance != null && _instance._built && IsOpen)
				{
					_instance.Redraw();
				}
			}
		});
		_prefetch.ConnectTimeout = TimeSpan.FromSeconds(5.0);
		_prefetch.Timeout = TimeSpan.FromSeconds(15.0);
		_prefetch.DisableCache = true;
		_prefetch.AddHeader("X-OffServer-Client", OffServerLink.ClientVersion);
		_prefetch.Send();
	}

	private void UseCachedSnapshot()
	{
		string text = SourceUrl();
		if (_viewSource != text)
		{
			_events.Clear();
			_hasSnapshot = false;
			_selected = null;
			_dailyResetText = "ร\u0e35เซ\u0e47ตล\u0e34ม\u0e34ต: รอข\u0e49อม\u0e39ลเซ\u0e34ร\u0e4cฟเวอร\u0e4c";
			_viewSource = text;
		}
		if (!_hasSnapshot && Cache.TryGetValue(text, out var value) && Time.realtimeSinceStartup - value.ReceivedAt < 300f)
		{
			try
			{
				ApplySnapshot(value.Data, value.ReceivedAt, cached: true);
			}
			catch
			{
				Cache.Remove(text);
			}
		}
	}

	private void Load()
	{
		_pollAt = Time.realtimeSinceStartup + 30f;
		UseCachedSnapshot();
		Prepare(force: true);
	}

	private void ApplySnapshot(JObject data, float receivedAt, bool cached)
	{
		if (!B(data, "ok") || !(data["events"] is JArray))
		{
			throw new InvalidOperationException("ข\u0e49อม\u0e39ลอ\u0e35เวนต\u0e4cย\u0e31งไม\u0e48พร\u0e49อม");
		}
		List<Entry> list = new List<Entry>();
		foreach (JObject item in (IEnumerable<JToken>)(JArray)data["events"])
		{
			double num = N(item, "start");
			double num2 = N(item, "end");
			if (num <= 0.0 || num2 <= num || S(item, "id").Length == 0)
			{
				throw new InvalidOperationException("ข\u0e49อม\u0e39ลเวลาอ\u0e35เวนต\u0e4cไม\u0e48ถ\u0e39กต\u0e49อง");
			}
			list.Add(new Entry
			{
				Id = S(item, "id"),
				Name = S(item, "name"),
				Icon = S(item, "icon"),
				Place = S(item, "place"),
				Description = S(item, "description"),
				Start = num,
				End = num2,
				Manual = B(item, "manual"),
				AllDay = B(item, "allDay"),
				Guide = ReadGuide(item)
			});
		}
		double num3 = N(data, "now");
		double num4 = N(data, "from");
		double num5 = N(data, "until");
		double num6 = N(data, "timezone");
		if (num3 <= 0.0 || num5 <= num4 || Math.Abs(num6) > 14.0)
		{
			throw new InvalidOperationException("ข\u0e49อม\u0e39ลปฏ\u0e34ท\u0e34นไม\u0e48ถ\u0e39กต\u0e49อง");
		}
		_clockDelta = num3 + (double)Math.Max(0f, Time.realtimeSinceStartup - receivedAt) - (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		_offset = num6;
		_from = num4;
		_until = num5;
		_dailyResetText = "ร\u0e35เซ\u0e47ตล\u0e34ม\u0e34ต: รอข\u0e49อม\u0e39ลเซ\u0e34ร\u0e4cฟเวอร\u0e4c";
		if (data["dailyReset"] is JObject jObject2 && jObject2["hour"] != null && jObject2["timezone"] != null)
		{
			double num7 = N(jObject2, "hour");
			double num8 = N(jObject2, "timezone");
			if (num7 >= 0.0 && num7 < 24.0 && Math.Abs(num8) <= 14.0)
			{
				int num9 = (int)Math.Round((num7 - num8 + _offset) * 60.0);
				num9 = (num9 % 1440 + 1440) % 1440;
				_dailyResetText = "ร\u0e35เซ\u0e47ตล\u0e34ม\u0e34ตรายว\u0e31น " + (num9 / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (num9 % 60).ToString("00", CultureInfo.InvariantCulture) + " น.";
			}
		}
		if (_selected != null)
		{
			_selected = list.Find((Entry e) => e.Id == _selected.Id);
		}
		_events.Clear();
		_events.AddRange(list);
		_hasSnapshot = true;
		if (_day < num4 || _day >= num5)
		{
			_day = Math.Floor((Now + _offset * 3600.0) / 86400.0) * 86400.0 - _offset * 3600.0;
		}
		_status = ((TestSource.Length <= 0) ? (cached ? "ข\u0e49อม\u0e39ลล\u0e48าส\u0e38ดท\u0e35\u0e48ม\u0e35 · อ\u0e31ปเดต " : "ข\u0e49อม\u0e39ลจากเซ\u0e34ร\u0e4cฟเวอร\u0e4c · อ\u0e31ปเดต ") : ((S(data, "testSource") == "live-dev") ? "ทดสอบ · ข\u0e49อม\u0e39ลจร\u0e34ง DEV · อ\u0e31ปเดต " : (cached ? "ทดสอบ · ข\u0e49อม\u0e39ลล\u0e48าส\u0e38ดจากสำเนา · อ\u0e31ปเดต " : "ทดสอบ · สำเนาตารางจร\u0e34ง · อ\u0e31ปเดต "))) + Local(num3).ToString("HH:mm:ss");
		_viewSource = SourceUrl();
	}

	private static string[] ReadGuide(JObject row)
	{
		List<string> list = new List<string>();
		if (row["guide"] is JArray jArray)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item.Type == JTokenType.String && !string.IsNullOrEmpty((string)item))
				{
					list.Add((string)item);
				}
			}
		}
		return list.ToArray();
	}

	private void ShowInfo(Entry entry, int page = 0)
	{
		if (this == null || entry == null || !IsOpen || UIManager.MessageBox == null || UIManager.MessageBox.IsShow)
		{
			return;
		}
		string[] pages = ((entry.Guide != null && entry.Guide.Length != 0) ? entry.Guide : new string[1] { entry.Description + "\n\nเซ\u0e34ร\u0e4cฟเวอร\u0e4cย\u0e31งไม\u0e48ได\u0e49ระบ\u0e38ว\u0e34ธ\u0e35เข\u0e49าร\u0e48วมเพ\u0e34\u0e48มเต\u0e34ม" });
		page = Math.Max(0, Math.Min(page, pages.Length - 1));
		string mainText = entry.Name + " · ว\u0e34ธ\u0e35เข\u0e49าร\u0e48วม " + (page + 1) + "/" + pages.Length;
		UIManager.MessageBox.Show(mainText, pages[page], delegate(int choice)
		{
			if (!(this == null) && IsOpen)
			{
				if (choice == 0 && page > 0)
				{
					ShowInfo(entry, page - 1);
				}
				else if (choice == 1 && page + 1 < pages.Length)
				{
					ShowInfo(entry, page + 1);
				}
			}
		}, new MessageBox.Button("ก\u0e48อนหน\u0e49า", PresetButton.Style.Solid, null, page == 0), new MessageBox.Button("ถ\u0e31ดไป", PresetButton.Style.Solid, null, page + 1 >= pages.Length), new MessageBox.Button("ป\u0e34ด"));
	}

	private UISprite InfoButton(GameObject parent, string key, int x, int y, Action action)
	{
		UISprite uISprite = Sprite(parent, "icon_information", x, y, 32, 32, 25, UIBasicSprite.Type.Simple);
		uISprite.gameObject.name = "Calendar_" + key;
		NGUITools.AddWidgetCollider(uISprite.gameObject);
		UIEventListener.Get(uISprite.gameObject).onClick = delegate
		{
			action();
		};
		_cellClicks[key] = action;
		return uISprite;
	}

	private static double N(JObject o, string k)
	{
		try
		{
			return Convert.ToDouble(((JValue)o[k]).Value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0.0;
		}
	}

	private static string S(JObject o, string k)
	{
		if (o[k] is JValue { Value: not null } jValue)
		{
			return Convert.ToString(jValue.Value, CultureInfo.InvariantCulture);
		}
		return "";
	}

	private static bool B(JObject o, string k)
	{
		if (o[k] is JValue jValue && jValue.Value is bool)
		{
			return (bool)jValue.Value;
		}
		return false;
	}

	private bool Toast(string text, string icon, Entry entry)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = text.Split('\n')[0].Trim().Replace("\ud83c\udf83", "").Replace("\ud83c\udf89", "")
			.Replace("⚔", "")
			.Replace("\ud83c\udfc1", "")
			.Trim();
		bool flag = IsWarText(text) || (entry?.Id.StartsWith("war:") ?? false);
		bool flag2 = text.Contains("จบแล\u0e49ว") || text.ToLowerInvariant().Contains("ended");
		bool flag3 = text.Contains("จะเร\u0e34\u0e48ม");
		if (entry == null)
		{
			foreach (Entry @event in _events)
			{
				if (!string.IsNullOrEmpty(@event.Name) && text.IndexOf(@event.Name, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					entry = @event;
					break;
				}
			}
			if (entry == null && flag)
			{
				entry = _events.Find((Entry e) => e.Id.StartsWith("war:") && e.End > Now);
			}
		}
		string text3 = (flag3 ? text2 : ((!flag) ? ((entry != null) ? (entry.Name + (flag2 ? "จบแล\u0e49ว" : "เร\u0e34\u0e48มแล\u0e49ว")) : text2) : (flag2 ? "สงครามแคลนจบแล\u0e49ว" : "สงครามเร\u0e34\u0e48มข\u0e36\u0e49นแล\u0e49ว")));
		int num = text3.IndexOf(" — ", StringComparison.Ordinal);
		if (num > 0)
		{
			text3 = text3.Substring(0, num);
		}
		if (text.Contains("ทดสอบ") && !text3.Contains("ทดสอบ"))
		{
			text3 = "ทดสอบ · " + text3;
		}
		if (text3.Length > 60)
		{
			text3 = text3.Substring(0, 59) + "…";
		}
		string text4 = ((entry != null) ? (entry.Place + " · " + TimeRange(entry)) : text2);
		if (text4 == text3)
		{
			text4 = "แจ\u0e49งผ\u0e39\u0e49เล\u0e48นท\u0e38กเกาะ · กดด\u0e39อ\u0e35เวนต\u0e4cเพ\u0e37\u0e48อเป\u0e34ดปฏ\u0e34ท\u0e34น";
		}
		if (text4.Length > 100)
		{
			text4 = text4.Substring(0, 99) + "…";
		}
		string text5 = (flag3 ? "soon" : (flag2 ? "end" : "start"));
		string key = (flag ? ("war:" + text5) : ((entry != null) ? (entry.Id + ":" + text5) : text3));
		if (_recentNotices.TryGetValue(key, out var value) && Time.realtimeSinceStartup - value < 15f)
		{
			return true;
		}
		if (_recentNotices.Count > 64)
		{
			_recentNotices.Clear();
		}
		_recentNotices[key] = Time.realtimeSinceStartup;
		if (_notices.Count >= 16)
		{
			_notices.Dequeue();
		}
		_notices.Enqueue(new Notice
		{
			Title = text3,
			Text = text4,
			Icon = icon,
			Entry = entry
		});
		UnityEngine.Debug.Log("[EventNotice] queued " + text3 + " pending=" + _notices.Count);
		return true;
	}

	private void UpdateNotices()
	{
		if (EventsVisible || Blocked())
		{
			if (_notice.activeSelf)
			{
				_notice.SetActive(value: false);
			}
			return;
		}
		if (_currentNotice != null && _notice.activeSelf && Time.realtimeSinceStartup >= _noticeUntil)
		{
			DismissNotice();
		}
		if (_currentNotice == null && _notices.Count > 0)
		{
			_currentNotice = _notices.Dequeue();
		}
		if (_currentNotice != null)
		{
			if (_notice.activeSelf)
			{
				float num = _noticeUntil - Time.realtimeSinceStartup;
				int num2 = Math.Max(1, Mathf.RoundToInt(330f * Mathf.Clamp01((12f - num) / 11f)));
				_noticeProgressLeft.width = num2;
				_noticeProgressLeft.transform.localPosition = At(580 - num2, 232, num2, 2);
				_noticeProgressRight.width = num2;
				_noticeProgressRight.transform.localPosition = At(580, 232, num2, 2);
				_noticePanel.alpha = ((num >= 1f) ? 1f : Mathf.SmoothStep(0f, 1f, num));
			}
			else
			{
				_noticeTitle.text = _currentNotice.Title;
				_noticeText.text = _currentNotice.Text;
				SetIcon(_noticeIcon, _currentNotice.Icon);
				_noticeUntil = Time.realtimeSinceStartup + 12f;
				_noticeProgressLeft.width = 1;
				_noticeProgressLeft.transform.localPosition = At(579, 232, 1, 2);
				_noticeProgressRight.width = 1;
				_noticeProgressRight.transform.localPosition = At(580, 232, 1, 2);
				_noticePanel.alpha = 1f;
				_notice.SetActive(value: true);
				UISound.PlayOpenGroup(UISound.GroupType.Event);
				UnityEngine.Debug.Log("[EventNotice] shown " + _currentNotice.Title);
			}
		}
	}

	private void DismissNotice()
	{
		_notice.SetActive(value: false);
		_currentNotice = null;
	}

	private void ViewNotice()
	{
		Notice notice = _currentNotice;
		if (notice == null || !Show())
		{
			return;
		}
		if (notice.Entry != null)
		{
			Entry entry = _events.Find((Entry e) => e.Id == notice.Entry.Id) ?? notice.Entry;
			double num = ((entry.Start <= Now && Now < entry.End) ? Now : entry.Start);
			_day = Math.Max(_from, Math.Min(_until - 86400.0, Math.Floor((num + _offset * 3600.0) / 86400.0) * 86400.0 - _offset * 3600.0));
			_selected = entry;
			Redraw();
		}
		DismissNotice();
	}

	private void SetIcon(UISprite target, string icon)
	{
		if (!ResourceSingleton<UISpriteManager>.Instance().TryGet(icon, out var _, out var _))
		{
			icon = "icon_mainhud_shop";
		}
		target.spriteName = icon;
	}

	private bool Build()
	{
		if (_built)
		{
			return true;
		}
		if (!Singleton<UIManager>.HasInstance() || UIManager.Popup == null)
		{
			return false;
		}
		ShopCoinAcceptPopup shopCoinAcceptPopup = UIManager.Popup.Tooltip<ShopCoinAcceptPopup>();
		if (shopCoinAcceptPopup == null)
		{
			return false;
		}
		SelectableButton[] componentsInChildren = shopCoinAcceptPopup.GetComponentsInChildren<SelectableButton>(includeInactive: true);
		foreach (SelectableButton selectableButton in componentsInChildren)
		{
			if (selectableButton.name == "Cancel")
			{
				_buttonTemplate = selectableButton;
			}
		}
		UILabel[] componentsInChildren2 = shopCoinAcceptPopup.GetComponentsInChildren<UILabel>(includeInactive: true);
		foreach (UILabel uILabel in componentsInChildren2)
		{
			if (_fontTemplate == null && !(uILabel is UISpriteLabel) && (uILabel.trueTypeFont != null || uILabel.bitmapFont != null))
			{
				_fontTemplate = uILabel;
			}
		}
		if (_buttonTemplate == null || _fontTemplate == null)
		{
			return false;
		}
		_calendar = base.gameObject.AddChild();
		_calendar.name = "EventCalendar";
		_calendar.AddComponent<UIPanel>().depth = 5900;
		Box(_calendar, 0, 0, 1160, 810, new Color(0f, 0f, 0f, 0.42f), 0);
		Sprite(_calendar, "bg_box_grunge", 0, 0, 1160, 810, 1, UIBasicSprite.Type.Simple).color = new Color(0f, 0f, 0f, 0.392f);
		Box(_calendar, 0, 0, 1160, 64, new Color(0f, 0f, 0f, 0.35f), 2);
		_timezone = Label(_calendar, "", 24, 6, 380, 26, 18, Color.white);
		_resetTime = Label(_calendar, "", 24, 34, 380, 24, 16, Grey);
		_range = Label(_calendar, "", 440, 10, 370, 48, 28, Gold);
		Button(_calendar, "today", "ว\u0e31นน\u0e35\u0e49", 1008, 12, 126, 44, delegate
		{
			_day = Math.Floor((Now + _offset * 3600.0) / 86400.0) * 86400.0 - _offset * 3600.0;
			_page = 0;
			_selected = null;
			Redraw();
		});
		string[] array = new string[7] { "จ\u0e31นทร\u0e4c", "อ\u0e31งคาร", "พ\u0e38ธ", "พฤห\u0e31สบด\u0e35", "ศ\u0e38กร\u0e4c", "เสาร\u0e4c", "อาท\u0e34ตย\u0e4c" };
		for (int num = 0; num < 7; num++)
		{
			Label(_calendar, array[num], 24 + num * 100, 76, 94, 30, 18, Color.white).alignment = NGUIText.Alignment.Center;
		}
		for (int num2 = 0; num2 < 42; num2++)
		{
			int index = num2;
			int num3 = 24 + num2 % 7 * 100;
			int num4 = 112 + num2 / 7 * 100;
			Action selectDay = delegate
			{
				double num8 = GridStart() + (double)(index * 86400);
				if (Local(num8).Month == Local(Now).Month && Local(num8).Year == Local(Now).Year && (!_hasSnapshot || (!(num8 < _from) && !(num8 >= _until))))
				{
					_day = num8;
					_page = 0;
					_selected = null;
					Redraw();
				}
			};
			UISprite uISprite = Box(_calendar, num3, num4, 94, 92, new Color(0f, 0f, 0f, 0.588f), 3, collider: true);
			_dayBackgrounds.Add(uISprite);
			UIEventListener.Get(uISprite.gameObject).onClick = delegate
			{
				selectDay();
			};
			_cellClicks["day" + num2] = selectDay;
			UILabel uILabel2 = Label(_calendar, "", num3 + 8, num4 + 4, 78, 23, 20, Color.white);
			uILabel2.alignment = NGUIText.Alignment.Right;
			_dayLabels.Add(uILabel2);
			UISprite[] array2 = new UISprite[3];
			for (int num5 = 0; num5 < 3; num5++)
			{
				array2[num5] = Sprite(_calendar, "icon_mainhud_shop", num3 + 5 + num5 * 24, num4 + 35, 22, 22, 6, UIBasicSprite.Type.Simple);
			}
			_dayIcons.Add(array2);
			_dayOverflow.Add(Label(_calendar, "", num3 + 74, num4 + 35, 19, 24, 13, Gold));
			UILabel uILabel3 = Label(_calendar, "", num3 + 3, num4 + 73, 88, 18, 13, Grey);
			uILabel3.alignment = NGUIText.Alignment.Center;
			_dayCounts.Add(uILabel3);
		}
		Label(_calendar, "เล\u0e37อกว\u0e31นท\u0e35\u0e48เพ\u0e37\u0e48อด\u0e39เวลาและรายละเอ\u0e35ยดอ\u0e35เวนต\u0e4c", 24, 724, 700, 36, 18, Grey);
		_dayTitle = Label(_calendar, "", 766, 86, 350, 34, 23, Gold);
		for (int num6 = 0; num6 < 3; num6++)
		{
			int index2 = num6;
			int num7 = 134 + num6 * 90;
			GameObject gameObject = _calendar.AddChild();
			gameObject.name = "EventRow" + num6;
			_rows.Add(gameObject);
			Box(gameObject, 766, num7, 350, 74, new Color(1f, 1f, 1f, 0.035f), 3, collider: true);
			UIEventListener.Get(gameObject.GetComponentInChildren<UISprite>().gameObject).onClick = delegate
			{
				int num8 = _page * 3 + index2;
				if (num8 < _visible.Count)
				{
					_selected = _visible[num8];
					DrawDetail();
				}
			};
			_rowIcons.Add(Sprite(gameObject, "icon_mainhud_shop", 774, num7 + 18, 34, 34, 6, UIBasicSprite.Type.Simple));
			_rowTexts.Add(Label(gameObject, "", 818, num7 + 8, 264, 58, 18, Color.white));
			InfoButton(gameObject, "info-row" + num6, 1084, num7 + 20, delegate
			{
				int num8 = _page * 3 + index2;
				if (num8 < _visible.Count)
				{
					ShowInfo(_visible[num8]);
				}
			});
		}
		Button(_calendar, "rows-prev", "ก\u0e48อนหน\u0e49า", 766, 428, 132, 40, delegate
		{
			if (_page > 0)
			{
				_page--;
			}
			Redraw();
		});
		Button(_calendar, "rows-next", "ถ\u0e31ดไป", 978, 428, 138, 40, delegate
		{
			if ((_page + 1) * 3 < _visible.Count)
			{
				_page++;
			}
			Redraw();
		});
		_detailState = Label(_calendar, "", 766, 496, 350, 28, 17, Gold);
		_detailIcon = Sprite(_calendar, "icon_mainhud_shop", 766, 534, 42, 42, 6, UIBasicSprite.Type.Simple);
		_detailTitle = Label(_calendar, "", 820, 528, 256, 56, 24, Gold);
		_detailInfo = InfoButton(_calendar, "info", 1084, 540, delegate
		{
			ShowInfo(_selected);
		});
		_detailTime = Label(_calendar, "", 766, 598, 350, 38, 18, Color.white);
		_detailPlace = Label(_calendar, "", 766, 644, 350, 28, 18, Grey);
		_detailText = Label(_calendar, "", 766, 684, 350, 96, 16, Color.white);
		_summary = Label(_calendar, "", 24, 772, 700, 30, 16, Grey);
		_notice = base.gameObject.AddChild();
		_notice.name = "EventStartNotification";
		_noticePanel = _notice.AddComponent<UIPanel>();
		_noticePanel.depth = 5910;
		Box(_notice, 250, 130, 660, 104, new Color32(24, 24, 23, 170), 1, collider: true);
		Sprite(_notice, Surface("bg_popup_pc", "bg_tooltip_grunge"), 250, 130, 660, 104, 2, UIBasicSprite.Type.Sliced).color = new Color(1f, 1f, 1f, 0.28f);
		_noticeIcon = Sprite(_notice, "Icon_battle_emblem", 270, 158, 44, 44, 6, UIBasicSprite.Type.Simple);
		_noticeTitle = Label(_notice, "", 326, 140, 416, 32, 22, Gold);
		_noticeText = Label(_notice, "", 326, 178, 416, 40, 14, Color.white);
		Button(_notice, "notice-view", "ด\u0e39อ\u0e35เวนต\u0e4c", 756, 182, 124, 36, ViewNotice);
		Box(_notice, 250, 232, 660, 2, new Color(1f, 1f, 1f, 0.12f), 4);
		_noticeProgressLeft = Box(_notice, 579, 232, 1, 2, Gold, 5);
		_noticeProgressRight = Box(_notice, 580, 232, 1, 2, Gold, 5);
		_calendar.SetActive(value: false);
		_notice.SetActive(value: false);
		_built = true;
		return true;
	}

	private double GridStart()
	{
		DateTime dateTime = Local(_day);
		DateTime dateTime2 = new DateTime(dateTime.Year, dateTime.Month, 1);
		return (dateTime2 - new DateTime(1970, 1, 1)).TotalSeconds - _offset * 3600.0 - (double)((int)(dateTime2.DayOfWeek + 6) % 7 * 86400);
	}

	private void Redraw()
	{
		string[] array = new string[12]
		{
			"มกราคม", "ก\u0e38มภาพ\u0e31นธ\u0e4c", "ม\u0e35นาคม", "เมษายน", "พฤษภาคม", "ม\u0e34ถ\u0e38นายน", "กรกฎาคม", "ส\u0e34งหาคม", "ก\u0e31นยายน", "ต\u0e38ลาคม",
			"พฤศจ\u0e34กายน", "ธ\u0e31นวาคม"
		};
		_timezone.text = "เวลาเซ\u0e34ร\u0e4cฟเวอร\u0e4c · UTC" + ((_offset >= 0.0) ? "+" : "") + _offset.ToString("0.##", CultureInfo.InvariantCulture);
		_resetTime.text = _dailyResetText;
		DateTime dateTime = Local(_day);
		_range.text = array[dateTime.Month - 1] + " " + dateTime.Year;
		for (int i = 0; i < 42; i++)
		{
			double num = GridStart() + (double)(i * 86400);
			DateTime dateTime2 = Local(num);
			int num2 = 0;
			foreach (Entry @event in _events)
			{
				if (@event.Start < num + 86400.0 && @event.End > num)
				{
					num2++;
				}
			}
			bool flag = _hasSnapshot && num >= _from && num < _until;
			bool flag2 = dateTime2.Month == dateTime.Month && dateTime2.Year == dateTime.Year;
			_dayBackgrounds[i].gameObject.SetActive(flag2);
			_dayLabels[i].gameObject.SetActive(flag2);
			_dayCounts[i].gameObject.SetActive(flag2);
			_dayLabels[i].text = (flag2 ? dateTime2.Day.ToString() : "");
			_dayLabels[i].color = ((Math.Abs(num - _day) < 1.0) ? Gold : ((dateTime2.Month != dateTime.Month) ? Grey : Color.white));
			List<Entry> list = new List<Entry>();
			HashSet<string> hashSet = new HashSet<string>();
			foreach (Entry event2 in _events)
			{
				if (event2.Start < num + 86400.0 && event2.End > num)
				{
					string item = (event2.Id.StartsWith("war:") ? "war" : (event2.Id.StartsWith("weekend:") ? "weekend" : event2.Id));
					if (hashSet.Add(item))
					{
						list.Add(event2);
					}
				}
			}
			_dayCounts[i].text = ((flag && list.Count > 0) ? (list.Count + " ก\u0e34จกรรม") : "");
			for (int j = 0; j < 3; j++)
			{
				UISprite uISprite = _dayIcons[i][j];
				bool flag3 = flag2 && flag && j < list.Count;
				uISprite.gameObject.SetActive(flag3);
				if (flag3)
				{
					SetIcon(uISprite, list[j].Icon);
					int num3 = Math.Min(3, list.Count);
					int size = ((list.Count > 3) ? 20 : 22);
					int num4 = ((list.Count > 3) ? 22 : 25);
					int num5 = ((list.Count > 3) ? 4 : ((94 - (num3 * 22 + (num3 - 1) * 3)) / 2));
					PositionDayIcon(uISprite, 24 + i % 7 * 100 + num5 + j * num4, 112 + i / 7 * 100 + 35, size);
				}
			}
			_dayOverflow[i].gameObject.SetActive(flag2 && flag && list.Count > 3);
			_dayOverflow[i].text = ((list.Count > 3) ? ("+" + (list.Count - 3)) : "");
			_dayBackgrounds[i].color = ((Math.Abs(num - _day) < 1.0) ? new Color(0.32f, 0.28f, 0.13f, 0.85f) : new Color(0f, 0f, 0f, (dateTime2.Month == dateTime.Month) ? 0.588f : 0.22f));
		}
		_dayTitle.text = Local(_day).ToString("dd/MM/yyyy") + " · อ\u0e35เวนต\u0e4cประจำว\u0e31น";
		_visible.Clear();
		foreach (Entry event3 in _events)
		{
			if (event3.Start < _day + 86400.0 && event3.End > _day)
			{
				_visible.Add(event3);
			}
		}
		if (_page * 3 >= _visible.Count)
		{
			_page = 0;
		}
		for (int k = 0; k < 3; k++)
		{
			int num6 = _page * 3 + k;
			bool flag4 = num6 < _visible.Count;
			_rows[k].SetActive(flag4);
			if (flag4)
			{
				Entry entry = _visible[num6];
				SetIcon(_rowIcons[k], entry.Icon);
				_rowTexts[k].text = (entry.AllDay ? "ท\u0e31\u0e49งว\u0e31น" : (entry.Manual ? "เป\u0e34ดอย\u0e39\u0e48" : Local(Math.Max(entry.Start, _day)).ToString("HH:mm"))) + "  " + entry.Name + "\n[aaaaa2]" + entry.Place + " · " + StatusOf(entry) + "[-]";
			}
		}
		if (_selected == null || !_visible.Exists((Entry e) => e.Id == _selected.Id))
		{
			_selected = ((_visible.Count > 0) ? _visible[0] : null);
			foreach (Entry item2 in _visible)
			{
				if (item2.Id.StartsWith("war:"))
				{
					_selected = item2;
					break;
				}
			}
		}
		DrawDetail();
		_summary.text = _status;
	}

	private void DrawDetail()
	{
		_detailInfo.gameObject.SetActive(_selected != null);
		if (_selected == null)
		{
			_detailIcon.gameObject.SetActive(value: false);
			_detailTitle.text = (_hasSnapshot ? "ไม\u0e48ม\u0e35อ\u0e35เวนต\u0e4cตามกำหนด" : "ย\u0e31งอ\u0e48านข\u0e49อม\u0e39ลไม\u0e48ได\u0e49");
			_detailState.text = "";
			_detailTime.text = "";
			_detailPlace.text = "";
			_detailText.text = (_hasSnapshot ? "เล\u0e37อกว\u0e31นอ\u0e37\u0e48นเพ\u0e37\u0e48อด\u0e39ตารางอ\u0e35เวนต\u0e4c\nระบบจะอ\u0e31ปเดตข\u0e49อม\u0e39ลจากเซ\u0e34ร\u0e4cฟเวอร\u0e4cอ\u0e31ตโนม\u0e31ต\u0e34" : _status);
		}
		else
		{
			Entry selected = _selected;
			_detailIcon.gameObject.SetActive(value: true);
			SetIcon(_detailIcon, selected.Icon);
			_detailTitle.text = selected.Name;
			_detailState.text = StatusOf(selected);
			_detailTime.text = TimeRange(selected);
			_detailPlace.text = selected.Place;
			_detailText.text = selected.Description;
		}
	}

	private static Vector3 At(int x, int y, int w, int h)
	{
		return new Vector3(x + w / 2 - 580, 405 - y - h / 2, 0f);
	}

	private static string Surface(string pc, string fallback)
	{
		if (!ResourceSingleton<UISpriteManager>.Instance().TryGet(pc, out var _, out var _))
		{
			return fallback;
		}
		return pc;
	}

	private UISprite Sprite(GameObject parent, string name, int x, int y, int w, int h, int depth, UIBasicSprite.Type type)
	{
		UISprite uISprite = parent.AddSprite(name, depth);
		uISprite.type = type;
		uISprite.width = w;
		uISprite.height = h;
		uISprite.transform.localPosition = At(x, y, w, h);
		RememberWidth(uISprite.transform, uISprite, null, w, h, name.StartsWith("bg_", StringComparison.Ordinal));
		return uISprite;
	}

	private UISprite Box(GameObject parent, int x, int y, int w, int h, Color color, int depth, bool collider = false)
	{
		UISprite uISprite = Sprite(parent, "bg_white", x, y, w, h, depth, UIBasicSprite.Type.Simple);
		uISprite.color = color;
		if (collider)
		{
			NGUITools.AddWidgetCollider(uISprite.gameObject);
		}
		return uISprite;
	}

	private UILabel Label(GameObject parent, string text, int x, int y, int w, int h, int size, Color color)
	{
		GameObject gameObject = parent.AddChild();
		gameObject.name = "CalendarLabel";
		UILabel uILabel = gameObject.AddComponent<UILabel>();
		uILabel.bitmapFont = _fontTemplate.bitmapFont;
		if (_fontTemplate.trueTypeFont != null)
		{
			uILabel.trueTypeFont = _fontTemplate.trueTypeFont;
		}
		uILabel.effectStyle = _fontTemplate.effectStyle;
		uILabel.effectColor = _fontTemplate.effectColor;
		uILabel.pivot = UIWidget.Pivot.Left;
		uILabel.alignment = NGUIText.Alignment.Left;
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.width = w;
		uILabel.height = h;
		uILabel.fontSize = size;
		uILabel.color = color;
		uILabel.depth = 20;
		uILabel.text = text;
		uILabel.transform.localPosition = new Vector3(x - 580, 405 - y - h / 2, 0f);
		RememberWidth(gameObject.transform, uILabel, null, w, h, stretch: true);
		return uILabel;
	}

	private void Button(GameObject parent, string key, string text, int x, int y, int w, int h, Action action)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent.transform);
		gameObject.name = "Calendar_" + key;
		UIRect component = gameObject.GetComponent<UIRect>();
		if (component != null)
		{
			component.SetAnchor((Transform)null);
		}
		gameObject.transform.localPosition = At(x, y, w, h);
		gameObject.transform.localScale = Vector3.one;
		SelectableButton component2 = gameObject.GetComponent<SelectableButton>();
		UIWidget[] componentsInChildren = gameObject.GetComponentsInChildren<UIWidget>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].depth += 10;
		}
		component2.SetDimensions(w, h);
		component2.ShortcutCommand = InputCommand.None;
		component2.Text = (string.IsNullOrEmpty(text) ? " " : text);
		component2.Disabled = false;
		component2.Clicked = action;
		_buttons[key] = component2;
		RememberWidth(gameObject.transform, component2.Widget, component2, w, h, stretch: true);
		gameObject.SetActive(value: true);
	}

	private void RememberWidth(Transform target, UIWidget widget, SelectableButton button, int width, int height, bool stretch)
	{
		if (!(_calendar == null) && target.IsChildOf(_calendar.transform))
		{
			_widthItems[target] = new WidthItem
			{
				Target = target,
				Position = target.localPosition,
				Widget = widget,
				Button = button,
				Width = width,
				Height = height,
				Stretch = stretch
			};
		}
	}

	private void PositionDayIcon(UISprite icon, int x, int y, int size)
	{
		icon.width = size;
		icon.height = size;
		Vector3 vector = At(x, y, size, size);
		if (_widthItems.TryGetValue(icon.transform, out var value))
		{
			value.Position = vector;
			value.Width = size;
			value.Height = size;
		}
		vector.x *= _calendarWidthRatio;
		icon.transform.localPosition = vector;
	}

	private void FitCalendarWidth(float ratio)
	{
		ratio = Mathf.Max(0.1f, ratio);
		if (Mathf.Abs(ratio - _calendarWidthRatio) < 0.0001f)
		{
			return;
		}
		_calendarWidthRatio = ratio;
		foreach (WidthItem value in _widthItems.Values)
		{
			if (value.Target == null)
			{
				continue;
			}
			Vector3 position = value.Position;
			position.x *= ratio;
			value.Target.localPosition = position;
			if (value.Stretch)
			{
				int width = Mathf.Max(1, Mathf.RoundToInt((float)value.Width * ratio));
				if (value.Button != null)
				{
					value.Button.SetDimensions(width, value.Height);
				}
				else if (value.Widget != null)
				{
					value.Widget.width = width;
				}
				if (value.Target.GetComponent<Collider>() != null || value.Target.GetComponent<Collider2D>() != null)
				{
					NGUITools.UpdateWidgetCollider(value.Target.gameObject);
				}
			}
		}
	}

	private void Fit()
	{
		UIRoot uIRoot = Singleton<UIManager>.Instance().UIRoot;
		float num = uIRoot.activeHeight;
		float num2 = num * (float)Screen.width / Math.Max(1f, Screen.height);
		EventGroup eventGroup = UIManager.FindScript<EventGroup>();
		if (_host != null && eventGroup != null && eventGroup.IsOpened)
		{
			Vector3[] worldCorners = _host.worldCorners;
			Vector3 vector = uIRoot.transform.InverseTransformPoint(worldCorners[0]);
			Vector3 vector2 = uIRoot.transform.InverseTransformPoint(worldCorners[2]);
			float num3 = Mathf.Min((Mathf.Abs(vector2.x - vector.x) - 8f) / 1160f, (Mathf.Abs(vector2.y - vector.y) - 8f) / 810f);
			if (eventGroup.TryGetCalendarBounds(uIRoot.transform, out var bounds))
			{
				num3 = Mathf.Max(0.1f, (bounds.height - 8f) / 810f);
				FitCalendarWidth((bounds.width - 8f) / (1160f * num3));
				vector = new Vector3(bounds.xMin, bounds.yMin, 0f);
				vector2 = new Vector3(bounds.xMax, bounds.yMax, 0f);
			}
			else
			{
				FitCalendarWidth(1f);
			}
			_calendar.transform.localScale = Vector3.one * Mathf.Max(0.1f, num3);
			_calendar.transform.localPosition = (vector + vector2) * 0.5f;
		}
		else if (_calendar.activeSelf)
		{
			_calendar.SetActive(value: false);
		}
		float num4 = Mathf.Min(1f, (num2 - 40f) / 1160f);
		_notice.transform.localScale = Vector3.one * num4;
		_notice.transform.localPosition = new Vector3(0f, num / 2f - 80f - 275f * num4, 0f);
	}

	private void OnDestroy()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}
}

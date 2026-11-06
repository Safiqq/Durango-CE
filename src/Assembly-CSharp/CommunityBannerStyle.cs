using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BestHTTP;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Yaml;

public static class CommunityBannerStyle
{
	public sealed class Entry
	{
		public string TitleTh;

		public string Icon;

		public string Tag;

		public double EndAt;
	}

	public const string HttpPath = "/banners";

	public const float RefetchSec = 30f;

	public const double PromotionCountdownDays = 30.0;

	private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

	private static int _serverStyle;

	private static int _override = -1;

	private static int _serverOnlyDuel;

	private static int _overrideOnlyDuel = -1;

	private static string _signature = "0";

	private static float _fetchedAt = -1000f;

	private static bool _fetching;

	private static string _gateway;

	private static string _last = "not fetched";

	public static int Style
	{
		get
		{
			if (_override < 0)
			{
				return _serverStyle;
			}
			return _override;
		}
	}

	public static bool OnlyDuel => ((_overrideOnlyDuel >= 0) ? _overrideOnlyDuel : _serverOnlyDuel) >= 1;

	public static event Action Changed;

	public static bool Shows(PromotionLink link)
	{
		if (!OnlyDuel)
		{
			return true;
		}
		string text = link?.WebLink;
		if (!(text == "ui://PvpRanked"))
		{
			return text == "ui://PvpIsland";
		}
		return true;
	}

	public static void Fetch(bool force)
	{
		try
		{
			if (_fetching || _override >= 0 || !CommunityLink.Active)
			{
				return;
			}
			string text = CurrentGateway();
			if (string.IsNullOrEmpty(text) || (!force && Time.realtimeSinceStartup - _fetchedAt < 30f && CommunityLink.SameGateway(text, _gateway)))
			{
				return;
			}
			_fetching = true;
			_fetchedAt = Time.realtimeSinceStartup;
			_gateway = text;
			HTTPRequest hTTPRequest = new HTTPRequest(new Uri(text.TrimEnd('/') + "/banners"), HTTPMethods.Get, delegate(HTTPRequest req, HTTPResponse resp)
			{
				_fetching = false;
				try
				{
					if (resp != null && resp.IsSuccess)
					{
						Apply(resp.DataAsText, "server");
					}
					else
					{
						_last = "server " + ((resp != null) ? resp.StatusCode.ToString() : "no reply") + " → style 0";
						SetServer(0, 0, null, "0");
					}
				}
				catch (Exception ex2)
				{
					_last = "parse error " + ex2.Message;
					SetServer(0, 0, null, "0");
				}
			});
			hTTPRequest.Timeout = TimeSpan.FromSeconds(5.0);
			hTTPRequest.DisableCache = true;
			hTTPRequest.Send();
		}
		catch (Exception ex)
		{
			_fetching = false;
			_last = "fetch error " + ex.Message;
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

	public static void Apply(string json, string source)
	{
		JObject jObject = JObject.Parse(string.IsNullOrEmpty(json) ? "{}" : json);
		int style = Mathf.Clamp(Int(jObject["style"]), 0, 1);
		int onlyDuel = Mathf.Clamp(Int(jObject["only_duel"]), 0, 1);
		List<KeyValuePair<string, Entry>> list = new List<KeyValuePair<string, Entry>>();
		if (jObject["banners"] is JArray jArray)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item is JObject jObject2)
				{
					Entry value = new Entry
					{
						TitleTh = Str(jObject2["title_th"]),
						Icon = Str(jObject2["icon"]),
						Tag = Str(jObject2["tag"]),
						EndAt = Num(jObject2["end_at"])
					};
					string text = Str(jObject2["web_link"]);
					string text2 = Str(jObject2["commodity_id"]);
					if (!string.IsNullOrEmpty(text))
					{
						list.Add(new KeyValuePair<string, Entry>(text, value));
					}
					if (!string.IsNullOrEmpty(text2))
					{
						list.Add(new KeyValuePair<string, Entry>("commodity:" + text2, value));
					}
				}
			}
		}
		_last = source + " style " + style + " · only_duel " + onlyDuel + " · " + list.Count + " rows";
		SetServer(style, onlyDuel, list, json ?? string.Empty);
	}

	private static void SetServer(int style, int onlyDuel, List<KeyValuePair<string, Entry>> rows, string signature)
	{
		bool flag = signature != _signature;
		_signature = signature;
		_serverStyle = style;
		_serverOnlyDuel = onlyDuel;
		_entries.Clear();
		if (rows != null)
		{
			foreach (KeyValuePair<string, Entry> row in rows)
			{
				_entries[row.Key] = row.Value;
			}
		}
		if (flag)
		{
			RaiseChanged();
		}
	}

	private static void RaiseChanged()
	{
		if (CommunityBannerStyle.Changed == null)
		{
			return;
		}
		Delegate[] invocationList = CommunityBannerStyle.Changed.GetInvocationList();
		foreach (Delegate obj in invocationList)
		{
			try
			{
				((Action)obj)();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
	}

	public static Entry For(PromotionLink link)
	{
		if (link == null)
		{
			return null;
		}
		if (!string.IsNullOrEmpty(link.WebLink) && _entries.TryGetValue(link.WebLink, out var value))
		{
			return value;
		}
		if (!string.IsNullOrEmpty(link.CommodityId) && _entries.TryGetValue("commodity:" + link.CommodityId, out value))
		{
			return value;
		}
		return null;
	}

	public static string IconFor(PromotionLink link, Entry entry)
	{
		if (entry != null && !string.IsNullOrEmpty(entry.Icon))
		{
			return entry.Icon;
		}
		switch (link?.WebLink)
		{
		case "ui://PvpRanked":
		case "ui://PvpIsland":
			return "action_attack_sword";
		case "ui://Music":
			return "icon_instrument_guitar01";
		case "ui://Story":
			return "tag_purpose_book";
		default:
			return null;
		}
	}

	public static string TitleFor(PromotionLink link, Entry entry)
	{
		if (CommunityRanked.Thai && entry != null && !string.IsNullOrEmpty(entry.TitleTh))
		{
			return OneLine(entry.TitleTh);
		}
		string text = null;
		switch (link?.WebLink)
		{
		case "ui://PvpRanked":
			text = CommunityRanked.T("ดวลจ\u0e31ดอ\u0e31นด\u0e31บ", "Ranked duel");
			break;
		case "ui://PvpIsland":
			text = CommunityRanked.T("เกาะกะเลาะว\u0e34วาท", "Brawl Island");
			break;
		case "ui://Music":
			text = CommunityRanked.T("เล\u0e48นเคร\u0e37\u0e48องดนตร\u0e35", "Play music");
			break;
		case "ui://Story":
			text = CommunityRanked.T("บ\u0e31นท\u0e36กประจำว\u0e31น", "Journal");
			break;
		}
		if (string.IsNullOrEmpty(text) && link != null)
		{
			text = OneLine(link.HudText);
			if (string.IsNullOrEmpty(text) || HasHangul(text))
			{
				text = OneLine(link.MainText);
			}
		}
		if (string.IsNullOrEmpty(text) || HasHangul(text))
		{
			return string.Empty;
		}
		return text;
	}

	public static string TagFor(PromotionLink link, Entry entry, double now, out bool countdown)
	{
		countdown = false;
		if (entry != null && !string.IsNullOrEmpty(entry.Tag) && !HasHangul(entry.Tag))
		{
			return entry.Tag;
		}
		double num = entry?.EndAt ?? 0.0;
		if (num <= 0.0 && link != null && !string.IsNullOrEmpty(link.EndAt))
		{
			double num2 = Times.ParseDateTimeToUnixTime(link.EndAt);
			if (num2 > now && num2 - now <= 2592000.0)
			{
				num = num2;
			}
		}
		if (num <= now)
		{
			return null;
		}
		countdown = true;
		return TimeLeft(num - now);
	}

	public static string TimeLeft(double sec)
	{
		if (sec >= 86400.0)
		{
			int num = (int)(sec / 86400.0);
			return CommunityRanked.T("เหล\u0e37อ " + num + " ว\u0e31น", num + ((num == 1) ? " day left" : " days left"));
		}
		if (sec >= 3600.0)
		{
			int num2 = (int)(sec / 3600.0);
			return CommunityRanked.T("เหล\u0e37อ " + num2 + " ชม.", num2 + "h left");
		}
		int num3 = Mathf.Max(1, (int)(sec / 60.0));
		return CommunityRanked.T("เหล\u0e37อ " + num3 + " นาท\u0e35", num3 + " min left");
	}

	public static double Now()
	{
		try
		{
			double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
			if (predictedServerTime > 1000000000.0)
			{
				return predictedServerTime;
			}
		}
		catch (Exception)
		{
		}
		return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
	}

	public static bool HasHangul(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			return Regex.IsMatch(text, "[\\u1100-\\u11FF\\u3130-\\u318F\\uAC00-\\uD7A3]");
		}
		return false;
	}

	private static string OneLine(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		return Regex.Replace(text, "\\s+", " ").Trim();
	}

	private static string Str(JToken token)
	{
		if (token == null || token.Type == JTokenType.Null)
		{
			return string.Empty;
		}
		return (((string)token) ?? string.Empty).Trim();
	}

	private static int Int(JToken token)
	{
		try
		{
			return (token != null && token.Type != JTokenType.Null) ? ((int)token) : 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static double Num(JToken token)
	{
		try
		{
			return (token != null && token.Type != JTokenType.Null) ? ((double)token) : 0.0;
		}
		catch (Exception)
		{
			return 0.0;
		}
	}

	public static string DebugCommand(string[] args)
	{
		switch ((args.Length > 1) ? args[1].ToLowerInvariant() : "state")
		{
		case "fetch":
			Fetch(force: true);
			return "fetch sent · " + Describe();
		case "demo":
		{
			double num = Now();
			_override = 1;
			_entries.Clear();
			_entries["ui://PvpRanked"] = new Entry
			{
				Tag = CommunityRanked.T("ใหม\u0e48", "New")
			};
			_entries["ui://PvpIsland"] = new Entry
			{
				Tag = CommunityRanked.T("ใหม\u0e48", "New")
			};
			_entries["ui://Story"] = new Entry
			{
				EndAt = num + 777600.0 + 3600.0
			};
			_last = "demo";
			RaiseChanged();
			return "demo · " + Describe();
		}
		case "off":
			_override = 0;
			_last = "off (this machine)";
			RaiseChanged();
			return "off · " + Describe();
		case "onlyduel":
			_overrideOnlyDuel = ((args.Length > 2 && args[2] == "server") ? (-1) : ((args.Length <= 2 || !(args[2] == "0")) ? 1 : 0));
			RaiseChanged();
			return "onlyduel · " + Describe();
		case "server":
			_override = -1;
			_overrideOnlyDuel = -1;
			_signature = "-";
			_entries.Clear();
			_last = "server (refetch)";
			RaiseChanged();
			Fetch(force: true);
			return "server · " + Describe();
		default:
			return Describe();
		}
	}

	public static string Describe()
	{
		return "style " + Style + ((_override >= 0) ? " (override)" : string.Empty) + " · server style " + _serverStyle + " · only_duel " + (OnlyDuel ? 1 : 0) + ((_overrideOnlyDuel >= 0) ? " (override)" : string.Empty) + " · rows " + _entries.Count + " · last " + _last + " · skins " + CommunityBannerSkin.Count;
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Network;
using Durango.UI;
using Durango.Utils;
using InteractionData;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using UnityEngine;

public class CommunityExchange : GameSystem<CommunityExchange>
{
	public const byte UpHello = 1;

	public const byte UpAuSearch = 10;

	public const byte UpAuMine = 11;

	public const byte UpAuList = 12;

	public const byte UpAuBid = 13;

	public const byte UpAuCancel = 14;

	public const byte UpAuClaim = 15;

	public const byte UpTrRequest = 20;

	public const byte UpTrRespond = 21;

	public const byte UpTrAdd = 22;

	public const byte UpTrRemove = 23;

	public const byte UpTrMoney = 24;

	public const byte UpTrReady = 25;

	public const byte UpTrConfirm = 26;

	public const byte UpTrCancel = 27;

	public const byte UpTrState = 28;

	public const byte DownConfig = 1;

	public const byte DownResult = 2;

	public const byte DownNotice = 3;

	public const byte DownTrade = 4;

	public const byte DownPending = 5;

	private const float ReplyTimeoutSec = 15f;

	public static bool ConfigReceived;

	public static bool AuctionEnabled;

	public static bool TradeEnabled;

	public static int[] Durations = new int[3] { 6, 12, 24 };

	public static double TaxRate = 0.05;

	public static double ListFeeRatio;

	public static double StepRatio = 0.05;

	public static long StepMin = 10L;

	public static long MinStartPrice = 1L;

	public static int MaxPieces = 99;

	public static int MaxActive = 10;

	public static float MinDurability = 0.9f;

	public static int TradeMaxSlots = 6;

	public static int TradeMaxPieces = 99;

	public static double TradeFeeRatio;

	public static float TradeConfirmLockSec = 3f;

	public static int PendingClaims;

	public static JObject Trade;

	public static string LastSent = string.Empty;

	public static string LastDown = string.Empty;

	public static int HelloSent;

	public static readonly List<string> TraceLog = new List<string>();

	private static int _req;

	private static readonly Dictionary<int, KeyValuePair<float, Action<JObject>>> _waiting = new Dictionary<int, KeyValuePair<float, Action<JObject>>>();

	private bool _helloPending;

	private bool _wasReady;

	private static InteractionSystem _hookedOn;

	private static string _invitePromptTrade = string.Empty;

	private float _nextShowTry;

	private float _nextTradeSync;

	private static bool _syncing;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"menu_trade",
			new string[4] { "เทรด", "Trade", "Tukar", "거래" }
		},
		{
			"trade_asked",
			new string[4] { "ส\u0e48งคำขอเทรดแล\u0e49ว รออ\u0e35กฝ\u0e48ายตอบร\u0e31บ", "Trade request sent — waiting for the other player", "Permintaan tukar dikirim", "거래를 요청했습니다" }
		},
		{
			"trade_done",
			new string[4] { "เทรดสำเร\u0e47จ — ของและเง\u0e34นเข\u0e49ากระเป\u0e4bาแล\u0e49ว", "Trade complete — items and T Stone are in your bag", "Tukar selesai", "거래가 완료되었습니다" }
		},
		{
			"trade_closed",
			new string[4] { "ยกเล\u0e34กเทรดแล\u0e49ว — ของและเง\u0e34นค\u0e37นเจ\u0e49าของแล\u0e49ว", "Trade cancelled — everything went back", "Tukar dibatalkan", "거래가 취소되었습니다" }
		},
		{
			"invite_main",
			new string[4] { "<em>{0}</em> ขอเทรดก\u0e31บค\u0e38ณ", "<em>{0}</em> wants to trade with you", "<em>{0}</em> ingin bertukar", "<em>{0}</em>님이 거래를 요청했습니다" }
		},
		{
			"invite_sub",
			new string[4] { "ของท\u0e35\u0e48วางในเทรดถ\u0e39กพ\u0e31กไว\u0e49ท\u0e35\u0e48เซ\u0e34ร\u0e4cฟจนเทรดจบ · ยกเล\u0e34ก/หล\u0e38ดเกม = ค\u0e37นเจ\u0e49าของ", "Offered items are held by the server until the trade ends · cancel/disconnect = returned", "Barang ditahan server sampai selesai", "거래가 끝날 때까지 서버가 보관합니다" }
		},
		{
			"accept",
			new string[4] { "ตอบร\u0e31บ", "Accept", "Terima", "수락" }
		},
		{
			"decline",
			new string[4] { "ปฏ\u0e34เสธ", "Decline", "Tolak", "거절" }
		},
		{
			"no_reply",
			new string[4] { "เซ\u0e34ร\u0e4cฟไม\u0e48ตอบ ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "No reply from the server — try again", "Server tidak menjawab", "서버 응답이 없습니다" }
		}
	};

	public static event Action Changed;

	public static void Trace(string what)
	{
		TraceLog.Add(Time.realtimeSinceStartup.ToString("0.0") + " " + what);
		if (TraceLog.Count > 60)
		{
			TraceLog.RemoveAt(0);
		}
	}

	private void Awake()
	{
		Trace("Awake (" + base.gameObject.name + ")");
		ResetState();
		Connections.Frontend.On<ExchangeDown>(OnDown);
		try
		{
			Singleton<GameManager>.Instance().WelcomeReceived += OnWelcome;
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private void OnDestroy()
	{
		Trace("OnDestroy → reset (" + _waiting.Count + " waiting)");
		ResetState();
	}

	private void OnWelcome(Welcome welcome)
	{
		Trace("Welcome → reset (" + _waiting.Count + " waiting)");
		ResetState();
		_helloPending = true;
	}

	private static void ResetState()
	{
		CommunityAuctionUI.Reset();
		ConfigReceived = false;
		AuctionEnabled = false;
		TradeEnabled = false;
		PendingClaims = 0;
		Trade = null;
		_invitePromptTrade = string.Empty;
		foreach (KeyValuePair<int, KeyValuePair<float, Action<JObject>>> item in new List<KeyValuePair<int, KeyValuePair<float, Action<JObject>>>>(_waiting))
		{
			SafeCall(item.Value.Value, null);
		}
		_waiting.Clear();
		try
		{
			CommunityTradeUI.CloseWindow();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		RaiseChanged();
	}

	private void Update()
	{
		bool isReady = GameManager.IsReady;
		if (isReady != _wasReady)
		{
			_wasReady = isReady;
			if (!isReady)
			{
				Trade = null;
				CommunityTradeUI.CloseWindow();
			}
		}
		if (_helloPending && isReady)
		{
			_helloPending = false;
			SendHello();
		}
		if (_waiting.Count > 0)
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			List<int> list = null;
			foreach (KeyValuePair<int, KeyValuePair<float, Action<JObject>>> item in _waiting)
			{
				if (realtimeSinceStartup - item.Value.Key > 15f)
				{
					(list ?? (list = new List<int>())).Add(item.Key);
				}
			}
			if (list != null)
			{
				foreach (int item2 in list)
				{
					Trace("timeout req " + item2);
					Action<JObject> value = _waiting[item2].Value;
					_waiting.Remove(item2);
					SafeCall(value, null);
				}
			}
		}
		try
		{
			HookInteraction();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		if (Trade != null && isReady)
		{
			float realtimeSinceStartup2 = Time.realtimeSinceStartup;
			if ((int)Num(Trade, "state", 0.0) == 2 && !CommunityTradeUI.IsOpen && realtimeSinceStartup2 >= _nextShowTry)
			{
				_nextShowTry = realtimeSinceStartup2 + 1f;
				CommunityTradeUI.ShowWindow();
			}
			if (realtimeSinceStartup2 >= _nextTradeSync)
			{
				_nextTradeSync = realtimeSinceStartup2 + 5f;
				SyncTrade();
			}
		}
	}

	private static void SyncTrade()
	{
		if (_syncing || Trade == null)
		{
			return;
		}
		_syncing = true;
		string tradeId = Str(Trade, "tradeId");
		Request(28, new JObject(), delegate(JObject r)
		{
			_syncing = false;
			if (r != null && Bool(r, "ok") && !(r["trade"] is JObject) && Trade != null && Str(Trade, "tradeId") == tradeId)
			{
				Trade = null;
				CommunityTradeUI.CloseWindow();
				UIManager.SystemMsg(Text("trade_closed"));
				RaiseChanged();
			}
		});
	}

	private static void SafeCall(Action<JObject> cb, JObject o)
	{
		if (cb == null)
		{
			return;
		}
		try
		{
			cb(o);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void RaiseChanged()
	{
		if (CommunityExchange.Changed == null)
		{
			return;
		}
		try
		{
			CommunityExchange.Changed();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void SendHello()
	{
		if (CommunityLink.Active)
		{
			JObject jObject = new JObject();
			jObject["v"] = 1;
			if (SendRaw(1, jObject))
			{
				HelloSent++;
			}
		}
	}

	private static bool SendRaw(byte kind, JObject body)
	{
		string text = body.ToString(Formatting.None);
		LastSent = kind + " " + text;
		try
		{
			Connection frontend = Connections.Frontend;
			if (frontend == null || !frontend.Connected())
			{
				LastSent += " (offline)";
				return false;
			}
			frontend.Send(new ExchangeUp
			{
				Kind = kind,
				Json = text
			}, noReply: true);
			return true;
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[Exchange] send failed: " + ex.Message);
			return false;
		}
	}

	public static void Request(byte kind, JObject body, Action<JObject> onReply)
	{
		body = body ?? new JObject();
		int num = ++_req;
		body["req"] = num;
		Trace("↑ " + kind + " req " + num);
		if (onReply != null)
		{
			_waiting[num] = new KeyValuePair<float, Action<JObject>>(Time.realtimeSinceStartup, onReply);
		}
		if (!SendRaw(kind, body) && onReply != null)
		{
			_waiting.Remove(num);
			SafeCall(onReply, null);
		}
	}

	private static void OnDown(ExchangeDown msg, PacketHeader header)
	{
		JObject jObject = Parse(msg.Json);
		LastDown = msg.Kind + " " + ((msg.Json != null && msg.Json.Length > 300) ? msg.Json.Substring(0, 300) : msg.Json);
		if (jObject == null)
		{
			Trace("↓ " + msg.Kind + " parse fail (" + ((msg.Json != null) ? msg.Json.Length : 0) + " chars)");
			return;
		}
		try
		{
			switch (msg.Kind)
			{
			case 1:
				ApplyConfig(jObject);
				break;
			case 2:
			{
				int key = (int)Num(jObject, "req", 0.0);
				Trace("↓ 2 req " + key + " " + Str(jObject, "op") + (_waiting.ContainsKey(key) ? " → callback" : " (no one waiting)"));
				if (_waiting.TryGetValue(key, out var value))
				{
					_waiting.Remove(key);
					SafeCall(value.Value, jObject);
				}
				ApplyTradeFrom(jObject);
				break;
			}
			case 3:
				Trace("↓ 3 notice " + Str(jObject, "kind"));
				OnNotice(jObject);
				break;
			case 4:
				Trace("↓ 4 trade " + Str(jObject, "kind") + " v" + Num(jObject["trade"] as JObject, "version", 0.0));
				OnTrade(jObject);
				break;
			case 5:
				PendingClaims = (int)Num(jObject, "claims", 0.0);
				RaiseChanged();
				break;
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void ApplyConfig(JObject o)
	{
		JObject jObject = o["auction"] as JObject;
		JObject jObject2 = o["trade"] as JObject;
		if (jObject != null)
		{
			AuctionEnabled = Bool(jObject, "enabled");
			if (jObject["durations"] is JArray { Count: >0 } jArray)
			{
				List<int> list = new List<int>();
				foreach (JToken item in (IEnumerable<JToken>)jArray)
				{
					list.Add((int)NumOf(item, 0.0));
				}
				Durations = list.ToArray();
			}
			TaxRate = Num(jObject, "taxRate", 0.05);
			ListFeeRatio = Num(jObject, "listFeeRatio", 0.0);
			StepRatio = Num(jObject, "stepRatio", 0.05);
			StepMin = (long)Num(jObject, "stepMin", 10.0);
			MinStartPrice = (long)Num(jObject, "minStartPrice", 1.0);
			MaxPieces = (int)Num(jObject, "maxPieces", 99.0);
			MaxActive = (int)Num(jObject, "maxActive", 10.0);
			MinDurability = (float)Num(jObject, "minDurability", 0.9);
		}
		if (jObject2 != null)
		{
			TradeEnabled = Bool(jObject2, "enabled");
			TradeMaxSlots = (int)Num(jObject2, "maxSlots", 6.0);
			TradeMaxPieces = (int)Num(jObject2, "maxPieces", 99.0);
			TradeFeeRatio = Num(jObject2, "feeRatio", 0.0);
			TradeConfirmLockSec = (float)Num(jObject2, "confirmLockSec", 3.0);
		}
		ConfigReceived = true;
		RaiseChanged();
	}

	private static void OnNotice(JObject o)
	{
		string text = Str(o, "text");
		if (text.Length > 0)
		{
			UIManager.SystemMsg(text);
		}
		CommunityAuctionUI.OnNotice(Str(o, "kind"));
	}

	private static void ApplyTradeFrom(JObject result)
	{
		JObject jObject = result["trade"] as JObject;
		string text = Str(result, "op");
		if (jObject != null && text.StartsWith("player.trade.", StringComparison.Ordinal))
		{
			int num = (int)Num(jObject, "state", 0.0);
			if (num == 1 || num == 2)
			{
				SetTrade(jObject);
			}
		}
	}

	private static void OnTrade(JObject o)
	{
		string text = Str(o, "kind");
		if (!(o["trade"] is JObject jObject))
		{
			return;
		}
		switch (text)
		{
		case "invite":
			AskInvite(jObject);
			break;
		case "state":
			SetTrade(jObject);
			break;
		case "done":
			Trade = null;
			CommunityTradeUI.CloseWindow();
			UIManager.SystemMsg(Text("trade_done"));
			RaiseChanged();
			break;
		case "closed":
			if ((Trade != null && Str(Trade, "tradeId") == Str(jObject, "tradeId")) || Str(jObject, "tradeId") == _invitePromptTrade)
			{
				Trade = null;
				CommunityTradeUI.CloseWindow();
				string text2 = Str(jObject, "text");
				UIManager.SystemMsg((text2.Length > 0) ? text2 : Text("trade_closed"));
				RaiseChanged();
			}
			break;
		}
	}

	private static void SetTrade(JObject t)
	{
		Trade = t;
		if ((int)Num(t, "state", 0.0) == 2)
		{
			CommunityTradeUI.ShowWindow();
		}
		RaiseChanged();
		CommunityTradeUI.Refresh();
	}

	private static void AskInvite(JObject t)
	{
		string tradeId = Str(t, "tradeId");
		string text = ((t["other"] is JObject o) ? Str(o, "name") : "?");
		_invitePromptTrade = tradeId;
		MessageBox messageBox = UIManager.MessageBox;
		messageBox.Show(Text("invite_main", text), Text("invite_sub"), delegate(bool ok)
		{
			if (!(_invitePromptTrade != tradeId))
			{
				_invitePromptTrade = string.Empty;
				Request(21, new JObject
				{
					["tradeId"] = tradeId,
					["accept"] = ok
				}, delegate(JObject r)
				{
					if (r != null && !Bool(r, "ok"))
					{
						UIManager.SystemMsg(Str(r, "text"));
					}
				});
			}
		}, Text("accept"), Text("decline"));
		float num = (float)Num(t, "expiresInSec", 30.0);
		if (num > 1f)
		{
			messageBox.SetHideTimer(Time.time + num);
		}
	}

	private static void HookInteraction()
	{
		if (GameManager.IsReady && GameSystem<InteractionSystem>.HasInstance())
		{
			InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
			if (!(interactionSystem == _hookedOn))
			{
				interactionSystem.AddInteractionHandler(Interaction.CommunityTrade, OnTradeAction);
				interactionSystem.PostTouched += OnPostTouched;
				_hookedOn = interactionSystem;
			}
		}
	}

	private static void OnPostTouched(InteractionMenuList list, InteractionObject target)
	{
		try
		{
			if (TradeEnabled && target != null && list != null)
			{
				PlayerBehavior targetComponent = target.GetTargetComponent<PlayerBehavior>();
				if (!(targetComponent == null) && !string.IsNullOrEmpty(targetComponent.EntityId) && !(targetComponent.EntityId == GameManager.PlayerId))
				{
					InteractionMenuData data = new InteractionMenuData(Interaction.CommunityTrade);
					data.Name = Text("menu_trade");
					data.Disabled = Trade != null;
					list.Add(data);
				}
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void OnTradeAction(InteractionObject target)
	{
		PlayerBehavior playerBehavior = target?.GetTargetComponent<PlayerBehavior>();
		if (!(playerBehavior == null) && !string.IsNullOrEmpty(playerBehavior.EntityId))
		{
			RequestTrade(playerBehavior.EntityId);
		}
	}

	public static void RequestTrade(string entityId)
	{
		JObject jObject = new JObject();
		jObject["targetId"] = entityId;
		Request(20, jObject, delegate(JObject r)
		{
			if (r == null)
			{
				UIManager.SystemMsg(Text("no_reply"));
			}
			else if (!Bool(r, "ok"))
			{
				UIManager.SystemMsg(Str(r, "text"));
			}
			else if ((int)Num(r["trade"] as JObject, "state", 0.0) != 2)
			{
				UIManager.SystemMsg(Text("trade_asked"));
			}
		});
	}

	public static ItemData ItemOf(JToken token)
	{
		if (!(token is JObject o))
		{
			return null;
		}
		return CommunityItemShare.Decode(Str(o, "b64"));
	}

	public static JObject Parse(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return new JObject();
		}
		try
		{
			return JToken.Parse(json) as JObject;
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[Exchange] bad json: " + ex.Message);
			return null;
		}
	}

	public static string Str(JObject o, string key)
	{
		JToken jToken = o?[key];
		if (jToken == null || jToken.Type == JTokenType.Null)
		{
			return string.Empty;
		}
		return jToken.ToString();
	}

	public static double Num(JObject o, string key, double fallback)
	{
		JToken jToken = o?[key];
		if (jToken == null)
		{
			return fallback;
		}
		try
		{
			object obj = ((jToken is JValue jValue) ? jValue.Value : null);
			if (obj is long)
			{
				return (long)obj;
			}
			if (obj is int)
			{
				return (int)obj;
			}
			if (obj is double result)
			{
				return result;
			}
			if (obj is float)
			{
				return (float)obj;
			}
			if (obj is decimal)
			{
				return (double)(decimal)obj;
			}
			if (double.TryParse((obj != null) ? Convert.ToString(obj, CultureInfo.InvariantCulture) : jToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				return result2;
			}
		}
		catch (Exception ex)
		{
			Trace("Num " + key + " failed: " + ex.GetType().Name + " " + ex.Message);
		}
		return fallback;
	}

	private static double NumOf(JToken token, double fallback)
	{
		return Num(new JObject { ["v"] = token }, "v", fallback);
	}

	public static bool Bool(JObject o, string key)
	{
		JToken jToken = o?[key];
		if (jToken == null)
		{
			return false;
		}
		if (jToken is JValue jValue && jValue.Value is bool)
		{
			return (bool)jValue.Value;
		}
		if (!(jToken.ToString() == "1"))
		{
			return jToken.ToString() == "true";
		}
		return true;
	}

	public static string TStoneText(long amount)
	{
		return Durango.Logic.Item.Inventory.CurrencyFormat(amount, Currency.TStone);
	}

	public static Dictionary<string, object> State()
	{
		return new Dictionary<string, object>
		{
			["config"] = ConfigReceived,
			["auction"] = AuctionEnabled,
			["trade"] = TradeEnabled,
			["pending"] = PendingClaims,
			["helloSent"] = HelloSent,
			["tradeOpen"] = Trade != null,
			["tradeVersion"] = ((Trade != null) ? Num(Trade, "version", 0.0) : 0.0),
			["window"] = CommunityTradeUI.IsOpen,
			["lastSent"] = LastSent,
			["lastDown"] = LastDown,
			["waiting"] = _waiting.Count,
			["trace"] = TraceLog.ToArray()
		};
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return CommunityAuctionUI.Text(key, args);
		}
		string text = CommunityDiscordLink.Lang switch
		{
			"th" => value[0],
			"id" => value[2],
			"ko" => value[3],
			_ => value[1],
		};
		if (args == null || args.Length == 0)
		{
			return text;
		}
		try
		{
			return string.Format(text, args);
		}
		catch (FormatException)
		{
			return text;
		}
	}
}

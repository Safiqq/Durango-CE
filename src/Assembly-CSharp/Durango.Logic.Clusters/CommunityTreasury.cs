using System;
using System.Collections.Generic;
using System.Globalization;
using BestHTTP;
using BestHTTP.Forms;
using Durango.UI;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using UnityEngine;

namespace Durango.Logic.Clusters;

public static class CommunityTreasury
{
	private const int HttpTimeoutSec = 15;

	private const float StaleSec = 20f;

	public static bool Active;

	public static long Balance;

	public static long Rate;

	public static double Fee;

	public static long PerCoin;

	public static long MaxCoins;

	public static long? DailyLeft;

	public static long[] Packs = new long[3] { 10L, 100L, 1000L };

	public static bool Busy;

	public static bool Fetching;

	public static string LastEvent = string.Empty;

	private static float _fetchedAt = -1000f;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"title",
			new string[4] { "คล\u0e31งเง\u0e34นเอ\u0e47ม", "M Treasury", "M Treasury", "M 금고" }
		},
		{
			"button",
			new string[4] { "คล\u0e31งเง\u0e34นเอ\u0e47ม", "M Treasury", "M Treasury", "M 금고" }
		},
		{
			"balance",
			new string[4] { "ยอดคล\u0e31งเง\u0e34นเอ\u0e47มตอนน\u0e35\u0e49 (ท\u0e31\u0e49งเซ\u0e34ร\u0e4cฟ)", "M Treasury now (whole server)", "Saldo M Treasury (seluruh server)", "현재 M 금고 잔액 (서버 전체)" }
		},
		{
			"max",
			new string[4] { "คล\u0e31งจ\u0e48ายได\u0e49ส\u0e39งส\u0e38ด", "Treasury can pay up to", "Maksimal dibayar", "최대 지급 가능" }
		},
		{
			"amount",
			new string[4] { "จำนวน Durango Coin ท\u0e35\u0e48จะแลก", "Durango Coin to trade", "Durango Coin yang ditukar", "교환할 듀랑고 코인" }
		},
		{
			"have",
			new string[4] { "ม\u0e35 {0}", "You have {0}", "Kamu punya {0}", "보유 {0}" }
		},
		{
			"tap",
			new string[4] { "แตะเพ\u0e37\u0e48อใส\u0e48จำนวน", "Tap to enter an amount", "Ketuk untuk isi jumlah", "눌러서 수량 입력" }
		},
		{
			"max_btn",
			new string[4] { "ส\u0e39งส\u0e38ด", "Max", "Maks", "최대" }
		},
		{
			"pay",
			new string[4] { "คล\u0e31งจ\u0e48าย", "Treasury pays", "Dibayar treasury", "금고 지급" }
		},
		{
			"fee",
			new string[4] { "ค\u0e48าธรรมเน\u0e35ยม {0}% (เผาท\u0e34\u0e49ง)", "Fee {0}% (burned)", "Biaya {0}% (dibakar)", "수수료 {0}% (소각)" }
		},
		{
			"get",
			new string[4] { "ค\u0e38ณได\u0e49ร\u0e31บ", "You get", "Kamu dapat", "받는 금액" }
		},
		{
			"now",
			new string[4] { "ตอนน\u0e35\u0e49", "Now", "Sekarang", "현재" }
		},
		{
			"after",
			new string[4] { "หล\u0e31งแลก", "After", "Sesudah", "교환 후" }
		},
		{
			"cancel",
			new string[4] { "ยกเล\u0e34ก", "Cancel", "Batal", "취소" }
		},
		{
			"exchange",
			new string[4] { "แลก", "Trade", "Tukar", "교환" }
		},
		{
			"exchange_n",
			new string[4] { "แลก {0} Coin", "Trade {0} Coin", "Tukar {0} Coin", "{0} 코인 교환" }
		},
		{
			"st_ok",
			new string[4] { "คล\u0e31งพอจ\u0e48าย", "The treasury can pay this", "Treasury cukup", "금고 잔액 충분" }
		},
		{
			"st_partial",
			new string[4] { "คล\u0e31งม\u0e35พอแค\u0e48 {0} Coin — Coin ท\u0e35\u0e48เหล\u0e37อไม\u0e48ถ\u0e39กห\u0e31ก", "The treasury can pay only {0} Coin — the rest is not taken", "Treasury hanya cukup {0} Coin — sisanya tidak dipotong", "금고가 {0} 코인만 지급 가능 — 나머지는 차감되지 않음" }
		},
		{
			"st_daily",
			new string[4] { "ว\u0e31นน\u0e35\u0e49แลกได\u0e49อ\u0e35ก {0} Coin", "{0} Coin left for today", "Sisa {0} Coin hari ini", "오늘 {0} 코인 남음" }
		},
		{
			"st_empty",
			new string[4] { "คล\u0e31งหมด — รอผ\u0e39\u0e49เล\u0e48นแลก T Stone เป\u0e47น Coin เพ\u0e34\u0e48มก\u0e48อน", "The treasury is empty — wait for players to trade T Stone for Coin", "Treasury kosong — tunggu pemain menukar T Stone ke Coin", "금고가 비었습니다 — T Stone → 코인 교환을 기다려 주세요" }
		},
		{
			"st_coin",
			new string[4] { "Coin ไม\u0e48พอ", "Not enough Coin", "Coin tidak cukup", "코인이 부족합니다" }
		},
		{
			"st_none",
			new string[4] { "ใส\u0e48จำนวน Coin ท\u0e35\u0e48จะแลก", "Enter how much Coin to trade", "Isi jumlah Coin", "교환할 코인 수량을 입력하세요" }
		},
		{
			"st_loading",
			new string[4] { "กำล\u0e31งโหลดยอดคล\u0e31ง…", "Loading the treasury…", "Memuat treasury…", "금고 불러오는 중…" }
		},
		{
			"st_off",
			new string[4] { "คล\u0e31งเง\u0e34นเอ\u0e47มป\u0e34ดอย\u0e39\u0e48", "The M Treasury is closed", "M Treasury sedang ditutup", "M 금고가 닫혀 있습니다" }
		},
		{
			"confirm",
			new string[4] { "แลกแล\u0e49วจะได\u0e49 {0} T Stone (ห\u0e31กค\u0e48าธรรมเน\u0e35ยม {1} T)", "You will get {0} T Stone ({1} T fee)", "Kamu akan dapat {0} T Stone (biaya {1} T)", "{0} T Stone을 받습니다 (수수료 {1} T)" }
		},
		{
			"done",
			new string[4] { "แลก {0} Coin → ได\u0e49 {1} T Stone · คล\u0e31งเหล\u0e37อ {2} T", "Traded {0} Coin → got {1} T Stone · treasury left {2} T", "Menukar {0} Coin → dapat {1} T Stone · sisa treasury {2} T", "{0} 코인 교환 → {1} T Stone 획득 · 금고 잔액 {2} T" }
		},
		{
			"busy",
			new string[4] { "กำล\u0e31งแลกอย\u0e39\u0e48 — รอส\u0e31กคร\u0e39\u0e48", "Trade in progress — please wait", "Sedang menukar — tunggu sebentar", "교환 중입니다. 잠시 기다려 주세요." }
		},
		{
			"failed",
			new string[4] { "แลกไม\u0e48สำเร\u0e47จ", "Trade failed", "Penukaran gagal", "교환 실패" }
		},
		{
			"fail_empty",
			new string[4] { "คล\u0e31งเง\u0e34นเอ\u0e47มหมดแล\u0e49ว", "The M Treasury is empty", "M Treasury sudah kosong", "M 금고가 비었습니다" }
		},
		{
			"fail_coin",
			new string[4] { "Coin ไม\u0e48พอ", "Not enough Coin", "Coin tidak cukup", "코인이 부족합니다" }
		},
		{
			"fail_full",
			new string[4] { "กระเป\u0e4bา T Stone เต\u0e47ม", "Your T Stone wallet is full", "Dompet T Stone penuh", "T Stone 보유 한도를 초과합니다" }
		},
		{
			"fail_daily",
			new string[4] { "แลกครบเพดานของว\u0e31นน\u0e35\u0e49แล\u0e49ว", "You reached today's limit", "Batas hari ini tercapai", "오늘 한도에 도달했습니다" }
		},
		{
			"fail_frozen",
			new string[4] { "บ\u0e31ญช\u0e35ถ\u0e39กระง\u0e31บการเง\u0e34นช\u0e31\u0e48วคราว ต\u0e34ดต\u0e48อ GM", "Your wallet is frozen — contact a GM", "Dompetmu dibekukan — hubungi GM", "재화 사용이 일시 정지되었습니다. GM에게 문의하세요." }
		},
		{
			"fail_disabled",
			new string[4] { "คล\u0e31งเง\u0e34นเอ\u0e47มป\u0e34ดอย\u0e39\u0e48", "The M Treasury is closed", "M Treasury sedang ditutup", "M 금고가 닫혀 있습니다" }
		},
		{
			"fail_bad_amount",
			new string[4] { "จำนวนไม\u0e48ถ\u0e39กต\u0e49อง", "Invalid amount", "Jumlah tidak valid", "잘못된 수량입니다" }
		},
		{
			"fail_rate_limited",
			new string[4] { "แลกถ\u0e35\u0e48เก\u0e34นไป — รอส\u0e31กคร\u0e39\u0e48", "Too many trades — wait a moment", "Terlalu sering — tunggu sebentar", "너무 자주 교환했습니다. 잠시 후 다시 시도하세요." }
		},
		{
			"fail_unresolved",
			new string[4] { "เซ\u0e34ร\u0e4cฟตอบช\u0e49า — ถ\u0e49า Coin ถ\u0e39กห\u0e31กแต\u0e48ไม\u0e48ได\u0e49 T Stone แจ\u0e49งแอดม\u0e34นพร\u0e49อมรห\u0e31ส {0}", "The server was slow — if Coin was taken but no T Stone arrived, tell an admin with code {0}", "Server lambat — jika Coin terpotong tanpa T Stone, lapor admin dengan kode {0}", "서버 응답 지연 — 코인만 차감되었다면 코드 {0}로 관리자에게 문의하세요" }
		},
		{
			"fail_unsupported",
			new string[4] { "เซ\u0e34ร\u0e4cฟน\u0e35\u0e49ย\u0e31งไม\u0e48ม\u0e35คล\u0e31งเง\u0e34นเอ\u0e47ม", "This server has no M Treasury yet", "Server ini belum punya M Treasury", "이 서버에는 아직 M 금고가 없습니다" }
		},
		{
			"fail_unauthorized",
			new string[4] { "ต\u0e49องเข\u0e49าเกมก\u0e48อน", "Enter the world first", "Masuk ke dunia dulu", "먼저 게임에 접속하세요" }
		},
		{
			"fail_offline",
			new string[4] { "ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Couldn't reach the server. Try again.", "Tidak dapat menghubungi server. Coba lagi.", "서버에 연결할 수 없습니다. 다시 시도하세요." }
		}
	};

	private static string GatewayUrl
	{
		get
		{
			string text = GameManager.GatewayUrl;
			if (string.IsNullOrEmpty(text))
			{
				text = CommunityLink.Gateway;
			}
			return (text ?? string.Empty).TrimEnd('/');
		}
	}

	public static bool Usable
	{
		get
		{
			if (CommunityLink.Active && GatewayUrl.Length > 0)
			{
				return !string.IsNullOrEmpty(GameManager.SessionToken);
			}
			return false;
		}
	}

	public static long CoinBalance
	{
		get
		{
			try
			{
				return Math.Max(0L, InventorySystem.Wallet.GetBalance(Currency.Coin));
			}
			catch (Exception)
			{
				return 0L;
			}
		}
	}

	public static long TStoneBalance
	{
		get
		{
			try
			{
				return InventorySystem.Wallet.GetBalance(Currency.TStone);
			}
			catch (Exception)
			{
				return 0L;
			}
		}
	}

	public static event Action Changed;

	public static long PayoutOf(long drawn)
	{
		if (drawn <= 0)
		{
			return 0L;
		}
		double num = Math.Round(Math.Min(1.0, Math.Max(0.0, Fee)), 6);
		return (long)Math.Floor((double)drawn * (1.0 - num) + 1E-06);
	}

	public static long EffectiveCoins(long want)
	{
		if (want <= 0)
		{
			return 0L;
		}
		long num = Math.Min(want, Math.Max(0L, MaxCoins));
		if (DailyLeft.HasValue)
		{
			num = Math.Min(num, Math.Max(0L, DailyLeft.Value));
		}
		return num;
	}

	public static void FetchIfStale()
	{
		if (Time.realtimeSinceStartup - _fetchedAt >= 20f)
		{
			Fetch(null);
		}
	}

	public static void Fetch(Action<bool> done)
	{
		if (!Usable)
		{
			SetInactive("unavailable");
			if (done != null)
			{
				done(obj: false);
			}
			return;
		}
		if (Fetching)
		{
			if (done != null)
			{
				done(Active);
			}
			return;
		}
		Fetching = true;
		Send("/treasury/info", null, delegate(JObject o, int http)
		{
			Fetching = false;
			_fetchedAt = Time.realtimeSinceStartup;
			bool flag = o != null && Bool(o, "ok");
			if (flag)
			{
				Apply(o);
			}
			else
			{
				SetInactive((http == 404) ? "unsupported" : ("info HTTP " + http));
			}
			if (done != null)
			{
				done(flag && Active);
			}
		});
	}

	public static void Exchange(long want, Action<bool, string, JObject> done)
	{
		if (!Usable)
		{
			LastEvent = "unavailable";
			if (done != null)
			{
				done(arg1: false, "offline", null);
			}
			return;
		}
		if (Busy)
		{
			if (done != null)
			{
				done(arg1: false, "busy", null);
			}
			return;
		}
		Busy = true;
		Send("/treasury/exchange", new Dictionary<string, string> {
		{
			"coin",
			want.ToString()
		} }, delegate(JObject o, int http)
		{
			Busy = false;
			bool flag = o != null && Bool(o, "ok");
			string text = ((o != null && o["reason"] != null) ? (((string)o["reason"]) ?? string.Empty) : string.Empty);
			if (!flag && text.Length == 0)
			{
				text = ((http == 404) ? "unsupported" : ((o == null) ? "offline" : "failed"));
			}
			if (o != null && o["info"] is JObject)
			{
				Apply((JObject)o["info"]);
				_fetchedAt = Time.realtimeSinceStartup;
			}
			LastEvent = (flag ? ("ok " + Long(o, "coins") + " coin → " + Long(o, "tStone") + " T") : ("fail HTTP " + http + " " + text));
			UnityEngine.Debug.Log("[Community] m-treasury exchange " + want + " → " + LastEvent);
			if (done != null)
			{
				done(flag, text, o);
			}
		});
	}

	public static bool TryOpenFromCoinSlot(Currency currency)
	{
		if (!Active)
		{
			return false;
		}
		Currency currency2 = currency.Normalize();
		if (currency2 != Currency.MobileCoin && currency2 != Currency.PcCoin && currency2 != Currency.Coin)
		{
			return false;
		}
		ShopGroup shopGroup = UIManager.FindScript<ShopGroup>();
		if (shopGroup == null || !shopGroup.IsOpened)
		{
			return false;
		}
		CommunityTreasuryUI.Open();
		return true;
	}

	private static void Apply(JObject o)
	{
		Active = Bool(o, "active");
		Balance = Long(o, "balance");
		Rate = Long(o, "rate");
		Fee = ((o["fee"] != null && (o["fee"].Type == JTokenType.Float || o["fee"].Type == JTokenType.Integer)) ? ((double)o["fee"]) : 0.0);
		PerCoin = Long(o, "perCoin");
		MaxCoins = Long(o, "maxCoins");
		DailyLeft = ((o["dailyLeft"] != null && o["dailyLeft"].Type == JTokenType.Integer) ? new long?((long)o["dailyLeft"]) : ((long?)null));
		if (o["packs"] is JArray { Count: >0 } jArray)
		{
			List<long> list = new List<long>();
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item.Type == JTokenType.Integer && (long)item > 0)
				{
					list.Add((long)item);
				}
			}
			if (list.Count > 0)
			{
				Packs = list.ToArray();
			}
		}
		if (Rate <= 0)
		{
			Active = false;
		}
		LastEvent = "info active=" + Active + " balance=" + Balance + " rate=" + Rate + " fee=" + Fee;
		Notify();
	}

	private static void SetInactive(string why)
	{
		bool active = Active;
		Active = false;
		LastEvent = why;
		if (active)
		{
			Notify();
		}
	}

	private static void Notify()
	{
		try
		{
			if (CommunityTreasury.Changed != null)
			{
				CommunityTreasury.Changed();
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static bool Bool(JObject o, string key)
	{
		if (o[key] != null && o[key].Type == JTokenType.Boolean)
		{
			return (bool)o[key];
		}
		return false;
	}

	public static long Long(JObject o, string key)
	{
		if (o == null || o[key] == null || (o[key].Type != JTokenType.Integer && o[key].Type != JTokenType.Float))
		{
			return 0L;
		}
		return (long)o[key];
	}

	private static void Send(string path, Dictionary<string, string> fields, Action<JObject, int> done)
	{
		HTTPRequest hTTPRequest = new HTTPRequest(new Uri(GatewayUrl + path), (fields != null) ? HTTPMethods.Post : HTTPMethods.Get, delegate(HTTPRequest req, HTTPResponse resp)
		{
			JObject jObject = null;
			int arg = resp?.StatusCode ?? 0;
			try
			{
				if (resp != null && !string.IsNullOrEmpty(resp.DataAsText))
				{
					jObject = JObject.Parse(resp.DataAsText);
				}
			}
			catch (Exception)
			{
				jObject = null;
			}
			if (jObject == null)
			{
				UnityEngine.Debug.Log("[Community] m-treasury " + path + " ล\u0e49ม: " + req.State.ToString() + " HTTP " + arg + ((req.Exception != null) ? (" " + req.Exception.Message) : string.Empty));
			}
			try
			{
				done(jObject, arg);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		});
		hTTPRequest.AddHeader("Authorization", GameManager.SessionToken);
		hTTPRequest.AddHeader("Accept", "application/json");
		hTTPRequest.AddHeader("X-Community-Client", CommunityLink.ClientVersion);
		hTTPRequest.AddHeader("X-Community-Caps", "nametag-emoji, item-link, server-popup, voice-chat, i18n-keys, pet-scale, pvp-ranked, pvp-duel, gather-check, rift-coin, rift-scope");
		if (fields != null)
		{
			hTTPRequest.FormUsage = HTTPFormUsage.UrlEncoded;
			hTTPRequest.AddField("lang", CommunityDiscordLink.Lang);
			foreach (KeyValuePair<string, string> field in fields)
			{
				hTTPRequest.AddField(field.Key, field.Value);
			}
		}
		hTTPRequest.ConnectTimeout = TimeSpan.FromSeconds(10.0);
		hTTPRequest.Timeout = TimeSpan.FromSeconds(15.0);
		hTTPRequest.DisableCache = true;
		hTTPRequest.Send();
	}

	public static bool HasText(string key)
	{
		return Texts.ContainsKey(key);
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
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

	public static string N(long value)
	{
		return value.ToString("N0", CultureInfo.InvariantCulture);
	}
}

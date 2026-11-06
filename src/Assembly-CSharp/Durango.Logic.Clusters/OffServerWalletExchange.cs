using System;
using System.Collections.Generic;
using BestHTTP;
using BestHTTP.Forms;
using Durango.Logic.Item;
using Durango.UI;
using Durango.UI.Popup;
using Newtonsoft.Json.Linq;
using Shared.Economy;
using UnityEngine;

namespace Durango.Logic.Clusters;

public static class OffServerWalletExchange
{
	public const string ToGem = "t2g";

	public const string ToTStone = "g2t";

	private const int HttpTimeoutSec = 15;

	public static int Rate = 10;

	public static bool Busy;

	public static string LastEvent = string.Empty;

	public static readonly bool Enabled = false;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"title",
			new string[4] { "แลกเปล\u0e35\u0e48ยน T-Stone ↔ Gem", "Exchange T-Stone ↔ Gem", "Tukar T-Stone ↔ Gem", "T-Stone ↔ 젬 교환" }
		},
		{
			"rate",
			new string[4] { "อ\u0e31ตรา {0} = {1} (แลกได\u0e49ท\u0e31\u0e49งสองทาง)", "Rate {0} = {1} (both ways)", "Kurs {0} = {1} (dua arah)", "교환 비율 {0} = {1} (양방향)" }
		},
		{
			"btn_t2g",
			new string[4] { "T-Stone → Gem", "T-Stone → Gem", "T-Stone → Gem", "T-Stone → 젬" }
		},
		{
			"btn_g2t",
			new string[4] { "Gem → T-Stone", "Gem → T-Stone", "Gem → T-Stone", "젬 → T-Stone" }
		},
		{
			"close",
			new string[4] { "ป\u0e34ด", "Close", "Tutup", "닫기" }
		},
		{
			"amount_t2g",
			new string[4] { "จำนวน Gem ท\u0e35\u0e48ต\u0e49องการ", "Gems to get", "Jumlah Gem yang diinginkan", "받을 젬 수량" }
		},
		{
			"amount_g2t",
			new string[4] { "จำนวน Gem ท\u0e35\u0e48จะแลก", "Gems to exchange", "Jumlah Gem yang ditukar", "교환할 젬 수량" }
		},
		{
			"confirm",
			new string[4] { "แลกแล\u0e49วจะได\u0e49 {0}", "You will get {0}", "Kamu akan mendapat {0}", "{0}을(를) 받습니다" }
		},
		{
			"btn_exchange",
			new string[4] { "แลก", "Exchange", "Tukar", "교환" }
		},
		{
			"done_t2g",
			new string[4] { "แลก T-Stone {0:N0} → Gem {1:N0} แล\u0e49ว", "Exchanged {0:N0} T-Stone for {1:N0} Gem", "Menukar {0:N0} T-Stone menjadi {1:N0} Gem", "T-Stone {0:N0} → 젬 {1:N0} 교환 완료" }
		},
		{
			"done_g2t",
			new string[4] { "แลก Gem {0:N0} → T-Stone {1:N0} แล\u0e49ว", "Exchanged {0:N0} Gem for {1:N0} T-Stone", "Menukar {0:N0} Gem menjadi {1:N0} T-Stone", "젬 {0:N0} → T-Stone {1:N0} 교환 완료" }
		},
		{
			"busy",
			new string[4] { "กำล\u0e31งแลกอย\u0e39\u0e48 — รอส\u0e31กคร\u0e39\u0e48", "Exchange in progress — please wait", "Sedang menukar — tunggu sebentar", "교환 중입니다. 잠시 기다려 주세요." }
		},
		{
			"failed",
			new string[4] { "แลกไม\u0e48สำเร\u0e47จ", "Exchange failed", "Penukaran gagal", "교환 실패" }
		},
		{
			"fail_tstone",
			new string[4] { "T-Stone ไม\u0e48พอ", "Not enough T-Stone", "T-Stone tidak cukup", "T-Stone이 부족합니다" }
		},
		{
			"fail_tstone_min",
			new string[4] { "T-Stone ไม\u0e48พอ (ต\u0e49องม\u0e35อย\u0e48างน\u0e49อย {0:N0})", "Not enough T-Stone (need at least {0:N0})", "T-Stone tidak cukup (minimal {0:N0})", "T-Stone이 부족합니다 (최소 {0:N0})" }
		},
		{
			"fail_gem",
			new string[4] { "Gem ไม\u0e48พอ", "Not enough Gem", "Gem tidak cukup", "젬이 부족합니다" }
		},
		{
			"fail_full",
			new string[4] { "ยอดปลายทางเต\u0e47มแล\u0e49ว", "Balance limit reached", "Saldo sudah maksimal", "보유 한도를 초과합니다" }
		},
		{
			"fail_frozen",
			new string[4] { "บ\u0e31ญช\u0e35ถ\u0e39กระง\u0e31บการเง\u0e34นช\u0e31\u0e48วคราว ต\u0e34ดต\u0e48อ GM", "Your wallet is frozen — contact a GM", "Dompetmu dibekukan — hubungi GM", "재화 사용이 일시 정지되었습니다. GM에게 문의하세요." }
		},
		{
			"fail_bad_amount",
			new string[4] { "จำนวนไม\u0e48ถ\u0e39กต\u0e49อง", "Invalid amount", "Jumlah tidak valid", "잘못된 수량입니다" }
		},
		{
			"fail_disabled",
			new string[4] { "ป\u0e34ดการแลกช\u0e31\u0e48วคราว", "Exchange is disabled for now", "Penukaran sedang dinonaktifkan", "교환이 일시 중지되었습니다" }
		},
		{
			"fail_rate_limited",
			new string[4] { "แลกถ\u0e35\u0e48เก\u0e34นไป — รอส\u0e31กคร\u0e39\u0e48", "Too many exchanges — wait a moment", "Terlalu sering — tunggu sebentar", "너무 자주 교환했습니다. 잠시 후 다시 시도하세요." }
		},
		{
			"fail_unsupported",
			new string[4] { "เซ\u0e34ร\u0e4cฟน\u0e35\u0e49ย\u0e31งไม\u0e48รองร\u0e31บการแลก", "This server doesn't support exchange yet", "Server ini belum mendukung penukaran", "이 서버는 아직 교환을 지원하지 않습니다" }
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
				text = OffServerLink.Gateway;
			}
			return (text ?? string.Empty).TrimEnd('/');
		}
	}

	public static bool Available
	{
		get
		{
			if (Enabled && OffServerLink.Active && GatewayUrl.Length > 0)
			{
				return !string.IsNullOrEmpty(GameManager.SessionToken);
			}
			return false;
		}
	}

	private static long Balance(Currency currency)
	{
		try
		{
			return InventorySystem.Wallet.GetBalance(currency);
		}
		catch (Exception)
		{
			return 0L;
		}
	}

	public static void Open()
	{
		if (Busy)
		{
			UIManager.SystemMsg(Text("busy"));
			return;
		}
		MessageBox messageBox = UIManager.MessageBox;
		messageBox.SetCurrencyInfo(Currency.TStone);
		messageBox.SetCurrencyInfo(Currency.Gem);
		string text = Inventory.CurrencyFormat(Rate, Inventory.GetIcon(Currency.TStone));
		string text2 = Inventory.CurrencyFormat(1L, Inventory.GetIcon(Currency.Gem));
		messageBox.Show(Text("title"), Text("rate", text, text2), delegate(int index)
		{
			switch (index)
			{
			case 0:
				AskAmount("t2g");
				break;
			case 1:
				AskAmount("g2t");
				break;
			}
		}, new MessageBox.Button(Text("btn_t2g")), new MessageBox.Button(Text("btn_g2t")), Text("close"));
	}

	private static void AskAmount(string dir)
	{
		int num = Math.Max(1, Rate);
		long num2 = ((dir == "t2g") ? (Balance(Currency.TStone) / num) : Balance(Currency.Gem));
		if (num2 <= 0)
		{
			UIManager.SystemMsg((dir == "t2g") ? Text("fail_tstone_min", num) : Text("fail_gem"));
		}
		else
		{
			UIManager.Popup.Tooltip<NumberInputPopup>().Show(1L, Currency.Gem, Text((dir == "t2g") ? "amount_t2g" : "amount_g2t"), delegate(long value)
			{
				Confirm(dir, value);
			}, num2);
		}
	}

	private static void Confirm(string dir, long gem)
	{
		if (gem <= 0)
		{
			return;
		}
		int num = Math.Max(1, Rate);
		long num2 = gem * num;
		bool flag = dir == "t2g";
		long num3 = (flag ? num2 : gem);
		Currency currency = ((!flag) ? Currency.Gem : Currency.TStone);
		if (num3 > Balance(currency))
		{
			UIManager.SystemMsg(Text(flag ? "fail_tstone" : "fail_gem"));
			return;
		}
		string text = (flag ? Inventory.CurrencyFormat(gem, Inventory.GetIcon(Currency.Gem)) : Inventory.CurrencyFormat(num2, Inventory.GetIcon(Currency.TStone)));
		UIManager.MessageBox.ShowPayConfirm(num3, currency, Text("confirm", text), delegate(bool ok)
		{
			if (ok)
			{
				Exchange(dir, gem);
			}
		}, Text("btn_exchange"));
	}

	public static void Exchange(string dir, long gem, Action<bool, string> done = null)
	{
		if (!Available)
		{
			LastEvent = "unavailable";
			UIManager.SystemMsg(Text("fail_offline"));
			if (done != null)
			{
				done(arg1: false, "unavailable");
			}
			return;
		}
		Post("/wallet/exchange", new Dictionary<string, string>
		{
			{ "dir", dir },
			{
				"gem",
				gem.ToString()
			}
		}, delegate(JObject o, int http)
		{
			bool flag = o != null && o["ok"] != null && o["ok"].Type == JTokenType.Boolean && (bool)o["ok"];
			string text = ((o != null && o["reason"] != null) ? (((string)o["reason"]) ?? string.Empty) : string.Empty);
			if (o != null && o["rate"] != null && o["rate"].Type == JTokenType.Integer && (int)o["rate"] > 0)
			{
				Rate = (int)o["rate"];
			}
			if (flag)
			{
				long num = gem * Math.Max(1, Rate);
				LastEvent = "ok " + dir + " " + gem;
				UnityEngine.Debug.Log("[OffServer] wallet exchange " + dir + " gem=" + gem + " → tStone=" + ((o["tStone"] != null) ? o["tStone"].ToString() : "?") + " gem=" + ((o["gem"] != null) ? o["gem"].ToString() : "?"));
				UIManager.SystemMsg((dir == "t2g") ? Text("done_t2g", num, gem) : Text("done_g2t", gem, num));
			}
			else
			{
				if (text.Length == 0)
				{
					text = ((http == 404) ? "unsupported" : ((o == null) ? "offline" : "failed"));
				}
				LastEvent = "fail HTTP " + http + " " + text;
				UnityEngine.Debug.Log("[OffServer] wallet exchange " + LastEvent);
				UIManager.SystemMsg(Texts.ContainsKey("fail_" + text) ? Text("fail_" + text, Math.Max(1, Rate)) : (Text("failed") + " (" + text + ")"));
			}
			if (done != null)
			{
				done(flag, text);
			}
		});
	}

	private static void Post(string path, Dictionary<string, string> fields, Action<JObject, int> done)
	{
		if (Busy)
		{
			LastEvent = "busy";
			done(null, 0);
			return;
		}
		Busy = true;
		HTTPRequest hTTPRequest = new HTTPRequest(new Uri(GatewayUrl + path), HTTPMethods.Post, delegate(HTTPRequest req, HTTPResponse resp)
		{
			Busy = false;
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
				UnityEngine.Debug.Log("[OffServer] wallet " + path + " ล\u0e49ม: " + req.State.ToString() + " HTTP " + arg + ((req.Exception != null) ? (" " + req.Exception.Message) : string.Empty));
			}
			done(jObject, arg);
		});
		hTTPRequest.FormUsage = HTTPFormUsage.UrlEncoded;
		hTTPRequest.AddHeader("Authorization", GameManager.SessionToken);
		hTTPRequest.AddHeader("Accept", "application/json");
		hTTPRequest.AddHeader("X-OffServer-Client", OffServerLink.ClientVersion);
		hTTPRequest.AddHeader("X-OffServer-Caps", "nametag-emoji, item-link, server-popup, voice-chat, i18n-keys, pet-scale, pvp-ranked, pvp-duel, gather-check, rift-coin, rift-scope");
		hTTPRequest.AddField("lang", OffServerDiscordLink.Lang);
		foreach (KeyValuePair<string, string> field in fields)
		{
			hTTPRequest.AddField(field.Key, field.Value);
		}
		hTTPRequest.ConnectTimeout = TimeSpan.FromSeconds(10.0);
		hTTPRequest.Timeout = TimeSpan.FromSeconds(15.0);
		hTTPRequest.DisableCache = true;
		hTTPRequest.Send();
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		string text = OffServerDiscordLink.Lang switch
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

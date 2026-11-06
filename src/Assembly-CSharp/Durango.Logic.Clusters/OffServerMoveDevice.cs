using System;
using System.Collections.Generic;
using BestHTTP;
using BestHTTP.Forms;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Durango.Logic.Clusters;

public static class OffServerMoveDevice
{
	public const string SettingKey = "move_device";

	public const string RedeemSettingKey = "move_redeem";

	private const int HttpTimeoutSec = 20;

	private static bool s_busy;

	public static string LastCode = string.Empty;

	public static string LastEvent = string.Empty;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"button",
			new string[4] { "ย\u0e49ายเคร\u0e37\u0e48อง", "Move Device", "Pindah Perangkat", "기기 이전" }
		},
		{
			"redeem_button",
			new string[4] { "กรอกรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง", "Enter Move Code", "Masukkan Kode Pindah", "이전 코드 입력" }
		},
		{
			"code_title",
			new string[4] { "รห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง: {0}", "Move code: {0}", "Kode pindah: {0}", "이전 코드: {0}" }
		},
		{
			"code_howto",
			new string[4] { "ท\u0e35\u0e48เคร\u0e37\u0e48องใหม\u0e48: เข\u0e49าเกม > ต\u0e31\u0e49งค\u0e48า > บ\u0e31ญช\u0e35 > กรอกรห\u0e31สย\u0e49ายเคร\u0e37\u0e48อง > ใส\u0e48รห\u0e31สน\u0e35\u0e49\nใช\u0e49ได\u0e49คร\u0e31\u0e49งเด\u0e35ยว ภายใน {0} นาท\u0e35 — อย\u0e48าบอกรห\u0e31สน\u0e35\u0e49ก\u0e31บคนอ\u0e37\u0e48น", "On the new device: enter the game > Settings > Account > Enter Move Code > type this code.\nOne use only, within {0} minutes. Don't share this code.", "Di perangkat baru: masuk game > Pengaturan > Akun > Masukkan Kode Pindah > ketik kode ini.\nSekali pakai, dalam {0} menit. Jangan bagikan kode ini.", "새 기기에서: 게임 접속 > 설정 > 계정 > 이전 코드 입력 > 이 코드를 입력하세요.\n1회용이며 {0}분 안에 사용하세요. 다른 사람에게 알려주지 마세요." }
		},
		{
			"moved_title",
			new string[4] { "ย\u0e49ายเคร\u0e37\u0e48องสำเร\u0e47จ", "Device moved", "Perangkat dipindahkan", "기기 이전 완료" }
		},
		{
			"moved",
			new string[4] { "บ\u0e31ญช\u0e35เด\u0e34มผ\u0e39กก\u0e31บเคร\u0e37\u0e48องน\u0e35\u0e49แล\u0e49ว — กด OK เพ\u0e37\u0e48อกล\u0e31บหน\u0e49า Title แล\u0e49วเข\u0e49าใหม\u0e48เพ\u0e37\u0e48อโหลดต\u0e31วละครของบ\u0e31ญช\u0e35น\u0e35\u0e49", "Your account is now on this device. Press OK to go back to the title screen and enter again to load its characters.", "Akun Anda sekarang ada di perangkat ini. Tekan OK untuk kembali ke layar judul lalu masuk lagi untuk memuat karakternya.", "이제 이 기기에서 계정을 사용할 수 있습니다. OK를 눌러 타이틀 화면으로 돌아간 뒤 다시 접속하면 캐릭터를 불러옵니다." }
		},
		{
			"not_online",
			new string[4] { "ต\u0e49องเข\u0e49าเซ\u0e34ร\u0e4cฟก\u0e48อนถ\u0e36งจะขอรห\u0e31สย\u0e49ายเคร\u0e37\u0e48องได\u0e49", "Connect to a server first to get a move code.", "Masuk ke server dulu untuk mendapatkan kode pindah.", "이전 코드를 받으려면 먼저 서버에 접속하세요." }
		},
		{
			"net_error",
			new string[4] { "ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Couldn't reach the server. Please try again.", "Tidak dapat menghubungi server. Silakan coba lagi.", "서버에 연결할 수 없습니다. 다시 시도하세요." }
		},
		{
			"failed",
			new string[4] { "ขอรห\u0e31สย\u0e49ายเคร\u0e37\u0e48องไม\u0e48สำเร\u0e47จ", "Couldn't get a move code", "Gagal mendapatkan kode pindah", "이전 코드를 받지 못했습니다" }
		},
		{
			"fail_rate_limited",
			new string[4] { "ขอรห\u0e31สถ\u0e35\u0e48เก\u0e34นไป — รอส\u0e31กคร\u0e39\u0e48แล\u0e49วลองใหม\u0e48", "Too many requests — wait a moment and try again.", "Terlalu sering — tunggu sebentar lalu coba lagi.", "요청이 너무 많습니다 — 잠시 후 다시 시도하세요." }
		},
		{
			"fail_disabled",
			new string[4] { "ระบบย\u0e49ายเคร\u0e37\u0e48องด\u0e49วยรห\u0e31สป\u0e34ดอย\u0e39\u0e48ช\u0e31\u0e48วคราว", "Moving with a code is turned off for now.", "Pindah dengan kode sedang dinonaktifkan.", "코드로 기기 이전은 현재 꺼져 있습니다." }
		},
		{
			"fail_no_account",
			new string[4] { "บ\u0e31ญช\u0e35น\u0e35\u0e49ย\u0e31งไม\u0e48ม\u0e35ต\u0e31วละคร — สร\u0e49างต\u0e31วละครก\u0e48อนแล\u0e49วค\u0e48อยขอรห\u0e31ส", "This account has no character yet — create one first.", "Akun ini belum punya karakter — buat dulu.", "이 계정에는 아직 캐릭터가 없습니다 — 먼저 캐릭터를 만드세요." }
		},
		{
			"fail_unsupported",
			new string[4] { "เซ\u0e34ร\u0e4cฟน\u0e35\u0e49ย\u0e31งไม\u0e48รองร\u0e31บการย\u0e49ายเคร\u0e37\u0e48องด\u0e49วยรห\u0e31ส — รอเซ\u0e34ร\u0e4cฟอ\u0e31ปเดตก\u0e48อน", "This server doesn't support move codes yet — wait for the server update.", "Server ini belum mendukung kode pindah — tunggu pembaruan server.", "이 서버는 아직 이전 코드를 지원하지 않습니다 — 서버 업데이트를 기다려 주세요." }
		},
		{
			"fail_unauthorized",
			new string[4] { "ย\u0e37นย\u0e31นต\u0e31วไม\u0e48ผ\u0e48าน — กล\u0e31บหน\u0e49า Title แล\u0e49วเข\u0e49าใหม\u0e48 แล\u0e49วลองอ\u0e35กคร\u0e31\u0e49ง", "Session check failed — go back to the title screen, enter again and retry.", "Verifikasi sesi gagal — kembali ke layar judul, masuk lagi lalu coba lagi.", "세션 확인 실패 — 타이틀 화면으로 돌아가 다시 접속한 뒤 시도하세요." }
		}
	};

	public static string ButtonLabel => Text("button");

	public static string RedeemButtonLabel => Text("redeem_button");

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

	public static bool LooksLikeCode(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		int num = 0;
		foreach (char c in text)
		{
			if (c >= '0' && c <= '9')
			{
				num++;
			}
			else if (c != '-' && !char.IsWhiteSpace(c))
			{
				return false;
			}
		}
		return num == 9;
	}

	public static void Begin()
	{
		if (!OffServerLink.Active || GatewayUrl.Length == 0 || string.IsNullOrEmpty(GameManager.SessionToken))
		{
			Popup(null, Text("not_online"));
		}
		else
		{
			if (s_busy)
			{
				return;
			}
			s_busy = true;
			HTTPRequest hTTPRequest = new HTTPRequest(new Uri(GatewayUrl + "/account/move-code"), HTTPMethods.Post, delegate(HTTPRequest req, HTTPResponse resp)
			{
				s_busy = false;
				JObject jObject = null;
				int num = resp?.StatusCode ?? 0;
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
					LastEvent = "error " + req.State.ToString() + " " + num;
					UnityEngine.Debug.Log("[OffServer] Move Device ล\u0e49ม: " + req.State.ToString() + " HTTP " + num + ((req.Exception != null) ? (" " + req.Exception.Message) : string.Empty));
					Popup(null, Text("net_error"));
				}
				else
				{
					string text = Str(jObject, "code");
					if (!Bool(jObject, "ok") || text.Length != 9)
					{
						string text2 = Str(jObject, "reason");
						if (text2.Length == 0 && num == 404)
						{
							text2 = "unsupported";
						}
						LastEvent = "HTTP " + num + " ok=false reason=" + text2;
						UnityEngine.Debug.Log("[OffServer] Move Device " + LastEvent);
						Popup(null, Texts.ContainsKey("fail_" + text2) ? Text("fail_" + text2) : (Text("failed") + " (" + text2 + ")"));
					}
					else
					{
						LastCode = text;
						LastEvent = "HTTP " + num + " ok=true code=" + text.Substring(0, 3) + "…";
						UnityEngine.Debug.Log("[OffServer] Move Device " + LastEvent);
						int num2 = 10;
						JToken jToken = jObject["expires_in"];
						if (jToken != null && jToken.Type == JTokenType.Integer)
						{
							num2 = Math.Max(1, (int)jToken / 60);
						}
						Popup(Text("code_title", text.Substring(0, 3) + " " + text.Substring(3, 3) + " " + text.Substring(6, 3)), Text("code_howto", num2));
					}
				}
			});
			hTTPRequest.FormUsage = HTTPFormUsage.UrlEncoded;
			hTTPRequest.AddHeader("Authorization", GameManager.SessionToken);
			hTTPRequest.AddHeader("Accept", "application/json");
			hTTPRequest.AddHeader("X-OffServer-Client", OffServerLink.ClientVersion);
			hTTPRequest.AddHeader("X-OffServer-Caps", "nametag-emoji, item-link, server-popup, voice-chat, i18n-keys, pet-scale, pvp-ranked, pvp-duel, gather-check, rift-coin, rift-scope");
			hTTPRequest.AddField("lang", OffServerDiscordLink.Lang);
			hTTPRequest.ConnectTimeout = TimeSpan.FromSeconds(10.0);
			hTTPRequest.Timeout = TimeSpan.FromSeconds(20.0);
			hTTPRequest.DisableCache = true;
			hTTPRequest.Send();
		}
	}

	public static void OnRedeemed()
	{
		LastEvent = "redeemed";
		UnityEngine.Debug.Log("[OffServer] Move Device: กรอกรห\u0e31สสำเร\u0e47จ → กล\u0e31บหน\u0e49า Title");
		try
		{
			UIManager.MessageBox.Show(OffServerDiscordLink.Clean(Text("moved_title")), OffServerDiscordLink.Clean(Text("moved")), (Action)delegate
			{
				try
				{
					Singleton<GameManager>.Instance().MoveToTitle();
				}
				catch (Exception ex2)
				{
					UnityEngine.Debug.LogWarning("[OffServer] Move Device: กล\u0e31บหน\u0e49า Title ไม\u0e48ได\u0e49: " + ex2.Message);
				}
			}, (string)null);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning("[OffServer] Move Device popup: " + ex.Message);
		}
	}

	private static void Popup(string title, string message)
	{
		try
		{
			if (string.IsNullOrEmpty(title))
			{
				UIManager.MessageBox.Show(OffServerDiscordLink.Clean(message));
			}
			else
			{
				UIManager.MessageBox.Show(OffServerDiscordLink.Clean(title), OffServerDiscordLink.Clean(message));
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning("[OffServer] Move Device popup: " + ex.Message);
		}
	}

	private static bool Bool(JObject o, string key)
	{
		JToken jToken = o?[key];
		if (jToken != null && jToken.Type == JTokenType.Boolean)
		{
			return (bool)jToken;
		}
		return false;
	}

	private static string Str(JObject o, string key)
	{
		JToken jToken = o?[key];
		if (jToken == null || jToken.Type == JTokenType.Null)
		{
			return string.Empty;
		}
		return jToken.ToString();
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BestHTTP;
using BestHTTP.Forms;
using Durango.UI;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Durango.Logic.Clusters;

public class OffServerDiscordLink : MonoBehaviour
{
	public const string SettingKey = "discord_link";

	public static readonly int[] DefaultPorts = new int[3] { 38431, 38432, 38433 };

	private const float WaitSeconds = 300f;

	private const int HttpTimeoutSec = 20;

	private const string ReturnScheme = "durangooffserver";

	private static bool s_answerCallbackAtOnce;

	private static string s_returnToGameUrl = string.Empty;

	private static OffServerDiscordLink _instance;

	public static string LastAuthorizeUrl = string.Empty;

	public static string LastEvent = string.Empty;

	private readonly Queue<Action> _main = new Queue<Action>();

	private TcpListener _listener;

	private volatile bool _listening;

	private int _port;

	private string _state;

	private string _redirectUri;

	private float _deadline;

	private bool _waiting;

	private bool _finishing;

	private int _generation;

	public static string LastCaller = string.Empty;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"button",
			new string[4] { "Link Discord", "Link Discord", "Link Discord", "Link Discord" }
		},
		{
			"button_linked",
			new string[4] { "ผ\u0e39กก\u0e31บ @{0} ✓", "Linked: @{0} ✓", "Tertaut: @{0} ✓", "연동됨: @{0} ✓" }
		},
		{
			"opened",
			new string[4] { "เป\u0e34ดเบราว\u0e4cเซอร\u0e4cแล\u0e49ว — กด «อน\u0e38ญาต» (Authorize) ใน Discord แล\u0e49วกล\u0e31บมาท\u0e35\u0e48เกม", "Browser opened — press Authorize on Discord, then come back to the game.", "Browser dibuka — tekan Authorize di Discord, lalu kembali ke game.", "브라우저가 열렸습니다 — Discord에서 승인(Authorize)을 누른 뒤 게임으로 돌아오세요." }
		},
		{
			"waiting_again",
			new string[4] { "ย\u0e31งรอผลจาก Discord อย\u0e39\u0e48 — เป\u0e34ดหน\u0e49าเบราว\u0e4cเซอร\u0e4cให\u0e49อ\u0e35กคร\u0e31\u0e49งแล\u0e49ว", "Still waiting for Discord — the page was opened again.", "Masih menunggu Discord — halaman dibuka lagi.", "아직 Discord 응답을 기다리는 중입니다 — 페이지를 다시 열었습니다." }
		},
		{
			"finishing",
			new string[4] { "กำล\u0e31งผ\u0e39กก\u0e31บ Discord อย\u0e39\u0e48 รอส\u0e31กคร\u0e39\u0e48…", "Linking with Discord, please wait…", "Sedang menautkan dengan Discord, tunggu sebentar…", "Discord 연동 중입니다. 잠시만 기다려 주세요…" }
		},
		{
			"timeout",
			new string[4] { "หมดเวลารอ Discord (5 นาท\u0e35) — กด Link Discord ใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Timed out waiting for Discord (5 minutes). Press Link Discord again.", "Waktu menunggu Discord habis (5 menit). Tekan Link Discord lagi.", "Discord 응답 대기 시간이 초과되었습니다(5분). Link Discord를 다시 누르세요." }
		},
		{
			"no_port",
			new string[4] { "เป\u0e34ดช\u0e48องร\u0e31บผลจาก Discord ในเคร\u0e37\u0e48องไม\u0e48ได\u0e49 (พอร\u0e4cต {0} ถ\u0e39กโปรแกรมอ\u0e37\u0e48นใช\u0e49อย\u0e39\u0e48) — ป\u0e34ดโปรแกรมน\u0e31\u0e49นแล\u0e49วลองใหม\u0e48 หร\u0e37อใช\u0e49 panel ในด\u0e34สแทน", "Couldn't open the local callback port ({0} are used by another program). Close that program and try again, or use the panel in Discord instead.", "Tidak bisa membuka port callback lokal ({0} dipakai program lain). Tutup program itu lalu coba lagi, atau gunakan panel di Discord.", "로컬 콜백 포트를 열 수 없습니다({0} 포트를 다른 프로그램이 사용 중). 해당 프로그램을 닫고 다시 시도하거나 Discord 패널을 이용하세요." }
		},
		{
			"net_error",
			new string[4] { "ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Couldn't reach the server. Please try again.", "Tidak dapat menghubungi server. Silakan coba lagi.", "서버에 연결할 수 없습니다. 다시 시도하세요." }
		},
		{
			"not_online",
			new string[4] { "ต\u0e49องเข\u0e49าเซ\u0e34ร\u0e4cฟก\u0e48อนถ\u0e36งจะผ\u0e39ก Discord ได\u0e49", "Connect to a server first to link Discord.", "Masuk ke server dulu untuk menautkan Discord.", "Discord를 연동하려면 먼저 서버에 접속하세요." }
		},
		{
			"disabled",
			new string[4] { "ป\u0e38\u0e48มน\u0e35\u0e49ป\u0e34ดอย\u0e39\u0e48 — ใช\u0e49 panel ในด\u0e34สแทน", "This button is turned off — use the panel in Discord instead.", "Tombol ini sedang nonaktif — gunakan panel di Discord.", "이 버튼은 꺼져 있습니다 — Discord 패널을 이용하세요." }
		},
		{
			"failed",
			new string[4] { "ผ\u0e39กไม\u0e48สำเร\u0e47จ", "Link failed", "Gagal menautkan", "연동 실패" }
		},
		{
			"linked_ask",
			new string[4] { "บ\u0e31ญช\u0e35น\u0e35\u0e49ผ\u0e39กก\u0e31บ Discord @{0} อย\u0e39\u0e48แล\u0e49ว ✓\nต\u0e49องการผ\u0e39ก Discord บ\u0e31ญช\u0e35อ\u0e37\u0e48นแทนไหม?", "This account is already linked with Discord @{0} ✓\nLink a different Discord account instead?", "Akun ini sudah tertaut dengan Discord @{0} ✓\nTautkan akun Discord lain sebagai gantinya?", "이 계정은 이미 Discord @{0}와(과) 연동되어 있습니다 ✓\n다른 Discord 계정으로 연동하시겠습니까?" }
		},
		{
			"page_state_title",
			new string[4] { "❌ ล\u0e34งก\u0e4cน\u0e35\u0e49ไม\u0e48ตรงก\u0e31บคำขอของเกม", "❌ This link doesn't match the game's request", "❌ Tautan ini tidak cocok dengan permintaan game", "❌ 게임의 요청과 일치하지 않는 링크입니다" }
		},
		{
			"page_state",
			new string[4] { "กล\u0e31บไปท\u0e35\u0e48เกมแล\u0e49วกด Link Discord ใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง — ป\u0e34ดแท\u0e47บน\u0e35\u0e49ได\u0e49", "Go back to the game and press Link Discord again. You can close this tab.", "Kembali ke game dan tekan Link Discord lagi. Tab ini boleh ditutup.", "게임으로 돌아가 Link Discord를 다시 누르세요. 이 탭은 닫아도 됩니다." }
		},
		{
			"page_return_title",
			new string[4] { "ได\u0e49ร\u0e31บผลจาก Discord แล\u0e49ว", "Discord answered", "Discord sudah menjawab", "Discord 응답을 받았습니다" }
		},
		{
			"page_return",
			new string[4] { "กล\u0e31บเข\u0e49าเกมเพ\u0e37\u0e48อผ\u0e39กให\u0e49เสร\u0e47จ — ผลจะข\u0e36\u0e49นในเกม", "Go back to the game to finish linking — the result shows in the game.", "Kembali ke game untuk menyelesaikan penautan — hasilnya muncul di game.", "게임으로 돌아가 연동을 마치세요 — 결과는 게임에 표시됩니다." }
		},
		{
			"page_return_button",
			new string[4] { "กล\u0e31บเข\u0e49าเกม", "Back to the game", "Kembali ke game", "게임으로 돌아가기" }
		},
		{
			"page_net_title",
			new string[4] { "❌ ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟเกมไม\u0e48ได\u0e49", "❌ Couldn't reach the game server", "❌ Tidak dapat menghubungi server game", "❌ 게임 서버에 연결할 수 없습니다" }
		},
		{
			"page_net",
			new string[4] { "กล\u0e31บไปท\u0e35\u0e48เกมแล\u0e49วลองกด Link Discord ใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง — ป\u0e34ดแท\u0e47บน\u0e35\u0e49ได\u0e49", "Go back to the game and try Link Discord again. You can close this tab.", "Kembali ke game dan coba Link Discord lagi. Tab ini boleh ditutup.", "게임으로 돌아가 Link Discord를 다시 시도하세요. 이 탭은 닫아도 됩니다." }
		}
	};

	public static bool Busy
	{
		get
		{
			if (_instance != null)
			{
				if (!_instance._waiting)
				{
					return _instance._finishing;
				}
				return true;
			}
			return false;
		}
	}

	public static int Port
	{
		get
		{
			if (!(_instance != null))
			{
				return 0;
			}
			return _instance._port;
		}
	}

	private static OffServerDiscordLink Instance
	{
		get
		{
			if (_instance == null)
			{
				GameObject obj = new GameObject("OffServerDiscordLink");
				UnityEngine.Object.DontDestroyOnLoad(obj);
				_instance = obj.AddComponent<OffServerDiscordLink>();
			}
			return _instance;
		}
	}

	public static string Lang
	{
		get
		{
			string text = (LocalizeSystem.Locale ?? string.Empty).ToLowerInvariant();
			if (text.StartsWith("id") || text.StartsWith("in"))
			{
				return "id";
			}
			if (text.StartsWith("ko"))
			{
				return "ko";
			}
			return "en";
		}
	}

	public static string ButtonLabel
	{
		get
		{
			string discordName = OffServerLink.DiscordName;
			if (string.IsNullOrEmpty(discordName))
			{
				return Text("button");
			}
			return Text("button_linked", discordName);
		}
	}

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

	public static void Begin(bool openBrowser = true)
	{
		LastCaller = global::System.Environment.StackTrace;
		s_answerCallbackAtOnce = Application.platform == RuntimePlatform.Android;
		s_returnToGameUrl = "intent://discord-link#Intent;scheme=durangooffserver;package=" + Application.identifier + ";end";
		if (openBrowser && OffServerLink.TestBridgePort > 0 && global::System.Environment.GetEnvironmentVariable("OFFSERVER_TEST_NO_BROWSER") == "1")
		{
			openBrowser = false;
		}
		Instance.BeginInternal(openBrowser);
	}

	public static void Cancel()
	{
		if (_instance != null)
		{
			_instance.StopWaiting();
		}
	}

	private void BeginInternal(bool openBrowser)
	{
		if (!OffServerLink.Active || GatewayUrl.Length == 0 || string.IsNullOrEmpty(GameManager.SessionToken))
		{
			Popup(null, Text("not_online"));
			return;
		}
		if (_finishing)
		{
			Toast(Text("finishing"));
			return;
		}
		if (_waiting)
		{
			if (openBrowser && LastAuthorizeUrl.Length > 0)
			{
				Application.OpenURL(LastAuthorizeUrl);
			}
			Toast(Text("waiting_again"));
			return;
		}
		Post("status", null, delegate(JObject o)
		{
			if (o == null)
			{
				Popup(null, Text("net_error"));
			}
			else if (!IsOk(o))
			{
				PopupFail(o);
			}
			else if (!Bool(o, "enabled"))
			{
				string text = Str(o, "message");
				Popup(null, (text.Length > 0) ? text : Text("disabled"));
			}
			else
			{
				int[] ports = Ports(o);
				if (Bool(o, "linked"))
				{
					string text2 = Str(o, "discord_name");
					if (text2.Length > 0 && text2 != OffServerLink.DiscordName)
					{
						OffServerLink.DiscordName = text2;
						RefreshUi();
					}
					UIManager.MessageBox.Show(Clean(Text("linked_ask", (text2.Length > 0) ? text2 : "?")), delegate(bool ok)
					{
						if (ok)
						{
							StartFlow(ports, openBrowser);
						}
					});
				}
				else
				{
					StartFlow(ports, openBrowser);
				}
			}
		});
	}

	private void StartFlow(int[] ports, bool openBrowser)
	{
		StopWaiting();
		TcpListener tcpListener = null;
		int port = 0;
		foreach (int num in ports)
		{
			try
			{
				TcpListener tcpListener2 = new TcpListener(IPAddress.Loopback, num);
				tcpListener2.Start();
				tcpListener = tcpListener2;
				port = num;
			}
			catch (Exception)
			{
				continue;
			}
			break;
		}
		if (tcpListener == null)
		{
			string[] array = new string[ports.Length];
			for (int j = 0; j < ports.Length; j++)
			{
				array[j] = ports[j].ToString();
			}
			Popup(null, Text("no_port", string.Join(", ", array)));
			return;
		}
		int generation = ++_generation;
		_listener = tcpListener;
		_port = port;
		_redirectUri = "http://127.0.0.1:" + port + "/callback";
		_state = null;
		_waiting = true;
		_deadline = Time.realtimeSinceStartup + 300f;
		_listening = true;
		Thread thread = new Thread(AcceptLoop);
		thread.IsBackground = true;
		thread.Name = "OffServerDiscordLink";
		thread.Start(tcpListener);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary["redirect_uri"] = _redirectUri;
		dictionary["account_id"] = OffServerLink.AccountId;
		if (!GameManager.IsTitleScene && !string.IsNullOrEmpty(GameManager.PlayerId))
		{
			dictionary["entity_id"] = GameManager.PlayerId;
		}
		Post("start", dictionary, delegate(JObject o)
		{
			if (generation == _generation && _waiting)
			{
				string text = ((o == null) ? string.Empty : Str(o, "url"));
				if (o == null || !IsOk(o) || text.Length == 0)
				{
					StopWaiting();
					if (o == null)
					{
						Popup(null, Text("net_error"));
					}
					else
					{
						PopupFail(o);
					}
				}
				else
				{
					_state = Str(o, "state");
					string text2 = Str(o, "redirect_uri");
					if (text2.Length > 0)
					{
						_redirectUri = text2;
					}
					LastAuthorizeUrl = text;
					if (openBrowser)
					{
						Application.OpenURL(text);
					}
					Toast(Text("opened"));
				}
			}
		});
	}

	private void StopWaiting()
	{
		_waiting = false;
		_listening = false;
		_generation++;
		TcpListener listener = _listener;
		_listener = null;
		if (listener != null)
		{
			try
			{
				listener.Stop();
			}
			catch (Exception)
			{
			}
		}
	}

	private void AcceptLoop(object arg)
	{
		TcpListener tcpListener = (TcpListener)arg;
		while (_listening)
		{
			TcpClient parameter;
			try
			{
				parameter = tcpListener.AcceptTcpClient();
			}
			catch (Exception)
			{
				break;
			}
			Thread thread = new Thread(HandleClient);
			thread.IsBackground = true;
			thread.Start(parameter);
		}
	}

	private void HandleClient(object arg)
	{
		TcpClient tcpClient = (TcpClient)arg;
		try
		{
			tcpClient.ReceiveTimeout = 10000;
			tcpClient.SendTimeout = 10000;
			string[] array = (ReadRequestLine(tcpClient.GetStream()) ?? string.Empty).Split(' ');
			if (array.Length < 2 || array[0] != "GET")
			{
				WriteResponse(tcpClient, "405 Method Not Allowed", "text/plain; charset=utf-8", "method not allowed");
				return;
			}
			string text = array[1];
			string query = string.Empty;
			int num = text.IndexOf('?');
			if (num >= 0)
			{
				query = text.Substring(num + 1);
				text = text.Substring(0, num);
			}
			if (text != "/callback")
			{
				WriteResponse(tcpClient, "404 Not Found", "text/plain; charset=utf-8", "not found");
				return;
			}
			Dictionary<string, string> query2 = ParseQuery(query);
			TcpClient pendingClient = tcpClient;
			if (s_answerCallbackAtOnce)
			{
				WriteResponse(tcpClient, "200 OK", "text/html; charset=utf-8", ReturnToGamePage());
				pendingClient = null;
			}
			lock (_main)
			{
				_main.Enqueue(delegate
				{
					OnCallback(pendingClient, query2);
				});
			}
		}
		catch (Exception)
		{
			try
			{
				tcpClient.Close();
			}
			catch (Exception)
			{
			}
		}
	}

	private void OnCallback(TcpClient client, Dictionary<string, string> query)
	{
		string text = Get(query, "state");
		string text2 = Get(query, "code");
		string text3 = Get(query, "error");
		if (!_waiting || _finishing || string.IsNullOrEmpty(_state) || text != _state)
		{
			LastEvent = "callback state_mismatch";
			if (client != null)
			{
				WriteResponse(client, "400 Bad Request", "text/html; charset=utf-8", LocalPage("page_state"));
			}
			else
			{
				Popup(Text("page_state_title"), Text("page_state"));
			}
			return;
		}
		string state = _state;
		string redirectUri = _redirectUri;
		StopWaiting();
		_finishing = true;
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary["state"] = state;
		dictionary["redirect_uri"] = redirectUri;
		if (text2.Length > 0)
		{
			dictionary["code"] = text2;
		}
		else
		{
			dictionary["error"] = ((text3.Length > 0) ? text3 : "access_denied");
		}
		Post("finish", dictionary, delegate(JObject o)
		{
			_finishing = false;
			if (client != null)
			{
				WriteResponse(client, "200 OK", "text/html; charset=utf-8", (o != null) ? LocalPage("page_return") : LocalPage("page_net"));
			}
			if (o == null)
			{
				Popup(null, Text("net_error"));
			}
			else
			{
				HandleResult(o);
			}
		});
	}

	private void HandleResult(JObject o)
	{
		if (!IsOk(o))
		{
			PopupFail(o);
			return;
		}
		string text = Str(o, "result");
		string text2 = Str(o, "title");
		string text3 = Str(o, "message");
		string text4 = Str(o, "discord_name");
		switch (text)
		{
		case "linked":
		case "already":
			if (text4.Length > 0)
			{
				OffServerLink.DiscordName = text4;
			}
			RefreshUi();
			Popup(text2, text3);
			break;
		case "needs_confirm":
		{
			string state = Str(o, "state");
			UIManager.MessageBox.Show(Clean(text2), Clean(text3), delegate(bool ok)
			{
				if (ok)
				{
					Confirm(state);
				}
			});
			break;
		}
		case "moved":
		{
			bool relogin = o["relogin"] == null || Bool(o, "relogin");
			if (text4.Length > 0)
			{
				OffServerLink.DiscordName = text4;
			}
			UIManager.MessageBox.Show(Clean(text2), Clean(text3), (Action)delegate
			{
				if (relogin)
				{
					BackToTitle();
				}
				else
				{
					RefreshUi();
				}
			}, (string)null);
			break;
		}
		default:
			Popup(text2, text3);
			break;
		}
	}

	private void Confirm(string state)
	{
		if (_finishing)
		{
			return;
		}
		_finishing = true;
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		dictionary["state"] = state;
		Post("confirm", dictionary, delegate(JObject o)
		{
			_finishing = false;
			if (o == null)
			{
				Popup(null, Text("net_error"));
			}
			else
			{
				HandleResult(o);
			}
		});
	}

	private static void BackToTitle()
	{
		try
		{
			Singleton<GameManager>.Instance().MoveToTitle();
		}
		catch (Exception)
		{
		}
	}

	private static void RefreshUi()
	{
		try
		{
			ConfigMainWidget.RefreshOffServerAccount();
		}
		catch (Exception)
		{
		}
	}

	private void Update()
	{
		while (true)
		{
			Action action;
			lock (_main)
			{
				if (_main.Count == 0)
				{
					break;
				}
				action = _main.Dequeue();
				goto IL_0039;
			}
			IL_0039:
			try
			{
				action();
			}
			catch (Exception)
			{
			}
		}
		if (_waiting && Time.realtimeSinceStartup > _deadline)
		{
			StopWaiting();
			LastEvent = "timeout";
			Popup(null, Text("timeout"));
		}
	}

	private void OnDestroy()
	{
		StopWaiting();
	}

	private void OnApplicationQuit()
	{
		StopWaiting();
	}

	private static void Post(string op, Dictionary<string, string> fields, Action<JObject> callback)
	{
		HTTPRequest hTTPRequest = new HTTPRequest(new Uri(GatewayUrl + "/discord/oauth/" + op), HTTPMethods.Post, delegate(HTTPRequest req, HTTPResponse resp)
		{
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
				LastEvent = op + " error " + req.State.ToString() + " " + num;
			}
			else
			{
				LastEvent = op + " HTTP " + num + " ok=" + Bool(jObject, "ok") + " result=" + Str(jObject, "result") + " reason=" + Str(jObject, "reason");
			}
			callback(jObject);
		});
		hTTPRequest.FormUsage = HTTPFormUsage.UrlEncoded;
		hTTPRequest.AddHeader("Authorization", GameManager.SessionToken);
		hTTPRequest.AddHeader("Accept", "application/json");
		hTTPRequest.AddHeader("X-OffServer-Client", OffServerLink.ClientVersion);
		hTTPRequest.AddHeader("X-OffServer-Caps", "nametag-emoji, item-link, server-popup, voice-chat, i18n-keys, pet-scale, pvp-ranked, pvp-duel, gather-check, rift-coin, rift-scope");
		hTTPRequest.AddField("lang", Lang);
		if (fields != null)
		{
			foreach (KeyValuePair<string, string> field in fields)
			{
				if (field.Value != null)
				{
					hTTPRequest.AddField(field.Key, field.Value);
				}
			}
		}
		hTTPRequest.ConnectTimeout = TimeSpan.FromSeconds(10.0);
		hTTPRequest.Timeout = TimeSpan.FromSeconds(20.0);
		hTTPRequest.DisableCache = true;
		hTTPRequest.Send();
	}

	private static bool IsOk(JObject o)
	{
		return Bool(o, "ok");
	}

	private static bool Bool(JObject o, string key)
	{
		if (o == null)
		{
			return false;
		}
		JToken jToken = o[key];
		if (jToken != null && jToken.Type == JTokenType.Boolean)
		{
			return (bool)jToken;
		}
		return false;
	}

	private static string Str(JObject o, string key)
	{
		if (o == null)
		{
			return string.Empty;
		}
		JToken jToken = o[key];
		if (jToken == null || jToken.Type == JTokenType.Null)
		{
			return string.Empty;
		}
		return jToken.ToString();
	}

	private static int[] Ports(JObject o)
	{
		List<int> list = new List<int>();
		if (o["ports"] is JArray jArray)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (int.TryParse(item.ToString(), out var result) && result > 0 && result < 65536)
				{
					list.Add(result);
				}
			}
		}
		if (list.Count != 0)
		{
			return list.ToArray();
		}
		return DefaultPorts;
	}

	private static string Get(Dictionary<string, string> d, string key)
	{
		if (!d.TryGetValue(key, out var value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}

	private static Dictionary<string, string> ParseQuery(string query)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array = query.Split('&');
		foreach (string text in array)
		{
			if (text.Length != 0)
			{
				int num = text.IndexOf('=');
				string text2 = ((num < 0) ? text : text.Substring(0, num));
				string text3 = ((num < 0) ? string.Empty : text.Substring(num + 1));
				try
				{
					text2 = Uri.UnescapeDataString(text2.Replace('+', ' '));
					text3 = Uri.UnescapeDataString(text3.Replace('+', ' '));
				}
				catch (Exception)
				{
				}
				if (!dictionary.ContainsKey(text2))
				{
					dictionary[text2] = text3;
				}
			}
		}
		return dictionary;
	}

	private static string ReadRequestLine(NetworkStream stream)
	{
		byte[] array = new byte[16384];
		int num = 0;
		while (num < array.Length)
		{
			int num2 = stream.Read(array, num, array.Length - num);
			if (num2 <= 0)
			{
				break;
			}
			num += num2;
			string text = Encoding.ASCII.GetString(array, 0, num);
			if (text.Contains("\r\n\r\n") || text.Contains("\n\n"))
			{
				break;
			}
		}
		if (num == 0)
		{
			return null;
		}
		string text2 = Encoding.ASCII.GetString(array, 0, num);
		int num3 = text2.IndexOf('\n');
		return ((num3 < 0) ? text2 : text2.Substring(0, num3)).Trim();
	}

	private static void WriteResponse(TcpClient client, string status, string contentType, string body)
	{
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
			byte[] bytes2 = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: " + contentType + "\r\nContent-Length: " + bytes.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
			NetworkStream stream = client.GetStream();
			stream.Write(bytes2, 0, bytes2.Length);
			stream.Write(bytes, 0, bytes.Length);
			stream.Flush();
		}
		catch (Exception)
		{
		}
		finally
		{
			try
			{
				client.Close();
			}
			catch (Exception)
			{
			}
		}
	}

	private static void Popup(string title, string message)
	{
		try
		{
			if (string.IsNullOrEmpty(title))
			{
				UIManager.MessageBox.Show(Clean(message));
			}
			else
			{
				UIManager.MessageBox.Show(Clean(title), Clean(message));
			}
		}
		catch (Exception)
		{
		}
	}

	private static void PopupFail(JObject o)
	{
		string title = Str(o, "title");
		string text = Str(o, "message");
		if (text.Length == 0)
		{
			text = Text("failed") + " (" + Str(o, "reason") + ")";
		}
		Popup(title, text);
	}

	private static void Toast(string message)
	{
		try
		{
			UIManager.SystemMsg(Clean(message), 6f);
		}
		catch (Exception)
		{
		}
	}

	public static string Clean(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (!char.IsSurrogate(c) && c != '\ufe0f' && c != '\u200d' && (c < '☀' || c > '➿' || c == '✓'))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString().Trim();
	}

	private static string HtmlEscape(string s)
	{
		return (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
			.Replace("\"", "&quot;");
	}

	private static string ReturnToGamePage()
	{
		string text = HtmlEscape(Clean(Text("page_return_title")));
		string text2 = HtmlEscape(Text("page_return"));
		string text3 = HtmlEscape(Text("page_return_button"));
		string text4 = HtmlEscape(s_returnToGameUrl);
		return "<!doctype html><html lang=\"" + Lang + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + text + "</title><style>body{font-family:system-ui,'Segoe UI',Tahoma,sans-serif;background:#1e1f22;color:#f2f3f5;display:flex;min-height:100vh;align-items:center;justify-content:center;margin:0}main{max-width:520px;padding:32px;background:#2b2d31;border-radius:12px;border-top:6px solid #5865f2;text-align:center}h1{font-size:22px;margin:0 0 12px}p{line-height:1.6;margin:0 0 24px}a{display:inline-block;padding:14px 28px;background:#5865f2;color:#fff;border-radius:8px;text-decoration:none;font-size:18px}</style></head><body><main><h1>" + text + "</h1><p>" + text2 + "</p><a href=\"" + text4 + "\">" + text3 + "</a></main></body></html>";
	}

	private static string LocalPage(string key)
	{
		string lang = Lang;
		string text = HtmlEscape(Text(key + "_title"));
		string text2 = HtmlEscape(Text(key));
		return "<!doctype html><html lang=\"" + lang + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" + text + "</title><style>body{font-family:system-ui,'Segoe UI',Tahoma,sans-serif;background:#1e1f22;color:#f2f3f5;display:flex;min-height:100vh;align-items:center;justify-content:center;margin:0}main{max-width:520px;padding:32px;background:#2b2d31;border-radius:12px;border-top:6px solid #da373c}h1{font-size:22px;margin:0 0 12px}p{line-height:1.6;margin:0}</style></head><body><main><h1>" + text + "</h1><p>" + text2 + "</p></main></body></html>";
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		string text = Lang switch
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

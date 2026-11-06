using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BestHTTP;
using Durango.Logic.Clusters;
using Durango.UI;
using Durango.UI.Control;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class OffServerLangPack
{
	private sealed class Pack
	{
		public string Locale;

		public string Name;

		public string Version;

		public int Translated;

		public int Total;

		public List<KeyValuePair<string, string>> Files = new List<KeyValuePair<string, string>>();

		public List<string> Urls = new List<string>();
	}

	public const string SettingKey = "lang_packs";

	private const int PerPage = 3;

	private static bool _busy;

	private static bool Th => false;

	public static string ButtonLabel => Tx("แพ\u0e47กภาษา (โหลด · ใส\u0e48เอง)", "Language packs (download · add your own)");

	private static string GatewayUrl
	{
		get
		{
			string text = GameManager.GatewayUrl;
			if (string.IsNullOrEmpty(text))
			{
				text = OffServerLink.Gateway;
			}
			if (string.IsNullOrEmpty(text) && OffServerLink.MultiServers != null && OffServerLink.MultiServers.Count > 0)
			{
				text = OffServerLink.MultiServers[0][1];
			}
			return (text ?? string.Empty).TrimEnd('/');
		}
	}

	private static string Tx(string th, string en)
	{
		if (!Th)
		{
			return en;
		}
		return th;
	}

	public static void Open()
	{
		if (_busy)
		{
			UIManager.SystemMsg(Tx("กำล\u0e31งโหลดอย\u0e39\u0e48 — รอส\u0e31กคร\u0e39\u0e48", "Still loading — please wait"));
			return;
		}
		string gatewayUrl = GatewayUrl;
		if (string.IsNullOrEmpty(gatewayUrl))
		{
			ShowFolderMenu(Tx("ไม\u0e48ร\u0e39\u0e49ท\u0e35\u0e48อย\u0e39\u0e48เซ\u0e34ร\u0e4cฟ — ใส\u0e48ไฟล\u0e4cภาษาเองในโฟลเดอร\u0e4cได\u0e49", "No server address — you can still add language files to the folder"));
			return;
		}
		_busy = true;
		Get(gatewayUrl + "/i18n/packs", delegate(byte[] bytes)
		{
			_busy = false;
			List<Pack> list = ((bytes != null) ? ParseIndex(Encoding.UTF8.GetString(bytes)) : null);
			if (list == null)
			{
				ShowFolderMenu(Tx("โหลดรายการภาษาจากเซ\u0e34ร\u0e4cฟไม\u0e48ได\u0e49 — ใส\u0e48ไฟล\u0e4cภาษาเองในโฟลเดอร\u0e4cได\u0e49", "Could not load the language list — you can still add language files to the folder"));
			}
			else
			{
				ShowPage(list, 0);
			}
		});
	}

	private static List<Pack> ParseIndex(string json)
	{
		try
		{
			List<Pack> list = new List<Pack>();
			if (!(JObject.Parse(json)["packs"] is JArray jArray))
			{
				return list;
			}
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				Pack pack = new Pack
				{
					Locale = (((string)item["locale"]) ?? ""),
					Name = (((string)item["name"]) ?? ""),
					Version = (((string)item["version"]) ?? ""),
					Translated = ((int?)item["serverTranslated"]).GetValueOrDefault(),
					Total = ((int?)item["serverTotal"]).GetValueOrDefault()
				};
				if (!OffServerI18n.IsSafeLocale(pack.Locale) || pack.Locale.StartsWith("_"))
				{
					continue;
				}
				if (item["files"] is JArray jArray2)
				{
					foreach (JToken item2 in (IEnumerable<JToken>)jArray2)
					{
						string text = ((string)item2["name"]) ?? "";
						if (!(text != "server.po") || !(text != "messages.po") || !(text != "language.json"))
						{
							pack.Files.Add(new KeyValuePair<string, string>(text, ((string)item2["sha256"]) ?? ""));
							pack.Urls.Add(((string)item2["url"]) ?? ("/i18n/packs/" + pack.Locale + "/" + text));
						}
					}
				}
				if (pack.Files.Count > 0)
				{
					list.Add(pack);
				}
			}
			return list;
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning("[LangPack] index: " + ex.Message);
			return null;
		}
	}

	private static string InstalledVersion(string locale)
	{
		try
		{
			string text = OffServerI18n.PackDir(locale);
			if (!Directory.Exists(text))
			{
				return null;
			}
			string path = Path.Combine(text, "language.json");
			if (File.Exists(path))
			{
				return OffServerI18n.JsonField(File.ReadAllText(path), "version") ?? "";
			}
			return (File.Exists(Path.Combine(text, "server.po")) || File.Exists(Path.Combine(text, "messages.po"))) ? "" : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void ShowPage(List<Pack> packs, int page)
	{
		int num = Math.Max(1, (packs.Count + 3 - 1) / 3);
		page = (page % num + num) % num;
		List<MessageBox.Button> list = new List<MessageBox.Button>();
		List<Action> actions = new List<Action>();
		for (int i = page * 3; i < Math.Min(packs.Count, (page + 1) * 3); i++)
		{
			Pack pack = packs[i];
			string text = InstalledVersion(pack.Locale);
			string text2 = ((pack.Total > 0) ? (" · " + 100 * pack.Translated / Math.Max(1, pack.Total) + "%") : "");
			string text3 = ((text != null) ? ((!(text == pack.Version)) ? ("⟳ " + pack.Name + " (" + pack.Locale + ")" + Tx(" — อ\u0e31ปเดต", " — update")) : ("✓ " + pack.Name + " (" + pack.Locale + ")" + Tx(" — โหลดซ\u0e49ำ", " — reinstall"))) : ("⬇ " + pack.Name + " (" + pack.Locale + ")" + text2));
			list.Add(new MessageBox.Button(text3, PresetButton.Style.Border));
			Pack captured = pack;
			actions.Add(delegate
			{
				Download(captured, delegate
				{
					ShowPage(packs, page);
				});
			});
		}
		if (num > 1)
		{
			list.Add(new MessageBox.Button(Tx("หน\u0e49าถ\u0e31ดไป ▸ (" + (page + 1) + "/" + num + ")", "Next page ▸ (" + (page + 1) + "/" + num + ")"), PresetButton.Style.Border));
			actions.Add(delegate
			{
				ShowPage(packs, page + 1);
			});
		}
		list.Add(new MessageBox.Button(Tx("\ud83d\udcc1 โฟลเดอร\u0e4cภาษา · ต\u0e49นแบบแปล", "\ud83d\udcc1 Language folder · template"), PresetButton.Style.Border));
		actions.Add(delegate
		{
			ShowFolderMenu(null);
		});
		list.Add(new MessageBox.Button(Tx("ป\u0e34ด", "Close")));
		actions.Add(delegate
		{
		});
		string subText = ((packs.Count == 0) ? Tx("เซ\u0e34ร\u0e4cฟย\u0e31งไม\u0e48ม\u0e35แพ\u0e47กภาษา — ใส\u0e48ไฟล\u0e4cเองได\u0e49ท\u0e35\u0e48 «โฟลเดอร\u0e4cภาษา»", "The server has no language packs yet — add your own in «Language folder»") : Tx("เล\u0e37อกภาษาเพ\u0e37\u0e48อดาวน\u0e4cโหลด แล\u0e49วเปล\u0e35\u0e48ยนภาษาท\u0e35\u0e48แถว «ภาษา» ด\u0e49านบน\nภาษาท\u0e35\u0e48ย\u0e31งแปลไม\u0e48ครบจะเห\u0e47นอ\u0e31งกฤษแทน", "Pick a language to download, then switch at the «Language» row above.\nUntranslated text falls back to English."));
		UIManager.MessageBox.Show(Tx("แพ\u0e47กภาษา", "Language packs"), subText, delegate(int index)
		{
			if (index >= 0 && index < actions.Count)
			{
				actions[index]();
			}
		}, list.ToArray());
	}

	private static void ShowFolderMenu(string note)
	{
		string dir = OffServerI18n.LanguagesDir;
		bool flag = Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer;
		List<OffServerI18n.PackInfo> list = OffServerI18n.DiscoverPacks();
		List<string> list2 = new List<string>();
		foreach (OffServerI18n.PackInfo item in list)
		{
			list2.Add(item.Name + " (" + item.Locale + ")");
		}
		string subText = ((note != null) ? (note + "\n\n") : "") + Tx("ท\u0e35\u0e48อย\u0e39\u0e48: ", "Path: ") + dir + "\n" + Tx("ใส\u0e48ไฟล\u0e4cท\u0e35\u0e48 Languages/<รห\u0e31สภาษา>/ เช\u0e48น vi_VN/ — messages.po (ข\u0e49อความเกม) · server.po (ข\u0e49อความเซ\u0e34ร\u0e4cฟ) · language.json {\"name\": \"ช\u0e37\u0e48อภาษา\"}", "Put files in Languages/<locale>/ e.g. vi_VN/ — messages.po (game text) · server.po (server text) · language.json {\"name\": \"Language name\"}") + "\n" + Tx("ในเคร\u0e37\u0e48องตอนน\u0e35\u0e49: ", "Installed: ") + ((list2.Count == 0) ? "-" : string.Join(", ", list2.ToArray()));
		List<MessageBox.Button> list3 = new List<MessageBox.Button>();
		List<Action> actions = new List<Action>();
		if (!flag)
		{
			list3.Add(new MessageBox.Button(Tx("เป\u0e34ดโฟลเดอร\u0e4c", "Open folder"), PresetButton.Style.Border));
			actions.Add(delegate
			{
				OpenFolder(dir);
			});
		}
		list3.Add(new MessageBox.Button(Tx("โหลดไฟล\u0e4cต\u0e49นแบบสำหร\u0e31บแปล", "Download translation template"), PresetButton.Style.Border));
		actions.Add(DownloadTemplate);
		list3.Add(new MessageBox.Button(Tx("ร\u0e35เฟรชรายการภาษา", "Refresh language list"), PresetButton.Style.Border));
		actions.Add(delegate
		{
			LocalizeSystem.ReloadLanguages();
			ConfigMainWidget.RefreshOffServerLanguages();
			UIManager.SystemMsg(Tx("อ\u0e31ปเดตรายการภาษาแล\u0e49ว", "Language list refreshed"));
		});
		list3.Add(new MessageBox.Button(Tx("ป\u0e34ด", "Close")));
		actions.Add(delegate
		{
		});
		UIManager.MessageBox.Show(Tx("โฟลเดอร\u0e4cภาษา", "Language folder"), subText, delegate(int index)
		{
			if (index >= 0 && index < actions.Count)
			{
				actions[index]();
			}
		}, list3.ToArray());
	}

	private static void OpenFolder(string dir)
	{
		try
		{
			Directory.CreateDirectory(dir);
			Application.OpenURL("file:///" + dir.Replace('\\', '/'));
		}
		catch (Exception ex)
		{
			UIManager.SystemMsg(Tx("เป\u0e34ดโฟลเดอร\u0e4cไม\u0e48ได\u0e49: ", "Could not open the folder: ") + ex.Message);
		}
	}

	private static void Download(Pack p, Action after)
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		UIManager.SystemMsg(Tx("กำล\u0e31งดาวน\u0e4cโหลด ", "Downloading ") + p.Name + "…");
		string text = Path.Combine(OffServerI18n.LanguagesDir, "_download_" + p.Locale);
		try
		{
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
			Directory.CreateDirectory(text);
		}
		catch (Exception ex)
		{
			_busy = false;
			UIManager.SystemMsg(Tx("ดาวน\u0e4cโหลดไม\u0e48ได\u0e49: ", "Download failed: ") + ex.Message);
			return;
		}
		DownloadNext(p, 0, text, after);
	}

	private static void DownloadNext(Pack p, int i, string tmp, Action after)
	{
		if (i >= p.Files.Count)
		{
			Finish(p, tmp, after);
			return;
		}
		Get(p.Urls[i].StartsWith("http") ? p.Urls[i] : (GatewayUrl + p.Urls[i]), delegate(byte[] bytes)
		{
			if (bytes == null || !ShaOk(bytes, p.Files[i].Value))
			{
				_busy = false;
				UIManager.SystemMsg(Tx("ดาวน\u0e4cโหลดไม\u0e48สำเร\u0e47จ (", "Download failed (") + p.Files[i].Key + ")");
			}
			else
			{
				try
				{
					File.WriteAllBytes(Path.Combine(tmp, p.Files[i].Key), bytes);
				}
				catch (Exception ex)
				{
					_busy = false;
					UIManager.SystemMsg(Tx("เข\u0e35ยนไฟล\u0e4cไม\u0e48ได\u0e49: ", "Could not save: ") + ex.Message);
					return;
				}
				DownloadNext(p, i + 1, tmp, after);
			}
		});
	}

	private static void Finish(Pack p, string tmp, Action after)
	{
		_busy = false;
		try
		{
			string text = OffServerI18n.PackDir(p.Locale);
			Directory.CreateDirectory(text);
			foreach (KeyValuePair<string, string> file in p.Files)
			{
				string text2 = Path.Combine(text, file.Key);
				if (File.Exists(text2))
				{
					File.Delete(text2);
				}
				File.Move(Path.Combine(tmp, file.Key), text2);
			}
			Directory.Delete(tmp, recursive: true);
		}
		catch (Exception ex)
		{
			UIManager.SystemMsg(Tx("ต\u0e34ดต\u0e31\u0e49งไม\u0e48ได\u0e49: ", "Install failed: ") + ex.Message);
			return;
		}
		LocalizeSystem.ReloadLanguages();
		if (LocalizeSystem.Locale == p.Locale)
		{
			LocalizeSystem.SetLocale(p.Locale);
		}
		ConfigMainWidget.RefreshOffServerLanguages();
		UIManager.SystemMsg(p.Name + Tx(" พร\u0e49อมใช\u0e49 — เล\u0e37อกท\u0e35\u0e48แถว «ภาษา»", " is ready — pick it at the «Language» row"));
		after?.Invoke();
	}

	private static void DownloadTemplate()
	{
		string gatewayUrl = GatewayUrl;
		if (!string.IsNullOrEmpty(gatewayUrl) && !_busy)
		{
			_busy = true;
			string dir = Path.Combine(OffServerI18n.LanguagesDir, "_template");
			string[] names = new string[2] { "server.pot", "messages.pot" };
			DownloadTemplateNext(gatewayUrl, dir, names, 0);
		}
	}

	private static void DownloadTemplateNext(string gw, string dir, string[] names, int i)
	{
		if (i >= names.Length)
		{
			_busy = false;
			UIManager.SystemMsg(Tx("ไฟล\u0e4cต\u0e49นแบบอย\u0e39\u0e48ท\u0e35\u0e48 ", "Templates saved to ") + dir);
			return;
		}
		Get(gw + "/i18n/template/" + names[i], delegate(byte[] bytes)
		{
			if (bytes != null)
			{
				try
				{
					Directory.CreateDirectory(dir);
					File.WriteAllBytes(Path.Combine(dir, names[i]), bytes);
				}
				catch (Exception)
				{
				}
			}
			DownloadTemplateNext(gw, dir, names, i + 1);
		});
	}

	private static bool ShaOk(byte[] bytes, string sha)
	{
		if (string.IsNullOrEmpty(sha))
		{
			return true;
		}
		using SHA256 sHA = SHA256.Create();
		byte[] array = sHA.ComputeHash(bytes);
		StringBuilder stringBuilder = new StringBuilder(64);
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append(b.ToString("x2"));
		}
		return string.Equals(stringBuilder.ToString(), sha, StringComparison.OrdinalIgnoreCase);
	}

	private static void Get(string url, Action<byte[]> done)
	{
		try
		{
			HTTPRequest hTTPRequest = new HTTPRequest(new Uri(url), HTTPMethods.Get, delegate(HTTPRequest r, HTTPResponse resp)
			{
				byte[] obj = null;
				try
				{
					if (resp != null && resp.IsSuccess)
					{
						obj = resp.Data;
					}
				}
				catch (Exception)
				{
				}
				done(obj);
			});
			hTTPRequest.Timeout = TimeSpan.FromSeconds(60.0);
			hTTPRequest.ConnectTimeout = TimeSpan.FromSeconds(10.0);
			hTTPRequest.DisableCache = true;
			hTTPRequest.Send();
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning("[LangPack] GET " + url + ": " + ex.Message);
			done(null);
		}
	}
}

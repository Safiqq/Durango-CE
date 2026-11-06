using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Durango.Development;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.Render.Camera;
using Durango.UI;
using Durango.UI.Control;
using Durango.Utils;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class OffServerRankedPages : MonoBehaviour
{
	private static OffServerRankedPages instance;

	private static long sequence;

	private OffServerRankedUI owner;

	private OffServerRankedWindow window;

	private GameObject content;

	private GameObject page;

	private GameObject holder;

	private GameObject prefab;

	private UILabel font;

	private SelectableButton buttonTemplate;

	private string screen = "ladder";

	private string mode = "1v1";

	private string pending = "";

	private string responseId = "";

	private string state = "closed";

	private string error = "";

	private int tab;

	private int pageIndex;

	private int received;

	private int ignored;

	private int lastWidth;

	private int lastHeight;

	private double deadline;

	private double receivedAt;

	private JObject data;

	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private static readonly Color Gold = new Color(1f, 0.847f, 0.357f);

	private static readonly Color Muted = new Color(0.68f, 0.63f, 0.53f);

	private static string T(string th, string en)
	{
		return OffServerRanked.T(th, en);
	}

	private static int I(JToken t)
	{
		return OffServerRanked.Int(t);
	}

	private static string S(JToken t)
	{
		return OffServerRanked.Str(t);
	}

	private static bool B(JToken t)
	{
		return OffServerRanked.Flag(t);
	}

	private static string N(JToken t)
	{
		return OffServerRanked.Num(t).ToString("0.##", CultureInfo.InvariantCulture);
	}

	private static string Text(JToken t)
	{
		return Escape(OffServerRanked.OneLanguage(S(t)));
	}

	private static string Escape(string text)
	{
		return (text ?? "").Replace("[", "［").Replace("]", "］");
	}

	private TField Field<TField>(string name)
	{
		return (TField)typeof(OffServerRankedUI).GetField(name, Private).GetValue(owner);
	}

	public static void Attach(OffServerRankedUI ui)
	{
		if (instance != null || ui == null)
		{
			return;
		}
		instance = ui.gameObject.AddComponent<OffServerRankedPages>();
		instance.owner = ui;
		try
		{
			instance.Setup();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private void Setup()
	{
		buttonTemplate = Field<SelectableButton>("_borderTpl");
		SelectableButton selectableButton = Field<SelectableButton>("_lGuide");
		if (selectableButton != null)
		{
			selectableButton.Clicked = delegate
			{
				Open("rules");
			};
		}
		Transform transform = Field<OffServerRankedWindow>("_lobbyWin").transform.Find("Container/Contents/Center");
		if (transform == null)
		{
			throw new Exception("Native ranked lobby center missing");
		}
		GameObject gameObject = transform.gameObject.AddChild();
		gameObject.name = "RankedPageLinks";
		gameObject.AddComponent<UIWidget>().SetAnchor(transform.gameObject, 1f, -410, 0f, 58, 1f, -20, 0f, 100);
		Button(gameObject, "OpenLadder", T("ตารางอ\u0e31นด\u0e31บ", "Standings"), -102f, 0f, 185, delegate
		{
			Open("ladder");
		});
		Button(gameObject, "OpenRewards", T("รางว\u0e31ล", "Rewards"), 103f, 0f, 185, delegate
		{
			Open("rewards");
		});
		prefab = Resources.Load<GameObject>("popup/WarpRushRankingRewardPopup");
		if (prefab == null)
		{
			throw new Exception("Native ranking reward popup missing");
		}
		font = prefab.GetComponentInChildren<UILabel>(includeInactive: true);
		window = (OffServerRankedWindow)typeof(OffServerRankedUI).GetMethod("CloneWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[2]
		{
			UIManager.FindScript<PvpIslandGroup>(),
			"OffServerRankedPages"
		});
		typeof(UIBase).GetField("_gameBlur", Private).SetValue(window, false);
		Transform transform2 = window.transform.Find("Container/Contents");
		if (transform2 != null)
		{
			transform2.gameObject.SetActive(value: false);
		}
		content = window.transform.Find("Container").gameObject.AddChild();
		content.name = "RankedPagesContent";
		content.AddComponent<UIPanel>().depth = Field<int>("_modalDepth") + 3;
		holder = new GameObject("RankedPagesInactiveTemplates");
		holder.SetActive(value: false);
		holder.transform.SetParent(base.transform, worldPositionStays: false);
		window.OnCloseSucceed += delegate
		{
			pending = "";
			data = null;
			state = "closed";
			if (page != null)
			{
				page.SetActive(value: false);
			}
		};
		OffServerRanked.Changed += OnRankedChanged;
		RegisterBridge();
	}

	private void OnRankedChanged()
	{
		if (OffServerRanked.InMatch || OffServerRanked.Found != null)
		{
			Hide();
		}
	}

	private void OnDestroy()
	{
		OffServerRanked.Changed -= OnRankedChanged;
		if (instance == this)
		{
			instance = null;
		}
	}

	public static void Hide()
	{
		if (instance != null && instance.window != null && instance.window.IsOpened)
		{
			instance.window.ForceClose();
		}
	}

	public static bool OnPacket(RankedDown message)
	{
		if (message.Kind != 8)
		{
			return false;
		}
		if (instance != null)
		{
			instance.Receive(message.Json);
		}
		return true;
	}

	private void Receive(string json)
	{
		JObject jObject;
		try
		{
			jObject = JObject.Parse(json);
		}
		catch
		{
			return;
		}
		if (window == null || !window.IsOpened || pending == "" || S(jObject["requestId"]) != pending || S(jObject["mode"]) != mode)
		{
			ignored++;
			return;
		}
		responseId = pending;
		pending = "";
		received++;
		receivedAt = Time.realtimeSinceStartupAsDouble;
		if (!B(jObject["ok"]))
		{
			data = null;
			state = "error";
			error = S(jObject["reason"]);
		}
		else
		{
			data = jObject;
			state = "ready";
			pageIndex = I(data["ladder"]?["page"]);
		}
		Draw();
	}

	private void Open(string name)
	{
		if (name != "ladder" && name != "rewards" && name != "rules")
		{
			return;
		}
		screen = name;
		tab = 0;
		pageIndex = 0;
		mode = ((Field<string>("_lobbyMode") == "3v3") ? "3v3" : "1v1");
		OffServerEventCalendar.Close();
		Field<OffServerRankedWindow>("_lobbyWin").ForceClose();
		if (window.Open())
		{
			if (window.BackgroundWidget != null)
			{
				window.BackgroundWidget.alpha = 0.18f;
			}
			Transform transform = window.transform.Find("Container/Contents");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
			}
			Request();
		}
	}

	private void Request()
	{
		data = null;
		error = "";
		pending = "";
		if (!OffServerLink.Active || !GameManager.IsReady)
		{
			state = "error";
			error = "disconnected";
			Draw();
			return;
		}
		long num = ++sequence;
		pending = num.ToString();
		state = "loading";
		deadline = Time.realtimeSinceStartupAsDouble + 12.0;
		Connections.Frontend.Send(new RankedUp
		{
			Kind = 12,
			Json = new JObject
			{
				["requestId"] = pending,
				["mode"] = mode,
				["page"] = pageIndex
			}.ToString(Formatting.None)
		}, noReply: true);
		Draw();
	}

	private void Update()
	{
		if (window == null || !window.IsOpened)
		{
			return;
		}
		if (lastWidth != Screen.width || lastHeight != Screen.height)
		{
			lastWidth = Screen.width;
			lastHeight = Screen.height;
			Draw();
		}
		if (!OffServerLink.Active || !GameManager.IsReady)
		{
			if (error != "disconnected")
			{
				pending = "";
				data = null;
				state = "error";
				error = "disconnected";
				Draw();
			}
			return;
		}
		if (state == "loading" && Time.realtimeSinceStartupAsDouble > deadline)
		{
			pending = "";
			data = null;
			state = "error";
			error = "timeout";
			Draw();
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Hide();
		}
	}

	private GameObject Child(GameObject parent, string name)
	{
		GameObject obj = parent.AddChild();
		obj.name = name;
		return obj;
	}

	private UISprite Sprite(string name, float x, float y, int w, int h, Color color, int depth = 15)
	{
		UISprite uISprite = page.AddSprite(name, depth);
		uISprite.transform.localPosition = new Vector3(x, y, 0f);
		uISprite.width = w;
		uISprite.height = h;
		uISprite.color = color;
		return uISprite;
	}

	private UILabel Label(string text, float x, float y, int w, int h, int size = 22, Color? color = null, NGUIText.Alignment align = NGUIText.Alignment.Left)
	{
		GameObject obj = Child(page, "Text");
		obj.transform.localPosition = new Vector3(x, y, 0f);
		UILabel uILabel = obj.AddComponent<UILabel>();
		uILabel.bitmapFont = font.bitmapFont;
		uILabel.trueTypeFont = font.trueTypeFont;
		uILabel.fontSize = size;
		uILabel.width = w;
		uILabel.height = h;
		uILabel.color = color ?? Color.white;
		uILabel.pivot = UIWidget.Pivot.Center;
		uILabel.alignment = align;
		uILabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		uILabel.depth = 40;
		uILabel.supportEncoding = false;
		uILabel.text = text;
		return uILabel;
	}

	private void Button(GameObject parent, string key, string text, float x, float y, int width, Action action, bool selected = false)
	{
		SelectableButton selectableButton = (SelectableButton)typeof(OffServerRankedUI).GetMethod("CloneButton", Private).Invoke(owner, new object[10]
		{
			buttonTemplate,
			parent,
			x,
			y,
			width,
			44,
			60,
			(!selected) ? PresetButton.Style.Border : PresetButton.Style.Solid,
			text,
			(Action)delegate
			{
				StartCoroutine(NextFrame(action));
			}
		});
		selectableButton.gameObject.name = key;
		typeof(SelectableButton).GetField("_toPreferredSize", Private).SetValue(selectableButton, false);
		typeof(SelectableButton).GetField("_fontSize", Private).SetValue(selectableButton, 20);
		selectableButton.Text = text;
		selectableButton.SetDimensions(width, 42);
	}

	private IEnumerator NextFrame(Action action)
	{
		yield return null;
		action();
	}

	private void Line(float y)
	{
		Sprite("bg_dotline_white2", 0f, y, 1040, 2, new Color(1f, 1f, 1f, 0.13f));
	}

	private void Frame()
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, holder.transform, worldPositionStays: false);
		gameObject.name = "NativeRankingRewardFrame";
		MonoBehaviour[] componentsInChildren = gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		foreach (MonoBehaviour monoBehaviour in componentsInChildren)
		{
			if (!(monoBehaviour is UIWidget) && !(monoBehaviour is UIPanel))
			{
				UnityEngine.Object.DestroyImmediate(monoBehaviour);
			}
		}
		Collider[] componentsInChildren2 = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren2[i]);
		}
		UIPanel[] componentsInChildren3 = gameObject.GetComponentsInChildren<UIPanel>(includeInactive: true);
		for (int i = 0; i < componentsInChildren3.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren3[i]);
		}
		UIRect[] componentsInChildren4 = gameObject.GetComponentsInChildren<UIRect>(includeInactive: true);
		for (int i = 0; i < componentsInChildren4.Length; i++)
		{
			componentsInChildren4[i].SetAnchor((Transform)null);
		}
		string[] array = new string[2] { "Tabs", "Items" };
		foreach (string n in array)
		{
			gameObject.transform.Find(n).gameObject.SetActive(value: false);
		}
		UILabel[] componentsInChildren5 = gameObject.GetComponentsInChildren<UILabel>(includeInactive: true);
		for (int i = 0; i < componentsInChildren5.Length; i++)
		{
			componentsInChildren5[i].gameObject.SetActive(value: false);
		}
		UISprite[] componentsInChildren6 = gameObject.GetComponentsInChildren<UISprite>(includeInactive: true);
		foreach (UISprite obj in componentsInChildren6)
		{
			obj.depth = Math.Min(obj.depth, 10);
		}
		gameObject.transform.Find("Header").gameObject.SetActive(value: false);
		UISprite component = gameObject.transform.Find("bg").GetComponent<UISprite>();
		component.width = 1220;
		component.height = 520;
		component.color = new Color(0.075f, 0.068f, 0.052f, 0.54f);
		component.transform.localPosition = new Vector3(0f, -8f, 0f);
		UISprite component2 = gameObject.transform.Find("border").GetComponent<UISprite>();
		component2.width = 1220;
		component2.height = 520;
		component2.color = new Color(1f, 1f, 1f, 0.065f);
		component2.transform.localPosition = new Vector3(0f, -8f, 0f);
		gameObject.transform.SetParent(page.transform, worldPositionStays: false);
		gameObject.transform.localPosition = Vector3.zero;
		NGUITools.SetLayer(gameObject, page.layer);
		gameObject.SetActive(value: true);
	}

	private void Draw()
	{
		if (page != null)
		{
			page.SetActive(value: false);
			UnityEngine.Object.Destroy(page);
		}
		page = Child(content, "Page");
		UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(window.Anchor);
		if (rootAnchor != null)
		{
			page.transform.localScale = Vector3.one * Mathf.Min((float)rootAnchor.width / 1280f, (float)rootAnchor.height / 720f);
		}
		UITitle componentInChildren = window.GetComponentInChildren<UITitle>(includeInactive: true);
		string text = ((screen == "ladder") ? T("อ\u0e31นด\u0e31บฤด\u0e39กาล", "Season standings") : ((screen == "rewards") ? T("รางว\u0e31ลการดวล", "Duel rewards") : T("กต\u0e34กาดวลจ\u0e31ดอ\u0e31นด\u0e31บ", "Ranked rules")));
		if (componentInChildren != null)
		{
			typeof(OffServerRankedUI).GetMethod("FixTitle", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[2] { componentInChildren, text });
		}
		Frame();
		Button(page, "Ladder", T("อ\u0e31นด\u0e31บ", "Ranks"), 147f, 328f, 105, delegate
		{
			screen = "ladder";
			tab = 0;
			Draw();
		}, screen == "ladder");
		Button(page, "Rewards", T("รางว\u0e31ล", "Rewards"), 260f, 328f, 105, delegate
		{
			screen = "rewards";
			tab = 0;
			Draw();
		}, screen == "rewards");
		Button(page, "Rules", T("กต\u0e34กา", "Rules"), 373f, 328f, 105, delegate
		{
			screen = "rules";
			tab = 0;
			Draw();
		}, screen == "rules");
		Button(page, "Back", T("หน\u0e49าดวล", "Lobby"), 486f, 328f, 105, delegate
		{
			Hide();
			OffServerRankedUI.OpenLobby(mode);
		});
		Button(page, "Refresh", T("ร\u0e35เฟรช", "Refresh"), 490f, 162f, 170, Request);
		if (data == null)
		{
			string text2 = ((state == "loading") ? T("กำล\u0e31งโหลดข\u0e49อม\u0e39ลจากเซ\u0e34ร\u0e4cฟเวอร\u0e4c…", "Loading server data…") : ((error == "disconnected") ? T("ขาดการเช\u0e37\u0e48อมต\u0e48อ กร\u0e38ณาเข\u0e49าเกมใหม\u0e48", "Disconnected. Please reconnect.") : T("โหลดข\u0e49อม\u0e39ลไม\u0e48สำเร\u0e47จ กร\u0e38ณาลองใหม\u0e48", "Unable to load data. Please retry.")));
			Label(text2, 0f, 10f, 1030, 100, 28, Gold, NGUIText.Alignment.Center);
			if (state == "error")
			{
				Button(page, "Retry", T("ลองใหม\u0e48", "Retry"), 0f, -90f, 260, Request);
			}
			return;
		}
		if (!B(data["enabled"]))
		{
			Label(T("ดวลจ\u0e31ดอ\u0e31นด\u0e31บป\u0e34ดช\u0e31\u0e48วคราว", "Ranked temporarily closed"), 0f, 186f, 1030, 28, 19, Gold, NGUIText.Alignment.Center);
		}
		string text3 = (File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), "ranked-standings-demo.flag")) ? T(" • อ\u0e31นด\u0e31บจำลอง", " • Sample standings") : "");
		Label((screen == "ladder") ? (mode + " • " + T("ฤด\u0e39 ", "Season ") + Text(data["season"]) + text3) : ((screen == "rewards") ? T("Coin ต\u0e48อบ\u0e31ญช\u0e35 • ฉายาแยกตามโหมด • ไม\u0e48ได\u0e49ร\u0e31บ EXP", "Account-wide Coin • Mode-specific titles • No EXP") : (T("ชนะ ", "Win ") + N(data["rules"]?["roundsToWin"]) + T(" ยก • เลเวลข\u0e31\u0e49นต\u0e48ำ ", " rounds • Minimum level ") + N(data["rules"]?["levelMin"]))), 0f, 218f, 1140, 38, 22, Muted);
		Label(T("ข\u0e49อม\u0e39ลจากเซ\u0e34ร\u0e4cฟเวอร\u0e4c", "Server data") + " • " + DateTimeOffset.FromUnixTimeSeconds((long)OffServerRanked.Num(data["now"])).ToOffset(TimeSpan.FromHours(7.0)).ToString("dd/MM HH:mm:ss"), -150f, -292f, 700, 28, 17, Muted);
		if (screen == "ladder")
		{
			Ladder();
		}
		else if (screen == "rewards")
		{
			Rewards();
		}
		else
		{
			Rules();
		}
	}

	private void Modes()
	{
		Button(page, "Mode1v1", "1v1", -420f, 162f, 170, delegate
		{
			ChangeMode("1v1");
		}, mode == "1v1");
		Button(page, "Mode3v3", "3v3", -230f, 162f, 170, delegate
		{
			ChangeMode("3v3");
		}, mode == "3v3");
	}

	private void ChangeMode(string value)
	{
		mode = value;
		pageIndex = 0;
		Request();
	}

	private string Tier(JToken tier)
	{
		string id = S(tier);
		if (id == "")
		{
			return T("ย\u0e31งไม\u0e48จ\u0e31ดข\u0e31\u0e49น", "Unranked");
		}
		JToken jToken = (data?["rewards"]?["tiers"] as JArray)?.FirstOrDefault((JToken x) => S(x["id"]) == id);
		if (jToken == null)
		{
			return Escape(id);
		}
		return Text(jToken["name"]);
	}

	private void RankRow(JToken row, float y, bool mine = false)
	{
		Sprite("bg_roundbox2", 0f, y, 1140, mine ? 52 : 44, mine ? new Color(0.42f, 0.31f, 0.1f, 0.46f) : new Color(0.15f, 0.13f, 0.1f, 0.32f));
		Label((I(row["rank"]) > 0) ? N(row["rank"]) : "—", -465f, y, 90, 42, 24, Gold);
		string text = Text(row["name"]);
		if (mine)
		{
			text = T("ค\u0e38ณ", "You") + ((text == "") ? "" : (" · " + text));
		}
		else if (text == "")
		{
			text = T("ผ\u0e39\u0e49เล\u0e48นไม\u0e48ระบ\u0e38ช\u0e37\u0e48อ", "Unnamed player");
		}
		Label(text, -235f, y, 355, 42, 22, mine ? Gold : Color.white);
		object obj = typeof(OffServerRankedUI).GetMethod("MakeEmblem", Private).Invoke(owner, new object[5] { page, 65f, y, 38f, 25 });
		typeof(OffServerRankedUI).GetMethod("SetEmblem", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[2]
		{
			obj,
			S(row["tier"])
		});
		Label((I(row["placement"]) > 0) ? (T("ว\u0e31ดระด\u0e31บเหล\u0e37อ ", "Placement left: ") + N(row["placement"])) : Tier(row["tier"]), 245f, y, 290, 42, 20);
		Label((row["rating"] != null && row["rating"].Type != JTokenType.Null) ? N(row["rating"]) : "—", 450f, y, 100, 42, 23, null, NGUIText.Alignment.Right);
	}

	private void Ladder()
	{
		Modes();
		JToken ladder = data["ladder"];
		JArray jArray = ladder["rows"] as JArray;
		Label(T("อ\u0e31นด\u0e31บ", "Rank"), -465f, 109f, 90, 30, 18, Muted);
		Label(T("ผ\u0e39\u0e49เล\u0e48น", "Player"), -235f, 109f, 355, 30, 18, Muted);
		Label(T("ข\u0e31\u0e49น", "Tier"), 130f, 109f, 250, 30, 18, Muted);
		Label(T("แต\u0e49ม", "Points"), 450f, 109f, 100, 30, 18, Muted, NGUIText.Alignment.Right);
		Line(88f);
		if (jArray == null || jArray.Count == 0)
		{
			Label(T("ย\u0e31งไม\u0e48ม\u0e35ผ\u0e39\u0e49เล\u0e48นว\u0e31ดระด\u0e31บครบในโหมดน\u0e35\u0e49", "No placed players in this mode yet"), 0f, -10f, 1030, 80, 25, Muted, NGUIText.Alignment.Center);
		}
		else
		{
			for (int i = 0; i < jArray.Count; i++)
			{
				float num = 55 - i * 48;
				RankRow(jArray[i], num);
				Line(num - 24f);
			}
		}
		RankRow(ladder["me"], -196f, mine: true);
		Button(page, "Previous", T("ก\u0e48อนหน\u0e49า", "Previous"), -425f, -244f, 180, delegate
		{
			if (pageIndex > 0)
			{
				pageIndex--;
				Request();
			}
		});
		Label(pageIndex + 1 + " / " + I(ladder["pages"]) + T(" หน\u0e49า • ", " pages • ") + N(ladder["total"]) + T(" คน", " players"), 0f, -244f, 600, 30, 18, Muted, NGUIText.Alignment.Center);
		Button(page, "Next", T("ถ\u0e31ดไป", "Next"), 425f, -244f, 180, delegate
		{
			if (pageIndex + 1 < I(ladder["pages"]))
			{
				pageIndex++;
				Request();
			}
		});
	}

	private void Rewards()
	{
		JToken jToken = data["rewards"];
		JToken jToken2 = jToken["account"];
		Button(page, "MatchReward", T("หล\u0e31งแมตช\u0e4c", "After match"), -360f, 162f, 300, delegate
		{
			tab = 0;
			Draw();
		}, tab == 0);
		Button(page, "SeasonReward", T("จบฤด\u0e39กาล", "Season end"), -30f, 162f, 300, delegate
		{
			tab = 1;
			Draw();
		}, tab == 1);
		if (tab == 0)
		{
			Sprite("bg_roundbox2", -275f, -8f, 530, 268, new Color(0.24f, 0.19f, 0.1f, 0.52f));
			Sprite("bg_roundbox2", 285f, -8f, 510, 268, new Color(0.14f, 0.12f, 0.08f, 0.4f));
			Label("Durango Coin", -245f, 87f, 480, 52, 34, Gold);
			Label(B(jToken["enabled"]) ? (T("ชนะ ", "Win ") + N(jToken["win"]) + "  /  " + T("แพ\u0e49 ", "Loss ") + N(jToken["lose"])) : T("รางว\u0e31ล Coin ป\u0e34ดอย\u0e39\u0e48", "Coin rewards are disabled"), -245f, 20f, 480, 64, 34);
			Label(((I(jToken["cap"]) > 0) ? (T("ไม\u0e48เก\u0e34น ", "Up to ") + N(jToken["cap"]) + T(" แมตช\u0e4c / ว\u0e31น / บ\u0e31ญช\u0e35", " matches / day / account")) : T("ไม\u0e48จำก\u0e31ดจำนวนแมตช\u0e4cร\u0e31บ Coin ต\u0e48อว\u0e31น", "No daily rewarded-match cap")) + "\n" + T("3v3 ได\u0e49คนละเท\u0e48าก\u0e31น • ไม\u0e48ได\u0e49ร\u0e31บ EXP", "Equal per-player 3v3 rewards • No EXP"), -245f, -60f, 480, 90);
			Label(T("ฉายาประจำฤด\u0e39กาล", "Season titles"), 285f, 87f, 460, 52, 29, Gold);
			object obj = typeof(OffServerRankedUI).GetMethod("MakeEmblem", Private).Invoke(owner, new object[5] { page, 125f, -10f, 86f, 25 });
			typeof(OffServerRankedUI).GetMethod("SetEmblem", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[2] { obj, "master" });
			Label(T("ข\u0e31\u0e49นส\u0e39งส\u0e38ดประจำฤด\u0e39\nฉายาพ\u0e34เศษ Top ", "Season peak tier\nSpecial Top ") + N(jToken["topTitles"]) + T("\nไม\u0e48ม\u0e35ค\u0e48าพล\u0e31ง • ระบ\u0e38ฤด\u0e39กาล", "\nNo stats • Season retained"), 365f, -10f, 290, 128, 23);
			Line(-151f);
			Label(B(jToken2?["ok"]) ? (T("ว\u0e31นน\u0e35\u0e49ร\u0e31บแล\u0e49ว ", "Rewarded today: ") + N(jToken2["used"]) + T(" แมตช\u0e4c • เหล\u0e37อ ", " matches • Remaining: ") + ((I(jToken2["remaining"]) < 0) ? "∞" : N(jToken2["remaining"]))) : T("ตรวจโควตาไม\u0e48ได\u0e49 กร\u0e38ณาร\u0e35เฟรช", "Quota unavailable. Please refresh."), 0f, -183f, 1030, 48, 25, Gold, NGUIText.Alignment.Center);
			Label(B(jToken2?["ok"]) ? (T("ยอดบ\u0e31ญช\u0e35 ", "Account balance: ") + N(jToken2["coin"]) + " Coin" + T(" • ร\u0e35เซ\u0e47ตโควตา ", " • Quota resets ") + DateTimeOffset.FromUnixTimeSeconds((long)OffServerRanked.Num(jToken2["resetsAt"])).ToOffset(TimeSpan.FromHours(7.0)).ToString("dd/MM HH:mm")) : "", 0f, -234f, 1030, 38, 20, Muted, NGUIText.Alignment.Center);
			return;
		}
		JArray jArray = jToken["tiers"] as JArray;
		int num = Math.Min(6, jArray?.Count ?? 0);
		for (int num2 = 0; num2 < num; num2++)
		{
			float num3 = -440 + num2 * 176;
			object obj2 = typeof(OffServerRankedUI).GetMethod("MakeEmblem", Private).Invoke(owner, new object[5] { page, num3, 88f, 56f, 25 });
			typeof(OffServerRankedUI).GetMethod("SetEmblem", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[2]
			{
				obj2,
				S(jArray[num2]["id"])
			});
			Label(Text(jArray[num2]["name"]), num3, 33f, 170, 38, 21, null, NGUIText.Alignment.Center);
		}
		Sprite("bg_roundbox2", -275f, -110f, 530, 190, new Color(0.14f, 0.12f, 0.08f, 0.42f));
		Sprite("bg_roundbox2", 285f, -110f, 510, 190, new Color(0.14f, 0.12f, 0.08f, 0.42f));
		Label(T("เง\u0e37\u0e48อนไขร\u0e31บรางว\u0e31ล", "Reward conditions"), -265f, -43f, 480, 40, 23, Gold);
		string text = T("ต\u0e49องต\u0e35โดนค\u0e39\u0e48แข\u0e48งและจบด\u0e49วยการต\u0e48อส\u0e39\u0e49\nหล\u0e38ด / ออก / ย\u0e37นน\u0e34\u0e48ง: ไม\u0e48ม\u0e35 Coin\nบ\u0e31ญช\u0e35หร\u0e37อเคร\u0e37\u0e48องเด\u0e35ยวก\u0e31น: ไม\u0e48ได\u0e49รางว\u0e31ล", "Hit an opponent and finish through combat\nDisconnect / leave / idle: no Coin\nSame account or device: no reward");
		if (B(jToken["blockSameIp"]))
		{
			text = text + "\n" + T("IP เด\u0e35ยวก\u0e31น: ไม\u0e48ได\u0e49รางว\u0e31ล", "Same IP: no reward");
		}
		if (I(jToken["pairDailyLimit"]) > 0)
		{
			text = text + "\n" + T("ค\u0e39\u0e48เด\u0e34มน\u0e31บผลไม\u0e48เก\u0e34น ", "Same pair limited to ") + N(jToken["pairDailyLimit"]) + T(" แมตช\u0e4cต\u0e48อว\u0e31น", " matches per day");
		}
		Label(text, -265f, -130f, 480, 132, 19);
		JArray jArray2 = jToken["awards"] as JArray;
		Label(T("ฉายาท\u0e35\u0e48ค\u0e38ณได\u0e49ร\u0e31บในโหมด ", "Your earned titles in ") + mode, 285f, -43f, 470, 40, 23, Gold);
		Label((jArray2 == null || jArray2.Count == 0) ? T("ย\u0e31งไม\u0e48ม\u0e35ฉายาท\u0e35\u0e48ได\u0e49ร\u0e31บจากโหมดน\u0e35\u0e49", "No titles earned in this mode yet") : string.Join("\n", from x in jArray2.Reverse().Take(2)
			select Text(x["name"])), 285f, -130f, 470, 132, 21, null, NGUIText.Alignment.Center);
		Button(page, "RewardMode", T("เปล\u0e35\u0e48ยนโหมดเป\u0e47น ", "Switch mode to ") + ((mode == "1v1") ? "3v3" : "1v1"), 0f, -244f, 310, delegate
		{
			ChangeMode((mode == "1v1") ? "3v3" : "1v1");
		});
	}

	private void Rules()
	{
		JToken jToken = data["rules"];
		Button(page, "BasicRules", T("การแข\u0e48งข\u0e31น", "Match rules"), -360f, 162f, 300, delegate
		{
			tab = 0;
			Draw();
		}, tab == 0);
		Button(page, "FairRules", T("เง\u0e37\u0e48อนไข / อ\u0e38ปกรณ\u0e4c", "Conditions / equipment"), -30f, 162f, 330, delegate
		{
			tab = 1;
			Draw();
		}, tab == 1);
		if (tab == 0)
		{
			string[] array = new string[3]
			{
				T("เตร\u0e35ยมพร\u0e49อมก\u0e48อนดวล", "Prepare for the duel"),
				T("สามยก สามสภาพอากาศ", "Three rounds, three conditions"),
				T("ส\u0e39\u0e49ให\u0e49จบ แล\u0e49วกล\u0e31บบ\u0e49าน", "Finish and return safely")
			};
			IEnumerable<string> values = ((jToken["roundWeather"] as JArray) ?? new JArray()).Select((JToken t) => (!(S(t) == "fog")) ? ((!(S(t) == "rain")) ? T("พาย\u0e38วงหด", "Shrinking storm") : T("ฝน", "Rain")) : T("หมอก", "Fog"));
			string[] array2 = new string[3]
			{
				(B(jToken["ownGear"]) ? T("ใช\u0e49ต\u0e31วละครและอ\u0e38ปกรณ\u0e4cของต\u0e31วเอง", "Use your own character and gear") : T("ช\u0e38ดมาตรฐานเท\u0e48าก\u0e31นท\u0e38กคน", "Equal standard kits")) + T("\nเปล\u0e35\u0e48ยนอาว\u0e38ธ/เกราะช\u0e48วงพ\u0e31ก\nเลเวลข\u0e31\u0e49นต\u0e48ำ ", "\nChange gear during breaks\nMinimum level ") + N(jToken["levelMin"]) + T(" • ว\u0e31ดระด\u0e31บ ", " • Placement ") + N(jToken["placementGames"]) + T(" แมตช\u0e4c", " matches"),
				string.Join(" → ", values) + T("\nชนะ ", "\nWin ") + N(jToken["roundsToWin"]) + T(" ใน ", " of ") + (I(jToken["roundsToWin"]) * 2 - 1) + T(" ยก\nยกละ ", " rounds\nRound: ") + N(jToken["roundSec"]) + T(" ว\u0e34 • พ\u0e31ก ", "s • Break: ") + N(jToken["breakSec"]) + T(" ว\u0e34\nเสมอ: ต\u0e48อเวลา ", "s\nTie: overtime ") + N(jToken["overtimeSec"]) + T(" ว\u0e34", "s"),
				T("ยาช\u0e38ดแจก ", "Healing items: ") + N(jToken["potionsPerRound"]) + T(" ช\u0e34\u0e49นต\u0e48อยก\nไม\u0e48ตายจร\u0e34ง • ไม\u0e48ม\u0e35ของตก • ไม\u0e48ม\u0e35ช\u0e38บ\nย\u0e37นน\u0e34\u0e48ง ", " per round\nNo real death, drops or revives\nIdle ") + N(jToken["idleSec"]) + T(" ว\u0e34 = แพ\u0e49ยก\nหล\u0e38ด ", "s = round loss\nDisconnect ") + N(jToken["disconnectSec"]) + T(" ว\u0e34 = แพ\u0e49แมตช\u0e4c", "s = match loss")
			};
			for (int num = 0; num < 3; num++)
			{
				float x = -390 + num * 390;
				Sprite("bg_roundbox2", x, -27f, 370, 300, new Color(0.22f, 0.19f, 0.14f, 0.5f));
				Label("0" + (num + 1), x, 95f, 322, 40, 34, Gold);
				Label(array[num], x, 46f, 322, 54, 24, Gold);
				Label(array2[num], x, -72f, 322, 170, 20);
			}
			Label(T("หมดเวลา: เท\u0e35ยบ %เล\u0e37อด • 3v3 เท\u0e35ยบท\u0e31\u0e49งท\u0e35ม • ท\u0e35มแพ\u0e49ยกเล\u0e37อกฝ\u0e31\u0e48งเก\u0e34ด", "Time limit: HP% • 3v3 uses team HP • Round loser picks spawn side"), 0f, -218f, 1120, 40, 20, Muted, NGUIText.Alignment.Center);
		}
		else
		{
			Sprite("bg_roundbox2", 0f, -20f, 1120, 280, new Color(0.14f, 0.12f, 0.08f, 0.42f));
			Label(B(jToken["ownGear"]) ? T("ใช\u0e49ต\u0e31วละครและอ\u0e38ปกรณ\u0e4cของต\u0e31วเอง", "Use your own character and equipment") : T("ใช\u0e49ช\u0e38ดมาตรฐานในแมตช\u0e4c", "Standard kit in the match"), 0f, 85f, 1030, 52, 30, Gold, NGUIText.Alignment.Center);
			Label(T("เลเวลข\u0e31\u0e49นต\u0e48ำ ", "Minimum level ") + N(jToken["levelMin"]) + T(" • ว\u0e31ดระด\u0e31บ ", " • Placement ") + N(jToken["placementGames"]) + T(" แมตช\u0e4c\nยาช\u0e38ดแจก ", " matches\nMatch healing items: ") + N(jToken["potionsPerRound"]) + T(" ช\u0e34\u0e49นต\u0e48อยก • เปล\u0e35\u0e48ยนอาว\u0e38ธ/เกราะช\u0e48วงพ\u0e31ก", " per round • Change gear during breaks"), 0f, 13f, 1030, 90, 24, null, NGUIText.Alignment.Center);
			Line(-48f);
			Label(T("ย\u0e37นน\u0e34\u0e48ง ", "Idle ") + N(jToken["idleSec"]) + T(" ว\u0e34นาท\u0e35 = แพ\u0e49ยก\nหล\u0e38ดครบ ", " seconds = round loss\nDisconnected ") + N(jToken["disconnectSec"]) + T(" ว\u0e34นาท\u0e35 / ออกก\u0e48อนจบ = แพ\u0e49แมตช\u0e4c\nไม\u0e48ตายจร\u0e34ง ไม\u0e48ม\u0e35ของตก ห\u0e49ามช\u0e38บ และห\u0e49ามส\u0e31ตว\u0e4cเล\u0e35\u0e49ยง", " seconds / leave = match loss\nNo real death or drops; no revives or pets"), 0f, -138f, 1030, 145, 25, null, NGUIText.Alignment.Center);
		}
		Label(T("ค\u0e48ากต\u0e34กาป\u0e31จจ\u0e38บ\u0e31นจากเซ\u0e34ร\u0e4cฟเวอร\u0e4c • ไม\u0e48ได\u0e49ร\u0e31บ EXP", "Current server rules • No EXP"), 0f, -244f, 1030, 32, 19, Muted, NGUIText.Alignment.Center);
	}

	private void RegisterBridge()
	{
		Type typeFromHandle = typeof(TestBridge);
		object obj = typeFromHandle.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
		if (obj != null)
		{
			Delegate obj2 = Delegate.CreateDelegate(typeFromHandle.GetNestedType("Handler", BindingFlags.NonPublic), this, GetType().GetMethod("Command", Private));
			typeFromHandle.GetMethod("Add", Private).Invoke(obj, new object[3] { "rankpages", "rankpages open ladder|rewards|rules / mode 1v1|3v3 / tab 0|1 / refresh / close / state", obj2 });
		}
	}

	private IEnumerator Command(string[] args, TestBridge.Result result)
	{
		if (args.Length > 1)
		{
			string text = args[1];
			if (text == "capture" && args.Length > 2)
			{
				yield return null;
				yield return null;
				yield return new WaitForEndOfFrame();
				RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
				RenderTexture active = RenderTexture.active;
				try
				{
					foreach (Camera item in from c in Camera.allCameras
						where c.targetTexture != null
						orderby c.depth
						select c)
					{
						item.Render();
					}
					RenderTexture world = null;
					if (Singleton<MainCamera>.HasInstance())
					{
						world = Singleton<MainCamera>.Instance().TargetTexture;
					}
					if (world != null)
					{
						Graphics.Blit(world, renderTexture);
					}
					foreach (Camera item2 in from c in Camera.allCameras
						where c.targetTexture == null && (world == null || c.GetComponent<UICamera>() != null)
						orderby c.depth
						select c)
					{
						try
						{
							item2.targetTexture = renderTexture;
							item2.Render();
						}
						finally
						{
							item2.targetTexture = null;
						}
					}
					RenderTexture.active = renderTexture;
					Texture2D texture2D = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, mipChain: false);
					texture2D.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
					texture2D.Apply();
					File.WriteAllBytes(args[2], texture2D.EncodeToPNG());
					UnityEngine.Object.Destroy(texture2D);
				}
				finally
				{
					RenderTexture.active = active;
					UnityEngine.Object.Destroy(renderTexture);
				}
				result.Set("path", args[2]);
				yield break;
			}
			if (text == "open" && args.Length > 2)
			{
				Open(args[2]);
			}
			else
			{
				switch (text)
				{
				case "close":
					Hide();
					break;
				case "refresh":
					Request();
					break;
				case "mode":
					if (args.Length > 2 && (args[2] == "1v1" || args[2] == "3v3"))
					{
						ChangeMode(args[2]);
						break;
					}
					goto default;
				default:
					if (text == "tab" && args.Length > 2)
					{
						tab = ((args[2] == "1") ? 1 : 0);
						Draw();
					}
					else if (text != "state")
					{
						result.Fail("Unknown ranked pages command");
						yield break;
					}
					break;
				}
			}
		}
		result.Set("open", window != null && window.IsOpened).Set("screen", screen).Set("mode", mode)
			.Set("state", state)
			.Set("error", error)
			.Set("pending", pending)
			.Set("responseId", responseId)
			.Set("received", received)
			.Set("ignored", ignored)
			.Set("tab", tab)
			.Set("dataJson", (data == null) ? null : data.ToString(Formatting.None))
			.Set("source", "authenticated RankedDown kind 8")
			.Set("width", Screen.width)
			.Set("height", Screen.height)
			.Set("labels", (page != null && page.activeInHierarchy) ? (from l in page.GetComponentsInChildren<UILabel>()
				where l.isActiveAndEnabled && l.finalAlpha > 0.01f
				select l.text).ToArray() : new string[0]);
	}
}

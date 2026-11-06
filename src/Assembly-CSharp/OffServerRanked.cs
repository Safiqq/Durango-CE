using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.Terrain;
using Durango.Utils;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class OffServerRanked : GameSystem<OffServerRanked>
{
	public const string Mode1v1 = "1v1";

	public const string Mode3v3 = "3v3";

	public static readonly string[] TierIds = new string[6] { "copper", "silver", "gold", "platinum", "diamond", "master" };

	public const string Caps = "pvp-ranked";

	public static bool DemoArena;

	private bool _wasAvailable;

	public static string LastReceived = string.Empty;

	private static readonly HashSet<string> _enemies = new HashSet<string>();

	private static readonly HashSet<string> _allies = new HashSet<string>();

	private static readonly string[] DemoWeapons = new string[9] { "sword_onehand_metal_01", "sword_twohand_metal_01", "axe_onehand_metal_01", "axe_twohand_metal_01", "hammer_onehand_metal_01", "hammer_twohand_metal_01", "lance_twohand_metal_01", "bow_wooden_02", "crossbow_wooden_01" };

	public static bool StateKnown { get; private set; }

	public static JObject Lobby { get; private set; }

	public static JObject Match { get; private set; }

	public static JObject Result { get; private set; }

	public static JObject Found { get; private set; }

	public static JObject Coin { get; private set; }

	public static float FoundAt { get; private set; }

	public static int Received { get; private set; }

	public static bool ServerEnabled
	{
		get
		{
			if (Lobby != null)
			{
				return Flag(Lobby["enabled"]);
			}
			return false;
		}
	}

	public static bool Available
	{
		get
		{
			if (StateKnown && ServerEnabled && GameManager.IsReady && !GameManager.IsPrologueMode && OffServerLink.Active)
			{
				return !InMatch;
			}
			return false;
		}
	}

	public static bool OwnGear
	{
		get
		{
			if (!Flag(Lobby?["ownGear"]))
			{
				return Flag(Match?["ownGear"]);
			}
			return true;
		}
	}

	public static bool ReplacesBrawl
	{
		get
		{
			if (OffServerLink.Active)
			{
				return ServerEnabled;
			}
			return false;
		}
	}

	public static bool InMatch
	{
		get
		{
			if (Match != null && GameManager.IsReady)
			{
				if (!DemoArena)
				{
					if (GameManager.Region != null)
					{
						return GameManager.Region.IsPvpIsland();
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public static bool Fighting
	{
		get
		{
			string phase = Phase;
			if (InMatch && (phase == "Live" || phase == "Overtime"))
			{
				return !Flag(Match["out"]);
			}
			return false;
		}
	}

	public static bool Queued
	{
		get
		{
			if (Lobby != null)
			{
				return Lobby["queue"] is JObject;
			}
			return false;
		}
	}

	public static string QueuedMode
	{
		get
		{
			if (!Queued)
			{
				return string.Empty;
			}
			return Str(Lobby["queue"]["mode"]);
		}
	}

	public static string Phase
	{
		get
		{
			if (Match == null)
			{
				return string.Empty;
			}
			return Str(Match["phase"]);
		}
	}

	public static int MyTeam
	{
		get
		{
			if (Match == null)
			{
				return -1;
			}
			return Int(Match["myTeam"], -1);
		}
	}

	public static double PhaseLeft
	{
		get
		{
			if (Match == null)
			{
				return 0.0;
			}
			double num = Num(Match["endsAt"]);
			return Math.Max(0.0, num - Now);
		}
	}

	public static double Now
	{
		get
		{
			try
			{
				return Connections.Frontend.GetPredictedServerTime();
			}
			catch (Exception)
			{
				return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
			}
		}
	}

	public static bool Thai => OffServerDiscordLink.Lang == "th";

	public static event Action Changed;

	public static event Action<string> ToastReceived;

	public static JObject CoinFor(JObject result)
	{
		if (Coin == null)
		{
			return null;
		}
		string text = Str(result?["matchId"]);
		if (!string.IsNullOrEmpty(text) && !(text == Str(Coin["matchId"])))
		{
			return null;
		}
		return Coin;
	}

	public static bool IsEnemy(string entityId)
	{
		if (!string.IsNullOrEmpty(entityId))
		{
			return _enemies.Contains(entityId);
		}
		return false;
	}

	public static bool IsAlly(string entityId)
	{
		if (!string.IsNullOrEmpty(entityId))
		{
			return _allies.Contains(entityId);
		}
		return false;
	}

	private void Awake()
	{
		Connections.Frontend.On<RankedDown>(OnDown);
		Singleton<GameManager>.Instance().WelcomeReceived += OnWelcome;
	}

	private void OnDestroy()
	{
		if (Singleton<GameManager>.HasInstance())
		{
			Singleton<GameManager>.Instance().WelcomeReceived -= OnWelcome;
		}
	}

	private void OnWelcome(Welcome welcome)
	{
		StateKnown = false;
		Match = null;
		Found = null;
		_enemies.Clear();
		_allies.Clear();
		RaiseChanged();
	}

	private void Update()
	{
		if (GameManager.IsReady)
		{
			if (StateKnown || Match != null || Result != null || Found != null)
			{
				OffServerRankedUI.Ensure();
			}
			bool available = Available;
			if (available != _wasAvailable)
			{
				_wasAvailable = available;
				OffServerRankedUI.MenusChanged();
			}
		}
	}

	private void OnDown(RankedDown message, PacketHeader header)
	{
		if (OffServerRankedPages.OnPacket(message))
		{
			return;
		}
		try
		{
			JObject jObject = JObject.Parse(string.IsNullOrEmpty(message.Json) ? "{}" : message.Json);
			if (OffServerDuel.OnDown(message.Kind, jObject))
			{
				RaiseChanged();
				return;
			}
			Received++;
			LastReceived = message.Kind + " " + ((message.Json != null && message.Json.Length > 160) ? message.Json.Substring(0, 160) : message.Json);
			switch (message.Kind)
			{
			case 0:
				Lobby = jObject;
				StateKnown = true;
				break;
			case 1:
				Match = jObject;
				Result = null;
				RefreshTeams();
				break;
			case 2:
				Result = jObject;
				break;
			case 3:
			{
				string text = Str(jObject["text"]);
				if (jObject["titles"] is JArray { Count: >0 })
				{
					OffServerRankedUI.SeasonTitles(text);
				}
				else if (!string.IsNullOrEmpty(text))
				{
					UIManager.SystemMsg(OneLanguage(text));
					if (OffServerRanked.ToastReceived != null)
					{
						OffServerRanked.ToastReceived(text);
					}
				}
				break;
			}
			case 4:
				Found = jObject;
				FoundAt = Time.realtimeSinceStartup;
				Coin = null;
				break;
			case 7:
				Coin = jObject;
				break;
			}
			RaiseChanged();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static void RefreshTeams()
	{
		_enemies.Clear();
		_allies.Clear();
		JArray jArray = Match?["teams"] as JArray;
		int myTeam = MyTeam;
		if (jArray == null || myTeam < 0)
		{
			return;
		}
		for (int i = 0; i < jArray.Count; i++)
		{
			if (!(jArray[i] is JArray jArray2))
			{
				continue;
			}
			foreach (JToken item in (IEnumerable<JToken>)jArray2)
			{
				string text = Str(item["id"]);
				if (!string.IsNullOrEmpty(text))
				{
					((i == myTeam) ? _allies : _enemies).Add(text);
				}
			}
		}
	}

	private static void RaiseChanged()
	{
		try
		{
			OffServerRanked.Changed?.Invoke();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static double Num(JToken t, double def = 0.0)
	{
		if (!(t is JValue { Value: not null } jValue))
		{
			return def;
		}
		try
		{
			return Convert.ToDouble(jValue.Value, CultureInfo.InvariantCulture);
		}
		catch (Exception)
		{
			return def;
		}
	}

	public static double? NumOrNull(JToken t)
	{
		if (!(t is JValue { Value: not null }))
		{
			return null;
		}
		return Num(t);
	}

	public static int Int(JToken t, int def = 0)
	{
		if (!(t is JValue { Value: not null }))
		{
			return def;
		}
		return (int)Math.Round(Num(t, def));
	}

	public static string Str(JToken t)
	{
		if (!(t is JValue { Value: not null } jValue))
		{
			return string.Empty;
		}
		return Convert.ToString(jValue.Value, CultureInfo.InvariantCulture);
	}

	public static bool Flag(JToken t)
	{
		if (t is JValue jValue && jValue.Value is bool)
		{
			return (bool)jValue.Value;
		}
		return false;
	}

	public static void Queue(string mode)
	{
		Send(1, new JObject { ["mode"] = mode });
	}

	public static void Dequeue()
	{
		Send(2, null);
	}

	public static void PickWeapon(string proto)
	{
		Send(3, new JObject { ["proto"] = proto });
	}

	public static void PickWeaponItem(string itemId, string proto)
	{
		Send(3, new JObject
		{
			["itemId"] = itemId,
			["proto"] = proto
		});
	}

	public static void PickSide(int side)
	{
		Send(4, new JObject { ["side"] = side });
	}

	public static void Leave()
	{
		Send(5, null);
	}

	public static void AskState()
	{
		Send(6, null);
	}

	private static void Inject(byte kind, string json)
	{
		GameSystem<OffServerRanked>.Instance().OnDown(new RankedDown
		{
			Kind = kind,
			Json = json
		}, default(PacketHeader));
	}

	private static JObject DemoPlayer(string id, string name, string tier, double hp, bool dead = false)
	{
		return new JObject
		{
			["id"] = id,
			["name"] = name,
			["tier"] = tier,
			["hp"] = hp,
			["max"] = 100.0,
			["out"] = dead
		};
	}

	private static JObject DemoItem(string id, string proto, int level)
	{
		return new JObject
		{
			["id"] = id,
			["proto"] = proto,
			["level"] = level,
			["slot"] = ((proto.StartsWith("bow") || proto.Contains("twohand")) ? "both" : "main")
		};
	}

	private static JObject DemoRound(int round, int winner, string reason, double hp0, double hp1)
	{
		JObject jObject = new JObject();
		jObject["round"] = round;
		jObject["winner"] = winner;
		jObject["reason"] = reason;
		jObject["hp"] = new JArray(hp0, hp1);
		return jObject;
	}

	private static JObject DemoRing(string screen, double now)
	{
		Vector2 vector = Vector2.zero;
		if (PlayerBehavior.LocalPlayer != null)
		{
			vector = Util.ClientPositionToTilePosition(PlayerBehavior.LocalPlayer.CurrentPosition);
		}
		bool flag = screen == "ot";
		JObject jObject = new JObject
		{
			["cx"] = Math.Floor((double)vector.x + (flag ? 6.0 : 3.0)) + 0.5,
			["cz"] = Math.Floor(vector.y) + 0.5,
			["r"] = (flag ? 4.0 : 14.0),
			["r0"] = 14.0,
			["rMin"] = 4.0,
			["dmg"] = 0.08
		};
		if (screen == "storm")
		{
			jObject["shrinkFrom"] = now - 30.0;
			jObject["shrinkTo"] = now + 90.0;
		}
		return jObject;
	}

	private static string Demo(string screen, bool m3)
	{
		double now = Now;
		string text = (m3 ? "3v3" : "1v1");
		string text2 = ((PlayerBehavior.LocalPlayer != null) ? PlayerBehavior.LocalPlayer.PlayerName : "ShuShi");
		JArray jArray = (m3 ? new JArray(DemoPlayer("me", text2, "gold", 82.0), DemoPlayer("a1", "ใบไม\u0e49ป\u0e48า", "gold", 40.0), DemoPlayer("a2", "Tarbo99", "platinum", 0.0, dead: true)) : new JArray(DemoPlayer("me", text2, "gold", 82.0)));
		JArray jArray2 = (m3 ? new JArray(DemoPlayer("f1", "RaptorKim", "gold", 55.0), DemoPlayer("f2", "ห\u0e34นเหล\u0e47กไฟ", "silver", 90.0), DemoPlayer("f3", "MoaMoa", "master", 20.0)) : new JArray(DemoPlayer("f1", "RaptorKim", "gold", 64.0)));
		switch (screen)
		{
		case "off":
			DemoArena = false;
			Match = null;
			Result = null;
			Found = null;
			RefreshTeams();
			RaiseChanged();
			AskState();
			return "demo off";
		case "lobby":
		case "queue":
		case "place":
		case "master":
		{
			JObject jObject8 = new JObject
			{
				["tier"] = "gold",
				["next"] = "platinum",
				["progress"] = 0.62,
				["rating"] = null,
				["games"] = 27,
				["wins"] = 18,
				["losses"] = 9,
				["placement"] = 0,
				["rank"] = 41
			};
			JObject value3 = new JObject
			{
				["tier"] = "gold",
				["next"] = "platinum",
				["progress"] = 0.35,
				["rating"] = null,
				["games"] = 18,
				["wins"] = 11,
				["losses"] = 7,
				["placement"] = 0,
				["rank"] = 120
			};
			if (screen == "place")
			{
				jObject8 = new JObject
				{
					["tier"] = "",
					["next"] = "",
					["progress"] = null,
					["rating"] = null,
					["games"] = 2,
					["wins"] = 1,
					["losses"] = 1,
					["placement"] = 3,
					["rank"] = 0
				};
				value3 = (JObject)jObject8.DeepClone();
			}
			else if (screen == "master")
			{
				jObject8 = new JObject
				{
					["tier"] = "master",
					["next"] = "",
					["progress"] = null,
					["rating"] = 2164,
					["games"] = 80,
					["wins"] = 61,
					["losses"] = 19,
					["placement"] = 0,
					["rank"] = 3
				};
				value3 = new JObject
				{
					["tier"] = "master",
					["next"] = "",
					["progress"] = null,
					["rating"] = 2088,
					["games"] = 40,
					["wins"] = 29,
					["losses"] = 11,
					["placement"] = 0,
					["rank"] = 7
				};
			}
			JObject obj = new JObject
			{
				["enabled"] = true,
				["levelMin"] = 30,
				["season"] = "2026-10",
				["seasonEndsAt"] = Math.Floor(now) + 1036800.0,
				["now"] = now,
				["queue"] = ((screen == "queue") ? new JObject
				{
					["mode"] = text,
					["since"] = now - 42.0,
					["count"] = 3,
					["size"] = 1
				} : null),
				["inMatch"] = false,
				["modes"] = new JObject
				{
					["1v1"] = jObject8,
					["3v3"] = value3
				},
				["awards"] = new JArray()
			};
			object[] demoWeapons = DemoWeapons;
			obj["weapons"] = new JArray(demoWeapons);
			obj["potion"] = "hp_medicine_store";
			obj["potionsPerRound"] = 3;
			JObject jObject9 = obj;
			DemoArena = false;
			Match = null;
			Result = null;
			Found = null;
			Inject(0, jObject9.ToString(Formatting.None));
			OffServerRankedUI.OpenLobby(text, ask: false);
			return "demo " + screen + " · " + OffServerRankedUI.Describe();
		}
		case "found":
		{
			DemoArena = false;
			Match = null;
			Result = null;
			JObject jObject2 = new JObject();
			jObject2["mode"] = text;
			jObject2["rated"] = true;
			jObject2["myTeam"] = 0;
			jObject2["teams"] = new JArray(jArray, jArray2);
			jObject2["departAt"] = now + 5.0;
			JObject jObject5 = jObject2;
			Inject(4, jObject5.ToString(Formatting.None));
			return "demo found · " + OffServerRankedUI.Describe();
		}
		case "hud":
		case "count":
		case "storm":
		case "break":
		case "ot":
		case "breakown":
		case "breakwin":
		{
			DemoArena = true;
			Found = null;
			Result = null;
			bool flag2 = screen.StartsWith("break");
			string text3 = ((screen == "count") ? "Countdown" : ((screen == "ot") ? "Overtime" : (flag2 ? "Break" : "Live")));
			int num = ((screen == "count") ? 1 : ((screen == "storm") ? 3 : 2));
			JObject jObject2 = new JObject();
			jObject2["matchId"] = "demo";
			jObject2["mode"] = text;
			jObject2["phase"] = text3;
			jObject2["round"] = num;
			jObject2["score"] = ((screen == "count") ? new JArray(0, 0) : ((flag2 || screen == "storm") ? new JArray(1, 1) : new JArray(1, 0)));
			jObject2["endsAt"] = now + ((screen == "count") ? 3.0 : (flag2 ? 14.0 : ((text3 == "Overtime") ? 18.0 : 84.0)));
			jObject2["now"] = now;
			jObject2["weather"] = ((num <= 1) ? "pvp_fog" : ((num == 2) ? "pvp_rain" : "pvp_storm_01"));
			jObject2["roundsToWin"] = 2;
			jObject2["myTeam"] = 0;
			jObject2["teams"] = new JArray(jArray, jArray2);
			jObject2["sideOf"] = new JArray(0, 1);
			jObject2["loser"] = ((screen == "breakwin") ? 1 : 0);
			jObject2["potions"] = ((screen == "count") ? 3 : 2);
			jObject2["weapon"] = "lance_twohand_metal_01";
			JObject jObject6 = jObject2;
			object[] demoWeapons = DemoWeapons;
			jObject6["weapons"] = new JArray(demoWeapons);
			jObject2["winner"] = -1;
			jObject2["rounds"] = new JArray();
			jObject2["ring"] = DemoRing(screen, now);
			JObject jObject7 = jObject2;
			if (screen == "breakown")
			{
				JArray value2 = new JArray(DemoItem("w1", "sword_onehand_bone_03", 48), DemoItem("w2", "bow_metal_01", 52), DemoItem("w3", "lance_twohand_metal_02", 55), DemoItem("w4", "axe_onehand_stone_01", 12));
				jObject7["ownGear"] = true;
				jObject7["weaponItems"] = value2;
				jObject7["weapons"] = new JArray("sword_onehand_bone_03", "bow_metal_01", "lance_twohand_metal_02", "axe_onehand_stone_01");
				jObject7["weapon"] = "bow_metal_01";
			}
			Inject(1, jObject7.ToString(Formatting.None));
			return "demo " + screen + " · " + OffServerRankedUI.Describe();
		}
		case "win":
		case "place5":
		case "dc":
		case "gm":
		case "down":
		{
			DemoArena = true;
			bool flag = screen != "down" && screen != "dc";
			JObject jObject = new JObject
			{
				["counted"] = true,
				["placementLeft"] = 0
			};
			switch (screen)
			{
			case "down":
				jObject["tierBefore"] = "gold";
				jObject["tierAfter"] = "silver";
				jObject["progressBefore"] = 0.05;
				jObject["progressAfter"] = 0.85;
				jObject["next"] = "gold";
				break;
			case "gm":
				jObject["tierBefore"] = "master";
				jObject["tierAfter"] = "master";
				jObject["ratingBefore"] = 2146;
				jObject["ratingAfter"] = 2164;
				jObject["delta"] = 18;
				break;
			case "dc":
				jObject["tierBefore"] = "gold";
				jObject["tierAfter"] = "gold";
				jObject["progressBefore"] = 0.62;
				jObject["progressAfter"] = 0.55;
				jObject["next"] = "platinum";
				break;
			case "place5":
				jObject["tierBefore"] = "";
				jObject["tierAfter"] = "gold";
				jObject["progressBefore"] = null;
				jObject["progressAfter"] = 0.4;
				jObject["next"] = "platinum";
				break;
			default:
				jObject["tierBefore"] = "gold";
				jObject["tierAfter"] = "platinum";
				jObject["progressBefore"] = 0.0;
				jObject["progressAfter"] = 0.08;
				jObject["next"] = "diamond";
				break;
			}
			JArray value = ((screen == "dc") ? new JArray(DemoRound(1, 0, "ko", 0.71, 0.0), DemoRound(2, 1, "disconnect", 0.5, 0.8)) : ((screen == "down") ? new JArray(DemoRound(1, 1, "ko", 0.0, 0.4), DemoRound(2, 0, "time", 0.64, 0.38), DemoRound(3, 1, "ko", 0.0, 0.22)) : new JArray(DemoRound(1, 0, "ko", 0.71, 0.0), DemoRound(2, 1, "first_hit", 0.4, 0.6), DemoRound(3, 0, "ko", 0.38, 0.0))));
			JObject jObject2 = new JObject();
			jObject2["winner"] = ((!flag) ? 1 : 0);
			jObject2["myTeam"] = 0;
			jObject2["reason"] = ((screen == "dc") ? "disconnect" : "rounds");
			jObject2["score"] = (flag ? new JArray(2, 1) : new JArray(1, 2));
			jObject2["rounds"] = value;
			jObject2["change"] = jObject;
			jObject2["rated"] = true;
			JObject jObject3 = jObject2;
			if (Match == null)
			{
				jObject2 = new JObject();
				jObject2["matchId"] = "demo";
				jObject2["mode"] = text;
				jObject2["phase"] = "Finished";
				jObject2["myTeam"] = 0;
				jObject2["teams"] = new JArray(jArray, jArray2);
				Match = jObject2;
			}
			Inject(2, jObject3.ToString(Formatting.None));
			JObject jObject4 = ((screen == "dc") ? new JObject
			{
				["matchId"] = "",
				["granted"] = false,
				["amount"] = 0,
				["won"] = false,
				["reason"] = "disconnect",
				["remaining"] = 6,
				["cap"] = 10,
				["text"] = T("หล\u0e38ดเกม/ออกก\u0e48อนจบ — ไม\u0e48ได\u0e49 Coin", "Disconnected or left — no Coin")
			} : new JObject
			{
				["matchId"] = "",
				["granted"] = true,
				["amount"] = (flag ? 10 : 5),
				["won"] = flag,
				["reason"] = "",
				["remaining"] = 6,
				["cap"] = 10,
				["coin"] = 120
			});
			Inject(7, jObject4.ToString(Formatting.None));
			return "demo " + screen + " · " + OffServerRankedUI.Describe();
		}
		case "title":
			Inject(3, new JObject
			{
				["text"] = "ปรมาจารย\u0e4cดวล 1v1 · ฤด\u0e39 2026-10\nท\u0e47อป 10 ดวล 1v1 · ฤด\u0e39 2026-10",
				["titles"] = new JArray("ranked_1v1_master_2026_10", "ranked_1v1_top10_2026_10")
			}.ToString(Formatting.None));
			return "demo title";
		default:
			return "demo lobby|queue|place|master|found|hud|count|ot|storm|break|breakwin|breakown|win|down|gm|dc|place5|title|off [3v3]";
		}
	}

	public static void CloseResult()
	{
		Result = null;
		RaiseChanged();
	}

	private static void Send(byte kind, JObject json)
	{
		if (OffServerLink.Active && GameManager.IsReady)
		{
			Connections.Frontend.Send(new RankedUp
			{
				Kind = kind,
				Json = (json ?? new JObject()).ToString(Formatting.None)
			}, noReply: true);
		}
	}

	public static string T(string th, string en)
	{
		if (!Thai)
		{
			return en;
		}
		return th;
	}

	public static string OneLanguage(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		int num = text.IndexOf('\n');
		if (num < 0)
		{
			return text;
		}
		if (!Thai)
		{
			return text.Substring(num + 1);
		}
		return text.Substring(0, num);
	}

	public static string TierName(string tier)
	{
		switch (tier)
		{
		case "copper":
			return T("ทองแดง", "Copper");
		case "silver":
			return T("เง\u0e34น", "Silver");
		case "gold":
			return T("ทอง", "Gold");
		case "platinum":
			return T("แพลต\u0e34น\u0e31ม", "Platinum");
		case "diamond":
			return T("เพชร", "Diamond");
		case "master":
			return T("ปรมาจารย\u0e4c", "Master");
		case "top10":
			return T("ท\u0e47อป 10", "Top 10");
		case "":
		case null:
			return T("ว\u0e31ดระด\u0e31บ", "Placement");
		default:
			return tier;
		}
	}

	public static Color TierColor(string tier)
	{
		return tier switch
		{
			"copper" => new Color(0.72f, 0.45f, 0.29f),
			"silver" => new Color(0.75f, 0.78f, 0.82f),
			"gold" => new Color(1f, 0.8f, 0.3f),
			"platinum" => new Color(0.45f, 0.85f, 0.8f),
			"diamond" => new Color(0.55f, 0.7f, 1f),
			"master" => new Color(0.85f, 0.45f, 1f),
			_ => new Color(0.5f, 0.5f, 0.5f),
		};
	}

	public static string WeatherIcon(string weather)
	{
		if (!(weather == "pvp_fog"))
		{
			if (weather == "pvp_rain")
			{
				return "icon_se_pvp_rain";
			}
			return "icon_se_pvp_storm";
		}
		return "icon_se_pvp_fog";
	}

	public static string WeatherName(string weather)
	{
		if (!(weather == "pvp_fog"))
		{
			if (weather == "pvp_rain")
			{
				return T("ฝน", "Rain");
			}
			return T("พาย\u0e38วงหด", "Storm");
		}
		return T("หมอก", "Fog");
	}

	public static string PhaseName(string phase)
	{
		return phase switch
		{
			"Arriving" => T("รอคนครบ", "Waiting"),
			"Countdown" => T("น\u0e31บถอยหล\u0e31ง", "Get ready"),
			"Live" => T("ส\u0e39\u0e49!", "Fight!"),
			"Overtime" => T("ต\u0e48อเวลา — โดนต\u0e35ก\u0e48อนแพ\u0e49", "Overtime — first hit loses"),
			"Break" => T("พ\u0e31ก", "Break"),
			"Finished" => T("จบแมตช\u0e4c", "Match over"),
			_ => phase ?? string.Empty,
		};
	}

	public static string ReasonName(string reason)
	{
		return reason switch
		{
			"ko" => T("เล\u0e37อดหมด", "Knock-out"),
			"idle" => T("ย\u0e37นน\u0e34\u0e48ง", "Idle"),
			"time" => T("หมดเวลา", "Time"),
			"first_hit" => T("ต\u0e48อเวลา", "Overtime"),
			"coin" => T("ต\u0e48อเวลา (ส\u0e38\u0e48ม)", "Overtime (draw)"),
			"rounds" => T("ชนะ 2 ใน 3", "Best of 3"),
			"leave" => T("ออกก\u0e48อนจบ", "Left early"),
			"disconnect" => T("หล\u0e38ดเกม", "Disconnected"),
			"noshow" => T("อ\u0e35กฝ\u0e48ายไม\u0e48มา", "No-show"),
			"cancel" => T("ยกเล\u0e34ก", "Cancelled"),
			_ => reason ?? string.Empty,
		};
	}

	public static string SideName(int side)
	{
		if (side != 0)
		{
			return T("ฝ\u0e31\u0e48งตะว\u0e31นออก", "East side");
		}
		return T("ฝ\u0e31\u0e48งตะว\u0e31นตก", "West side");
	}

	public static string WeaponName(string proto)
	{
		if (proto == null)
		{
			return string.Empty;
		}
		if (proto.StartsWith("sword_onehand"))
		{
			return T("ดาบม\u0e37อเด\u0e35ยว", "One-handed sword");
		}
		if (proto.StartsWith("sword_twohand"))
		{
			return T("ดาบสองม\u0e37อ", "Two-handed sword");
		}
		if (proto.StartsWith("axe_onehand"))
		{
			return T("ขวานม\u0e37อเด\u0e35ยว", "One-handed axe");
		}
		if (proto.StartsWith("axe_twohand"))
		{
			return T("ขวานสองม\u0e37อ", "Two-handed axe");
		}
		if (proto.StartsWith("hammer_onehand"))
		{
			return T("ค\u0e49อนม\u0e37อเด\u0e35ยว", "One-handed hammer");
		}
		if (proto.StartsWith("hammer_twohand"))
		{
			return T("ค\u0e49อนสองม\u0e37อ", "Two-handed hammer");
		}
		if (proto.StartsWith("lance"))
		{
			return T("หอก", "Spear");
		}
		if (proto.StartsWith("crossbow"))
		{
			return T("หน\u0e49าไม\u0e49", "Crossbow");
		}
		if (proto.StartsWith("bow"))
		{
			return T("ธน\u0e39", "Bow");
		}
		return proto;
	}

	public static string Clock(double sec)
	{
		int num = Mathf.Max(0, Mathf.CeilToInt((float)sec));
		return (num / 60).ToString("0") + ":" + (num % 60).ToString("00");
	}

	public static string DebugCommand(string[] args)
	{
		switch ((args.Length > 1) ? args[1].ToLowerInvariant() : "state")
		{
		case "hello":
			Send(0, null);
			return "hello sent (by hand — the server pushes State on Ready for caps pvp-ranked)";
		case "fake":
			if (args.Length < 4)
			{
				return "ranked fake <kind 0-4> <json — ' in place of double quotes>";
			}
			Inject((byte)Mathf.Clamp(Convert.ToInt32(args[2]), 0, 255), string.Join(" ", args, 3, args.Length - 3).Replace('\'', '"'));
			return "fake " + args[2] + " ok · " + OffServerRankedUI.Describe();
		case "demo":
			return Demo((args.Length > 2) ? args[2].ToLowerInvariant() : "lobby", args.Length > 3 && args[3] == "3v3");
		case "ring":
			return OffServerRankedRing.DebugCommand(args) + " | " + OffServerRankedUI.Describe();
		case "anim":
			return OffServerRankedUI.AnimCommand(args);
		case "queue":
			Queue((args.Length > 2) ? args[2] : "1v1");
			return "queue sent";
		case "cancel":
			Dequeue();
			return "dequeue sent";
		case "weapon":
			PickWeapon((args.Length > 2) ? args[2] : string.Empty);
			return "weapon sent";
		case "side":
			PickSide((args.Length > 2 && args[2] == "1") ? 1 : 0);
			return "side sent";
		case "leave":
			Leave();
			return "leave sent";
		case "lobby":
			OffServerRankedUI.OpenLobby();
			return "lobby open · " + OffServerRankedUI.Describe();
		case "close":
			OffServerRankedUI.CloseAll();
			return "closed";
		default:
			return "known " + StateKnown + " enabled " + ServerEnabled + " available " + Available + " inMatch " + InMatch + " queued " + QueuedMode + " phase " + Phase + " team " + MyTeam + " enemies " + _enemies.Count + " received " + Received + " last " + LastReceived + " | " + OffServerRankedUI.Describe();
		}
	}
}

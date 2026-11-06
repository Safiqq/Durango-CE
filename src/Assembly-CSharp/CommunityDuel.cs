using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.Terrain;
using Durango.Utils;
using InteractionData;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CommunityDuel : GameSystem<CommunityDuel>
{
	private sealed class Ring
	{
		public string Id;

		public Vector2 Center;

		public float Radius;

		public string A;

		public string B;
	}

	public const string Mode = "duel";

	public const string Caps = "pvp-duel";

	private const string RingPrefix = "duel:";

	private const string OutsideKey = "CommunityDuelOut";

	private static readonly List<Ring> _rings = new List<Ring>();

	private static readonly HashSet<string> _shown = new HashSet<string>(StringComparer.Ordinal);

	private static InteractionSystem _hookedOn;

	private float _nextOutside;

	public static string LastReceived = string.Empty;

	public static bool Enabled { get; private set; }

	public static JObject Match { get; private set; }

	public static JObject LastInvite { get; private set; }

	public static bool InDuel
	{
		get
		{
			if (Match != null && GameManager.IsReady)
			{
				return CommunityRanked.Str(Match["phase"]) != "Finished";
			}
			return false;
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
			return CommunityRanked.Str(Match["phase"]);
		}
	}

	public static bool Fighting
	{
		get
		{
			string phase = Phase;
			if (InDuel && (phase == "Live" || phase == "Overtime"))
			{
				return !CommunityRanked.Flag(Match["out"]);
			}
			return false;
		}
	}

	public static string OpponentId
	{
		get
		{
			if (Match == null)
			{
				return string.Empty;
			}
			int num = CommunityRanked.Int(Match["myTeam"], -1);
			if (!(Match["teams"] is JArray jArray) || num < 0 || jArray.Count != 2 || !(jArray[1 - num] is JArray { Count: not 0 } jArray2))
			{
				return string.Empty;
			}
			return CommunityRanked.Str(jArray2[0]["id"]);
		}
	}

	public static bool IsOpponent(string entityId)
	{
		if (!string.IsNullOrEmpty(entityId))
		{
			return entityId == OpponentId;
		}
		return false;
	}

	public static bool IsDueling(string entityId)
	{
		if (string.IsNullOrEmpty(entityId))
		{
			return false;
		}
		for (int i = 0; i < _rings.Count; i++)
		{
			if (_rings[i].A == entityId || _rings[i].B == entityId)
			{
				return true;
			}
		}
		return false;
	}

	private void Awake()
	{
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
		Match = null;
		_rings.Clear();
		HideRings();
	}

	public static bool OnDown(byte kind, JObject json)
	{
		if (json == null)
		{
			return false;
		}
		bool flag = CommunityRanked.Str(json["mode"]) == "duel";
		if (kind != 5 && kind != 6 && !((kind == 1 || kind == 2) && flag))
		{
			return false;
		}
		try
		{
			LastReceived = kind + " " + json.ToString(Formatting.None);
			if (LastReceived.Length > 160)
			{
				LastReceived = LastReceived.Substring(0, 160);
			}
			switch (kind)
			{
			case 1:
				Match = json;
				break;
			case 2:
				Match = null;
				break;
			case 5:
				LastInvite = json;
				ShowInvite(json);
				break;
			case 6:
				Enabled = CommunityRanked.Flag(json["enabled"]);
				_rings.Clear();
				if (!(json["rings"] is JArray jArray))
				{
					break;
				}
				foreach (JToken item in (IEnumerable<JToken>)jArray)
				{
					_rings.Add(new Ring
					{
						Id = CommunityRanked.Str(item["id"]),
						Center = new Vector2((float)CommunityRanked.Num(item["cx"]), (float)CommunityRanked.Num(item["cz"])),
						Radius = (float)CommunityRanked.Num(item["r"]),
						A = CommunityRanked.Str(item["a"]),
						B = CommunityRanked.Str(item["b"])
					});
				}
				break;
			case 3:
			case 4:
				break;
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
		return true;
	}

	public static void Challenge(string targetId)
	{
		Send(7, new JObject { ["targetId"] = targetId });
	}

	public static void Accept(string fromId)
	{
		Send(8, new JObject { ["fromId"] = fromId ?? string.Empty });
	}

	public static void Decline(string fromId)
	{
		Send(9, new JObject { ["fromId"] = fromId ?? string.Empty });
	}

	public static void Forfeit()
	{
		Send(10, null);
	}

	private static void Send(byte kind, JObject json)
	{
		if (CommunityLink.Active && GameManager.IsReady)
		{
			Connections.Frontend.Send(new RankedUp
			{
				Kind = kind,
				Json = (json ?? new JObject()).ToString(Formatting.None)
			}, noReply: true);
		}
	}

	private static void ShowInvite(JObject invite)
	{
		string fromId = CommunityRanked.Str(invite["fromId"]);
		string text = CommunityRanked.Str(invite["fromName"]);
		int num = Mathf.Max(1, Mathf.RoundToInt((float)(CommunityRanked.Num(invite["expiresAt"]) - CommunityRanked.Num(invite["now"]))));
		try
		{
			UIManager.MessageBox.Show(CommunityRanked.T(text + " ท\u0e49าค\u0e38ณดวล!", text + " challenges you to a duel!"), CommunityRanked.T("3 ยก ชนะ 2 · ใช\u0e49ของต\u0e31วเอง · เล\u0e37อดหมดแค\u0e48แพ\u0e49ยก ไม\u0e48ตายจร\u0e34ง · ห\u0e49ามออกนอกวง (ตอบภายใน " + num + " ว\u0e34)", "Best of 3 · your own gear · out of health only loses the round · stay inside the ring (answer within " + num + "s)"), delegate(bool ok)
			{
				if (ok)
				{
					Accept(fromId);
				}
				else
				{
					Decline(fromId);
				}
			}, CommunityRanked.T("ร\u0e31บคำท\u0e49า", "Accept"), CommunityRanked.T("ปฏ\u0e34เสธ", "Decline"));
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			UIManager.SystemMsg(CommunityRanked.T(text + " ท\u0e49าค\u0e38ณดวล! พ\u0e34มพ\u0e4c /duel accept", text + " challenges you to a duel! Type /duel accept"), 6f);
		}
	}

	private static void HookInteraction()
	{
		if (GameManager.IsReady && GameSystem<InteractionSystem>.HasInstance())
		{
			InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
			if (!(interactionSystem == _hookedOn))
			{
				interactionSystem.AddInteractionHandler(Interaction.CommunityDuel, OnDuelAction);
				interactionSystem.PostTouched += OnPostTouched;
				_hookedOn = interactionSystem;
			}
		}
	}

	private static void OnPostTouched(InteractionMenuList list, InteractionObject target)
	{
		try
		{
			if (Enabled && target != null && list != null)
			{
				PlayerBehavior targetComponent = target.GetTargetComponent<PlayerBehavior>();
				if (!(targetComponent == null) && !string.IsNullOrEmpty(targetComponent.EntityId) && !(targetComponent.EntityId == GameManager.PlayerId) && !CommunityRanked.InMatch)
				{
					InteractionMenuData data = new InteractionMenuData(Interaction.CommunityDuel);
					data.Name = CommunityRanked.T("ท\u0e49าดวล", "Duel");
					data.Disabled = InDuel || IsDueling(targetComponent.EntityId);
					list.Add(data);
				}
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static void OnDuelAction(InteractionObject target)
	{
		PlayerBehavior playerBehavior = target?.GetTargetComponent<PlayerBehavior>();
		if (playerBehavior != null && !string.IsNullOrEmpty(playerBehavior.EntityId))
		{
			Challenge(playerBehavior.EntityId);
		}
	}

	private void Update()
	{
		if (!GameManager.IsReady)
		{
			return;
		}
		HookInteraction();
		string text = ((Match != null) ? ("duel:" + CommunityRanked.Str(Match["matchId"])) : null);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		JObject jObject = (InDuel ? (Match["ring"] as JObject) : null);
		if (jObject != null)
		{
			hashSet.Add(text);
			CommunityRingView.Show(text, new Vector2((float)CommunityRanked.Num(jObject["cx"]), (float)CommunityRanked.Num(jObject["cz"])), (float)CommunityRanked.Num(jObject["r"]));
		}
		for (int i = 0; i < _rings.Count; i++)
		{
			string text2 = "duel:" + _rings[i].Id;
			if (!(text2 == text))
			{
				hashSet.Add(text2);
				CommunityRingView.Show(text2, _rings[i].Center, _rings[i].Radius);
			}
		}
		foreach (string item in new List<string>(_shown))
		{
			if (!hashSet.Contains(item))
			{
				CommunityRingView.Hide(item);
				_shown.Remove(item);
			}
		}
		_shown.UnionWith(hashSet);
		if (jObject != null && Fighting && Time.realtimeSinceStartup >= _nextOutside && Outside(jObject))
		{
			_nextOutside = Time.realtimeSinceStartup + 0.5f;
			double num = CommunityRanked.Num(jObject["dmg"], 0.08) * 100.0;
			UIManager.SystemMsg("CommunityDuelOut", CommunityRanked.T("ออกนอกสนาม! กล\u0e31บเข\u0e49าวง (เส\u0e35ยเล\u0e37อด " + num.ToString("0") + "%/ว\u0e34)", "Out of the ring! Get back inside (" + num.ToString("0") + "% HP/s)"), 1.2f);
		}
	}

	private static bool Outside(JObject ring)
	{
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		Vector2 vector = Util.ClientPositionToTilePosition(localPlayer.CurrentPosition);
		float num = vector.x - (float)CommunityRanked.Num(ring["cx"]);
		float num2 = vector.y - (float)CommunityRanked.Num(ring["cz"]);
		float num3 = (float)CommunityRanked.Num(ring["r"]);
		return num * num + num2 * num2 > num3 * num3;
	}

	private static void HideRings()
	{
		foreach (string item in _shown)
		{
			CommunityRingView.Hide(item);
		}
		_shown.Clear();
	}

	public static string DebugCommand(string[] args)
	{
		switch ((args.Length > 1) ? args[1].ToLowerInvariant() : "state")
		{
		case "challenge":
			Challenge((args.Length > 2) ? args[2] : string.Empty);
			return "challenge sent";
		case "accept":
			Accept((args.Length > 2) ? args[2] : CommunityRanked.Str(LastInvite?["fromId"]));
			return "accept sent";
		case "decline":
			Decline((args.Length > 2) ? args[2] : CommunityRanked.Str(LastInvite?["fromId"]));
			return "decline sent";
		case "leave":
			Forfeit();
			return "forfeit sent";
		case "fake":
			if (args.Length < 4)
			{
				return "duel fake <kind 1|2|5|6> <json — ' in place of double quotes>";
			}
			return "fake " + OnDown((byte)Mathf.Clamp(Convert.ToInt32(args[2]), 0, 255), JObject.Parse(string.Join(" ", args, 3, args.Length - 3).Replace('\'', '"'))) + " · " + Describe();
		default:
			return Describe();
		}
	}

	public static string Describe()
	{
		return "enabled " + Enabled + " inDuel " + InDuel + " phase " + Phase + " fighting " + Fighting + " opponent " + OpponentId + " rings " + _rings.Count + " shown " + _shown.Count + " last " + LastReceived + " | " + CommunityRingView.Describe();
	}
}

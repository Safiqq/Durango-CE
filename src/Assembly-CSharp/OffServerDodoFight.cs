using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.Network;
using Durango.Render.Camera;
using Durango.UI;
using Durango.Utils;
using InteractionData;
using Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class OffServerDodoFight : GameSystem<OffServerDodoFight>
{
	public sealed class Skill
	{
		public string Id = string.Empty;

		public string Line = string.Empty;

		public string Tier = string.Empty;

		public float Rate;

		public float Cooldown;
	}

	public sealed class Card
	{
		public string Owner = string.Empty;

		public string OwnerName = string.Empty;

		public int Species;

		public string Name = string.Empty;

		public int Str;

		public int Agi;

		public int Dex;

		public int Luk;

		public int AtkPref = 50;

		public int Power;

		public int Wins;

		public int Losses;

		public string Tint = string.Empty;

		public readonly List<Skill> Skills = new List<Skill>();

		public readonly List<Skill> Basic = new List<Skill>();
	}

	public sealed class PlayerEntry
	{
		public string Id = string.Empty;

		public string Name = string.Empty;

		public int Power;

		public bool Busy;
	}

	public sealed class Lobby
	{
		public Card Me;

		public bool HasDodo;

		public bool InQueue;

		public int Queue;

		public readonly List<PlayerEntry> Players = new List<PlayerEntry>();

		public string ArenaState = "idle";

		public string ArenaMatch = string.Empty;

		public readonly string[] ArenaSides = new string[2]
		{
			string.Empty,
			string.Empty
		};

		public bool PracticeOk;
	}

	public sealed class Side
	{
		public string Name = string.Empty;

		public string OwnerName = string.Empty;

		public Card Card;

		public float Hp;

		public float HpMax;

		public int CheersLeft;

		public readonly bool[] Armed = new bool[2];

		public string Entity = string.Empty;

		public string Aura = string.Empty;

		public readonly float[] ReadyAt = new float[2];

		public float ReadyIn(int slot)
		{
			if (slot >= 0 && slot <= 1)
			{
				return Mathf.Max(0f, ReadyAt[slot] - Time.realtimeSinceStartup);
			}
			return 0f;
		}
	}

	public sealed class Betting
	{
		public bool Open;

		public float ClosesIn;

		public readonly float[] Odds = new float[2];

		public bool HasMine;

		public int MineSide;

		public long MineAmount;

		public bool Allowed;

		public float ReceivedAt;

		public float ClosesInNow => Mathf.Max(0f, ClosesIn - (Time.realtimeSinceStartup - ReceivedAt));
	}

	public sealed class EventLine
	{
		public int Side = -1;

		public string Kind = string.Empty;

		public string Text = string.Empty;
	}

	public sealed class Match
	{
		public string Id = string.Empty;

		public string Mode = string.Empty;

		public string Phase = string.Empty;

		public int Round;

		public int MaxRounds;

		public readonly int[] RoundWins = new int[2];

		public float TimeLeft;

		public string You = "spectator";

		public readonly Side[] Sides = new Side[2]
		{
			new Side(),
			new Side()
		};

		public readonly Betting Bet = new Betting();

		public float ReceivedAt;

		public float SeenAt;

		public int MySide
		{
			get
			{
				if (You == "owner0")
				{
					return 0;
				}
				if (You == "owner1")
				{
					return 1;
				}
				return -1;
			}
		}

		public bool IsOwner => MySide >= 0;

		public float TimeLeftNow
		{
			get
			{
				if (Phase == "ended")
				{
					return Mathf.Max(0f, TimeLeft);
				}
				return Mathf.Max(0f, TimeLeft - (Time.realtimeSinceStartup - ReceivedAt));
			}
		}
	}

	public sealed class MenuRow
	{
		public string Id = string.Empty;

		public string Name = string.Empty;

		public string Icon = string.Empty;

		public bool Disabled;

		public JObject Raw;
	}

	public const byte UpHello = 1;

	public const byte UpLobby = 2;

	public const byte UpDetail = 3;

	public const byte UpReroll = 4;

	public const byte UpChallenge = 5;

	public const byte UpChallengeReply = 6;

	public const byte UpQueue = 7;

	public const byte UpPractice = 8;

	public const byte UpCheer = 9;

	public const byte UpBet = 10;

	public const byte UpClose = 11;

	public const byte DownConfig = 1;

	public const byte DownLobby = 2;

	public const byte DownDetail = 3;

	public const byte DownMatch = 4;

	public const byte DownEvent = 5;

	public const byte DownMatchClosed = 6;

	public const byte DownChallenged = 7;

	public const byte DownToast = 8;

	public const byte DownMenu = 9;

	public const int ProtocolVersion = 2;

	public const int EventKeep = 4;

	private const float StaleMatchSec = 90f;

	public static string LastSent = string.Empty;

	public static string LastDown = string.Empty;

	public static string LastToast = string.Empty;

	public static string LastChallenge = string.Empty;

	private static readonly Dictionary<string, Match> _matches = new Dictionary<string, Match>(StringComparer.Ordinal);

	private static readonly Dictionary<string, List<EventLine>> _events = new Dictionary<string, List<EventLine>>(StringComparer.Ordinal);

	private static readonly List<EventLine> _noEvents = new List<EventLine>();

	private static string _sticky;

	private bool _helloPending;

	private bool _wasReady;

	private float _staleCheckAt;

	private static bool _cheerRegistered;

	private static InteractionSystem _cheerRegisteredOn;

	private static string _cheerSig = string.Empty;

	private float _cheerCheckAt;

	private const string CheerDescKey = "#context_action_desc_OffServerDodoCheer";

	private static FieldInfo _cheerIconField;

	public static string MenuRole = string.Empty;

	public static readonly List<MenuRow> MenuRows = new List<MenuRow>();

	public static JArray MenuPlayers;

	private const string NpcPrefix = "npc_dodo_";

	public static string LastMenuPick = string.Empty;

	private static string _camEntity = string.Empty;

	private static string _camKey = string.Empty;

	private static string _camReleasedKey = string.Empty;

	private static Vector3 _camStartPos;

	private static float _camCheckAt;

	private static readonly string[] CheerClips = new string[5] { "Emotion_Cheerup_A", "Emotion_Cheerup_B", "Emotion_Cheerup_C", "Emotion_Cheerup_D", "Emotion_Clap" };

	private const string JoyClip = "Emotion_Joy";

	private const float CheerNear = 2400f;

	private static float _cheerMotionAt;

	private static float _joyAt = -100f;

	public static string LastCheerClip = string.Empty;

	public static bool ConfigReceived { get; private set; }

	public static bool Enabled { get; private set; }

	public static long RerollPrice { get; private set; }

	public static long BetMin { get; private set; }

	public static double WinTax { get; private set; }

	public static int CheerPct { get; private set; }

	public static int CheersPerRound { get; private set; }

	public static int StatMax { get; private set; }

	public static float PickMaxTiles { get; private set; }

	public static int Version { get; private set; }

	public static Lobby CurrentLobby { get; private set; }

	public static Card DetailCard { get; private set; }

	public static int HelloSent { get; private set; }

	public static Match CurrentMatch
	{
		get
		{
			Match match = null;
			Match match2 = null;
			foreach (Match value2 in _matches.Values)
			{
				if (value2.IsOwner && (match == null || value2.SeenAt > match.SeenAt))
				{
					match = value2;
				}
				if (match2 == null || (value2.Mode == "pvp" && match2.Mode != "pvp") || (value2.Mode == "pvp" == (match2.Mode == "pvp") && value2.SeenAt > match2.SeenAt))
				{
					match2 = value2;
				}
			}
			if (match != null)
			{
				return match;
			}
			if (_sticky != null && _matches.TryGetValue(_sticky, out var value))
			{
				return value;
			}
			_sticky = match2?.Id;
			return match2;
		}
	}

	public static IEnumerable<Match> Matches => _matches.Values;

	public static int MatchCount => _matches.Count;

	public static bool CheerInHoneycomb
	{
		get
		{
			Match currentMatch = CurrentMatch;
			if (!_cheerRegistered || currentMatch == null || !currentMatch.IsOwner || currentMatch.Phase == "ended")
			{
				return false;
			}
			if (GameSystem<CombatSystem>.HasInstance() && GameSystem<CombatSystem>.Instance().CombatMode)
			{
				return false;
			}
			Side side = currentMatch.Sides[currentMatch.MySide];
			if (side.Card != null)
			{
				return side.Card.Skills.Count > 0;
			}
			return false;
		}
	}

	public static string CameraFollowing => _camEntity;

	public static List<EventLine> EventsOf(string matchId)
	{
		if (!string.IsNullOrEmpty(matchId) && _events.TryGetValue(matchId, out var value))
		{
			return value;
		}
		return _noEvents;
	}

	private static void Bump()
	{
		Version++;
	}

	private void Awake()
	{
		ResetState();
		Connections.Frontend.On<DodoDown>(OnDodoDown);
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
		ResetState();
	}

	private void OnWelcome(Welcome welcome)
	{
		ResetState();
		_helloPending = true;
	}

	private static void ResetState()
	{
		ConfigReceived = false;
		Enabled = false;
		RerollPrice = 0L;
		BetMin = 0L;
		WinTax = 0.0;
		CheerPct = 20;
		CheersPerRound = 3;
		StatMax = 50;
		PickMaxTiles = 12f;
		CurrentLobby = null;
		DetailCard = null;
		_matches.Clear();
		_events.Clear();
		_sticky = null;
		Bump();
		try
		{
			if (_camEntity.Length > 0 && Singleton<CameraController>.HasInstance())
			{
				Singleton<CameraController>.Instance().Target((UnityEngine.GameObject)null, 0f);
			}
			_camEntity = string.Empty;
			OffServerDodoFightFx.ClearAll();
			OffServerDodoFightUI.CloseAll();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private void Update()
	{
		try
		{
			UpdateCheerActions();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		try
		{
			UpdateCameraFollow();
		}
		catch (Exception exception2)
		{
			UnityEngine.Debug.LogException(exception2);
		}
		try
		{
			UpdateCheerMotion();
		}
		catch (Exception exception3)
		{
			UnityEngine.Debug.LogException(exception3);
		}
		bool isReady = GameManager.IsReady;
		if (isReady != _wasReady)
		{
			_wasReady = isReady;
			if (!isReady)
			{
				_matches.Clear();
				_events.Clear();
				CurrentLobby = null;
				DetailCard = null;
				Bump();
				OffServerDodoFightFx.ClearAll();
				OffServerDodoFightUI.CloseAll();
			}
		}
		if (_helloPending && isReady)
		{
			_helloPending = false;
			SendHello();
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_matches.Count <= 0 || !(realtimeSinceStartup >= _staleCheckAt))
		{
			return;
		}
		_staleCheckAt = realtimeSinceStartup + 5f;
		List<string> list = null;
		foreach (KeyValuePair<string, Match> match in _matches)
		{
			if (realtimeSinceStartup - match.Value.SeenAt > 90f)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(match.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			_matches.Remove(item);
			_events.Remove(item);
			OffServerDodoFightFx.ClearMatch(item);
		}
		Bump();
	}

	private static void CheerActions(List<InteractionMenuData> actions)
	{
		BetActions(actions);
		if (Enabled && CheerInHoneycomb)
		{
			Match currentMatch = CurrentMatch;
			Side side = currentMatch.Sides[currentMatch.MySide];
			for (int i = 0; i < side.Card.Skills.Count && i < 2; i++)
			{
				Skill skill = side.Card.Skills[i];
				InteractionMenuData item = new InteractionMenuData(Interaction.OffServerDodoCheer);
				item.Id = "dodo_cheer" + i;
				item.Name = OffServerDodoFightUI.SkillName(skill.Id);
				item.Icon = new ItemIcon(side.Armed[i] ? "action_speed_up" : ((skill.Line == "defend") ? "action_guard" : "action_attack"));
				item.SetColor(PresetColor.UIPet);
				actions.Add(item);
			}
		}
	}

	private static void OnCheerAction(InteractionObject obj)
	{
		string text = InteractionSystem.CurrentMenu.Id ?? string.Empty;
		if (text.StartsWith("dodo_cheer", StringComparison.Ordinal))
		{
			Cheer((text == "dodo_cheer1") ? 1 : 0);
		}
	}

	private void UpdateCheerActions()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup < _cheerCheckAt)
		{
			return;
		}
		_cheerCheckAt = realtimeSinceStartup + 0.25f;
		if (!GameManager.IsReady || !Singleton<UIManager>.HasInstance() || !GameSystem<InteractionSystem>.HasInstance())
		{
			return;
		}
		InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
		if (!_cheerRegistered || interactionSystem != _cheerRegisteredOn)
		{
			interactionSystem.RegisterContextActionFinder(CheerActions);
			interactionSystem.AddInteractionHandler(Interaction.OffServerDodoCheer, OnCheerAction);
			interactionSystem.AddInteractionHandler(Interaction.OffServerDodoAction, OnMenuAction);
			interactionSystem.PostTouched += OnPostTouched;
			_cheerRegisteredOn = interactionSystem;
			_cheerRegistered = true;
			_cheerSig = string.Empty;
		}
		string text = BetSig();
		Match currentMatch = CurrentMatch;
		bool cheerInHoneycomb = CheerInHoneycomb;
		if (cheerInHoneycomb)
		{
			Side side = currentMatch.Sides[currentMatch.MySide];
			text = text + currentMatch.Id + side.Armed[0] + side.Armed[1] + side.Card.Skills.Count;
		}
		if (text == _cheerSig && !cheerInHoneycomb)
		{
			return;
		}
		ContextActionGroupBase contextActionGroupBase = UIManager.FindScript<ContextActionGroupBase>();
		if (text != _cheerSig)
		{
			_cheerSig = text;
			EnsureCheerDescription();
			if (contextActionGroupBase != null)
			{
				contextActionGroupBase.RefreshActionList();
			}
		}
		if (!cheerInHoneycomb || contextActionGroupBase == null)
		{
			return;
		}
		ContextActionButtonsBase componentInChildren = contextActionGroupBase.GetComponentInChildren<ContextActionButtonsBase>(includeInactive: true);
		if (componentInChildren == null)
		{
			return;
		}
		Side side2 = currentMatch.Sides[currentMatch.MySide];
		TintCheerButtons(componentInChildren, side2);
		double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
		for (int i = 0; i < side2.Card.Skills.Count && i < 2; i++)
		{
			float num = side2.ReadyIn(i);
			float num2 = Mathf.Max(num, side2.Card.Skills[i].Cooldown);
			if (num > 0.05f)
			{
				componentInChildren.SetActionCooltime(Interaction.OffServerDodoCheer, "dodo_cheer" + i, predictedServerTime - (double)(num2 - num), predictedServerTime + (double)num);
			}
			else
			{
				componentInChildren.ClearActionCooltime(Interaction.OffServerDodoCheer, "dodo_cheer" + i);
			}
		}
	}

	private static void TintCheerButtons(Component root, Side side)
	{
		if (_cheerIconField == null)
		{
			_cheerIconField = typeof(ContextActionButtonBase).GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic);
			if (_cheerIconField == null)
			{
				return;
			}
		}
		ContextActionButtonBase[] componentsInChildren = root.GetComponentsInChildren<ContextActionButtonBase>(includeInactive: false);
		foreach (ContextActionButtonBase contextActionButtonBase in componentsInChildren)
		{
			if (contextActionButtonBase.Menu.Action == Interaction.OffServerDodoCheer)
			{
				UISprite uISprite = _cheerIconField.GetValue(contextActionButtonBase) as UISprite;
				int slot = ((contextActionButtonBase.Menu.Id == "dodo_cheer1") ? 1 : 0);
				if (uISprite != null && side.ReadyIn(slot) <= 0.05f && uISprite.color != PresetColor.UIPet)
				{
					uISprite.color = PresetColor.UIPet;
				}
			}
		}
	}

	private static void EnsureCheerDescription()
	{
		try
		{
			if (!LocalizeSystem.Has("#context_action_desc_OffServerDodoCheer"))
			{
				FieldInfo field = typeof(LocalizeSystem).GetField("_keyValueDict", BindingFlags.Static | BindingFlags.NonPublic);
				Dictionary<string, string> dictionary = ((field != null) ? (field.GetValue(null) as Dictionary<string, string>) : null);
				if (dictionary != null)
				{
					dictionary["#context_action_desc_OffServerDodoCheer"] = OffServerDodoFightUI.T("cheer_desc", CheerPct);
				}
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static bool IsDodoNpc(string entityId)
	{
		if (!string.IsNullOrEmpty(entityId))
		{
			return entityId.StartsWith("npc_dodo_", StringComparison.Ordinal);
		}
		return false;
	}

	private static string ApplyMenu(JObject o)
	{
		MenuRole = Str(o, "role");
		MenuRows.Clear();
		if (o["rows"] is JArray jArray)
		{
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item is JObject jObject && Str(jObject, "id").Length > 0)
				{
					MenuRows.Add(new MenuRow
					{
						Id = Str(jObject, "id"),
						Name = Str(jObject, "name"),
						Icon = Str(jObject, "icon"),
						Disabled = Bool(jObject, "disabled", fallback: false),
						Raw = jObject
					});
				}
			}
		}
		MenuPlayers = o["players"] as JArray;
		Bump();
		try
		{
			if (GameSystem<InteractionSystem>.HasInstance())
			{
				InteractionSystem interactionSystem = GameSystem<InteractionSystem>.Instance();
				if (IsDodoNpc(interactionSystem.LastTouched.EntityId))
				{
					InteractionMenuList menuList = interactionSystem.MenuList;
					for (int num = menuList.Count - 1; num >= 0; num--)
					{
						if (menuList[num].Action == Interaction.OffServerDodoAction)
						{
							menuList.RemoveAt(num);
						}
					}
					AddMenuRows(menuList);
					menuList.Apply();
				}
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		return "menu " + MenuRole + " rows " + MenuRows.Count;
	}

	private static void AddMenuRows(InteractionMenuList list)
	{
		foreach (MenuRow menuRow in MenuRows)
		{
			InteractionMenuData data = new InteractionMenuData(Interaction.OffServerDodoAction);
			data.Id = menuRow.Id;
			data.Name = menuRow.Name;
			if (menuRow.Icon.Length > 0)
			{
				data.Icon = new ItemIcon(menuRow.Icon);
			}
			data.Disabled = menuRow.Disabled;
			list.Add(data);
		}
	}

	private static void OnPostTouched(InteractionMenuList list, InteractionObject target)
	{
		try
		{
			if (Enabled && target != null && IsDodoNpc(target.EntityId) && MenuRows.Count > 0 && target.EntityId.IndexOf(MenuRole, StringComparison.Ordinal) >= 0)
			{
				AddMenuRows(list);
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static MenuRow MenuRowOf(string id)
	{
		foreach (MenuRow menuRow in MenuRows)
		{
			if (menuRow.Id == id)
			{
				return menuRow;
			}
		}
		return null;
	}

	private static void OnMenuAction(InteractionObject obj)
	{
		string text = InteractionSystem.CurrentMenu.Id ?? string.Empty;
		try
		{
			if (text.StartsWith("hbet", StringComparison.Ordinal))
			{
				Match currentMatch = CurrentMatch;
				int num = ((text == "hbet1") ? 1 : 0);
				if (currentMatch != null)
				{
					OffServerDodoFightUI.AskBetFor(currentMatch.Id, num, OffServerDodoFightUI.SideLabel(currentMatch, num), currentMatch.Bet.Odds[num]);
				}
				return;
			}
			MenuRow menuRow = MenuRowOf(text);
			if (menuRow == null || menuRow.Disabled)
			{
				return;
			}
			LastMenuPick = text;
			switch (text)
			{
			case "reroll":
			case "get":
				OffServerDodoFightUI.AskReroll();
				return;
			case "detail":
				RequestDetail(null);
				return;
			case "queue":
				SetQueue(Bool(menuRow.Raw, "join", fallback: true));
				return;
			case "practice":
				OffServerDodoFightUI.StartPick();
				return;
			case "challenge":
				OffServerDodoFightUI.PickPlayer(MenuPlayers);
				return;
			case "bet0":
			case "bet1":
				OffServerDodoFightUI.AskBetFor(Str(menuRow.Raw, "match"), (int)Num(menuRow.Raw, "side", 0.0), Str(menuRow.Raw, "side_name"), (float)Num(menuRow.Raw, "odds", 1.0));
				return;
			}
			if (text.StartsWith("duel_", StringComparison.Ordinal))
			{
				Challenge(Str(menuRow.Raw, "target"));
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static string CameraFollowTarget(out string key)
	{
		key = string.Empty;
		Match match = null;
		int num = -1;
		foreach (Match value in _matches.Values)
		{
			if (!(value.Phase != "running"))
			{
				int num2 = (value.IsOwner ? value.MySide : (value.Bet.HasMine ? value.Bet.MineSide : (-1)));
				if (num2 >= 0 && num2 <= 1 && (match == null || (value.IsOwner && !match.IsOwner)))
				{
					match = value;
					num = num2;
				}
			}
		}
		if (match == null)
		{
			return string.Empty;
		}
		key = match.Id + "|" + match.Round;
		return match.Sides[num].Entity ?? string.Empty;
	}

	private static void UpdateCameraFollow()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (realtimeSinceStartup < _camCheckAt)
		{
			return;
		}
		_camCheckAt = realtimeSinceStartup + 0.2f;
		if (!GameManager.IsReady || PlayerBehavior.LocalPlayer == null || !Singleton<CameraController>.HasInstance())
		{
			_camEntity = string.Empty;
			return;
		}
		string key = string.Empty;
		string text = (Enabled ? CameraFollowTarget(out key) : string.Empty);
		if (text.Length > 0 && key == _camReleasedKey)
		{
			text = string.Empty;
		}
		Vector3 position = PlayerBehavior.LocalPlayer.transform.position;
		if (_camEntity.Length > 0 && text == _camEntity)
		{
			if (!((position - _camStartPos).sqrMagnitude > 40000f))
			{
				return;
			}
			_camReleasedKey = _camKey;
			text = string.Empty;
		}
		if (text == _camEntity)
		{
			return;
		}
		CameraController cameraController = Singleton<CameraController>.Instance();
		if (text.Length > 0)
		{
			AnimalBehavior animalBehavior = (Singleton<AnimalManager>.HasInstance() ? Singleton<AnimalManager>.Instance().GetAnimal(text) : null);
			if (!(animalBehavior == null))
			{
				cameraController.Target(animalBehavior.gameObject, 0.6f);
				_camEntity = text;
				_camKey = key;
				_camStartPos = position;
			}
		}
		else
		{
			if (_camEntity.Length > 0)
			{
				cameraController.Target((UnityEngine.GameObject)null, 0.6f);
			}
			_camEntity = string.Empty;
			_camKey = string.Empty;
		}
	}

	private static int MySideIn(Match match)
	{
		if (match.IsOwner)
		{
			return match.MySide;
		}
		if (!match.Bet.HasMine || match.Bet.MineAmount <= 0 || match.Bet.MineSide < 0 || match.Bet.MineSide > 1)
		{
			return -1;
		}
		return match.Bet.MineSide;
	}

	private static bool CanCheerNear(string entity)
	{
		if (!GameManager.IsReady || PlayerBehavior.LocalPlayer == null || string.IsNullOrEmpty(entity))
		{
			return false;
		}
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if ((bool)localPlayer.IsMoving || localPlayer.IsRiding)
		{
			return false;
		}
		if (GameSystem<CombatSystem>.HasInstance() && GameSystem<CombatSystem>.Instance().CombatMode)
		{
			return false;
		}
		AnimalBehavior animalBehavior = (Singleton<AnimalManager>.HasInstance() ? Singleton<AnimalManager>.Instance().GetAnimal(entity) : null);
		if (animalBehavior != null)
		{
			return (animalBehavior.transform.position - localPlayer.transform.position).sqrMagnitude <= 5760000f;
		}
		return false;
	}

	private static void PlayCheerClip(string clip)
	{
		PlayerController.MotionUpdater.Motion(clip);
		LastCheerClip = clip;
	}

	private static void UpdateCheerMotion()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (!Enabled || realtimeSinceStartup < _cheerMotionAt)
		{
			return;
		}
		Match match = null;
		int num = -1;
		foreach (Match value in _matches.Values)
		{
			int num2 = MySideIn(value);
			if (value.Phase == "running" && num2 >= 0 && (match == null || (value.IsOwner && !match.IsOwner)))
			{
				match = value;
				num = num2;
			}
		}
		if (match == null)
		{
			_cheerMotionAt = realtimeSinceStartup + UnityEngine.Random.Range(0.5f, 2f);
			return;
		}
		if (realtimeSinceStartup - _joyAt < 3f || !CanCheerNear(match.Sides[num].Entity))
		{
			_cheerMotionAt = realtimeSinceStartup + 0.5f;
			return;
		}
		string text = CheerClips[UnityEngine.Random.Range(0, CheerClips.Length)];
		if (text == LastCheerClip)
		{
			text = CheerClips[(Array.IndexOf(CheerClips, text) + 1) % CheerClips.Length];
		}
		PlayCheerClip(text);
		_cheerMotionAt = realtimeSinceStartup + UnityEngine.Random.Range(3f, 6f);
	}

	private static void CheerMotionEvent(Match match, int side, string kind)
	{
		if (Enabled && match != null && (side == 0 || side == 1) && (!(kind != "round") || !(kind != "end")))
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (MySideIn(match) == side && !(realtimeSinceStartup - _joyAt < 3f) && CanCheerNear(match.Sides[side].Entity))
			{
				PlayCheerClip("Emotion_Joy");
				_joyAt = realtimeSinceStartup;
				_cheerMotionAt = realtimeSinceStartup + 3f;
			}
		}
	}

	private static bool BetInHoneycomb(out Match match)
	{
		match = CurrentMatch;
		if (!Enabled || !_cheerRegistered || match == null || match.Phase == "ended" || !match.Bet.Open || !match.Bet.Allowed || match.Bet.HasMine)
		{
			return false;
		}
		if (GameSystem<CombatSystem>.HasInstance())
		{
			return !GameSystem<CombatSystem>.Instance().CombatMode;
		}
		return true;
	}

	private static string BetSig()
	{
		if (!BetInHoneycomb(out var match))
		{
			return string.Empty;
		}
		return "bet" + match.Id + match.Bet.Odds[0] + match.Bet.Odds[1] + match.MySide;
	}

	private static void BetActions(List<InteractionMenuData> actions)
	{
		if (!BetInHoneycomb(out var match))
		{
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!match.IsOwner || match.MySide == i)
			{
				InteractionMenuData item = new InteractionMenuData(Interaction.OffServerDodoAction);
				item.Id = "hbet" + i;
				item.Name = OffServerDodoFightUI.T("bet_hex", OffServerDodoFightUI.SideLabel(match, i), match.Bet.Odds[i].ToString("0.##", CultureInfo.InvariantCulture));
				item.Icon = new ItemIcon("act_invest");
				item.SetColor((i == 0) ? new Color(1f, 0.45f, 0.4f) : new Color(0.45f, 0.65f, 1f));
				actions.Add(item);
			}
		}
	}

	public static bool Send(byte kind, object payload)
	{
		string text;
		try
		{
			if (payload == null)
			{
				text = "{}";
			}
			else if (!(payload is string))
			{
				text = ((!(payload is JToken)) ? JsonConvert.SerializeObject(payload) : ((JToken)payload).ToString(Formatting.None));
			}
			else
			{
				text = ((string)payload).Trim();
				if (text.Length == 0)
				{
					text = "{}";
				}
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[DodoFight] bad payload: " + ex.Message);
			return false;
		}
		LastSent = kind + " " + text;
		try
		{
			Connection frontend = Connections.Frontend;
			if (frontend == null || !frontend.Connected())
			{
				LastSent += " (offline)";
				return false;
			}
			frontend.Send(new DodoUp
			{
				Kind = kind,
				Json = text
			}, noReply: true);
			return true;
		}
		catch (Exception ex2)
		{
			UnityEngine.Debug.Log("[DodoFight] send failed: " + ex2.Message);
			return false;
		}
	}

	private static void SendHello()
	{
		if (OffServerLink.Active)
		{
			JObject jObject = new JObject();
			jObject["v"] = 2;
			if (Send(1, jObject))
			{
				HelloSent++;
			}
		}
	}

	private static JObject One(string key, JToken value)
	{
		return new JObject { [key] = value };
	}

	public static void RequestLobby()
	{
		Send(2, null);
	}

	public static void RequestDetail(string playerId)
	{
		Send(3, One("player", playerId ?? string.Empty));
	}

	public static void Reroll()
	{
		Send(4, null);
	}

	public static void Challenge(string target)
	{
		if (!string.IsNullOrEmpty(target))
		{
			Send(5, One("target", target));
		}
	}

	public static void ReplyChallenge(string from, bool accept)
	{
		JObject jObject = One("from", from ?? string.Empty);
		jObject["accept"] = accept;
		Send(6, jObject);
	}

	public static void SetQueue(bool join)
	{
		Send(7, One("join", join));
	}

	public static void Practice()
	{
		OffServerDodoFightUI.StartPick();
	}

	public static bool PracticeAt(float tx, float tz)
	{
		JObject jObject = new JObject();
		jObject["x"] = Math.Round(tx, 2);
		jObject["z"] = Math.Round(tz, 2);
		return Send(8, jObject);
	}

	public static void SendClose()
	{
		Send(11, null);
	}

	public static bool Cheer(int slot)
	{
		Match currentMatch = CurrentMatch;
		if (currentMatch == null || !currentMatch.IsOwner || slot < 0 || slot > 1 || currentMatch.Phase == "ended")
		{
			return false;
		}
		Side side = currentMatch.Sides[currentMatch.MySide];
		if (side.CheersLeft <= 0 || side.Armed[slot] || side.Card == null || side.Card.Skills.Count <= slot)
		{
			return false;
		}
		JObject jObject = One("match", currentMatch.Id);
		jObject["slot"] = slot;
		if (!Send(9, jObject))
		{
			return false;
		}
		side.Armed[slot] = true;
		side.CheersLeft--;
		Bump();
		return true;
	}

	public static bool SendBet(string matchId, int side, long amount)
	{
		if (string.IsNullOrEmpty(matchId) || side < 0 || side > 1 || amount <= 0)
		{
			return false;
		}
		JObject jObject = One("match", matchId);
		jObject["side"] = side;
		jObject["amount"] = amount;
		return Send(10, jObject);
	}

	private void OnDodoDown(DodoDown msg, PacketHeader header)
	{
		try
		{
			HandleDown(msg.Kind, msg.Json);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	public static string HandleDown(byte kind, string json)
	{
		JObject jObject = ParseObject(json);
		LastDown = kind + " " + ((json != null && json.Length > 200) ? (json.Substring(0, 200) + "…") : json);
		if (jObject == null)
		{
			return "bad json";
		}
		if (kind == 1)
		{
			ApplyConfig(jObject, json);
			return "config enabled=" + Enabled;
		}
		if (!Enabled)
		{
			return "ignored (no Config with enabled:true yet)";
		}
		switch (kind)
		{
		case 2:
			CurrentLobby = ParseLobby(jObject);
			Bump();
			OffServerDodoFightUI.ShowLobby();
			return "lobby";
		case 3:
		{
			Card card = ParseCard(jObject["card"]);
			if (card == null)
			{
				return "detail without card";
			}
			DetailCard = card;
			Bump();
			OffServerDodoFightUI.ShowDetail();
			return "detail " + card.Name;
		}
		case 4:
			return ApplyMatch(jObject);
		case 5:
		{
			string text5 = Str(jObject, "match");
			if (!_events.TryGetValue(text5, out var value))
			{
				value = new List<EventLine>();
				_events[text5] = value;
			}
			value.Add(new EventLine
			{
				Side = (int)Num(jObject, "side", -1.0),
				Kind = Str(jObject, "kind"),
				Text = Str(jObject, "text")
			});
			if (Str(jObject, "kind") == "skill" && _matches.TryGetValue(text5, out var value2))
			{
				int num = (int)Num(jObject, "side", -1.0);
				Side side = ((num == 0 || num == 1) ? value2.Sides[num] : null);
				if (side != null && side.Card != null)
				{
					string text6 = Str(jObject, "skill");
					for (int i = 0; i < side.Card.Skills.Count && i < 2; i++)
					{
						if (side.Card.Skills[i].Id == text6)
						{
							side.ReadyAt[i] = Time.realtimeSinceStartup + side.Card.Skills[i].Cooldown;
						}
					}
				}
			}
			if (Str(jObject, "kind") == "skill")
			{
				try
				{
					OffServerDodoFightFx.PlaySkill(Str(jObject, "skill"), Str(jObject, "line"), Str(jObject, "entity"), Str(jObject, "target"), jObject["fx"] as JArray);
				}
				catch (Exception exception)
				{
					UnityEngine.Debug.LogException(exception);
				}
			}
			while (value.Count > 4)
			{
				value.RemoveAt(0);
			}
			if (_matches.TryGetValue(text5, out var value3))
			{
				value3.SeenAt = Time.realtimeSinceStartup;
			}
			Bump();
			OffServerDodoFightUI.MatchChanged();
			if (value3 != null)
			{
				OffServerDodoFightUI.MatchEvent(value3, (int)Num(jObject, "side", -1.0), Str(jObject, "kind"), Str(jObject, "text"), Str(jObject, "skill"), Str(jObject, "tier"));
				try
				{
					CheerMotionEvent(value3, (int)Num(jObject, "side", -1.0), Str(jObject, "kind"));
				}
				catch (Exception exception2)
				{
					UnityEngine.Debug.LogException(exception2);
				}
			}
			return "event " + text5;
		}
		case 6:
		{
			string text4 = Str(jObject, "match");
			_matches.Remove(text4);
			_events.Remove(text4);
			OffServerDodoFightFx.ClearMatch(text4);
			Bump();
			OffServerDodoFightUI.MatchChanged();
			return "closed " + text4;
		}
		case 7:
		{
			string text2 = Str(jObject, "from");
			string text3 = Str(jObject, "name");
			LastChallenge = text2 + " " + text3;
			OffServerDodoFightUI.ShowChallenged(text2, text3, ParseCard(jObject["card"]), (float)Num(jObject, "expires_in", 20.0));
			return "challenged by " + text3;
		}
		case 9:
			return ApplyMenu(jObject);
		case 8:
		{
			string text = Str(jObject, "text");
			if (text.Length > 0)
			{
				LastToast = text;
				UIManager.SystemMsg(text);
			}
			return "toast";
		}
		default:
			return "unknown kind " + kind;
		}
	}

	private static void ApplyConfig(JObject o, string raw)
	{
		bool flag = Enabled;
		bool configReceived = ConfigReceived;
		Enabled = Bool(o, "enabled", fallback: false);
		RerollPrice = (long)Num(o, "reroll_price", RerollPrice);
		BetMin = Math.Max(0L, (long)Num(o, "bet_min", 0.0));
		WinTax = Math.Round(Mathf.Clamp01((float)Num(o, "win_tax", 0.0)), 4);
		CheerPct = (int)Num(o, "cheer_pct", CheerPct);
		CheersPerRound = (int)Num(o, "cheers_per_round", CheersPerRound);
		StatMax = Mathf.Max(1, (int)Num(o, "stat_max", StatMax));
		PickMaxTiles = Mathf.Max(1f, (float)Num(o, "practice_pick_max_tiles", PickMaxTiles));
		ConfigReceived = true;
		if (!configReceived || flag != Enabled)
		{
			UnityEngine.Debug.Log("[DodoFight] config: " + raw);
		}
		if (!Enabled)
		{
			CurrentLobby = null;
			DetailCard = null;
			_matches.Clear();
			_events.Clear();
			OffServerDodoFightFx.ClearAll();
			OffServerDodoFightUI.CloseAll();
		}
		Bump();
	}

	private static string ApplyMatch(JObject o)
	{
		string text = Str(o, "match");
		if (text.Length == 0)
		{
			return "match without id";
		}
		Match value;
		bool flag = !_matches.TryGetValue(text, out value);
		if (flag)
		{
			value = new Match();
			value.Id = text;
			_matches[text] = value;
		}
		value.Mode = Str(o, "mode");
		value.Phase = Str(o, "phase");
		value.Round = (int)Num(o, "round", 0.0);
		value.MaxRounds = (int)Num(o, "max_rounds", 0.0);
		ReadInts(o["round_wins"] as JArray, value.RoundWins);
		value.TimeLeft = (float)Num(o, "time_left", 0.0);
		string text2 = Str(o, "you");
		value.You = ((text2.Length > 0) ? text2 : "spectator");
		value.ReceivedAt = Time.realtimeSinceStartup;
		value.SeenAt = value.ReceivedAt;
		JArray jArray = o["sides"] as JArray;
		for (int i = 0; i < 2; i++)
		{
			JObject jObject = ((jArray != null && i < jArray.Count) ? (jArray[i] as JObject) : null);
			Side side = value.Sides[i];
			if (jObject != null)
			{
				side.Name = Str(jObject, "name");
				side.OwnerName = Str(jObject, "owner_name");
				Card card = ParseCard(jObject["card"]);
				if (card != null)
				{
					side.Card = card;
				}
				side.Hp = (float)Num(jObject, "hp", 0.0);
				side.HpMax = (float)Num(jObject, "hp_max", 0.0);
				side.CheersLeft = (int)Num(jObject, "cheers_left", 0.0);
				JArray jArray2 = jObject["armed"] as JArray;
				for (int j = 0; j < 2; j++)
				{
					side.Armed[j] = jArray2 != null && j < jArray2.Count && TokenBool(jArray2[j]);
				}
				JArray jArray3 = jObject["ready_in"] as JArray;
				for (int k = 0; k < 2; k++)
				{
					side.ReadyAt[k] = Time.realtimeSinceStartup + ((jArray3 != null && k < jArray3.Count) ? ((float)TokenNum(jArray3[k], 0.0)) : 0f);
				}
				string text3 = Str(jObject, "entity");
				if (text3.Length > 0)
				{
					side.Entity = text3;
					side.Aura = Str(jObject, "aura");
				}
			}
		}
		try
		{
			OffServerDodoFightFx.SetMatch(text, new string[2]
			{
				value.Sides[0].Entity,
				value.Sides[1].Entity
			}, new string[2]
			{
				value.Sides[0].Aura,
				value.Sides[1].Aura
			}, new string[2]
			{
				(value.Sides[0].Card != null) ? value.Sides[0].Card.Tint : string.Empty,
				(value.Sides[1].Card != null) ? value.Sides[1].Card.Tint : string.Empty
			});
			if (flag)
			{
				List<string> list = new List<string>();
				for (int l = 0; l < 2; l++)
				{
					if (value.Sides[l].Card == null)
					{
						continue;
					}
					foreach (Skill skill in value.Sides[l].Card.Skills)
					{
						list.Add(skill.Id);
					}
				}
				OffServerDodoFightFx.Preload(list);
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		Betting bet = value.Bet;
		if (!(o["betting"] is JObject jObject2))
		{
			bet.Open = false;
			bet.Allowed = false;
			bet.HasMine = false;
		}
		else
		{
			bet.Open = Bool(jObject2, "open", fallback: false);
			bet.ClosesIn = (float)Num(jObject2, "closes_in", 0.0);
			bet.ReceivedAt = Time.realtimeSinceStartup;
			bet.Allowed = Bool(jObject2, "allowed", fallback: false);
			JArray jArray4 = jObject2["odds"] as JArray;
			for (int m = 0; m < 2; m++)
			{
				bet.Odds[m] = ((jArray4 != null && m < jArray4.Count) ? ((float)TokenNum(jArray4[m], 0.0)) : 0f);
			}
			JObject jObject3 = jObject2["mine"] as JObject;
			bet.HasMine = jObject3 != null;
			bet.MineSide = ((jObject3 != null) ? ((int)Num(jObject3, "side", 0.0)) : 0);
			bet.MineAmount = ((jObject3 != null) ? ((long)Num(jObject3, "amount", 0.0)) : 0);
		}
		Bump();
		if (flag && value.IsOwner && value.Phase != "ended")
		{
			OffServerDodoFightUI.OwnMatchStarted();
		}
		OffServerDodoFightUI.MatchChanged();
		return "match " + text + " " + value.Phase;
	}

	public static JObject ParseObject(string json)
	{
		if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
		{
			return new JObject();
		}
		try
		{
			return JToken.Parse(json) as JObject;
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.Log("[DodoFight] bad json: " + ex.Message);
			return null;
		}
	}

	internal static string Str(JObject o, string key)
	{
		JToken jToken = o?[key];
		if (jToken == null || jToken.Type == JTokenType.Null || jToken.Type == JTokenType.Undefined)
		{
			return string.Empty;
		}
		if (jToken.Type == JTokenType.String)
		{
			return ((string)jToken) ?? string.Empty;
		}
		return jToken.ToString(Formatting.None);
	}

	internal static double Num(JObject o, string key, double fallback)
	{
		return TokenNum(o?[key], fallback);
	}

	private static double TokenNum(JToken t, double fallback)
	{
		if (t == null)
		{
			return fallback;
		}
		switch (t.Type)
		{
		case JTokenType.Integer:
		case JTokenType.Float:
			return Convert.ToDouble(((JValue)t).Value, CultureInfo.InvariantCulture);
		case JTokenType.Boolean:
			if (!Convert.ToBoolean(((JValue)t).Value, CultureInfo.InvariantCulture))
			{
				return 0.0;
			}
			return 1.0;
		case JTokenType.String:
		{
			if (double.TryParse((string)t, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
			return fallback;
		}
		default:
			return fallback;
		}
	}

	private static bool Bool(JObject o, string key, bool fallback)
	{
		JToken jToken = o?[key];
		if (jToken == null || jToken.Type == JTokenType.Null)
		{
			return fallback;
		}
		return TokenBool(jToken);
	}

	private static bool TokenBool(JToken t)
	{
		if (t == null)
		{
			return false;
		}
		switch (t.Type)
		{
		case JTokenType.Boolean:
			return Convert.ToBoolean(((JValue)t).Value, CultureInfo.InvariantCulture);
		case JTokenType.Integer:
		case JTokenType.Float:
			return Convert.ToDouble(((JValue)t).Value, CultureInfo.InvariantCulture) != 0.0;
		case JTokenType.String:
		{
			string text = (((string)t) ?? string.Empty).Trim().ToLowerInvariant();
			if (!(text == "true") && !(text == "1"))
			{
				return text == "yes";
			}
			return true;
		}
		default:
			return false;
		}
	}

	private static void ReadInts(JArray array, int[] into)
	{
		for (int i = 0; i < into.Length; i++)
		{
			into[i] = ((array != null && i < array.Count) ? ((int)TokenNum(array[i], 0.0)) : 0);
		}
	}

	private static Card ParseCard(JToken token)
	{
		if (!(token is JObject jObject))
		{
			return null;
		}
		Card card = new Card();
		card.Owner = Str(jObject, "owner");
		card.OwnerName = Str(jObject, "owner_name");
		card.Species = (int)Num(jObject, "species", 0.0);
		card.Name = Str(jObject, "name");
		card.Str = (int)Math.Round(Num(jObject, "str", 0.0));
		card.Agi = (int)Math.Round(Num(jObject, "agi", 0.0));
		card.Dex = (int)Math.Round(Num(jObject, "dex", 0.0));
		card.Luk = (int)Math.Round(Num(jObject, "luk", 0.0));
		card.AtkPref = Mathf.Clamp((int)Math.Round(Num(jObject, "atk_pref", 50.0)), 0, 100);
		card.Power = (int)Math.Round(Num(jObject, "power", 0.0));
		card.Wins = (int)Num(jObject, "wins", 0.0);
		card.Losses = (int)Num(jObject, "losses", 0.0);
		card.Tint = Str(jObject, "tint");
		ReadSkills(jObject["skills"] as JArray, card.Skills);
		ReadSkills(jObject["basic"] as JArray, card.Basic);
		return card;
	}

	private static void ReadSkills(JArray array, List<Skill> into)
	{
		if (array == null)
		{
			return;
		}
		foreach (JToken item in (IEnumerable<JToken>)array)
		{
			if (item is JObject o)
			{
				into.Add(new Skill
				{
					Id = Str(o, "id"),
					Line = Str(o, "line"),
					Tier = Str(o, "tier"),
					Rate = (float)Num(o, "rate", 0.0),
					Cooldown = (float)Num(o, "cooldown", 0.0)
				});
			}
		}
	}

	private static Lobby ParseLobby(JObject o)
	{
		Lobby lobby = new Lobby();
		lobby.Me = ParseCard(o["me"]);
		lobby.HasDodo = Bool(o, "has_dodo", lobby.Me != null);
		lobby.InQueue = Bool(o, "in_queue", fallback: false);
		lobby.Queue = (int)Num(o, "queue", 0.0);
		lobby.PracticeOk = Bool(o, "practice_ok", fallback: false);
		if (o["players"] is JArray jArray)
		{
			string playerId = GameManager.PlayerId;
			foreach (JToken item in (IEnumerable<JToken>)jArray)
			{
				if (item is JObject o2)
				{
					PlayerEntry playerEntry = new PlayerEntry();
					playerEntry.Id = Str(o2, "id");
					playerEntry.Name = Str(o2, "name");
					playerEntry.Power = (int)Math.Round(Num(o2, "power", 0.0));
					playerEntry.Busy = Bool(o2, "busy", fallback: false);
					if (playerEntry.Id.Length > 0 && playerEntry.Id != playerId)
					{
						lobby.Players.Add(playerEntry);
					}
				}
			}
		}
		if (o["arena"] is JObject jObject)
		{
			string text = Str(jObject, "state");
			lobby.ArenaState = ((text.Length > 0) ? text : "idle");
			lobby.ArenaMatch = Str(jObject, "match");
			JArray jArray2 = jObject["sides"] as JArray;
			for (int i = 0; i < 2; i++)
			{
				lobby.ArenaSides[i] = ((jArray2 != null && i < jArray2.Count && jArray2[i].Type == JTokenType.String) ? ((string)jArray2[i]) : string.Empty);
			}
		}
		return lobby;
	}

	public static Dictionary<string, object> DebugState()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary["configReceived"] = ConfigReceived;
		dictionary["enabled"] = Enabled;
		dictionary["rerollPrice"] = RerollPrice;
		dictionary["betMin"] = BetMin;
		dictionary["winTax"] = WinTax;
		dictionary["cheerPct"] = CheerPct;
		dictionary["cheersPerRound"] = CheersPerRound;
		dictionary["statMax"] = StatMax;
		dictionary["helloSent"] = HelloSent;
		dictionary["helloPending"] = GameSystem<OffServerDodoFight>.HasInstance() && GameSystem<OffServerDodoFight>.Instance()._helloPending;
		dictionary["lastSent"] = LastSent;
		dictionary["lastDown"] = LastDown;
		dictionary["lastToast"] = LastToast;
		dictionary["lastChallenge"] = LastChallenge;
		dictionary["version"] = Version;
		dictionary["cheerClip"] = LastCheerClip;
		Lobby currentLobby = CurrentLobby;
		if (currentLobby != null)
		{
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
			dictionary2["hasDodo"] = currentLobby.HasDodo;
			dictionary2["me"] = DebugCard(currentLobby.Me);
			dictionary2["inQueue"] = currentLobby.InQueue;
			dictionary2["queue"] = currentLobby.Queue;
			dictionary2["practiceOk"] = currentLobby.PracticeOk;
			dictionary2["arena"] = currentLobby.ArenaState + " " + currentLobby.ArenaMatch + " " + currentLobby.ArenaSides[0] + " vs " + currentLobby.ArenaSides[1];
			List<object> list = new List<object>();
			foreach (PlayerEntry player in currentLobby.Players)
			{
				list.Add(player.Id + " " + player.Name + " power " + player.Power + (player.Busy ? " busy" : string.Empty));
			}
			dictionary2["players"] = list;
			dictionary["lobby"] = dictionary2;
		}
		dictionary["detail"] = DebugCard(DetailCard);
		List<object> list2 = new List<object>();
		foreach (Match value in _matches.Values)
		{
			Dictionary<string, object> dictionary3 = new Dictionary<string, object>();
			dictionary3["id"] = value.Id;
			dictionary3["mode"] = value.Mode;
			dictionary3["phase"] = value.Phase;
			dictionary3["round"] = value.Round + "/" + value.MaxRounds;
			dictionary3["roundWins"] = value.RoundWins[0] + ":" + value.RoundWins[1];
			dictionary3["timeLeft"] = Math.Round(value.TimeLeftNow, 1);
			dictionary3["you"] = value.You;
			for (int i = 0; i < 2; i++)
			{
				Side side = value.Sides[i];
				dictionary3["side" + i] = side.Name + " (" + side.OwnerName + ") hp " + side.Hp + "/" + side.HpMax + " cheers " + side.CheersLeft + " armed " + side.Armed[0] + "," + side.Armed[1] + " entity " + side.Entity + " " + side.Aura;
			}
			dictionary3["betting"] = "open " + value.Bet.Open + " allowed " + value.Bet.Allowed + " closesIn " + Math.Round(value.Bet.ClosesInNow, 1) + " odds " + value.Bet.Odds[0].ToString(CultureInfo.InvariantCulture) + "/" + value.Bet.Odds[1].ToString(CultureInfo.InvariantCulture) + (value.Bet.HasMine ? (" mine side " + value.Bet.MineSide + " amount " + value.Bet.MineAmount) : " mine none");
			List<object> list3 = new List<object>();
			foreach (EventLine item in EventsOf(value.Id))
			{
				list3.Add(item.Side + " " + item.Kind + " " + item.Text);
			}
			dictionary3["events"] = list3;
			list2.Add(dictionary3);
		}
		dictionary["matches"] = list2;
		Match currentMatch = CurrentMatch;
		dictionary["currentMatch"] = ((currentMatch != null) ? currentMatch.Id : string.Empty);
		dictionary["ui"] = OffServerDodoFightUI.DebugState();
		dictionary["fx"] = OffServerDodoFightFx.DebugState();
		return dictionary;
	}

	private static object DebugCard(Card c)
	{
		if (c == null)
		{
			return null;
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary["owner"] = c.Owner + " " + c.OwnerName;
		dictionary["species"] = c.Species;
		dictionary["name"] = c.Name;
		dictionary["stats"] = "str " + c.Str + " agi " + c.Agi + " dex " + c.Dex + " luk " + c.Luk + " atk_pref " + c.AtkPref;
		dictionary["power"] = c.Power;
		dictionary["record"] = c.Wins + "-" + c.Losses;
		List<object> list = new List<object>();
		foreach (Skill skill in c.Skills)
		{
			list.Add(skill.Id + " " + skill.Line + " " + skill.Tier + " " + skill.Rate.ToString(CultureInfo.InvariantCulture));
		}
		dictionary["skills"] = list;
		List<object> list2 = new List<object>();
		foreach (Skill item in c.Basic)
		{
			list2.Add(item.Id + " " + item.Line);
		}
		dictionary["basic"] = list2;
		return dictionary;
	}

	public static string DebugCommand(string[] args)
	{
		if (args == null || args.Length == 0 || args[0] == "state")
		{
			return null;
		}
		switch (args[0])
		{
		case "send":
		case "fake":
		{
			if (args.Length < 2 || !byte.TryParse(args[1], out var result5))
			{
				return "usage: dodo " + args[0] + " <kind> <json>  (single quotes inside the json: {'match':'m1'})";
			}
			string text = ((args.Length > 2) ? string.Join(" ", args, 2, args.Length - 2) : "{}");
			if (text.StartsWith("@", StringComparison.Ordinal))
			{
				try
				{
					text = File.ReadAllText(text.Substring(1));
				}
				catch (Exception ex)
				{
					return "cannot read " + text.Substring(1) + ": " + ex.Message;
				}
			}
			JToken jToken;
			try
			{
				jToken = JToken.Parse(text);
			}
			catch (Exception ex2)
			{
				return "bad json (" + ex2.Message + ") — TestBridge removes double quotes: use single quotes {'k':'v'} or @<file>";
			}
			string text2 = jToken.ToString(Formatting.None);
			if (args[0] == "send")
			{
				if (!Send(result5, text2))
				{
					return "not sent (offline) " + result5 + " " + text2;
				}
				return "sent " + result5 + " " + text2;
			}
			return "fake " + result5 + ": " + HandleDown(result5, text2);
		}
		case "click":
			if (args.Length < 2)
			{
				return "usage: dodo click <key> — keys: " + string.Join(" ", OffServerDodoFightUI.ClickableKeys().ToArray());
			}
			if (!OffServerDodoFightUI.Click(args[1]))
			{
				return "not clickable: " + args[1] + " — clickable: " + string.Join(" ", OffServerDodoFightUI.ClickableKeys().ToArray());
			}
			return "clicked " + args[1];
		case "cheer":
		{
			if (args.Length < 2 || !int.TryParse(args[1], out var result6))
			{
				return "usage: dodo cheer <0|1>";
			}
			return "cheer " + result6 + " " + Cheer(result6);
		}
		case "bet":
		{
			if (args.Length < 3 || !int.TryParse(args[1], out var result) || !long.TryParse(args[2], out var result2))
			{
				return "usage: dodo bet <side 0|1> <amount> (no popup)";
			}
			Match currentMatch2 = CurrentMatch;
			if (currentMatch2 == null)
			{
				return "no match";
			}
			return "bet " + SendBet(currentMatch2.Id, result, result2);
		}
		case "hello":
			SendHello();
			return "hello sent " + HelloSent;
		case "lobby":
			RequestLobby();
			return "lobby requested";
		case "close":
			OffServerDodoFightUI.CloseAll();
			return "closed";
		case "reset":
			ResetState();
			return "reset";
		case "pick":
			OffServerDodoFightUI.StartPick();
			return "pick mode " + OffServerDodoFightUI.PickOpen;
		case "pickat":
		{
			if (args.Length < 3 || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
			{
				return "usage: dodo pickat <tileX> <tileZ> (= tapping that spot in pick mode)";
			}
			return "practice at " + result3 + "," + result4 + " sent " + PracticeAt(result3, result4);
		}
		case "tint":
		case "aura":
		case "fxpath":
		case "fx":
		{
			Match currentMatch = CurrentMatch;
			return OffServerDodoFightFx.DebugCommand(args, (currentMatch != null) ? currentMatch.Sides[0].Entity : string.Empty, (currentMatch != null) ? currentMatch.Sides[1].Entity : string.Empty);
		}
		default:
			return "unknown: " + args[0] + " (state|send|fake|click|cheer|bet|hello|lobby|close|reset|pick|pickat|fx|fxpath|aura)";
		}
	}
}

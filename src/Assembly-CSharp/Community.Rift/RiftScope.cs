using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Durango.Logic.Item;
using Durango.Logic.Map;
using Durango.Network;
using Durango.Render.Camera;
using Durango.Terrain;
using Durango.UI;
using Durango.UI.Popup;
using Durango.Utils;
using L10N;
using Messages;
using Newtonsoft.Json.Linq;
using Shared.Battle;
using Shared.Economy;
using UnityEngine;
using Yaml;

namespace Community.Rift;

public sealed class RiftScope : MonoBehaviour
{
	public const string Caps = "rift-scope";

	private const string MarkerPrefix = "rift_scope_";

	private const string TrackKey = "rift_scope_track";

	public const int MaxRadius = 9;

	public const float MarkerSeconds = 10f;

	public const float TrackSeconds = 120f;

	public static readonly Dictionary<int, ScopeAnimal> Discoveries = new Dictionary<int, ScopeAnimal>();

	private const float BarWidth = 220f;

	private const float BarScale = 0.6f;

	private const float BarLift = 60f;

	private const string ProgressPath = "Main/MainView/QuestContainer/Items/ScrollView/QuestBaseWidget/QuestBasePane/Infos/Info/Progress";

	private static readonly Color Aggro = new Color32(byte.MaxValue, 96, 80, byte.MaxValue);

	private static readonly Color Shy = PresetColor.UIYellow;

	private static readonly Color Calm = new Color32(111, byte.MaxValue, 91, byte.MaxValue);

	private static RiftScope instance;

	private static Connection listeningOn;

	private readonly Dictionary<string, UIProgressBar> bars = new Dictionary<string, UIProgressBar>();

	private readonly List<string> markers = new List<string>();

	private float markersUntil;

	private ScopeAnimal tracked;

	private float trackUntil;

	public static ScopeResult Last { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		try
		{
			EnsureInstance();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static void EnsureInstance()
	{
		if (!(instance != null))
		{
			GameObject obj = new GameObject("Rift scope");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			instance = obj.AddComponent<RiftScope>();
		}
	}

	private static void Listen(Connection conn)
	{
		if (conn == null || conn == listeningOn)
		{
			return;
		}
		conn.On(delegate(RiftScopeDown message, PacketHeader header)
		{
			if (conn == Connections.FrontendIfAny && conn == listeningOn)
			{
				OnDown(message, header);
			}
		});
		listeningOn = conn;
	}

	private static void OnDown(RiftScopeDown message, PacketHeader header)
	{
		try
		{
			if (message.Kind == 1)
			{
				RiftCompassHud.Receive(JObject.Parse(message.Json ?? "{}"));
			}
			else if (message.Kind == 0)
			{
				Show(Parse(JObject.Parse(message.Json ?? "{}")));
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static void Show(ScopeResult result)
	{
		EnsureInstance();
		if (result == null || !Singleton<UIManager>.HasInstance() || PlayerBehavior.LocalPlayer == null)
		{
			return;
		}
		result.Radius = 9;
		result.Ttl = 10f;
		HashSet<string> ids = new HashSet<string>();
		result.Animals.RemoveAll(delegate(ScopeAnimal a)
		{
			AnimalBehavior animalBehavior = InScene(a);
			if (animalBehavior == null || !ids.Add(a.Id))
			{
				return true;
			}
			a.Observed = animalBehavior;
			a.Observer = PlayerBehavior.LocalPlayer;
			Discoveries[a.Type] = new ScopeAnimal
			{
				Type = a.Type,
				Name = a.Name,
				Role = a.Role,
				Corpse = a.Corpse
			};
			return false;
		});
		Last = result;
		instance.PlaceMarkers(result);
		RiftScopeWindow.ShowResult(result);
	}

	private static ScopeResult Parse(JObject json)
	{
		ScopeResult scopeResult = new ScopeResult
		{
			At = Num(json["at"], 0.0),
			Ttl = (float)Num(json["ttl"], 10.0),
			Radius = (int)Num(json["radius"], 0.0),
			FromTx = (int)Num((json["from"] == null) ? null : json["from"]["tx"], 0.0),
			FromTz = (int)Num((json["from"] == null) ? null : json["from"]["tz"], 0.0)
		};
		if (!(json["animals"] is JArray jArray))
		{
			return scopeResult;
		}
		foreach (JToken item in (IEnumerable<JToken>)jArray)
		{
			JArray jArray2 = item["corpse"] as JArray;
			scopeResult.Animals.Add(new ScopeAnimal
			{
				Id = (string)item["id"],
				Type = (int)Num(item["type"], 0.0),
				Name = (string)item["name"],
				Level = (int)Num(item["level"], 0.0),
				Life = (float)Num(item["life"], 0.0),
				MaxLife = (float)Num(item["maxLife"], 0.0),
				Role = (((string)item["role"]) ?? "calm"),
				Boss = Flag(item["boss"]),
				Elite = Flag(item["elite"]),
				Dist = (int)Num(item["dist"], 0.0),
				Tx = (int)Num(item["tx"], 0.0),
				Tz = (int)Num(item["tz"], 0.0),
				Px = (float)Num(item["px"], Num(item["tx"], 0.0)),
				Pz = (float)Num(item["pz"], Num(item["tz"], 0.0)),
				Sleeping = Flag(item["sleeping"]),
				Fighting = Flag(item["fighting"]),
				Corpse = ((jArray2 == null) ? new string[0] : jArray2.ToObject<string[]>()),
				Attack = OptionalNumber(item["attack"]),
				AttackRating = OptionalNumber(item["attackRating"]),
				Defense = OptionalNumber(item["defense"])
			});
		}
		return scopeResult;
	}

	public static double? OptionalNumber(JToken token)
	{
		if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
		{
			return null;
		}
		double num = token.Value<double>();
		if (!double.IsNaN(num) && !double.IsInfinity(num))
		{
			return num;
		}
		return null;
	}

	public static string StatText(double? value)
	{
		if (!value.HasValue)
		{
			return "—";
		}
		return value.Value.ToString("N0", T.Culture);
	}

	private static double Num(JToken token, double fallback)
	{
		if (token != null && token.Type != JTokenType.Null)
		{
			return token.Value<double>();
		}
		return fallback;
	}

	private static bool Flag(JToken token)
	{
		if (token != null && token.Type == JTokenType.Boolean)
		{
			return (bool)token;
		}
		return false;
	}

	public static Color RoleColor(string role)
	{
		if (!(role == "aggro"))
		{
			if (!(role == "shy"))
			{
				return Calm;
			}
			return Shy;
		}
		return Aggro;
	}

	public static string RoleName(string role)
	{
		if (!(role == "aggro"))
		{
			if (!(role == "shy"))
			{
				return RiftText.T("สงบ", "Calm");
			}
			return RiftText.T("ข\u0e35\u0e49กล\u0e31ว", "Skittish");
		}
		return RiftText.T("ด\u0e38", "Aggressive");
	}

	public static string MapIcon(ScopeAnimal animal)
	{
		if (!(animal.Role == "aggro"))
		{
			return "icon_map_herbivorous_animal";
		}
		return "icon_map_carnivorous_animal";
	}

	public static AnimalBehavior InScene(ScopeAnimal animal)
	{
		if (animal == null || string.IsNullOrEmpty(animal.Id) || !Singleton<AnimalManager>.HasInstance())
		{
			return null;
		}
		AnimalBehavior animal2 = Singleton<AnimalManager>.Instance().GetAnimal(animal.Id);
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (animal2 == null || localPlayer == null || !localPlayer.gameObject.activeInHierarchy || !animal2.IsPresentAndShown || !animal2.IsAlive || animal2.EntityTypeId != animal.Type || animal2.GetComponent<PetAI>() != null || animal2.gameObject.scene != localPlayer.gameObject.scene || animal2.Floor.Value != localPlayer.Floor.Value || (animal.Observer != null && animal.Observer != localPlayer) || (animal.Observed != null && animal.Observed != animal2) || (animal2.Life != null && animal2.Life.Get() <= 0f))
		{
			return null;
		}
		Vector2 a = Durango.Terrain.Util.ClientPositionToTilePosition(animal2.CurrentPosition);
		Vector2 p = Durango.Terrain.Util.ClientPositionToTilePosition(localPlayer.CurrentPosition);
		if (TileDistance(a, p) > 9)
		{
			return null;
		}
		return animal2;
	}

	public static int TileDistance(Vector2 a, Vector2 p)
	{
		Point2 point = new Point2(a);
		Point2 point2 = new Point2(p);
		return Math.Max(Math.Abs(point.x - point2.x), Math.Abs(point.y - point2.y));
	}

	public static bool IsEligible(ScopeAnimal animal)
	{
		if (animal != null && animal.Observed != null && animal.Observer != null)
		{
			return InScene(animal) == animal.Observed;
		}
		return false;
	}

	public static void Life(ScopeAnimal animal, out float life, out float maxLife)
	{
		AnimalBehavior animalBehavior = InScene(animal);
		if (animalBehavior != null && animalBehavior.Life != null)
		{
			life = (animalBehavior.IsAlive ? Mathf.Max(0f, animalBehavior.Life.Get()) : 0f);
			maxLife = animalBehavior.Life.Max();
			if (maxLife > 0f)
			{
				return;
			}
		}
		life = 0f;
		maxLife = 0f;
	}

	public static string Describe(ScopeAnimal animal, float life)
	{
		List<string> list = new List<string> { RoleName(animal.Role) };
		if (animal.Boss)
		{
			list.Add(RiftText.T("บอส", "Boss"));
		}
		else if (animal.Elite)
		{
			list.Add(RiftText.T("อ\u0e35ล\u0e34ต", "Elite"));
		}
		int num = animal.Dist;
		AnimalBehavior animalBehavior = InScene(animal);
		if (animalBehavior != null && PlayerBehavior.LocalPlayer != null)
		{
			Vector2 a = Durango.Terrain.Util.ClientPositionToTilePosition(animalBehavior.CurrentPosition);
			Vector2 p = Durango.Terrain.Util.ClientPositionToTilePosition(PlayerBehavior.LocalPlayer.CurrentPosition);
			num = TileDistance(a, p);
		}
		list.Add(RiftText.T("ห\u0e48าง {0} ไทล\u0e4c", "{0} tiles away", num));
		if (life <= 0f)
		{
			list.Add(RiftText.T("ล\u0e49มแล\u0e49ว", "Down"));
		}
		else if (animal.Fighting)
		{
			list.Add(RiftText.T("กำล\u0e31งส\u0e39\u0e49ก\u0e31บค\u0e38ณ", "Fighting you"));
		}
		else if (animal.Sleeping)
		{
			list.Add(RiftText.T("หล\u0e31บอย\u0e39\u0e48", "Sleeping"));
		}
		return string.Join(" · ", list.ToArray());
	}

	private void PlaceMarkers(ScopeResult result)
	{
		ClearMarkers();
		markersUntil = Time.unscaledTime + Mathf.Max(1f, result.Ttl);
		foreach (ScopeAnimal animal in result.Animals)
		{
			if (IsEligible(animal))
			{
				string text = "rift_scope_" + animal.Id;
				MapIconIndicator orAdd = MapIndicators.GetOrAdd<MapIconIndicator>(text, IndicatorType.Animal);
				if (orAdd != null)
				{
					AnimalBehavior animalBehavior = InScene(animal);
					orAdd.SetTarget(animalBehavior.gameObject);
					orAdd.SetIcon(MapIcon(animal), RoleColor(animal.Role), 30, 21);
					orAdd.SetTooltip(animal.DisplayName + " " + LocalizeUtil.FormatLevel(animal.Level));
					markers.Add(text);
				}
				AddBar(animal);
			}
		}
	}

	private void ClearMarkers()
	{
		foreach (string marker in markers)
		{
			MapIndicators.Remove(marker, IndicatorType.Animal);
		}
		markers.Clear();
		foreach (UIProgressBar value in bars.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
		bars.Clear();
		markersUntil = 0f;
	}

	private void AddBar(ScopeAnimal animal)
	{
		if (!Singleton<UIManager>.HasInstance() || InScene(animal) == null || bars.ContainsKey(animal.Id))
		{
			return;
		}
		QuestGroup questGroup = UIManager.FindScript<QuestGroup>();
		AnimalFloatingGroup animalFloatingGroup = UIManager.FindScript<AnimalFloatingGroup>();
		Transform transform = ((questGroup == null) ? null : questGroup.transform.Find("Main/MainView/QuestContainer/Items/ScrollView/QuestBaseWidget/QuestBasePane/Infos/Info/Progress"));
		if (!(transform == null) && !(animalFloatingGroup == null))
		{
			GameObject obj = UnityEngine.Object.Instantiate(transform.gameObject, animalFloatingGroup.transform, worldPositionStays: false);
			obj.name = "RiftScopeLife " + animal.Id;
			NGUITools.SetLayer(obj, animalFloatingGroup.gameObject.layer);
			obj.SetActive(value: true);
			UIWidget component = obj.GetComponent<UIWidget>();
			if (component != null)
			{
				component.SetAnchor((Transform)null);
				component.width = 220;
			}
			obj.transform.localScale = Vector3.one * 0.6f;
			UIProgressBar component2 = obj.GetComponent<UIProgressBar>();
			component2.foregroundWidget.color = new Color32(134, 12, 10, byte.MaxValue);
			UIUtility.UpdateAnchors(obj.transform);
			bars[animal.Id] = component2;
		}
	}

	private void UpdateBars()
	{
		if (Last == null || !Singleton<UIManager>.HasInstance() || !Singleton<MainCamera>.HasInstance())
		{
			return;
		}
		float num = (Singleton<MainCamera>.HasInstance() ? Singleton<MainCamera>.Instance().Zoom : 1f);
		foreach (ScopeAnimal animal in Last.Animals)
		{
			if (!bars.TryGetValue(animal.Id, out var value) || value == null)
			{
				continue;
			}
			AnimalBehavior animalBehavior = InScene(animal);
			value.gameObject.SetActive(animalBehavior != null);
			if (!(animalBehavior == null))
			{
				Transform bodyPartTransform = animalBehavior.GetBodyPartTransform(BodyPart.Head, bAllowNull: true);
				Vector3 localPosition = MainCamera.WorldToNGUIPos(((bodyPartTransform != null) ? bodyPartTransform : animalBehavior.transform).position);
				localPosition.y += 60f * num;
				value.transform.localPosition = localPosition;
				Life(animal, out var life, out var maxLife);
				value.value = ((maxLife > 0f) ? Mathf.Clamp01(life / maxLife) : 0f);
				UILabel componentInChildren = value.GetComponentInChildren<UILabel>(includeInactive: true);
				if (componentInChildren != null)
				{
					componentInChildren.text = string.Format(T.Culture, "{0:N0} / {1:N0}", Mathf.CeilToInt(life), Mathf.CeilToInt(maxLife));
				}
			}
		}
	}

	public static bool IsTracked(string id)
	{
		if (instance != null && instance.tracked != null)
		{
			return instance.tracked.Id == id;
		}
		return false;
	}

	public static void ToggleTrack(ScopeAnimal animal)
	{
		EnsureInstance();
		if (animal != null)
		{
			if (IsTracked(animal.Id))
			{
				instance.StopTrack();
			}
			else if (IsEligible(animal) && Last != null && Last.Animals.Contains(animal))
			{
				instance.tracked = animal;
				instance.trackUntil = Time.unscaledTime + 120f;
				instance.PointAt(animal);
			}
		}
	}

	private void PointAt(ScopeAnimal animal)
	{
		if (!Singleton<UIManager>.HasInstance())
		{
			return;
		}
		NavigateGroup navigateGroup = UIManager.FindScript<NavigateGroup>();
		if (!(navigateGroup == null) && !(navigateGroup.Point == null))
		{
			AnimalBehavior animalBehavior = InScene(animal);
			string portrait = AnimalYaml.GetPortrait(animal.Type);
			bool flag = !string.IsNullOrEmpty(portrait) && ResourceSingleton<UISpriteManager>.Instance().GetSprite(portrait) != null;
			PointTargetController.Arguments args = new PointTargetController.Arguments
			{
				Icon = (flag ? portrait : MapIcon(animal)),
				IconSize = 42,
				IconColor = (flag ? Color.white : RoleColor(animal.Role)),
				BorderColor = Durango.Logic.Item.Inventory.RiftCoinColor,
				ShowBg = true
			};
			if (!(animalBehavior == null))
			{
				args.Target = animalBehavior.transform;
				navigateGroup.Point.SetTarget("rift_scope_track", args);
			}
		}
	}

	private void StopTrack()
	{
		if (tracked == null)
		{
			return;
		}
		tracked = null;
		trackUntil = 0f;
		if (Singleton<UIManager>.HasInstance())
		{
			NavigateGroup navigateGroup = UIManager.FindScript<NavigateGroup>();
			if (navigateGroup != null && navigateGroup.Point != null)
			{
				navigateGroup.Point.ClearTarget("rift_scope_track");
			}
		}
	}

	private void UpdateTrack()
	{
		if (tracked != null && (Time.unscaledTime >= trackUntil || !IsEligible(tracked)))
		{
			StopTrack();
		}
	}

	private void Update()
	{
		Connection frontendIfAny = Connections.FrontendIfAny;
		if (frontendIfAny != listeningOn)
		{
			ClearMarkers();
			StopTrack();
			Last = null;
			Discoveries.Clear();
			RiftCompassHud.Clear();
			if (Singleton<UIManager>.HasInstance())
			{
				RiftScopeWindow.ResetSession();
			}
			listeningOn = null;
			Listen(frontendIfAny);
		}
		if (markersUntil > 0f && Time.unscaledTime >= markersUntil)
		{
			ClearMarkers();
		}
		if (Last != null)
		{
			foreach (ScopeAnimal animal in Last.Animals)
			{
				if (IsEligible(animal))
				{
					continue;
				}
				string text = "rift_scope_" + animal.Id;
				if (markers.Remove(text))
				{
					MapIndicators.Remove(text, IndicatorType.Animal);
				}
				if (bars.TryGetValue(animal.Id, out var value))
				{
					if (value != null)
					{
						UnityEngine.Object.Destroy(value.gameObject);
					}
					bars.Remove(animal.Id);
				}
			}
		}
		UpdateTrack();
	}

	private void OnDestroy()
	{
		if (!(instance != this))
		{
			ClearMarkers();
			StopTrack();
			Last = null;
			Discoveries.Clear();
			instance = null;
			listeningOn = null;
		}
	}

	private void LateUpdate()
	{
		if (bars.Count > 0)
		{
			UpdateBars();
		}
	}

	public static string DebugCommand(string[] args)
	{
		EnsureInstance();
		switch ((args.Length != 0) ? args[0].ToLowerInvariant() : "state")
		{
		case "fake":
			Show((args.Length > 1) ? Parse(JObject.Parse(string.Join(" ", args, 1, args.Length - 1).Replace('\'', '"'))) : FakeFromScene());
			return "fake result shown";
		case "track":
		{
			if (Last == null || Last.Animals.Count == 0)
			{
				return "no result";
			}
			int value = ((args.Length > 1) ? int.Parse(args[1], CultureInfo.InvariantCulture) : 0);
			ToggleTrack(Last.Animals[Mathf.Clamp(value, 0, Last.Animals.Count - 1)]);
			return "track toggled";
		}
		case "tab":
			if (!RiftScopeWindow.SelectTab((args.Length > 1) ? int.Parse(args[1], CultureInfo.InvariantCulture) : 0))
			{
				return "window not open";
			}
			return "tab selected";
		case "close":
			RiftScopeWindow.CloseWindow();
			return "closed";
		case "clear":
			instance.ClearMarkers();
			instance.StopTrack();
			return "cleared";
		case "coin":
			return FakeRiftCoin((args.Length > 1) ? long.Parse(args[1], CultureInfo.InvariantCulture) : 125);
		case "wallet":
			UIManager.Popup.Tooltip<WalletInfoPopup>().Show(Singleton<UIManager>.Instance().UIRoot.transform, Vector2.zero);
			UIManager.SystemMsg(Durango.Logic.Item.Inventory.CurrencyFormat(InventorySystem.Wallet.GetBalance(Currency.RiftCoin), Currency.RiftCoin));
			return "wallet shown";
		default:
			return null;
		}
	}

	public static Dictionary<string, object> DebugState(bool inspect = false)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary["listening"] = listeningOn != null && listeningOn == Connections.FrontendIfAny;
		dictionary["window"] = RiftScopeWindow.IsOpen;
		dictionary["animals"] = ((Last != null) ? Last.Animals.Count : 0);
		dictionary["markers"] = ((!(instance == null)) ? instance.markers.Count : 0);
		dictionary["bars"] = ((!(instance == null)) ? instance.bars.Count : 0);
		dictionary["tracked"] = ((instance == null || instance.tracked == null) ? null : instance.tracked.Id);
		dictionary["rift_coin"] = InventorySystem.Wallet.GetBalance(Currency.RiftCoin);
		if (inspect)
		{
			dictionary["presentation"] = RiftScopeWindow.DebugPresentation();
			List<object> list = new List<object>();
			if (Last != null)
			{
				foreach (ScopeAnimal animal in Last.Animals)
				{
					list.Add(new Dictionary<string, object>
					{
						{ "id", animal.Id },
						{ "type", animal.Type },
						{ "name", animal.DisplayName },
						{
							"eligible",
							IsEligible(animal)
						},
						{ "tx", animal.Tx },
						{ "tz", animal.Tz }
					});
				}
			}
			dictionary["results"] = list;
			List<object> list2 = new List<object>();
			PointTargetWidget[] array = UnityEngine.Object.FindObjectsByType<PointTargetWidget>();
			foreach (PointTargetWidget pointTargetWidget in array)
			{
				if (!(pointTargetWidget.Key != "rift_scope_track"))
				{
					UISprite uISprite = (UISprite)typeof(PointTargetWidget).GetField("_portraitSprite", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointTargetWidget);
					UISprite uISprite2 = (UISprite)typeof(PointTargetWidget).GetField("_bgSprite", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointTargetWidget);
					list2.Add(new Dictionary<string, object>
					{
						{ "sprite", uISprite.spriteName },
						{
							"color",
							uISprite.color.ToString()
						},
						{ "width", uISprite.width },
						{ "height", uISprite.height },
						{
							"background",
							uISprite2.gameObject.activeSelf
						},
						{
							"visible",
							uISprite.gameObject.activeInHierarchy
						}
					});
				}
			}
			dictionary["trackingPoints"] = list2;
		}
		return dictionary;
	}

	private static ScopeResult FakeFromScene()
	{
		ScopeResult scopeResult = new ScopeResult
		{
			Ttl = 10f,
			Radius = 30
		};
		if (PlayerBehavior.LocalPlayer == null || !Singleton<AnimalManager>.HasInstance())
		{
			return scopeResult;
		}
		Vector2 vector = Durango.Terrain.Util.ClientPositionToTilePosition(PlayerBehavior.LocalPlayer.CurrentPosition);
		scopeResult.FromTx = (int)vector.x;
		scopeResult.FromTz = (int)vector.y;
		string[] array = new string[3] { "aggro", "shy", "calm" };
		string[][] array2 = new string[3][]
		{
			new string[2] { "meat_raw", "leather" },
			new string[2] { "meat_raw", "feather" },
			new string[1] { "meat_raw" }
		};
		List<KeyValuePair<int, AnimalBehavior>> list = new List<KeyValuePair<int, AnimalBehavior>>();
		AnimalBehavior[] array3 = UnityEngine.Object.FindObjectsByType<AnimalBehavior>();
		foreach (AnimalBehavior animalBehavior in array3)
		{
			if (!(animalBehavior == null) && animalBehavior.IsAlive && !string.IsNullOrEmpty(animalBehavior.EntityId) && !(animalBehavior.GetComponent<PetAI>() != null))
			{
				Vector2 vector2 = Durango.Terrain.Util.ClientPositionToTilePosition(animalBehavior.CurrentPosition);
				int num = Mathf.FloorToInt(Mathf.Max(Mathf.Abs(vector2.x - vector.x), Mathf.Abs(vector2.y - vector.y)));
				if (num <= scopeResult.Radius)
				{
					list.Add(new KeyValuePair<int, AnimalBehavior>(num, animalBehavior));
				}
			}
		}
		list.Sort((KeyValuePair<int, AnimalBehavior> x, KeyValuePair<int, AnimalBehavior> y) => x.Key.CompareTo(y.Key));
		for (int num2 = 0; num2 < list.Count && num2 < 8; num2++)
		{
			AnimalBehavior value = list[num2].Value;
			Vector2 vector3 = Durango.Terrain.Util.ClientPositionToTilePosition(value.CurrentPosition);
			scopeResult.Animals.Add(new ScopeAnimal
			{
				Id = value.EntityId,
				Type = value.EntityTypeId,
				Name = value.GetName(),
				Level = value.Level,
				Life = ((value.Life == null) ? 0f : value.Life.Get()),
				MaxLife = ((value.Life == null) ? 0f : value.Life.Max()),
				Role = array[num2 % 3],
				Dist = list[num2].Key,
				Tx = (int)vector3.x,
				Tz = (int)vector3.y,
				Px = vector3.x,
				Pz = vector3.y,
				Sleeping = (num2 == 2),
				Fighting = (num2 == 0),
				Corpse = array2[num2 % 3]
			});
		}
		return scopeResult;
	}

	private static string FakeRiftCoin(long amount)
	{
		if (!GameSystem<InventorySystem>.HasInstance())
		{
			return "no inventory";
		}
		Wallet wallet = InventorySystem.Wallet;
		Dictionary<Currency, long> dictionary = ((wallet.UnpaidBalances == null) ? new Dictionary<Currency, long>() : new Dictionary<Currency, long>(wallet.UnpaidBalances));
		dictionary[Currency.RiftCoin] = amount;
		wallet.UnpaidBalances = dictionary;
		MethodInfo method = typeof(InventorySystem).GetMethod("UpdatePlayerWallet", BindingFlags.Instance | BindingFlags.NonPublic);
		if (method == null)
		{
			return "no wallet update";
		}
		method.Invoke(GameSystem<InventorySystem>.Instance(), new object[1] { wallet });
		return "rift coin = " + amount;
	}
}

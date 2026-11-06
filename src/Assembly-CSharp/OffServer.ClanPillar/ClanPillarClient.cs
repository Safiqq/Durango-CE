using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BestHTTP;
using Durango.Development;
using Durango.Logic.Clusters;
using Durango.Logic.Estate;
using Durango.Logic.Explore;
using Durango.Logic.Map;
using Durango.UI;
using Durango.Utils;
using Messages;
using Newtonsoft.Json.Linq;
using Shared.Estate;
using UnityEngine;

namespace OffServer.ClanPillar;

public sealed class ClanPillarClient : MonoBehaviour
{
	private sealed class Pillar
	{
		public string RegionId;

		public string ClanId;

		public string ClanName;

		public Point2 Tile;

		public Point2? Gate;

		public PillarSiegeState Siege;
	}

	private const float PollSeconds = 15f;

	private const string FlagPrefix = "offserver-pillar-";

	private const string BadgeName = "PillarClanEmblem";

	private const string GatePrefix = "offserver-riftgate-";

	private static ClanPillarClient _instance;

	private readonly Dictionary<string, Pillar> _pillars = new Dictionary<string, Pillar>();

	private readonly Dictionary<ExploreRegionNode, string> _badgeOwners = new Dictionary<ExploreRegionNode, string>();

	private readonly Dictionary<ExploreRegionNode, float> _badgeRetry = new Dictionary<ExploreRegionNode, float>();

	private readonly Dictionary<MapFlagIndicator, float> _flagRetry = new Dictionary<MapFlagIndicator, float>();

	private readonly HashSet<string> _flags = new HashSet<string>();

	private readonly HashSet<string> _gates = new HashSet<string>();

	private HTTPRequest _request;

	private string _url = "";

	private string _etag = "";

	private string _region = "";

	private string _diagnostic = "not requested";

	private bool _valid;

	private bool _queued;

	private bool _mapWasOpen;

	private bool _sailWasOpen;

	private float _lastAttempt = -100f;

	private float _nextTick;

	private int _generation;

	private int _requests;

	private int _status;

	private int _nameVersion;

	private double _serverTime;

	private float _clockAt;

	private float _verifiedAt = -100f;

	public static void Attach()
	{
		if (!(_instance != null))
		{
			GameObject obj = new GameObject("OffServerClanPillars");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			_instance = obj.AddComponent<ClanPillarClient>();
			obj.AddComponent<PillarSiegeClient>();
		}
	}

	public static bool TrySiege(string regionId, out PillarSiegeState state, out double now)
	{
		state = null;
		now = 0.0;
		ClanPillarClient instance = _instance;
		if (instance == null || !instance._valid || Time.realtimeSinceStartup - instance._verifiedAt > 45f || string.IsNullOrEmpty(regionId) || !instance._pillars.TryGetValue(regionId, out var value) || value.Siege == null)
		{
			return false;
		}
		state = value.Siege;
		now = instance._serverTime + (double)Time.realtimeSinceStartup - (double)instance._clockAt;
		return true;
	}

	public static bool IsPillarRegion(Durango.Logic.Explore.Region region)
	{
		if (region != null && region.TemplateId != null)
		{
			return region.TemplateId.EndsWith("Pillar", StringComparison.Ordinal);
		}
		return false;
	}

	public static Durango.Logic.Explore.Region RegionOf(Route route)
	{
		if (GameManager.Region == null || !(route.RegionId == GameManager.Region.Id))
		{
			return route.Region();
		}
		return GameManager.Region;
	}

	public static bool IsPillarRoute(Route route)
	{
		return IsPillarRegion(RegionOf(route));
	}

	public static GameObject SiegeTarget(GameObject o)
	{
		Artifact artifact = ((o == null) ? null : o.GetComponentInParent<Artifact>());
		if (artifact == null || artifact.Durability == null || artifact.Durability.When(0f) <= 0.0)
		{
			return null;
		}
		EstateInfo estateInfo = EstateSystem.GetEstateInfo(artifact.WorldTile);
		if (estateInfo == null || estateInfo.License.Type != OwnerType.ClanWarphole || estateInfo.License.IsProtected() || ClanSystem.IsMyClanOrAlliance(estateInfo.License.OwnerId))
		{
			return null;
		}
		return artifact.gameObject;
	}

	private string Source()
	{
		string text = (string.IsNullOrEmpty(GameManager.GatewayUrl) ? OffServerLink.Gateway : GameManager.GatewayUrl);
		if (!string.IsNullOrEmpty(text))
		{
			return text.TrimEnd('/') + "/clan-pillars";
		}
		return "";
	}

	private static bool IsOpen<T>() where T : UIBase
	{
		return UnityEngine.Object.FindObjectsByType<T>().Any((T v) => v.IsOpened);
	}

	private void OnDestroy()
	{
		if (_request != null)
		{
			_request.Abort();
		}
		if (_instance == this)
		{
			_instance = null;
		}
	}

	private void Update()
	{
		if (Time.realtimeSinceStartup < _nextTick)
		{
			return;
		}
		_nextTick = Time.realtimeSinceStartup + 0.5f;
		if (GameManager.IsTitleScene || GameManager.Region == null)
		{
			_region = "";
			_mapWasOpen = (_sailWasOpen = false);
			return;
		}
		string text = Source();
		if (text != _url)
		{
			_generation++;
			if (_request != null)
			{
				_request.Abort();
			}
			_request = null;
			_url = text;
			_etag = "";
			_valid = false;
			_pillars.Clear();
			_lastAttempt = -100f;
			Hide();
			_queued = true;
		}
		bool flag = IsOpen<WorldMapGroup>();
		bool flag2 = IsOpen<ExploreGroup>();
		if (_region != GameManager.Region.Id || (flag && !_mapWasOpen) || (flag2 && !_sailWasOpen))
		{
			_queued = true;
		}
		_region = GameManager.Region.Id;
		_mapWasOpen = flag;
		_sailWasOpen = flag2;
		if ((flag || flag2 || IsPillarRegion(GameManager.Region)) && Time.realtimeSinceStartup - _lastAttempt >= 15f)
		{
			_queued = true;
		}
		if (_queued && _request == null && Time.realtimeSinceStartup - _lastAttempt >= 15f)
		{
			Fetch();
		}
		if (_valid)
		{
			Apply();
		}
	}

	private void Fetch()
	{
		_queued = false;
		if (!Uri.TryCreate(_url, UriKind.Absolute, out var result) || (result.Scheme != "http" && result.Scheme != "https"))
		{
			return;
		}
		int generation = _generation;
		_lastAttempt = Time.realtimeSinceStartup;
		_requests++;
		_request = new HTTPRequest(result, HTTPMethods.Get, delegate(HTTPRequest req, HTTPResponse response)
		{
			if (generation != _generation || req != _request)
			{
				return;
			}
			_request = null;
			_status = response?.StatusCode ?? 0;
			try
			{
				if (_status == 304 && _valid)
				{
					_verifiedAt = Time.realtimeSinceStartup;
					_diagnostic = "not modified";
					Apply();
				}
				else
				{
					if (_status != 200)
					{
						throw new InvalidOperationException("HTTP " + _status);
					}
					double serverTime;
					Dictionary<string, Pillar> next = Parse(response.DataAsText, out serverTime);
					List<string> ids = (from id in _pillars.Keys.Union(next.Keys)
						where !_pillars.ContainsKey(id) || !next.ContainsKey(id) || _pillars[id].ClanId != next[id].ClanId || _pillars[id].ClanName != next[id].ClanName
						select id).ToList();
					_pillars.Clear();
					foreach (KeyValuePair<string, Pillar> item in next)
					{
						_pillars.Add(item.Key, item.Value);
					}
					_etag = response.GetFirstHeaderValue("ETag") ?? "";
					_valid = true;
					_diagnostic = "ready";
					_serverTime = serverTime;
					_clockAt = (_verifiedAt = Time.realtimeSinceStartup);
					RefreshNames(ids);
					Apply();
				}
			}
			catch (Exception ex)
			{
				_diagnostic = ex.Message;
				_valid = false;
				_etag = "";
				_pillars.Clear();
				Hide();
			}
		});
		_request.DisableCache = true;
		_request.ConnectTimeout = TimeSpan.FromSeconds(3.0);
		_request.Timeout = TimeSpan.FromSeconds(8.0);
		if (_etag.Length > 0)
		{
			_request.AddHeader("If-None-Match", _etag);
		}
		_request.Send();
	}

	private static Dictionary<string, Pillar> Parse(string text, out double serverTime)
	{
		JObject jObject = JObject.Parse(text);
		JValue jValue = jObject["serverTime"] as JValue;
		serverTime = ((jValue != null && (jValue.Type == JTokenType.Integer || jValue.Type == JTokenType.Float)) ? Convert.ToDouble(jValue.Value, CultureInfo.InvariantCulture) : 0.0);
		JArray jArray = jObject["pillars"] as JArray;
		if ((int?)jObject["v"] != 1 || jArray == null)
		{
			throw new InvalidOperationException("schema");
		}
		Dictionary<string, Pillar> dictionary = new Dictionary<string, Pillar>();
		foreach (JToken item in (IEnumerable<JToken>)jArray)
		{
			JObject jObject2 = item as JObject;
			JArray jArray2 = ((jObject2 == null) ? null : (jObject2["tile"] as JArray));
			string text2 = ((jObject2 == null) ? null : ((string)jObject2["regionId"]));
			if (string.IsNullOrEmpty(text2) || dictionary.ContainsKey(text2) || jArray2 == null || jArray2.Count != 2 || jArray2.Any((JToken v) => v.Type != JTokenType.Integer || (int)v < 0 || (int)v > 65535))
			{
				throw new InvalidOperationException("tile/id");
			}
			JToken jToken = jObject2["clanId"];
			JToken jToken2 = jObject2["clanName"];
			if (jToken == null || jToken2 == null)
			{
				throw new InvalidOperationException("owner");
			}
			if (jToken2.Type != JTokenType.Null && jToken2.Type != JTokenType.String)
			{
				throw new InvalidOperationException("clan name");
			}
			if (jToken.Type != JTokenType.Null && (jToken.Type != JTokenType.String || string.IsNullOrEmpty((string)jToken)))
			{
				throw new InvalidOperationException("clan id");
			}
			Point2? gate = ((jObject2["riftGate"] is JArray { Count: 2 } jArray3 && jArray3.All((JToken v) => v.Type == JTokenType.Integer && (int)v >= 0 && (int)v <= 65535)) ? new Point2?(new Point2((int)jArray3[0], (int)jArray3[1])) : ((Point2?)null));
			dictionary.Add(text2, new Pillar
			{
				RegionId = text2,
				ClanId = (string)jToken,
				ClanName = (string)jToken2,
				Tile = new Point2((int)jArray2[0], (int)jArray2[1]),
				Gate = gate,
				Siege = ((serverTime > 0.0 && !double.IsNaN(serverTime) && !double.IsInfinity(serverTime)) ? PillarSiegeState.Read(jObject2) : null)
			});
		}
		return dictionary;
	}

	private void RefreshNames(List<string> ids)
	{
		if (ids.Count == 0 || !GameSystem<MapSystem>.HasInstance())
		{
			return;
		}
		int generation = _generation;
		int version = ++_nameVersion;
		GameSystem<MapSystem>.Instance().GetRegions(ids, delegate(Messages.Region[] regions)
		{
			if (generation == _generation && version == _nameVersion && regions != null)
			{
				for (int i = 0; i < regions.Length; i++)
				{
					Messages.Region region = regions[i];
					if (!string.IsNullOrEmpty(region.Id))
					{
						GameSystem<ExploreSystem>.Instance().AddRegion(new Durango.Logic.Explore.Region(region));
						if (GameManager.Region != null && region.Id == GameManager.Region.Id)
						{
							string text = GameManager.Region.Name;
							GameManager.Region.Rename(region.Name);
							WorldMapGroup worldMapGroup = UnityEngine.Object.FindObjectsByType<WorldMapGroup>().FirstOrDefault((WorldMapGroup v) => v.IsOpened);
							if (worldMapGroup != null && !string.IsNullOrEmpty(text))
							{
								UILabel[] componentsInChildren = worldMapGroup.GetComponentsInChildren<UILabel>();
								foreach (UILabel uILabel in componentsInChildren)
								{
									if (uILabel.text.Contains(text))
									{
										uILabel.text = uILabel.text.Replace(text, region.Name ?? "");
									}
								}
							}
						}
					}
				}
				ExploreRegionNode[] array = UnityEngine.Object.FindObjectsByType<ExploreRegionNode>();
				foreach (ExploreRegionNode exploreRegionNode in array)
				{
					if (ids.Contains(exploreRegionNode.Route.RegionId))
					{
						exploreRegionNode.Set(exploreRegionNode.Route);
					}
				}
				Apply();
			}
		}, refresh: true);
	}

	private void Apply()
	{
		if (_valid && GameManager.Region != null)
		{
			ApplyFlags();
			ApplyGates();
			ApplyBadges();
		}
	}

	private void ApplyGates()
	{
		if (!Singleton<MapIndicators>.HasInstance())
		{
			return;
		}
		HashSet<string> alive = new HashSet<string>();
		foreach (Pillar value in _pillars.Values)
		{
			if (!(value.RegionId != GameManager.Region.Id) && value.Gate.HasValue)
			{
				string text = "offserver-riftgate-" + value.RegionId;
				alive.Add(text);
				MapIconIndicator orAdd = MapIndicators.GetOrAdd<MapIconIndicator>(text, IndicatorType.Artifact);
				if (!(orAdd == null))
				{
					orAdd.SetTarget(value.Gate.Value);
					orAdd.SetIcon("warphole_personal", RiftGateClient.IconColor, 36, 10);
					orAdd.SetTooltip("ประต\u0e39รอยแยก");
				}
			}
		}
		string[] array = _gates.Where((string k) => !alive.Contains(k)).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			MapIndicators.Remove(array[num], IndicatorType.Artifact);
		}
		_gates.Clear();
		_gates.UnionWith(alive);
	}

	private void ApplyFlags()
	{
		if (!Singleton<MapIndicators>.HasInstance())
		{
			return;
		}
		HashSet<string> alive = new HashSet<string>();
		foreach (Pillar value2 in _pillars.Values)
		{
			if (value2.RegionId != GameManager.Region.Id || string.IsNullOrEmpty(value2.ClanId))
			{
				continue;
			}
			string text = "offserver-pillar-" + value2.RegionId;
			alive.Add(text);
			MapFlagIndicator orAdd = MapIndicators.GetOrAdd<MapFlagIndicator>(text, IndicatorType.ClanFlag);
			if (!(orAdd == null))
			{
				orAdd.SetTarget(value2.Tile);
				orAdd.SetTooltip(value2.ClanName ?? "");
				orAdd.ToggleHideFlag(MapIndicator.HideFlag.Zoom, hide: false);
				if (orAdd.ClanId != value2.ClanId || (!orAdd.HasEmblem && (!_flagRetry.TryGetValue(orAdd, out var value) || Time.realtimeSinceStartup >= value)))
				{
					_flagRetry[orAdd] = Time.realtimeSinceStartup + 15f;
					orAdd.SetOwnerClan(value2.ClanId);
				}
			}
		}
		string[] array = _flags.Where((string k) => !alive.Contains(k)).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			MapIndicators.Remove(array[num], IndicatorType.ClanFlag);
		}
		_flags.Clear();
		_flags.UnionWith(alive);
	}

	private void ApplyBadges()
	{
		ExploreRegionNode[] array = UnityEngine.Object.FindObjectsByType<ExploreRegionNode>();
		foreach (ExploreRegionNode exploreRegionNode in array)
		{
			Pillar value;
			string text = (_pillars.TryGetValue(exploreRegionNode.Route.RegionId, out value) ? value.ClanId : null);
			Transform transform = exploreRegionNode.transform.Find("PillarClanEmblem");
			if (string.IsNullOrEmpty(text))
			{
				if (transform != null)
				{
					transform.gameObject.SetActive(value: false);
				}
				_badgeOwners.Remove(exploreRegionNode);
				_badgeRetry.Remove(exploreRegionNode);
				continue;
			}
			UITexture texture;
			if (transform == null)
			{
				texture = exploreRegionNode.gameObject.AddWidget<UITexture>(exploreRegionNode.RegionSpriteDepth + 5);
				texture.name = "PillarClanEmblem";
				texture.SetDimensions(36, 36);
				texture.transform.localPosition = new Vector3(35f, 35f, 0f);
			}
			else
			{
				texture = transform.GetComponent<UITexture>();
			}
			if (_badgeOwners.TryGetValue(exploreRegionNode, out var value2) && value2 == text && (texture.mainTexture != null || (_badgeRetry.TryGetValue(exploreRegionNode, out var value3) && Time.realtimeSinceStartup < value3)))
			{
				continue;
			}
			_badgeRetry[exploreRegionNode] = Time.realtimeSinceStartup + 15f;
			_badgeOwners[exploreRegionNode] = text;
			texture.gameObject.SetActive(value: false);
			texture.mainTexture = null;
			ExploreRegionNode key = exploreRegionNode;
			string requested = text;
			ClanSystem.GetEmblem(text, delegate(Point2 pos)
			{
				if (!(texture == null) && !(key == null) && _valid && _badgeOwners.TryGetValue(key, out var value4) && !(value4 != requested) && pos.x >= 0 && pos.y >= 0)
				{
					EmblemTexture.Set(texture, pos);
					texture.gameObject.SetActive(value: true);
				}
			}, refresh: true);
		}
	}

	private void Hide()
	{
		ExploreRegionNode[] array = UnityEngine.Object.FindObjectsByType<ExploreRegionNode>();
		for (int i = 0; i < array.Length; i++)
		{
			Transform transform = array[i].transform.Find("PillarClanEmblem");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
			}
		}
		_badgeOwners.Clear();
		_badgeRetry.Clear();
		_flagRetry.Clear();
		if (Singleton<MapIndicators>.HasInstance())
		{
			foreach (string flag in _flags)
			{
				MapIndicators.Remove(flag, IndicatorType.ClanFlag);
			}
		}
		_flags.Clear();
		if (Singleton<MapIndicators>.HasInstance())
		{
			foreach (string gate in _gates)
			{
				MapIndicators.Remove(gate, IndicatorType.Artifact);
			}
		}
		_gates.Clear();
	}

	public static bool Handles(string[] args)
	{
		if (args.Length > 1)
		{
			return args[1] == "pillar";
		}
		return false;
	}

	public static IEnumerator Command(string[] args, TestBridge.Result result)
	{
		Attach();
		if (args.Length > 2 && args[2] == "siege")
		{
			yield return PillarSiegeClient.Command(args, result);
			yield break;
		}
		ClanPillarClient instance = _instance;
		if (args.Length > 2 && args[2] == "fetch")
		{
			instance._queued = true;
			instance._lastAttempt = -100f;
		}
		if (args.Length > 3 && args[2] == "combat")
		{
			Artifact artifact = Singleton<ArtifactManager>.Instance().Find(args[3]);
			if (artifact == null || InteractionSystem.CombatTargetObjectFilter(artifact.gameObject) == null)
			{
				result.Fail("native combat target filter rejected artifact");
				yield break;
			}
			CombatSystem combatSystem = GameSystem<CombatSystem>.Instance();
			combatSystem.CombatMode = true;
			combatSystem.SelectTarget(artifact.EntityId);
			result.Set("target", (combatSystem.Target.Value == null) ? null : combatSystem.Target.Value.GetEntityId());
		}
		result.Set("source", instance.Source()).Set("status", instance._status).Set("diagnostic", instance._diagnostic)
			.Set("valid", instance._valid)
			.Set("etag", instance._etag)
			.Set("requests", instance._requests)
			.Set("pillars", instance._pillars.Values.Select((Pillar p) => new Dictionary<string, object>
			{
				{ "region", p.RegionId },
				{ "clan", p.ClanId },
				{ "name", p.ClanName }
			}).ToArray())
			.Set("flags", instance._flags.ToArray())
			.Set("badges", instance._badgeOwners.Select((KeyValuePair<ExploreRegionNode, string> b) => new Dictionary<string, object>
			{
				{
					"region",
					(b.Key == null) ? null : b.Key.Route.RegionId
				},
				{ "clan", b.Value }
			}).ToArray())
			.Set("routeNodes", (from n in UnityEngine.Object.FindObjectsByType<ExploreRegionNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				where !string.IsNullOrEmpty(n.Route.RegionId)
				select new Dictionary<string, object>
				{
					{
						"region",
						n.Route.RegionId
					},
					{
						"template",
						RegionOf(n.Route).TemplateId
					},
					{
						"pillar",
						IsPillarRoute(n.Route)
					},
					{
						"parent",
						(n.transform.parent == null) ? null : n.transform.parent.name
					},
					{
						"active",
						n.gameObject.activeInHierarchy
					},
					{
						"x",
						n.transform.localPosition.x
					},
					{
						"y",
						n.transform.localPosition.y
					}
				}).ToArray());
		yield return null;
	}
}

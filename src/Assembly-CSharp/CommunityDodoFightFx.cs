using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.Render.Particle;
using Durango.UI.InGame;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CommunityDodoFightFx : MonoBehaviour
{
	private sealed class Aura
	{
		public string Match = string.Empty;

		public string Entity = string.Empty;

		public int Color;

		public FillBorderAlert Alert;

		public AnimalBehavior Animal;

		public float Radius;

		public string How = string.Empty;

		public float Height;

		public float NextLookAt;

		public float PulseUntil;

		public bool Pulsing;

		public string Tint = string.Empty;

		public bool TintApplied;
	}

	public sealed class Step
	{
		public string Path = string.Empty;

		public bool OnTarget;

		public bool Body = true;

		public float Delay;

		public float Stop = 3f;

		public float Scale;
	}

	private sealed class Pending
	{
		public float At;

		public Action Run;
	}

	private const float Lifted = 15f;

	private const float Span = 100000f;

	private const float Coverage = 0.78f;

	private const float PulseSec = 1.2f;

	private const float DefaultStop = 3f;

	private const float HitDelay = 0.4f;

	private static readonly Color[] AuraColors = new Color[2]
	{
		new Color(1f, 0.24f, 0.2f),
		new Color(0.25f, 0.55f, 1f)
	};

	private const string Fx = "Particle/";

	private const string Activate = "Particle/FX_SkillActivated_Common_01.prefab";

	private const string ActivateLarge = "Particle/FX_SkillActivated_Common_01_Large.prefab";

	private static readonly Dictionary<string, Step[]> Table = new Dictionary<string, Step[]>(StringComparer.Ordinal)
	{
		{
			"peck_storm",
			new Step[3]
			{
				Hit("FX_CriticalFlash_Stab_M", 0f),
				Hit("FX_CriticalFlash_Stab_M", 0.22f),
				Hit("FX_CriticalFlash_Stab_M", 0.44f)
			}
		},
		{
			"peck_pierce",
			new Step[1] { Hit("FX_Attack_Penetration_01", 0f) }
		},
		{
			"peck_double",
			new Step[2]
			{
				Hit("FX_CriticalFlash_Stab_M", 0f),
				Hit("FX_CriticalFlash_Stab_M", 0.22f)
			}
		},
		{
			"peck_hard",
			new Step[1] { Hit("FX_CriticalFlash_Stab_M", 0f) }
		},
		{
			"meteor",
			new Step[2]
			{
				Self("FX_Rush_S_01", body: false),
				Ground("FX_Dust_Impact_XL_01", 0.55f)
			}
		},
		{
			"rend",
			new Step[1] { Hit("FX_CriticalFlash_Slash_01", 0f) }
		},
		{
			"headbutt",
			new Step[1] { Hit("FX_CriticalFlash_Strike_01", 0f) }
		},
		{
			"dropkick",
			new Step[1] { Hit("FX_Barehand_Attack_Kick_A", 0f) }
		},
		{
			"primal_armor",
			new Step[1] { Self("FX_Login_Shield", body: false) }
		},
		{
			"counter",
			new Step[1] { Self("FX_CrossCounter", body: true) }
		},
		{
			"wall",
			new Step[1] { Self("FX_Barrier_01", body: false, 1.5f) }
		},
		{
			"fluff_up",
			new Step[1] { Self("FX_SkillActivated_Common_02", body: false) }
		},
		{
			"evade",
			new Step[1] { Self("FX_SkillActivated_Common_02", body: false) }
		},
		{
			"thorns",
			new Step[1] { Self("FX_Flash_Parried_01", body: true) }
		},
		{
			"lick",
			new Step[1] { Self("FX_Condition_HealthUp_01", body: false, 1.5f) }
		},
		{
			"roar",
			new Step[1] { Self("FX_Provocation_01", body: false) }
		}
	};

	private static readonly HashSet<string> AttackSkills = new HashSet<string>(StringComparer.Ordinal) { "peck_storm", "peck_pierce", "peck_double", "peck_hard", "meteor", "rend", "headbutt", "dropkick" };

	private static readonly Step[] UnknownAttack = new Step[1] { Hit("FX_CriticalFlash_Strike_01", 0f) };

	private static readonly Step[] UnknownDefend = new Step[1] { Self("FX_SkillActivated_Common_02", body: false) };

	private static readonly Color[] PickColors = new Color[2]
	{
		new Color(1f, 0.82f, 0.3f),
		new Color(1f, 0.3f, 0.25f)
	};

	private const float PickRadius = 260f;

	private static readonly FillBorderAlert[] _pick = new FillBorderAlert[2];

	private static readonly Dictionary<string, Aura> _auras = new Dictionary<string, Aura>(StringComparer.Ordinal);

	private static readonly List<FillBorderAlert>[] _free = new List<FillBorderAlert>[2]
	{
		new List<FillBorderAlert>(),
		new List<FillBorderAlert>()
	};

	private static readonly List<Pending> _pending = new List<Pending>();

	private static readonly HashSet<string> _cached = new HashSet<string>(StringComparer.Ordinal);

	private static CommunityDodoFightFx _instance;

	private static Texture2D _borderTexture;

	public static float RadiusMul = 1f;

	public static string LastPlayed = string.Empty;

	public static int Played { get; private set; }

	private static Step Hit(string name, float extraDelay)
	{
		return new Step
		{
			Path = "Particle/" + name + ".prefab",
			OnTarget = true,
			Body = true,
			Delay = 0.4f + extraDelay
		};
	}

	private static Step Ground(string name, float scale)
	{
		return new Step
		{
			Path = "Particle/" + name + ".prefab",
			OnTarget = true,
			Body = false,
			Delay = 0.4f,
			Scale = scale
		};
	}

	private static Step Self(string name, bool body, float stop = 3f)
	{
		return new Step
		{
			Path = "Particle/" + name + ".prefab",
			OnTarget = false,
			Body = body,
			Stop = stop
		};
	}

	private static CommunityDodoFightFx Instance()
	{
		if (_instance == null)
		{
			GameObject obj = new GameObject("CommunityDodoFightFx");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			_instance = obj.AddComponent<CommunityDodoFightFx>();
		}
		return _instance;
	}

	private static AnimalBehavior Find(string entity)
	{
		if (string.IsNullOrEmpty(entity) || !Singleton<AnimalManager>.HasInstance())
		{
			return null;
		}
		return Singleton<AnimalManager>.Instance().GetAnimal(entity);
	}

	public static void SetMatch(string matchId, string[] entities, string[] auras, string[] tints = null)
	{
		for (int i = 0; i < 2; i++)
		{
			string text = ((entities != null && i < entities.Length) ? entities[i] : null);
			if (!string.IsNullOrEmpty(text))
			{
				int num;
				switch ((auras != null && i < auras.Length && auras[i] != null) ? auras[i].Trim().ToLowerInvariant() : string.Empty)
				{
				case "none":
				case "off":
					Remove(text);
					continue;
				default:
					num = i;
					break;
				case "blue":
					num = 1;
					break;
				case "red":
					num = 0;
					break;
				}
				int num2 = num;
				if (_auras.TryGetValue(text, out var value) && value.Color != num2)
				{
					Remove(text);
					value = null;
				}
				if (value == null)
				{
					value = new Aura();
					value.Entity = text;
					value.Color = num2;
					_auras[text] = value;
					Instance();
				}
				value.Match = matchId ?? string.Empty;
				string text2 = ((tints != null && i < tints.Length && tints[i] != null) ? tints[i] : string.Empty);
				if (text2 != value.Tint)
				{
					value.Tint = text2;
					value.TintApplied = false;
				}
			}
		}
	}

	public static void ClearMatch(string matchId)
	{
		List<string> list = null;
		foreach (KeyValuePair<string, Aura> aura in _auras)
		{
			if (aura.Value.Match == matchId)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(aura.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			Remove(item);
		}
	}

	public static void ClearAll()
	{
		foreach (string item in new List<string>(_auras.Keys))
		{
			Remove(item);
		}
		_pending.Clear();
	}

	private static void Remove(string entity)
	{
		if (_auras.TryGetValue(entity, out var value))
		{
			_auras.Remove(entity);
			Release(value);
		}
	}

	private static void Release(Aura a)
	{
		if (a.Alert != null)
		{
			a.Alert.Stop(0f);
			_free[a.Color].Add(a.Alert);
			a.Alert = null;
		}
		a.Animal = null;
	}

	private static Texture2D BorderTexture()
	{
		if (_borderTexture == null)
		{
			_borderTexture = new Texture2D(16, 1, TextureFormat.RGBA32, mipChain: false);
			_borderTexture.wrapMode = TextureWrapMode.Clamp;
			for (int i = 0; i < 16; i++)
			{
				float num = ((float)i + 0.5f) / 16f;
				_borderTexture.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Lerp(0.2f, 1f, num * num)));
			}
			_borderTexture.Apply();
		}
		return _borderTexture;
	}

	private FillBorderAlert TakeAlert(int color)
	{
		List<FillBorderAlert> list = _free[color];
		while (list.Count > 0)
		{
			FillBorderAlert fillBorderAlert = list[list.Count - 1];
			list.RemoveAt(list.Count - 1);
			if (fillBorderAlert != null)
			{
				return fillBorderAlert;
			}
		}
		GameObject gameObject = new GameObject("dodoAura" + color);
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		FillBorderAlert fillBorderAlert2 = gameObject.AddComponent<FillBorderAlert>();
		Color color2 = AuraColors[color];
		fillBorderAlert2.Init(new Color(color2.r, color2.g, color2.b, 0.2f), new Color(color2.r, color2.g, color2.b, 0.95f), BorderTexture());
		gameObject.SetActive(value: false);
		return fillBorderAlert2;
	}

	private static float Measure(AnimalBehavior a, out string how, out float height)
	{
		bool flag = false;
		Bounds bounds = default(Bounds);
		SkinnedMeshRenderer[] componentsInChildren = a.GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			if (skinnedMeshRenderer.enabled)
			{
				if (!flag)
				{
					bounds = skinnedMeshRenderer.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(skinnedMeshRenderer.bounds);
				}
			}
		}
		float num = (flag ? (Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.75f) : 0f);
		height = (flag ? Mathf.Max(40f, bounds.max.y - a.transform.position.y) : (150f * Mathf.Abs(a.transform.lossyScale.y)));
		float num2 = Mathf.Max(a.XRadius, a.YRadius) * Mathf.Abs(a.transform.lossyScale.x);
		float num3 = num;
		if (num2 > 1f)
		{
			num3 = ((num > 1f) ? Mathf.Min(num, num2 * 2.5f + 60f) : (num2 * 1.3f));
		}
		how = "mesh " + Mathf.RoundToInt(num) + " body " + Mathf.RoundToInt(num2) + " scale " + a.transform.lossyScale.x.ToString("0.##", CultureInfo.InvariantCulture);
		return Mathf.Clamp(num3 + 30f, 90f, 420f);
	}

	private static void ShowAlert(Aura a, bool full, bool fresh)
	{
		float num = AreaOfEffectVisualizer.Now();
		float num2 = (full ? 1.02f : 0.78f);
		a.Alert.Show(num - num2 * 100000f, num + (1f - num2) * 100000f, fresh ? num : (num - 1f), num + 100000f);
		a.Pulsing = full;
	}

	private void LateUpdate()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_pending.Count > 0)
		{
			List<Pending> list = null;
			for (int num = _pending.Count - 1; num >= 0; num--)
			{
				if (_pending[num].At <= realtimeSinceStartup)
				{
					if (list == null)
					{
						list = new List<Pending>();
					}
					list.Add(_pending[num]);
					_pending.RemoveAt(num);
				}
			}
			if (list != null)
			{
				for (int num2 = list.Count - 1; num2 >= 0; num2--)
				{
					try
					{
						list[num2].Run();
					}
					catch (Exception exception)
					{
						UnityEngine.Debug.LogException(exception);
					}
				}
			}
		}
		foreach (Aura value in _auras.Values)
		{
			if (value.Animal == null)
			{
				if (value.Alert != null && value.Alert.gameObject.activeSelf)
				{
					value.Alert.Stop(0f);
				}
				if (realtimeSinceStartup < value.NextLookAt)
				{
					continue;
				}
				value.NextLookAt = realtimeSinceStartup + 0.25f;
				AnimalBehavior animalBehavior = Find(value.Entity);
				if (animalBehavior == null)
				{
					continue;
				}
				value.Animal = animalBehavior;
				value.Radius = Measure(animalBehavior, out value.How, out value.Height);
				if (value.Alert == null)
				{
					value.Alert = TakeAlert(value.Color);
				}
				value.Alert.SetArc(animalBehavior.transform.position + Vector3.up * 15f, value.Radius * RadiusMul, 0f, 360f);
				ShowAlert(value, realtimeSinceStartup < value.PulseUntil, fresh: true);
				value.TintApplied = false;
			}
			Vector3 position = value.Animal.transform.position + Vector3.up * 15f;
			value.Alert.transform.position = position;
			bool flag = realtimeSinceStartup < value.PulseUntil;
			if (flag != value.Pulsing)
			{
				ShowAlert(value, flag, fresh: false);
			}
		}
	}

	public static bool TopOf(string entity, out Vector3 top)
	{
		top = Vector3.zero;
		if (string.IsNullOrEmpty(entity))
		{
			return false;
		}
		if (_auras.TryGetValue(entity, out var value) && value.Animal != null)
		{
			top = value.Animal.transform.position + Vector3.up * value.Height;
			return true;
		}
		AnimalBehavior animalBehavior = Find(entity);
		if (animalBehavior == null)
		{
			return false;
		}
		Measure(animalBehavior, out var _, out var height);
		top = animalBehavior.transform.position + Vector3.up * height;
		return true;
	}

	public static void ShowPick(Vector3 world, bool valid)
	{
		CommunityDodoFightFx communityDodoFightFx = Instance();
		int num = ((!valid) ? 1 : 0);
		for (int i = 0; i < 2; i++)
		{
			if (i != num && _pick[i] != null && _pick[i].gameObject.activeSelf)
			{
				_pick[i].Stop(0f);
			}
		}
		if (_pick[num] == null)
		{
			GameObject gameObject = new GameObject("dodoPick" + num);
			gameObject.transform.SetParent(communityDodoFightFx.transform, worldPositionStays: false);
			_pick[num] = gameObject.AddComponent<FillBorderAlert>();
			Color color = PickColors[num];
			_pick[num].Init(new Color(color.r, color.g, color.b, 0.22f), new Color(color.r, color.g, color.b, 0.95f), BorderTexture());
			gameObject.SetActive(value: false);
		}
		Vector3 position = world + Vector3.up * 15f;
		if (!_pick[num].gameObject.activeSelf)
		{
			_pick[num].SetArc(position, 260f, 0f, 360f);
			float num2 = AreaOfEffectVisualizer.Now();
			_pick[num].Show(num2 - 60000.004f, num2 + 40000f, num2, num2 + 100000f);
		}
		_pick[num].transform.position = position;
	}

	public static void PlayDodge(string entity)
	{
		Emit(Find(entity), "Particle/FX_Rush_S_01.prefab", body: false, 1.5f, 0f);
	}

	public static string SkillIcon(string skill, string line)
	{
		switch (skill)
		{
		case "peck_storm":
			return "action_attack_ratio";
		case "peck_pierce":
			return "action_onehand_smash";
		case "peck_double":
		case "peck_hard":
			return "action_attack";
		case "meteor":
		case "headbutt":
			return "action_tackle";
		case "rend":
			return "action_twohand_strike";
		case "dropkick":
			return "action_kick";
		case "primal_armor":
			return "action_defense";
		case "fluff_up":
		case "counter":
			return "action_defense_ratio";
		case "thorns":
		case "wall":
			return "action_guard";
		case "evade":
			return "action_evade";
		case "lick":
			return "action_master_energetic";
		case "roar":
			return "action_taunt";
		default:
			if (!(line == "defend"))
			{
				return "action_attack";
			}
			return "action_guard";
		}
	}

	public static void HidePick()
	{
		for (int i = 0; i < 2; i++)
		{
			if (_pick[i] != null && _pick[i].gameObject.activeSelf)
			{
				_pick[i].Stop(0f);
			}
		}
	}

	public static int PlaySkill(string skill, string line, string entity, string target, JArray fx)
	{
		Step[] array = ((fx != null) ? ParseSteps(fx) : null);
		if (array == null)
		{
			array = (Table.TryGetValue(skill ?? string.Empty, out var value) ? value : ((line == "defend") ? UnknownDefend : UnknownAttack));
		}
		if (!string.IsNullOrEmpty(entity) && _auras.TryGetValue(entity, out var value2))
		{
			value2.PulseUntil = Time.realtimeSinceStartup + 1.2f;
		}
		int num = 0;
		if (line != "defend" && fx == null)
		{
			AnimalBehavior animalBehavior = Find(entity);
			if (animalBehavior != null && Emit(animalBehavior, (Mathf.Min(animalBehavior.XRadius, animalBehavior.YRadius) >= 100f) ? "Particle/FX_SkillActivated_Common_01_Large.prefab" : "Particle/FX_SkillActivated_Common_01.prefab", body: false, 3f, 0f))
			{
				num++;
			}
		}
		Step[] array2 = array;
		foreach (Step step in array2)
		{
			Step s = step;
			string who = (s.OnTarget ? target : entity);
			if (s.Delay <= 0f)
			{
				if (Emit(Find(who), s.Path, s.Body, s.Stop, s.Scale))
				{
					num++;
				}
				continue;
			}
			Instance();
			_pending.Add(new Pending
			{
				At = Time.realtimeSinceStartup + s.Delay,
				Run = delegate
				{
					Emit(Find(who), s.Path, s.Body, s.Stop, s.Scale);
				}
			});
			num++;
		}
		Played++;
		LastPlayed = skill + " " + line + " " + entity + " -> " + target + " steps " + array.Length;
		return num;
	}

	private static bool Emit(AnimalBehavior a, string path, bool body, float stop, float scale)
	{
		if (a == null || string.IsNullOrEmpty(path) || !Singleton<ParticleManager>.HasInstance())
		{
			return false;
		}
		Transform transform = (body ? a.Bip001Transform : null);
		if (transform == null)
		{
			transform = a.transform;
		}
		int id = ParticleManager.EmitFollow(path, Vector3.zero, Quaternion.identity, transform, useLocalPosition: true, comeForwardToCamera: false, groundDecal: false, (scale > 0f) ? (Vector3.one * scale) : default(Vector3));
		if (stop > 0f)
		{
			Instance();
			_pending.Add(new Pending
			{
				At = Time.realtimeSinceStartup + stop,
				Run = delegate
				{
					ParticleManager.Stop(id, immediately: false);
				}
			});
		}
		return true;
	}

	public static void Preload(IEnumerable<string> skills)
	{
		if (skills == null)
		{
			return;
		}
		Cache("Particle/FX_SkillActivated_Common_01.prefab");
		Cache("Particle/FX_SkillActivated_Common_01_Large.prefab");
		foreach (string skill in skills)
		{
			if (skill != null && Table.TryGetValue(skill, out var value))
			{
				Step[] array = value;
				for (int i = 0; i < array.Length; i++)
				{
					Cache(array[i].Path);
				}
			}
		}
	}

	private static void Cache(string path)
	{
		if (!string.IsNullOrEmpty(path) && Singleton<ParticleManager>.HasInstance() && _cached.Add(path))
		{
			ParticleManager.Cache(path);
		}
	}

	private static Step[] ParseSteps(JArray fx)
	{
		List<Step> list = new List<Step>();
		foreach (JToken item in (IEnumerable<JToken>)fx)
		{
			if (!(item is JObject o))
			{
				continue;
			}
			string text = CommunityDodoFight.Str(o, "path");
			if (text.Length != 0)
			{
				if (text.IndexOf('/') < 0)
				{
					text = "Particle/" + text + (text.EndsWith(".prefab", StringComparison.Ordinal) ? string.Empty : ".prefab");
				}
				list.Add(new Step
				{
					Path = text,
					OnTarget = (CommunityDodoFight.Str(o, "on") == "target"),
					Body = (CommunityDodoFight.Str(o, "at") != "ground"),
					Delay = Mathf.Clamp((float)CommunityDodoFight.Num(o, "delay", 0.0), 0f, 10f),
					Stop = Mathf.Clamp((float)CommunityDodoFight.Num(o, "stop", 3.0), 0f, 30f),
					Scale = Mathf.Clamp((float)CommunityDodoFight.Num(o, "scale", 0.0), 0f, 20f)
				});
			}
		}
		return list.ToArray();
	}

	public static Dictionary<string, object> DebugState()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		List<object> list = new List<object>();
		foreach (Aura value in _auras.Values)
		{
			list.Add(value.Entity + " " + ((value.Color == 0) ? "red" : "blue") + " match " + value.Match + ((value.Animal != null) ? (" found r " + Mathf.RoundToInt(value.Radius * RadiusMul) + " (" + value.How + ") shown " + (value.Alert != null && value.Alert.gameObject.activeSelf)) : " waiting"));
		}
		dictionary["auras"] = list;
		dictionary["radiusMul"] = RadiusMul;
		dictionary["played"] = Played;
		dictionary["lastPlayed"] = LastPlayed;
		dictionary["pending"] = _pending.Count;
		return dictionary;
	}

	public static string DebugCommand(string[] args, string defaultSelf, string defaultTarget)
	{
		switch (args[0])
		{
		case "fx":
		{
			if (args.Length < 2)
			{
				return "usage: dodo fx <skill> [entity] [target] — skills: " + string.Join(" ", new List<string>(Table.Keys).ToArray());
			}
			string text = ((args.Length > 2) ? args[2] : defaultSelf);
			string text2 = ((args.Length > 3) ? args[3] : defaultTarget);
			string line = (AttackSkills.Contains(args[1]) ? "attack" : "defend");
			return "fx " + args[1] + " " + text + " -> " + text2 + " emitted " + PlaySkill(args[1], line, text, text2, null) + " (self found " + (Find(text) != null) + " · target found " + (Find(text2) != null) + ")";
		}
		case "fxpath":
		{
			if (args.Length < 3)
			{
				return "usage: dodo fxpath <path|FX_name> <entity> [body|ground] [stop sec]";
			}
			string text3 = args[1];
			if (text3.IndexOf('/') < 0)
			{
				text3 = "Particle/" + text3 + (text3.EndsWith(".prefab", StringComparison.Ordinal) ? string.Empty : ".prefab");
			}
			if (args.Length <= 4 || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				result2 = 3f;
			}
			return "fxpath " + text3 + " on " + args[2] + " " + Emit(Find(args[2]), text3, args.Length <= 3 || args[3] != "ground", result2, 0f);
		}
		case "aura":
		{
			if (args.Length >= 2 && args[1] == "clear")
			{
				ClearAll();
				return "auras cleared";
			}
			if (args.Length >= 3 && args[1] == "mul" && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				RadiusMul = Mathf.Clamp(result, 0.2f, 5f);
				foreach (Aura value in _auras.Values)
				{
					value.Animal = null;
				}
				return "aura radius × " + RadiusMul;
			}
			if (args.Length < 3)
			{
				return "usage: dodo aura <entity> <red|blue|none> | clear | mul <x>";
			}
			SetMatch("test", new string[1] { args[1] }, new string[1] { args[2] });
			return "aura " + args[1] + " " + args[2];
		}
		default:
			return null;
		}
	}
}

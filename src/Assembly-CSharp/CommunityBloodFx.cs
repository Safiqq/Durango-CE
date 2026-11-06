using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.System.Config;
using Durango.Terrain;
using Durango.Utils;
using Messages;
using Shared.Battle;
using UnityEngine;
using UnityEngine.Rendering;

public class CommunityBloodFx : MonoBehaviour
{
	private struct Drop
	{
		public Vector3 Pos;

		public Vector3 Vel;

		public float Size;

		public float Age;

		public float Life;

		public float GroundY;

		public float StainSize;

		public bool Mist;

		public float Shade;
	}

	private struct Stain
	{
		public Vector3 Pos;

		public float Size;

		public float Rot;

		public float Age;

		public float Delay;

		public int Variant;

		public float Shade;

		public float Dying;
	}

	public static float StainLife = 180f;

	public static float StainFadeFrom = 165f;

	public static float AmountMul = 1f;

	private const float Gravity = 1700f;

	private const float StainLift = 0.8f;

	private const float CullDistance = 40000f;

	private static Color DropColor = new Color(0.72f, 0.03f, 0.04f, 1f);

	private static Color MistColor = new Color(0.75f, 0.05f, 0.06f, 0.5f);

	private static Color StainColor = new Color(0.52f, 0.02f, 0.03f, 0.95f);

	private static bool _enabled = true;

	public static float Mul = 1f;

	public static string LastHit = string.Empty;

	private const float CapFade = 1.5f;

	private static Shader _shader;

	private static Texture2D _dropTex;

	private static Texture2D _stainTex;

	private static Material _dropMat;

	private static Material _stainMat;

	private static CommunityBloodFx _inst;

	private readonly List<Drop> _drops = new List<Drop>();

	private readonly List<Stain> _stains = new List<Stain>();

	private Mesh _dropMesh;

	private Mesh _stainMesh;

	private readonly List<Vector3> _v = new List<Vector3>();

	private readonly List<Vector2> _uv = new List<Vector2>();

	private readonly List<Color> _c = new List<Color>();

	private readonly List<int> _t = new List<int>();

	private Camera _cam;

	public static int Hits { get; private set; }

	public static int DropsSpawned { get; private set; }

	public static int StainsSpawned { get; private set; }

	private static int MaxDrops
	{
		get
		{
			if (!Application.isMobilePlatform)
			{
				return 700;
			}
			return 360;
		}
	}

	private static int MaxStains
	{
		get
		{
			if (!Application.isMobilePlatform)
			{
				return 500;
			}
			return 220;
		}
	}

	public static bool Enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			_enabled = value;
			if (!value)
			{
				Clear();
			}
		}
	}

	public static int LiveDrops
	{
		get
		{
			if (!(_inst != null))
			{
				return 0;
			}
			return _inst._drops.Count;
		}
	}

	public static int LiveStains
	{
		get
		{
			if (!(_inst != null))
			{
				return 0;
			}
			return _inst._stains.Count;
		}
	}

	public static void SetStainSeconds(float seconds)
	{
		StainLife = Mathf.Clamp(seconds, 5f, 900f);
		StainFadeFrom = StainLife - Mathf.Min(15f, StainLife * 0.25f);
	}

	public static void SetColor(string value)
	{
		switch (value)
		{
		case "green":
			DropColor = new Color(0.25f, 0.62f, 0.08f, 1f);
			MistColor = new Color(0.35f, 0.7f, 0.12f, 0.5f);
			StainColor = new Color(0.18f, 0.42f, 0.05f, 0.95f);
			break;
		case "blue":
			DropColor = new Color(0.1f, 0.3f, 0.85f, 1f);
			MistColor = new Color(0.2f, 0.4f, 0.9f, 0.5f);
			StainColor = new Color(0.06f, 0.18f, 0.55f, 0.95f);
			break;
		case "purple":
			DropColor = new Color(0.5f, 0.1f, 0.75f, 1f);
			MistColor = new Color(0.6f, 0.2f, 0.85f, 0.5f);
			StainColor = new Color(0.32f, 0.06f, 0.48f, 0.95f);
			break;
		case "black":
			DropColor = new Color(0.08f, 0.07f, 0.07f, 1f);
			MistColor = new Color(0.15f, 0.13f, 0.13f, 0.45f);
			StainColor = new Color(0.05f, 0.04f, 0.04f, 0.9f);
			break;
		default:
			DropColor = new Color(0.72f, 0.03f, 0.04f, 1f);
			MistColor = new Color(0.75f, 0.05f, 0.06f, 0.5f);
			StainColor = new Color(0.52f, 0.02f, 0.03f, 0.95f);
			break;
		}
	}

	public static void SetAmount(string value)
	{
		switch (value)
		{
		case "low":
			AmountMul = 0.5f;
			break;
		case "high":
			AmountMul = 1.6f;
			break;
		case "max":
			AmountMul = 2.4f;
			break;
		default:
			AmountMul = 1f;
			break;
		}
	}

	public static void OnHit(CharacterBehavior victim, Damage damage, DamageableEntity attacker, Vector3 hitPos, bool customHitEffect)
	{
		if (!_enabled || victim == null || damage.Value <= 0 || damage.Result != DamageResult.Hit || customHitEffect || attacker == null || !victim.isActiveAndEnabled)
		{
			return;
		}
		float num = AttackTypeMul(damage.AttackType);
		if (num <= 0f)
		{
			return;
		}
		try
		{
			float num2 = ((victim.Life != null) ? victim.Life.Max() : 0f);
			float intensity = Intensity((num2 > 0f) ? ((float)damage.Value / num2) : 0.05f, (damage.Effects & DamageEffects.Critical) != 0) * num;
			Vector3 currentPosition = attacker.GetCurrentPosition();
			Vector3 dir = hitPos - currentPosition;
			dir.y = 0f;
			float size = SizeMul(victim.Size);
			Splash(hitPos, victim.transform.position.y, dir, intensity, size);
			LastHit = damage.Value + "/" + Mathf.RoundToInt(num2) + (((damage.Effects & DamageEffects.Critical) != DamageEffects.None) ? " crit" : string.Empty) + " " + damage.AttackType.ToString() + " → i " + intensity.ToString("0.00") + " s " + size.ToString("0.00");
		}
		catch (Exception)
		{
		}
	}

	public static float Intensity(float lifeShare, bool critical)
	{
		float b = Mathf.Clamp01(0.12f + 2.4f * Mathf.Sqrt(Mathf.Max(0f, lifeShare)));
		b = Mathf.Max(0.3f, b);
		if (critical)
		{
			b = Mathf.Min(1.4f, b * 1.25f + 0.15f);
		}
		return b;
	}

	public static float SizeMul(CharacterBehavior.SizeLevel size)
	{
		return size switch
		{
			CharacterBehavior.SizeLevel.Small => 0.75f,
			CharacterBehavior.SizeLevel.Large => 1.6f,
			_ => 1f,
		};
	}

	public static float AttackTypeMul(AttackType type)
	{
		switch (type)
		{
		case AttackType.Invalid:
		case AttackType.Demolition:
			return 0f;
		case AttackType.BareHands:
		case AttackType.Blunt:
		case AttackType.Stone:
		case AttackType.SmallBody:
		case AttackType.LargeBody:
			return 0.75f;
		case AttackType.Bomb:
			return 0.85f;
		case AttackType.LargeTear:
		case AttackType.Chainsaw:
			return 1.2f;
		default:
			return 1f;
		}
	}

	public static void Splash(Vector3 hitPos, float groundY, Vector3 dir, float intensity, float size)
	{
		if (!_enabled || !EnsureAssets())
		{
			return;
		}
		CommunityBloodFx communityBloodFx = Instance();
		if (!(communityBloodFx == null))
		{
			if (communityBloodFx._cam == null)
			{
				communityBloodFx._cam = Camera.main;
			}
			if (!(communityBloodFx._cam != null) || !((communityBloodFx._cam.transform.position - hitPos).sqrMagnitude > 1.6E+09f))
			{
				communityBloodFx.Spawn(hitPos, groundY, dir, intensity, size);
				Hits++;
			}
		}
	}

	public static void Clear()
	{
		if (_inst != null)
		{
			_inst._drops.Clear();
			_inst._stains.Clear();
			_inst.Rebuild();
		}
	}

	public static void Step(float dt)
	{
		if (_inst != null)
		{
			_inst.Simulate(dt);
			_inst.Rebuild();
		}
	}

	private void Spawn(Vector3 hitPos, float groundY, Vector3 dir, float intensity, float size)
	{
		bool flag = dir.sqrMagnitude > 1f;
		Vector3 vector = (flag ? dir.normalized : Vector3.forward);
		float num = Mathf.Max(0.05f, Mul * AmountMul);
		int num2 = Mathf.RoundToInt(Mathf.Lerp(24f, 80f, Mathf.Clamp01(intensity)) * (1f + Mathf.Max(0f, intensity - 1f)) * Mathf.Sqrt(size) * num);
		float num3 = (0.5f + 0.55f * intensity) * Mathf.Sqrt(size);
		for (int i = 0; i < num2; i++)
		{
			Drop d = default(Drop);
			bool flag2 = flag && UnityEngine.Random.value < 0.75f;
			float y = (flag2 ? UnityEngine.Random.Range(-55f, 55f) : UnityEngine.Random.Range(0f, 360f));
			Vector3 vector2 = Quaternion.Euler(0f, y, 0f) * vector;
			float num4 = Mathf.Lerp(60f, 520f, Mathf.Pow(UnityEngine.Random.value, 0.7f)) * num3 * (flag2 ? 1f : 0.55f);
			d.Vel = vector2 * num4 + Vector3.up * UnityEngine.Random.Range(40f, 380f) * (0.6f + 0.5f * intensity);
			d.Pos = hitPos + UnityEngine.Random.insideUnitSphere * 6f * size;
			d.Size = Mathf.Lerp(7f, 18f, UnityEngine.Random.value * UnityEngine.Random.value) * (0.85f + 0.4f * intensity) * Mathf.Sqrt(size);
			d.Life = 3f;
			d.GroundY = groundY;
			d.StainSize = ((UnityEngine.Random.value < 0.5f) ? (d.Size * UnityEngine.Random.Range(2.4f, 4.2f)) : 0f);
			d.Shade = UnityEngine.Random.Range(0.75f, 1.1f);
			AddDrop(d);
		}
		int num5 = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, Mathf.Clamp01(intensity)) * num);
		for (int j = 0; j < num5; j++)
		{
			AddDrop(new Drop
			{
				Mist = true,
				Pos = hitPos + UnityEngine.Random.insideUnitSphere * 8f * size,
				Vel = vector * UnityEngine.Random.Range(40f, 160f) * num3 + UnityEngine.Random.insideUnitSphere * 50f + Vector3.up * 30f,
				Size = UnityEngine.Random.Range(36f, 64f) * (0.8f + 0.4f * intensity) * size,
				Life = UnityEngine.Random.Range(0.3f, 0.5f),
				GroundY = groundY - 10000f,
				Shade = UnityEngine.Random.Range(0.85f, 1.1f)
			});
		}
		if (!IsWater(hitPos))
		{
			AddStain(new Stain
			{
				Pos = new Vector3(hitPos.x, groundY, hitPos.z) + vector * UnityEngine.Random.Range(20f, 45f) * size,
				Size = Mathf.Lerp(60f, 150f, Mathf.Clamp01(intensity - 0.2f)) * size * Mathf.Sqrt(num),
				Delay = 0.22f,
				Rot = UnityEngine.Random.Range(0f, 360f),
				Variant = UnityEngine.Random.Range(0, 4),
				Shade = UnityEngine.Random.Range(0.85f, 1f)
			});
		}
	}

	private void AddDrop(Drop d)
	{
		if (_drops.Count >= MaxDrops)
		{
			_drops.RemoveAt(0);
		}
		_drops.Add(d);
		DropsSpawned++;
	}

	private void AddStain(Stain s)
	{
		_stains.Add(s);
		int num = _stains.Count - MaxStains;
		for (int i = 0; i < num && i < _stains.Count; i++)
		{
			Stain value = _stains[i];
			if (value.Dying <= 0f)
			{
				value.Dying = 1.5f;
				_stains[i] = value;
			}
		}
		while (_stains.Count > MaxStains + 80)
		{
			_stains.RemoveAt(0);
		}
		StainsSpawned++;
	}

	private static bool IsWater(Vector3 pos)
	{
		try
		{
			if (!Singleton<TerrainBase>.HasInstance() || !TerrainBase.IsPlayerInitialized)
			{
				return false;
			}
			return TerrainWater.GetWaterDepthLevel(Singleton<TerrainBase>.Instance().GetTileDepth(Util.ClientPositionToTilePosition(pos))) != TerrainWater.WaterDepthLevel.Land;
		}
		catch
		{
			return false;
		}
	}

	private void Update()
	{
		Simulate(Time.deltaTime);
	}

	private void LateUpdate()
	{
		Rebuild();
	}

	private void Simulate(float dt)
	{
		if (dt <= 0f)
		{
			return;
		}
		for (int num = _drops.Count - 1; num >= 0; num--)
		{
			Drop value = _drops[num];
			value.Age += dt;
			if (value.Mist)
			{
				value.Vel *= Mathf.Max(0f, 1f - 4f * dt);
				value.Pos += value.Vel * dt;
			}
			else
			{
				value.Vel.y -= 1700f * dt;
				value.Pos += value.Vel * dt;
				if (value.Pos.y <= value.GroundY)
				{
					if (value.StainSize > 0f && !IsWater(value.Pos))
					{
						AddStain(new Stain
						{
							Pos = new Vector3(value.Pos.x, value.GroundY, value.Pos.z),
							Size = value.StainSize,
							Rot = UnityEngine.Random.Range(0f, 360f),
							Variant = UnityEngine.Random.Range(0, 4),
							Shade = value.Shade
						});
					}
					_drops.RemoveAt(num);
					continue;
				}
			}
			if (value.Age >= value.Life)
			{
				_drops.RemoveAt(num);
			}
			else
			{
				_drops[num] = value;
			}
		}
		for (int num2 = _stains.Count - 1; num2 >= 0; num2--)
		{
			Stain value2 = _stains[num2];
			if (value2.Delay > 0f)
			{
				value2.Delay -= dt;
			}
			else
			{
				value2.Age += dt;
			}
			if (value2.Dying > 0f)
			{
				value2.Dying -= dt;
				if (value2.Dying <= 0f)
				{
					_stains.RemoveAt(num2);
					continue;
				}
			}
			if (value2.Age >= StainLife)
			{
				_stains.RemoveAt(num2);
			}
			else
			{
				_stains[num2] = value2;
			}
		}
	}

	private void Rebuild()
	{
		if (_dropMesh == null || _stainMesh == null)
		{
			return;
		}
		if (_cam == null)
		{
			_cam = Camera.main;
		}
		Vector3 vector = ((_cam != null) ? _cam.transform.forward : Vector3.forward);
		Vector3 vector2 = ((_cam != null) ? _cam.transform.up : Vector3.up);
		Vector3 vector3 = ((_cam != null) ? _cam.transform.right : Vector3.right);
		Begin();
		for (int i = 0; i < _drops.Count; i++)
		{
			Drop drop = _drops[i];
			if (drop.Mist)
			{
				float num = drop.Age / drop.Life;
				float num2 = drop.Size * Mathf.Lerp(0.6f, 2.2f, Mathf.Sqrt(num));
				Color color = MistColor * drop.Shade;
				color.a = MistColor.a * (1f - num) * Mathf.Clamp01(num * 8f);
				Quad(drop.Pos, vector3 * num2, vector2 * num2, color, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
			}
			else
			{
				Vector3 vector4 = drop.Vel - Vector3.Dot(drop.Vel, vector) * vector;
				float magnitude = vector4.magnitude;
				Vector3 vector5 = ((magnitude > 1f) ? (vector4 / magnitude) : vector2);
				Vector3 normalized = Vector3.Cross(vector5, vector).normalized;
				float num3 = drop.Size * (1f + Mathf.Min(1.8f, magnitude * 0.003f));
				Color color2 = DropColor * drop.Shade;
				color2.a = 1f;
				Quad(drop.Pos, normalized * drop.Size * 0.4f, vector5 * num3 * 0.5f, color2, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
			}
		}
		End(_dropMesh);
		Begin();
		for (int j = 0; j < _stains.Count; j++)
		{
			Stain stain = _stains[j];
			if (!(stain.Delay > 0f))
			{
				float num4 = Mathf.Lerp(0.35f, 1f, 1f - Mathf.Pow(1f - Mathf.Clamp01(stain.Age / 0.18f), 3f));
				float num5 = ((stain.Age < StainFadeFrom) ? 1f : (1f - (stain.Age - StainFadeFrom) / (StainLife - StainFadeFrom)));
				Color color3 = StainColor * stain.Shade;
				if (stain.Dying > 0f)
				{
					num5 = Mathf.Min(num5, stain.Dying / 1.5f);
				}
				color3.a = StainColor.a * Mathf.Clamp01(num5);
				float num6 = stain.Size * 0.5f * num4;
				Quaternion quaternion = Quaternion.Euler(0f, stain.Rot, 0f);
				Vector2 vector6 = new Vector2((float)(stain.Variant % 2) * 0.5f, (float)(stain.Variant / 2) * 0.5f);
				Quad(stain.Pos + Vector3.up * 0.8f, quaternion * Vector3.right * num6, quaternion * Vector3.forward * num6, color3, vector6, vector6 + new Vector2(0.5f, 0.5f));
			}
		}
		End(_stainMesh);
	}

	private void Begin()
	{
		_v.Clear();
		_uv.Clear();
		_c.Clear();
		_t.Clear();
	}

	private void Quad(Vector3 center, Vector3 halfX, Vector3 halfY, Color color, Vector2 uvMin, Vector2 uvMax)
	{
		int count = _v.Count;
		_v.Add(center - halfX - halfY);
		_v.Add(center + halfX - halfY);
		_v.Add(center + halfX + halfY);
		_v.Add(center - halfX + halfY);
		_uv.Add(new Vector2(uvMin.x, uvMin.y));
		_uv.Add(new Vector2(uvMax.x, uvMin.y));
		_uv.Add(new Vector2(uvMax.x, uvMax.y));
		_uv.Add(new Vector2(uvMin.x, uvMax.y));
		_c.Add(color);
		_c.Add(color);
		_c.Add(color);
		_c.Add(color);
		_t.Add(count);
		_t.Add(count + 2);
		_t.Add(count + 1);
		_t.Add(count);
		_t.Add(count + 3);
		_t.Add(count + 2);
	}

	private void End(Mesh mesh)
	{
		mesh.Clear();
		mesh.SetVertices(_v);
		mesh.SetUVs(0, _uv);
		mesh.SetColors(_c);
		mesh.SetTriangles(_t, 0, calculateBounds: false);
		mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000f);
	}

	private static CommunityBloodFx Instance()
	{
		if (_inst != null)
		{
			return _inst;
		}
		_inst = new GameObject("CommunityBloodFx").AddComponent<CommunityBloodFx>();
		_inst._dropMesh = _inst.Part("drops", _dropMat);
		_inst._stainMesh = _inst.Part("stains", _stainMat);
		return _inst;
	}

	private Mesh Part(string name, Material material)
	{
		GameObject obj = new GameObject(name);
		obj.transform.SetParent(base.transform, worldPositionStays: false);
		obj.layer = base.gameObject.layer;
		Mesh mesh = new Mesh();
		mesh.name = "CommunityBloodFx " + name;
		mesh.MarkDynamic();
		mesh.hideFlags = HideFlags.DontSave;
		obj.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.lightProbeUsage = LightProbeUsage.Off;
		meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
		return mesh;
	}

	private void OnDestroy()
	{
		if (_inst == this)
		{
			_inst = null;
		}
		if (_dropMesh != null)
		{
			UnityEngine.Object.Destroy(_dropMesh);
		}
		if (_stainMesh != null)
		{
			UnityEngine.Object.Destroy(_stainMesh);
		}
	}

	private static bool EnsureAssets()
	{
		if (_shader == null)
		{
			_shader = Resources.Load<Shader>("CommunityBloodFx/BloodAlpha");
			if (_shader == null || !_shader.isSupported)
			{
				_shader = null;
				_enabled = false;
				return false;
			}
		}
		if (_dropTex == null)
		{
			_dropTex = BuildDropTexture(64);
		}
		if (_stainTex == null)
		{
			_stainTex = BuildStainAtlas(128, 41);
		}
		if (_dropMat == null)
		{
			_dropMat = NewMaterial(_dropTex, 0f);
			_dropMat.renderQueue = 2995;
		}
		if (_stainMat == null)
		{
			_stainMat = NewMaterial(_stainTex, -2f);
			_stainMat.renderQueue = 2990;
		}
		return true;
	}

	private static Material NewMaterial(Texture2D tex, float offset)
	{
		Material material = new Material(_shader);
		material.mainTexture = tex;
		material.SetColor("_TintColor", Color.white);
		material.SetFloat("_Offset", offset);
		material.hideFlags = HideFlags.DontSave;
		return material;
	}

	private static Texture2D BuildDropTexture(int size)
	{
		Texture2D texture2D = new Texture2D(size * 2, size, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.hideFlags = HideFlags.DontSave;
		Color[] array = new Color[size * 2 * size];
		float num = (float)size * 0.5f;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num2 = ((float)j + 0.5f - num) / num;
				float num3 = ((float)i + 0.5f - num) / num;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				float a = 1f - Edge(0.72f, 0.95f, num4);
				float num5 = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(num2, num3), new Vector2(-0.3f, 0.32f)) / 0.28f);
				float num6 = Mathf.Lerp(1f, 0.7f, Edge(0.4f, 0.9f, num4)) + num5 * 0.35f;
				array[i * size * 2 + j] = new Color(num6, num6, num6, a);
				float a2 = Mathf.Exp((0f - num4) * num4 * 3.2f) * (1f - Edge(0.8f, 1f, num4));
				array[i * size * 2 + size + j] = new Color(1f, 1f, 1f, a2);
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return texture2D;
	}

	private static Texture2D BuildStainAtlas(int cell, int seed)
	{
		int num = cell * 2;
		Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: true);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.filterMode = FilterMode.Trilinear;
		texture2D.hideFlags = HideFlags.DontSave;
		Color[] array = new Color[num * num];
		UnityEngine.Random.State state = UnityEngine.Random.state;
		UnityEngine.Random.InitState(seed);
		for (int i = 0; i < 4; i++)
		{
			int num2 = i % 2 * cell;
			int num3 = i / 2 * cell;
			float[] array2 = new float[cell * cell];
			float num4 = UnityEngine.Random.Range(0.4f, 0.48f);
			float[] array3 = new float[4];
			float[] array4 = new float[4];
			for (int j = 0; j < 4; j++)
			{
				array3[j] = UnityEngine.Random.Range(0.03f, 0.09f) / (float)(j + 1) * 1.6f;
				array4[j] = UnityEngine.Random.Range(0f, 6.2832f);
			}
			Vector2 vector = new Vector2(UnityEngine.Random.Range(-0.06f, 0.06f), UnityEngine.Random.Range(-0.06f, 0.06f));
			for (int k = 0; k < cell; k++)
			{
				for (int l = 0; l < cell; l++)
				{
					Vector2 vector2 = new Vector2(((float)l + 0.5f) / (float)cell * 2f - 1f, ((float)k + 0.5f) / (float)cell * 2f - 1f) - vector;
					float num5 = Mathf.Atan2(vector2.y, vector2.x);
					float num6 = num4;
					for (int m = 0; m < 4; m++)
					{
						num6 += array3[m] * Mathf.Sin(num5 * (float)(m * 2 + 3) + array4[m]);
					}
					float x = vector2.magnitude / num6;
					array2[k * cell + l] = 1f - Edge(0.9f, 1.02f, x);
				}
			}
			int num7 = UnityEngine.Random.Range(10, 18);
			for (int n = 0; n < num7; n++)
			{
				float f = UnityEngine.Random.Range(0f, 6.2832f);
				float num8 = UnityEngine.Random.Range(num4 * 1.05f, 0.9f);
				Vector2 center = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num8;
				float radius = UnityEngine.Random.Range(0.02f, 0.07f) * Mathf.Lerp(1.2f, 0.5f, num8);
				Blob(array2, cell, center, radius);
			}
			int num9 = UnityEngine.Random.Range(2, 5);
			for (int num10 = 0; num10 < num9; num10++)
			{
				float f2 = UnityEngine.Random.Range(0f, 6.2832f);
				Vector2 vector3 = new Vector2(Mathf.Cos(f2), Mathf.Sin(f2));
				float num11 = UnityEngine.Random.Range(0.25f, 0.45f);
				for (float num12 = 0f; num12 <= 1f; num12 += 0.08f)
				{
					Vector2 center2 = vector + vector3 * (num4 * 0.8f + num11 * num12);
					if (center2.magnitude > 0.95f)
					{
						break;
					}
					Blob(array2, cell, center2, Mathf.Lerp(0.055f, 0.015f, num12));
				}
			}
			for (int num13 = 0; num13 < cell; num13++)
			{
				for (int num14 = 0; num14 < cell; num14++)
				{
					float num15 = Mathf.Clamp01(array2[num13 * cell + num14]);
					float t = Mathf.Clamp01(num15 * 1.4f - 0.4f);
					float num16 = Mathf.Lerp(0.7f, 1.05f, t);
					if (num13 == 0 || num14 == 0 || num13 == cell - 1 || num14 == cell - 1)
					{
						num15 = 0f;
					}
					array[(num3 + num13) * num + num2 + num14] = new Color(num16, num16, num16, num15);
				}
			}
		}
		UnityEngine.Random.state = state;
		texture2D.SetPixels(array);
		texture2D.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		return texture2D;
	}

	private static float Edge(float e0, float e1, float x)
	{
		float num = Mathf.Clamp01((x - e0) / (e1 - e0));
		return num * num * (3f - 2f * num);
	}

	private static void Blob(float[] field, int cell, Vector2 center, float radius)
	{
		int num = Mathf.Max(0, Mathf.FloorToInt((center.x - radius + 1f) * 0.5f * (float)cell) - 1);
		int num2 = Mathf.Min(cell - 1, Mathf.CeilToInt((center.x + radius + 1f) * 0.5f * (float)cell) + 1);
		int num3 = Mathf.Max(0, Mathf.FloorToInt((center.y - radius + 1f) * 0.5f * (float)cell) - 1);
		int num4 = Mathf.Min(cell - 1, Mathf.CeilToInt((center.y + radius + 1f) * 0.5f * (float)cell) + 1);
		for (int i = num3; i <= num4; i++)
		{
			for (int j = num; j <= num2; j++)
			{
				Vector2 a = new Vector2(((float)j + 0.5f) / (float)cell * 2f - 1f, ((float)i + 0.5f) / (float)cell * 2f - 1f);
				float num5 = 1f - Edge(0.8f, 1.05f, Vector2.Distance(a, center) / radius);
				if (num5 > field[i * cell + j])
				{
					field[i * cell + j] = num5;
				}
			}
		}
	}

	public static string DebugCommand(string[] args)
	{
		switch ((args.Length > 1) ? args[1].ToLowerInvariant() : "state")
		{
		case "on":
			ConfigInstance.ChangeValue("blood_fx", value: true);
			break;
		case "off":
			ConfigInstance.ChangeValue("blood_fx", value: false);
			break;
		case "mul":
		{
			if (args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				Mul = Mathf.Clamp(result2, 0.1f, 5f);
			}
			break;
		}
		case "clear":
			Clear();
			break;
		case "color":
			if (args.Length > 2)
			{
				ConfigInstance.ChangeValue("blood_color", args[2].ToLowerInvariant());
			}
			break;
		case "play":
		{
			float result = 0.8f;
			if (args.Length > 2)
			{
				float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out result);
			}
			PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
			if (localPlayer == null)
			{
				return "no local player";
			}
			Vector3 position = localPlayer.transform.position;
			Camera main = Camera.main;
			Vector3 dir = ((main != null) ? (position - main.transform.position) : Vector3.forward);
			dir.y = 0f;
			Splash(position + Vector3.up * 110f, position.y, dir, result, 1f);
			break;
		}
		}
		return "enabled " + _enabled + " · mul " + Mul + " · amount " + AmountMul + " · stain " + StainLife + "s · hits " + Hits + " · drops " + DropsSpawned + " (live " + LiveDrops + ") · stains " + StainsSpawned + " (live " + LiveStains + ") · last " + LastHit;
	}
}

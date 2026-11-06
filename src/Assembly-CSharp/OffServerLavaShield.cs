using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class OffServerLavaShield : MonoBehaviour
{
	private sealed class Piece
	{
		public Renderer Renderer;

		public Transform Transform;
	}

	public const float Length = 2.4f;

	private const float GrowEnd = 0.45f;

	private const float BreakAt = 1.85f;

	private const float WallEnd = 0.6f;

	private const float DomeHeight = 1.2f;

	private const int DomeLon = 18;

	private const int DomeLat = 7;

	private static readonly Color Orange = new Color(1f, 0.5f, 0.12f);

	private static readonly Color Violet = new Color(0.85f, 0.35f, 1f);

	private static Shader _shader;

	private static Texture2D _crack;

	private static Texture2D _runes;

	private static Texture2D _dot;

	private static Texture2D _band;

	private static Texture2D _ribbon;

	private static readonly List<OffServerLavaShield> _live = new List<OffServerLavaShield>();

	private static MaterialPropertyBlock _block;

	public static string LastPlayed = string.Empty;

	public static float SizeMul = 1f;

	private Transform _follow;

	private float _start;

	private float _radius;

	private Piece _dome;

	private Piece _top;

	private Piece _ring;

	private Piece _wall;

	private Piece _runeDisc;

	private Piece _lava;

	private readonly Piece[] _ribbons = new Piece[3];

	private readonly float[] _ribbonSpeed = new float[3] { 140f, -110f, 170f };

	private readonly Quaternion[] _ribbonTilt = new Quaternion[3];

	private ParticleSystem _sparks;

	private Mesh _domeMesh;

	private Vector3[] _domeBase;

	private Vector3[] _domeWork;

	private Vector3[] _shardVel;

	private bool _broken;

	private Camera _cam;

	public static int Played { get; private set; }

	public static int LiveCount
	{
		get
		{
			_live.RemoveAll((OffServerLavaShield x) => x == null);
			return _live.Count;
		}
	}

	public static OffServerLavaShield Play(Transform follow, Vector3 position, float radius, string why)
	{
		if (!EnsureAssets())
		{
			return null;
		}
		GameObject gameObject = new GameObject("OffServerLavaShield");
		OffServerLavaShield offServerLavaShield = gameObject.AddComponent<OffServerLavaShield>();
		offServerLavaShield._follow = follow;
		offServerLavaShield._radius = Mathf.Clamp(radius, 60f, 600f) * SizeMul;
		gameObject.transform.position = ((follow != null) ? follow.position : position);
		offServerLavaShield.Build();
		offServerLavaShield._start = Time.time;
		_live.Add(offServerLavaShield);
		Played++;
		LastPlayed = why + " r " + Mathf.RoundToInt(offServerLavaShield._radius) + " at " + gameObject.transform.position;
		return offServerLavaShield;
	}

	public static void StopAll()
	{
		foreach (OffServerLavaShield item in _live)
		{
			if (item != null)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}
		_live.Clear();
	}

	private static bool EnsureAssets()
	{
		if (_shader == null)
		{
			_shader = Resources.Load<Shader>("OffServerLavaShield/LavaShieldAdd");
			if (_shader == null || !_shader.isSupported)
			{
				_shader = null;
				return false;
			}
		}
		if (_crack == null)
		{
			_crack = BuildCrackTexture(128, 6, 17);
		}
		if (_runes == null)
		{
			_runes = BuildRuneTexture(256, 23);
		}
		if (_dot == null)
		{
			_dot = BuildDotTexture(64);
		}
		if (_band == null)
		{
			_band = BuildBandTexture(32);
		}
		if (_ribbon == null)
		{
			_ribbon = BuildRibbonTexture(128, 32);
		}
		if (_block == null)
		{
			_block = new MaterialPropertyBlock();
		}
		return true;
	}

	private static Material NewMaterial(Texture2D tex, Color tint, float boost, Vector2 scroll, float rim)
	{
		Material material = new Material(_shader);
		material.mainTexture = tex;
		material.SetColor("_TintColor", tint);
		material.SetFloat("_Boost", boost);
		material.SetVector("_Scroll", new Vector4(scroll.x, scroll.y, 0f, 0f));
		material.SetFloat("_Rim", rim);
		material.SetColor("_RimColor", new Color(1f, 0.45f, 0.1f, 1f));
		material.hideFlags = HideFlags.DontSave;
		return material;
	}

	private static Texture2D BuildCrackTexture(int size, int cells, int seed)
	{
		System.Random random = new System.Random(seed);
		Vector2[] array = new Vector2[cells * cells];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
		}
		Color[] array2 = new Color[size * size];
		Color b = new Color(1f, 0.95f, 0.7f);
		for (int j = 0; j < size; j++)
		{
			for (int k = 0; k < size; k++)
			{
				float num = ((float)k + 0.5f) / (float)size * (float)cells;
				float num2 = ((float)j + 0.5f) / (float)size * (float)cells;
				int num3 = Mathf.FloorToInt(num);
				int num4 = Mathf.FloorToInt(num2);
				float num5 = 99f;
				float num6 = 99f;
				for (int l = -1; l <= 1; l++)
				{
					for (int m = -1; m <= 1; m++)
					{
						int num7 = num3 + m;
						int num8 = num4 + l;
						Vector2 vector = array[(num8 % cells + cells) % cells * cells + (num7 % cells + cells) % cells];
						float num9 = (float)num7 + vector.x - num;
						float num10 = (float)num8 + vector.y - num2;
						float num11 = Mathf.Sqrt(num9 * num9 + num10 * num10);
						if (num11 < num5)
						{
							num6 = num5;
							num5 = num11;
						}
						else if (num11 < num6)
						{
							num6 = num11;
						}
					}
				}
				float num12 = num6 - num5;
				float num13 = Mathf.Clamp01(1f - num12 / 0.07f);
				float num14 = Mathf.Exp((0f - num12) * 9f);
				Color color = Color.Lerp(Orange, b, num13 * num13);
				float a = Mathf.Clamp01(num13 + num14 * 0.55f + 0.07f);
				array2[j * size + k] = new Color(color.r, color.g, color.b, a);
			}
		}
		return Finish(new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true), array2, TextureWrapMode.Repeat);
	}

	private static Texture2D BuildRuneTexture(int size, int seed)
	{
		System.Random random = new System.Random(seed);
		Vector4[][] array = new Vector4[22][];
		for (int i = 0; i < 22; i++)
		{
			int num = 2 + random.Next(3);
			array[i] = new Vector4[num];
			for (int j = 0; j < num; j++)
			{
				Vector2 vector = new Vector2(random.Next(3) - 1, random.Next(3) - 1);
				Vector2 vector2 = new Vector2(random.Next(3) - 1, random.Next(3) - 1);
				if (vector == vector2)
				{
					vector2.y = 0f - vector.y;
					if (vector == vector2)
					{
						vector2.x = 0f - vector.x - (float)((vector.x == 0f) ? 1 : 0);
					}
				}
				array[i][j] = new Vector4(vector.x, vector.y, vector2.x, vector2.y);
			}
		}
		Color[] array2 = new Color[size * size];
		for (int k = 0; k < size; k++)
		{
			for (int l = 0; l < size; l++)
			{
				float num2 = ((float)l + 0.5f) / (float)size * 2f - 1f;
				float num3 = ((float)k + 0.5f) / (float)size * 2f - 1f;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				float a = 0f;
				a = Mathf.Max(a, Line(num4, 0.965f, 0.012f));
				a = Mathf.Max(a, Line(num4, 0.925f, 0.006f) * 0.8f);
				a = Mathf.Max(a, Line(num4, 0.735f, 0.008f));
				a = Mathf.Max(a, Line(num4, 0.5f, 0.005f) * 0.6f);
				float num5 = Mathf.Atan2(num3, num2);
				if (num5 < 0f)
				{
					num5 += MathF.PI * 2f;
				}
				if (num4 > 0.5f && num4 < 0.735f)
				{
					float num6 = num5 / (MathF.PI * 2f) * 64f;
					float num7 = Mathf.Abs(num6 - Mathf.Round(num6));
					if (num4 > 0.68f)
					{
						a = Mathf.Max(a, Mathf.Clamp01(1f - num7 / 0.08f) * 0.7f);
					}
				}
				if (num4 > 0.747f && num4 < 0.923f)
				{
					int num8 = Mathf.FloorToInt(num5 / (MathF.PI * 2f) * 22f) % 22;
					float f = ((float)num8 + 0.5f) / 22f * MathF.PI * 2f;
					Vector2 vector3 = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
					Vector2 rhs = new Vector2(0f - vector3.y, vector3.x);
					Vector2 lhs = new Vector2(num2, num3) - vector3 * 0.835f;
					Vector2 p = new Vector2(Vector2.Dot(lhs, rhs), Vector2.Dot(lhs, vector3)) / 0.044f;
					float num9 = 9f;
					Vector4[] array3 = array[num8];
					for (int m = 0; m < array3.Length; m++)
					{
						Vector4 vector4 = array3[m];
						num9 = Mathf.Min(num9, Segment(p, new Vector2(vector4.x, vector4.y), new Vector2(vector4.z, vector4.w)));
					}
					a = Mathf.Max(a, Mathf.Clamp01(1f - (num9 - 0.12f) / 0.14f));
				}
				float num10 = ((num4 < 0.98f) ? (0.06f + 0.06f * Mathf.Clamp01((num4 - 0.4f) / 0.6f)) : 0f);
				float a2 = Mathf.Clamp01(a + num10);
				Color color = Color.Lerp(Orange, new Color(1f, 0.8f, 0.45f), a * 0.6f);
				array2[k * size + l] = new Color(color.r, color.g, color.b, a2);
			}
		}
		return Finish(new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true), array2, TextureWrapMode.Clamp);
	}

	private static float Line(float r, float at, float half)
	{
		return Mathf.Clamp01(1f - Mathf.Abs(r - at) / half);
	}

	private static float Segment(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 vector = b - a;
		float num = Mathf.Clamp01(Vector2.Dot(p - a, vector) / Mathf.Max(0.0001f, vector.sqrMagnitude));
		return (p - (a + vector * num)).magnitude;
	}

	private static Texture2D BuildDotTexture(int size)
	{
		Color[] array = new Color[size * size];
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num = ((float)j + 0.5f) / (float)size * 2f - 1f;
				float num2 = ((float)i + 0.5f) / (float)size * 2f - 1f;
				float num3 = Mathf.Clamp01(1f - Mathf.Sqrt(num * num + num2 * num2));
				array[i * size + j] = new Color(1f, 1f, 1f, num3 * num3);
			}
		}
		return Finish(new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true), array, TextureWrapMode.Clamp);
	}

	private static Texture2D BuildBandTexture(int size)
	{
		Color[] array = new Color[size * 4];
		for (int i = 0; i < size; i++)
		{
			float num = ((float)i + 0.5f) / (float)size * 2f - 1f;
			float a = Mathf.Exp((0f - num) * num * 5f);
			for (int j = 0; j < 4; j++)
			{
				array[i * 4 + j] = new Color(1f, 1f, 1f, a);
			}
		}
		return Finish(new Texture2D(4, size, TextureFormat.RGBA32, mipChain: false), array, TextureWrapMode.Clamp);
	}

	private static Texture2D BuildRibbonTexture(int w, int h)
	{
		Color[] array = new Color[w * h];
		Color b = new Color(1f, 0.85f, 1f);
		for (int i = 0; i < h; i++)
		{
			float num = ((float)i + 0.5f) / (float)h * 2f - 1f;
			float num2 = Mathf.Exp((0f - num) * num * 3.2f);
			float t = Mathf.Exp((0f - num) * num * 18f);
			for (int j = 0; j < w; j++)
			{
				float num3 = ((float)j + 0.5f) / (float)w;
				float num4 = Mathf.Pow(Mathf.Sin(num3 * MathF.PI), 0.6f) * Mathf.Lerp(0.55f, 1f, num3);
				Color color = Color.Lerp(Violet, b, t);
				array[i * w + j] = new Color(color.r, color.g, color.b, Mathf.Clamp01(num2 * num4));
			}
		}
		return Finish(new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false), array, TextureWrapMode.Clamp);
	}

	private static Texture2D Finish(Texture2D tex, Color[] px, TextureWrapMode wrap)
	{
		tex.SetPixels(px);
		tex.wrapMode = wrap;
		tex.filterMode = FilterMode.Bilinear;
		tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		tex.hideFlags = HideFlags.DontSave;
		return tex;
	}

	private Piece AddPiece(string name, Mesh mesh, Material material)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.layer = base.gameObject.layer;
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterial = material;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.lightProbeUsage = LightProbeUsage.Off;
		meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
		return new Piece
		{
			Renderer = meshRenderer,
			Transform = gameObject.transform
		};
	}

	private void Build()
	{
		float radius = _radius;
		float num = radius * 0.035f;
		_runeDisc = AddPiece("runes", Quad(radius * 1.75f, num * 0.5f, new Color(1f, 1f, 1f, 1f)), NewMaterial(_runes, Color.white, 1.25f, Vector2.zero, 0f));
		_lava = AddPiece("lava", Disc(radius * 0.97f, num, 3, 24), NewMaterial(_crack, new Color(1f, 0.35f, 0.08f), 0.85f, new Vector2(0.05f, 0.11f), 0f));
		_lava.Renderer.sharedMaterial.mainTextureScale = new Vector2(2.5f, 2.5f);
		_ring = AddPiece("ring", Annulus(radius * 0.93f, radius * 1.08f, num * 1.5f, 40), NewMaterial(_band, new Color(1f, 0.85f, 0.55f), 2.4f, Vector2.zero, 0f));
		_wall = AddPiece("wall", Wall(radius * 1f, radius * 0.55f, 32), NewMaterial(_dot, new Color(1f, 0.7f, 0.35f), 1.5f, Vector2.zero, 0f));
		_domeMesh = Dome(radius);
		_dome = AddPiece("dome", _domeMesh, NewMaterial(_crack, Color.white, 2.3f, new Vector2(0.015f, 0.04f), 1.4f));
		_dome.Renderer.sharedMaterial.mainTextureScale = new Vector2(3f, 1.2f);
		_top = AddPiece("top", Quad(radius * 0.55f, 0f, new Color(1f, 0.95f, 0.75f, 1f)), NewMaterial(_dot, new Color(1f, 0.85f, 0.5f), 2.2f, Vector2.zero, 0f));
		_top.Transform.localPosition = new Vector3(0f, radius * 0.97f * 1.2f, 0f);
		Mesh mesh = Ribbon(radius * 1.1f, radius * 0.17f, 230f, 36);
		Material material = NewMaterial(_ribbon, Color.white, 2.6f, Vector2.zero, 0f);
		float[] array = new float[3] { 18f, -24f, 32f };
		float[] array2 = new float[3] { 0.5f, 0.75f, 0.35f };
		for (int i = 0; i < 3; i++)
		{
			_ribbons[i] = AddPiece("ribbon" + i, mesh, material);
			_ribbons[i].Transform.localPosition = new Vector3(0f, radius * array2[i], 0f);
			_ribbonTilt[i] = Quaternion.Euler(array[i], 0f, array[i] * 0.5f);
			_ribbons[i].Transform.localRotation = Quaternion.Euler(0f, (float)i * 120f, 0f) * _ribbonTilt[i];
		}
		_sparks = BuildSparks(radius);
		SetFade(0f);
	}

	private ParticleSystem BuildSparks(float radius)
	{
		GameObject gameObject = new GameObject("sparks");
		gameObject.layer = base.gameObject.layer;
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
		ParticleSystem particleSystem = gameObject.AddComponent<ParticleSystem>();
		particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		ParticleSystem.MainModule main = particleSystem.main;
		main.duration = 1.85f;
		main.loop = false;
		main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
		main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.6f, radius * 1.6f);
		main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.04f, radius * 0.09f);
		main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.08f));
		main.gravityModifier = 0f;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.maxParticles = 40;
		main.playOnAwake = false;
		main.scalingMode = ParticleSystemScalingMode.Hierarchy;
		ParticleSystem.EmissionModule emission = particleSystem.emission;
		emission.rateOverTime = 14f;
		emission.SetBursts(new ParticleSystem.Burst[1]
		{
			new ParticleSystem.Burst(0.05f, 10)
		});
		ParticleSystem.ShapeModule shape = particleSystem.shape;
		shape.shapeType = ParticleSystemShapeType.Cone;
		shape.angle = 18f;
		shape.radius = radius * 0.9f;
		shape.radiusThickness = 0.3f;
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f)
		}, new GradientAlphaKey[3]
		{
			new GradientAlphaKey(0f, 0f),
			new GradientAlphaKey(1f, 0.15f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
		sizeOverLifetime.enabled = true;
		sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		component.renderMode = ParticleSystemRenderMode.Billboard;
		component.sharedMaterial = NewMaterial(_dot, Color.white, 2f, Vector2.zero, 0f);
		component.shadowCastingMode = ShadowCastingMode.Off;
		component.receiveShadows = false;
		return particleSystem;
	}

	private static Mesh Quad(float half, float lift, Color color)
	{
		Mesh mesh = new Mesh();
		mesh.vertices = new Vector3[4]
		{
			new Vector3(0f - half, lift, 0f - half),
			new Vector3(half, lift, 0f - half),
			new Vector3(half, lift, half),
			new Vector3(0f - half, lift, half)
		};
		mesh.uv = new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(0f, 1f)
		};
		mesh.colors = new Color[4] { color, color, color, color };
		mesh.normals = new Vector3[4]
		{
			Vector3.up,
			Vector3.up,
			Vector3.up,
			Vector3.up
		};
		mesh.triangles = new int[6] { 0, 2, 1, 0, 3, 2 };
		return mesh;
	}

	private static Mesh Disc(float radius, float lift, int rings, int segs)
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<Color> list3 = new List<Color>();
		List<int> list4 = new List<int>();
		for (int i = 0; i <= rings; i++)
		{
			float num = (float)i / (float)rings;
			float a = Mathf.Lerp(0.15f, 1f, num * num) * ((i == rings) ? 0f : 1f);
			for (int j = 0; j <= segs; j++)
			{
				float f = (float)j / (float)segs * MathF.PI * 2f;
				float num2 = Mathf.Cos(f) * radius * num;
				float num3 = Mathf.Sin(f) * radius * num;
				list.Add(new Vector3(num2, lift, num3));
				list2.Add(new Vector2(num2 / radius * 0.5f + 0.5f, num3 / radius * 0.5f + 0.5f));
				list3.Add(new Color(1f, 1f, 1f, a));
			}
		}
		for (int k = 0; k < rings; k++)
		{
			for (int l = 0; l < segs; l++)
			{
				int num4 = k * (segs + 1) + l;
				int num5 = num4 + segs + 1;
				list4.Add(num4);
				list4.Add(num5);
				list4.Add(num4 + 1);
				list4.Add(num4 + 1);
				list4.Add(num5);
				list4.Add(num5 + 1);
			}
		}
		return Make(list, list2, list3, list4, Vector3.up);
	}

	private static Mesh Annulus(float inner, float outer, float lift, int segs)
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<Color> list3 = new List<Color>();
		List<int> list4 = new List<int>();
		for (int i = 0; i <= segs; i++)
		{
			float num = (float)i / (float)segs;
			float f = num * MathF.PI * 2f;
			Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
			list.Add(vector * inner + Vector3.up * lift);
			list.Add(vector * outer + Vector3.up * lift);
			list2.Add(new Vector2(num * 6f, 0f));
			list2.Add(new Vector2(num * 6f, 1f));
			list3.Add(Color.white);
			list3.Add(Color.white);
		}
		for (int j = 0; j < segs; j++)
		{
			int num2 = j * 2;
			list4.Add(num2);
			list4.Add(num2 + 1);
			list4.Add(num2 + 2);
			list4.Add(num2 + 2);
			list4.Add(num2 + 1);
			list4.Add(num2 + 3);
		}
		return Make(list, list2, list3, list4, Vector3.up);
	}

	private static Mesh Wall(float radius, float height, int segs)
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<Color> list3 = new List<Color>();
		List<int> list4 = new List<int>();
		for (int i = 0; i <= segs; i++)
		{
			float f = (float)i / (float)segs * MathF.PI * 2f;
			Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * radius;
			list.Add(vector);
			list.Add(vector + Vector3.up * height);
			list2.Add(new Vector2(0.5f, 0.5f));
			list2.Add(new Vector2(0.5f, 1f));
			list3.Add(Color.white);
			list3.Add(new Color(1f, 1f, 1f, 0f));
		}
		for (int j = 0; j < segs; j++)
		{
			int num = j * 2;
			list4.Add(num);
			list4.Add(num + 1);
			list4.Add(num + 2);
			list4.Add(num + 2);
			list4.Add(num + 1);
			list4.Add(num + 3);
		}
		return Make(list, list2, list3, list4, Vector3.forward);
	}

	private static Mesh Dome(float radius)
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<Color> list3 = new List<Color>();
		List<Vector3> list4 = new List<Vector3>();
		List<int> list5 = new List<int>();
		for (int i = 0; i < 7; i++)
		{
			for (int j = 0; j < 18; j++)
			{
				int count = list.Count;
				for (int k = 0; k < 4; k++)
				{
					int num = i + ((k >= 2) ? 1 : 0);
					int num2 = j + ((k == 1 || k == 2) ? 1 : 0);
					float num3 = (float)num / 7f;
					float f = num3 * MathF.PI * 0.5f;
					float f2 = (float)num2 / 18f * MathF.PI * 2f;
					Vector3 vector = new Vector3(Mathf.Cos(f) * Mathf.Cos(f2), Mathf.Sin(f), Mathf.Cos(f) * Mathf.Sin(f2));
					list.Add(vector * radius);
					list4.Add(vector);
					list2.Add(new Vector2((float)num2 / 18f, num3));
					float value = Mathf.Lerp(0.75f, 0.45f, Mathf.Sin(num3 * MathF.PI)) + ((num3 > 0.85f) ? 0.35f : 0f);
					Color item = Color.Lerp(new Color(1f, 0.55f, 0.25f), Color.white, num3);
					item.a = Mathf.Clamp01(value);
					list3.Add(item);
				}
				list5.Add(count);
				list5.Add(count + 3);
				list5.Add(count + 1);
				list5.Add(count + 1);
				list5.Add(count + 3);
				list5.Add(count + 2);
			}
		}
		Mesh mesh = new Mesh();
		mesh.SetVertices(list);
		mesh.SetUVs(0, list2);
		mesh.SetColors(list3);
		mesh.SetNormals(list4);
		mesh.SetTriangles(list5, 0);
		mesh.MarkDynamic();
		mesh.RecalculateBounds();
		return mesh;
	}

	private static Mesh Ribbon(float radius, float width, float arc, int segs)
	{
		List<Vector3> list = new List<Vector3>();
		List<Vector2> list2 = new List<Vector2>();
		List<Color> list3 = new List<Color>();
		List<int> list4 = new List<int>();
		for (int i = 0; i <= segs; i++)
		{
			float num = (float)i / (float)segs;
			float f = num * arc * (MathF.PI / 180f);
			Vector3 vector = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
			Vector3 vector2 = Vector3.Lerp(Vector3.up, vector, 0.25f).normalized * (width * 0.5f);
			list.Add(vector * radius - vector2);
			list.Add(vector * radius + vector2);
			list2.Add(new Vector2(num, 0f));
			list2.Add(new Vector2(num, 1f));
			list3.Add(Color.white);
			list3.Add(Color.white);
		}
		for (int j = 0; j < segs; j++)
		{
			int num2 = j * 2;
			list4.Add(num2);
			list4.Add(num2 + 1);
			list4.Add(num2 + 2);
			list4.Add(num2 + 2);
			list4.Add(num2 + 1);
			list4.Add(num2 + 3);
		}
		return Make(list, list2, list3, list4, Vector3.up);
	}

	private static Mesh Make(List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t, Vector3 normal)
	{
		Mesh mesh = new Mesh();
		mesh.SetVertices(v);
		mesh.SetUVs(0, uv);
		mesh.SetColors(c);
		Vector3[] array = new Vector3[v.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = normal;
		}
		mesh.normals = array;
		mesh.SetTriangles(t, 0);
		mesh.RecalculateBounds();
		return mesh;
	}

	private void SetPieceFade(Piece p, float fade)
	{
		if (p != null && p.Renderer != null)
		{
			p.Renderer.enabled = fade > 0.003f;
			p.Renderer.GetPropertyBlock(_block);
			_block.SetFloat("_Fade", fade);
			p.Renderer.SetPropertyBlock(_block);
		}
	}

	private void SetFade(float f)
	{
		SetPieceFade(_dome, f);
		SetPieceFade(_top, f);
		SetPieceFade(_ring, f);
		SetPieceFade(_wall, f);
		SetPieceFade(_runeDisc, f);
		SetPieceFade(_lava, f);
		for (int i = 0; i < _ribbons.Length; i++)
		{
			SetPieceFade(_ribbons[i], f);
		}
	}

	private static float Smooth(float a, float b, float t)
	{
		return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
	}

	private void Update()
	{
		float num = Time.time - _start;
		if (num >= 2.4f)
		{
			_live.Remove(this);
			UnityEngine.Object.Destroy(base.gameObject);
			return;
		}
		if (_follow != null)
		{
			base.transform.position = _follow.position;
		}
		else
		{
			_follow = null;
		}
		Tick(num);
	}

	public void Tick(float num)
	{
		float num2 = Smooth(0f, 0.25f, num) * (1f - Smooth(1.85f, 2.4f, num));
		float num3 = Mathf.Lerp(0.6f, 1f, Smooth(0f, 0.35f, num));
		_runeDisc.Transform.localScale = new Vector3(num3, 1f, num3);
		_runeDisc.Transform.localRotation = Quaternion.Euler(0f, num * 25f, 0f);
		SetPieceFade(_runeDisc, num2);
		SetPieceFade(_ring, num2 * (1f + 0.6f * (1f - Smooth(0f, 0.5f, num))));
		SetPieceFade(_lava, num2 * Smooth(0.15f, 0.6f, num));
		float num4 = Smooth(0f, 0.2f, num) * (1f - Smooth(0.3f, 0.6f, num));
		_wall.Transform.localScale = new Vector3(1f, Mathf.Lerp(0.3f, 1.4f, Smooth(0f, 0.6f, num)), 1f);
		SetPieceFade(_wall, num4 * 1.4f);
		float num5 = Smooth(0.08f, 0.45f, num);
		if (!_broken)
		{
			_dome.Transform.localScale = new Vector3(Mathf.Lerp(0.85f, 1f, num5), Mathf.Max(0.02f, num5) * 1.2f, Mathf.Lerp(0.85f, 1f, num5));
			float num6 = 0.85f + 0.15f * Mathf.Sin(num * 9f);
			SetPieceFade(_dome, num5 * num6);
			SetPieceFade(_top, num5 * (0.8f + 0.2f * Mathf.Sin(num * 13f)));
			if (num >= 1.85f)
			{
				StartBreak();
			}
		}
		else
		{
			UpdateBreak(num - 1.85f);
		}
		float fade = Smooth(0.25f, 0.6f, num) * (1f - Smooth(1.85f, 2.25f, num));
		float num7 = 1f + 0.5f * Smooth(1.85f, 2.4f, num);
		for (int i = 0; i < _ribbons.Length; i++)
		{
			Piece piece = _ribbons[i];
			piece.Transform.localRotation = Quaternion.Euler(0f, (float)i * 120f + num * _ribbonSpeed[i], 0f) * _ribbonTilt[i];
			piece.Transform.localScale = new Vector3(num7, 1f, num7);
			SetPieceFade(piece, fade);
		}
		if (!Application.isPlaying)
		{
			if (_sparks != null)
			{
				_sparks.Simulate(Mathf.Max(0f, num - 0.05f), withChildren: true, restart: true);
			}
		}
		else if (_sparks != null && num > 0.05f && !_sparks.isPlaying && num < 1.85f)
		{
			_sparks.Play(withChildren: true);
		}
		if (_cam == null)
		{
			_cam = Camera.main;
		}
		if (_cam != null && _top != null)
		{
			_top.Transform.rotation = Quaternion.FromToRotation(Vector3.up, -_cam.transform.forward);
		}
	}

	private void StartBreak()
	{
		_broken = true;
		_domeBase = _domeMesh.vertices;
		_domeWork = new Vector3[_domeBase.Length];
		_shardVel = new Vector3[_domeBase.Length / 4];
		System.Random random = new System.Random(Played * 7919 + (int)(_radius * 13f));
		for (int i = 0; i < _shardVel.Length; i++)
		{
			Vector3 normalized = ((_domeBase[i * 4] + _domeBase[i * 4 + 2]) * 0.5f).normalized;
			float num = 0.9f + (float)random.NextDouble() * 1.1f;
			_shardVel[i] = (normalized + Vector3.up * 0.35f) * (_radius * num) + new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * _radius * 0.6f;
		}
		_dome.Transform.localScale = new Vector3(1f, 1.2f, 1f);
		SetPieceFade(_top, 0f);
	}

	private void UpdateBreak(float t)
	{
		float num = _radius * 2.4f;
		float num2 = Mathf.Clamp01(1f - t / 0.5500001f);
		float num3 = Mathf.Lerp(0.25f, 1f, num2);
		for (int i = 0; i < _shardVel.Length; i++)
		{
			Vector3 vector = (_domeBase[i * 4] + _domeBase[i * 4 + 2]) * 0.5f;
			Vector3 vector2 = vector + _shardVel[i] * t + Vector3.down * (0.5f * num * t * t);
			for (int j = 0; j < 4; j++)
			{
				_domeWork[i * 4 + j] = vector2 + (_domeBase[i * 4 + j] - vector) * num3;
			}
		}
		_domeMesh.vertices = _domeWork;
		_domeMesh.RecalculateBounds();
		float num4 = 1f + 1.2f * Mathf.Clamp01(1f - t / 0.12f);
		SetPieceFade(_dome, num2 * num2 * num4);
	}

	private void OnDestroy()
	{
		_live.Remove(this);
		HashSet<UnityEngine.Object> hashSet = new HashSet<UnityEngine.Object>();
		foreach (Transform item in base.transform)
		{
			MeshFilter component = item.GetComponent<MeshFilter>();
			if (component != null && component.sharedMesh != null)
			{
				hashSet.Add(component.sharedMesh);
			}
			Renderer component2 = item.GetComponent<Renderer>();
			if (component2 != null && component2.sharedMaterial != null)
			{
				hashSet.Add(component2.sharedMaterial);
			}
		}
		foreach (UnityEngine.Object item2 in hashSet)
		{
			UnityEngine.Object.Destroy(item2);
		}
	}
}

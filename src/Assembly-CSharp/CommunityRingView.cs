using System;
using System.Collections.Generic;
using Durango.Render.Camera;
using Durango.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

public class CommunityRingView : MonoBehaviour
{
	public const float TileUnits = 200f;

	public static Color Tint = new Color(1f, 0.847f, 0.357f, 1f);

	public static float WallHeight = 300f;

	public static float WallAlpha = 0.6f;

	public static float GroundBand = 36f;

	public static float GroundAlpha = 0.6f;

	public static float Pulse = 0.18f;

	private static readonly Dictionary<string, CommunityRingView> _views = new Dictionary<string, CommunityRingView>(StringComparer.Ordinal);

	private static Material _material;

	private Mesh _wall;

	private Mesh _ground;

	private MeshRenderer _wallRenderer;

	private MeshRenderer _groundRenderer;

	private float _radius = -1f;

	private float _built = -1f;

	public static int Count => _views.Count;

	public static void Show(string id, Vector2 centerTile, float radiusTiles)
	{
		if (string.IsNullOrEmpty(id) || radiusTiles <= 0f)
		{
			Hide(id);
			return;
		}
		if (!_views.TryGetValue(id, out var value) || value == null)
		{
			GameObject obj = new GameObject("CommunityRing_" + id);
			UnityEngine.Object.DontDestroyOnLoad(obj);
			value = obj.AddComponent<CommunityRingView>();
			value.Init();
			_views[id] = value;
		}
		value.Place(Util.TilePositionToClientPosition(centerTile), radiusTiles * 200f);
	}

	public static void Hide(string id)
	{
		if (!string.IsNullOrEmpty(id) && _views.TryGetValue(id, out var value))
		{
			_views.Remove(id);
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
	}

	public static void HideAll()
	{
		foreach (CommunityRingView value in _views.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
		_views.Clear();
	}

	public static void Restyle()
	{
		foreach (CommunityRingView value in _views.Values)
		{
			if (value != null)
			{
				value._built = -1f;
			}
		}
	}

	private static Material SharedMaterial()
	{
		if (_material == null)
		{
			_material = new Material(NGUITools.defaultShader)
			{
				mainTexture = Texture2D.whiteTexture
			};
		}
		return _material;
	}

	private void Init()
	{
		_wall = new Mesh();
		_wall.MarkDynamic();
		GameObject gameObject = new GameObject("Wall");
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject.AddComponent<MeshFilter>().sharedMesh = _wall;
		_wallRenderer = gameObject.AddComponent<MeshRenderer>();
		_wallRenderer.sharedMaterial = SharedMaterial();
		_wallRenderer.shadowCastingMode = ShadowCastingMode.Off;
		_wallRenderer.receiveShadows = false;
		_ground = new Mesh();
		_ground.MarkDynamic();
		GameObject gameObject2 = new GameObject("Ground");
		gameObject2.transform.SetParent(base.transform, worldPositionStays: false);
		gameObject2.layer = OverlayCamera.Layer;
		gameObject2.AddComponent<MeshFilter>().sharedMesh = _ground;
		_groundRenderer = gameObject2.AddComponent<MeshRenderer>();
		_groundRenderer.sharedMaterial = SharedMaterial();
		_groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
		_groundRenderer.receiveShadows = false;
	}

	private void Place(Vector3 center, float radius)
	{
		base.transform.position = center;
		_radius = radius;
		if (_built < 0f || Mathf.Abs(_built - radius) > 1f)
		{
			Build(radius);
		}
	}

	private void Update()
	{
		if (!(_wall == null) && !(Pulse <= 0f))
		{
			float num = 1f - Pulse * 0.5f * (1f + Mathf.Sin(Time.time * 2.4f));
			Color[] colors = _wall.colors;
			int num2 = colors.Length / 2;
			for (int i = 0; i < num2; i++)
			{
				colors[i * 2].a = WallAlpha * num;
			}
			_wall.colors = colors;
		}
	}

	private void Build(float radius)
	{
		_built = radius;
		int num = Mathf.Clamp(Mathf.CeilToInt(MathF.PI * 2f * radius / 60f), 48, 360);
		Vector3[] array = new Vector3[(num + 1) * 2];
		Color[] array2 = new Color[array.Length];
		int[] array3 = new int[num * 6];
		Color tint = Tint;
		tint.a = WallAlpha;
		Color tint2 = Tint;
		tint2.a = 0f;
		for (int i = 0; i <= num; i++)
		{
			float f = MathF.PI * 2f * (float)i / (float)num;
			Vector3 vector = new Vector3(Mathf.Cos(f) * radius, 0f, Mathf.Sin(f) * radius);
			array[i * 2] = vector + Vector3.down * 30f;
			array[i * 2 + 1] = vector + Vector3.up * WallHeight;
			array2[i * 2] = tint;
			array2[i * 2 + 1] = tint2;
			if (i < num)
			{
				int num2 = i * 2;
				int num3 = i * 6;
				array3[num3] = num2;
				array3[num3 + 1] = num2 + 1;
				array3[num3 + 2] = num2 + 2;
				array3[num3 + 3] = num2 + 1;
				array3[num3 + 4] = num2 + 3;
				array3[num3 + 5] = num2 + 2;
			}
		}
		_wall.Clear();
		_wall.vertices = array;
		_wall.colors = array2;
		_wall.triangles = array3;
		_wall.RecalculateBounds();
		float num4 = Mathf.Max(0f, radius - GroundBand);
		Vector3[] array4 = new Vector3[(num + 1) * 3];
		Color[] array5 = new Color[array4.Length];
		int[] array6 = new int[num * 12];
		Color tint3 = Tint;
		tint3.a = GroundAlpha;
		Color tint4 = Tint;
		tint4.a = GroundAlpha * 0.25f;
		float num5 = Mathf.Max(num4, radius - 8f);
		for (int j = 0; j <= num; j++)
		{
			float f2 = MathF.PI * 2f * (float)j / (float)num;
			Vector3 vector2 = new Vector3(Mathf.Cos(f2), 0f, Mathf.Sin(f2));
			array4[j * 3] = vector2 * num4;
			array4[j * 3 + 1] = vector2 * num5;
			array4[j * 3 + 2] = vector2 * radius;
			array5[j * 3] = tint4;
			array5[j * 3 + 1] = tint3;
			array5[j * 3 + 2] = tint3;
			if (j < num)
			{
				int num6 = j * 3;
				int num7 = j * 12;
				for (int k = 0; k < 2; k++)
				{
					int num8 = (array6[num7 + k * 6] = num6 + k);
					array6[num7 + k * 6 + 1] = num8 + 3;
					array6[num7 + k * 6 + 2] = num8 + 1;
					array6[num7 + k * 6 + 3] = num8 + 1;
					array6[num7 + k * 6 + 4] = num8 + 3;
					array6[num7 + k * 6 + 5] = num8 + 4;
				}
			}
		}
		_ground.Clear();
		_ground.vertices = array4;
		_ground.colors = array5;
		_ground.triangles = array6;
		_ground.RecalculateBounds();
	}

	public static string Describe()
	{
		return "views " + _views.Count + " wall " + WallHeight + "/" + WallAlpha + " band " + GroundBand + "/" + GroundAlpha + " pulse " + Pulse;
	}
}

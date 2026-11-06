using System;
using System.Collections.Generic;
using Durango.Development;
using Messages;
using UnityEngine;
using UnityEngine.Rendering;

namespace Durango.UI.InGame;

public class FillBorderAlert : MonoBehaviour
{
	public Action<FillBorderAlert> Finished;

	private const float FadeIn = 0.3f;

	private const float FadeOut = 0.3f;

	private const float BorderSize = 40f;

	private Mesh _bgMesh;

	private Mesh _fillMesh;

	private Material _bgMaterial;

	private Material _fillMaterial;

	private bool _showing;

	private bool _ownsMaterials;

	private bool _enemy;

	private float _nextStyleAt;

	private float _lineWidth = -1f;

	private Mesh _glowMesh;

	private readonly List<Vector3> _glowVerts = new List<Vector3>();

	private readonly List<int> _glowTris = new List<int>();

	private readonly List<Color> _glowCols = new List<Color>();

	private readonly List<float> _glowWeights = new List<float>();

	private Mesh _rimMesh;

	private Material _rimMaterial;

	private Material _glowMaterial;

	private readonly List<Vector3> _rimVerts = new List<Vector3>();

	private readonly List<Color> _rimCols = new List<Color>();

	private readonly List<int> _rimTris = new List<int>();

	private readonly List<Vector2> _borderDirections = new List<Vector2>();

	private float _progress;

	private float _urgency;

	private readonly List<Vector3> _bgVerts = new List<Vector3>();

	private readonly List<Color> _bgCols = new List<Color>();

	private readonly List<int> _bgTris = new List<int>();

	private readonly List<Vector3> _fillVerts = new List<Vector3>();

	private readonly List<Vector2> _fillUvs = new List<Vector2>();

	private readonly List<Color> _fillCols = new List<Color>();

	private readonly List<int> _fillTris = new List<int>();

	private readonly List<Vector3> _outter = new List<Vector3>();

	private readonly List<Vector3> _inner = new List<Vector3>();

	private Color _bgColor;

	private Color _fillColor;

	private float _startAt;

	private float _finishAt;

	private float _showAt;

	private float _hideAt;

	private const int PointCount = 2;

	private bool _isInit;

	public static int ActiveViewers { get; private set; }

	public string Stage => AttackRangeStyle.Phase(AreaOfEffectVisualizer.Now(), _finishAt, _hideAt);

	public int Id { get; set; }

	public void Init(Color bgColor, Color borderColor, Texture2D fillTexture, Material background = null, Material border = null, Material accent = null)
	{
		if (!_isInit)
		{
			_isInit = true;
			_bgMesh = new Mesh();
			MeshFilter meshFilter = base.gameObject.AddComponent<MeshFilter>();
			_ownsMaterials = background == null;
			MeshRenderer meshRenderer = base.gameObject.AddComponent<MeshRenderer>();
			Material obj = background ?? new Material(NGUITools.defaultShader)
			{
				mainTexture = Texture2D.whiteTexture
			};
			Material sharedMaterial = obj;
			_bgMaterial = obj;
			meshRenderer.sharedMaterial = sharedMaterial;
			meshFilter.sharedMesh = _bgMesh;
			_fillMesh = new Mesh();
			GameObject gameObject = base.gameObject.AddChild();
			MeshFilter meshFilter2 = gameObject.AddComponent<MeshFilter>();
			gameObject.AddComponent<MeshRenderer>().sharedMaterial = (_fillMaterial = border ?? FillBorderAlerts.CreateAccentMaterial(fillTexture));
			meshFilter2.sharedMesh = _fillMesh;
			_rimMesh = new Mesh
			{
				name = "Attack range accent"
			};
			GameObject obj2 = base.gameObject.AddChild();
			obj2.name = "Range Rim";
			obj2.AddComponent<MeshFilter>().sharedMesh = _rimMesh;
			_rimMaterial = _bgMaterial;
			_glowMaterial = accent ?? FillBorderAlerts.CreateAccentMaterial(Texture2D.whiteTexture);
			obj2.AddComponent<MeshRenderer>().sharedMaterial = _rimMaterial;
			GetComponent<MeshRenderer>().sortingOrder = 0;
			gameObject.GetComponent<MeshRenderer>().sortingOrder = 2;
			obj2.GetComponent<MeshRenderer>().sortingOrder = 3;
			_glowMesh = new Mesh
			{
				name = "Attack range inner glow"
			};
			GameObject obj3 = base.gameObject.AddChild();
			obj3.name = "Range Inner Glow";
			obj3.AddComponent<MeshFilter>().sharedMesh = _glowMesh;
			obj3.AddComponent<MeshRenderer>().sharedMaterial = _glowMaterial;
			obj3.GetComponent<MeshRenderer>().sortingOrder = 1;
			_bgMesh.MarkDynamic();
			_fillMesh.MarkDynamic();
			_rimMesh.MarkDynamic();
			_glowMesh.MarkDynamic();
			_bgColor = bgColor;
			_fillColor = borderColor;
			_enemy = borderColor.r > borderColor.g;
		}
	}

	public void SetArc(Vector3 position, float radius, float startAngle, float endAngle)
	{
		MakeRadiusMesh(radius, startAngle, endAngle);
		base.transform.position = position;
	}

	public void SetRect(Vector3 position, float width, float height, float angle)
	{
		MakeRectMesh(width, height, angle);
		base.transform.position = position;
	}

	public void SetMask(AttackRangeMask mask)
	{
		BeginMakeMesh();
		bool[] cells = new bool[mask.Width * mask.Height];
		for (int i = 0; i < mask.Runs.Length; i += 3)
		{
			int num = mask.Runs[i];
			int num2 = mask.Runs[i + 1];
			int num3 = mask.Runs[i + 2];
			for (int j = num2; j <= num3; j++)
			{
				cells[num * mask.Width + j] = true;
			}
			int count = _bgVerts.Count;
			_bgVerts.Add(new Vector3((float)num2 * mask.Cell, 0f, (float)num * mask.Cell));
			_bgVerts.Add(new Vector3((float)(num3 + 1) * mask.Cell, 0f, (float)num * mask.Cell));
			_bgVerts.Add(new Vector3((float)(num3 + 1) * mask.Cell, 0f, (float)(num + 1) * mask.Cell));
			_bgVerts.Add(new Vector3((float)num2 * mask.Cell, 0f, (float)(num + 1) * mask.Cell));
			for (int k = 0; k < 4; k++)
			{
				_bgCols.Add(_bgColor);
			}
			_bgTris.Add(count);
			_bgTris.Add(count + 2);
			_bgTris.Add(count + 1);
			_bgTris.Add(count);
			_bgTris.Add(count + 3);
			_bgTris.Add(count + 2);
		}
		for (int l = 0; l < mask.Height; l++)
		{
			for (int m = 0; m < mask.Width; m++)
			{
				if (Filled(m, l))
				{
					Vector3 vector = new Vector3((float)m * mask.Cell, 0f, (float)l * mask.Cell);
					Vector3 vector2 = vector + new Vector3(mask.Cell, 0f, 0f);
					Vector3 vector3 = vector2 + new Vector3(0f, 0f, mask.Cell);
					Vector3 vector4 = vector + new Vector3(0f, 0f, mask.Cell);
					if (!Filled(m, l - 1))
					{
						Edge(vector, vector2, Vector3.forward);
					}
					if (!Filled(m + 1, l))
					{
						Edge(vector2, vector3, Vector3.left);
					}
					if (!Filled(m, l + 1))
					{
						Edge(vector3, vector4, Vector3.back);
					}
					if (!Filled(m - 1, l))
					{
						Edge(vector4, vector, Vector3.right);
					}
				}
			}
		}
		_bgMesh.indexFormat = IndexFormat.UInt32;
		_fillMesh.indexFormat = IndexFormat.UInt32;
		_bgMesh.SetVertices(_bgVerts);
		_bgMesh.SetColors(_bgCols);
		_bgMesh.SetTriangles(_bgTris, 0);
		_fillMesh.SetVertices(_fillVerts);
		_fillMesh.SetColors(_fillCols);
		_fillMesh.SetUVs(0, _fillUvs);
		_fillMesh.SetTriangles(_fillTris, 0);
		MakeBorderDetails();
		MakeMaskGlow(mask);
		base.transform.position = new WorldPosition(mask.X, mask.Z).ToClientPosition();
		void Edge(Vector3 a, Vector3 b, Vector3 inward)
		{
			int count2 = _fillVerts.Count;
			float num4 = Mathf.Min(40f, mask.Cell * 0.45f);
			_inner.Add(a + inward * num4);
			_outter.Add(a);
			_inner.Add(b + inward * num4);
			_outter.Add(b);
			_fillVerts.Add(a + inward * num4);
			_fillVerts.Add(a);
			_fillVerts.Add(b + inward * num4);
			_fillVerts.Add(b);
			for (int n = 0; n < 4; n++)
			{
				_fillCols.Add(_fillColor);
				_fillUvs.Add(new Vector2(n % 2, 0f));
			}
			_fillTris.Add(count2);
			_fillTris.Add(count2 + 2);
			_fillTris.Add(count2 + 1);
			_fillTris.Add(count2 + 2);
			_fillTris.Add(count2 + 3);
			_fillTris.Add(count2 + 1);
		}
		bool Filled(int x, int z)
		{
			if (x >= 0 && z >= 0 && x < mask.Width && z < mask.Height)
			{
				return cells[z * mask.Width + x];
			}
			return false;
		}
	}

	public void Show(float startAt, float finishAt, float showAt, float hideAt)
	{
		_startAt = startAt;
		_finishAt = finishAt;
		_showAt = showAt;
		_hideAt = hideAt;
		if (!_showing)
		{
			ActiveViewers++;
		}
		_showing = true;
		_nextStyleAt = 0f;
		base.gameObject.SetActive(value: true);
		Update();
		if (!Application.isPlaying)
		{
			EditorUpdateLoop.Play(this);
		}
	}

	public void Stop(float delay)
	{
		if (delay > 0f)
		{
			float num = AreaOfEffectVisualizer.Now();
			_hideAt = Mathf.Min(num + delay, _hideAt);
			return;
		}
		base.gameObject.SetActive(value: false);
		if (!Application.isPlaying)
		{
			OnDisable();
		}
	}

	private void OnDisable()
	{
		if (_showing)
		{
			_showing = false;
			ActiveViewers = Mathf.Max(0, ActiveViewers - 1);
			base.gameObject.SetActive(value: false);
			if (Finished != null)
			{
				Finished(this);
			}
		}
	}

	private void Update()
	{
		if (!_isInit)
		{
			return;
		}
		if (AreaOfEffectVisualizer.Now() > _hideAt + 0.3f)
		{
			Stop(0f);
			return;
		}
		float num = AreaOfEffectVisualizer.Now();
		AttackRangeStyle.Refresh(num);
		if (_lineWidth != AttackRangeStyle.Width)
		{
			MakeBorderDetails();
		}
		if (!(num < _nextStyleAt))
		{
			_nextStyleAt = num + AttackRangeStyle.UpdateInterval(ActiveViewers);
			UpdateProgress();
			UpdateAlpha();
			_bgMesh.SetColors(_bgCols);
			_fillMesh.SetColors(_fillCols);
			_rimMesh.SetColors(_rimCols);
			_glowMesh.SetColors(_glowCols);
		}
	}

	private void UpdateProgress()
	{
		float num = AreaOfEffectVisualizer.Now();
		Color color = AttackRangeStyle.Palette(Color.Lerp(_fillColor, _enemy ? new Color(1f, 0.1f, 0.035f, _fillColor.a) : new Color(0.12f, 1f, 0.26f, _fillColor.a), 0.8f), _enemy);
		_progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - _startAt) / Mathf.Max(0.001f, _finishAt - _startAt)));
		_urgency = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - (_finishAt - num) / 0.4f));
		bool flag = !AttackRangeStyle.ReduceMotion && AttackRangeStyle.Quality != "low";
		float num2 = (flag ? (_urgency * (0.5f + 0.5f * Mathf.Sin(num * 8f))) : 0f);
		float f = _progress * MathF.PI * 2f;
		Vector2 rhs = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
		int count = _fillVerts.Count;
		for (int i = 0; i < _borderDirections.Count; i++)
		{
			float num3 = (flag ? Mathf.Max(0f, Vector2.Dot(_borderDirections[i], rhs)) : 0f);
			num3 *= num3;
			num3 *= num3;
			num3 *= num3;
			float num4 = Mathf.Repeat(Mathf.Atan2(_borderDirections[i].y, _borderDirections[i].x) / (MathF.PI * 2f), 1f);
			float num5 = ((flag && num4 <= _progress) ? 1f : 0f);
			float num6 = ((AttackRangeStyle.ColorAssist && _enemy && Mathf.Repeat(Mathf.Atan2(_borderDirections[i].y, _borderDirections[i].x) * (8f / MathF.PI), 1f) > 0.65f) ? 0.22f : 1f);
			Color value = Color.Lerp(color, Color.white, num3 * 0.25f);
			value.a = Mathf.Clamp01(0.32f + num5 * 0.1f + num3 * 0.12f + num2 * 0.04f);
			_fillCols[i * 2] = value;
			_fillCols[i * 2 + 1] = value;
			Color value2 = Color.Lerp(color, Color.white, 0.08f + num5 * 0.05f + num3 * 0.32f + _urgency * 0.05f);
			value2.a = Mathf.Max(0.9f, color.a) * Mathf.Clamp01(0.9f + num3 * 0.1f) * num6;
			_rimCols[i * 2] = value2;
			value2.a *= 0.35f;
			_rimCols[i * 2 + 1] = value2;
			Color value3 = Color.Lerp(color, Color.white, 0.1f + num3 * 0.35f);
			value3.a = (0.2f + num5 * 0.1f + num3 * 0.15f) * num6;
			_rimCols[count + i * 2] = value3;
			_rimCols[count + i * 2 + 1] = value3;
		}
		for (int j = 0; j < _glowWeights.Count; j++)
		{
			Color value4 = color;
			value4.a = _glowWeights[j] * (0.26f + _progress * 0.1f + _urgency * 0.1f);
			if (AttackRangeStyle.Quality == "low" || ActiveViewers > 32)
			{
				value4.a = 0f;
			}
			_glowCols[j] = value4;
		}
	}

	private void UpdateAlpha()
	{
		float value = 1f;
		float num = AreaOfEffectVisualizer.Now();
		if (num > _hideAt)
		{
			value = 1f - (num - _hideAt) / 0.3f;
		}
		float num2 = Mathf.Min(0.12f, Mathf.Max(0.03f, (_finishAt - _showAt) * 0.25f));
		if (num <= _hideAt && num < _showAt + num2)
		{
			value = (num - _showAt) / num2;
		}
		value = Mathf.Clamp01(value);
		for (int i = 0; i < _bgCols.Count; i++)
		{
			Color value2 = AttackRangeStyle.Palette(_bgColor, _enemy);
			value2.a *= value * AttackRangeStyle.Intensity * (0.65f + _progress * 0.25f + _urgency * 0.1f) * ((ActiveViewers > 12) ? 0.72f : 1f);
			_bgCols[i] = value2;
		}
		for (int j = 0; j < _fillCols.Count; j++)
		{
			Color value3 = _fillCols[j];
			value3.a = Mathf.Clamp01(value3.a * value * AttackRangeStyle.Intensity);
			_fillCols[j] = value3;
		}
		for (int k = 0; k < _rimCols.Count; k++)
		{
			Color value4 = _rimCols[k];
			value4.a = Mathf.Clamp01(value4.a * value * AttackRangeStyle.Intensity);
			_rimCols[k] = value4;
		}
		for (int l = 0; l < _glowCols.Count; l++)
		{
			Color value5 = _glowCols[l];
			value5.a = Mathf.Clamp01(value5.a * value * AttackRangeStyle.Intensity);
			_glowCols[l] = value5;
		}
	}

	private void MakeBorderDetails()
	{
		_lineWidth = AttackRangeStyle.Width;
		_rimMesh.Clear();
		_rimVerts.Clear();
		_rimCols.Clear();
		_rimTris.Clear();
		_borderDirections.Clear();
		Vector3 center = _bgMesh.bounds.center;
		int count = _fillVerts.Count;
		for (int i = 0; i < 2; i++)
		{
			for (int j = 0; j < count; j += 2)
			{
				Vector3 a = _fillVerts[j];
				Vector3 b = _fillVerts[j + 1];
				float num = Vector3.Distance(a, b);
				float num2 = Mathf.Min(((i == 0) ? 4f : 1.5f) * _lineWidth, num * ((i == 0) ? 0.6f : 0.15f));
				float num3 = ((i == 0) ? 1f : 0.3f);
				float t = Mathf.Max(0f, num3 - num2 / Mathf.Max(0.001f, num));
				_rimVerts.Add(Vector3.Lerp(a, b, t));
				_rimVerts.Add(Vector3.Lerp(a, b, num3));
				_rimCols.Add(_fillColor);
				_rimCols.Add(_fillColor);
				if (i == 0)
				{
					_borderDirections.Add(new Vector2(b.x - center.x, b.z - center.z).normalized);
				}
			}
			foreach (int fillTri in _fillTris)
			{
				_rimTris.Add(fillTri + i * count);
			}
		}
		_rimMesh.indexFormat = IndexFormat.UInt32;
		_rimMesh.SetVertices(_rimVerts);
		_rimMesh.SetColors(_rimCols);
		_rimMesh.SetTriangles(_rimTris, 0);
	}

	private void OnDestroy()
	{
		Release(_bgMesh);
		Release(_fillMesh);
		Release(_rimMesh);
		Release(_glowMesh);
		if (_ownsMaterials)
		{
			Release(_bgMaterial);
			Release(_fillMaterial);
			Release(_glowMaterial);
		}
	}

	private static void Release(UnityEngine.Object resource)
	{
		if (!(resource == null))
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(resource);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(resource);
			}
		}
	}

	private void BeginMakeMesh()
	{
		_glowMesh.Clear();
		_glowVerts.Clear();
		_glowCols.Clear();
		_glowTris.Clear();
		_glowWeights.Clear();
		base.transform.localRotation = Quaternion.identity;
		_inner.Clear();
		_outter.Clear();
		_bgMesh.Clear();
		_fillMesh.Clear();
		_bgVerts.Clear();
		_bgCols.Clear();
		_bgTris.Clear();
		_fillVerts.Clear();
		_fillCols.Clear();
		_fillUvs.Clear();
		_fillTris.Clear();
	}

	private void MakeMaskGlow(AttackRangeMask mask)
	{
		AttackRangeMaskGlow data = AttackRangeMaskGlow.Get(mask);
		if (data == null)
		{
			return;
		}
		int stride = mask.Width + 1;
		for (int i = 0; i < mask.Height; i++)
		{
			for (int j = 0; j < mask.Width; j++)
			{
				if (data.Cells[i * mask.Width + j] && (!(data.Distances[i * stride + j] >= 60f) || !(data.Distances[i * stride + j + 1] >= 60f) || !(data.Distances[(i + 1) * stride + j] >= 60f) || !(data.Distances[(i + 1) * stride + j + 1] >= 60f)))
				{
					int count = _glowVerts.Count;
					Vertex(j, i);
					Vertex(j + 1, i);
					Vertex(j + 1, i + 1);
					Vertex(j, i + 1);
					_glowTris.Add(count);
					_glowTris.Add(count + 2);
					_glowTris.Add(count + 1);
					_glowTris.Add(count);
					_glowTris.Add(count + 3);
					_glowTris.Add(count + 2);
				}
			}
		}
		_glowMesh.indexFormat = IndexFormat.UInt32;
		_glowMesh.SetVertices(_glowVerts);
		_glowMesh.SetColors(_glowCols);
		_glowMesh.SetTriangles(_glowTris, 0);
		void Vertex(int x, int z)
		{
			_glowVerts.Add(new Vector3((float)x * mask.Cell, 0f, (float)z * mask.Cell));
			_glowCols.Add(_fillColor);
			float num = 1f - Mathf.Clamp01(data.Distances[z * stride + x] / 60f);
			_glowWeights.Add(num * num);
		}
	}

	private void EndMakeMesh()
	{
		Vector3 zero = Vector3.zero;
		for (int i = 0; i < _outter.Count; i++)
		{
			zero += _outter[i];
		}
		zero /= (float)_outter.Count;
		_bgVerts.Add(zero);
		_bgCols.Add(_bgColor);
		for (int j = 0; j < _outter.Count; j++)
		{
			_bgVerts.Add(_outter[j]);
			_bgCols.Add(_bgColor);
			_bgTris.Add(0);
			_bgTris.Add((j + 1) % _outter.Count + 1);
			_bgTris.Add(j + 1);
			_fillVerts.Add(_inner[j]);
			_fillVerts.Add(_outter[j]);
			_fillCols.Add(_fillColor);
			_fillCols.Add(_fillColor);
			_fillUvs.Add(new Vector2(0f, 0f));
			_fillUvs.Add(new Vector2(1f, 0f));
			int num = j * 2;
			int num2 = (j + 1) % _outter.Count * 2;
			_fillTris.Add(num);
			_fillTris.Add(num2);
			_fillTris.Add(num + 1);
			_fillTris.Add(num2);
			_fillTris.Add(num2 + 1);
			_fillTris.Add(num + 1);
		}
		_bgMesh.SetVertices(_bgVerts);
		_bgMesh.SetColors(_bgCols);
		_bgMesh.SetTriangles(_bgTris, 0);
		_fillMesh.SetVertices(_fillVerts);
		_fillMesh.SetColors(_fillCols);
		_fillMesh.SetUVs(0, _fillUvs);
		_fillMesh.SetTriangles(_fillTris, 0);
		MakeBorderDetails();
	}

	private void MakeRadiusMesh(float radius, float start, float end)
	{
		BeginMakeMesh();
		float num = Mathf.Abs(start - end);
		if (num < 360f)
		{
			float num2 = Mathf.Asin(40f / (radius - 40f));
			float num3 = MathF.PI / 180f * start + num2;
			float num4 = MathF.PI / 180f * end - num2;
			Vector2 vector = new Vector2(Mathf.Sin(num3), Mathf.Cos(num3)) * (radius - 40f);
			Vector2 vector2 = new Vector2(Mathf.Sin(num4), Mathf.Cos(num4)) * (radius - 40f);
			FillCorner(vector, start - 90f, num3 * 57.29578f, isInner: true);
			FillArc(radius, num3, num4);
			FillCorner(vector2, num4 * 57.29578f, end + 90f, isInner: true);
			if (num > 180f)
			{
				Vector2 vector3 = new Vector2(Mathf.Sin(end * (MathF.PI / 180f)), Mathf.Cos(end * (MathF.PI / 180f)));
				Vector2 vector4 = new Vector2(Mathf.Sin(start * (MathF.PI / 180f)), Mathf.Cos(start * (MathF.PI / 180f)));
				FillLine(vector3 * (radius - 40f), Vector2.zero, 1f);
				FillCorner(Vector2.zero, end - 90f, start + 90f, isInner: false);
				FillLine(Vector2.zero, vector4 * (radius - 40f), 1f);
			}
			else
			{
				float num5 = 40f / Mathf.Sin(num * (MathF.PI / 180f) * 0.5f);
				float f = Mathf.Lerp(start, end, 0.5f) * (MathF.PI / 180f);
				Vector2 vector5 = new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * num5;
				FillLine(vector2, vector5, 0f);
				FillCorner(vector5, end + 90f, start + 270f, isInner: true);
				FillLine(vector5, vector, 0f);
			}
		}
		else
		{
			FillArc(radius, 0f, MathF.PI * 2f);
		}
		EndMakeMesh();
	}

	private void MakeRectMesh(float width, float height, float angle)
	{
		BeginMakeMesh();
		Vector2[] array = new Vector2[4];
		Vector2 vector = new Vector2(Mathf.Sin(angle * (MathF.PI / 180f)), Mathf.Cos(angle * (MathF.PI / 180f)));
		Vector2 vector2 = new Vector2(vector.y, 0f - vector.x);
		array[0] = -vector * 0.5f * width + -vector2 * 0.5f * height;
		array[1] = vector * 0.5f * width + -vector2 * 0.5f * height;
		array[2] = vector * 0.5f * width + vector2 * 0.5f * height;
		array[3] = -vector * 0.5f * width + vector2 * 0.5f * height;
		for (int i = 0; i < array.Length; i++)
		{
			Vector2 vector3 = array[i];
			Vector2 vector4 = array[(i + 1) % array.Length];
			Vector2 normalized = (vector4 - vector3).normalized;
			vector3 += normalized * 40f;
			vector4 -= normalized * 40f;
			FillLine(vector3, vector4, 1f);
			FillCorner(vector4 + new Vector2(normalized.y, 0f - normalized.x) * 40f, angle + (float)(i - 1) * 90f, angle + (float)i * 90f, isInner: true);
		}
		EndMakeMesh();
	}

	private void FillArc(float radius, float a1, float a2)
	{
		int num = Mathf.RoundToInt(MathF.PI * 2f * radius / 10f * (Mathf.Abs(a1 - a2) / (MathF.PI * 2f)));
		for (int i = 0; i < num; i++)
		{
			float f = Mathf.Lerp(a1, a2, (float)i / (float)(num - 1));
			Vector3 vector = new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f));
			_inner.Add(vector * (radius - 40f));
			_outter.Add(vector * radius);
		}
	}

	private void FillLine(Vector2 p1, Vector2 p2, float pivotRatio)
	{
		Vector2 vector = p2 - p1;
		float magnitude = vector.magnitude;
		int num = Mathf.RoundToInt(magnitude / 10f);
		Vector2 vector2 = vector / magnitude;
		Vector2 vector3 = new Vector2(vector2.y, 0f - vector2.x);
		for (int i = 0; i < num; i++)
		{
			Vector2 vector4 = Vector2.Lerp(p1, p2, (float)i / (float)(num - 1));
			Vector2 vector5 = vector4 - vector3 * 40f * (1f - pivotRatio);
			Vector2 vector6 = vector4 + vector3 * 40f * pivotRatio;
			_outter.Add(new Vector3(vector5.x, 0f, vector5.y));
			_inner.Add(new Vector3(vector6.x, 0f, vector6.y));
		}
	}

	private void FillCorner(Vector2 pos, float start, float end, bool isInner)
	{
		int num = Mathf.RoundToInt(Mathf.Abs(end - start) / 20f);
		for (int i = 1; i < num; i++)
		{
			float f = Mathf.Lerp(start, end, (float)i / (float)num) * (MathF.PI / 180f);
			Vector2 vector = new Vector2(Mathf.Sin(f), Mathf.Cos(f));
			Vector2 vector2 = pos;
			Vector2 vector3 = vector2 + vector * 40f;
			if (isInner)
			{
				_outter.Add(new Vector3(vector3.x, 0f, vector3.y));
				_inner.Add(new Vector3(vector2.x, 0f, vector2.y));
			}
			else
			{
				_outter.Add(new Vector3(vector2.x, 0f, vector2.y));
				_inner.Add(new Vector3(vector3.x, 0f, vector3.y));
			}
		}
	}
}

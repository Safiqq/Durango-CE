using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PigeonCoopToolkit.Effects.Trails;

public abstract class TrailRenderer_Base : MonoBehaviour
{
	public PCTrailRendererData TrailData;

	public bool Emit;

	public int MaxNumberOfPoints = 50;

	protected bool _emit;

	protected PCTrail _activeTrail;

	protected Transform _t;

	private List<PCTrail> _fadingTrails;

	[SerializeField]
	private bool _flipU;

	[SerializeField]
	private bool _flipV;

	private Camera _cam;

	public bool Paused { get; set; }

	public float OverrideDeltaTime { get; set; }

	[ContextMenu("CLEARER")]
	public void NewClear()
	{
		if (Application.isPlaying)
		{
			ClearSystem(emitState: true);
		}
	}

	protected virtual void Awake()
	{
		_activeTrail = new PCTrail(MaxNumberOfPoints);
		_fadingTrails = new List<PCTrail>();
		_t = base.transform;
		_emit = Emit;
		_cam = Camera.main;
	}

	protected virtual void Start()
	{
	}

	protected virtual void Update()
	{
	}

	protected virtual float GetDeltaTime()
	{
		return (!Paused) ? Time.deltaTime : OverrideDeltaTime;
	}

	protected virtual void LateUpdate()
	{
		CheckEmitChange();
		float deltaTime = GetDeltaTime();
		ProcessUpdate(deltaTime);
		OverrideDeltaTime = 0f;
	}

	public void ProcessUpdate(float deltaTime)
	{
		if (_activeTrail != null)
		{
			UpdatePoints(deltaTime, _activeTrail);
			GenerateMesh(_activeTrail);
			DrawMesh(_activeTrail.Mesh, TrailData.TrailMaterial);
		}
		for (int num = _fadingTrails.Count - 1; num >= 0; num--)
		{
			if (_fadingTrails[num] == null || !_fadingTrails[num].Points.Any((PCTrailPoint a) => a.TimeActive() < TrailData.Lifetime))
			{
				if (_fadingTrails[num] != null)
				{
					_fadingTrails[num].Dispose();
				}
				_fadingTrails.RemoveAt(num);
			}
			else
			{
				UpdatePoints(deltaTime, _fadingTrails[num]);
				GenerateMesh(_fadingTrails[num]);
				DrawMesh(_fadingTrails[num].Mesh, TrailData.TrailMaterial);
			}
		}
	}

	protected virtual void OnDestroy()
	{
		if (_activeTrail != null)
		{
			_activeTrail.Dispose();
			_activeTrail = null;
		}
		if (_fadingTrails == null)
		{
			return;
		}
		foreach (PCTrail fadingTrail in _fadingTrails)
		{
			fadingTrail?.Dispose();
		}
		_fadingTrails.Clear();
	}

	protected virtual void OnStopEmit()
	{
	}

	protected virtual void OnStartEmit()
	{
	}

	protected virtual void Reset()
	{
		if (TrailData == null)
		{
			TrailData = new PCTrailRendererData();
		}
		TrailData.ColorOverLife = new Gradient();
		TrailData.Lifetime = 1f;
		TrailData.SizeOverLife = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
		MaxNumberOfPoints = 50;
	}

	protected virtual void InitialiseNewPoint(PCTrailPoint newPoint)
	{
	}

	protected virtual void UpdatePoint(PCTrailPoint point, float deltaTime)
	{
	}

	protected void AddPoint(PCTrailPoint newPoint, Vector3 pos)
	{
		if (_activeTrail != null)
		{
			newPoint.Position = pos;
			newPoint.PointNumber = ((_activeTrail.Points.Count != 0) ? (_activeTrail.Points[_activeTrail.Points.Count - 1].PointNumber + 1) : 0);
			InitialiseNewPoint(newPoint);
			newPoint.SetDistanceFromStart((_activeTrail.Points.Count != 0) ? (_activeTrail.Points[_activeTrail.Points.Count - 1].GetDistanceFromStart() + Vector3.Distance(_activeTrail.Points[_activeTrail.Points.Count - 1].Position, pos)) : 0f);
			if (TrailData.UseForwardOverride)
			{
				newPoint.Forward = ((!TrailData.ForwardOverrideRelative) ? TrailData.ForwardOverride.normalized : _t.TransformDirection(TrailData.ForwardOverride.normalized));
			}
			_activeTrail.Points.Add(newPoint);
		}
	}

	private void GenerateMesh(PCTrail trail)
	{
		trail.Mesh.Clear(keepVertexLayout: false);
		Vector3 vector = ((!(_cam != null)) ? Vector3.forward : _cam.transform.forward);
		if (TrailData.UseForwardOverride)
		{
			vector = TrailData.ForwardOverride.normalized;
		}
		trail.activePointCount = NumberOfActivePoints(trail);
		if (trail.activePointCount < 2)
		{
			return;
		}
		int num = 0;
		for (int i = 0; i < trail.Points.Count; i++)
		{
			PCTrailPoint pCTrailPoint = trail.Points[i];
			float time = pCTrailPoint.TimeActive() / TrailData.Lifetime;
			if (!(pCTrailPoint.TimeActive() > TrailData.Lifetime))
			{
				if (TrailData.UseForwardOverride && TrailData.ForwardOverrideRelative)
				{
					vector = pCTrailPoint.Forward;
				}
				Vector3 zero = Vector3.zero;
				zero = ((i >= trail.Points.Count - 1) ? Vector3.Cross((pCTrailPoint.Position - trail.Points[i - 1].Position).normalized, vector).normalized : Vector3.Cross((trail.Points[i + 1].Position - pCTrailPoint.Position).normalized, vector).normalized);
				Vector3 a = (pCTrailPoint.Position + pCTrailPoint.Position2) * 0.5f;
				Color color = ((!TrailData.StretchColorToFit) ? TrailData.ColorOverLife.Evaluate(time) : TrailData.ColorOverLife.Evaluate(1f - (float)num / (float)trail.activePointCount / 2f));
				float num2 = ((!TrailData.StretchSizeToFit) ? TrailData.SizeOverLife.Evaluate(time) : TrailData.SizeOverLife.Evaluate(1f - (float)num / (float)trail.activePointCount / 2f));
				if (pCTrailPoint.Position2 == Vector3.zero)
				{
					ref Vector3 reference = ref trail.verticies[num];
					reference = pCTrailPoint.Position + zero * num2;
				}
				else
				{
					ref Vector3 reference2 = ref trail.verticies[num];
					reference2 = Vector3.Lerp(a, pCTrailPoint.Position2, num2);
				}
				if (TrailData.MaterialTileLength <= 0f)
				{
					ref Vector2 reference3 = ref trail.uvs[num];
					reference3 = new Vector2((float)num / (float)trail.activePointCount / 2f, 0f);
				}
				else
				{
					ref Vector2 reference4 = ref trail.uvs[num];
					reference4 = new Vector2(pCTrailPoint.GetDistanceFromStart() / TrailData.MaterialTileLength, 0f);
				}
				if (_flipU)
				{
					trail.uvs[num].x = 1f - trail.uvs[num].x;
				}
				if (_flipV)
				{
					trail.uvs[num].y = 1f - trail.uvs[num].y;
				}
				trail.normals[num] = vector;
				trail.colors[num] = color;
				num++;
				if (pCTrailPoint.Position2 == Vector3.zero)
				{
					ref Vector3 reference5 = ref trail.verticies[num];
					reference5 = pCTrailPoint.Position - zero * num2;
				}
				else
				{
					ref Vector3 reference6 = ref trail.verticies[num];
					reference6 = Vector3.Lerp(a, pCTrailPoint.Position, num2);
				}
				if (TrailData.MaterialTileLength <= 0f)
				{
					ref Vector2 reference7 = ref trail.uvs[num];
					reference7 = new Vector2((float)num / (float)trail.activePointCount / 2f, 1f);
				}
				else
				{
					ref Vector2 reference8 = ref trail.uvs[num];
					reference8 = new Vector2(pCTrailPoint.GetDistanceFromStart() / TrailData.MaterialTileLength, 1f);
				}
				if (_flipU)
				{
					trail.uvs[num].x = 1f - trail.uvs[num].x;
				}
				if (_flipV)
				{
					trail.uvs[num].y = 1f - trail.uvs[num].y;
				}
				trail.normals[num] = vector;
				trail.colors[num] = color;
				num++;
			}
		}
		Vector2 vector2 = trail.verticies[num - 1];
		for (int j = num; j < trail.verticies.Length; j++)
		{
			ref Vector3 reference9 = ref trail.verticies[j];
			reference9 = vector2;
		}
		int num3 = 0;
		for (int k = 0; k < 2 * (trail.activePointCount - 1); k++)
		{
			if (k % 2 == 0)
			{
				trail.indicies[num3] = k;
				num3++;
				trail.indicies[num3] = k + 1;
				num3++;
				trail.indicies[num3] = k + 2;
			}
			else
			{
				trail.indicies[num3] = k + 2;
				num3++;
				trail.indicies[num3] = k + 1;
				num3++;
				trail.indicies[num3] = k;
			}
			num3++;
		}
		int num4 = trail.indicies[num3 - 1];
		for (int l = num3; l < trail.indicies.Length; l++)
		{
			trail.indicies[l] = num4;
		}
		trail.Mesh.vertices = trail.verticies;
		trail.Mesh.SetIndices(trail.indicies, MeshTopology.Triangles, 0);
		trail.Mesh.uv = trail.uvs;
		trail.Mesh.normals = trail.normals;
		trail.Mesh.colors = trail.colors;
	}

	private void DrawMesh(Mesh trailMesh, Material trailMaterial)
	{
		Graphics.DrawMesh(trailMesh, Matrix4x4.identity, trailMaterial, base.gameObject.layer);
	}

	private void UpdatePoints(float deltaTime, PCTrail line)
	{
		for (int i = 0; i < line.Points.Count; i++)
		{
			line.Points[i].Update(deltaTime);
			UpdatePoint(line.Points[i], deltaTime);
		}
	}

	private void CheckEmitChange()
	{
		if (_emit != Emit)
		{
			_emit = Emit;
			if (_emit)
			{
				OnStartEmit();
				_activeTrail = new PCTrail(MaxNumberOfPoints);
			}
			else
			{
				OnStopEmit();
				_fadingTrails.Add(_activeTrail);
				_activeTrail = null;
			}
		}
	}

	private int NumberOfActivePoints(PCTrail line)
	{
		int num = 0;
		for (int i = 0; i < line.Points.Count; i++)
		{
			if (line.Points[i].TimeActive() < TrailData.Lifetime)
			{
				num++;
			}
		}
		return num;
	}

	public void ClearSystem(bool emitState)
	{
		if (_activeTrail != null)
		{
			_activeTrail.Dispose();
			_activeTrail = null;
		}
		if (_fadingTrails != null)
		{
			foreach (PCTrail fadingTrail in _fadingTrails)
			{
				fadingTrail?.Dispose();
			}
			_fadingTrails.Clear();
		}
		Emit = emitState;
		_emit = !emitState;
		CheckEmitChange();
	}

	public int NumSegments()
	{
		int num = 0;
		if (_activeTrail != null && NumberOfActivePoints(_activeTrail) != 0)
		{
			num++;
		}
		return num + _fadingTrails.Count;
	}
}

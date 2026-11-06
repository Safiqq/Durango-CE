using UnityEngine;

namespace PigeonCoopToolkit.Effects.Trails;

[AddComponentMenu("Pigeon Coop Toolkit/Effects/Smoke Trail")]
public class SmokeTrail : TrailRenderer_Base
{
	public float MinVertexDistance = 0.1f;

	private Vector3 _lastPosition;

	private float _distanceMoved;

	public float RandomForceScale = 1f;

	private Transform _clampPosition;

	public void ClampFirstPointTo(Transform clampTo)
	{
		_clampPosition = clampTo;
	}

	public void ClearClamp()
	{
		_clampPosition = null;
	}

	protected override void Start()
	{
		base.Start();
		_lastPosition = _t.position;
	}

	protected override void Update()
	{
		if (_emit)
		{
			_distanceMoved += Vector3.Distance(_t.position, _lastPosition);
			if (_distanceMoved != 0f && _distanceMoved >= MinVertexDistance)
			{
				AddPoint(new SmokeTrailPoint(), _lastPosition);
				_distanceMoved = 0f;
			}
			_lastPosition = _t.position;
		}
		base.Update();
	}

	protected override void OnStartEmit()
	{
		_lastPosition = _t.position;
		_distanceMoved = 0f;
	}

	protected override void UpdatePoint(PCTrailPoint point, float deltaTime)
	{
		if ((bool)_clampPosition && point.PointNumber == 0)
		{
			point.Position = _clampPosition.position;
		}
	}

	protected override void Reset()
	{
		base.Reset();
		MinVertexDistance = 0.1f;
		RandomForceScale = 1f;
	}

	protected override void InitialiseNewPoint(PCTrailPoint newPoint)
	{
		((SmokeTrailPoint)newPoint).RandomVec = Random.onUnitSphere * RandomForceScale;
	}
}

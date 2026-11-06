using System.Collections.Generic;
using Messages;
using UnityEngine;

namespace Durango.UI.InGame;

public class FillBorderAlerts : AreaOfEffectAlert
{
	[SerializeField]
	private AnimationCurve _borderAlpha;

	[SerializeField]
	private Color _backgroundColor;

	[SerializeField]
	private Color _borderColor;

	private Texture2D _borderTexture;

	private Material _sharedBackground;

	private Material _sharedBorder;

	private Material _sharedAccent;

	private readonly Stack<FillBorderAlert> _pool = new Stack<FillBorderAlert>();

	private readonly Dictionary<int, FillBorderAlert> _alerts = new Dictionary<int, FillBorderAlert>();

	private const int MaxPooledViewers = 32;

	public static Shader AccentShader
	{
		get
		{
			Shader shader = Shader.Find("Durango/NGUI/Additive");
			if (shader == null || !shader.isSupported)
			{
				shader = Shader.Find("Durango/Particle/Legacy/Additive_No_ZTest");
			}
			if (!(shader != null) || !shader.isSupported)
			{
				return NGUITools.defaultShader;
			}
			return shader;
		}
	}

	public static Material CreateAccentMaterial(Texture texture)
	{
		Material material = new Material(AccentShader);
		material.mainTexture = texture;
		material.SetFloat("_Saturation", 1f);
		return material;
	}

	public override int ShowMask(AttackRangeMask mask, float startAt, float finishAt, float hideAt)
	{
		if (mask == null || !mask.IsValid() || mask.Runs.Length == 0)
		{
			return -1;
		}
		FillBorderAlert viewer = GetViewer();
		viewer.SetMask(mask);
		int num = (viewer.Id = AreaOfEffectVisualizer.GetNextId());
		int num2 = num;
		_alerts.Add(num2, viewer);
		viewer.Show(startAt, finishAt, startAt, hideAt);
		return num2;
	}

	public override int ShowCircle(Vector3 position, float radius, float startAt, float finishAt, float showAt, float hideAt)
	{
		return ShowArc(position, radius, 0f, 360f, startAt, finishAt, showAt, hideAt);
	}

	public override int ShowArc(Vector3 position, float radius, float startAngle, float endAngle, float startAt, float finishAt, float showAt, float hideAt)
	{
		FillBorderAlert viewer = GetViewer();
		viewer.SetArc(position, radius, startAngle, endAngle);
		int num = (viewer.Id = AreaOfEffectVisualizer.GetNextId());
		int num2 = num;
		_alerts.Add(num2, viewer);
		viewer.Show(startAt, finishAt, showAt, hideAt);
		return num2;
	}

	public override int ShowRect(Vector3 position, float width, float height, float angle, float startAt, float finishAt, float showAt, float hideAt)
	{
		FillBorderAlert viewer = GetViewer();
		viewer.SetRect(position, width, height, angle);
		int num = (viewer.Id = AreaOfEffectVisualizer.GetNextId());
		int num2 = num;
		_alerts.Add(num2, viewer);
		viewer.Show(startAt, finishAt, showAt, hideAt);
		return num2;
	}

	public override void Stop(int id, float delay)
	{
		if (_alerts.TryGetValue(id, out var value))
		{
			value.Stop(delay);
		}
	}

	public override void Move(int id, Vector3 position)
	{
		if (_alerts.TryGetValue(id, out var value))
		{
			value.transform.position = position;
		}
	}

	public override void Rotate(int id, float angle)
	{
		if (_alerts.TryGetValue(id, out var value))
		{
			value.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
		}
	}

	private Texture2D GetBorderTexture()
	{
		if (_borderTexture == null)
		{
			if (_borderAlpha.length > 0)
			{
				_borderTexture = new Texture2D(64, 1, TextureFormat.RGBA32, mipChain: false);
				_borderTexture.wrapMode = TextureWrapMode.Clamp;
				_borderTexture.filterMode = FilterMode.Bilinear;
				float time = _borderAlpha.keys[_borderAlpha.length - 1].time;
				for (int i = 0; i < _borderTexture.width; i++)
				{
					float num = ((float)i + 0.5f) / (float)_borderTexture.width;
					Color white = Color.white;
					white.a = _borderAlpha.Evaluate(num * time);
					_borderTexture.SetPixel(i, 0, white);
				}
				_borderTexture.Apply();
			}
			else
			{
				_borderTexture = Texture2D.whiteTexture;
			}
		}
		return _borderTexture;
	}

	private FillBorderAlert GetViewer()
	{
		while (_pool.Count > 0)
		{
			FillBorderAlert fillBorderAlert = _pool.Pop();
			if (fillBorderAlert != null)
			{
				return fillBorderAlert;
			}
		}
		FillBorderAlert fillBorderAlert2 = base.gameObject.AddChild<FillBorderAlert>();
		if (_sharedBackground == null)
		{
			_sharedBackground = new Material(NGUITools.defaultShader)
			{
				mainTexture = Texture2D.whiteTexture
			};
		}
		if (_sharedBorder == null)
		{
			_sharedBorder = CreateAccentMaterial(GetBorderTexture());
		}
		if (_sharedAccent == null)
		{
			_sharedAccent = CreateAccentMaterial(Texture2D.whiteTexture);
		}
		fillBorderAlert2.Init(_backgroundColor, _borderColor, GetBorderTexture(), _sharedBackground, _sharedBorder, _sharedAccent);
		fillBorderAlert2.gameObject.SetActive(value: false);
		fillBorderAlert2.Finished = OnFinishViewer;
		fillBorderAlert2.gameObject.hideFlags = HideFlags.DontSave;
		return fillBorderAlert2;
	}

	private void OnFinishViewer(FillBorderAlert viewer)
	{
		int id = viewer.Id;
		if (_alerts.Remove(id))
		{
			if (Application.isPlaying && _pool.Count < 32)
			{
				_pool.Push(viewer);
			}
			else if (Application.isPlaying)
			{
				Object.Destroy(viewer.gameObject);
			}
			else
			{
				Object.DestroyImmediate(viewer.gameObject);
			}
		}
	}

	private void OnDestroy()
	{
		if (Application.isPlaying)
		{
			Object.Destroy(_sharedBackground);
			Object.Destroy(_sharedBorder);
			Object.Destroy(_sharedAccent);
		}
		else
		{
			Object.DestroyImmediate(_sharedBackground);
			Object.DestroyImmediate(_sharedBorder);
			Object.DestroyImmediate(_sharedAccent);
		}
		foreach (FillBorderAlert value in _alerts.Values)
		{
			if (value != null)
			{
				value.Finished = null;
			}
		}
		foreach (FillBorderAlert item in _pool)
		{
			if (item != null)
			{
				item.Finished = null;
			}
		}
		_alerts.Clear();
		_pool.Clear();
		if (_borderTexture != null && _borderTexture != Texture2D.whiteTexture)
		{
			if (Application.isPlaying)
			{
				Object.Destroy(_borderTexture);
			}
			else
			{
				Object.DestroyImmediate(_borderTexture);
			}
		}
	}
}

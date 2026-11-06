using Durango.UI.InGame;
using UnityEngine;

public class OffServerVoiceRing : MonoBehaviour
{
	private const float Lifted = 15f;

	private const float Span = 100000f;

	private static Texture2D _borderTexture;

	private readonly FillBorderAlert[] _alerts = new FillBorderAlert[3];

	private Transform _target;

	private int _shown = -1;

	private float _radius = -1f;

	private bool _talking;

	public static OffServerVoiceRing Create(string name, bool small)
	{
		GameObject obj = new GameObject(name);
		Object.DontDestroyOnLoad(obj);
		return obj.AddComponent<OffServerVoiceRing>();
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
				_borderTexture.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Lerp(0.15f, 1f, num * num)));
			}
			_borderTexture.Apply();
		}
		return _borderTexture;
	}

	private FillBorderAlert Alert(int level)
	{
		if (_alerts[level] == null)
		{
			GameObject obj = new GameObject("range" + level);
			obj.transform.SetParent(base.transform, worldPositionStays: false);
			FillBorderAlert fillBorderAlert = obj.AddComponent<FillBorderAlert>();
			Color color = OffServerVoiceUI.LevelColor(level);
			fillBorderAlert.Init(new Color(color.r, color.g, color.b, 0.13f), new Color(color.r, color.g, color.b, 0.95f), BorderTexture());
			obj.SetActive(value: false);
			_alerts[level] = fillBorderAlert;
		}
		return _alerts[level];
	}

	public void Show(Transform target, float radius, int level, bool talking)
	{
		_target = target;
		if (target == null)
		{
			Hide();
			return;
		}
		level = Mathf.Clamp(level, 0, 2);
		FillBorderAlert fillBorderAlert = Alert(level);
		bool flag = _shown != level || !fillBorderAlert.gameObject.activeSelf;
		if (_shown >= 0 && _shown != level && _alerts[_shown] != null)
		{
			_alerts[_shown].Stop(0f);
		}
		Vector3 position = target.position + Vector3.up * 15f;
		if (flag || Mathf.Abs(radius - _radius) > 0.5f)
		{
			fillBorderAlert.SetArc(position, radius, 0f, 360f);
			_radius = radius;
		}
		if (flag || talking != _talking)
		{
			float num = AreaOfEffectVisualizer.Now();
			float num2 = (talking ? 0.8f : 0.45f);
			fillBorderAlert.Show(num - num2 * 100000f, num + (1f - num2) * 100000f, flag ? num : (num - 1f), num + 100000f);
		}
		_shown = level;
		_talking = talking;
		fillBorderAlert.transform.position = position;
	}

	private void Hide()
	{
		if (_shown >= 0 && _alerts[_shown] != null && _alerts[_shown].gameObject.activeSelf)
		{
			_alerts[_shown].Stop(0f);
		}
		_shown = -1;
	}

	private void LateUpdate()
	{
		if (_shown < 0)
		{
			return;
		}
		if (_target == null)
		{
			Hide();
			return;
		}
		FillBorderAlert fillBorderAlert = _alerts[_shown];
		if (fillBorderAlert != null)
		{
			fillBorderAlert.transform.position = _target.position + Vector3.up * 15f;
		}
	}
}

using System;
using Durango.System;
using Durango.Utils;
using UnityEngine;

public class OffServerRunButtonUI : MonoBehaviour
{
	private const int PanelDepth = 60;

	private const int TexSize = 128;

	private const string PrefX = "offserver_run_btn_rx";

	private const string PrefY = "offserver_run_btn_ry";

	private const string PrefSize = "offserver_run_btn_rsize";

	private static OffServerRunButtonUI _inst;

	private static Func<bool> _hudVisible;

	private static Texture2D _texWalk;

	private static Texture2D _texRun;

	private UIWidget _box;

	private UITexture _icon;

	private UILabel _label;

	private bool _shown;

	private int _placedX = int.MinValue;

	private int _placedY = int.MinValue;

	private int _placedSize;

	private static bool PcUi
	{
		get
		{
			try
			{
				return Platform.Instance.UsePCUI;
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static void Attach(Func<bool> hudVisible)
	{
		_hudVisible = hudVisible;
		if (!(_inst != null))
		{
			UIRoot uIRoot = (Singleton<UIManager>.HasInstance() ? Singleton<UIManager>.Instance().UIRoot : null);
			if (!(uIRoot == null))
			{
				GameObject obj = uIRoot.gameObject.AddChild();
				obj.name = "OffServerRunButton";
				_inst = obj.AddComponent<OffServerRunButtonUI>();
				_inst.Build();
			}
		}
	}

	private static void DefaultPlacement(out int x, out int y, out int size)
	{
		if (PcUi)
		{
			x = 424;
			y = 86;
			size = 60;
		}
		else
		{
			x = 470;
			y = 120;
			size = 104;
		}
	}

	private static void Placement(out int x, out int y, out int size)
	{
		DefaultPlacement(out x, out y, out size);
		try
		{
			string text = (PcUi ? "_pc" : "_mobile");
			x = PlayerPrefs.GetInt("offserver_run_btn_rx" + text, x);
			y = PlayerPrefs.GetInt("offserver_run_btn_ry" + text, y);
			size = PlayerPrefs.GetInt("offserver_run_btn_rsize" + text, size);
		}
		catch (Exception)
		{
		}
		size = Mathf.Clamp(size, 32, 320);
	}

	public static void SetPlacement(int x, int y, int size)
	{
		try
		{
			string text = (PcUi ? "_pc" : "_mobile");
			if (x == int.MinValue)
			{
				PlayerPrefs.DeleteKey("offserver_run_btn_rx" + text);
				PlayerPrefs.DeleteKey("offserver_run_btn_ry" + text);
				PlayerPrefs.DeleteKey("offserver_run_btn_rsize" + text);
			}
			else
			{
				PlayerPrefs.SetInt("offserver_run_btn_rx" + text, x);
				PlayerPrefs.SetInt("offserver_run_btn_ry" + text, y);
				if (size > 0)
				{
					PlayerPrefs.SetInt("offserver_run_btn_rsize" + text, size);
				}
			}
			PlayerPrefs.Save();
		}
		catch (Exception)
		{
		}
		if (_inst != null)
		{
			_inst.Place(force: true);
		}
	}

	public static string Describe()
	{
		Placement(out var x, out var y, out var size);
		string text = ((_inst == null) ? "ui none" : ("ui shown " + _inst._shown + " hud " + HudVisible()));
		return text + " pc " + PcUi + " pos " + x + "," + y + " size " + size;
	}

	private static bool HudVisible()
	{
		if (_hudVisible == null)
		{
			return true;
		}
		try
		{
			return _hudVisible();
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void Build()
	{
		base.gameObject.AddComponent<UIPanel>().depth = 60;
		_box = base.gameObject.AddWidget<UIWidget>(0);
		_box.name = "Box";
		_icon = _box.gameObject.AddWidget<UITexture>(2);
		_icon.name = "Icon";
		_icon.mainTexture = Tex(running: false);
		NGUITools.AddWidgetCollider(_icon.gameObject);
		UIEventListener uIEventListener = UIEventListener.Get(_icon.gameObject);
		uIEventListener.onPress = (UIEventListener.BoolDelegate)Delegate.Combine(uIEventListener.onPress, (UIEventListener.BoolDelegate)delegate(GameObject go, bool pressed)
		{
			OffServerRunButton.Press(pressed);
		});
		_label = _box.gameObject.AddWidget<UILabel>(3);
		_label.name = "Label";
		UILabel uILabel = FindStyle();
		if (uILabel != null)
		{
			if (uILabel.bitmapFont != null)
			{
				_label.bitmapFont = uILabel.bitmapFont;
			}
			else
			{
				_label.trueTypeFont = uILabel.trueTypeFont;
			}
		}
		_label.alignment = NGUIText.Alignment.Center;
		_label.overflowMethod = UILabel.Overflow.ShrinkContent;
		_label.effectStyle = UILabel.Effect.Outline;
		_label.effectColor = new Color(0f, 0f, 0f, 0.8f);
		_label.color = Color.white;
		Place(force: true);
		OffServerHudLayout.Register("run", _box.transform, "ว\u0e34\u0e48ง / Run", "movement");
		_box.gameObject.SetActive(value: false);
		_shown = false;
		OffServerRunButton.Changed += Refresh;
	}

	private void OnDestroy()
	{
		OffServerRunButton.Changed -= Refresh;
		if (_inst == this)
		{
			_inst = null;
		}
	}

	private void Place(bool force)
	{
		Placement(out var x, out var y, out var size);
		if (!OffServerHudLayout.HasPlacement("run") && (force || x != _placedX || y != _placedY || size != _placedSize))
		{
			_placedX = x;
			_placedY = y;
			_placedSize = size;
			UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(UIBase.AnchorType.Base);
			int num = size / 2;
			if (rootAnchor != null)
			{
				_box.SetAnchor(rootAnchor.gameObject, 1f, -x - num, 0f, y - num, 1f, -x + num, 0f, y + num);
			}
			else
			{
				_box.width = size;
				_box.height = size;
			}
			_icon.width = size;
			_icon.height = size;
			_icon.transform.localPosition = Vector3.zero;
			_label.fontSize = Mathf.Max(14, Mathf.RoundToInt((float)size * 0.24f));
			_label.width = size + 40;
			_label.height = _label.fontSize + 8;
			_label.transform.localPosition = new Vector3(0f, (float)(-num) - (float)_label.fontSize * 0.6f, 0f);
			_label.text = OffServerRunButton.Label();
			_box.ResetAndUpdateAnchors();
		}
	}

	private void Update()
	{
		bool flag = OffServerRunButton.Available && HudVisible();
		if (flag != _shown)
		{
			_shown = flag;
			_box.gameObject.SetActive(flag);
			if (flag)
			{
				Place(force: true);
				Refresh();
			}
		}
	}

	private void Refresh()
	{
		if (!(_icon == null))
		{
			bool running = OffServerRunButton.Running;
			bool desired = OffServerRunButton.Desired;
			_icon.mainTexture = Tex(running);
			_icon.alpha = ((running == desired) ? 1f : 0.7f);
			_label.text = OffServerRunButton.Label();
			_label.color = (running ? new Color(1f, 0.78f, 0.35f, 1f) : Color.white);
		}
	}

	private static UILabel FindStyle()
	{
		try
		{
			if (UIManager.MessageBox != null)
			{
				UILabel[] componentsInChildren = UIManager.MessageBox.GetComponentsInChildren<UILabel>(includeInactive: true);
				foreach (UILabel uILabel in componentsInChildren)
				{
					if (uILabel.trueTypeFont != null || uILabel.bitmapFont != null)
					{
						return uILabel;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	private static Texture2D Tex(bool running)
	{
		if (running)
		{
			if (_texRun == null)
			{
				_texRun = Draw(new Color(0.98f, 0.52f, 0.1f, 0.92f), new Color(1f, 0.92f, 0.7f, 1f));
			}
			return _texRun;
		}
		if (_texWalk == null)
		{
			_texWalk = Draw(new Color(0.05f, 0.06f, 0.08f, 0.62f), new Color(1f, 1f, 1f, 0.85f));
		}
		return _texWalk;
	}

	private static Texture2D Draw(Color fill, Color ring)
	{
		Texture2D texture2D = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.name = "OffServerRunIcon";
		Color32[] array = new Color32[16384];
		Vector2[] array2 = new Vector2[18]
		{
			new Vector2(70f, 80f),
			new Vector2(60f, 54f),
			new Vector2(68f, 75f),
			new Vector2(83f, 66f),
			new Vector2(83f, 66f),
			new Vector2(92f, 77f),
			new Vector2(68f, 75f),
			new Vector2(53f, 71f),
			new Vector2(53f, 71f),
			new Vector2(45f, 58f),
			new Vector2(60f, 54f),
			new Vector2(77f, 43f),
			new Vector2(77f, 43f),
			new Vector2(80f, 24f),
			new Vector2(60f, 54f),
			new Vector2(49f, 38f),
			new Vector2(49f, 38f),
			new Vector2(32f, 35f)
		};
		Vector2 b = new Vector2(77f, 95f);
		Vector2[] array3 = new Vector2[6]
		{
			new Vector2(20f, 82f),
			new Vector2(36f, 82f),
			new Vector2(16f, 68f),
			new Vector2(34f, 68f),
			new Vector2(20f, 54f),
			new Vector2(34f, 54f)
		};
		for (int i = 0; i < 128; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				Vector2 vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
				float num = Vector2.Distance(vector, new Vector2(64f, 64f));
				float num2 = Mathf.Clamp01(61f - num);
				Color dst = fill;
				dst.a *= num2;
				float num3 = Mathf.Clamp01(1f - Mathf.Abs(num - 58.5f) / 2.5f) * num2;
				dst = Over(dst, new Color(ring.r, ring.g, ring.b, ring.a * num3));
				float num4 = Vector2.Distance(vector, b) - 9.5f;
				for (int k = 0; k < array2.Length; k += 2)
				{
					num4 = Mathf.Min(num4, SegDist(vector, array2[k], array2[k + 1]) - 4.6f);
				}
				float num5 = Mathf.Clamp01(0.5f - num4);
				dst = Over(dst, new Color(1f, 1f, 1f, num5 * num2));
				float num6 = float.MaxValue;
				for (int l = 0; l < array3.Length; l += 2)
				{
					num6 = Mathf.Min(num6, SegDist(vector, array3[l], array3[l + 1]) - 2.6f);
				}
				dst = Over(dst, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - num6) * 0.75f * num2));
				array[i * 128 + j] = dst;
			}
		}
		texture2D.SetPixels32(array);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		return texture2D;
	}

	private static Color Over(Color dst, Color src)
	{
		float num = src.a + dst.a * (1f - src.a);
		if (num <= 0.0001f)
		{
			return new Color(0f, 0f, 0f, 0f);
		}
		return new Color((src.r * src.a + dst.r * dst.a * (1f - src.a)) / num, (src.g * src.a + dst.g * dst.a * (1f - src.a)) / num, (src.b * src.a + dst.b * dst.a * (1f - src.a)) / num, num);
	}

	private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
	{
		Vector2 vector = b - a;
		float num = Mathf.Clamp01(Vector2.Dot(p - a, vector) / Mathf.Max(0.0001f, vector.sqrMagnitude));
		return Vector2.Distance(p, a + vector * num);
	}
}

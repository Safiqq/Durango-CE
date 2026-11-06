using System;
using Durango.UI.Control;
using UnityEngine;

namespace Durango.UI;

public class ExpertStableAreaWaveSprite : CustomFillSprite
{
	private UISpriteData _wave1;

	private UISpriteData _wave2;

	private UISpriteData _whiteSprite;

	private const string WaveSprite1 = "stable_area_wave_01";

	private const string WaveSprite2 = "stable_area_wave_02";

	private const string WhiteSprite = "map_square";

	private Vector3[] _waveCorners = new Vector3[4];

	[SerializeField]
	private float _lean;

	[SerializeField]
	private float _leanMargin;

	protected override void OnStart()
	{
		base.OnStart();
		UISpriteManager uISpriteManager = ResourceSingleton<UISpriteManager>.Instance();
		_wave1 = uISpriteManager.GetSprite("stable_area_wave_01");
		_wave2 = uISpriteManager.GetSprite("stable_area_wave_02");
		_whiteSprite = uISpriteManager.GetSprite("map_square");
	}

	public override void OnFill(UIGeometry.Arguments arguments)
	{
		if (_wave1 == null || _wave2 == null)
		{
			return;
		}
		float f = MathF.PI / 180f * _lean;
		BetterList<Vector3> verts = arguments.verts;
		BetterList<Vector2> uvs = arguments.uvs;
		BetterList<Color> cols = arguments.cols;
		int size = verts.size;
		float num = _wave1.width + _wave1.paddingLeft + _wave1.paddingRight;
		float num2 = Mathf.Tan(f);
		Vector2 vector = new Vector2(num + (float)base.height * 0.5f * num2, (float)base.height * 0.5f);
		Vector3[] array = localCorners;
		Rect rect = new Rect((Vector2)array[0], (Vector2)(array[2] - array[0]));
		Color white = Color.white;
		for (int i = 0; i < 4; i++)
		{
			Vector3 vector2 = Vector3.zero;
			UISpriteData sprite = null;
			int num3 = 0;
			switch (i)
			{
			case 0:
				vector2 = new Vector2(rect.xMin, rect.yMin);
				sprite = _wave2;
				num3 = 2;
				break;
			case 1:
				vector2 = new Vector2(rect.xMin + _leanMargin, rect.yMin + vector.y);
				sprite = _wave1;
				num3 = 2;
				break;
			case 2:
				vector2 = new Vector2(rect.xMax, rect.yMax) - vector;
				sprite = _wave2;
				break;
			case 3:
				vector2 = new Vector2(rect.xMax - _leanMargin, rect.yMax - vector.y) - vector;
				sprite = _wave1;
				break;
			}
			_waveCorners[num3 % 4] = vector2;
			_waveCorners[(num3 + 2) % 4] = vector2 + new Vector3(vector.x, vector.y);
			_waveCorners[(num3 + 1) % 4] = vector2 + new Vector3(vector.x - num, vector.y);
			_waveCorners[(num3 + 3) % 4] = vector2 + new Vector3(num, 0f);
			DrawSprite(verts, uvs, cols, sprite, _waveCorners, white);
		}
		for (int j = 0; j < 2; j++)
		{
			switch (j)
			{
			case 0:
				_waveCorners[0] = new Vector3(rect.xMin + num, rect.yMin);
				_waveCorners[1] = _waveCorners[0] + new Vector3(vector.y * num2, vector.y);
				_waveCorners[3] = new Vector2(rect.xMax - _leanMargin, rect.yMax - vector.y) - vector;
				_waveCorners[2] = _waveCorners[3] + new Vector3(vector.y * num2, vector.y);
				break;
			case 1:
				_waveCorners[0] = new Vector2(rect.xMin + _leanMargin + num, rect.yMin + vector.y);
				_waveCorners[1] = _waveCorners[0] + new Vector3(vector.y * num2, vector.y);
				_waveCorners[3] = new Vector2(rect.xMax, rect.yMax) - vector;
				_waveCorners[2] = _waveCorners[3] + new Vector3(vector.y * num2, vector.y);
				break;
			}
			DrawSprite(verts, uvs, cols, _whiteSprite, _waveCorners, white);
		}
		if (onPostFill != null)
		{
			onPostFill(this, size, arguments);
		}
	}
}

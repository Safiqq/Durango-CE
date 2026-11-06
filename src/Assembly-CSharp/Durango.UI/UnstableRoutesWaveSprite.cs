using System;
using Durango.UI.Control;
using UnityEngine;

namespace Durango.UI;

public class UnstableRoutesWaveSprite : CustomFillSprite
{
	private UISpriteData _waveSprite;

	private UISpriteData _whiteSprite;

	private const string WaveSprite = "stable_area_wave_02";

	private const string WhiteSprite = "map_square";

	private Vector3[] _waveCorners = new Vector3[4];

	[SerializeField]
	private float _lean;

	public float MinimumWidth
	{
		get
		{
			UISpriteData waveSprite = _waveSprite;
			if (waveSprite == null)
			{
				return 0f;
			}
			Vector2 vector = new Vector2(waveSprite.width + waveSprite.paddingLeft + waveSprite.paddingRight, waveSprite.height + waveSprite.paddingBottom + waveSprite.paddingTop);
			float f = MathF.PI / 180f * _lean;
			return vector.x + (float)base.height * Mathf.Tan(f);
		}
	}

	protected override void OnStart()
	{
		base.OnStart();
		_waveSprite = ResourceSingleton<UISpriteManager>.Instance().GetSprite("stable_area_wave_02");
		_whiteSprite = ResourceSingleton<UISpriteManager>.Instance().GetSprite("map_square");
	}

	public override void OnFill(UIGeometry.Arguments arguments)
	{
		UISpriteData waveSprite = _waveSprite;
		if (waveSprite == null)
		{
			return;
		}
		Vector2 vector = new Vector2(waveSprite.width + waveSprite.paddingLeft + waveSprite.paddingRight, waveSprite.height + waveSprite.paddingBottom + waveSprite.paddingTop);
		float minimumWidth = MinimumWidth;
		if (!((float)base.width < minimumWidth))
		{
			BetterList<Vector3> verts = arguments.verts;
			BetterList<Vector2> uvs = arguments.uvs;
			BetterList<Color> cols = arguments.cols;
			int size = verts.size;
			Vector3[] array = localCorners;
			Rect rect = new Rect((Vector2)array[0], (Vector2)(array[2] - array[0]));
			Color white = Color.white;
			Vector3 vector2 = new Vector3(Mathf.Max(rect.xMax, rect.xMin + minimumWidth), rect.yMax);
			_waveCorners[3] = vector2;
			_waveCorners[0] = new Vector3(vector2.x - vector.x, vector2.y);
			_waveCorners[1] = new Vector3(vector2.x - minimumWidth, rect.yMin);
			_waveCorners[2] = new Vector3(vector2.x - minimumWidth + vector.x, rect.yMin);
			DrawSprite(verts, uvs, cols, waveSprite, _waveCorners, white);
			_waveCorners[2] = _waveCorners[0];
			_waveCorners[3] = _waveCorners[1];
			_waveCorners[1] = new Vector3(rect.xMin, rect.yMax);
			_waveCorners[0] = new Vector3(rect.xMin, rect.yMin);
			DrawSprite(verts, uvs, cols, _whiteSprite, _waveCorners, white);
			if (onPostFill != null)
			{
				onPostFill(this, size, arguments);
			}
		}
	}
}

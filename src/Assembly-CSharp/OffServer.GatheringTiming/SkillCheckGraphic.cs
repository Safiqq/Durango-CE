using System;
using UnityEngine;

namespace OffServer.GatheringTiming;

public sealed class SkillCheckGraphic : UIWidget
{
	public Material SharedMaterial;

	public double Angle = 90.0;

	public double Target = 210.0;

	public float Progress;

	public float Visibility = 1f;

	public bool Failure;

	public bool DrawProgress = true;

	public double GoodHalf = 25.0;

	public double GreatHalf = 4.5;

	public float Glow;

	public float Ripple = -1f;

	public bool Trail;

	public Color HaloColor = Gold;

	public static readonly Color Gold = new Color(1f, 0.83f, 0.42f, 1f);

	private static readonly Color PaleGold = new Color(1f, 0.91f, 0.64f, 1f);

	private static readonly Color Cream = new Color(0.95f, 0.89f, 0.76f, 1f);

	private static readonly Color Ember = new Color(1f, 0.23f, 0.25f, 1f);

	private static readonly Color White = new Color(0.94f, 0.94f, 0.93f, 1f);

	private static readonly Color Red = new Color(0.87f, 0.1f, 0.12f, 1f);

	public override Material material => SharedMaterial;

	public override Texture mainTexture => Texture2D.whiteTexture;

	private Color C(Color c)
	{
		c.a *= Visibility;
		return c;
	}

	private static Color WithAlpha(Color c, float alpha)
	{
		c.a = Mathf.Clamp01(alpha);
		return c;
	}

	private void Quad(UIGeometry.Arguments a, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color c)
	{
		Vector2[] array = new Vector2[4] { p0, p1, p2, p3 };
		for (int i = 0; i < array.Length; i++)
		{
			Vector2 vector = array[i];
			a.verts.Add(new Vector3(vector.x, vector.y));
			a.uvs.Add(new Vector2(0.5f, 0.5f));
			a.cols.Add(c);
		}
	}

	private void Line(UIGeometry.Arguments a, Vector2 start, Vector2 end, float thickness, Color c)
	{
		Vector2 normalized = (end - start).normalized;
		Vector2 vector = new Vector2(0f - normalized.y, normalized.x) * thickness * 0.5f;
		Quad(a, start - vector, start + vector, end + vector, end - vector, c);
	}

	private static Vector2 Point(double angle, float radius)
	{
		double num = angle * Math.PI / 180.0;
		return new Vector2((float)Math.Cos(num) * radius, (float)Math.Sin(num) * radius + 35f);
	}

	private void Arc(UIGeometry.Arguments a, double from, double to, float inner, float outer, Color c)
	{
		int num = Math.Max(1, (int)Math.Ceiling(Math.Abs(to - from) / 1.5));
		for (int i = 0; i < num; i++)
		{
			double angle = from + (to - from) * (double)i / (double)num;
			double angle2 = from + (to - from) * (double)(i + 1) / (double)num;
			Quad(a, Point(angle, inner), Point(angle, outer), Point(angle2, outer), Point(angle2, inner), c);
		}
	}

	private void Rect(UIGeometry.Arguments a, float x0, float y0, float x1, float y1, Color c)
	{
		Quad(a, new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), c);
	}

	public override void OnFill(UIGeometry.Arguments a)
	{
		Arc(a, 0.0, 360.0, 0f, 132f, C(new Color(0f, 0f, 0f, 0.55f)));
		Arc(a, 0.0, 360.0, 132f, 142f, C(new Color(0f, 0f, 0f, 0.25f)));
		for (int i = 0; i < 4; i++)
		{
			Arc(a, 0.0, 360.0, 114f + (float)i * 3.5f, 117.5f + (float)i * 3.5f, C(WithAlpha(HaloColor, Glow * (0.32f - (float)i * 0.07f))));
		}
		if (Ripple >= 0f)
		{
			float num = 106f * (0.9f + 0.35f * Ripple);
			Arc(a, 0.0, 360.0, num - 1.5f, num + 1.5f, C(WithAlpha(Cream, 0.9f * (1f - Ripple))));
		}
		Arc(a, 0.0, 360.0, 105f, 106.6f, C(new Color(0.65f, 0.65f, 0.64f, 0.85f)));
		double num2 = Target - GoodHalf;
		double num3 = Target + GoodHalf;
		double num4 = num3 - GreatHalf * 2.0;
		for (int j = 0; j < 3; j++)
		{
			Arc(a, num4 - 1.5, num3 + 1.5, 96 - j * 3, 114 + j * 3, C(WithAlpha(Gold, (0.22f - (float)j * 0.06f) * (0.6f + 0.4f * Glow))));
		}
		Arc(a, num2, num3, 97.5f, 99.5f, C(White));
		Arc(a, num2, num3, 111f, 113f, C(White));
		Line(a, Point(num2, 98f), Point(num2, 113f), 1.8f, C(White));
		Line(a, Point(num3, 98f), Point(num3, 113f), 1.8f, C(White));
		Arc(a, num4, num3, 99.5f, 111f, C(PaleGold));
		if (Trail)
		{
			for (int k = 0; k < 6; k++)
			{
				Arc(a, Angle + (double)(k * 6), Angle + (double)((k + 1) * 6), 101.5f, 108.5f, C(WithAlpha(Ember, 0.5f * (1f - (float)k / 6f))));
			}
		}
		Line(a, Point(Angle + 180.0, 9f), Point(Angle, 117f), 7f, C(WithAlpha(Ember, 0.35f)));
		Line(a, Point(Angle + 180.0, 9f), Point(Angle, 117f), 2.5f, C(Red));
		Arc(a, 0.0, 360.0, 0f, 4f, C(Red));
		Rect(a, -29f, -42f, 29f, -9.5f, C(new Color(0f, 0f, 0f, 0.35f)));
		Rect(a, -29f, -11f, 29f, -9.5f, C(White));
		Rect(a, -29f, -42f, 29f, -40.5f, C(White));
		Rect(a, -29f, -41f, -27.5f, -10f, C(White));
		Rect(a, 27.5f, -41f, 29f, -10f, C(White));
		if (DrawProgress)
		{
			Rect(a, -195f, -174f, 195f, -164f, new Color(0.24f, 0.24f, 0.24f, 1f));
			Rect(a, -195f, -174f, -195f + 390f * Mathf.Clamp01(Progress), -164f, Failure ? Red : White);
		}
	}
}

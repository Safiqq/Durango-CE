using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace DurangoLauncher;

internal static class SvgPath
{
	private static readonly Regex Token = new Regex("[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\\d*\\.\\d+|\\d+\\.?)(?:[eE][-+]?\\d+)?", RegexOptions.Compiled);

	public static void AddTo(GraphicsPath path, string d)
	{
		List<string> toks = (from m in Token.Matches(d)
			select m.Value).ToList();
		int i = 0;
		char c = 'M';
		PointF pointF = PointF.Empty;
		PointF pointF2 = PointF.Empty;
		PointF pointF3 = PointF.Empty;
		char value = ' ';
		while (i < toks.Count)
		{
			if (IsCmd(i))
			{
				c = toks[i++][0];
			}
			bool flag = char.IsLower(c);
			float num = (flag ? pointF.X : 0f);
			float num2 = (flag ? pointF.Y : 0f);
			switch (char.ToUpperInvariant(c))
			{
			case 'M':
				pointF = new PointF(num + N(), num2 + N());
				pointF2 = pointF;
				path.StartFigure();
				c = (flag ? 'l' : 'L');
				value = 'M';
				continue;
			case 'L':
			{
				PointF pointF4 = new PointF(num + N(), num2 + N());
				path.AddLine(pointF, pointF4);
				pointF = pointF4;
				break;
			}
			case 'H':
			{
				PointF pointF12 = new PointF(num + N(), pointF.Y);
				path.AddLine(pointF, pointF12);
				pointF = pointF12;
				break;
			}
			case 'V':
			{
				PointF pointF11 = new PointF(pointF.X, num2 + N());
				path.AddLine(pointF, pointF11);
				pointF = pointF11;
				break;
			}
			case 'C':
			{
				PointF pointF8 = new PointF(num + N(), num2 + N());
				PointF pointF9 = new PointF(num + N(), num2 + N());
				PointF pointF10 = new PointF(num + N(), num2 + N());
				path.AddBezier(pointF, pointF8, pointF9, pointF10);
				pointF3 = pointF9;
				pointF = pointF10;
				break;
			}
			case 'S':
			{
				PointF pointF15 = ("CcSs".Contains(value) ? new PointF(2f * pointF.X - pointF3.X, 2f * pointF.Y - pointF3.Y) : pointF);
				PointF pointF16 = new PointF(num + N(), num2 + N());
				PointF pointF17 = new PointF(num + N(), num2 + N());
				path.AddBezier(pointF, pointF15, pointF16, pointF17);
				pointF3 = pointF16;
				pointF = pointF17;
				break;
			}
			case 'Q':
			{
				PointF pointF13 = new PointF(num + N(), num2 + N());
				PointF pointF14 = new PointF(num + N(), num2 + N());
				AddQuad(path, pointF, pointF13, pointF14);
				pointF3 = pointF13;
				pointF = pointF14;
				break;
			}
			case 'T':
			{
				PointF pointF6 = ("QqTt".Contains(value) ? new PointF(2f * pointF.X - pointF3.X, 2f * pointF.Y - pointF3.Y) : pointF);
				PointF pointF7 = new PointF(num + N(), num2 + N());
				AddQuad(path, pointF, pointF6, pointF7);
				pointF3 = pointF6;
				pointF = pointF7;
				break;
			}
			case 'A':
			{
				float rx = N();
				float ry = N();
				float rotDeg = N();
				bool large = N() != 0f;
				bool sweep = N() != 0f;
				PointF pointF5 = new PointF(num + N(), num2 + N());
				AddArc(path, pointF, rx, ry, rotDeg, large, sweep, pointF5);
				pointF = pointF5;
				break;
			}
			case 'Z':
				path.CloseFigure();
				pointF = pointF2;
				if (i < toks.Count && !IsCmd(i))
				{
					i++;
				}
				break;
			default:
				i++;
				break;
			}
			value = c;
		}
		bool IsCmd(int k)
		{
			if (k < toks.Count)
			{
				return char.IsLetter(toks[k][0]);
			}
			return false;
		}
		float N()
		{
			return float.Parse(toks[i++], CultureInfo.InvariantCulture);
		}
	}

	private static void AddQuad(GraphicsPath path, PointF p0, PointF q, PointF p1)
	{
		path.AddBezier(p0, new PointF(p0.X + 2f / 3f * (q.X - p0.X), p0.Y + 2f / 3f * (q.Y - p0.Y)), new PointF(p1.X + 2f / 3f * (q.X - p1.X), p1.Y + 2f / 3f * (q.Y - p1.Y)), p1);
	}

	private static void AddArc(GraphicsPath path, PointF p0, float rx, float ry, float rotDeg, bool large, bool sweep, PointF p1)
	{
		if (p0 == p1)
		{
			return;
		}
		rx = Math.Abs(rx);
		ry = Math.Abs(ry);
		if (rx == 0f || ry == 0f)
		{
			path.AddLine(p0, p1);
			return;
		}
		double num = (double)rotDeg * Math.PI / 180.0;
		double cos = Math.Cos(num);
		double sin = Math.Sin(num);
		double num2 = (double)(p0.X - p1.X) / 2.0;
		double num3 = (double)(p0.Y - p1.Y) / 2.0;
		double num4 = cos * num2 + sin * num3;
		double num5 = (0.0 - sin) * num2 + cos * num3;
		double num6 = num4 * num4 / (double)(rx * rx) + num5 * num5 / (double)(ry * ry);
		double Rx = rx;
		double Ry = ry;
		if (num6 > 1.0)
		{
			Rx *= Math.Sqrt(num6);
			Ry *= Math.Sqrt(num6);
		}
		double num7 = Rx * Rx * Ry * Ry - Rx * Rx * num5 * num5 - Ry * Ry * num4 * num4;
		double num8 = Rx * Rx * num5 * num5 + Ry * Ry * num4 * num4;
		double num9 = (double)((large != sweep) ? 1 : (-1)) * Math.Sqrt(Math.Max(0.0, num7 / num8));
		double num10 = num9 * Rx * num5 / Ry;
		double num11 = (0.0 - num9) * Ry * num4 / Rx;
		double cx = cos * num10 - sin * num11 + (double)(p0.X + p1.X) / 2.0;
		double cy = sin * num10 + cos * num11 + (double)(p0.Y + p1.Y) / 2.0;
		double num12 = Angle(1.0, 0.0, (num4 - num10) / Rx, (num5 - num11) / Ry);
		double num13 = Angle((num4 - num10) / Rx, (num5 - num11) / Ry, (0.0 - num4 - num10) / Rx, (0.0 - num5 - num11) / Ry);
		if (!sweep && num13 > 0.0)
		{
			num13 -= Math.PI * 2.0;
		}
		else if (sweep && num13 < 0.0)
		{
			num13 += Math.PI * 2.0;
		}
		int num14 = Math.Max(1, (int)Math.Ceiling(Math.Abs(num13) / (Math.PI / 2.0) - 1E-09));
		double num15 = num13 / (double)num14;
		double num16 = 1.3333333333333333 * Math.Tan(num15 / 4.0);
		for (int i = 0; i < num14; i++)
		{
			double num17 = num12 + (double)i * num15;
			double a = num17 + num15;
			PointF pointF = ((i == 0) ? p0 : E(num17));
			PointF pointF2 = ((i == num14 - 1) ? p1 : E(a));
			PointF pointF3 = D(num17);
			PointF pointF4 = D(a);
			path.AddBezier(pointF, new PointF(pointF.X + (float)num16 * pointF3.X, pointF.Y + (float)num16 * pointF3.Y), new PointF(pointF2.X - (float)num16 * pointF4.X, pointF2.Y - (float)num16 * pointF4.Y), pointF2);
		}
		static double Angle(double ux, double uy, double vx, double vy)
		{
			return Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
		}
		PointF D(double num18)
		{
			return new PointF((float)((0.0 - Rx) * Math.Sin(num18) * cos - Ry * Math.Cos(num18) * sin), (float)((0.0 - Rx) * Math.Sin(num18) * sin + Ry * Math.Cos(num18) * cos));
		}
		PointF E(double num18)
		{
			return new PointF((float)(cx + Rx * Math.Cos(num18) * cos - Ry * Math.Sin(num18) * sin), (float)(cy + Rx * Math.Cos(num18) * sin + Ry * Math.Sin(num18) * cos));
		}
	}
}

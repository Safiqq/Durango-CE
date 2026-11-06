using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DurangoLauncher;

internal static class Ui
{
	public static readonly Color K0 = Hex(789010);

	public static readonly Color K1 = Hex(1380894);

	public static readonly Color K2 = Hex(1906985);

	public static readonly Color K3 = Hex(2827581);

	public static readonly Color PurpleDark = Hex(3351659);

	public static readonly Color PurpleLight = Hex(12100863);

	public static readonly Color Yellow = Hex(16106776);

	public static readonly Color YellowHover = Hex(16767050);

	public static readonly Color OnYellow = Hex(1840384);

	public static readonly Color Text = Hex(16118523);

	public static readonly Color TextDim = Hex(11577796);

	public static readonly Color TextMute = Hex(8024977);

	public static readonly Color Good = Hex(6018698);

	public static readonly Color Bad = Hex(15761518);

	private static readonly PrivateFontCollection Embedded = new PrivateFontCollection();

	private static readonly bool Loaded = LoadFont("Kanit-Regular.ttf") & LoadFont("Kanit-Medium.ttf");

	public static readonly FontFamily? Regular = Family("Kanit");

	public static readonly FontFamily? Medium = Family("Kanit Medium");

	public static readonly string Fallback = (Has("Leelawadee UI") ? "Leelawadee UI" : "Tahoma");

	private static readonly StringFormat WrapFormat = new StringFormat
	{
		Trimming = (StringTrimming)3,
		FormatFlags = (StringFormatFlags)8192
	};

	public static bool FontsLoaded
	{
		get
		{
			if (Loaded && Regular != null)
			{
				return Medium != null;
			}
			return false;
		}
	}

	private static Color Hex(int rgb)
	{
		return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
	}

	public static Color A(Color c, int alpha)
	{
		return Color.FromArgb(Math.Clamp(alpha, 0, 255), c.R, c.G, c.B);
	}

	[DllImport("gdi32.dll")]
	private static extern nint AddFontMemResourceEx(nint pbFont, uint cbFont, nint pdv, ref uint pcFonts);

	private static bool Has(string family)
	{
		return FontFamily.Families.Any((FontFamily f) => string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));
	}

	private static FontFamily? Family(string name)
	{
		return ((IEnumerable<FontFamily>)((FontCollection)Embedded).Families).FirstOrDefault((Func<FontFamily, bool>)((FontFamily f) => f.Name == name));
	}

	private static bool LoadFont(string name)
	{
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
			if (stream == null)
			{
				return false;
			}
			byte[] array = new byte[stream.Length];
			stream.ReadExactly(array);
			nint num = Marshal.AllocCoTaskMem(array.Length);
			Marshal.Copy(array, 0, num, array.Length);
			Embedded.AddMemoryFont((IntPtr)num, array.Length);
			uint pcFonts = 0u;
			AddFontMemResourceEx(num, (uint)array.Length, IntPtr.Zero, ref pcFonts);
			return true;
		}
		catch (Exception ex)
		{
			Log.Write("โหลดฟอนต\u0e4c " + name + " ไม\u0e48ได\u0e49: " + ex.Message);
			return false;
		}
	}

	public static Font NewFont(float px, bool medium)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		FontFamily val = (medium ? Medium : Regular);
		if (val != null)
		{
			return new Font(val, px, (FontStyle)0, (GraphicsUnit)2);
		}
		return new Font(Fallback, px, (FontStyle)(medium ? 1 : 0), (GraphicsUnit)2);
	}

	public static GraphicsPath Round(RectangleF r, float rad)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		GraphicsPath val = new GraphicsPath();
		float num = Math.Min(rad * 2f, Math.Min(r.Width, r.Height));
		if (num <= 0f)
		{
			val.AddRectangle(r);
			return val;
		}
		val.AddArc(r.X, r.Y, num, num, 180f, 90f);
		val.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		val.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		val.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		val.CloseFigure();
		return val;
	}

	public static void Fill(Graphics g, Rectangle r, float rad, Color c)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		GraphicsPath val = Round(r, rad);
		try
		{
			SolidBrush val2 = new SolidBrush(c);
			try
			{
				g.FillPath((Brush)(object)val2, val);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static void Stroke(Graphics g, Rectangle r, float rad, Color c, float w = 1f)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		GraphicsPath val = Round(new RectangleF((float)r.X + 0.5f, (float)r.Y + 0.5f, r.Width - 1, r.Height - 1), rad);
		try
		{
			Pen val2 = new Pen(c, w);
			try
			{
				g.DrawPath(val2, val);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static void Card(Graphics g, Rectangle r, float rad, Color fill, Color border)
	{
		Fill(g, r, rad, fill);
		Stroke(g, r, rad, border);
	}

	public static void Dot(Graphics g, PointF c, float size, Color color)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		SolidBrush val = new SolidBrush(color);
		try
		{
			g.FillEllipse((Brush)(object)val, c.X - size / 2f, c.Y - size / 2f, size, size);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static void Draw(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags = (TextFormatFlags)4)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		TextRenderer.DrawText((IDeviceContext)(object)g, s, f, r, c, (TextFormatFlags)((int)flags | 0x800 | 0x1000000 | 0x10000000));
	}

	public static void DrawWrapped(Graphics g, string s, Font f, Rectangle r, Color c)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected O, but got Unknown
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		SolidBrush val = new SolidBrush(c);
		try
		{
			TextRenderingHint textRenderingHint = g.TextRenderingHint;
			g.TextRenderingHint = (TextRenderingHint)3;
			g.DrawString(s, f, (Brush)(object)val, (RectangleF)r, WrapFormat);
			g.TextRenderingHint = textRenderingHint;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static int MeasureWrapped(Graphics g, string s, Font f, int width)
	{
		return (int)Math.Ceiling(g.MeasureString(s, f, width, WrapFormat).Height);
	}

	public static Size Measure(string s, Font f)
	{
		return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), (TextFormatFlags)268437504);
	}

	public static Bitmap? LoadImage(string name)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		try
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
			if (stream == null)
			{
				return null;
			}
			Image val = Image.FromStream(stream);
			try
			{
				return new Bitmap(val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			Log.Write("โหลดภาพ " + name + " ไม\u0e48ได\u0e49: " + ex.Message);
			return null;
		}
	}

	public static void DrawCover(Graphics g, Image img, Rectangle dst, RectangleF src, ImageAttributes? attrs = null)
	{
		float num = (float)dst.Width / (float)Math.Max(1, dst.Height);
		if (src.Width / Math.Max(1f, src.Height) > num)
		{
			float num2 = src.Height * num;
			src = new RectangleF(src.X + (src.Width - num2) / 2f, src.Y, num2, src.Height);
		}
		else
		{
			float num3 = src.Width / num;
			src = new RectangleF(src.X, src.Y + (src.Height - num3) / 2f, src.Width, num3);
		}
		if (attrs == null)
		{
			g.DrawImage(img, (RectangleF)dst, src, (GraphicsUnit)2);
		}
		else
		{
			g.DrawImage(img, dst, src.X, src.Y, src.Width, src.Height, (GraphicsUnit)2, attrs);
		}
	}

	public static ImageAttributes PurpleTint()
	{
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		//IL_00b3: Expected O, but got Unknown
		float[] array = new float[3] { 0.62f, 0.47f, 1f };
		float[] array2 = new float[3] { 0.299f, 0.587f, 0.114f };
		float[][] array3 = new float[5][];
		for (int i = 0; i < 3; i++)
		{
			array3[i] = new float[5];
			for (int j = 0; j < 3; j++)
			{
				array3[i][j] = 0.78f * (((i == j) ? 0.3f : 0f) + 0.7f * array2[i] * array[j]);
			}
		}
		array3[3] = new float[5] { 0f, 0f, 0f, 1f, 0f };
		array3[4] = new float[5] { 0.012f, 0.008f, 0.03f, 0f, 1f };
		ImageAttributes val = new ImageAttributes();
		val.SetColorMatrix(new ColorMatrix(array3));
		return val;
	}
}

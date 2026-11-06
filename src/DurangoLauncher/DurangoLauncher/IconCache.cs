using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

internal sealed class IconCache : IDisposable
{
	public static readonly string Dir = Path.Combine(Session.Dir, "icons");

	private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

	public const int TintedMax = 512;

	private readonly TintLru<Bitmap> _tinted = new TintLru<Bitmap>(512);

	private readonly ConcurrentDictionary<string, Bitmap?> _mem = new ConcurrentDictionary<string, Bitmap>();

	private readonly ConcurrentDictionary<string, byte> _loading = new ConcurrentDictionary<string, byte>();

	private readonly ConcurrentDictionary<string, DateTime> _retryAt = new ConcurrentDictionary<string, DateTime>();

	private readonly HttpClient _http = LauncherTradeApi.CreateHttp();

	private SynchronizationContext? _ui;

	private string _base = "";

	private Action? _onLoaded;

	private bool _disposed;

	public int TintedCount => _tinted.Count;

	public void Configure(string launcherApi, Action onLoaded)
	{
		if (_base != launcherApi.TrimEnd('/'))
		{
			_base = launcherApi.TrimEnd('/');
			_mem.Clear();
			_retryAt.Clear();
			ClearTinted();
		}
		_onLoaded = onLoaded;
		if (_ui == null)
		{
			_ui = SynchronizationContext.Current;
		}
	}

	public Bitmap? Get(TradeItem it)
	{
		return Get(it.Icon, it.IconTint, it.Colors);
	}

	public Bitmap? Get(string icon, string? tint, uint[]? colors)
	{
		Bitmap raw = Get(icon);
		if (raw == null || IconTint.Effective(tint, colors) == "none")
		{
			return raw;
		}
		string text = IconTint.Key(icon, tint, colors);
		try
		{
			return _tinted.GetOrAdd(text, () => Tint(raw, tint, colors));
		}
		catch (Exception ex)
		{
			Log.Write("icon tint " + text + ": " + ex.Message);
			return raw;
		}
	}

	public static Bitmap Tint(Bitmap raw, string? tint, uint[]? colors)
	{
		int width = ((Image)raw).Width;
		int height = ((Image)raw).Height;
		uint[] array = new uint[width * height];
		Rectangle rectangle = new Rectangle(0, 0, width, height);
		BitmapData val = raw.LockBits(rectangle, (ImageLockMode)1, (PixelFormat)2498570);
		try
		{
			int[] array2 = new int[width];
			for (int i = 0; i < height; i++)
			{
				Marshal.Copy((nint)val.Scan0 + i * val.Stride, array2, 0, width);
				for (int j = 0; j < width; j++)
				{
					array[i * width + j] = (uint)array2[j];
				}
			}
		}
		finally
		{
			raw.UnlockBits(val);
		}
		return ToBitmap(width, height, IconTint.Apply(array, tint, colors));
	}

	private void ClearTinted()
	{
		_tinted.Clear();
	}

	public Bitmap? Get(string icon)
	{
		if (string.IsNullOrEmpty(icon) || _base.Length == 0 || !LauncherTradeApi.ValidIconName(icon))
		{
			return null;
		}
		if (_mem.TryGetValue(icon, out Bitmap value))
		{
			return value;
		}
		if (_retryAt.TryGetValue(icon, out var value2) && DateTime.UtcNow < value2)
		{
			return null;
		}
		if (_loading.TryAdd(icon, 0))
		{
			LoadAsync(icon);
		}
		return null;
	}

	private async Task LoadAsync(string icon)
	{
		Bitmap bmp = null;
		bool failed = false;
		try
		{
			byte[] array = ReadFile(icon);
			if (array == null)
			{
				array = await LauncherTradeApi.IconAsync(_base, icon, _http).ConfigureAwait(continueOnCapturedContext: false);
				if (array != null)
				{
					WriteFile(icon, array);
				}
			}
			if (array != null)
			{
				bmp = Decode(array);
			}
		}
		catch (Exception ex)
		{
			failed = true;
			Log.Write("icon " + icon + ": " + ex.Message);
		}
		if (_disposed)
		{
			Bitmap obj = bmp;
			if (obj != null)
			{
				((Image)obj).Dispose();
			}
			return;
		}
		if (failed)
		{
			_retryAt[icon] = DateTime.UtcNow.AddSeconds(60.0);
		}
		else
		{
			_mem[icon] = bmp;
		}
		_loading.TryRemove(icon, out var _);
		Action cb = _onLoaded;
		if (cb == null)
		{
			return;
		}
		if (_ui != null)
		{
			_ui.Post(delegate
			{
				if (!_disposed)
				{
					cb();
				}
			}, null);
		}
		else
		{
			cb();
		}
	}

	private static byte[]? ReadFile(string icon)
	{
		try
		{
			string text = Path.Combine(Dir, icon + ".webp");
			FileInfo fileInfo = new FileInfo(text);
			if (!fileInfo.Exists || fileInfo.Length == 0L || DateTime.UtcNow - fileInfo.LastWriteTimeUtc > MaxAge)
			{
				return null;
			}
			return File.ReadAllBytes(text);
		}
		catch
		{
			return null;
		}
	}

	private static void WriteFile(string icon, byte[] bytes)
	{
		try
		{
			Directory.CreateDirectory(Dir);
			string text = Path.Combine(Dir, icon + ".webp");
			File.WriteAllBytes(text + ".tmp", bytes);
			File.Move(text + ".tmp", text, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.Write("icon cache write " + icon + ": " + ex.Message);
		}
	}

	public static Bitmap? Decode(byte[] bytes)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		WebpLossless.Image image = WebpLossless.Decode(bytes);
		if (image == null)
		{
			try
			{
				using MemoryStream memoryStream = new MemoryStream(bytes);
				Image val = Image.FromStream((Stream)memoryStream);
				try
				{
					return new Bitmap(val);
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			catch
			{
				return null;
			}
		}
		return ToBitmap(image.Width, image.Height, image.Argb);
	}

	public static Bitmap ToBitmap(int w, int h, uint[] argb)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		Bitmap val = new Bitmap(w, h, (PixelFormat)2498570);
		BitmapData val2 = val.LockBits(new Rectangle(0, 0, w, h), (ImageLockMode)2, ((Image)val).PixelFormat);
		try
		{
			int[] array = new int[w];
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					array[j] = (int)argb[i * w + j];
				}
				Marshal.Copy(array, 0, (nint)val2.Scan0 + i * val2.Stride, w);
			}
		}
		finally
		{
			val.UnlockBits(val2);
		}
		return val;
	}

	public void Dispose()
	{
		_disposed = true;
		ClearTinted();
		foreach (Bitmap value in _mem.Values)
		{
			if (value != null)
			{
				((Image)value).Dispose();
			}
		}
		_mem.Clear();
		_http.Dispose();
	}
}

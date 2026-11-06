using System;
using System.IO;

namespace DurangoLauncher;

internal static class DeviceId
{
	private static string? _v;

	public static string Value => _v ?? (_v = Load());

	private static string Load()
	{
		string path = Path.Combine(Session.Dir, "device.txt");
		try
		{
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path).Trim();
				if (text != null && text.Length >= 16)
				{
					return text;
				}
			}
			string text2 = Guid.NewGuid().ToString("N");
			Directory.CreateDirectory(Session.Dir);
			File.WriteAllText(path, text2);
			return text2;
		}
		catch
		{
			return Guid.NewGuid().ToString("N");
		}
	}
}

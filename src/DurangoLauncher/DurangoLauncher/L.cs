using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DurangoLauncher;

internal static class L
{
	public static bool En { get; private set; }

	private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DurangoLauncher", "settings.txt");

	[DllImport("kernel32.dll")]
	private static extern ushort GetUserDefaultUILanguage();

	public static void Load()
	{
		En = true;
	}

	public static void Set(bool en)
	{
		En = true;
	}

	public static string T(string th, string en)
	{
		return en;
	}
}

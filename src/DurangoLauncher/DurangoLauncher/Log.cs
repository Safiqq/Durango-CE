using System;
using System.Globalization;
using System.IO;

namespace DurangoLauncher;

internal static class Log
{
	public static void Write(string msg)
	{
		try
		{
			File.AppendAllText(Path.Combine(Path.GetTempPath(), "DurangoLauncher.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + msg + Environment.NewLine);
		}
		catch
		{
		}
	}
}

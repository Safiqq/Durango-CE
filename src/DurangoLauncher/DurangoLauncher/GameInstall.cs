using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DurangoLauncher;

public sealed class GameInstall
{
	public const string GameExe = "Durango.exe";

	public const string ConfigFile = "server.txt";

	public const string LauncherExe = "DurangoLauncher.exe";

	public const string InstallFolder = "Durango";

	public static readonly string? SelfPath = FindSelf();

	public static readonly string LauncherVersion = "1.0.0-CE";

	public const string DefaultGateway = "http://127.0.0.1:8190";

	public const string DefaultServerName = "Durango: Community Edition";

	public const string GitHubRepo = "Safiqq/Durango-CE";

	public string Dir { get; }

	public string Gateway { get; private set; } = "http://127.0.0.1:8190";

	public string Version { get; private set; } = "1.0.0-CE";

	public bool Installed => File.Exists(ExePath);

	public string ConfigPath
	{
		get
		{
			string serverTxt = Path.Combine(Dir, "server.txt");
			if (File.Exists(serverTxt))
			{
				return serverTxt;
			}
			string oldTxt = Path.Combine(Dir, "offserver.txt");
			if (File.Exists(oldTxt))
			{
				return oldTxt;
			}
			return serverTxt;
		}
	}

	public string VersionPath => Path.Combine(Dir, "Durango_Data", "version.txt");

	public string ExePath => Path.Combine(Dir, "Durango.exe");

	private GameInstall(string dir)
	{
		Dir = dir;
	}

	public static GameInstall Locate()
	{
		string text = ((SelfPath != null) ? Path.GetDirectoryName(SelfPath) : AppContext.BaseDirectory);
		string dir = text;
		if (!File.Exists(Path.Combine(text, "Durango.exe")))
		{
			if (Directory.Exists(Path.Combine(text, "Durango")))
			{
				dir = Path.Combine(text, "Durango");
			}
		}
		GameInstall gameInstall = At(dir);
		Log.Write("gameDir = " + gameInstall.Dir + (gameInstall.Installed ? "" : " (not installed)"));
		return gameInstall;
	}

	public static GameInstall At(string dir)
	{
		GameInstall gameInstall = new GameInstall(Path.GetFullPath(dir));
		gameInstall.Reload();
		return gameInstall;
	}

	private static string? FindSelf()
	{
		string processPath = Environment.ProcessPath;
		if (string.IsNullOrEmpty(processPath) || Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return Path.GetFullPath(processPath);
	}

	public void Reload()
	{
		if (Installed)
		{
			EnsureConfig();
		}
		Gateway = ReadGateway() ?? "http://127.0.0.1:8190";
		Version = ReadVersion();
		CleanupOldLauncher();
	}

	public static bool GameRunning()
	{
		Process[] p1 = Process.GetProcessesByName("Durango");
		bool running = p1.Length != 0;
		foreach (Process p in p1) p.Dispose();
		return running;
	}

	private void EnsureConfig()
	{
		if (File.Exists(ConfigPath))
		{
			return;
		}
		try
		{
			string target = Path.Combine(Dir, "server.txt");
			File.WriteAllText(target, "# Durango: Community Edition server configuration\r\ngateway=http://127.0.0.1:8190\r\nname=Local Server\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Log.Write("Created server.txt with default local configuration");
		}
		catch (Exception ex)
		{
			Log.Write("Could not write server config: " + ex.Message);
		}
	}

	private string? ReadGateway()
	{
		try
		{
			string result = null;
			string[] array = File.ReadAllLines(ConfigPath, Encoding.UTF8);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length == 0 || text.StartsWith('#'))
				{
					continue;
				}
				int num = text.IndexOf('=');
				string text2 = ((num < 0) ? "" : text.Substring(0, num).Trim().ToLowerInvariant());
				string text4;
				if (num >= 0)
				{
					string text3 = text;
					int num2 = num + 1;
					text4 = text3.Substring(num2, text3.Length - num2).Trim();
				}
				else
				{
					text4 = text;
				}
				string text5 = text4;
				bool flag = num < 0;
				if (!flag)
				{
					bool flag2 = ((text2 == "gateway" || text2 == "url") ? true : false);
					flag = flag2;
				}
				if (flag)
				{
					string text6 = text5.TrimEnd('/');
					if (!text6.StartsWith("http://") && !text6.StartsWith("https://"))
					{
						text6 = "http://" + text6;
					}
					result = text6;
				}
			}
			return result;
		}
		catch
		{
			return null;
		}
	}

	private string ReadVersion()
	{
		try
		{
			return File.Exists(VersionPath) ? File.ReadAllText(VersionPath).Trim() : "?";
		}
		catch
		{
			return "?";
		}
	}

	private static void CleanupOldLauncher()
	{
		if (SelfPath == null)
		{
			return;
		}
		try
		{
			if (File.Exists(SelfPath + ".old"))
			{
				File.Delete(SelfPath + ".old");
			}
		}
		catch
		{
		}
	}

	public void Launch(string? ticket = null)
	{
		if (ticket != null && (ticket.Length == 0 || !ticket.All(delegate(char c)
		{
			bool flag = char.IsAsciiLetterOrDigit(c);
			if (!flag)
			{
				bool flag2 = ((c == '-' || c == '.' || c == '_') ? true : false);
				flag = flag2;
			}
			return flag;
		})))
		{
			throw new InvalidOperationException("ต\u0e31\u0e4bวเข\u0e49าเกมผ\u0e34ดร\u0e39ปแบบ");
		}
		string text = Path.Combine(Dir, "player.log");
		Process process = Process.Start(new ProcessStartInfo
		{
			FileName = ExePath,
			Arguments = "-logFile \"" + text + "\" -offserver-launcher " + (ticket ?? Version),
			WorkingDirectory = Dir,
			UseShellExecute = true
		}) ?? throw new InvalidOperationException("Process.Start ค\u0e37น null");
		Log.Write($"launched pid={process.Id} v{Version} gateway={Gateway}" + ((ticket != null) ? " · ต\u0e31\u0e4bว Durango ID" : ""));
	}

	public static int CompareVersion(string a, string b)
	{
		int[] array = ParseVer(a);
		int[] array2 = ParseVer(b);
		for (int i = 0; i < Math.Max(array.Length, array2.Length); i++)
		{
			int num = ((i < array.Length) ? array[i] : 0);
			int num2 = ((i < array2.Length) ? array2[i] : 0);
			if (num != num2)
			{
				if (num >= num2)
				{
					return 1;
				}
				return -1;
			}
		}
		return 0;
	}

	private static int[] ParseVer(string v)
	{
		List<int> list = new List<int>();
		string[] array = (v ?? "").TrimStart('v', 'V').Split('.');
		foreach (string source in array)
		{
			list.Add(int.TryParse(new string(source.Where(char.IsDigit).ToArray()), out var result) ? result : 0);
		}
		return list.ToArray();
	}

	static GameInstall()
	{
		Version version = typeof(GameInstall).Assembly.GetName().Version;
		LauncherVersion = (((object)version != null) ? $"{version.Major}.{version.Minor}.{version.Build}" : "0");
	}
}

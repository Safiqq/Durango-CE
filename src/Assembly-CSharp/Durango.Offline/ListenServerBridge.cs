using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Durango.Offline;

/// <summary>
/// Listen Server Lifecycle Bridge for Durango: Community Edition.
/// Automatically detects if a standalone Dedicated Server or background Listen Server
/// is running on 127.0.0.1:8190. If not running, launches DurangoServer executable
/// in the background for local / LAN hosting, seamlessly bridging solo and multiplayer.
/// </summary>
public static class ListenServerBridge
{
	private static Process _serverProcess;
	private static bool _initialized;
	private static readonly object _lock = new object();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	public static void AutoStart()
	{
		EnsureStarted();
	}

	public static void EnsureStarted()
	{
		lock (_lock)
		{
			if (_initialized) return;
			_initialized = true;

			// 1. Check if server is already running on 127.0.0.1:8190
			if (IsServerAlive("127.0.0.1", 8190))
			{
				UnityEngine.Debug.Log("[ListenServerBridge] Active server detected on 127.0.0.1:8190");
				return;
			}

			// 2. Find DurangoServer executable
			string serverExe = FindServerExecutable();
			if (!string.IsNullOrEmpty(serverExe) && File.Exists(serverExe))
			{
				StartServerProcess(serverExe);
			}
			else
			{
				UnityEngine.Debug.LogWarning("[ListenServerBridge] DurangoServer executable not found; falling back to embedded gateway.");
				LocalGatewayServer.EnsureStarted();
			}

			try
			{
				Application.quitting += Stop;
			}
			catch { }
		}
	}

	private static bool IsServerAlive(string host, int port)
	{
		try
		{
			using var client = new TcpClient();
			var result = client.BeginConnect(host, port, null, null);
			bool success = result.AsyncWaitHandle.WaitOne(300);
			if (!success) return false;
			client.EndConnect(result);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string FindServerExecutable()
	{
		string baseDir = Path.GetDirectoryName(Application.dataPath) ?? ".";
		string exeName = (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
			? "DurangoServer.exe"
			: "DurangoServer";

		string[] candidates = new string[]
		{
			Path.Combine(baseDir, exeName),
			Path.Combine(baseDir, "server", exeName),
			Path.Combine(Directory.GetCurrentDirectory(), exeName),
			Path.Combine(Directory.GetCurrentDirectory(), "build", "server", exeName),
			Path.Combine(Directory.GetCurrentDirectory(), "build", "Durango-CE", exeName)
		};

		foreach (string candidate in candidates)
		{
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}
		return null;
	}

	private static void StartServerProcess(string exePath)
	{
		try
		{
			string workDir = Path.GetDirectoryName(exePath);
			var psi = new ProcessStartInfo
			{
				FileName = exePath,
				WorkingDirectory = string.IsNullOrEmpty(workDir) ? "." : workDir,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			_serverProcess = Process.Start(psi);
			UnityEngine.Debug.Log($"[ListenServerBridge] Launched {exePath} (PID: {_serverProcess?.Id})");

			// Wait briefly for socket binding
			for (int i = 0; i < 20; i++)
			{
				Thread.Sleep(100);
				if (IsServerAlive("127.0.0.1", 8190))
				{
					UnityEngine.Debug.Log("[ListenServerBridge] Listen server successfully listening on 127.0.0.1:8190");
					break;
				}
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning($"[ListenServerBridge] Failed to launch server process: {ex.Message}. Falling back to embedded gateway.");
			LocalGatewayServer.EnsureStarted();
		}
	}

	public static void Stop()
	{
		try
		{
			if (_serverProcess != null && !_serverProcess.HasExited)
			{
				_serverProcess.Kill();
				_serverProcess.Dispose();
				_serverProcess = null;
				UnityEngine.Debug.Log("[ListenServerBridge] Terminated child server process.");
			}
		}
		catch { }
	}
}

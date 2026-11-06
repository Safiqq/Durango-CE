using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class LauncherSelfUpdate
{
	public static async Task<bool> TryDownloadAsync(HttpClient http, string url, string dest, string sha256, long size, Action<long>? onBytes, CancellationToken ct)
	{
		// SANITIZED: Auto-replacing launcher binary with untrusted remote payload is disabled.
		await Task.CompletedTask;
		return false;
	}
}

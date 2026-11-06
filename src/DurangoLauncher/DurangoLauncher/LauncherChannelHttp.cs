using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class LauncherChannelHttp
{
	public static readonly HttpClient Client = CreateHttp();

	private static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromMinutes(10L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DurangoLauncher/" + GameInstall.LauncherVersion + " (+https://github.com/Safiqq/Durango-CE)");
		return httpClient;
	}

	public static async Task<LauncherChannel.Release?> FetchAsync()
	{
		using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10L));
		try
		{
			return await LauncherChannel.FetchAsync(Client, "Safiqq/Durango-CE", cts.Token);
		}
		catch (OperationCanceledException)
		{
			Log.Write("pc-launcher.json: timeout (10s) — skipping");
			return null;
		}
	}
}

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class UpdateSource
{
	private static readonly HttpClient Http = CreateHttp();

	private static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(10L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DurangoLauncher/1.0 (+https://github.com/Safiqq/Durango-CE)");
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
		return httpClient;
	}

	public static async Task<UpdateInfo> FetchAsync(string gateway)
	{
		UpdateInfo info = new UpdateInfo();
		Task<(int? Online, TimeSpan Skew)> status = FetchStatusAsync(gateway);
		TimeSpan? dateSkew = null;
		try
		{
			using HttpResponseMessage resp = await Http.GetAsync(gateway + "/launcher");
			DateTimeOffset received = DateTimeOffset.UtcNow;
			resp.EnsureSuccessStatusCode();
			ParseGateway(await resp.Content.ReadAsStringAsync(), info);
			info.ServerOnline = true;
			if (resp.Headers.Date.HasValue)
			{
				dateSkew = ServerClock.Skew(resp.Headers.Date, received);
			}
		}
		catch (Exception ex)
		{
			info.Error = ex.Message;
			Log.Write("gateway /launcher: " + ex.Message);
		}
		if (!info.ServerOnline || string.IsNullOrEmpty(info.LatestVersion))
		{
			try
			{
				await FillFromGitHubAsync(info);
			}
			catch (Exception ex2)
			{
				Log.Write("github releases: " + ex2.Message);
			}
		}
		(int?, TimeSpan) obj = await status;
		int? item = obj.Item1;
		TimeSpan item2 = obj.Item2;
		info.OnlinePlayers = item;
		info.ClockSkew = (info.ServerOnline ? (dateSkew ?? item2) : TimeSpan.Zero);
		if (info.ClockSkew != TimeSpan.Zero)
		{
			Log.Write($"นาฬ\u0e34กาเคร\u0e37\u0e48องเพ\u0e35\u0e49ยนจากเซ\u0e34ร\u0e4cฟ {info.ClockSkew.TotalSeconds:N0} ว\u0e34 — น\u0e31บถอยหล\u0e31งใช\u0e49เวลาเซ\u0e34ร\u0e4cฟ");
		}
		return info;
	}

	public static async Task<int?> FetchOnlineAsync(string gateway)
	{
		return (await FetchStatusAsync(gateway)).Item1;
	}

	private static async Task<(int? Online, TimeSpan Skew)> FetchStatusAsync(string gateway)
	{
		try
		{
			string json = await Http.GetStringAsync(gateway + "/status");
			DateTimeOffset utcNow = DateTimeOffset.UtcNow;
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			int value2;
			int? item = ((rootElement.TryGetProperty("online", out value) && value.TryGetInt32(out value2)) ? new int?(value2) : ((int?)null));
			TimeSpan item2 = TimeSpan.Zero;
			if (rootElement.TryGetProperty("at", out var value3) && value3.ValueKind == JsonValueKind.Number && value3.TryGetDouble(out var value4) && value4 > 0.0)
			{
				item2 = ServerClock.Skew(DateTimeOffset.FromUnixTimeMilliseconds((long)(value4 * 1000.0)), utcNow, ServerClock.StatusThreshold);
			}
			return (Online: item, Skew: item2);
		}
		catch (Exception ex)
		{
			Log.Write("gateway /status: " + ex.Message);
			return (Online: null, Skew: TimeSpan.Zero);
		}
	}

	private static void ParseGateway(string json, UpdateInfo info)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(json);
		JsonElement rootElement = jsonDocument.RootElement;
		info.LatestVersion = Str(rootElement, "latest_version");
		info.MinVersion = Str(rootElement, "min_version");
		if (rootElement.TryGetProperty("news", out var value) && value.ValueKind == JsonValueKind.Array)
		{
			info.News = (from n in value.EnumerateArray()
				select new NewsItem(Str(n, "date"), Str(n, "title"), Str(n, "body"), Str(n, "title_en"), Str(n, "body_en"), Str(n, "tag"))).ToArray();
		}
		info.Countdown = Countdown.Parse(rootElement);
		info.Account = AccountConfig.Parse(rootElement);
	}

	private static async Task FillFromGitHubAsync(UpdateInfo info)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(await Http.GetStringAsync("https://api.github.com/repos/Safiqq/Durango-CE/releases/latest"));
		string text = Str(jsonDocument.RootElement, "tag_name").TrimStart('v', 'V');
		if (string.IsNullOrEmpty(info.LatestVersion))
		{
			info.LatestVersion = text;
		}
		else if (GameInstall.CompareVersion(text, info.LatestVersion) > 0)
		{
			info.LatestVersion = text;
		}
	}

	public static async Task<ReleaseAssets?> FetchReleaseAsync(string version)
	{
		string ver = version.TrimStart('v', 'V');
		string text = $"https://github.com/{"Safiqq/Durango-CE"}/releases/download/v{ver}/";
		ReleaseAssets direct = new ReleaseAssets(text + "manifest-v" + ver + ".json", text + "Durango-CE-v" + ver + ".zip", text + "DurangoLauncher.exe", text + "chunks-v" + ver + ".json.gz", text + "blocks-v" + ver + ".bin");
		using (HttpRequestMessage probe = new HttpRequestMessage(HttpMethod.Get, direct.ManifestUrl))
		{
			probe.Headers.Range = new RangeHeaderValue(0L, 0L);
			using HttpResponseMessage httpResponseMessage = await Http.SendAsync(probe, HttpCompletionOption.ResponseHeadersRead);
			if (httpResponseMessage.IsSuccessStatusCode)
			{
				return direct;
			}
		}
		using HttpResponseMessage api = await Http.GetAsync("https://api.github.com/repos/Safiqq/Durango-CE/releases/tags/v" + ver);
		if (api.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		api.EnsureSuccessStatusCode();
		using JsonDocument jsonDocument = JsonDocument.Parse(await api.Content.ReadAsStringAsync());
		string text2 = "";
		string text3 = "";
		string launcherExeUrl = "";
		string chunksUrl = "";
		string blocksUrl = "";
		if (jsonDocument.RootElement.TryGetProperty("assets", out var value) && value.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement item in value.EnumerateArray())
			{
				string text4 = Str(item, "name");
				if (text4.StartsWith("manifest-", StringComparison.OrdinalIgnoreCase) && text4.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
				{
					text2 = Str(item, "browser_download_url");
				}
				else if (text4.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !text4.Contains("-update-from-", StringComparison.OrdinalIgnoreCase))
				{
					text3 = Str(item, "browser_download_url");
				}
				else if (text4.Equals("DurangoLauncher.exe", StringComparison.OrdinalIgnoreCase))
				{
					launcherExeUrl = Str(item, "browser_download_url");
				}
				else if (text4.Equals("chunks-v" + ver + ".json.gz", StringComparison.OrdinalIgnoreCase))
				{
					chunksUrl = Str(item, "browser_download_url");
				}
				else if (text4.Equals("blocks-v" + ver + ".bin", StringComparison.OrdinalIgnoreCase))
				{
					blocksUrl = Str(item, "browser_download_url");
				}
			}
		}
		return (text2.Length > 0 && text3.Length > 0) ? new ReleaseAssets(text2, text3, launcherExeUrl, chunksUrl, blocksUrl) : null;
	}

	private static string Str(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return value.GetString() ?? "";
	}
}

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class LauncherChannel
{
	public sealed record Release(string Version, string Url, long Size, string Sha256);

	private static readonly Regex Sha = new Regex("^[0-9a-f]{64}$");

	public static string Url(string repo)
	{
		return "https://raw.githubusercontent.com/" + repo + "/main/pc-launcher.json";
	}

	public static Release? Parse(string json)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement rootElement = jsonDocument.RootElement;
			string text = Str(rootElement, "version").TrimStart('v', 'V');
			string text2 = Str(rootElement, "url");
			string text3 = Str(rootElement, "sha256").ToLowerInvariant();
			JsonElement value;
			long num = ((rootElement.TryGetProperty("size", out value) && value.ValueKind == JsonValueKind.Number) ? value.GetInt64() : 0);
			if (Parts(text).Length == 0 || !text2.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || !Sha.IsMatch(text3) || num <= 0)
			{
				return null;
			}
			return new Release(text, text2, num, text3);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	public static async Task<Release?> FetchAsync(HttpClient http, string repo, CancellationToken ct)
	{
		try
		{
			Release? release = Parse(await http.GetStringAsync(Url(repo) + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60, ct));
			if (release == null)
			{
				Log.Write("pc-launcher.json: ผ\u0e34ดร\u0e39ป — ข\u0e49าม");
			}
			return release;
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			Log.Write("pc-launcher.json: " + ex.Message);
			return null;
		}
	}

	public static bool IsNewer(string a, string b)
	{
		int[] array = Parts(a);
		int[] array2 = Parts(b);
		for (int i = 0; i < Math.Max(array.Length, array2.Length); i++)
		{
			int num = ((i < array.Length) ? array[i] : 0);
			int num2 = ((i < array2.Length) ? array2[i] : 0);
			if (num != num2)
			{
				return num > num2;
			}
		}
		return false;
	}

	public static bool VersionMatches(string fileVersion, string version)
	{
		int[] array = Parts(fileVersion);
		int[] array2 = Parts(version);
		if (array.Length >= array2.Length && array2.Length != 0)
		{
			return array2.SequenceEqual(array.Take(array2.Length));
		}
		return false;
	}

	private static int[] Parts(string v)
	{
		Match match = Regex.Match(v ?? "", "^\\d+(\\.\\d+)*");
		if (!match.Success)
		{
			return Array.Empty<int>();
		}
		return match.Value.Split('.').Select(int.Parse).ToArray();
	}

	private static string Str(JsonElement e, string name)
	{
		if (!e.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return (value.GetString() ?? "").Trim();
	}
}

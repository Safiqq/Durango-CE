using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class EventCalendarApi
{
	public const string Path = "/events/calendar";

	private static readonly Lazy<HttpClient> Shared = new Lazy<HttpClient>(() => CreateHttp());

	public static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(10L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DurangoLauncher/" + (typeof(EventCalendarApi).Assembly.GetName().Version?.ToString(3) ?? "0"));
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
		return httpClient;
	}

	public static async Task<CalFetchResult> FetchAsync(string gateway, HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		if (!Uri.TryCreate(gateway.TrimEnd('/') + "/events/calendar", UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttp && result.Scheme != Uri.UriSchemeHttps))
		{
			return new CalFetchResult(CalFetchKind.Unavailable, null, "bad gateway");
		}
		try
		{
			using HttpResponseMessage resp = await (http ?? Shared.Value).GetAsync(result, ct);
			if (resp.StatusCode == HttpStatusCode.NotFound)
			{
				return new CalFetchResult(CalFetchKind.NotFound, null, "HTTP 404");
			}
			string text = await resp.Content.ReadAsStringAsync(ct);
			if (!resp.IsSuccessStatusCode)
			{
				return new CalFetchResult(CalFetchKind.Unavailable, null, $"HTTP {resp.StatusCode}");
			}
			using JsonDocument jsonDocument = JsonDocument.Parse((text.Length > 0) ? text : "{}");
			CalendarData calendarData = CalendarData.Parse(jsonDocument.RootElement);
			return (calendarData != null) ? new CalFetchResult(CalFetchKind.Ok, calendarData) : new CalFetchResult(CalFetchKind.Unavailable, null, "ผ\u0e34ดร\u0e39ป");
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			Log.Write("events/calendar: " + ex2.Message);
			return new CalFetchResult(CalFetchKind.Unavailable, null, ex2.GetType().Name);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public sealed class LauncherTradeApi
{
	public const string ApiHeader = "X-OffServer-Launcher-Api";

	public const string ApiVersion = "5";

	private readonly HttpClient _http;

	private readonly Func<string?> _token;

	public string Base { get; }

	public string Server { get; }

	public static string Lang
	{
		get
		{
			if (!L.En)
			{
				return "th";
			}
			return "en";
		}
	}

	public LauncherTradeApi(string launcherApi, string server, Func<string?> refreshToken, HttpClient? http = null)
	{
		Base = launcherApi.TrimEnd('/');
		Server = server;
		_token = refreshToken;
		_http = http ?? CreateHttp();
	}

	public static HttpClient CreateHttp(string? userAgent = null)
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(15L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent ?? ("DurangoLauncher/" + (typeof(LauncherTradeApi).Assembly.GetName().Version?.ToString(3) ?? "0")));
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
		httpClient.DefaultRequestHeaders.Add("X-OffServer-Launcher-Api", "5");
		return httpClient;
	}

	public static bool SafeUrl(string url)
	{
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri result))
		{
			if (!(result.Scheme == Uri.UriSchemeHttps))
			{
				if (result.Scheme == Uri.UriSchemeHttp)
				{
					if (!result.IsLoopback)
					{
						return result.Host == "localhost";
					}
					return true;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool ValidIconName(string icon)
	{
		int length = icon.Length;
		if (length > 0 && length <= 120)
		{
			return icon.All(delegate(char c)
			{
				bool flag = char.IsAsciiLetterOrDigit(c);
				if (!flag)
				{
					bool flag2 = ((c == '-' || c == '_') ? true : false);
					flag = flag2;
				}
				return flag;
			});
		}
		return false;
	}

	private async Task<JsonElement> PostAsync(string path, JsonObject body, CancellationToken ct, Func<string, string?>? fallback = null)
	{
		string text = Base + path;
		if (!SafeUrl(text))
		{
			throw new ApiError("insecure", L.T("ท\u0e35\u0e48อย\u0e39\u0e48ระบบบ\u0e31ญช\u0e35ไม\u0e48ใช\u0e48 HTTPS — ไม\u0e48ส\u0e48งข\u0e49อม\u0e39ลบ\u0e31ญช\u0e35", "The account server is not HTTPS — refusing to send account data"));
		}
		string text2 = _token() ?? "";
		if (text2.Length == 0)
		{
			throw new ApiError("invalid_refresh", L.T("กร\u0e38ณาเข\u0e49าส\u0e39\u0e48ระบบอ\u0e35กคร\u0e31\u0e49ง", "Please sign in again"), 401);
		}
		body["refresh_token"] = text2;
		body["server"] = Server;
		body["lang"] = Lang;
		HttpResponseMessage resp;
		try
		{
			using StringContent content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
			resp = await _http.PostAsync(text, content, ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			Log.Write("launcher-api POST " + path + ": " + ex2.Message);
			throw new ApiError("network", L.T("ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟเวอร\u0e4cไม\u0e48ได\u0e49 — ตรวจเน\u0e47ตแล\u0e49วลองใหม\u0e48", "Cannot reach the server — check your connection and try again"));
		}
		using (resp)
		{
			string text3 = await resp.Content.ReadAsStringAsync(ct);
			JsonElement jsonElement;
			try
			{
				jsonElement = JsonDocument.Parse((text3.Length > 0) ? text3 : "{}").RootElement.Clone();
			}
			catch
			{
				Log.Write($"launcher-api POST {path}: HTTP {resp.StatusCode} ไม\u0e48ใช\u0e48 JSON");
				throw new ApiError("server", L.T($"เซ\u0e34ร\u0e4cฟเวอร\u0e4cตอบผ\u0e34ดร\u0e39ปแบบ (HTTP {resp.StatusCode}) — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", $"Unexpected server reply (HTTP {resp.StatusCode}) — please try again"), (int)resp.StatusCode);
			}
			if (resp.IsSuccessStatusCode && (!jsonElement.TryGetProperty("ok", out var value) || value.ValueKind != JsonValueKind.False))
			{
				return jsonElement;
			}
			int? retryHeader = null;
			TimeSpan? timeSpan = resp.Headers.RetryAfter?.Delta;
			if (timeSpan.HasValue)
			{
				retryHeader = (int)Math.Ceiling(timeSpan.GetValueOrDefault().TotalSeconds);
			}
			throw ToError(jsonElement, (int)resp.StatusCode, path, retryHeader, fallback);
		}
	}

	public static ApiError ToError(JsonElement root, int status, string path = "", int? retryHeader = null, Func<string, string?>? fallback = null)
	{
		string text = ((status == 429) ? "rate_limited" : "server");
		string text2 = "";
		JsonElement eo = default(JsonElement);
		if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var value))
		{
			if (value.ValueKind == JsonValueKind.Object)
			{
				eo = value;
				text = J.Str(value, "code", text);
				text2 = J.Str(value, "message");
			}
			else if (value.ValueKind == JsonValueKind.String)
			{
				text = value.GetString() ?? text;
			}
		}
		else if (root.ValueKind == JsonValueKind.Object)
		{
			string text3 = J.Str(root, "code");
			if (text3 != null && text3.Length > 0)
			{
				text = text3;
			}
		}
		if (text2.Length == 0 && root.ValueKind == JsonValueKind.Object)
		{
			text2 = J.Str(root, "message");
		}
		if (text2.Length == 0)
		{
			text2 = fallback?.Invoke(text) ?? DefaultMessage(text, status);
		}
		if (path.Length > 0)
		{
			Log.Write($"launcher-api POST {path}: HTTP {status} {text}");
		}
		return new ApiError(text, text2, status)
		{
			RetryAfter = (Pick("retry_after_s") ?? Pick("retry_after") ?? retryHeader),
			Wait = Pick("wait"),
			Hours = Pick("hours"),
			Price = PickL("price"),
			Fee = PickL("fee"),
			Feature = PickS("feature"),
			Username = PickS("username"),
			Who = PickS("who"),
			UsedAt = PickL("used_at"),
			Since = PickL("since"),
			HoursText = PickS("hours")
		};
		int? Pick(string k)
		{
			int? result = J.IntN(root, k);
			if (!result.HasValue)
			{
				if (eo.ValueKind != JsonValueKind.Object)
				{
					return null;
				}
				return J.IntN(eo, k);
			}
			return result;
		}
		long? PickL(string k)
		{
			long? result = J.LongN(root, k);
			if (!result.HasValue)
			{
				if (eo.ValueKind != JsonValueKind.Object)
				{
					return null;
				}
				return J.LongN(eo, k);
			}
			return result;
		}
		string? PickS(string k)
		{
			string text4 = J.Str(root, k);
			if (text4 == null || text4.Length <= 0)
			{
				if (eo.ValueKind == JsonValueKind.Object)
				{
					string text5 = J.Str(eo, k);
					if (text5 != null && text5.Length > 0)
					{
						return text5;
					}
				}
				return null;
			}
			return text4;
		}
	}

	public static string DefaultMessage(string code, int status)
	{
		switch (code)
		{
		case "invalid_refresh":
		case "token_expired":
			return L.T("เซสช\u0e31นหมดอาย\u0e38 — เข\u0e49าส\u0e39\u0e48ระบบอ\u0e35กคร\u0e31\u0e49ง", "Your session expired — please sign in again");
		case "feature_disabled":
			return L.T("เซ\u0e34ร\u0e4cฟป\u0e34ดฟ\u0e35เจอร\u0e4cน\u0e35\u0e49อย\u0e39\u0e48", "This feature is turned off on the server");
		case "rate_limited":
			return L.T("ส\u0e48งคำขอถ\u0e35\u0e48เก\u0e34นไป — รอส\u0e31กคร\u0e39\u0e48", "Too many requests — please wait a moment");
		case "not_your_character":
			return L.T("ไม\u0e48ใช\u0e48ต\u0e31วละครของไอด\u0e35น\u0e35\u0e49", "That character does not belong to this ID");
		case "disabled":
			return L.T("ระบบน\u0e35\u0e49ป\u0e34ดอย\u0e39\u0e48", "This system is disabled");
		case "not_linked":
			return L.T("ไอด\u0e35ย\u0e31งไม\u0e48ได\u0e49ผ\u0e39กก\u0e31บบ\u0e31ญช\u0e35เกม", "This ID is not linked to a game account");
		case "banned":
			return L.T("บ\u0e31ญช\u0e35ถ\u0e39กระง\u0e31บการแชท", "Chat is blocked for this account");
		case "muted":
			return L.T("ถ\u0e39ก GM ป\u0e34ดแชทอย\u0e39\u0e48", "You are muted by a GM");
		case "empty":
			return L.T("ย\u0e31งไม\u0e48ได\u0e49ใส\u0e48อะไร", "Nothing to send");
		case "too_long":
			return L.T("ข\u0e49อความยาวเก\u0e34นกำหนด", "The message is too long");
		case "cooldown":
			return L.T("ส\u0e48งถ\u0e35\u0e48เก\u0e34นไป — รออ\u0e35กส\u0e31กคร\u0e39\u0e48", "Too fast — wait a moment");
		case "unavailable":
			return L.T("เซ\u0e34ร\u0e4cฟเกมไม\u0e48ตอบ — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "The game server is not responding — try again");
		case "gone":
			return L.T("รายการน\u0e35\u0e49ไม\u0e48ม\u0e35แล\u0e49ว", "This listing is gone");
		case "own":
			return L.T("ซ\u0e37\u0e49อของต\u0e31วเองไม\u0e48ได\u0e49", "You cannot buy your own listing");
		case "price_changed":
			return L.T("ราคาเปล\u0e35\u0e48ยนแล\u0e49ว — ตรวจราคาใหม\u0e48แล\u0e49วกดซ\u0e37\u0e49ออ\u0e35กคร\u0e31\u0e49ง", "The price changed — check the new price and buy again");
		case "tstone":
			return L.T("T Stone ไม\u0e48พอ", "Not enough T Stone");
		case "frozen":
		case "economy_frozen":
			return L.T("เศรษฐก\u0e34จถ\u0e39กฟร\u0e35ซอย\u0e39\u0e48 — ทำรายการไม\u0e48ได\u0e49ช\u0e31\u0e48วคราว", "The economy is frozen — transactions are paused");
		case "limit":
			return L.T("เก\u0e34นโควตาท\u0e35\u0e48กำหนด", "Over the allowed limit");
		case "none":
			return L.T("ไม\u0e48ม\u0e35เง\u0e34นท\u0e35\u0e48ขายได\u0e49ให\u0e49ร\u0e31บ", "Nothing to collect");
		case "level_me":
			return L.T("ต\u0e31วละครของค\u0e38ณเลเวลไม\u0e48ถ\u0e36งท\u0e35\u0e48เทรดได\u0e49", "Your character is below the trade level");
		case "level_them":
			return L.T("อ\u0e35กฝ\u0e48ายเลเวลไม\u0e48ถ\u0e36งท\u0e35\u0e48เทรดได\u0e49", "The other player is below the trade level");
		case "level":
			return L.T("เลเวลไม\u0e48ถ\u0e36งท\u0e35\u0e48เทรดได\u0e49", "Below the trade level");
		case "unknown":
			return L.T("ไม\u0e48พบผ\u0e39\u0e49เล\u0e48น/ไอเทมท\u0e35\u0e48ระบ\u0e38", "Player or item not found");
		case "self":
			return L.T("เทรดก\u0e31บต\u0e31วเองไม\u0e48ได\u0e49", "You cannot trade with yourself");
		case "items":
			return L.T("ไอเทมบางช\u0e34\u0e49นใส\u0e48ข\u0e49อเสนอไม\u0e48ได\u0e49 (ล\u0e47อก/พ\u0e31กอย\u0e39\u0e48/หาย)", "Some items cannot be offered (locked, held or missing)");
		case "full":
			return L.T("ม\u0e35ข\u0e49อเสนอเป\u0e34ดค\u0e49างครบแล\u0e49ว — ป\u0e34ดอ\u0e31นเก\u0e48าก\u0e48อน", "Too many open offers — close one first");
		case "declined_recent":
			return L.T("อ\u0e35กฝ\u0e48ายเพ\u0e34\u0e48งปฏ\u0e34เสธ — รอส\u0e31กคร\u0e39\u0e48ก\u0e48อนส\u0e48งใหม\u0e48", "They just declined — wait before sending again");
		case "blocked":
			return L.T("ส\u0e48งข\u0e49อเสนอให\u0e49ผ\u0e39\u0e49เล\u0e48นน\u0e35\u0e49ไม\u0e48ได\u0e49", "You cannot send an offer to this player");
		case "state":
			return L.T("ข\u0e49อเสนอน\u0e35\u0e49ป\u0e34ดไปแล\u0e49ว", "This offer is already closed");
		case "stale":
			return L.T("ข\u0e49อเสนอเปล\u0e35\u0e48ยนไปแล\u0e49ว — โหลดใหม\u0e48แล\u0e49วกดอ\u0e35กคร\u0e31\u0e49ง", "The offer changed — reload and press again");
		case "expired":
			return L.T("ข\u0e49อเสนอหมดอาย\u0e38แล\u0e49ว", "This offer has expired");
		case "changed":
			return L.T("ของฝ\u0e31\u0e48งผ\u0e39\u0e49ส\u0e48งหายไป — เซ\u0e34ร\u0e4cฟป\u0e34ดข\u0e49อเสนอให\u0e49แล\u0e49ว", "The sender's items are gone — the offer was closed");
		case "closed":
			return L.T("ห\u0e49องน\u0e35\u0e49ป\u0e34ดแล\u0e49ว", "This room is closed");
		case "want_items":
			return L.T("ขอได\u0e49เฉพาะ T Stone หร\u0e37อช\u0e34\u0e49นท\u0e35\u0e48อย\u0e39\u0e48ในข\u0e49อเสนอเด\u0e34มของอ\u0e35กฝ\u0e48าย", "You can only ask for T Stone or items from their original offer");
		case "not_yours":
			return L.T("แก\u0e49ได\u0e49เฉพาะข\u0e49อเสนอท\u0e35\u0e48ค\u0e38ณส\u0e48งเอง", "You can only edit offers you sent");
		case "supporter_only":
			return L.T("เทรดผ\u0e48านร\u0e31นเช\u0e48อใช\u0e49ได\u0e49เฉพาะบ\u0e31ญช\u0e35 Supporter", "Launcher trading is for Supporter accounts only");
		case "supporter_lost":
			return L.T("ข\u0e49อเสนอถ\u0e39กยกเล\u0e34ก: อ\u0e35กฝ\u0e48ายไม\u0e48ม\u0e35ยศ Supporter แล\u0e49ว · ของและ T ค\u0e37นทางจดหมาย", "The offer was cancelled: a side is no longer a Supporter · items and T return by mail");
		case "network":
			return L.T("ต\u0e34ดต\u0e48อเซ\u0e34ร\u0e4cฟเวอร\u0e4cไม\u0e48ได\u0e49", "Cannot reach the server");
		default:
			return L.T("เก\u0e34ดข\u0e49อผ\u0e34ดพลาด (" + code + ((status > 0) ? $" · HTTP {status}" : "") + ") — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Something went wrong (" + code + ((status > 0) ? $" · HTTP {status}" : "") + ") — please try again");
		}
	}

	private static JsonObject B(string entityId)
	{
		return new JsonObject { ["entity_id"] = entityId };
	}

	public static async Task<LauncherFeatures> FeaturesAsync(string launcherApi, string server, HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		string text = launcherApi.TrimEnd('/') + "/features" + ((server.Length > 0) ? ("?server=" + Uri.EscapeDataString(server)) : "");
		if (!SafeUrl(text))
		{
			return LauncherFeatures.Off;
		}
		try
		{
			if (http == null)
			{
				http = CreateHttp();
			}
			using HttpResponseMessage resp = await http.GetAsync(text, ct);
			if (!resp.IsSuccessStatusCode)
			{
				return LauncherFeatures.Off;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
			return LauncherFeatures.Parse(jsonDocument.RootElement);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException) || !ct.IsCancellationRequested)
		{
			Log.Write("launcher-api features: " + ex.Message);
			return LauncherFeatures.Off;
		}
	}

	public Task<LauncherFeatures> FeaturesAsync(CancellationToken ct = default(CancellationToken))
	{
		return FeaturesAsync(Base, Server, _http, ct);
	}

	public async Task<TradeSummary> SummaryAsync(long chatSince, CancellationToken ct = default(CancellationToken))
	{
		return TradeSummary.Parse(await PostAsync("/summary", new JsonObject { ["chat_since"] = chatSince }, ct));
	}

	public async Task<CharactersResult> CharactersAsync(CancellationToken ct = default(CancellationToken))
	{
		return CharactersResult.Parse(await PostAsync("/characters", new JsonObject(), ct));
	}

	public async Task<ItemsResult> ItemsAsync(string entityId, CancellationToken ct = default(CancellationToken))
	{
		return ItemsResult.Parse(await PostAsync("/characters/items", B(entityId), ct));
	}

	public async Task<PriceResult> PriceAsync(string proto, int? level, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = new JsonObject { ["proto"] = proto };
		if (level.HasValue)
		{
			jsonObject["level"] = level;
		}
		return PriceResult.Parse(await PostAsync("/market/price", jsonObject, ct));
	}

	public async Task<ChatHistory> ChatHistoryAsync(long since, int limit = 100, CancellationToken ct = default(CancellationToken))
	{
		return ChatHistory.Parse(await PostAsync("/chat/history", new JsonObject
		{
			["since"] = since,
			["limit"] = Math.Clamp(limit, 1, 100)
		}, ct));
	}

	public async Task<long> ChatSayAsync(string entityId, string text, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["text"] = text;
		return J.Long(await PostAsync("/chat/say", jsonObject, ct), "id", 0L);
	}

	public async Task<bool> ChatPrefAsync(string entityId, bool off, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["off"] = off;
		return J.Bool(await PostAsync("/chat/pref", jsonObject, ct), "off");
	}

	public async Task<MarketSearch> MarketSearchAsync(string entityId, string? name, string? group, int? levelMin, int? levelMax, string sort, int skip, bool favoriteOnly, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		if (!string.IsNullOrWhiteSpace(name))
		{
			jsonObject["name"] = name.Trim();
		}
		if (!string.IsNullOrEmpty(group))
		{
			jsonObject["group"] = group;
		}
		if (levelMin.HasValue)
		{
			jsonObject["level_min"] = levelMin;
		}
		if (levelMax.HasValue)
		{
			jsonObject["level_max"] = levelMax;
		}
		jsonObject["sort"] = sort;
		jsonObject["skip"] = Math.Max(0, skip);
		if (favoriteOnly)
		{
			jsonObject["favorite_only"] = true;
		}
		return MarketSearch.Parse(await PostAsync("/market/search", jsonObject, ct));
	}

	public async Task<BuyResult> MarketBuyAsync(string entityId, string productId, long expectPrice, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["product_id"] = productId;
		jsonObject["expect_price"] = expectPrice;
		return BuyResult.Parse(await PostAsync("/market/buy", jsonObject, ct));
	}

	public async Task<MarketMine> MarketMineAsync(string entityId, CancellationToken ct = default(CancellationToken))
	{
		return MarketMine.Parse(await PostAsync("/market/mine", B(entityId), ct));
	}

	public async Task<CollectResult> MarketCollectAsync(string entityId, CancellationToken ct = default(CancellationToken))
	{
		return CollectResult.Parse(await PostAsync("/market/collect", B(entityId), ct));
	}

	public async Task<string[]> MarketFavoriteAsync(string entityId, string productId, bool on, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["product_id"] = productId;
		jsonObject["on"] = on;
		return J.StrArr(await PostAsync("/market/favorite", jsonObject, ct), "favorites");
	}

	public async Task<AuctionResult> AuctionSearchAsync(string entityId, string? name, string? group, string sort, int skip, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		if (!string.IsNullOrWhiteSpace(name))
		{
			jsonObject["name"] = name.Trim();
		}
		if (!string.IsNullOrEmpty(group))
		{
			jsonObject["group"] = group;
		}
		jsonObject["sort"] = sort;
		jsonObject["skip"] = Math.Max(0, skip);
		return AuctionResult.Parse(await PostAsync("/auction/search", jsonObject, ct));
	}

	public async Task<AuctionResult> AuctionBidAsync(string entityId, string lotId, long amount, long expect, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["lot_id"] = lotId;
		jsonObject["amount"] = amount;
		jsonObject["expect"] = expect;
		return AuctionResult.Parse(await PostAsync("/auction/bid", jsonObject, ct));
	}

	public async Task<AuctionResult> AuctionMineAsync(string entityId, string kind, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["kind"] = kind;
		return AuctionResult.Parse(await PostAsync("/auction/mine", jsonObject, ct));
	}

	public async Task<AuctionResult> AuctionClaimAsync(string entityId, string? claimId, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		if (claimId != null)
		{
			jsonObject["claim_id"] = claimId;
		}
		return AuctionResult.Parse(await PostAsync("/auction/claim", jsonObject, ct));
	}

	public async Task<TradePlayer[]> TradePlayersAsync(string entityId, string name, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["name"] = name.Trim();
		return J.Objs(await PostAsync("/trade/players", jsonObject, ct), "players").Select(TradePlayer.Parse).ToArray();
	}

	private static JsonObject DraftBody(string entityId, OfferDraft d)
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["give_item_ids"] = new JsonArray(((IEnumerable<string>)d.GiveItemIds).Select((Func<string, JsonNode>)((string x) => x)).ToArray());
		jsonObject["give_tstone"] = d.GiveTstone;
		jsonObject["want_item_ids"] = new JsonArray(((IEnumerable<string>)d.WantItemIds).Select((Func<string, JsonNode>)((string x) => x)).ToArray());
		jsonObject["want_tstone"] = d.WantTstone;
		if (d.Message.Length > 0)
		{
			jsonObject["message"] = d.Message;
		}
		return jsonObject;
	}

	public async Task<TradeOffer> OfferCreateAsync(string entityId, OfferDraft d, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = DraftBody(entityId, d with
		{
			WantItemIds = Array.Empty<string>()
		});
		jsonObject["to_entity_id"] = d.ToEntityId;
		return TradeOffer.Parse(J.Obj(await PostAsync("/trade/offers/create", jsonObject, ct), "offer"));
	}

	public async Task<TradeOffer> OfferEditAsync(string entityId, string offerId, int version, OfferDraft d, bool sendWantItems, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["offer_id"] = offerId;
		jsonObject["version"] = version;
		jsonObject["give_item_ids"] = new JsonArray(((IEnumerable<string>)d.GiveItemIds).Select((Func<string, JsonNode>)((string x) => x)).ToArray());
		jsonObject["give_tstone"] = d.GiveTstone;
		jsonObject["want_tstone"] = d.WantTstone;
		if (sendWantItems)
		{
			jsonObject["want_item_ids"] = new JsonArray(((IEnumerable<string>)d.WantItemIds).Select((Func<string, JsonNode>)((string x) => x)).ToArray());
		}
		jsonObject["message"] = d.Message;
		return TradeOffer.Parse(J.Obj(await PostAsync("/trade/offers/edit", jsonObject, ct), "offer"));
	}

	public async Task<OfferList> OffersAsync(string entityId, string box, long? since = null, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["box"] = box;
		if (since.HasValue)
		{
			jsonObject["since"] = since;
		}
		return OfferList.Parse(await PostAsync("/trade/offers/list", jsonObject, ct));
	}

	public async Task<TradeOffer> OfferGetAsync(string entityId, string offerId, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["offer_id"] = offerId;
		return TradeOffer.Parse(J.Obj(await PostAsync("/trade/offers/get", jsonObject, ct), "offer"));
	}

	public async Task<(TradeOffer Offer, string MailId)> OfferAcceptAsync(string entityId, string offerId, int version, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["offer_id"] = offerId;
		jsonObject["version"] = version;
		JsonElement e = await PostAsync("/trade/offers/accept", jsonObject, ct);
		return (Offer: TradeOffer.Parse(J.Obj(e, "offer")), MailId: J.Str(e, "mail_id"));
	}

	public Task<TradeOffer> OfferDeclineAsync(string entityId, string offerId, CancellationToken ct = default(CancellationToken))
	{
		return OfferCloseAsync("/trade/offers/decline", entityId, offerId, ct);
	}

	public Task<TradeOffer> OfferCancelAsync(string entityId, string offerId, CancellationToken ct = default(CancellationToken))
	{
		return OfferCloseAsync("/trade/offers/cancel", entityId, offerId, ct);
	}

	private async Task<TradeOffer> OfferCloseAsync(string path, string entityId, string offerId, CancellationToken ct)
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["offer_id"] = offerId;
		return TradeOffer.Parse(J.Obj(await PostAsync(path, jsonObject, ct), "offer"));
	}

	public async Task<TradeOffer> OfferCounterAsync(string entityId, string offerId, OfferDraft d, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = DraftBody(entityId, d);
		jsonObject["offer_id"] = offerId;
		return TradeOffer.Parse(J.Obj(await PostAsync("/trade/offers/counter", jsonObject, ct), "offer"));
	}

	public async Task<OfferChat> OfferChatAsync(string entityId, string offerId, long since, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["offer_id"] = offerId;
		jsonObject["since"] = since;
		return OfferChat.Parse(await PostAsync("/trade/offers/chat", jsonObject, ct));
	}

	public async Task<long> OfferChatSayAsync(string entityId, string offerId, string text, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["offer_id"] = offerId;
		jsonObject["text"] = text;
		return J.Long(await PostAsync("/trade/offers/chat/say", jsonObject, ct), "id", 0L);
	}

	public async Task<CouponResult> CouponAsync(string entityId, string code, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = B(entityId);
		jsonObject["code"] = code;
		return CouponResult.Parse(await PostAsync("/coupon", jsonObject, ct, CouponText.Fallback));
	}

	public async Task<CouponHistory> CouponHistoryAsync(CancellationToken ct = default(CancellationToken))
	{
		return CouponHistory.Parse(await PostAsync("/coupon/history", new JsonObject(), ct, CouponText.Fallback));
	}

	public static async Task<byte[]?> IconAsync(string launcherApi, string icon, HttpClient? http = null, CancellationToken ct = default(CancellationToken))
	{
		if (!ValidIconName(icon))
		{
			return null;
		}
		string text = launcherApi.TrimEnd('/') + "/icons/" + icon + ".webp";
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttps && (!(result.Scheme == Uri.UriSchemeHttp) || (!result.IsLoopback && !(result.Host == "localhost")))))
		{
			return null;
		}
		if (http == null)
		{
			http = CreateHttp();
		}
		using HttpResponseMessage resp = await http.GetAsync(text, ct);
		if (resp.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
		resp.EnsureSuccessStatusCode();
		byte[] array = await resp.Content.ReadAsByteArrayAsync(ct);
		return (array.Length != 0) ? array : null;
	}

	public Task<byte[]?> IconAsync(string icon, CancellationToken ct = default(CancellationToken))
	{
		return IconAsync(Base, icon, _http, ct);
	}
}

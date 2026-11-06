using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DurangoLauncher;

public static class AccountApi
{
	private static readonly HttpClient Http = CreateHttp();

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

	private static HttpClient CreateHttp()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromSeconds(15L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DurangoLauncher/" + GameInstall.LauncherVersion);
		httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
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

	private static async Task<JsonElement> PostAsync(string url, JsonObject body, CancellationToken ct = default(CancellationToken))
	{
		if (!SafeUrl(url))
		{
			throw new ApiError("insecure", L.T("ท\u0e35\u0e48อย\u0e39\u0e48ระบบบ\u0e31ญช\u0e35ไม\u0e48ใช\u0e48 HTTPS — ไม\u0e48ส\u0e48งรห\u0e31สผ\u0e48าน", "The account server is not HTTPS — refusing to send credentials"));
		}
		HttpResponseMessage resp;
		try
		{
			using StringContent content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
			resp = await Http.PostAsync(url, content, ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex2)
		{
			Log.Write("account POST " + Path(url) + ": " + ex2.Message);
			throw new ApiError("network", L.T("ต\u0e34ดต\u0e48อระบบบ\u0e31ญช\u0e35ไม\u0e48ได\u0e49 — ตรวจเน\u0e47ตแล\u0e49วลองใหม\u0e48", "Cannot reach the account server — check your connection and try again"));
		}
		using (resp)
		{
			string text = await resp.Content.ReadAsStringAsync(ct);
			JsonElement jsonElement;
			try
			{
				jsonElement = JsonDocument.Parse((text.Length > 0) ? text : "{}").RootElement.Clone();
			}
			catch
			{
				Log.Write($"account POST {Path(url)}: HTTP {resp.StatusCode} ไม\u0e48ใช\u0e48 JSON");
				throw new ApiError("server", L.T($"ระบบบ\u0e31ญช\u0e35ตอบผ\u0e34ดร\u0e39ปแบบ (HTTP {resp.StatusCode}) — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", $"The account server returned an unexpected reply (HTTP {resp.StatusCode}) — please try again"), (int)resp.StatusCode);
			}
			if (resp.IsSuccessStatusCode && (!jsonElement.TryGetProperty("ok", out var value) || value.ValueKind != JsonValueKind.False))
			{
				return jsonElement;
			}
			throw ToError(jsonElement, (int)resp.StatusCode, url);
		}
	}

	private static ApiError ToError(JsonElement root, int status, string url)
	{
		string text = "server";
		string text2 = "";
		if (root.TryGetProperty("error", out var value))
		{
			if (value.ValueKind == JsonValueKind.Object)
			{
				text = Str(value, "code", text);
				text2 = Str(value, "message", "");
			}
			else if (value.ValueKind == JsonValueKind.String)
			{
				text = value.GetString() ?? text;
			}
		}
		if (text2.Length == 0)
		{
			text2 = L.T("ระบบบ\u0e31ญช\u0e35ข\u0e31ดข\u0e49อง (" + text + ") — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Account server error (" + text + ") — please try again");
		}
		Log.Write($"account POST {Path(url)}: HTTP {status} {text}");
		JsonElement value2;
		JsonElement eo = ((root.TryGetProperty("error", out value2) && value2.ValueKind == JsonValueKind.Object) ? value2 : default(JsonElement));
		DiscordUser discordUser = DiscordReply.DiscordOf(root);
		return new ApiError(text, text2, status)
		{
			AttemptsLeft = Pick("attempts_left"),
			RetryAfter = (Pick("retry_after_s") ?? Pick("retry_after")),
			Username = PickS("username"),
			Reason = PickS("reason"),
			SignupToken = DiscordReply.Field(root, "signup_token"),
			LoginToken = DiscordReply.Field(root, "login_token"),
			DiscordId = discordUser.Id,
			DiscordName = discordUser.Name,
			DiscordAvatar = discordUser.AvatarUrl
		};
		static int? Int(JsonElement r, string k)
		{
			if (!r.TryGetProperty(k, out var value3) || value3.ValueKind != JsonValueKind.Number || !value3.TryGetInt32(out var value4))
			{
				return null;
			}
			return value4;
		}
		int? Pick(string k)
		{
			int? result = Int(root, k);
			if (!result.HasValue)
			{
				if (eo.ValueKind != JsonValueKind.Object)
				{
					return null;
				}
				return Int(eo, k);
			}
			return result;
		}
		string? PickS(string k)
		{
			string text3 = Str(root, k, "");
			if (text3 == null || text3.Length <= 0)
			{
				if (eo.ValueKind == JsonValueKind.Object)
				{
					string text4 = Str(eo, k, "");
					if (text4 != null && text4.Length > 0)
					{
						return text4;
					}
				}
				return null;
			}
			return text3;
		}
	}

	private static string Path(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result))
		{
			return url;
		}
		return result.AbsolutePath;
	}

	private static string Str(JsonElement e, string k, string def)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return def;
		}
		return value.GetString() ?? def;
	}

	private static int Num(JsonElement e, string k, int def)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var value2))
		{
			return def;
		}
		return value2;
	}

	private static DiscordUser ParseDiscord(JsonElement root)
	{
		return DiscordReply.DiscordOf(root);
	}

	public static LoginResult ParseLogin(JsonElement root)
	{
		string text = Str(root, "ticket", "");
		if (text.Length == 0)
		{
			throw new ApiError("server", L.T("ระบบบ\u0e31ญช\u0e35ไม\u0e48ส\u0e48งต\u0e31\u0e4bวเข\u0e49าเกม — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "The account server sent no game ticket — please try again"));
		}
		JsonElement value;
		JsonElement e = ((root.TryGetProperty("user", out value) && value.ValueKind == JsonValueKind.Object) ? value : default(JsonElement));
		DiscordUser discordUser = ParseDiscord(root);
		JsonElement value2;
		string[] array = ((root.TryGetProperty("roles", out value2) && value2.ValueKind == JsonValueKind.Array) ? (from v in value2.EnumerateArray()
			where v.ValueKind == JsonValueKind.String
			select v.GetString()).ToArray() : Array.Empty<string>());
		JsonElement value3;
		bool supporter = (root.TryGetProperty("supporter", out value3) ? (value3.ValueKind == JsonValueKind.True) : array.Any((string x) => x.Equals("Supporter", StringComparison.OrdinalIgnoreCase)));
		string text2 = Str(root, "refresh_token", "");
		JsonElement value4;
		bool hasDiscord = !root.TryGetProperty("has_discord", out value4) || value4.ValueKind != JsonValueKind.False;
		return new LoginResult(text, Num(root, "ticket_expires_in", 120), (text2.Length > 0) ? text2 : null, Num(root, "refresh_expires_in", 2592000), Str(e, "username", ""), discordUser.Id, discordUser.Name, discordUser.AvatarUrl, array, supporter)
		{
			Email = Str(root, "email", Str(e, "email", "")),
			HasDiscord = hasDiscord
		};
	}

	private static JsonObject Device(AccountConfig c, bool remember)
	{
		return new JsonObject
		{
			["remember"] = remember,
			["server"] = c.Server,
			["device_id"] = DeviceId.Value,
			["device_name"] = "Windows · " + Environment.MachineName,
			["lang"] = Lang
		};
	}

	public static async Task<LoginResult> LoginAsync(AccountConfig c, string username, string password, bool remember)
	{
		JsonObject jsonObject = Device(c, remember);
		jsonObject["username"] = username;
		jsonObject["password"] = password;
		return ParseLogin(await PostAsync(c.Api + "/login", jsonObject));
	}

	public static async Task<LoginResult> RefreshAsync(AccountConfig c, string refreshToken)
	{
		return ParseLogin(await PostAsync(c.Api + "/refresh", new JsonObject
		{
			["refresh_token"] = refreshToken,
			["server"] = c.Server,
			["lang"] = Lang
		}));
	}

	public static async Task LogoutAsync(AccountConfig c, string refreshToken, CancellationToken ct = default(CancellationToken))
	{
		try
		{
			await PostAsync(c.Api + "/logout", new JsonObject
			{
				["refresh_token"] = refreshToken,
				["lang"] = Lang
			}, ct);
		}
		catch (Exception ex)
		{
			Log.Write("logout: " + ex.Message);
		}
	}

	public static async Task<LoginResult> SignupAsync(AccountConfig c, string signupToken, string username, string password, bool remember)
	{
		JsonObject jsonObject = Device(c, remember);
		jsonObject["signup_token"] = signupToken;
		jsonObject["username"] = username;
		jsonObject["password"] = password;
		return ParseLogin(await PostAsync(c.LauncherApi + "/signup", jsonObject));
	}

	public static async Task<LoginResult> ResetAsync(AccountConfig c, string resetToken, string password, bool remember)
	{
		JsonObject jsonObject = Device(c, remember);
		jsonObject["reset_token"] = resetToken;
		jsonObject["password"] = password;
		return ParseLogin(await PostAsync(c.LauncherApi + "/reset", jsonObject));
	}

	public static async Task<(string Token, int ExpiresIn)> ChangePasswordAsync(AccountConfig c, string refreshToken, string oldPw, string newPw)
	{
		JsonElement e = await PostAsync(c.LauncherApi + "/password", new JsonObject
		{
			["refresh_token"] = refreshToken,
			["old_password"] = oldPw,
			["new_password"] = newPw,
			["lang"] = Lang
		});
		return (Token: Str(e, "refresh_token", refreshToken), ExpiresIn: Num(e, "refresh_expires_in", 2592000));
	}

	public static async Task<DiscordStart> DiscordStartAsync(AccountConfig c, string purpose, string redirectUri, CancellationToken ct, string? refreshToken = null)
	{
		JsonObject jsonObject = new JsonObject
		{
			["purpose"] = purpose,
			["redirect_uri"] = redirectUri,
			["lang"] = Lang
		};
		if (refreshToken != null)
		{
			jsonObject["refresh_token"] = refreshToken;
		}
		JsonElement e = await PostAsync(c.LauncherApi + "/discord/start", jsonObject, ct);
		string url = Str(e, "url", "");
		if (!SafeUrl(url))
		{
			throw new ApiError("server", L.T("ระบบบ\u0e31ญช\u0e35ไม\u0e48ส\u0e48งล\u0e34งก\u0e4c Discord — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "The account server sent no Discord link — please try again"));
		}
		string text = Str(e, "poll_id", "");
		return new DiscordStart(Str(e, "state", ""), url, Num(e, "expires_in", 300), (text.Length > 0) ? text : null, Math.Clamp(Num(e, "interval", 2), 1, 10));
	}

	public static async Task<DiscordDone> DiscordFinishAsync(AccountConfig c, string state, string code, string redirectUri, CancellationToken ct)
	{
		return ParseDone(await PostAsync(c.LauncherApi + "/discord/finish", new JsonObject
		{
			["state"] = state,
			["code"] = code,
			["redirect_uri"] = redirectUri,
			["lang"] = Lang
		}, ct));
	}

	public static async Task<DiscordDone?> DiscordPollAsync(AccountConfig c, string pollId, CancellationToken ct)
	{
		JsonElement jsonElement = await PostAsync(c.LauncherApi + "/discord/poll", new JsonObject
		{
			["poll_id"] = pollId,
			["lang"] = Lang
		}, ct);
		string text = Str(jsonElement, "status", "pending");
		if (text == "pending")
		{
			return null;
		}
		if (text == "error")
		{
			string text2 = Str(jsonElement, "code", "server");
			string text3 = Str(jsonElement, "message", "");
			DiscordUser discordUser = DiscordReply.DiscordOf(jsonElement);
			throw new ApiError(text2, (text3.Length > 0) ? text3 : L.T("ย\u0e37นย\u0e31น Discord ไม\u0e48สำเร\u0e47จ (" + text2 + ")", "Discord verification failed (" + text2 + ")"), 200)
			{
				Username = DiscordReply.Field(jsonElement, "username"),
				SignupToken = DiscordReply.Field(jsonElement, "signup_token"),
				LoginToken = DiscordReply.Field(jsonElement, "login_token"),
				DiscordId = discordUser.Id,
				DiscordName = discordUser.Name,
				DiscordAvatar = discordUser.AvatarUrl
			};
		}
		return ParseDone(jsonElement);
	}

	private static DiscordDone ParseDone(JsonElement r)
	{
		return DiscordReply.ParseDone(r);
	}

	public static async Task<LoginResult> DiscordLoginAsync(AccountConfig c, string loginToken, bool remember, CancellationToken ct = default(CancellationToken))
	{
		JsonObject jsonObject = Device(c, remember);
		jsonObject["login_token"] = loginToken;
		return ParseLogin(await PostAsync(c.LauncherApi + "/discord/login", jsonObject, ct));
	}

	public static async Task<AccountFeatures> FeaturesAsync(AccountConfig c, CancellationToken ct = default(CancellationToken))
	{
		string text = c.LauncherApi + "/features";
		if (!SafeUrl(text))
		{
			return AccountFeatures.Off;
		}
		try
		{
			using HttpResponseMessage resp = await Http.GetAsync(text, ct);
			if (!resp.IsSuccessStatusCode)
			{
				return AccountFeatures.Off;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
			return AccountFeatures.Parse(jsonDocument.RootElement);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException) || !ct.IsCancellationRequested)
		{
			Log.Write("account features: " + ex.Message);
			return AccountFeatures.Off;
		}
	}

	public static async Task<EmailSent> EmailStartAsync(AccountConfig c, string email, string purpose, string? refreshToken = null, string? password = null)
	{
		JsonObject jsonObject = new JsonObject
		{
			["email"] = email,
			["purpose"] = purpose,
			["lang"] = Lang
		};
		if (purpose == "add")
		{
			jsonObject["refresh_token"] = refreshToken ?? "";
			jsonObject["password"] = password ?? "";
		}
		JsonElement e = await PostAsync(c.LauncherApi + "/email/start", jsonObject);
		string text = Str(e, "email_id", "");
		if (text.Length == 0)
		{
			throw new ApiError("server", L.T("ระบบบ\u0e31ญช\u0e35ไม\u0e48ตอบร\u0e31บคำขอรห\u0e31ส — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "The account server did not accept the request — please try again"));
		}
		return new EmailSent(text, Str(e, "email", email), Num(e, "expires_in", 600), Num(e, "resend_in", 60));
	}

	public static async Task<EmailVerified> EmailVerifyAsync(AccountConfig c, string emailId, string code)
	{
		JsonElement e = await PostAsync(c.LauncherApi + "/email/verify", new JsonObject
		{
			["email_id"] = emailId,
			["code"] = code,
			["lang"] = Lang
		});
		string text = Str(e, "signup_token", "");
		string text2 = Str(e, "reset_token", "");
		string text3 = Str(e, "username", "");
		string text4 = Str(e, "purpose", "");
		if (text4 == "add")
		{
			return new EmailVerified(null, null, (text3.Length > 0) ? text3 : null, Str(e, "email", ""), text4);
		}
		if (text.Length == 0 && text2.Length == 0)
		{
			throw new ApiError("server", L.T("ย\u0e37นย\u0e31นอ\u0e35เมลไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Email verification failed — please try again"));
		}
		return new EmailVerified((text.Length > 0) ? text : null, (text2.Length > 0) ? text2 : null, (text3.Length > 0) ? text3 : null, Str(e, "email", ""));
	}

	public static async Task<Bitmap?> AvatarAsync(string url)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result) || result.Scheme != Uri.UriSchemeHttps)
		{
			return null;
		}
		try
		{
			using MemoryStream memoryStream = new MemoryStream(await Http.GetByteArrayAsync(result));
			Image val = Image.FromStream((Stream)memoryStream);
			try
			{
				return new Bitmap(val);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			Log.Write("avatar: " + ex.Message);
			return null;
		}
	}
}

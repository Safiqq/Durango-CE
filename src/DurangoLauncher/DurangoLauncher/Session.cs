using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DurangoLauncher;

public sealed class Session
{
	public static readonly string Dir = ((Environment.GetEnvironmentVariable("DURANGO_LAUNCHER_TEST") == "1") ? Path.Combine(Path.GetTempPath(), "DurangoLauncher-test") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DurangoLauncher"));

	public string Username = "";

	public string DiscordId = "";

	public string DiscordName = "";

	public string AvatarUrl = "";

	public string[] Roles = Array.Empty<string>();

	public bool Supporter;

	public string Email = "";

	public bool HasDiscord = true;

	public string RefreshToken = "";

	public DateTime RefreshUntilUtc;

	public bool Remember;

	public string Via = "password";

	public string? Ticket;

	public DateTime TicketAtUtc;

	private static string FilePath => Path.Combine(Dir, "account.dat");

	private static string LastUserPath => Path.Combine(Dir, "last-user.txt");

	public static Session From(LoginResult r, bool remember, string? fallbackUser = null, string via = "password")
	{
		return new Session
		{
			Remember = remember,
			Via = via
		}.Apply(r, fallbackUser);
	}

	public Session Apply(LoginResult r, string? fallbackUser = null)
	{
		Username = ((r.Username.Length > 0) ? r.Username : (fallbackUser ?? Username));
		HasDiscord = r.HasDiscord;
		DiscordId = ((!r.HasDiscord) ? "" : ((r.DiscordId.Length > 0) ? r.DiscordId : DiscordId));
		DiscordName = ((!r.HasDiscord) ? "" : ((r.DiscordName.Length > 0) ? r.DiscordName : DiscordName));
		Email = r.Email;
		AvatarUrl = r.AvatarUrl;
		Roles = r.Roles;
		Supporter = r.Supporter;
		if (r.RefreshToken != null)
		{
			RefreshToken = r.RefreshToken;
			RefreshUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, r.RefreshExpiresIn));
		}
		Ticket = r.Ticket;
		TicketAtUtc = DateTime.UtcNow;
		return this;
	}

	public void Save()
	{
		if (!Remember || RefreshToken.Length == 0)
		{
			Delete();
			return;
		}
		try
		{
			JsonObject jsonObject = new JsonObject
			{
				["v"] = 1,
				["username"] = Username,
				["discord_id"] = DiscordId,
				["discord_name"] = DiscordName,
				["avatar_url"] = AvatarUrl,
				["supporter"] = Supporter,
				["roles"] = new JsonArray(((IEnumerable<string>)Roles).Select((Func<string, JsonNode>)((string x) => x)).ToArray()),
				["email"] = Email,
				["has_discord"] = HasDiscord,
				["refresh_token"] = RefreshToken,
				["refresh_until"] = RefreshUntilUtc.ToString("o"),
				["via"] = Via
			};
			Directory.CreateDirectory(Dir);
			File.WriteAllBytes(FilePath, Dpapi.Protect(Encoding.UTF8.GetBytes(jsonObject.ToJsonString())));
			SaveLastUser(Username);
		}
		catch (Exception ex)
		{
			Log.Write("บ\u0e31นท\u0e36กบ\u0e31ญช\u0e35ไม\u0e48ได\u0e49: " + ex.Message);
		}
	}

	public static Session? Load()
	{
		try
		{
			if (!File.Exists(FilePath))
			{
				return null;
			}
			using JsonDocument jsonDocument = JsonDocument.Parse(Encoding.UTF8.GetString(Dpapi.Unprotect(File.ReadAllBytes(FilePath))));
			JsonElement r = jsonDocument.RootElement;
			JsonElement value;
			JsonElement value2;
			JsonElement value3;
			DateTime result;
			Session session = new Session
			{
				Remember = true,
				Username = S("username"),
				DiscordId = S("discord_id"),
				DiscordName = S("discord_name"),
				AvatarUrl = S("avatar_url"),
				Supporter = (r.TryGetProperty("supporter", out value) && value.ValueKind == JsonValueKind.True),
				Email = S("email"),
				HasDiscord = (!r.TryGetProperty("has_discord", out value2) || value2.ValueKind != JsonValueKind.False),
				Roles = ((r.TryGetProperty("roles", out value3) && value3.ValueKind == JsonValueKind.Array) ? (from v in value3.EnumerateArray()
					where v.ValueKind == JsonValueKind.String
					select v.GetString()).ToArray() : Array.Empty<string>()),
				RefreshToken = S("refresh_token"),
				Via = LoginVia.Normalize(S("via")),
				RefreshUntilUtc = (DateTime.TryParse(S("refresh_until"), null, DateTimeStyles.RoundtripKind, out result) ? result.ToUniversalTime() : DateTime.UtcNow)
			};
			if (session.RefreshToken.Length == 0 || session.RefreshUntilUtc < DateTime.UtcNow)
			{
				Delete();
				return null;
			}
			return session;
			string S(string k)
			{
				if (!r.TryGetProperty(k, out var value4) || value4.ValueKind != JsonValueKind.String)
				{
					return "";
				}
				return value4.GetString() ?? "";
			}
		}
		catch (Exception ex)
		{
			Log.Write("อ\u0e48านบ\u0e31ญช\u0e35ท\u0e35\u0e48จำไว\u0e49ไม\u0e48ได\u0e49 (เคร\u0e37\u0e48อง/ผ\u0e39\u0e49ใช\u0e49 Windows อ\u0e37\u0e48น?): " + ex.Message);
			Delete();
			return null;
		}
	}

	public static void Delete()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				File.Delete(FilePath);
			}
		}
		catch
		{
		}
	}

	public static string LastUser()
	{
		try
		{
			return File.Exists(LastUserPath) ? File.ReadAllText(LastUserPath).Trim() : "";
		}
		catch
		{
			return "";
		}
	}

	public static void SaveLastUser(string user)
	{
		try
		{
			Directory.CreateDirectory(Dir);
			if (user.Length == 0)
			{
				if (File.Exists(LastUserPath))
				{
					File.Delete(LastUserPath);
				}
			}
			else
			{
				File.WriteAllText(LastUserPath, user);
			}
		}
		catch
		{
		}
	}
}

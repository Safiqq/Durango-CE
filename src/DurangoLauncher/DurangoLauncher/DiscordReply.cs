using System.Text.Json;

namespace DurangoLauncher;

public static class DiscordReply
{
	private static JsonElement ErrObj(JsonElement root)
	{
		return J.Obj(root, "error");
	}

	public static string? Field(JsonElement root, string k)
	{
		string text = J.Str(root, k);
		if (text == null || text.Length <= 0)
		{
			string text2 = J.Str(ErrObj(root), k);
			if (text2 == null || text2.Length <= 0)
			{
				return null;
			}
			return text2;
		}
		return text;
	}

	public static DiscordUser DiscordOf(JsonElement root)
	{
		JsonElement e = J.Obj(root, "discord");
		if (e.ValueKind != JsonValueKind.Object)
		{
			e = J.Obj(ErrObj(root), "discord");
		}
		JsonElement e2 = J.Obj(root, "user");
		return new DiscordUser(J.Str(e, "id", J.Str(e2, "discord_id")), J.Str(e, "name", J.Str(e2, "discord_name")), J.Str(e, "avatar_url"));
	}

	public static DiscordDone ParseDone(JsonElement r)
	{
		string text = J.Str(r, "purpose");
		string text2 = Field(r, "signup_token");
		string text3 = Field(r, "reset_token");
		string text4 = Field(r, "login_token");
		string username = Field(r, "username");
		DiscordUser discord = DiscordOf(r);
		if (text == "link")
		{
			return new DiscordDone(discord, null, null, username, Linked: true, null, text);
		}
		if (text4 != null && (text == "login" || (text2 == null && text3 == null)))
		{
			return new DiscordDone(discord, null, null, username, Linked: false, text4, "login");
		}
		if (text2 == null && text3 == null)
		{
			throw new ApiError("server", L.T("ย\u0e37นย\u0e31น Discord ไม\u0e48สำเร\u0e47จ — ลองใหม\u0e48อ\u0e35กคร\u0e31\u0e49ง", "Discord verification failed — please try again"));
		}
		return new DiscordDone(discord, text2, text3, username, Linked: false, null, text);
	}
}

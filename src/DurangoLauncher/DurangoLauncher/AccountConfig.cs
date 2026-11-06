using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record AccountConfig(bool Required, string Server, string Api, string LauncherApi, string DiscordInvite)
{
	public const string DefaultBase = "https://admin.dorangu.tech/id";

	public const string DefaultInvite = "https://discord.gg/RMfeg6Drbu";

	public static readonly AccountConfig Default = new AccountConfig(Required: true, "CloseBeta", "https://admin.dorangu.tech/id/api/game", "https://admin.dorangu.tech/id/api/launcher", "https://discord.gg/RMfeg6Drbu");

	public static AccountConfig Parse(JsonElement root)
	{
		if (!root.TryGetProperty("account", out var a) || a.ValueKind != JsonValueKind.Object)
		{
			return Default;
		}
		JsonElement value;
		return new AccountConfig(!a.TryGetProperty("required", out value) || value.ValueKind != JsonValueKind.False, S("server", Default.Server), S("api", Default.Api), S("launcher_api", Default.LauncherApi), S("discord_invite", Default.DiscordInvite));
		string S(string k, string def)
		{
			if (a.TryGetProperty(k, out var value2) && value2.ValueKind == JsonValueKind.String)
			{
				string text = value2.GetString();
				if (text != null && text.Length > 0)
				{
					return text.TrimEnd('/');
				}
			}
			return def;
		}
	}

	[CompilerGenerated]
	private AccountConfig(AccountConfig original)
	{
		Required = original.Required;
		Server = original.Server;
		Api = original.Api;
		LauncherApi = original.LauncherApi;
		DiscordInvite = original.DiscordInvite;
	}
}

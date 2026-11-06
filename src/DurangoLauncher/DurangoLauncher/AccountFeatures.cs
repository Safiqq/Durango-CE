using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record AccountFeatures(bool EmailSignup, bool EmailReset, bool EmailAdd = false, bool DiscordLogin = false)
{
	public static readonly AccountFeatures Off = new AccountFeatures(EmailSignup: false, EmailReset: false);

	public static AccountFeatures Parse(JsonElement r)
	{
		return new AccountFeatures(B("email_signup"), B("email_reset"), B("email_add"), B("discord_login"));
		bool B(string k)
		{
			if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty(k, out var value))
			{
				return value.ValueKind == JsonValueKind.True;
			}
			return false;
		}
	}

	public bool CanAddEmail(bool hasDiscord, string email)
	{
		if (EmailAdd && hasDiscord)
		{
			return email.Length == 0;
		}
		return false;
	}

	[CompilerGenerated]
	private AccountFeatures(AccountFeatures original)
	{
		EmailSignup = original.EmailSignup;
		EmailReset = original.EmailReset;
		EmailAdd = original.EmailAdd;
		DiscordLogin = original.DiscordLogin;
	}
}

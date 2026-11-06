namespace DurangoLauncher;

public static class DiscordFlow
{
	public enum Next
	{
		Signup2,
		Reset,
		Login,
		Linked,
		NoId,
		NoIdBanner,
		Deny,
		Timeout,
		Error
	}

	public static Next OnDone(string purpose, DiscordDone d)
	{
		switch (purpose)
		{
		case "signup":
			if (d.SignupToken != null)
			{
				return Next.Signup2;
			}
			break;
		case "reset":
			if (d.ResetToken != null)
			{
				return Next.Reset;
			}
			break;
		case "login":
			if (d.LoginToken != null)
			{
				return Next.Login;
			}
			break;
		}
		return Next.Error;
	}

	public static Next OnError(string purpose, ApiError e)
	{
		switch (e.Code)
		{
		case "denied":
		case "access_denied":
			return Next.Deny;
		case "expired":
			return Next.Timeout;
		case "discord_already_registered":
			return Next.Linked;
		case "discord_not_linked":
			if (purpose == "login" && e.SignupToken != null)
			{
				return Next.NoId;
			}
			return Next.NoIdBanner;
		default:
			return Next.Error;
		}
	}
}

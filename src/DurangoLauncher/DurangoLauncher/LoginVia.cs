namespace DurangoLauncher;

public static class LoginVia
{
	public const string Password = "password";

	public const string Discord = "discord";

	public const string Signup = "signup";

	public static string Normalize(string? v)
	{
		if ((!(v == "discord") && !(v == "signup")) || 1 == 0)
		{
			return "password";
		}
		return v;
	}

	public static bool ShowDiscordRow(string? via, bool hasDiscord)
	{
		return via == "discord" && hasDiscord;
	}
}

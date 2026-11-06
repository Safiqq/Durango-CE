namespace DurangoLauncher;

public static class GuideLink
{
	public const string Base = "https://safiqq.github.io/Durango-CE/";

	public static string Url(string page, bool en)
	{
		return "https://safiqq.github.io/Durango-CE/#/" + (string.IsNullOrEmpty(page) ? "" : ("guides/" + page));
	}
}

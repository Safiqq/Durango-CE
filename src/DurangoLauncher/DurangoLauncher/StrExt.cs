namespace DurangoLauncher;

internal static class StrExt
{
	public static string OrIfEmpty(this string s, string other)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return s;
		}
		return other;
	}
}

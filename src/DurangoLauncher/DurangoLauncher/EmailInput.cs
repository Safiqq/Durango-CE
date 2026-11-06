using System.Linq;

namespace DurangoLauncher;

public static class EmailInput
{
	public static string CleanCode(string? s)
	{
		return new string((s ?? "").Where(char.IsAsciiDigit).Take(6).ToArray());
	}

	public static string Clean(string? s)
	{
		return new string((s ?? "").Where((char ch) => !char.IsWhiteSpace(ch)).ToArray());
	}

	public static bool LooksOk(string? s)
	{
		string text = Clean(s);
		int num = text.IndexOf('@');
		if (num <= 0 || num != text.LastIndexOf('@') || text.Length > 254)
		{
			return false;
		}
		string text2 = text;
		int num2 = num + 1;
		string text3 = text2.Substring(num2, text2.Length - num2);
		if (text3.Length >= 4 && text3.Contains('.') && !text3.StartsWith('.') && !text3.EndsWith('.'))
		{
			return !text3.Contains("..");
		}
		return false;
	}
}

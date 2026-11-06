using System;
using L10N;

namespace OffServer.Rift;

public static class RiftText
{
	public static string T(string th, string en)
	{
		string text = L10N.T._(th);
		bool flag = (LocalizeSystem.Locale ?? string.Empty).StartsWith("th", StringComparison.OrdinalIgnoreCase);
		if (!(text == th) || flag)
		{
			return text;
		}
		return en;
	}

	public static string T(string th, string en, params object[] args)
	{
		return string.Format(L10N.T.Culture, T(th, en), args);
	}
}

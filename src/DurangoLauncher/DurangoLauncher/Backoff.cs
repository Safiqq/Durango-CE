using System;

namespace DurangoLauncher;

public sealed class Backoff
{
	public const int MaxMs = 60000;

	public int BaseMs { get; }

	public int CurrentMs { get; private set; }

	public int Failures { get; private set; }

	public Backoff(int baseMs)
	{
		BaseMs = Math.Max(250, baseMs);
		CurrentMs = BaseMs;
	}

	public int Ok()
	{
		Failures = 0;
		CurrentMs = BaseMs;
		return CurrentMs;
	}

	public int Fail(int? retryAfterS = null)
	{
		Failures++;
		int num = Math.Min(60000, Math.Max(BaseMs, CurrentMs) * 2);
		if (retryAfterS.HasValue)
		{
			int valueOrDefault = retryAfterS.GetValueOrDefault();
			if (valueOrDefault > 0)
			{
				num = Math.Max(num, Math.Min(60000, valueOrDefault * 1000));
			}
		}
		CurrentMs = num;
		return CurrentMs;
	}

	public static bool ShouldBackoff(ApiError e)
	{
		string code = e.Code;
		bool flag = ((code == "network" || code == "rate_limited") ? true : false);
		bool flag2 = flag;
		if (!flag2)
		{
			int status = e.Status;
			bool flag3 = ((status >= 500 || status == 429) ? true : false);
			flag2 = flag3;
		}
		return flag2;
	}
}

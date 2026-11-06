using System;

namespace DurangoLauncher;

public static class ServerClock
{
	public static readonly TimeSpan Threshold = TimeSpan.FromSeconds(5L);

	public static readonly TimeSpan StatusThreshold = TimeSpan.FromSeconds(30L);

	public static TimeSpan Skew(DateTimeOffset? serverTime, DateTimeOffset localNow)
	{
		return Skew(serverTime, localNow, Threshold);
	}

	public static TimeSpan Skew(DateTimeOffset? serverTime, DateTimeOffset localNow, TimeSpan threshold)
	{
		if (serverTime.HasValue)
		{
			DateTimeOffset valueOrDefault = serverTime.GetValueOrDefault();
			TimeSpan result = valueOrDefault - localNow;
			if (!(result.Duration() < threshold))
			{
				return result;
			}
			return TimeSpan.Zero;
		}
		return TimeSpan.Zero;
	}
}

using System;

namespace DurangoLauncher;

public sealed class UpdateInfo
{
	public string LatestVersion = "";

	public string MinVersion = "";

	public NewsItem[] News = Array.Empty<NewsItem>();

	public bool ServerOnline;

	public int? OnlinePlayers;

	public Countdown? Countdown;

	public TimeSpan ClockSkew;

	public AccountConfig Account = AccountConfig.Default;

	public string? Error;

	public DateTimeOffset Now => DateTimeOffset.UtcNow + ClockSkew;
}

namespace DurangoLauncher;

public sealed record DiscordStart(string State, string Url, int ExpiresIn, string? PollId, int Interval);

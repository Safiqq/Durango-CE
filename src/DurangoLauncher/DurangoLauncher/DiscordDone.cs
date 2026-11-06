namespace DurangoLauncher;

public sealed record DiscordDone(DiscordUser Discord, string? SignupToken, string? ResetToken, string? Username, bool Linked = false, string? LoginToken = null, string Purpose = "");

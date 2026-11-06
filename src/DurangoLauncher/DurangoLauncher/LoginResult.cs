namespace DurangoLauncher;

public sealed record LoginResult(string Ticket, int TicketExpiresIn, string? RefreshToken, int RefreshExpiresIn, string Username, string DiscordId, string DiscordName, string AvatarUrl, string[] Roles, bool Supporter)
{
	public string Email { get; init; } = "";

	public bool HasDiscord { get; init; } = true;
}

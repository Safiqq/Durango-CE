using System.Runtime.CompilerServices;

namespace DurangoLauncher;

public sealed record DiscordUser(string Id, string Name, string AvatarUrl)
{
	public bool IsEmpty
	{
		get
		{
			if (Id.Length == 0)
			{
				return Name.Length == 0;
			}
			return false;
		}
	}

	public static readonly DiscordUser None = new DiscordUser("", "", "");

	[CompilerGenerated]
	private DiscordUser(DiscordUser original)
	{
		Id = original.Id;
		Name = original.Name;
		AvatarUrl = original.AvatarUrl;
	}
}

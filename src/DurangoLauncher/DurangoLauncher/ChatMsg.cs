using System.Text.Json;

namespace DurangoLauncher;

public sealed record ChatMsg(long Id, long T, string Name, string Src, string Text)
{
	public static ChatMsg Parse(JsonElement e)
	{
		return new ChatMsg(J.Long(e, "id", 0L), J.Long(e, "t", 0L), J.Str(e, "name"), J.Str(e, "src", J.Bool(e, "web") ? "web" : "game"), J.Str(e, "text"));
	}
}

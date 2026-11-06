using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record ChatHistory(bool Enabled, int Cooldown, int MaxLen, long Last, ChatMsg[] Messages)
{
	public static ChatHistory Parse(JsonElement r)
	{
		JsonElement value;
		return new ChatHistory(!r.TryGetProperty("enabled", out value) || value.ValueKind != JsonValueKind.False, J.Int(r, "cooldown", 10), J.Int(r, "max_len", 300), J.Long(r, "last", 0L), J.Objs(r, "messages").Select(ChatMsg.Parse).ToArray());
	}
}

using System.Text.Json;

namespace DurangoLauncher;

public sealed record CollectResult(long Total, long Gross, long Tax, int Count)
{
	public static CollectResult Parse(JsonElement r)
	{
		return new CollectResult(J.Long(r, "total", 0L), J.Long(r, "gross", 0L), J.Long(r, "tax", 0L), J.Int(r, "count"));
	}
}

using System.Text.Json;

namespace DurangoLauncher;

public sealed record BuyResult(string MailId, long Price, long Fee, double FeeRate, long Tstone)
{
	public static BuyResult Parse(JsonElement r)
	{
		JsonElement value;
		return new BuyResult(J.Str(r, "mail_id"), J.Long(r, "price", 0L), J.Long(r, "fee", 0L), J.Dbl(r, "fee_rate"), (r.TryGetProperty("wallet", out value) && value.ValueKind == JsonValueKind.Object) ? J.Long(value, "tstone", 0L) : J.Long(r, "tstone", 0L));
	}
}

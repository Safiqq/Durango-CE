using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record CouponResult(string Kind, string Code, string Name, long Tstone, CouponGift[] Items, CouponGift[] Vouchers, int MailDays, string EntityId, string ToName, long CoinAdded, long Coin)
{
	public bool IsCoin => Kind == "coin";

	public static CouponResult Parse(JsonElement r)
	{
		return new CouponResult(J.Str(r, "kind", J.LongN(r, "coin_added").HasValue ? "coin" : "reward"), J.Str(r, "code"), J.Str(r, "name"), J.Long(r, "tstone", 0L), J.Objs(r, "items").Select(CouponGift.Parse).ToArray(), J.Objs(r, "vouchers").Select(CouponGift.Parse).ToArray(), J.Int(r, "mail_days"), J.Str(r, "entity_id"), J.Str(r, "to_name"), J.Long(r, "coin_added", 0L), J.Long(r, "coin", 0L));
	}
}

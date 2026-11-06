using System.Text.Json;

namespace DurangoLauncher;

public sealed record TradeOffer(string Id, string State, int Version, OfferSide From, OfferSide To, OfferBundle Give, OfferBundle Want, long Fee, double FeeRate, string? ReplyTo, string Message, long CreatedAt, long ExpiresAt, long ClosedAt, string CloseReason, long ChatLast, int Unread, long EditedAt = 0L)
{
	public bool Open => State == "pending";

	public bool Edited => EditedAt > 0;

	public bool IsIncoming(string me)
	{
		return To.EntityId == me;
	}

	public OfferSide Other(string me)
	{
		if (!(From.EntityId == me))
		{
			return From;
		}
		return To;
	}

	public (OfferBundle Theirs, OfferBundle Mine) Sides(string me)
	{
		if (!(From.EntityId == me))
		{
			return (Theirs: Give, Mine: Want);
		}
		return (Theirs: Want, Mine: Give);
	}

	public static TradeOffer Parse(JsonElement e)
	{
		string id = J.Str(e, "id", J.Str(e, "offer_id"));
		string state = J.Str(e, "state", "pending");
		int version = J.Int(e, "version");
		OfferSide offerSide = OfferSide.Parse(J.Obj(e, "from"));
		OfferSide to = OfferSide.Parse(J.Obj(e, "to"));
		OfferBundle give = OfferBundle.Parse(J.Obj(e, "give"));
		OfferBundle want = OfferBundle.Parse(J.Obj(e, "want"));
		long fee = J.Long(e, "fee", 0L);
		double feeRate = J.Dbl(e, "fee_rate");
		string text = J.Str(e, "reply_to");
		return new TradeOffer(id, state, version, offerSide, to, give, want, fee, feeRate, (text != null && text.Length > 0) ? text : null, J.Str(e, "message"), J.Long(e, "created_at", 0L), J.Long(e, "expires_at", 0L), J.Long(e, "closed_at", 0L), J.Str(e, "close_reason"), J.Long(e, "chat_last", 0L), J.Int(e, "unread"), J.Long(e, "edited_at", 0L));
	}
}

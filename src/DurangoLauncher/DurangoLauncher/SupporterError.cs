namespace DurangoLauncher;

public static class SupporterError
{
	public enum Kind
	{
		None,
		LockMe,
		Them,
		Lost
	}

	public static string ThemText => L.T("อ\u0e35กฝ\u0e48ายไม\u0e48ใช\u0e48 Supporter — เทรดผ\u0e48านร\u0e31นเช\u0e48อต\u0e49องเป\u0e47น Supporter ท\u0e31\u0e49งสองฝ\u0e48าย", "The other player is not a Supporter — launcher trades need Supporter on both sides");

	public static Kind Of(ApiError e)
	{
		string code = e.Code;
		if (!(code == "supporter_only"))
		{
			if (code == "supporter_lost")
			{
				return Kind.Lost;
			}
			return Kind.None;
		}
		return (!(e.Who == "them")) ? Kind.LockMe : Kind.Them;
	}

	public static string LostText(ApiError e)
	{
		if (e.Message.Length <= 0)
		{
			return LauncherTradeApi.DefaultMessage("supporter_lost", 409);
		}
		return e.Message;
	}

	public static string WhyShort(TradePlayer p)
	{
		if (p.Eligible)
		{
			return "";
		}
		return p.Why switch
		{
			"supporter" => L.T("ไม\u0e48ใช\u0e48 Supporter", "not a Supporter"), 
			"level" => L.T("เลเวลไม\u0e48ถ\u0e36ง", "level too low"), 
			"disabled" => L.T("ระบบเทรดป\u0e34ด", "trading is off"), 
			_ => L.T("เทรดไม\u0e48ได\u0e49", "cannot trade"), 
		};
	}

	public static string? WhyPick(TradePlayer p)
	{
		if (p.Eligible)
		{
			return null;
		}
		return p.Why switch
		{
			"supporter" => ThemText, 
			"level" => L.T("ผ\u0e39\u0e49เล\u0e48นน\u0e35\u0e49เลเวลไม\u0e48ถ\u0e36งท\u0e35\u0e48เทรดได\u0e49", "This player is below the trade level"), 
			"disabled" => L.T("ระบบเทรดป\u0e34ดอย\u0e39\u0e48", "Trading is turned off"), 
			_ => L.T("ผ\u0e39\u0e49เล\u0e48นน\u0e35\u0e49เทรดไม\u0e48ได\u0e49 (เลเวลไม\u0e48ถ\u0e36ง/ถ\u0e39กจำก\u0e31ด)", "This player cannot trade (level or restriction)"), 
		};
	}
}

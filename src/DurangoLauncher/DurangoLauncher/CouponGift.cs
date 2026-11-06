using System;
using System.Text.Json;

namespace DurangoLauncher;

public sealed record CouponGift(string Proto, string NameTh, string NameEn, string Icon, int Count)
{
	public string Name
	{
		get
		{
			if (!L.En)
			{
				if (NameTh.Length <= 0)
				{
					if (NameEn.Length <= 0)
					{
						return Proto;
					}
					return NameEn;
				}
				return NameTh;
			}
			if (NameEn.Length <= 0)
			{
				if (NameTh.Length <= 0)
				{
					return Proto;
				}
				return NameTh;
			}
			return NameEn;
		}
	}

	public static CouponGift Parse(JsonElement e)
	{
		var (nameTh, nameEn) = J.Name(e, "name");
		return new CouponGift(J.Str(e, "proto"), nameTh, nameEn, J.Str(e, "icon"), Math.Max(1, J.Int(e, "count", 1)));
	}
}

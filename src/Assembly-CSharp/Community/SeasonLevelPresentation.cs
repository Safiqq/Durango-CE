using System;
using Durango.Logic.Clusters;
using Community.Rift;
using UnityEngine;

namespace Community;

public static class SeasonLevelPresentation
{
	public static string T(string th, string en)
	{
		return RiftText.T(th, en);
	}

	public static bool IsLevelSeason(CommunitySeasonPass.Status s)
	{
		if (s != null && s.Active)
		{
			return s.ExpPerPoint > 0;
		}
		return false;
	}

	public static bool Fresh(CommunitySeasonPass.Status s)
	{
		if (s != null)
		{
			return Time.realtimeSinceStartup - s.ReceivedAt <= 90f;
		}
		return false;
	}

	public static bool Max(CommunitySeasonPass.Status s)
	{
		if (IsLevelSeason(s) && s.MaxTier > 0)
		{
			return s.SeasonLevel >= s.MaxTier;
		}
		return false;
	}

	public static string Level(CommunitySeasonPass.Status s)
	{
		if (s != null)
		{
			return "Season Lv. " + s.SeasonLevel + " / " + s.MaxTier;
		}
		return "Season Lv. --";
	}

	public static string Unit(CommunitySeasonPass.Status s)
	{
		if (s == null || s.ExpPerPoint != 1)
		{
			return T("แต\u0e49มจาก EXP", "EXP points");
		}
		return "EXP";
	}

	public static string Progress(CommunitySeasonPass.Status s)
	{
		if (s == null)
		{
			return T("กำล\u0e31งโหลดข\u0e49อม\u0e39ลซ\u0e35ซ\u0e31\u0e48น…", "Loading season…");
		}
		if (!s.Active)
		{
			return T("ย\u0e31งไม\u0e48ม\u0e35ซ\u0e35ซ\u0e31\u0e48นเป\u0e34ด", "No active season");
		}
		if (Max(s))
		{
			return T("เลเวลซ\u0e35ซ\u0e31\u0e48นส\u0e39งส\u0e38ด", "Maximum season level");
		}
		return Math.Max(0L, s.TierPoints).ToString("N0") + " / " + Math.Max(1, s.PointsPerTier).ToString("N0") + " " + Unit(s);
	}

	public static string Remaining(CommunitySeasonPass.Status s)
	{
		if (!IsLevelSeason(s) || Max(s))
		{
			return "";
		}
		return string.Format(T("อ\u0e35ก {0:N0} ถ\u0e36ง Lv.{1}", "{0:N0} to Lv.{1}"), Math.Max(0L, s.PointsPerTier - s.TierPoints), s.SeasonLevel + 1);
	}

	public static float QuotaRatio(CommunitySeasonPass.Status s)
	{
		if (s != null && s.ExpDailyPoints > 0)
		{
			return Mathf.Clamp01((float)s.ExpPointsToday / (float)s.ExpDailyPoints);
		}
		return 0f;
	}

	public static string Quota(CommunitySeasonPass.Status s)
	{
		if (!IsLevelSeason(s))
		{
			return "--";
		}
		if (s.ExpDailyPoints > 0)
		{
			return Math.Max(0, s.ExpPointsToday).ToString("N0") + " / " + s.ExpDailyPoints.ToString("N0");
		}
		return T("ไม\u0e48จำก\u0e31ดโควตารายว\u0e31น", "No daily limit");
	}

	public static string QuotaNote(CommunitySeasonPass.Status s)
	{
		if (!IsLevelSeason(s))
		{
			return "";
		}
		if (!Fresh(s))
		{
			return T("กำล\u0e31งอ\u0e31ปเดตข\u0e49อม\u0e39ล…", "Updating season data…");
		}
		if (s.ExpDailyPoints <= 0)
		{
			return "";
		}
		if (s.ExpPointsToday >= s.ExpDailyPoints)
		{
			return T("ครบโควตาว\u0e31นน\u0e35\u0e49 · สะสมต\u0e48อรอบว\u0e31นถ\u0e31ดไป", "Daily quota reached · continues next server day");
		}
		return string.Format(T("ว\u0e31นน\u0e35\u0e49สะสมได\u0e49อ\u0e35ก {0:N0} {1}", "{0:N0} {1} left today"), Math.Max(0, s.ExpDailyPoints - s.ExpPointsToday), Unit(s));
	}

	public static string End(CommunitySeasonPass.Status s)
	{
		if (s == null || !s.Active || s.Until <= 0.0)
		{
			return T("กำหนดส\u0e34\u0e49นส\u0e38ดซ\u0e35ซ\u0e31\u0e48น · รอประกาศ", "Season end · to be announced");
		}
		if (!Fresh(s))
		{
			return T("กำล\u0e31งอ\u0e31ปเดตเวลาซ\u0e35ซ\u0e31\u0e48น…", "Updating season time…");
		}
		double num = s.Now + (double)Math.Max(0f, Time.realtimeSinceStartup - s.ReceivedAt);
		double num2 = s.Until - num;
		if (num2 <= 0.0)
		{
			return T("รออ\u0e31ปเดตสถานะซ\u0e35ซ\u0e31\u0e48น", "Waiting for season update");
		}
		if (!(num2 >= 86400.0))
		{
			return string.Format(T("เหล\u0e37อ {0} ชม. {1} นาท\u0e35", "{0}h {1}m remaining"), (int)(num2 / 3600.0), (int)(num2 / 60.0) % 60);
		}
		return string.Format(T("เหล\u0e37อ {0} ว\u0e31น", "{0} days remaining"), Math.Ceiling(num2 / 86400.0));
	}
}

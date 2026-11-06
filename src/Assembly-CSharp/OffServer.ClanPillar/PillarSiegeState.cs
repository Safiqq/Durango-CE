using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace OffServer.ClanPillar;

public sealed class PillarSiegeState
{
	public string RegionId;

	public string SiteId;

	public string OwnerId;

	public string OwnerName;

	public string AttackerId;

	public string AttackerName;

	public string DefenderId;

	public string Stage;

	public double DeclaredAt;

	public double StartAt;

	public double EndAt;

	public double LockUntil;

	private static bool Number(JToken value, out double result)
	{
		result = 0.0;
		if (value == null || (value.Type != JTokenType.Integer && value.Type != JTokenType.Float))
		{
			return false;
		}
		result = Convert.ToDouble(((JValue)value).Value, CultureInfo.InvariantCulture);
		if (!double.IsNaN(result) && !double.IsInfinity(result) && result >= 0.0)
		{
			return result <= 253402300799.0;
		}
		return false;
	}

	public static PillarSiegeState Read(JObject row)
	{
		if (row != null && row["siege"] != null && Number(row["siegeLockUntil"], out var result))
		{
			JToken jToken = row["regionId"];
			if (jToken != null && jToken.Type == JTokenType.String)
			{
				JToken jToken2 = row["siteId"];
				if (jToken2 != null && jToken2.Type == JTokenType.String)
				{
					JToken jToken3 = row["clanId"];
					if (jToken3 == null || jToken3.Type != JTokenType.Null)
					{
						JToken jToken4 = row["clanId"];
						if (jToken4 == null || jToken4.Type != JTokenType.String)
						{
							goto IL_00ec;
						}
					}
					JToken jToken5 = row["clanName"];
					if (jToken5 == null || jToken5.Type != JTokenType.Null)
					{
						JToken jToken6 = row["clanName"];
						if (jToken6 == null || jToken6.Type != JTokenType.String)
						{
							goto IL_00ec;
						}
					}
					string text = (string)row["regionId"];
					string text2 = (string)row["siteId"];
					if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2))
					{
						return null;
					}
					PillarSiegeState pillarSiegeState = new PillarSiegeState
					{
						RegionId = text,
						SiteId = text2,
						OwnerId = (string)row["clanId"],
						OwnerName = (string)row["clanName"],
						LockUntil = result
					};
					if (row["siege"].Type == JTokenType.Null)
					{
						return pillarSiegeState;
					}
					if (row["siege"] is JObject jObject)
					{
						JToken jToken7 = jObject["stage"];
						if (jToken7 != null && jToken7.Type == JTokenType.String)
						{
							JToken jToken8 = jObject["attackerId"];
							if (jToken8 != null && jToken8.Type == JTokenType.String)
							{
								JToken jToken9 = jObject["defenderId"];
								if (jToken9 != null && jToken9.Type == JTokenType.String)
								{
									JToken jToken10 = jObject["attackerName"];
									if (jToken10 != null && jToken10.Type == JTokenType.String)
									{
										pillarSiegeState.Stage = (string)jObject["stage"];
										pillarSiegeState.AttackerId = (string)jObject["attackerId"];
										pillarSiegeState.AttackerName = (string)jObject["attackerName"];
										pillarSiegeState.DefenderId = (string)jObject["defenderId"];
										if ((pillarSiegeState.Stage != "prep" && pillarSiegeState.Stage != "war") || string.IsNullOrEmpty(pillarSiegeState.AttackerId) || string.IsNullOrEmpty(pillarSiegeState.DefenderId) || pillarSiegeState.DefenderId != pillarSiegeState.OwnerId || !Number(jObject["declaredAt"], out pillarSiegeState.DeclaredAt) || !Number(jObject["startAt"], out pillarSiegeState.StartAt) || !Number(jObject["endAt"], out pillarSiegeState.EndAt) || pillarSiegeState.DeclaredAt > pillarSiegeState.StartAt || pillarSiegeState.StartAt >= pillarSiegeState.EndAt)
										{
											return null;
										}
										return pillarSiegeState;
									}
								}
							}
						}
					}
					return null;
				}
			}
		}
		goto IL_00ec;
		IL_00ec:
		return null;
	}

	public bool CanDeclare(double now)
	{
		if (Stage == null && !string.IsNullOrEmpty(OwnerId))
		{
			return LockUntil <= now;
		}
		return false;
	}

	public double Remaining(double now)
	{
		return Math.Max(0.0, ((Stage == "prep") ? StartAt : ((Stage == "war") ? EndAt : LockUntil)) - now);
	}

	public bool HasCountdown(double now)
	{
		if (Stage == "prep" || Stage == "war")
		{
			return Remaining(now) > 0.0;
		}
		return false;
	}

	public static string Clock(double seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(Math.Max(0.0, Math.Ceiling(seconds)));
		return $"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
	}
}

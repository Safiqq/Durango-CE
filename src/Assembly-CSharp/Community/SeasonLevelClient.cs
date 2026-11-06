using Durango.Logic.Clusters;
using Messages;
using UnityEngine;

namespace Community;

public sealed class SeasonLevelClient : MonoBehaviour
{
	private static SeasonLevelClient instance;

	private static bool queued = true;

	private StatisticsSystem statistics;

	private string player;

	private float nextTick;

	private float nextPoll;

	private bool wasSeasonHud;

	public static void Attach()
	{
		if (!(instance != null))
		{
			GameObject obj = new GameObject("CommunitySeasonLevel");
			Object.DontDestroyOnLoad(obj);
			instance = obj.AddComponent<SeasonLevelClient>();
		}
	}

	public static void RequestRefresh()
	{
		Attach();
		queued = true;
	}

	private void OnExp(ExpGained msg)
	{
		if (msg.EntityId == GameManager.PlayerId)
		{
			queued = true;
		}
	}

	private void OnDestroy()
	{
		if (statistics != null)
		{
			statistics.ExpGained -= OnExp;
		}
		if (instance == this)
		{
			instance = null;
		}
	}

	private void Update()
	{
		if (Time.realtimeSinceStartup < nextTick)
		{
			return;
		}
		nextTick = Time.realtimeSinceStartup + 0.5f;
		if (GameManager.IsTitleScene || PlayerBehavior.LocalPlayer == null)
		{
			if (player != null)
			{
				CommunitySeasonPass.Reset();
			}
			player = null;
			queued = true;
			return;
		}
		if (player != GameManager.PlayerId)
		{
			CommunitySeasonPass.Reset();
			player = GameManager.PlayerId;
			queued = true;
			nextPoll = 0f;
		}
		StatisticsSystem statisticsSystem = GameSystem<StatisticsSystem>.Instance();
		if (statisticsSystem != statistics)
		{
			if (statistics != null)
			{
				statistics.ExpGained -= OnExp;
			}
			statistics = statisticsSystem;
			statistics.ExpGained += OnExp;
		}
		bool useSeasonHud = CommunitySeasonPass.UseSeasonHud;
		if (useSeasonHud != wasSeasonHud)
		{
			wasSeasonHud = useSeasonHud;
			CommunitySeasonPass.RefreshPresentation();
		}
		if (CommunitySeasonPass.Available && !CommunitySeasonPass.Busy)
		{
			if (Time.realtimeSinceStartup >= nextPoll)
			{
				queued = true;
			}
			if (queued && !(Time.realtimeSinceStartup - CommunitySeasonPass.LastFetchAt < 5f))
			{
				queued = false;
				nextPoll = Time.realtimeSinceStartup + (CommunitySeasonPass.UseSeasonHud ? 5f : 30f);
				CommunitySeasonPass.Fetch();
			}
		}
	}
}

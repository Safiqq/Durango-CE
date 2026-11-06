using System.Collections.Generic;
using Durango.Utils.Extensions;
using Newtonsoft.Json;
using UnityEngine;

namespace Durango.Logic.Clusters;

public class Account
{
	[JsonProperty(PropertyName = "players")]
	public List<PlayerInfo> Players;

	private int _maxPlayerSlotCount = 7;
	private int _playerSlotCount = 1;

	[JsonProperty(PropertyName = "max_player_slot_count")]
	public int MaxPlayerSlotCount
	{
		get
		{
			if (CommunityConfig.BypassCharacterSlotPurchase)
			{
				return global::System.Math.Max(_maxPlayerSlotCount, CommunityConfig.MaxCharacterSlots);
			}
			return _maxPlayerSlotCount;
		}
		set
		{
			_maxPlayerSlotCount = value;
		}
	}

	[JsonProperty(PropertyName = "player_slot_count")]
	public int PlayerSlotCount
	{
		get
		{
			if (CommunityConfig.BypassCharacterSlotPurchase)
			{
				return global::System.Math.Max(_playerSlotCount, MaxPlayerSlotCount);
			}
			return _playerSlotCount;
		}
		set
		{
			_playerSlotCount = value;
		}
	}

	private Pair<string, int> _recommendedPlayerIdCache;

	[JsonProperty(PropertyName = "account_id")]
	public string AccountId
	{
		get
		{
			return CommunityLink.ServerAccountId;
		}
		set
		{
			if (!string.IsNullOrEmpty(value))
			{
				UnityEngine.Debug.Log("[Community] /accounts → account_id=" + value);
				CommunityLink.ServerAccountId = value;
			}
		}
	}

	[JsonProperty(PropertyName = "discord_name")]
	public string DiscordName
	{
		get
		{
			return CommunityLink.DiscordName;
		}
		set
		{
			CommunityLink.DiscordName = value ?? string.Empty;
		}
	}

	public Pair<string, int> GetRecommendedPlayer()
	{
		if (KUtility.GetSize(Players) == 0)
		{
			return new Pair<string, int>(string.Empty, 0);
		}
		if (_recommendedPlayerIdCache.Item1 == string.Empty)
		{
			return _recommendedPlayerIdCache;
		}
		if (_recommendedPlayerIdCache.Item1 != null && Players.Exists((PlayerInfo elem) => elem.PlayerEntityId == _recommendedPlayerIdCache.Item1))
		{
			return _recommendedPlayerIdCache;
		}
		PlayerInfo lastConnected = Players.MaxBy((PlayerInfo elem) => elem.DisconnectedAt);
		int item = Players.FindIndex((PlayerInfo elem) => elem == lastConnected);
		return ApplyRecommendedPlayer(new Pair<string, int>(lastConnected.PlayerEntityId, item));
	}

	public Pair<string, int> ApplyRecommendedPlayer(Pair<string, int> idToSlot)
	{
		_recommendedPlayerIdCache = idToSlot;
		return idToSlot;
	}

	public string GetPlayerInfoText(string id)
	{
		int i = 0;
		for (int size = KUtility.GetSize(Players); i < size; i++)
		{
			PlayerInfo playerInfo = Players[i];
			if (playerInfo.PlayerEntityId == id)
			{
				return playerInfo.GetPlayerInfoText();
			}
		}
		return null;
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Durango.Logic.Clusters;
using Durango.Utils;
using Durango.Utils.Extensions;
using UnityEngine;
using PlayerInfo = Durango.Logic.Clusters.PlayerInfo;

namespace Durango.Offline;

public class Server
{
	private static Gateway _gateway;

	private static GameServer _gameServer;

	private static PlayerContext _localPlayer;

	private static WorldContext _localWorld;

	public static GameServer CurrentGameServer => _gameServer;

	public static PlayerContext CurrentPlayer => _localPlayer;

	public static WorldContext CurrentWorld => _localWorld;

	public string Key { get; private set; }

	public List<Context> Contexts { get; private set; }

	public Cluster Cluster { get; private set; }

	public Server(string key, Dictionary<string, string> names)
	{
		Server server = this;
		Key = key;
		Contexts = new List<Context>();
		Cluster = new Cluster();
		Cluster.Mode = ((!(key == "free")) ? Mode.Offline : Mode.Editable);
		Cluster.Names = names;
		Cluster.GatewayUrlRoot = "http://127.0.0.1:" + 8190;
		Cluster.OnRequestAccount = delegate(Action<Account> action)
		{
			Account account = new Account
			{
				Players = new List<PlayerInfo>()
			};
			foreach (Context context2 in server.Contexts)
			{
				PlayerContext player = context2.Player;
				PlayerInfo playerInfo = player.PlayerInfo;
				playerInfo.OfflineFunc = () => new Pair<PortraitBuilder.Argument, int>(player.AppearPlayer.Display.GetPortraitArgument(player.AppearPlayer.EntityId, player.AppearPlayer.IsMale()), player.AppearPlayer.Freq);
				account.Players.Add(playerInfo);
			}
			account.MaxPlayerSlotCount = Mathf.Max(2, (server.Cluster.Mode != Mode.Editable) ? account.Players.Count : 7);
			account.PlayerSlotCount = ((server.Cluster.Mode != Mode.Editable) ? account.Players.Count : 7);
			action?.Invoke(account);
		};
		Cluster.OnDeletePlayer = delegate(string entityId)
		{
			for (int i = 0; i < server.Contexts.Count; i++)
			{
				Context context = server.Contexts[i];
				if (context.EntityId == entityId)
				{
					try
					{
						File.Delete(context.Player.Path);
						File.Delete(context.World.Path);
					}
					catch (Exception)
					{
					}
					server.Contexts.RemoveAt(i);
					break;
				}
			}
		};
		Cluster.OnConfirm = delegate(string entityId)
		{
			Context context = server.Contexts.Find((Context x) => x.EntityId == entityId);
			if (context == null)
			{
				int num = 0;
				if (server.Contexts.Count > 0)
				{
					num = server.Contexts[server.Contexts.Count - 1].PlayerSlot + 1;
				}
				WorldContext worldContext = new WorldContext();
				worldContext.Initialize(WorldContext.MakePath(num, key));
				worldContext.PlayerSlot = num;
				PlayerContext playerContext2 = new PlayerContext();
				playerContext2.Initialize(PlayerContext.MakePath(num, key));
				playerContext2.PlayerSlot = num;
				context = new Context(worldContext, playerContext2);
				server.Contexts.Add(context);
			}
			BeginServer(context.World, context.Player);
		};
		string[] files = AppData.GetFiles(WorldContext.GetBasePath(Key), "*.world", SearchOption.TopDirectoryOnly);
		if (files == null)
		{
			return;
		}
		IEnumerable<WorldContext> enumerable = from x in files.Select(WorldContext.Load)
			where x != null
			select x;
		string[] files2 = AppData.GetFiles(WorldContext.GetBasePath(Key), "*.player", SearchOption.TopDirectoryOnly);
		Dictionary<int, PlayerContext> dictionary = new Dictionary<int, PlayerContext>();
		if (files2 != null)
		{
			foreach (PlayerContext item in from x in files2.Select(PlayerContext.Load)
				where x != null
				select x)
			{
				dictionary[item.PlayerSlot] = item;
			}
		}
		Contexts = new List<Context>();
		foreach (WorldContext item2 in enumerable)
		{
			PlayerContext playerContext = dictionary.Get(item2.PlayerSlot);
			if (playerContext == null)
			{
				playerContext = new PlayerContext();
				playerContext.Initialize(PlayerContext.MakePath(item2.PlayerSlot, Key));
			}
			Contexts.Add(new Context(item2, playerContext));
		}
		Contexts = Contexts.OrderBy((Context x) => x.PlayerSlot).ToList();
	}

	private static void BeginServer(WorldContext worldCtx, PlayerContext playerCtx)
	{
		EndServer();
		_gameServer = new GameServer(worldCtx, playerCtx);
		_gateway = new Gateway(_gameServer, worldCtx, playerCtx);
		_localWorld = worldCtx;
		_localPlayer = playerCtx;
	}

	public static void EnsureLocalServer()
	{
		if (_gameServer != null)
		{
			return;
		}

		try
		{
			string key = "offline";
			string worldPath = WorldContext.MakePath(0, key);
			string playerPath = PlayerContext.MakePath(0, key);

			WorldContext worldContext = null;
			if (File.Exists(worldPath))
			{
				worldContext = WorldContext.Load(worldPath);
			}
			if (worldContext == null)
			{
				worldContext = new WorldContext();
				worldContext.Initialize(worldPath);
				worldContext.PlayerSlot = 0;
			}

			PlayerContext playerContext = null;
			if (File.Exists(playerPath))
			{
				playerContext = PlayerContext.Load(playerPath);
			}
			if (playerContext == null)
			{
				playerContext = new PlayerContext();
				playerContext.Initialize(playerPath);
				playerContext.PlayerSlot = 0;
			}

			_gameServer = new GameServer(worldContext, playerContext);
			_localWorld = worldContext;
			_localPlayer = playerContext;
			Debug.Log("[EmbeddedGameServer] Local GameServer listening on port 8191");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[EmbeddedGameServer] Failed to start local GameServer: {ex.Message}");
		}
	}

	public static void UpdatePlayerFromPost(Dictionary<string, string> postData)
	{
		if (_localPlayer == null || _localWorld == null)
		{
			EnsureLocalServer();
		}
		if (_localPlayer == null || _localWorld == null)
		{
			return;
		}

		try
		{
			string name = postData.Get("name");
			if (!string.IsNullOrEmpty(name))
			{
				_localPlayer.PlayerInfo.PlayerName = name;
			}
			List<string> regionTemplateIds = Yaml.Util.Singleton<Yaml.Constants>.Instance.PersonalRegion.RegionTemplateIds;
			if (regionTemplateIds != null && regionTemplateIds.Count > 0)
			{
				int index = UnityEngine.Random.Range(0, regionTemplateIds.Count);
				string text = postData.Get("region");
				_localWorld.TerrainId = ((string.IsNullOrEmpty(text) || !regionTemplateIds.Contains(text)) ? regionTemplateIds[index] : text);
			}
			Gateway.UpdateAppearPlayer(_localPlayer, postData);
			string[] bodyColor = _localPlayer.AppearPlayer.Display.BodyColor;
			string[] array = new string[8] { "clothes_engineer", "clothes_officeworker", "clothes_student", "clothes_farmer", "clothes_waiter", "clothes_soldier", "clothes_homeworker", "clothes_jobless" };
			int value = postData.Get("job").ToInt();
			value = Mathf.Clamp(value, 0, array.Length - 1);
			Messages.Item? item = Cheats.MakeItem(array[value], 1);
			if (item.HasValue)
			{
				Messages.Item value2 = item.Value;
				if (bodyColor != null && bodyColor.Length >= 3)
				{
					value2.ColorR = bodyColor[0];
					value2.ColorG = bodyColor[1];
					value2.ColorB = bodyColor[2];
				}
				_localPlayer.InventoryItems.Add(value2);
				_localPlayer.EquippedItems["body"] = value2.Id;
			}
			_localWorld.Save();
			_localPlayer.Save();
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[Server] UpdatePlayerFromPost error: {ex.Message}");
		}
	}

	public static void EndServer()
	{
		if (_gateway != null)
		{
			_gateway.Close();
			_gateway = null;
		}
		if (_gameServer != null)
		{
			_gameServer.Close();
			_gameServer = null;
		}
	}

	public static void Process()
	{
		if (_gateway != null)
		{
			_gateway.Process();
		}
		if (_gameServer != null)
		{
			_gameServer.Process();
		}
	}

	public static void ConnectTo(string ip)
	{
		Cluster cluster = new Cluster();
		if (ip.StartsWith("http://"))
		{
			ip = ip.Substring(7);
		}
		string gatewayUrlRoot = "http://" + ip + ":" + 8190;
		cluster.OnRequestAccount = delegate(Action<Account> action)
		{
			Account account = new Account();
			account.MaxPlayerSlotCount = 7;
			account.PlayerSlotCount = 1;
			account.Players = new List<PlayerInfo>();
			account.Players.Add(_localPlayer.PlayerInfo);
			action?.Invoke(account);
		};
		cluster.LocalPlayer = Json.Write(_localPlayer);
		cluster.GatewayUrlRoot = gatewayUrlRoot;
		cluster.Mode = Mode.Offline;
		GameManager.ConnectCluster = cluster;
		GameManager.Emigrated = GameManager.EmigratedType.Explore;
		Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
	}
}

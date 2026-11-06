using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Centralized Configuration System for Durango: Community Edition.
/// Loads configuration from config.json (and falls back / overlays server.txt).
/// Enables bypassing character slot purchase restrictions and provides an extensible
/// key-value and section-based interface for future user configurations.
/// </summary>
public static class CommunityConfig
{
	private static bool _loaded;
	private static JObject _configRoot = new JObject();

	// Character slot options
	public static bool BypassCharacterSlotPurchase { get; set; } = true;
	public static int MaxCharacterSlots { get; set; } = 7;
	public static int UnlockedCharacterSlots { get; set; } = 7;

	// Network options
	public static string GatewayUrl { get; set; } = "http://127.0.0.1:8190";
	public static string ServerName { get; set; } = "Local Server";

	public static string ConfigJsonPath
	{
		get
		{
			string dir = (Application.platform == RuntimePlatform.Android)
				? Application.persistentDataPath
				: (Path.GetDirectoryName(Application.dataPath) ?? ".");
			return Path.Combine(dir, "config.json");
		}
	}

	public static string ServerTxtPath
	{
		get
		{
			string dir = (Application.platform == RuntimePlatform.Android)
				? Application.persistentDataPath
				: (Path.GetDirectoryName(Application.dataPath) ?? ".");
			return Path.Combine(dir, "server.txt");
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	public static void AutoLoad()
	{
		EnsureLoaded();
	}

	public static void EnsureLoaded()
	{
		if (_loaded) return;
		_loaded = true;
		Load();
	}

	public static void Load()
	{
		try
		{
			string jsonPath = ConfigJsonPath;
			if (File.Exists(jsonPath))
			{
				string text = File.ReadAllText(jsonPath, Encoding.UTF8);
				ParseJson(text);
				Debug.Log("[CommunityConfig] Loaded configuration from " + jsonPath);
			}
			else
			{
				SaveDefault(jsonPath);
				Debug.Log("[CommunityConfig] Created default configuration at " + jsonPath);
			}

			// Also overlay values from server.txt for backwards compatibility
			string txtPath = ServerTxtPath;
			if (File.Exists(txtPath))
			{
				ParseTxt(File.ReadAllLines(txtPath, Encoding.UTF8));
			}

			Debug.Log($"[CommunityConfig] BypassCharacterSlotPurchase={BypassCharacterSlotPurchase}, MaxSlots={MaxCharacterSlots}, UnlockedSlots={UnlockedCharacterSlots}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[CommunityConfig] Error loading configuration: " + ex.Message);
		}
	}

	private static void ParseJson(string json)
	{
		if (string.IsNullOrEmpty(json)) return;
		try
		{
			_configRoot = JObject.Parse(json);
			if (_configRoot == null) return;

			// 1. Character slots (nested)
			JToken csToken = _configRoot["character_slots"];
			if (csToken != null)
			{
				if (csToken["bypass_purchase"] != null)
					BypassCharacterSlotPurchase = csToken["bypass_purchase"].ToObject<bool>();
				if (csToken["max_slots"] != null)
					MaxCharacterSlots = csToken["max_slots"].ToObject<int>();
				if (csToken["unlocked_slots"] != null)
					UnlockedCharacterSlots = csToken["unlocked_slots"].ToObject<int>();
			}

			// Flat keys fallback
			if (_configRoot["bypass_character_slot_purchase"] != null)
				BypassCharacterSlotPurchase = _configRoot["bypass_character_slot_purchase"].ToObject<bool>();
			if (_configRoot["unlock_all_character_slots"] != null)
				BypassCharacterSlotPurchase = _configRoot["unlock_all_character_slots"].ToObject<bool>();
			if (_configRoot["max_character_slots"] != null)
				MaxCharacterSlots = _configRoot["max_character_slots"].ToObject<int>();
			if (_configRoot["unlocked_character_slots"] != null)
				UnlockedCharacterSlots = _configRoot["unlocked_character_slots"].ToObject<int>();

			// Network
			JToken netToken = _configRoot["network"];
			if (netToken != null)
			{
				if (netToken["gateway"] != null)
					GatewayUrl = netToken["gateway"].ToString();
				if (netToken["server_name"] != null)
					ServerName = netToken["server_name"].ToString();
			}
			if (_configRoot["gateway"] != null)
				GatewayUrl = _configRoot["gateway"].ToString();
			if (_configRoot["server_name"] != null)
				ServerName = _configRoot["server_name"].ToString();
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[CommunityConfig] ParseJson error: " + ex.Message);
		}
	}

	private static void ParseTxt(string[] lines)
	{
		if (lines == null) return;
		foreach (string line in lines)
		{
			string trimmed = line.Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
			int eq = trimmed.IndexOf('=');
			if (eq <= 0) continue;
			string key = trimmed.Substring(0, eq).Trim().ToLowerInvariant();
			string val = trimmed.Substring(eq + 1).Trim();

			switch (key)
			{
				case "bypass_character_slot_purchase":
				case "unlock_all_character_slots":
				case "bypass_character_slots":
					BypassCharacterSlotPurchase = (val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase));
					break;
				case "max_character_slots":
				case "max_player_slots":
				case "character_slots":
					if (int.TryParse(val, out int maxSlots))
					{
						MaxCharacterSlots = maxSlots;
						UnlockedCharacterSlots = maxSlots;
					}
					break;
				case "gateway":
				case "url":
					GatewayUrl = val;
					break;
				case "name":
					ServerName = val;
					break;
			}
		}
	}

	public static void SaveDefault(string path)
	{
		try
		{
			_configRoot = new JObject
			{
				["$schema_description"] = "Durango: Community Edition - Client Configuration",
				["character_slots"] = new JObject
				{
					["bypass_purchase"] = true,
					["max_slots"] = 7,
					["unlocked_slots"] = 7,
					["description"] = "Bypasses the 'Must Purchase Character Slot' restriction, unlocking all slots for free."
				},
				["network"] = new JObject
				{
					["gateway"] = "http://127.0.0.1:8190",
					["server_name"] = "Local Server"
				},
				["features"] = new JObject
				{
					["combat_loadout"] = false,
					["mh_hit_stop"] = false,
					["mh_animal_emotes"] = false,
					["mh_part_labels"] = false
				},
				["custom"] = new JObject()
			};
			string json = _configRoot.ToString(Formatting.Indented);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path, json, Encoding.UTF8);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[CommunityConfig] SaveDefault error: " + ex.Message);
		}
	}

	public static string GetString(string section, string key, string defaultValue = "")
	{
		EnsureLoaded();
		if (_configRoot == null) return defaultValue;
		try
		{
			JToken token = _configRoot[section]?[key] ?? _configRoot[key];
			return (token != null) ? token.ToString() : defaultValue;
		}
		catch
		{
			return defaultValue;
		}
	}

	public static bool GetBool(string section, string key, bool defaultValue = false)
	{
		EnsureLoaded();
		if (_configRoot == null) return defaultValue;
		try
		{
			JToken token = _configRoot[section]?[key] ?? _configRoot[key];
			if (token == null) return defaultValue;
			return token.ToObject<bool>();
		}
		catch
		{
			return defaultValue;
		}
	}

	public static int GetInt(string section, string key, int defaultValue = 0)
	{
		EnsureLoaded();
		if (_configRoot == null) return defaultValue;
		try
		{
			JToken token = _configRoot[section]?[key] ?? _configRoot[key];
			if (token == null) return defaultValue;
			return token.ToObject<int>();
		}
		catch
		{
			return defaultValue;
		}
	}
}

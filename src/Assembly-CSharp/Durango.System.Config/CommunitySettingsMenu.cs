using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.UI;

namespace Durango.System.Config;

public static class CommunitySettingsMenu
{
	public const string Audio = "community_audio";

	public const string Combat = "community_combat";

	public const string Chat = "community_chat";

	public const string Coupons = "community_coupon";

	public const string RangeDetails = "settings_more_range";

	public const string BloodDetails = "settings_more_blood";

	public const string HotkeyDetails = "settings_more_hotkeys";

	public const string OtherDetails = "settings_more_effects";

	private static readonly HashSet<string> _expanded = new HashSet<string>();

	private static readonly HashSet<string> RangeAdvanced = new HashSet<string> { "attack_range_width", "attack_range_reduce_motion", "attack_range_color_assist", "attack_range_quality" };

	private static readonly HashSet<string> BloodAdvanced = new HashSet<string> { "blood_amount", "blood_stain_time", "blood_color" };

	private static readonly HashSet<string> GraphicsKeys = new HashSet<string>
	{
		"resolution", "resolution_pc", "screen_mode", "fps", "max_frame_rate", "v_sync", "anti_aliasing", "ui_size", "shadow", "capture_filter",
		"visual_effect", "vignette", "chromatic_aberration", "max_visible_players"
	};

	public static string Resolve(string category)
	{
		if (category == "default")
		{
			return "screen";
		}
		if (category == "community_voice")
		{
			return "community_audio";
		}
		return category;
	}

	public static string Label(string category)
	{
		bool flag = CommunityDiscordLink.Lang == "th";
		switch (category)
		{
		case "screen":
			if (!flag)
			{
				return "Graphics";
			}
			return "ภาพและประส\u0e34ทธ\u0e34ภาพ";
		case "community_audio":
			if (!flag)
			{
				return "Audio and voice";
			}
			return "เส\u0e35ยงและไมค\u0e4c";
		case "hotkey":
			if (!flag)
			{
				return "Controls";
			}
			return "การควบค\u0e38ม";
		case "community_combat":
			if (!flag)
			{
				return "Combat and effects";
			}
			return "ต\u0e48อส\u0e39\u0e49และเอฟเฟกต\u0e4c";
		case "community_chat":
			if (!flag)
			{
				return "Chat and display";
			}
			return "แชทและการแสดงผล";
		case "account":
			if (!flag)
			{
				return "Account and language";
			}
			return "บ\u0e31ญช\u0e35และภาษา";
		case "community_coupon":
			if (!flag)
			{
				return "Coupons";
			}
			return "ค\u0e39ปอง";
		case "customer_service":
			if (!flag)
			{
				return "Help";
			}
			return "ช\u0e48วยเหล\u0e37อ";
		default:
			return LocalizeSystem.Get("#config_" + category);
		}
	}

	public static bool Omitted(Setting s)
	{
		if (s.DebugBuild || s.Key == "hide_debug_ui" || s.Key == "balance_info" || s.Key == "show_baseplate" || s.Key == "dump_personal_island")
		{
			return true;
		}
		string text = (s as ValueSetting)?.Value?.ToString() ?? "";
		if (s.Key == "major_announcement" || s.Key == "announcement" || s.Key == "update_state" || s.Key == "official_archive" || s.Key == "official_community" || s.Key == "user_forum")
		{
			if (text.IndexOf("nexon.com", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("PlayDurango", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("durango.wiki", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return text.IndexOf("trello.com/b/aIsqh0pl", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return true;
		}
		return false;
	}

	private static string Destination(string original, string key)
	{
		if (key == "coupon")
		{
			return "community_coupon";
		}
		if (original == "hotkey")
		{
			return original;
		}
		if (original == "community_voice" || key.EndsWith("_volume", StringComparison.Ordinal))
		{
			return "community_audio";
		}
		if (key == "terms" || key == "credit" || original == "customer_service")
		{
			return "customer_service";
		}
		switch (key)
		{
		default:
			if (!(original == "account"))
			{
				if (key == "target_lock_mode" || key.StartsWith("attack_range_", StringComparison.Ordinal) || key.StartsWith("blood_", StringComparison.Ordinal) || key == "lava_shield_fx")
				{
					return "community_combat";
				}
				if (GraphicsKeys.Contains(key) || original == "screen")
				{
					return "screen";
				}
				if (key.IndexOf("chat", StringComparison.Ordinal) < 0)
				{
					switch (key)
					{
					case "auto_translation":
					case "permit_conversation":
					case "show_draw_line":
					case "show_fps":
					case "show_name_flags":
						break;
					default:
						return "hotkey";
					}
				}
				return "community_chat";
			}
			goto case "locale";
		case "locale":
		case "voice_locale":
		case "net_transport":
		case "lang_packs":
			return "account";
		}
	}

	public static Dictionary<string, List<Setting>> AllRows()
	{
		Dictionary<string, List<Setting>> dictionary = new Dictionary<string, List<Setting>>();
		string[] array = new string[8] { "screen", "community_audio", "hotkey", "community_combat", "community_chat", "account", "community_coupon", "customer_service" };
		foreach (string key in array)
		{
			dictionary[key] = new List<Setting>();
		}
		if (ConfigInstance.Settings == null)
		{
			return dictionary;
		}
		HashSet<string> hashSet = new HashSet<string>();
		HashSet<string> hashSet2 = new HashSet<string>();
		foreach (KeyValuePair<string, List<Setting>> setting in ConfigInstance.Settings)
		{
			if (!(setting.Key != "community_voice") && !CommunityVoiceSettings.Visible)
			{
				continue;
			}
			foreach (Setting item in setting.Value)
			{
				if (!Omitted(item) && !Setting.IsHidden(item))
				{
					hashSet2.Add(item.Key);
				}
			}
		}
		foreach (KeyValuePair<string, List<Setting>> setting2 in ConfigInstance.Settings)
		{
			if (setting2.Key == "community_voice" && !CommunityVoiceSettings.Visible)
			{
				continue;
			}
			foreach (Setting item2 in setting2.Value)
			{
				if (item2.Type != SettingType.Grid && item2.Type != SettingType.Category && !Omitted(item2) && (!Setting.IsHidden(item2) || !hashSet2.Contains(item2.Key)) && hashSet.Add(item2.Key))
				{
					if (item2 is ValueSetting valueSetting)
					{
						valueSetting.GridNumber = 0;
					}
					dictionary[Destination(setting2.Key, item2.Key)].Add(item2);
				}
			}
		}
		return dictionary;
	}

	public static List<Setting> Rows(string category, bool includeCollapsed = false)
	{
		category = Resolve(category);
		if (!AllRows().TryGetValue(category, out var value))
		{
			return new List<Setting>();
		}
		if (includeCollapsed)
		{
			return value;
		}
		if (category == "hotkey")
		{
			List<Setting> list = new List<Setting>();
			HashSet<string> hashSet = new HashSet<string>();
			foreach (Setting item in value)
			{
				if (item.Key.StartsWith("hotkey_", StringComparison.Ordinal))
				{
					hashSet.Add(item.Key);
				}
				else
				{
					list.Add(item);
				}
			}
			AddDetails(list, value, "settings_more_hotkeys", hashSet, "ป\u0e38\u0e48มล\u0e31ด", "Keyboard shortcuts");
			return list;
		}
		if (category != "community_combat")
		{
			return value;
		}
		List<Setting> list2 = new List<Setting>();
		foreach (Setting item2 in value)
		{
			if (!RangeAdvanced.Contains(item2.Key) && !BloodAdvanced.Contains(item2.Key) && !(item2.Key == "lava_shield_fx"))
			{
				list2.Add(item2);
				if (item2.Key == "attack_range_intensity")
				{
					AddDetails(list2, value, "settings_more_range", RangeAdvanced, "รายละเอ\u0e35ยดวงระยะโจมต\u0e35", "Range details");
				}
				if (item2.Key == "blood_fx")
				{
					AddDetails(list2, value, "settings_more_blood", BloodAdvanced, "รายละเอ\u0e35ยดเอฟเฟกต\u0e4cเล\u0e37อด", "Blood details");
				}
			}
		}
		AddDetails(list2, value, "settings_more_effects", new HashSet<string> { "lava_shield_fx" }, "เอฟเฟกต\u0e4cอ\u0e37\u0e48น", "Other effects");
		return list2;
	}

	private static void AddDetails(List<Setting> visible, List<Setting> source, string key, HashSet<string> keys, string thai, string english)
	{
		if (!source.Exists((Setting s) => keys.Contains(s.Key) && !Setting.IsHidden(s)))
		{
			return;
		}
		bool flag = _expanded.Contains(key);
		visible.Add(new ValueSetting
		{
			Key = key,
			Type = SettingType.ButtonBox,
			GridNumber = 0,
			PrepareLabelText = ((CommunityDiscordLink.Lang == "th") ? thai : english) + (flag ? "  −" : "  +")
		});
		if (!flag)
		{
			return;
		}
		foreach (Setting item in source)
		{
			if (keys.Contains(item.Key))
			{
				visible.Add(item);
			}
		}
	}

	public static bool HandleAction(string key)
	{
		if (key != "settings_more_range" && key != "settings_more_blood" && key != "settings_more_hotkeys" && key != "settings_more_effects")
		{
			return false;
		}
		if (!_expanded.Remove(key))
		{
			_expanded.Add(key);
		}
		ConfigMainWidget.RefreshCommunityMenu((key == "settings_more_hotkeys") ? "hotkey" : "community_combat");
		return true;
	}

	public static IEnumerable<string> Categories()
	{
		foreach (KeyValuePair<string, List<Setting>> item in AllRows())
		{
			if (GameManager.ClusterMode != Mode.Online && (item.Key == "account" || item.Key == "community_coupon" || item.Key == "customer_service"))
			{
				continue;
			}
			foreach (Setting item2 in item.Value)
			{
				if (!Setting.IsHidden(item2))
				{
					yield return item.Key;
					break;
				}
			}
		}
	}
}

using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.UI.InGame;
using UnityEngine;

namespace Durango.System.Config;

public static class CommunityGameplayOptions
{
	public const string TargetLockKey = "target_lock_mode";

	public const string Nearest = "nearest";

	public const string LowestHp = "lowest_hp";

	public const string Manual = "manual";

	public const string PlayerAttackRangeKey = "attack_range_player";

	public const string MonsterAttackRangeKey = "attack_range_monster";

	public const string MaxVisiblePlayersKey = "max_visible_players";

	public const string VSyncKey = "v_sync";

	public const string ShowNameFlagsKey = "show_name_flags";

	public const string LavaShieldKey = "lava_shield_fx";

	public const string BloodFxKey = "blood_fx";

	public const string BloodAmountKey = "blood_amount";

	public const string BloodStainKey = "blood_stain_time";

	public const string BloodColorKey = "blood_color";

	public const string RunHoldKey = "run_hold";

	public static readonly string[] BloodColorOptions = new string[5] { "red", "green", "blue", "purple", "black" };

	public static readonly string[] BloodAmountOptions = new string[4] { "low", "normal", "high", "max" };

	public static readonly string[] BloodStainOptions = new string[4] { "30", "60", "180", "300" };

	public const string Unlimited = "all";

	public static readonly string[] MaxVisiblePlayersOptions = new string[7] { "0", "5", "10", "20", "30", "50", "all" };

	public static string TargetLockMode { get; internal set; } = "nearest";

	public static List<Setting> BuildSettings()
	{
		List<Setting> list = new List<Setting>();
		list.Add(new ValueSetting
		{
			Key = "hud_layout_edit",
			Type = SettingType.ButtonBox,
			HideOnPrologue = true,
			PrepareLabelText = "จ\u0e31ดตำแหน\u0e48งป\u0e38\u0e48ม / HUD layout"
		});
		if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
		{
			ValueSetting valueSetting = MakeSwitch("v_sync");
			valueSetting.HideOnPrologue = false;
			list.Add(valueSetting);
		}
		if (CommunityMobileMove.ShowSettingRow)
		{
			ToggleSetting toggleSetting = new ToggleSetting();
			toggleSetting.Key = "mobile_move_mode";
			toggleSetting.Type = SettingType.Toggle;
			toggleSetting.Options = CommunityMobileMoveRules.Options;
			toggleSetting.Default = "both";
			toggleSetting.HideOnPrologue = true;
			toggleSetting.PrepareLabelText = Label("mobile_move_mode");
			list.Add(toggleSetting);
		}
		list.Add(MakeToggle("target_lock_mode", new string[3] { "nearest", "lowest_hp", "manual" }, "nearest"));
		list.Add(MakeSwitch("attack_range_player", defaultValue: true));
		list.Add(MakeSwitch("attack_range_monster", defaultValue: true));
		list.Add(MakeToggle("attack_range_intensity", AttackRangeStyle.Levels, "normal"));
		list.Add(MakeToggle("attack_range_width", AttackRangeStyle.Widths, "normal"));
		list.Add(MakeSwitch("attack_range_reduce_motion"));
		list.Add(MakeSwitch("attack_range_color_assist"));
		list.Add(MakeToggle("attack_range_quality", AttackRangeStyle.Levels, "normal"));
		ToggleSetting toggleSetting2 = new ToggleSetting();
		toggleSetting2.Key = "max_visible_players";
		toggleSetting2.Type = SettingType.Toggle;
		toggleSetting2.Options = MaxVisiblePlayersOptions;
		toggleSetting2.Default = "all";
		toggleSetting2.HideOnPrologue = true;
		toggleSetting2.PrepareLabelText = Label("max_visible_players");
		list.Add(toggleSetting2);
		if (CommunityEmoji.NameTagToggleSupported)
		{
			list.Add(MakeSwitch("show_name_flags", defaultValue: true));
		}
		list.Add(MakeSwitch("lava_shield_fx"));
		list.Add(MakeSwitch("run_hold"));
		list.Add(MakeToggle("net_transport", CommunityUdp.Options, "auto"));
		list.Add(MakeSwitch("blood_fx", defaultValue: true));
		list.Add(MakeToggle("blood_amount", BloodAmountOptions, "normal"));
		list.Add(MakeToggle("blood_stain_time", BloodStainOptions, "180"));
		list.Add(MakeToggle("blood_color", BloodColorOptions, "red"));
		return list;
	}

	private static ToggleSetting MakeToggle(string key, string[] options, string defaultValue)
	{
		return new ToggleSetting
		{
			Key = key,
			Type = SettingType.Toggle,
			Options = options,
			Default = defaultValue,
			HideOnPrologue = true,
			PrepareLabelText = Label(key)
		};
	}

	private static ValueSetting MakeSwitch(string key, bool defaultValue = false)
	{
		return new ValueSetting
		{
			Key = key,
			Type = SettingType.Switch,
			Default = (defaultValue ? "true" : "false"),
			Value = defaultValue,
			HideOnPrologue = true,
			PrepareLabelText = Label(key)
		};
	}

	public static int ParseMaxVisiblePlayers(string value)
	{
		if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var result) && result >= 0)
		{
			return result;
		}
		return -1;
	}

	public static string Label(string key)
	{
		string lang = CommunityDiscordLink.Lang;
		switch (key)
		{
		case "attack_range_intensity":
			if (!(lang == "th"))
			{
				return "Attack range opacity";
			}
			return "ความเข\u0e49มของวงระยะโจมต\u0e35";
		case "attack_range_width":
			if (!(lang == "th"))
			{
				return "Attack range border width";
			}
			return "ความหนาขอบวงระยะโจมต\u0e35";
		case "attack_range_reduce_motion":
			if (!(lang == "th"))
			{
				return "Reduce attack range motion";
			}
			return "ลดแสงเคล\u0e37\u0e48อนไหวในวงระยะโจมต\u0e35";
		case "attack_range_color_assist":
			if (!(lang == "th"))
			{
				return "Range colour assistance (colour and dashes)";
			}
			return "ช\u0e48วยแยกส\u0e35วง (ส\u0e35และเส\u0e49นประ)";
		case "attack_range_quality":
			if (!(lang == "th"))
			{
				return "Attack range effect quality";
			}
			return "ค\u0e38ณภาพเอฟเฟกต\u0e4cวงระยะโจมต\u0e35";
		case "v_sync":
			return lang switch
			{
				"th" => "V-Sync (ลดภาพฉ\u0e35ก · เฟรมเรทตามจอ)",
				"id" => "V-Sync (kurangi robekan gambar)",
				"ko" => "수직 동기화 (V-Sync)",
				_ => "V-Sync (no tearing · frame rate follows the display)",
			};
		case "target_lock_mode":
			if (!(lang == "th"))
			{
				return "Target lock";
			}
			return "ล\u0e47อกเป\u0e49าหมาย";
		case "attack_range_player":
			return lang switch
			{
				"th" => "แสดงระยะโจมต\u0e35ของต\u0e31วเอง",
				"id" => "Tampilkan jangkauan serangan sendiri",
				"ko" => "내 공격 범위 표시",
				_ => "Show my attack range",
			};
		case "attack_range_monster":
			return lang switch
			{
				"th" => "แสดงระยะโจมต\u0e35ของมอนสเตอร\u0e4c",
				"id" => "Tampilkan jangkauan serangan monster",
				"ko" => "몬스터 공격 범위 표시",
				_ => "Show monster attack range",
			};
		case "max_visible_players":
			return lang switch
			{
				"th" => "จำนวนผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48นท\u0e35\u0e48แสดง",
				"id" => "Jumlah pemain lain yang ditampilkan",
				"ko" => "표시할 다른 플레이어 수",
				_ => "Other players shown",
			};
		case "show_name_flags":
			return lang switch
			{
				"th" => "แสดงธง/ไอคอนหน\u0e49าช\u0e37\u0e48อ",
				"id" => "Tampilkan bendera/ikon di depan nama",
				"ko" => "이름 앞 국기/아이콘 표시",
				_ => "Show flags/icons before names",
			};
		case "lava_shield_fx":
			return lang switch
			{
				"th" => "โล\u0e48ลาวาตอนวาร\u0e4cป (คนรอบข\u0e49างเห\u0e47นด\u0e49วย)",
				"id" => "Perisai lava saat warp (terlihat pemain lain)",
				"ko" => "워프 시 용암 보호막 (주변에도 표시)",
				_ => "Lava shield on warp (others see it too)",
			};
		case "run_hold":
			return lang switch
			{
				"th" => "กดค\u0e49างเพ\u0e37\u0e48อว\u0e34\u0e48ง (ป\u0e34ด = กดสล\u0e31บ)",
				"id" => "Tahan untuk berlari (mati = ketuk untuk ganti)",
				"ko" => "누르고 있는 동안 달리기 (끔 = 눌러서 전환)",
				_ => "Hold to run (off = tap to toggle)",
			};
		case "blood_fx":
			return lang switch
			{
				"th" => "เล\u0e37อดสาด + คราบเล\u0e37อดบนพ\u0e37\u0e49น",
				"id" => "Cipratan darah + noda darah di tanah",
				"ko" => "피 튀김 + 바닥 핏자국",
				_ => "Blood spray + blood stains on the ground",
			};
		case "blood_amount":
			return lang switch
			{
				"th" => "ปร\u0e34มาณเล\u0e37อดสาด",
				"id" => "Jumlah cipratan darah",
				"ko" => "피 튀김 양",
				_ => "Blood amount",
			};
		case "blood_color":
			return lang switch
			{
				"th" => "ส\u0e35เล\u0e37อด",
				"id" => "Warna darah",
				"ko" => "피 색상",
				_ => "Blood colour",
			};
		case "blood_stain_time":
			return lang switch
			{
				"th" => "เวลาคราบเล\u0e37อดบนพ\u0e37\u0e49น",
				"id" => "Lama noda darah di tanah",
				"ko" => "바닥 핏자국 유지 시간",
				_ => "Blood stains stay for",
			};
		case "net_transport":
			return CommunityUdp.Label();
		case "combat_move_cancel":
			return CommunityCombatMoveCancel.Label();
		case "mobile_move_mode":
			return CommunityMobileMove.Label();
		default:
			return null;
		}
	}

	public static string BloodOptionLabel(string key, string value)
	{
		string lang = CommunityDiscordLink.Lang;
		switch (key)
		{
		case "attack_range_intensity":
		case "attack_range_quality":
			if (!(value == "low"))
			{
				if (!(value == "high"))
				{
					if (!(lang == "th"))
					{
						return "Normal";
					}
					return "ปกต\u0e34";
				}
				if (!(lang == "th"))
				{
					return "High";
				}
				return "มาก";
			}
			if (!(lang == "th"))
			{
				return "Low";
			}
			return "น\u0e49อย";
		case "attack_range_width":
			if (!(value == "thin"))
			{
				if (!(value == "thick"))
				{
					if (!(lang == "th"))
					{
						return "Normal";
					}
					return "ปกต\u0e34";
				}
				if (!(lang == "th"))
				{
					return "Thick";
				}
				return "หนา";
			}
			if (!(lang == "th"))
			{
				return "Thin";
			}
			return "บาง";
		case "blood_amount":
			return value switch
			{
				"low" => lang switch
				{
					"ko" => "적게",
					"id" => "Sedikit",
					"th" => "น\u0e49อย",
					_ => "Low",
				},
				"high" => lang switch
				{
					"ko" => "많이",
					"id" => "Banyak",
					"th" => "มาก",
					_ => "High",
				},
				"max" => lang switch
				{
					"ko" => "최대",
					"id" => "Maks",
					"th" => "ส\u0e38ด",
					_ => "Max",
				},
				_ => lang switch
				{
					"ko" => "보통",
					"id" => "Normal",
					"th" => "ปกต\u0e34",
					_ => "Normal",
				},
			};
		case "blood_color":
			return value switch
			{
				"green" => lang switch
				{
					"ko" => "초록",
					"id" => "Hijau",
					"th" => "เข\u0e35ยว",
					_ => "Green",
				},
				"blue" => lang switch
				{
					"ko" => "파랑",
					"id" => "Biru",
					"th" => "น\u0e49ำเง\u0e34น",
					_ => "Blue",
				},
				"purple" => lang switch
				{
					"ko" => "보라",
					"id" => "Ungu",
					"th" => "ม\u0e48วง",
					_ => "Purple",
				},
				"black" => lang switch
				{
					"ko" => "검정",
					"id" => "Hitam",
					"th" => "ดำ",
					_ => "Black",
				},
				_ => lang switch
				{
					"ko" => "빨강",
					"id" => "Merah",
					"th" => "แดง",
					_ => "Red",
				},
			};
		case "blood_stain_time":
		{
			int num = ParseBloodStainSeconds(value);
			if (num < 60)
			{
				return num + lang switch
				{
					"ko" => "초",
					"id" => " dtk",
					"th" => " ว\u0e34",
					_ => " s",
				};
			}
			return num / 60 + lang switch
			{
				"ko" => "분",
				"id" => " mnt",
				"th" => " นาท\u0e35",
				_ => " min",
			};
		}
		default:
			return null;
		}
	}

	public static int ParseBloodStainSeconds(string value)
	{
		if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var result) && result > 0)
		{
			return result;
		}
		return 180;
	}

	public static string MaxVisiblePlayersOptionLabel(string value)
	{
		if (ParseMaxVisiblePlayers(value) >= 0)
		{
			return value;
		}
		return CommunityDiscordLink.Lang switch
		{
			"th" => "ท\u0e31\u0e49งหมด",
			"id" => "Semua",
			"ko" => "전체",
			_ => "All",
		};
	}
}

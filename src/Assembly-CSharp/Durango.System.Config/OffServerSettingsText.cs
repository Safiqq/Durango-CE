using Durango.Logic.Clusters;

namespace Durango.System.Config;

public static class OffServerSettingsText
{
	public static string RowLabel(string key)
	{
		bool flag = OffServerDiscordLink.Lang == "th";
		switch (key)
		{
		case "select_character":
			if (!flag)
			{
				return "To title";
			}
			return "กล\u0e31บหน\u0e49าหล\u0e31ก (To Title)";
		case "leave":
			if (!flag)
			{
				return "Delete account";
			}
			return "ลบบ\u0e31ญช\u0e35";
		case "coupon":
			if (!flag)
			{
				return "Redeem coupon";
			}
			return "ใช\u0e49ค\u0e39ปอง / โค\u0e49ดของรางว\u0e31ล";
		default:
			if (!flag)
			{
				return null;
			}
			switch (key)
			{
			case "resolution":
				return "ความละเอ\u0e35ยดภาพ";
			case "resolution_pc":
				return "ความละเอ\u0e35ยดหน\u0e49าจอ";
			case "screen_mode":
				return "โหมดหน\u0e49าจอ";
			case "max_frame_rate":
			case "fps":
				return "อ\u0e31ตราเฟรม (FPS)";
			case "v_sync":
				return "ซ\u0e34งก\u0e4cเฟรมก\u0e31บหน\u0e49าจอ (V-Sync)";
			case "max_visible_players":
				return "จำนวนผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48นท\u0e35\u0e48แสดง";
			case "shadow":
				return "ค\u0e38ณภาพเงา";
			case "anti_aliasing":
				return "ลดรอยหย\u0e31กของภาพ";
			case "visual_effect":
				return "ค\u0e38ณภาพเอฟเฟกต\u0e4cภาพ";
			case "ui_size":
				return "ขนาดหน\u0e49าต\u0e48างและป\u0e38\u0e48มควบค\u0e38ม";
			case "capture_filter":
				return "ใช\u0e49ฟ\u0e34ลเตอร\u0e4cเม\u0e37\u0e48อถ\u0e48ายภาพ";
			case "vignette":
				return "เงาม\u0e37ดบร\u0e34เวณขอบจอ";
			case "chromatic_aberration":
				return "เอฟเฟกต\u0e4cเหล\u0e37\u0e48อมส\u0e35บร\u0e34เวณขอบจอ";
			case "sfx_volume":
				return "ระด\u0e31บเส\u0e35ยงเอฟเฟกต\u0e4c";
			case "ambience_volume":
				return "ระด\u0e31บเส\u0e35ยงบรรยากาศ";
			case "bgm_volume":
				return "ระด\u0e31บเส\u0e35ยงเพลงประกอบ";
			case "midi_volume":
				return "ระด\u0e31บเส\u0e35ยงเคร\u0e37\u0e48องดนตร\u0e35";
			case "offserver_voice_mode":
				return "โหมดเป\u0e34ดไมโครโฟน";
			case "offserver_voice_level":
				return "ระยะเส\u0e35ยงพ\u0e39ด";
			case "offserver_voice_volume":
				return "ระด\u0e31บเส\u0e35ยงพ\u0e39ดของผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48น";
			case "offserver_voice_sensitivity":
				return "ความไวของไมโครโฟน";
			case "offserver_voice_deaf":
				return "ป\u0e34ดเส\u0e35ยงสนทนาท\u0e31\u0e49งหมด";
			case "offserver_voice_panel":
				return "จ\u0e31ดการเส\u0e35ยงผ\u0e39\u0e49เล\u0e48นใกล\u0e49ต\u0e31ว";
			case "ui_mode":
				return "ร\u0e39ปแบบหน\u0e49าจอควบค\u0e38ม";
			case "mobile_move_mode":
				return "ว\u0e34ธ\u0e35เด\u0e34นบนม\u0e37อถ\u0e37อ";
			case "run_hold":
				return "กดค\u0e49างเพ\u0e37\u0e48อว\u0e34\u0e48ง";
			case "orientation":
				return "แนวหน\u0e49าจอ";
			case "mouse_reversed":
				return "สล\u0e31บป\u0e38\u0e48มเมาส\u0e4cซ\u0e49ายและขวา";
			case "net_transport":
				return "ร\u0e39ปแบบการเช\u0e37\u0e48อมต\u0e48อ";
			case "attack_range_player":
				return "แสดงระยะโจมต\u0e35ของต\u0e31วละคร";
			case "attack_range_monster":
				return "แสดงระยะโจมต\u0e35ของส\u0e31ตว\u0e4c";
			case "attack_range_intensity":
				return "ความเข\u0e49มของวงโจมต\u0e35";
			case "attack_range_width":
				return "ความหนาขอบวงโจมต\u0e35";
			case "attack_range_reduce_motion":
				return "ลดแสงเคล\u0e37\u0e48อนไหวของวง";
			case "attack_range_color_assist":
				return "ช\u0e48วยแยกส\u0e35วงโจมต\u0e35";
			case "attack_range_quality":
				return "ค\u0e38ณภาพเอฟเฟกต\u0e4cวงโจมต\u0e35";
			case "lava_shield_fx":
				return "เอฟเฟกต\u0e4cโล\u0e48ลาวาตอนวาร\u0e4cป";
			case "blood_fx":
				return "แสดงเล\u0e37อดและคราบเล\u0e37อด";
			case "blood_amount":
				return "ปร\u0e34มาณเล\u0e37อด";
			case "blood_stain_time":
				return "ระยะเวลาคราบเล\u0e37อด";
			case "blood_color":
				return "ส\u0e35เล\u0e37อด";
			case "show_fps":
				return "แสดงผ\u0e39\u0e49เล\u0e48นออนไลน\u0e4c / คนบนเกาะ / FPS / Ping";
			case "show_name_flags":
				return "แสดงธงและไอคอนหน\u0e49าช\u0e37\u0e48อ";
			case "auto_translation":
				return "แปลข\u0e49อความแชทอ\u0e31ตโนม\u0e31ต\u0e34";
			case "permit_conversation":
				return "ร\u0e31บข\u0e49อความส\u0e48วนต\u0e31วจากผ\u0e39\u0e49เล\u0e48นอ\u0e37\u0e48น";
			case "show_line_chat":
				return "แสดงแชทแบบบรรท\u0e31ด";
			case "show_draw_line":
				return "แสดงเส\u0e49นวาดของผ\u0e39\u0e49เล\u0e48น";
			case "show_chat_bubble":
				return "แสดงฟองคำพ\u0e39ด";
			case "locale":
				return "ภาษาเกม";
			case "voice_locale":
				return "ภาษาเส\u0e35ยงต\u0e31วละคร";
			case "character_name":
				return "ช\u0e37\u0e48อต\u0e31วละคร";
			case "private_number":
				return "หมายเลขบ\u0e31ญช\u0e35";
			case "terms":
				return "ข\u0e49อกำหนดการใช\u0e49งาน";
			case "credit":
				return "เครด\u0e34ตผ\u0e39\u0e49จ\u0e31ดทำ";
			default:
				return null;
			}
		}
	}

	public static string OptionLabel(string key, string value)
	{
		if (key == "target_lock_mode")
		{
			bool flag = OffServerDiscordLink.Lang == "th";
			if (!(value == "lowest_hp"))
			{
				if (value == "manual")
				{
					if (!flag)
					{
						return "Manual";
					}
					return "แมนนวล";
				}
				if (!flag)
				{
					return "Nearest";
				}
				return "ใกล\u0e49ส\u0e38ด";
			}
			if (!flag)
			{
				return "Lowest HP";
			}
			return "เล\u0e37อดต\u0e48ำส\u0e38ด";
		}
		if (OffServerDiscordLink.Lang != "th")
		{
			return null;
		}
		string text = (value ?? "").ToLowerInvariant();
		switch (key)
		{
		case "fps":
			if (!text.EndsWith(" fps"))
			{
				if (!(text == "quality"))
				{
					return "30 FPS";
				}
				return "60 FPS";
			}
			return value;
		case "ui_mode":
			if (!(text == "pc"))
			{
				if (!(text == "mobile"))
				{
					return null;
				}
				return "ม\u0e37อถ\u0e37อ";
			}
			return "คอมพ\u0e34วเตอร\u0e4c";
		case "screen_mode":
			if (!(text == "fullscreen"))
			{
				if (!(text == "window"))
				{
					return null;
				}
				return "หน\u0e49าต\u0e48าง";
			}
			return "เต\u0e47มหน\u0e49าจอ";
		case "orientation":
			return text switch
			{
				"auto" => "หม\u0e38นอ\u0e31ตโนม\u0e31ต\u0e34",
				"portrait" => "แนวต\u0e31\u0e49ง",
				"landscape" => "แนวนอน",
				_ => null,
			};
		case "anti_aliasing":
			if (!(text == "0"))
			{
				return value + "×";
			}
			return "ป\u0e34ด";
		case "attack_range_intensity":
			if (!(text == "low"))
			{
				if (!(text == "high"))
				{
					return "ปกต\u0e34";
				}
				return "เข\u0e49ม";
			}
			return "จาง";
		case "resolution":
		case "shadow":
		case "visual_effect":
		case "attack_range_quality":
			switch (text)
			{
			default:
				return null;
			case "normal":
			case "medium":
				return "ปกต\u0e34";
			case "high":
				return "ส\u0e39ง";
			case "low":
				return "ต\u0e48ำ";
			}
		case "blood_amount":
			if (text == "max")
			{
				return "มากท\u0e35\u0e48ส\u0e38ด";
			}
			break;
		}
		return null;
	}
}

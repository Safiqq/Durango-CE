using System;
using System.Collections.Generic;
using System.Text;
using Durango.Logic.Clusters;
using Durango.Logic.InputSystem;
using Durango.UI;
using UnityEngine;

namespace Durango.System.Config;

public static class OffServerHotkeys
{
	private sealed class Binding
	{
		public KeySet Template;

		public InputCommand Command;
	}

	private sealed class Entry
	{
		public string Id;

		public string Section;

		public InputCommand[] Commands;

		public KeyCode DefaultCode;

		public Modifier DefaultMods;

		public KeyCode Code;

		public Modifier Mods;

		public readonly List<Binding> Bindings = new List<Binding>();
	}

	public const string Category = "hotkey";

	public const string ResetKey = "hotkey_reset";

	private const string SettingPrefix = "hotkey_";

	private const string PrefPrefix = "option:hotkey:";

	private static readonly string[] Sections = new string[5] { "move", "action", "slots", "menu", "etc" };

	private static readonly Entry[] Entries = new Entry[31]
	{
		E("move_up", "move", KeyCode.W, InputCommand.Up),
		E("move_down", "move", KeyCode.S, InputCommand.Down),
		E("move_left", "move", KeyCode.A, InputCommand.Left),
		E("move_right", "move", KeyCode.D, InputCommand.Right),
		E("jump", "move", KeyCode.Space, InputCommand.PlayerJump),
		E("dodge", "move", KeyCode.Q, InputCommand.PlayerDodge),
		E("interact", "action", KeyCode.E, InputCommand.Collect, InputCommand.BeginFight),
		E("mount", "action", KeyCode.G, InputCommand.Mount),
		E("helper", "action", KeyCode.LeftAlt, Modifier.LeftAlt, InputCommand.HelperButtonAction),
		E("voice_talk", "action", KeyCode.X, InputCommand.VoicePushToTalk),
		E("voice_panel", "action", KeyCode.V, InputCommand.VoicePanel),
		E("voice_range", "action", KeyCode.B, InputCommand.VoiceRange),
		E("slot1", "slots", KeyCode.Alpha1, InputCommand.ContextAction0),
		E("slot2", "slots", KeyCode.Alpha2, InputCommand.ContextAction1),
		E("slot3", "slots", KeyCode.Alpha3, InputCommand.ContextAction2),
		E("slot4", "slots", KeyCode.Alpha4, InputCommand.ContextAction3),
		E("slot5", "slots", KeyCode.Alpha5, InputCommand.ContextAction4),
		E("slot6", "slots", KeyCode.Alpha6, InputCommand.ContextAction5),
		E("slot7", "slots", KeyCode.Alpha7, InputCommand.ContextAction6),
		E("slot8", "slots", KeyCode.Alpha8, InputCommand.ContextAction7),
		E("inventory", "menu", KeyCode.I, InputCommand.Inventory),
		E("character", "menu", KeyCode.P, InputCommand.Character),
		E("skill", "menu", KeyCode.K, InputCommand.Skill),
		E("worldmap", "menu", KeyCode.M, InputCommand.WorldMap),
		E("craft", "menu", KeyCode.C, InputCommand.Recipe),
		E("quest", "menu", KeyCode.J, InputCommand.Quest),
		E("menu_list", "menu", KeyCode.Tab, InputCommand.ShowMenuList),
		E("last_menu", "menu", KeyCode.R, InputCommand.RepeatLastMenu),
		E("last_item", "menu", KeyCode.Z, InputCommand.ShowLastAddedItem),
		E("emote", "menu", KeyCode.T, InputCommand.CommunicationMenuButtonAction),
		E("screenshot", "etc", KeyCode.F12, InputCommand.ScreenCapture)
	};

	private static readonly KeyCode[] Reserved = new KeyCode[12]
	{
		KeyCode.Escape,
		KeyCode.Return,
		KeyCode.KeypadEnter,
		KeyCode.F8,
		KeyCode.UpArrow,
		KeyCode.DownArrow,
		KeyCode.LeftArrow,
		KeyCode.RightArrow,
		KeyCode.LeftWindows,
		KeyCode.RightWindows,
		KeyCode.Print,
		KeyCode.SysReq
	};

	private static readonly KeyCode[] ModifierKeys = new KeyCode[8]
	{
		KeyCode.LeftAlt,
		KeyCode.RightAlt,
		KeyCode.LeftControl,
		KeyCode.RightControl,
		KeyCode.LeftShift,
		KeyCode.RightShift,
		KeyCode.LeftMeta,
		KeyCode.RightMeta
	};

	private static readonly Modifier[] ModifierFlags = new Modifier[8]
	{
		Modifier.LeftAlt,
		Modifier.RightAlt,
		Modifier.LeftControl,
		Modifier.RightControl,
		Modifier.LeftShift,
		Modifier.RightShift,
		Modifier.LeftCommand,
		Modifier.RightCommand
	};

	private static KeyCode[] s_candidates;

	private static InputKeyboard s_keyboard;

	private static Entry s_capturing;

	private static KeyCode s_pendingModifierKey;

	private static bool s_holdUntilRelease;

	private static readonly Dictionary<string, string[]> Texts = new Dictionary<string, string[]>
	{
		{
			"tab",
			new string[4] { "ป\u0e38\u0e48มล\u0e31ด", "Hotkeys", "Tombol Pintas", "단축키" }
		},
		{
			"sec_move",
			new string[4] { "การเคล\u0e37\u0e48อนท\u0e35\u0e48", "Movement", "Gerakan", "이동" }
		},
		{
			"sec_action",
			new string[4] { "การกระทำ", "Actions", "Aksi", "행동" }
		},
		{
			"sec_slots",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31นขวาล\u0e48าง (เร\u0e35ยงจากป\u0e38\u0e48มแรก)", "Action buttons (bottom right, in order)", "Tombol aksi (kanan bawah, berurutan)", "우측 하단 액션 버튼 (순서대로)" }
		},
		{
			"sec_menu",
			new string[4] { "เมน\u0e39", "Menus", "Menu", "메뉴" }
		},
		{
			"sec_etc",
			new string[4] { "อ\u0e37\u0e48นๆ", "Other", "Lainnya", "기타" }
		},
		{
			"move_up",
			new string[4] { "เด\u0e34นหน\u0e49า", "Move forward", "Maju", "앞으로 이동" }
		},
		{
			"move_down",
			new string[4] { "ถอยหล\u0e31ง", "Move back", "Mundur", "뒤로 이동" }
		},
		{
			"move_left",
			new string[4] { "ไปทางซ\u0e49าย", "Move left", "Ke kiri", "왼쪽으로 이동" }
		},
		{
			"move_right",
			new string[4] { "ไปทางขวา", "Move right", "Ke kanan", "오른쪽으로 이동" }
		},
		{
			"jump",
			new string[4] { "กระโดด", "Jump", "Lompat", "점프" }
		},
		{
			"dodge",
			new string[4] { "กล\u0e34\u0e49งหลบ (ตอนต\u0e48อส\u0e39\u0e49)", "Dodge roll (in combat)", "Berguling menghindar (saat bertarung)", "구르기 회피 (전투 중)" }
		},
		{
			"interact",
			new string[4] { "โต\u0e49ตอบส\u0e34\u0e48งท\u0e35\u0e48ใกล\u0e49ท\u0e35\u0e48ส\u0e38ด", "Interact with nearest", "Interaksi terdekat", "가까운 대상과 상호작용" }
		},
		{
			"mount",
			new string[4] { "ข\u0e35\u0e48ส\u0e31ตว\u0e4cเล\u0e35\u0e49ยง", "Ride pet", "Tunggangi hewan", "펫 타기" }
		},
		{
			"helper",
			new string[4] { "แสดงช\u0e37\u0e48อส\u0e34\u0e48งของรอบต\u0e31ว", "Show nearby names", "Tampilkan nama sekitar", "주변 이름 보기" }
		},
		{
			"voice_talk",
			new string[4] { "กดค\u0e49างพ\u0e39ด (ไมค\u0e4c)", "Hold to talk (voice)", "Tahan untuk bicara (suara)", "누르고 말하기 (음성)" }
		},
		{
			"voice_panel",
			new string[4] { "แผงต\u0e31\u0e49งค\u0e48าไมค\u0e4c", "Voice panel", "Panel suara", "음성 설정 창" }
		},
		{
			"voice_range",
			new string[4] { "สล\u0e31บระยะเส\u0e35ยง (กระซ\u0e34บ/ปกต\u0e34/ตะโกน)", "Change voice range (whisper/normal/shout)", "Ganti jangkauan suara (bisik/normal/teriak)", "음성 범위 변경 (속삭임/보통/외침)" }
		},
		{
			"slot1",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 1", "Action button 1", "Tombol aksi 1", "액션 버튼 1" }
		},
		{
			"slot2",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 2", "Action button 2", "Tombol aksi 2", "액션 버튼 2" }
		},
		{
			"slot3",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 3", "Action button 3", "Tombol aksi 3", "액션 버튼 3" }
		},
		{
			"slot4",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 4", "Action button 4", "Tombol aksi 4", "액션 버튼 4" }
		},
		{
			"slot5",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 5", "Action button 5", "Tombol aksi 5", "액션 버튼 5" }
		},
		{
			"slot6",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 6", "Action button 6", "Tombol aksi 6", "액션 버튼 6" }
		},
		{
			"slot7",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 7", "Action button 7", "Tombol aksi 7", "액션 버튼 7" }
		},
		{
			"slot8",
			new string[4] { "ป\u0e38\u0e48มแอคช\u0e31น 8", "Action button 8", "Tombol aksi 8", "액션 버튼 8" }
		},
		{
			"inventory",
			new string[4] { "กระเป\u0e4bา", "Inventory", "Tas", "가방" }
		},
		{
			"character",
			new string[4] { "ต\u0e31วละคร", "Character", "Karakter", "캐릭터" }
		},
		{
			"skill",
			new string[4] { "สก\u0e34ล", "Skills", "Skill", "스킬" }
		},
		{
			"worldmap",
			new string[4] { "แผนท\u0e35\u0e48โลก", "World map", "Peta dunia", "월드맵" }
		},
		{
			"craft",
			new string[4] { "คราฟต\u0e4c", "Crafting", "Kerajinan", "제작" }
		},
		{
			"quest",
			new string[4] { "เควส", "Quests", "Misi", "퀘스트" }
		},
		{
			"menu_list",
			new string[4] { "เมน\u0e39หล\u0e31ก", "Game menu", "Menu game", "게임 메뉴" }
		},
		{
			"last_menu",
			new string[4] { "เป\u0e34ดเมน\u0e39ล\u0e48าส\u0e38ดอ\u0e35กคร\u0e31\u0e49ง", "Reopen last menu", "Buka menu terakhir", "최근 메뉴 다시 열기" }
		},
		{
			"last_item",
			new string[4] { "ไอเทมท\u0e35\u0e48ได\u0e49ล\u0e48าส\u0e38ด", "Last obtained item", "Item terakhir", "최근 획득 아이템" }
		},
		{
			"emote",
			new string[4] { "อ\u0e35โมต", "Emotes", "Emote", "감정 표현" }
		},
		{
			"screenshot",
			new string[4] { "ถ\u0e48ายภาพหน\u0e49าจอ", "Screenshot", "Tangkapan layar", "스크린샷" }
		},
		{
			"reset_all",
			new string[4] { "ค\u0e37นค\u0e48าป\u0e38\u0e48มล\u0e31ดท\u0e31\u0e49งหมด", "Reset all hotkeys", "Atur ulang semua tombol", "단축키 모두 초기화" }
		},
		{
			"reset_confirm",
			new string[4] { "ค\u0e37นค\u0e48าป\u0e38\u0e48มล\u0e31ดท\u0e31\u0e49งหมดเป\u0e47นค\u0e48าเร\u0e34\u0e48มต\u0e49น?", "Reset every hotkey to the default?", "Kembalikan semua tombol pintas ke bawaan?", "모든 단축키를 기본값으로 되돌릴까요?" }
		},
		{
			"reset_ok",
			new string[4] { "ค\u0e37นค\u0e48า", "Reset", "Atur ulang", "초기화" }
		},
		{
			"capture",
			new string[4] { "กดป\u0e38\u0e48มใหม\u0e48สำหร\u0e31บ «{0}»", "Press a new key for \"{0}\"", "Tekan tombol baru untuk \"{0}\"", "\"{0}\"에 쓸 키를 누르세요" }
		},
		{
			"capture_sub",
			new string[4] { "ตอนน\u0e35\u0e49: {0}  ·  Esc = ยกเล\u0e34ก  ·  ป\u0e38\u0e48มท\u0e35\u0e48ซ\u0e49ำก\u0e31บแถวอ\u0e37\u0e48นจะสล\u0e31บก\u0e31น", "Now: {0}  ·  Esc = cancel  ·  a key used by another row is swapped", "Sekarang: {0}  ·  Esc = batal  ·  tombol yang sudah dipakai akan ditukar", "현재: {0}  ·  Esc = 취소  ·  겹치는 키는 서로 바뀝니다" }
		},
		{
			"default",
			new string[4] { "ค\u0e48าเร\u0e34\u0e48มต\u0e49น ({0})", "Default ({0})", "Bawaan ({0})", "기본값 ({0})" }
		},
		{
			"cancel",
			new string[4] { "ยกเล\u0e34ก", "Cancel", "Batal", "취소" }
		}
	};

	public static bool Supported
	{
		get
		{
			if (Application.platform != RuntimePlatform.WindowsPlayer)
			{
				return Application.platform == RuntimePlatform.WindowsEditor;
			}
			return true;
		}
	}

	public static bool BlockKeyboard
	{
		get
		{
			if (s_capturing == null)
			{
				return s_holdUntilRelease;
			}
			return true;
		}
	}

	public static bool IsHeld(string id, KeyCode fallback)
	{
		Entry entry = Array.Find(Entries, (Entry e) => e.Id == id);
		KeyCode keyCode = ((entry != null && Supported) ? entry.Code : fallback);
		if (keyCode != KeyCode.None)
		{
			return Input.GetKey(keyCode);
		}
		return true;
	}

	public static string CaptionOf(string id, string fallback)
	{
		Entry entry = Array.Find(Entries, (Entry e) => e.Id == id);
		if (entry == null)
		{
			return fallback;
		}
		return Caption(entry.Code, entry.Mods);
	}

	private static Entry E(string id, string section, KeyCode code, params InputCommand[] commands)
	{
		return E(id, section, code, Modifier.None, commands);
	}

	private static Entry E(string id, string section, KeyCode code, Modifier mods, params InputCommand[] commands)
	{
		return new Entry
		{
			Id = id,
			Section = section,
			Commands = commands,
			DefaultCode = code,
			DefaultMods = mods,
			Code = code,
			Mods = mods
		};
	}

	public static void Attach(InputKeyboard keyboard)
	{
		s_keyboard = keyboard;
		s_capturing = null;
		if (!Supported || keyboard == null)
		{
			return;
		}
		Entry[] entries = Entries;
		foreach (Entry entry in entries)
		{
			entry.Bindings.Clear();
			entry.Code = entry.DefaultCode;
			entry.Mods = entry.DefaultMods;
			foreach (KeyValuePair<KeySet, InputCommand> item in keyboard.KeyMap)
			{
				if (item.Key.Code == entry.DefaultCode && item.Key.Modifiers == entry.DefaultMods && Array.IndexOf(entry.Commands, item.Value) >= 0)
				{
					Binding binding = new Binding();
					binding.Template = item.Key;
					binding.Command = item.Value;
					entry.Bindings.Add(binding);
				}
			}
		}
		entries = Entries;
		foreach (Entry entry2 in entries)
		{
			if (Load(entry2.Id, out var code, out var mods) && (code != entry2.Code || mods != entry2.Mods))
			{
				Unapply(entry2);
				entry2.Code = code;
				entry2.Mods = mods;
				Apply(entry2);
			}
		}
	}

	public static List<Setting> BuildSettings()
	{
		List<Setting> list = new List<Setting>();
		int num = 0;
		string[] sections = Sections;
		foreach (string text in sections)
		{
			num++;
			OffServerHotkeySetting offServerHotkeySetting = new OffServerHotkeySetting();
			offServerHotkeySetting.Key = "hotkey_cat_" + text;
			offServerHotkeySetting.Type = SettingType.Category;
			offServerHotkeySetting.TextKey = "sec_" + text;
			list.Add(offServerHotkeySetting);
			GridSetting gridSetting = new GridSetting();
			gridSetting.Key = "hotkey_grid_" + text;
			gridSetting.Type = SettingType.Grid;
			gridSetting.Number = num;
			list.Add(gridSetting);
			Entry[] entries = Entries;
			foreach (Entry entry in entries)
			{
				if (!(entry.Section != text))
				{
					OffServerHotkeySetting offServerHotkeySetting2 = new OffServerHotkeySetting();
					offServerHotkeySetting2.Key = "hotkey_" + entry.Id;
					offServerHotkeySetting2.Type = SettingType.ButtonBox;
					offServerHotkeySetting2.GridNumber = num;
					offServerHotkeySetting2.TextKey = entry.Id;
					offServerHotkeySetting2.ActionId = entry.Id;
					list.Add(offServerHotkeySetting2);
				}
			}
		}
		OffServerHotkeySetting offServerHotkeySetting3 = new OffServerHotkeySetting();
		offServerHotkeySetting3.Key = "hotkey_reset";
		offServerHotkeySetting3.Type = SettingType.ButtonBox;
		offServerHotkeySetting3.TextKey = "reset_all";
		list.Add(offServerHotkeySetting3);
		return list;
	}

	public static string CaptionFor(Setting setting)
	{
		if (!(setting is OffServerHotkeySetting offServerHotkeySetting) || string.IsNullOrEmpty(offServerHotkeySetting.ActionId))
		{
			return null;
		}
		Entry entry = Find(offServerHotkeySetting.ActionId);
		if (entry == null)
		{
			return null;
		}
		return Caption(entry.Code, entry.Mods);
	}

	public static bool HandleAction(string key)
	{
		if (string.IsNullOrEmpty(key) || !key.StartsWith("hotkey_", StringComparison.Ordinal))
		{
			return false;
		}
		if (key == "hotkey_reset")
		{
			UIManager.MessageBox.Show(Text("reset_confirm"), delegate(bool ok)
			{
				if (ok)
				{
					ResetAll();
				}
			}, Text("reset_ok"), Text("cancel"));
			return true;
		}
		Entry entry = Find(key.Substring("hotkey_".Length));
		if (entry == null)
		{
			return false;
		}
		BeginCapture(entry);
		return true;
	}

	private static void BeginCapture(Entry entry)
	{
		if (!Supported || s_keyboard == null)
		{
			return;
		}
		s_capturing = entry;
		s_pendingModifierKey = KeyCode.None;
		s_holdUntilRelease = true;
		OffServerHotkeyCaptureDriver.Ensure();
		UIManager.MessageBox.Show(Text("capture", Text(entry.Id)), Text("capture_sub", Caption(entry.Code, entry.Mods)), delegate(int index)
		{
			if (s_capturing == entry)
			{
				s_capturing = null;
				if (index == 0)
				{
					SetBinding(entry, entry.DefaultCode, entry.DefaultMods);
				}
			}
		}, Text("default", Caption(entry.DefaultCode, entry.DefaultMods)), Text("cancel"));
	}

	internal static void UpdateCapture()
	{
		if (s_capturing == null)
		{
			if (s_holdUntilRelease && !Input.anyKey)
			{
				s_holdUntilRelease = false;
			}
			return;
		}
		if (UIManager.MessageBox == null || !UIManager.MessageBox.IsShow)
		{
			s_capturing = null;
			return;
		}
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			s_capturing = null;
			UIManager.MessageBox.Hide();
			return;
		}
		Modifier modifier = Modifier.None;
		for (int i = 0; i < ModifierKeys.Length; i++)
		{
			if (Input.GetKey(ModifierKeys[i]))
			{
				modifier |= ModifierFlags[i];
			}
			if (Input.GetKeyDown(ModifierKeys[i]))
			{
				s_pendingModifierKey = ModifierKeys[i];
			}
		}
		KeyCode[] array = Candidates();
		for (int j = 0; j < array.Length; j++)
		{
			if (Input.GetKeyDown(array[j]))
			{
				Finish(array[j], modifier);
				return;
			}
		}
		if (s_pendingModifierKey != KeyCode.None && Input.GetKeyUp(s_pendingModifierKey))
		{
			Finish(s_pendingModifierKey, ModifierFlags[Array.IndexOf(ModifierKeys, s_pendingModifierKey)]);
		}
	}

	private static void Finish(KeyCode code, Modifier mods)
	{
		Entry entry = s_capturing;
		s_capturing = null;
		UIManager.MessageBox.Hide();
		if (entry != null)
		{
			SetBinding(entry, code, mods);
		}
	}

	private static KeyCode[] Candidates()
	{
		if (s_candidates != null)
		{
			return s_candidates;
		}
		List<KeyCode> list = new List<KeyCode>();
		foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
		{
			if (value > KeyCode.None && value < (KeyCode)320 && Array.IndexOf(ModifierKeys, value) < 0 && Array.IndexOf(Reserved, value) < 0 && !list.Contains(value))
			{
				list.Add(value);
			}
		}
		s_candidates = list.ToArray();
		return s_candidates;
	}

	private static void SetBinding(Entry entry, KeyCode code, Modifier mods)
	{
		Entry entry2 = FindConflict(entry, code, mods);
		if (entry2 != null || code != entry.Code || mods != entry.Mods)
		{
			KeyCode code2 = entry.Code;
			Modifier mods2 = entry.Mods;
			Unapply(entry);
			if (entry2 != null)
			{
				Unapply(entry2);
				entry2.Code = code2;
				entry2.Mods = mods2;
				Apply(entry2);
				Save(entry2);
			}
			entry.Code = code;
			entry.Mods = mods;
			Apply(entry);
			Save(entry);
			UnityEngine.Debug.Log("[OffServer] hotkey " + entry.Id + " = " + Caption(code, mods) + ((entry2 != null) ? (" (swapped with " + entry2.Id + ")") : string.Empty));
			ConfigMainWidget.RefreshOffServerHotkeys();
		}
	}

	private static void ResetAll()
	{
		Entry[] entries = Entries;
		for (int i = 0; i < entries.Length; i++)
		{
			Unapply(entries[i]);
		}
		entries = Entries;
		foreach (Entry entry in entries)
		{
			entry.Code = entry.DefaultCode;
			entry.Mods = entry.DefaultMods;
			Apply(entry);
			PlayerPrefs.DeleteKey("option:hotkey:" + entry.Id);
		}
		PlayerPrefs.Save();
		ConfigMainWidget.RefreshOffServerHotkeys();
	}

	private static Entry FindConflict(Entry entry, KeyCode code, Modifier mods)
	{
		if (code == KeyCode.None)
		{
			return null;
		}
		Entry[] entries = Entries;
		foreach (Entry entry2 in entries)
		{
			if (entry2 == entry || entry2.Code != code || entry2.Mods != mods)
			{
				continue;
			}
			foreach (Binding binding in entry.Bindings)
			{
				foreach (Binding binding2 in entry2.Bindings)
				{
					if ((binding.Template.Layers & binding2.Template.Layers) != Layer.None && (binding.Template.Trigger & binding2.Template.Trigger) != Trigger.None)
					{
						return entry2;
					}
				}
			}
		}
		return null;
	}

	private static KeySet Make(KeySet template, KeyCode code, Modifier mods)
	{
		return new KeySet(code, mods, template.Layers, template.Trigger, template.IgnoreModifier);
	}

	private static void Apply(Entry entry)
	{
		if (s_keyboard == null || entry.Code == KeyCode.None)
		{
			return;
		}
		KeyCodeDictionary keyMap = s_keyboard.KeyMap;
		foreach (Binding binding in entry.Bindings)
		{
			KeySet keySet = Make(binding.Template, entry.Code, entry.Mods);
			keyMap.RemoveBinding(keySet);
			keyMap.Add(keySet, binding.Command);
		}
	}

	private static void Unapply(Entry entry)
	{
		if (s_keyboard == null || entry.Code == KeyCode.None)
		{
			return;
		}
		KeyCodeDictionary keyMap = s_keyboard.KeyMap;
		foreach (Binding binding in entry.Bindings)
		{
			KeySet keySet = Make(binding.Template, entry.Code, entry.Mods);
			if (keyMap.TryGetValue(keySet, out var value) && value == binding.Command)
			{
				keyMap.RemoveBinding(keySet);
			}
		}
	}

	private static Entry Find(string id)
	{
		Entry[] entries = Entries;
		foreach (Entry entry in entries)
		{
			if (entry.Id == id)
			{
				return entry;
			}
		}
		return null;
	}

	private static void Save(Entry entry)
	{
		if (entry.Code == entry.DefaultCode && entry.Mods == entry.DefaultMods)
		{
			PlayerPrefs.DeleteKey("option:hotkey:" + entry.Id);
		}
		else
		{
			string key = "option:hotkey:" + entry.Id;
			int code = (int)entry.Code;
			string text = code.ToString();
			code = (int)entry.Mods;
			PlayerPrefs.SetString(key, text + "," + code);
		}
		PlayerPrefs.Save();
	}

	private static bool Load(string id, out KeyCode code, out Modifier mods)
	{
		code = KeyCode.None;
		mods = Modifier.None;
		string[] array = PlayerPrefs.GetString("option:hotkey:" + id, string.Empty).Split(',');
		if (array.Length != 2 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2))
		{
			return false;
		}
		code = (KeyCode)result;
		mods = (Modifier)result2;
		return true;
	}

	private static string Caption(KeyCode code, Modifier mods)
	{
		if (code == KeyCode.None)
		{
			return "—";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyCode item in new KeySet(code, mods).ToKeyCodes())
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append('+');
			}
			stringBuilder.Append(InputKeyboard.KeyToCaption(item));
		}
		return stringBuilder.ToString();
	}

	public static string Text(string key, params object[] args)
	{
		if (!Texts.TryGetValue(key, out var value))
		{
			return key;
		}
		string text = OffServerDiscordLink.Lang switch
		{
			"th" => value[0],
			"id" => value[2],
			"ko" => value[3],
			_ => value[1],
		};
		if (args == null || args.Length == 0)
		{
			return text;
		}
		try
		{
			return string.Format(text, args);
		}
		catch (FormatException)
		{
			return text;
		}
	}
}

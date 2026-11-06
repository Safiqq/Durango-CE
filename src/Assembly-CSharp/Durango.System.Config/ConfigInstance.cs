using System;
using System.Collections;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.Offline;
using Durango.Render;
using Durango.Render.Camera;
using Durango.Render.Particle;
using Durango.Render.Screen;
using Durango.UI;
using Durango.UI.Popup;
using Durango.Utils;
using Durango.Utils.Extensions;
using L10N;
using Messages;
using Newtonsoft.Json.Linq;
using Shared.Social;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Rendering.PostProcessing;
using Yaml.Util;

namespace Durango.System.Config;

public static class ConfigInstance
{
	private static string _targetVoiceLocale;

	public const string UiModeKey = "ui_mode";

	private static bool _loadedForPcUi;

	private static readonly string[] ExtraFpsOptions = new string[2] { "90 FPS", "120 FPS" };

	public static Dictionary<string, List<Setting>> Settings { get; private set; }

	public static event Action<string> ValueChanged;

	public static void ReloadForUiMode()
	{
		bool usePCUI = Platform.Instance.UsePCUI;
		if (Settings != null && usePCUI != _loadedForPcUi)
		{
			UnityEngine.Debug.Log("[OffServer] settings list reloaded for the " + (usePCUI ? "PC" : "mobile") + " UI");
			LoadFromJson();
			LoadConfigValue();
			UIScrollView.FixedScrollWheelFactor = (usePCUI ? 0.4f : 0f);
		}
	}

	public static void Initialize()
	{
		LoadFromJson();
		LoadConfigValue();
		GameManager.Reset += delegate
		{
			ConfigInstance.ValueChanged = null;
		};
		if (Platform.Instance.UsePCUI)
		{
			UIScrollView.FixedScrollWheelFactor = 0.4f;
		}
	}

	private static void LoadFromJson()
	{
		_loadedForPcUi = Platform.Instance.UsePCUI;
		Dictionary<string, JObject> dictionary = Json.ReadFromFile<Dictionary<string, JObject>>((!_loadedForPcUi) ? "config_menu" : "config_menu_pc");
		Settings = new Dictionary<string, List<Setting>>();
		foreach (KeyValuePair<string, JObject> item in dictionary)
		{
			string key = item.Key;
			if (!Settings.ContainsKey(key))
			{
				Settings[key] = new List<Setting>();
			}
			foreach (KeyValuePair<string, JToken> item2 in item.Value)
			{
				Setting setting;
				switch (item2.Value.Value<string>("Type").ToEnum(SettingType.Invalid))
				{
				case SettingType.Toggle:
				case SettingType.Locale:
				{
					setting = Json.Read<ToggleSetting>(item2.Value);
					if (!(item2.Key == "resolution"))
					{
						break;
					}
					ToggleSetting toggleSetting = (ToggleSetting)setting;
					if (DeviceInfo.IsLowResolutionAllowed() || !toggleSetting.Options.Contains("low"))
					{
						break;
					}
					string[] array = new string[toggleSetting.Options.Length - 1];
					int num = 0;
					string[] options = toggleSetting.Options;
					foreach (string text in options)
					{
						if (text != "low")
						{
							array[num++] = text;
						}
					}
					toggleSetting.Options = array;
					break;
				}
				case SettingType.Slider:
					setting = Json.Read<SliderSetting>(item2.Value);
					break;
				case SettingType.TextInput:
				case SettingType.Category:
				case SettingType.Switch:
				case SettingType.CheckBox:
				case SettingType.ButtonBox:
				case SettingType.Banner:
					setting = Json.Read<ValueSetting>(item2.Value);
					break;
				case SettingType.TextLabel:
					setting = Json.Read<LabelSetting>(item2.Value);
					break;
				case SettingType.Grid:
					setting = Json.Read<GridSetting>(item2.Value);
					break;
				case SettingType.Dropdown:
					setting = Json.Read<DropdownSetting>(item2.Value);
					break;
				default:
					setting = Json.Read<Setting>(item2.Value);
					break;
				case SettingType.Invalid:
					continue;
				}
				if (setting != null && !setting.DebugBuild)
				{
					setting.Key = item2.Key;
					Settings[key].Add(setting);
				}
			}
		}
		InjectUiModeSetting();
		InjectDiscordLinkSetting();
		InjectMoveRedeemSetting();
		InjectHotkeySettings();
		InjectFpsCounterSetting();
		InjectGameplaySettings();
		InjectLanguagePackSetting();
		InjectVoiceSettings();
		AdjustFpsOptions();
		HideCreditBefore317();
	}

	private static void HideCreditBefore317()
	{
		if (Settings == null || OffServerEmoji.NameTagToggleSupported)
		{
			return;
		}
		foreach (List<Setting> value in Settings.Values)
		{
			value.RemoveAll((Setting s) => s.Key == "credit");
		}
	}

	private static void InjectGameplaySettings()
	{
		if (Settings == null || !Settings.ContainsKey("default"))
		{
			return;
		}
		List<Setting> list = Settings["default"];
		if (list.Exists((Setting s) => s.Key == "max_visible_players"))
		{
			return;
		}
		int num = list.FindIndex((Setting s) => s.Key == "show_fps");
		if (num < 0)
		{
			num = list.FindIndex((Setting s) => s.Key == "fps");
		}
		List<Setting> list2 = OffServerGameplayOptions.BuildSettings();
		list2.RemoveAll(delegate(Setting s)
		{
			foreach (List<Setting> value in Settings.Values)
			{
				if (value.Exists((Setting x) => x.Key == s.Key))
				{
					return true;
				}
			}
			return false;
		});
		list.InsertRange((num >= 0) ? (num + 1) : 0, list2);
	}

	private static void InjectLanguagePackSetting()
	{
		if (Settings == null || !Settings.ContainsKey("default"))
		{
			return;
		}
		List<Setting> list = Settings["default"];
		if (!list.Exists((Setting s) => s.Key == "lang_packs"))
		{
			ValueSetting valueSetting = new ValueSetting();
			valueSetting.Key = "lang_packs";
			valueSetting.Type = SettingType.ButtonBox;
			valueSetting.PrepareLabelText = "Language packs";
			int num = list.FindIndex((Setting s) => s.Key == "locale");
			list.Insert((num >= 0) ? (num + 1) : 0, valueSetting);
		}
	}

	private static void InjectFpsCounterSetting()
	{
		if (Settings == null || !Settings.ContainsKey("default"))
		{
			return;
		}
		List<Setting> list = Settings["default"];
		if (!list.Exists((Setting s) => s.Key == "show_fps"))
		{
			ValueSetting valueSetting = new ValueSetting();
			valueSetting.Key = "show_fps";
			valueSetting.Type = SettingType.Switch;
			valueSetting.Default = "false";
			valueSetting.Value = false;
			valueSetting.PrepareLabelText = "Show frame rate (FPS)";
			int num = list.FindIndex((Setting s) => s.Key == "fps");
			list.Insert((num >= 0) ? (num + 1) : 0, valueSetting);
		}
	}

	private static void AdjustFpsOptions()
	{
		if (Settings == null)
		{
			return;
		}
		bool flag = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			foreach (Setting item in setting.Value)
			{
				if (!(item is ToggleSetting toggleSetting) || toggleSetting.Key != "fps" || toggleSetting.Options == null)
				{
					continue;
				}
				List<string> list = new List<string>(toggleSetting.Options);
				string[] extraFpsOptions = ExtraFpsOptions;
				foreach (string text in extraFpsOptions)
				{
					if (!toggleSetting.Contains(text))
					{
						list.Add(text);
					}
				}
				toggleSetting.Options = list.ToArray();
				if (flag && toggleSetting.Contains("quality"))
				{
					toggleSetting.Default = "quality";
				}
			}
		}
	}

	private static void InjectHotkeySettings()
	{
		if (Settings == null || !OffServerHotkeys.Supported || Settings.ContainsKey("hotkey"))
		{
			return;
		}
		List<Setting> value = OffServerHotkeys.BuildSettings();
		Dictionary<string, List<Setting>> dictionary = new Dictionary<string, List<Setting>>();
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			dictionary[setting.Key] = setting.Value;
			if (setting.Key == "default")
			{
				dictionary["hotkey"] = value;
			}
		}
		if (!dictionary.ContainsKey("hotkey"))
		{
			dictionary["hotkey"] = value;
		}
		Settings = dictionary;
	}

	private static void InjectVoiceSettings()
	{
		if (Settings == null || Settings.ContainsKey("offserver_voice"))
		{
			return;
		}
		List<Setting> value = OffServerVoiceSettings.BuildSettings();
		string text = (Settings.ContainsKey("hotkey") ? "hotkey" : "default");
		Dictionary<string, List<Setting>> dictionary = new Dictionary<string, List<Setting>>();
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			dictionary[setting.Key] = setting.Value;
			if (setting.Key == text)
			{
				dictionary["offserver_voice"] = value;
			}
		}
		if (!dictionary.ContainsKey("offserver_voice"))
		{
			dictionary["offserver_voice"] = value;
		}
		Settings = dictionary;
	}

	private static void InjectDiscordLinkSetting()
	{
		if (Settings == null || !Settings.ContainsKey("account") || !OffServerLink.Active)
		{
			return;
		}
		List<Setting> list = Settings["account"];
		if (list.Exists((Setting s) => s.Key == "discord_link"))
		{
			return;
		}
		ValueSetting valueSetting = new ValueSetting();
		valueSetting.Key = "discord_link";
		valueSetting.Type = SettingType.ButtonBox;
		valueSetting.GridNumber = 1;
		valueSetting.PrepareLabelText = "Link Discord";
		int num = list.FindIndex((Setting s) => s.Key == "coupon");
		if (num < 0)
		{
			num = list.FindIndex((Setting s) => s.Key == "select_character");
		}
		if (num >= 0)
		{
			list.Insert(num + 1, valueSetting);
		}
		else
		{
			list.Add(valueSetting);
		}
	}

	private static void InjectMoveRedeemSetting()
	{
		if (Settings == null || !Settings.ContainsKey("account"))
		{
			return;
		}
		List<Setting> list = Settings["account"];
		if (list.Exists((Setting s) => s.Key == "move_redeem"))
		{
			return;
		}
		int num = list.FindIndex((Setting s) => s.Key == "move_device");
		if (num >= 0)
		{
			int num2 = list.FindIndex((Setting s) => s.Key == "credit");
			if (num2 > num)
			{
				Setting item = list[num2];
				list.RemoveAt(num2);
				list.Insert(num, item);
				num++;
			}
			ValueSetting valueSetting = new ValueSetting();
			valueSetting.Key = "move_redeem";
			valueSetting.Type = SettingType.ButtonBox;
			valueSetting.GridNumber = ((!(list[num] is ValueSetting valueSetting2)) ? 1 : valueSetting2.GridNumber);
			valueSetting.PrepareLabelText = OffServerMoveDevice.RedeemButtonLabel;
			list.Insert(num + 1, valueSetting);
		}
	}

	private static void InjectUiModeSetting()
	{
		if (!UiMode.IsWindows || Settings == null)
		{
			return;
		}
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			if (setting.Value.Exists((Setting s) => s.Key == "ui_mode"))
			{
				return;
			}
		}
		string key = ((!Settings.ContainsKey("screen")) ? "default" : "screen");
		if (Settings.ContainsKey(key))
		{
			List<Setting> list = Settings[key];
			ToggleSetting toggleSetting = new ToggleSetting();
			toggleSetting.Key = "ui_mode";
			toggleSetting.Type = SettingType.Toggle;
			toggleSetting.Options = new string[2] { "PC", "Mobile" };
			toggleSetting.Default = "Mobile";
			toggleSetting.PrepareLabelText = "UI Mode (PC / Mobile)";
			ToggleSetting item = toggleSetting;
			int num = list.FindIndex((Setting s) => s.Key == "screen_mode");
			if (num >= 0)
			{
				list.Insert(num + 1, item);
			}
			else
			{
				list.Insert(0, item);
			}
		}
	}

	private static void LoadConfigValue()
	{
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			foreach (Setting item in setting.Value)
			{
				if (!(item is ValueSetting valueSetting))
				{
					continue;
				}
				string saveKey = GetSaveKey(valueSetting.Key);
				switch (valueSetting.Type)
				{
				case SettingType.Toggle:
				case SettingType.TextInput:
				case SettingType.Category:
				{
					string value3 = PlayerPrefs.GetString(saveKey, null);
					if (item is ToggleSetting toggleSetting && !toggleSetting.Contains(value3))
					{
						if (valueSetting.Key == "screen_mode")
						{
							SetValue(valueSetting.Key, Screen.fullScreen ? "fullscreen" : "window");
							break;
						}
						value3 = VisualEffectSavedByPcList(valueSetting.Key, saveKey) ?? valueSetting.Default;
					}
					ChangeValue(valueSetting.Key, value3, save: false);
					break;
				}
				case SettingType.Locale:
				{
					string text = PlayerPrefs.GetString(saveKey, null);
					if (string.IsNullOrEmpty(text))
					{
						string value2 = ((valueSetting.Key == "voice_locale") ? LocalizeSystem.VoiceLocale : LocalizeSystem.Locale);
						if (!string.IsNullOrEmpty(value2))
						{
							SetValue(valueSetting.Key, value2);
							break;
						}
					}
					if (valueSetting.Key == "locale" && !GameManager.IsTitleScene && !string.IsNullOrEmpty(LocalizeSystem.Locale) && text != LocalizeSystem.Locale)
					{
						SetValue(valueSetting.Key, LocalizeSystem.Locale);
					}
					else
					{
						ChangeValue(valueSetting.Key, text, save: false);
					}
					break;
				}
				case SettingType.Slider:
				{
					float result2 = PlayerPrefs.GetFloat(saveKey, float.NaN);
					if (float.IsNaN(result2))
					{
						if (valueSetting.Key == "max_frame_rate" && Application.targetFrameRate > 0)
						{
							SetValue(valueSetting.Key, (float)Application.targetFrameRate);
							break;
						}
						float.TryParse(valueSetting.Default, out result2);
					}
					ChangeValue(valueSetting.Key, result2, save: false);
					break;
				}
				case SettingType.Switch:
				case SettingType.CheckBox:
				{
					int num = PlayerPrefs.GetInt(saveKey, -1);
					if (num == -1)
					{
						num = VisualEffectSavedByMobileList(valueSetting.Key, saveKey);
					}
					bool result;
					if (num == -1)
					{
						bool.TryParse(valueSetting.Default, out result);
					}
					else
					{
						result = num == 1;
					}
					ChangeValue(valueSetting.Key, result, save: false);
					break;
				}
				case SettingType.Dropdown:
					if (item is DropdownSetting dropdownSetting)
					{
						string value = PlayerPrefs.GetString(saveKey, null);
						if (dropdownSetting.Key != "resolution_pc" && !dropdownSetting.Contains(value))
						{
							value = valueSetting.Default;
						}
						ChangeValue(valueSetting.Key, value, save: false);
					}
					break;
				}
			}
		}
	}

	private static string VisualEffectSavedByPcList(string key, string saveKey)
	{
		if (key != "visual_effect")
		{
			return null;
		}
		return PlayerPrefs.GetInt(saveKey, -1) switch
		{
			0 => "low",
			1 => "high",
			_ => null,
		};
	}

	private static int VisualEffectSavedByMobileList(string key, string saveKey)
	{
		if (key != "visual_effect")
		{
			return -1;
		}
		string text = PlayerPrefs.GetString(saveKey, null);
		if (!(text == "low"))
		{
			if (text == "high")
			{
				return 1;
			}
			return -1;
		}
		return 0;
	}

	private static string GetSaveKey(string key)
	{
		return "option:" + key;
	}

	public static void UpdateValue(string key, string resolution)
	{
		SetValue(key, resolution);
		SaveValue(key, resolution);
	}

	public static string ChangeValue(string key, string value, bool save = true)
	{
		switch (key)
		{
		case "resolution":
			value = ChangeResolution(value);
			break;
		case "resolution_pc":
			value = ChangeResolution_PC(value);
			break;
		case "screen_mode":
			value = ChangeScreenMode(value);
			break;
		case "target_lock_mode":
			OffServerGameplayOptions.TargetLockMode = value;
			break;
		case "blood_amount":
			OffServerBloodFx.SetAmount(value);
			break;
		case "blood_color":
			OffServerBloodFx.SetColor(value);
			break;
		case "blood_stain_time":
			OffServerBloodFx.SetStainSeconds(OffServerGameplayOptions.ParseBloodStainSeconds(value));
			break;
		case "anti_aliasing":
			value = ChangeAntiAliasing(value);
			break;
		case "ui_size":
			ChangeUISize(value);
			break;
		case "fps":
			ChangeFps(value);
			break;
		case "locale":
			value = ChangeLocale(value);
			break;
		case "voice_locale":
			value = ChangeVoiceLocale(value);
			break;
		case "orientation":
			ChangeOrientation(value, save);
			break;
		case "shadow":
			ChangeShadowOption(value);
			break;
		case "visual_effect":
			Firefly.ChangeFireflyOption(value == "high");
			break;
		case "ui_mode":
			value = UiMode.Normalize(value);
			break;
		case "max_visible_players":
			PlayerManager.MaxVisiblePlayers = OffServerGameplayOptions.ParseMaxVisiblePlayers(value);
			break;
		case "combat_move_cancel":
			OffServerCombatMoveCancel.OnSetting(value, save);
			break;
		case "mobile_move_mode":
			value = OffServerMobileMove.OnSetting(value);
			break;
		case "net_transport":
			OffServerUdp.OnSetting(value, save);
			break;
		case "offserver_voice_mode":
		case "offserver_voice_level":
		case "offserver_voice_volume":
		case "offserver_voice_sensitivity":
			if (save)
			{
				OffServerVoiceSettings.Apply(key, value);
			}
			break;
		}
		SetValue(key, value);
		if (save)
		{
			SaveValue(key, value);
			if (key == "ui_mode")
			{
				UiMode.ApplyChanged(value);
			}
		}
		return value;
	}

	public static void MuteAll()
	{
		ChangeSfxVolume(0f);
		ChangeAmbienceVolume(0f);
		ChangeBgmVolume(0f);
		ChangeMidiVolume(0f);
	}

	public static void UnMuteAll()
	{
		ChangeSfxVolume(GetValue("sfx_volume", 0f));
		ChangeAmbienceVolume(GetValue("ambience_volume", 0f));
		ChangeBgmVolume(GetValue("bgm_volume", 0f));
		ChangeMidiVolume(GetValue("midi_volume", 0f));
	}

	public static float ChangeValue(string key, float value, bool save = true)
	{
		switch (key)
		{
		case "sfx_volume":
			ChangeSfxVolume(value);
			break;
		case "ambience_volume":
			ChangeAmbienceVolume(value);
			break;
		case "bgm_volume":
			ChangeBgmVolume(value);
			break;
		case "midi_volume":
			ChangeMidiVolume(value);
			break;
		case "max_frame_rate":
			ChangeMaxFrameRate(value);
			break;
		}
		SetValue(key, value);
		if (save)
		{
			SaveValue(key, value);
		}
		return value;
	}

	public static bool ChangeValue(string key, bool value, bool save = true)
	{
		switch (key)
		{
		case "capture_filter":
			Durango.Render.Screen.ScreenCapture.ApplyFilter = value;
			break;
		case "auto_translation":
			SocialSystem.AutoTranslation = value;
			break;
		case "show_draw_line":
			PlayerManager.ShowDrawLine = value;
			break;
		case "hide_debug_ui":
			if (Durango.Utils.Singleton<UIManager>.HasInstance())
			{
				DevelopmentGroup developmentGroup = UIManager.FindScript<DevelopmentGroup>();
				if (developmentGroup != null)
				{
					developmentGroup.gameObject.SetActive(!value);
				}
			}
			break;
		case "show_chat_bubble":
			ChatBubbleGroup.On = value;
			break;
		case "show_line_chat":
			LineChatWidget.On = value;
			break;
		case "permit_conversation":
			GameSystem<SocialSystem>.Instance().SetSocialOption(SocialOptionType.AllowOutlanderConversation, value);
			save = false;
			break;
		case "chromatic_aberration":
			ChangePostProcessing(key, value);
			break;
		case "vignette":
			ChangeVignettingEffect(value);
			break;
		case "visual_effect":
			Firefly.ChangeFireflyOption(value);
			break;
		case "v_sync":
			ChangeVerticalSync(value);
			break;
		case "show_fps":
			OffServerFpsCounter.SetVisible(value);
			break;
		case "attack_range_player":
			CombatSystem.PlayerAttackRangeEnabled = value;
			break;
		case "attack_range_monster":
			CombatSystem.MonsterAttackRangeEnabled = value;
			break;
		case "show_name_flags":
			ApplyShowNameFlags(value);
			break;
		case "lava_shield_fx":
			if (save)
			{
				OffServerLavaShieldNet.OnSettingChanged(value);
			}
			break;
		case "blood_fx":
			OffServerBloodFx.Enabled = value;
			break;
		case "offserver_voice_deaf":
			if (save)
			{
				OffServerVoiceSettings.Apply(key, value);
			}
			break;
		case "mouse_reversed":
			ChangeMouseReversed(value);
			break;
		}
		SetValue(key, value);
		if (save)
		{
			SaveValue(key, value);
		}
		return value;
	}

	private static void ApplyShowNameFlags(bool show)
	{
		if (OffServerEmoji.SetShowNameTags(show))
		{
			UILabel.RefreshAllEmoji();
			if (GameSystem<SocialSystem>.HasInstance())
			{
				GameSystem<SocialSystem>.Instance().RefreshChatList();
			}
		}
	}

	private static void ChangePostProcessing(string key, bool value)
	{
		if (!Durango.Utils.Singleton<MainCamera>.HasInstance())
		{
			return;
		}
		Camera camera = Durango.Utils.Singleton<MainCamera>.Instance().Camera;
		if (camera == null)
		{
			return;
		}
		PostProcessVolume component = camera.GetComponent<PostProcessVolume>();
		if (!(component == null))
		{
			PostProcessEffectSettings postProcessEffectSettings = null;
			if (key != null && key == "chromatic_aberration")
			{
				postProcessEffectSettings = component.profile.GetSetting<ChromaticAberration>();
			}
			if (postProcessEffectSettings != null)
			{
				postProcessEffectSettings.enabled.value = value;
			}
		}
	}

	private static void ChangeVignettingEffect(bool value)
	{
		ScreenEffect.UseVignettingEffect = value;
	}

	private static void ChangeVerticalSync(bool value)
	{
		QualitySettings.vSyncCount = (value ? 1 : 0);
	}

	public static void UpdatePostProcessingSettings()
	{
		string[] array = new string[1] { "chromatic_aberration" };
		foreach (string key in array)
		{
			string value = PlayerPrefs.GetString(GetSaveKey(key), null);
			if (!string.IsNullOrEmpty(value))
			{
				ChangeValue(key, value);
			}
		}
	}

	private static void SaveValue(string key, string value)
	{
		PlayerPrefs.SetString("option:" + key, value);
		PlayerPrefs.Save();
	}

	private static void SaveValue(string key, float value)
	{
		PlayerPrefs.SetFloat("option:" + key, value);
		PlayerPrefs.Save();
	}

	private static void SaveValue(string key, bool value)
	{
		PlayerPrefs.SetInt("option:" + key, value ? 1 : 0);
		PlayerPrefs.Save();
	}

	public static void RefreshValue(string key)
	{
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			List<Setting> value = setting.Value;
			int i = 0;
			for (int count = value.Count; i < count; i++)
			{
				if (value[i] is ValueSetting valueSetting && !(valueSetting.Key != key))
				{
					switch (value[i].Type)
					{
					case SettingType.Toggle:
					case SettingType.TextInput:
					case SettingType.Locale:
					case SettingType.Category:
					case SettingType.Dropdown:
						ChangeValue(valueSetting.Key, (string)valueSetting.Value, save: false);
						break;
					case SettingType.Slider:
						ChangeValue(valueSetting.Key, (float)valueSetting.Value, save: false);
						break;
					case SettingType.Switch:
					case SettingType.CheckBox:
						ChangeValue(valueSetting.Key, (bool)valueSetting.Value, save: false);
						break;
					case SettingType.TextLabel:
					case SettingType.Button:
					case SettingType.Grid:
					case SettingType.Account:
					case SettingType.ButtonBox:
					case SettingType.TinyButton:
					case SettingType.Banner:
						break;
					}
					return;
				}
			}
		}
	}

	private static void SetValue<TV>(string key, TV value)
	{
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			List<Setting> value2 = setting.Value;
			int i = 0;
			for (int count = value2.Count; i < count; i++)
			{
				if (value2[i] is ValueSetting valueSetting && valueSetting.Key == key)
				{
					valueSetting.Value = value;
					if (ConfigInstance.ValueChanged != null)
					{
						ConfigInstance.ValueChanged(key);
					}
					break;
				}
			}
		}
	}

	private static ValueSetting GetValue(string key)
	{
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			List<Setting> value = setting.Value;
			int i = 0;
			for (int count = value.Count; i < count; i++)
			{
				if (value[i] is ValueSetting valueSetting && valueSetting.Key == key)
				{
					return valueSetting;
				}
			}
		}
		return null;
	}

	public static TV GetValue<TV>(string key, TV defaultValue = default(TV))
	{
		foreach (KeyValuePair<string, List<Setting>> setting in Settings)
		{
			List<Setting> value = setting.Value;
			int i = 0;
			for (int count = value.Count; i < count; i++)
			{
				if (value[i] is ValueSetting valueSetting && valueSetting.Key == key)
				{
					if (valueSetting.Value is TV)
					{
						return (TV)valueSetting.Value;
					}
					return defaultValue;
				}
			}
		}
		return defaultValue;
	}

	public static void NotifyAction(string key, ValueSetting op = null)
	{
		if (OffServerSettingsMenu.HandleAction(key) || OffServerHotkeys.HandleAction(key) || OffServerVoiceSettings.HandleAction(key))
		{
			return;
		}
		switch (key)
		{
		case "lang_packs":
			OffServerLangPack.Open();
			break;
		case "select_character":
			Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
			break;
		case "logout":
			Logout();
			break;
		case "engagement":
			UIManager.Popup.Tooltip<EngagementConfigPopup>().Show();
			break;
		case "show_baseplate":
			Platform.Instance.ShowPlate();
			break;
		case "dump_personal_island":
		{
			DumpPersonalIslandPopup popup = UIManager.Popup.Tooltip<DumpPersonalIslandPopup>();
			popup.Show();
			Connections.Frontend.Send(new RequestDumpedPersonalIsland
			{
				PlayerEntityId = GameManager.PlayerId
			}).On(delegate(DumpedPersonalIsland msg, PacketHeader header)
			{
				AppData.CreateDirectory(WorldContext.GetBasePath(GameManager.ClusterKey));
				WorldContext worldContext = new WorldContext();
				worldContext.Initialize(WorldContext.MakePath(msg.PlayerSlot, GameManager.ClusterKey));
				worldContext.PlayerSlot = msg.PlayerSlot;
				worldContext.TerrainId = msg.TerrainId;
				worldContext.Garden = msg.Garden;
				PlayerContext playerContext = new PlayerContext();
				playerContext.PlayerSlot = msg.PlayerSlot;
				playerContext.Initialize(PlayerContext.MakePath(msg.PlayerSlot, GameManager.ClusterKey));
				playerContext.AppearPlayer = msg.AppearPlayer;
				if (msg.InventoryItems != null)
				{
					playerContext.InventoryItems.AddRange(msg.InventoryItems);
				}
				for (int i = 0; i < playerContext.InventoryItems.Count; i++)
				{
					Item value = playerContext.InventoryItems[i];
					value.Ext = null;
					playerContext.InventoryItems[i] = value;
				}
				playerContext.PlayerInfo.PlayerLevel = msg.AppearPlayer.Level;
				playerContext.PlayerInfo.PlayerName = msg.AppearPlayer.Name;
				playerContext.PlayerInfo.PlayerEntityId = msg.AppearPlayer.EntityId;
				for (int j = 0; j < KUtility.GetSize(msg.Artifacts); j++)
				{
					AppearArtifact value2 = msg.Artifacts[j];
					value2.States.Cage = null;
					worldContext.Artifacts[value2.EntityId] = value2;
				}
				worldContext.Save(persistent: true);
				playerContext.Save();
				popup.Hide();
				UIManager.SystemMsg(T._("개인섬 기록이 완료되었습니다."));
			}).Rest(delegate
			{
				popup.Hide();
			});
			break;
		}
		case "coupon":
			if (!OffServerCouponUI.Open(OffServerCouponUI.Mode.Coupon))
			{
				UIManager.SystemMsg("เป\u0e34ดหน\u0e49าค\u0e39ปองไม\u0e48สำเร\u0e47จ กร\u0e38ณาเป\u0e34ดหน\u0e49าต\u0e31\u0e49งค\u0e48าใหม\u0e48");
			}
			break;
		case "move_redeem":
			if (!OffServerCouponUI.Open(OffServerCouponUI.Mode.Move))
			{
				UIManager.Popup.Tooltip<TextInputPopup>().Show(SendCoupon, OffServerMoveDevice.RedeemButtonLabel);
			}
			break;
		case "discord_link":
			OffServerDiscordLink.Begin();
			break;
		case "move_device":
			OffServerMoveDevice.Begin();
			break;
		case "credit":
			UIManager.FindScript<CreditGroup>().Open();
			break;
		case "terms":
			ShowTerms();
			break;
		case "change_account":
			ConnectSnsAccount();
			break;
		case "leave":
			DeleteAccount();
			break;
		case "balance_info":
			UIManager.Popup.Tooltip<PaidCurrencyInfoPopup>().DefaultSetting().Show();
			break;
		default:
			OpenUrl(key, op);
			break;
		}
	}

	public static void SendCoupon(string coupon)
	{
		if (string.IsNullOrEmpty(coupon))
		{
			return;
		}
		bool moveCode = OffServerMoveDevice.LooksLikeCode(coupon);
		Connections.Frontend.Send(new AcceptTENCoupon
		{
			CouponNum = coupon,
			ToyToken = (OffServerLink.Active ? OffServerLink.AccountId : Platform.Instance.Token)
		}).On<OK>(delegate
		{
			if (moveCode)
			{
				OffServerMoveDevice.OnRedeemed();
			}
			else
			{
				UIManager.Alarm.ShowNotify(T._("선물이 지급되었습니다. 우편함을 확인해주세요."), "icon_mainhud_shop", major: false);
			}
		});
	}

	private static string ChangeResolution(string value)
	{
		if (value.TryEnum<DeviceInfo.Resolution>(out var value2))
		{
			DeviceInfo.ChangeResolution(value2);
		}
		else
		{
			DeviceInfo.ChangeResolution(DeviceInfo.DefaultResolution);
			value = DeviceInfo.DefaultResolution.ToString().ToLower();
		}
		return value;
	}

	private static string ChangeResolution_PC(string value)
	{
		ScreenInfo.SetScreenSize(value);
		return value;
	}

	private static string ChangeScreenMode(string value)
	{
		ScreenInfo.SetScreenMode(value == "fullscreen");
		return value;
	}

	private static string ChangeAntiAliasing(string value)
	{
		BlitScreen.AntiAliasingChanged = true;
		BlitScreen.AntiAliasingValue = value.ToInt();
		return value;
	}

	private static void ChangeUISize(string value)
	{
		if (int.TryParse(value, out var result))
		{
			UIManager.SetUISize(result);
		}
	}

	private static void ChangeFps(string value)
	{
		if (value != null && value.EndsWith(" FPS") && int.TryParse(value.Substring(0, value.Length - 4), out var result) && result > 0)
		{
			Application.targetFrameRate = result;
		}
		else
		{
			Application.targetFrameRate = ((!(value == "quality")) ? 30 : 60);
		}
	}

	private static string ChangeLocale(string value)
	{
		if (!string.IsNullOrEmpty(value) && LocalizeSystem.Locale == value)
		{
			return value;
		}
		if (GameManager.IsTitleScene || string.IsNullOrEmpty(LocalizeSystem.Locale))
		{
			return LocalizeSystem.SetLocale(value);
		}
		if (UIManager.MessageBox == null)
		{
			return LocalizeSystem.Locale;
		}
		MessageBox.Button[] items = (Application.isEditor ? new MessageBox.Button[3]
		{
			new MessageBox.Button(T._("확인")),
			"변경 후 재시작 하지 않음",
			T._("취소")
		} : new MessageBox.Button[2]
		{
			new MessageBox.Button(T._("확인")),
			T._("취소")
		});
		UIManager.MessageBox.Show(T._("언어 설정을 바꾸면 게임을 재시작해야 합니다.\n재시작하시겠습니까?"), delegate(int index)
		{
			string targetVoiceLocale = _targetVoiceLocale;
			_targetVoiceLocale = null;
			if (Application.isEditor)
			{
				if (index >= 2)
				{
					return;
				}
			}
			else if (index >= 1)
			{
				return;
			}
			string value2 = LocalizeSystem.SetLocale(value);
			SaveValue("locale", value2);
			if (!string.IsNullOrEmpty(targetVoiceLocale))
			{
				string value3 = LocalizeSystem.SetVoiceLocale(targetVoiceLocale);
				SaveValue("voice_locale", value3);
			}
			if (index == 0)
			{
				Loader.Cached = false;
				Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
			}
			else
			{
				Durango.Utils.Singleton<GameManager>.Instance().StartCoroutine(NoRestartChangeLocalize());
			}
		}, items);
		return LocalizeSystem.Locale;
	}

	private static string ChangeVoiceLocale(string value)
	{
		if (!string.IsNullOrEmpty(value) && LocalizeSystem.VoiceLocale == value)
		{
			return value;
		}
		if (GameManager.IsTitleScene || string.IsNullOrEmpty(LocalizeSystem.VoiceLocale))
		{
			return LocalizeSystem.SetVoiceLocale(value);
		}
		if (UIManager.MessageBox.IsShow)
		{
			_targetVoiceLocale = value;
			return value;
		}
		UIManager.MessageBox.Show(T._("음성 설정을 바꾸면 게임을 재시작해야 합니다.\n재시작하시겠습니까?"), delegate(bool ok)
		{
			if (ok)
			{
				string value2 = LocalizeSystem.SetVoiceLocale(value);
				SaveValue("voice_locale", value2);
				Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
			}
		});
		return LocalizeSystem.VoiceLocale;
	}

	private static IEnumerator NoRestartChangeLocalize()
	{
		UIManager.ShowLoadingIcon(show: true);
		Loader.Cached = false;
		Loader.Load(Durango.Utils.Singleton<GameManager>.Instance());
		while (Loader.LoadState != Loader.State.Succees)
		{
			yield return null;
		}
		Connections.Frontend.Close();
	}

	private static void ChangeOrientation(string value, bool update)
	{
		OrientationController.Orientation orientation = value.ToEnum(OrientationController.Orientation.Landscape);
		OrientationController.SetTargetOrientation(orientation, update);
		if (orientation != OrientationController.Orientation.Landscape && !Application.isEditor && !update)
		{
			Analytics.CustomEvent(orientation.ToString());
		}
	}

	private static void ChangeShadowOption(string value)
	{
		ShadowOption shadowOption = ShadowOption.Normal;
		if (!(value == "high"))
		{
			if (value == "low")
			{
				shadowOption = ShadowOption.Simple;
			}
		}
		else
		{
			shadowOption = ShadowOption.Normal;
		}
		PlaneShadowManager.ChangeOption(shadowOption);
	}

	private static void ChangeMouseReversed(bool value)
	{
		InputSystem.IsMouseButtonReversed = value;
	}

	private static void ChangeSfxVolume(float val)
	{
		SoundManager.SetSfxVolume(val);
	}

	private static void ChangeAmbienceVolume(float val)
	{
		SoundManager.SetAmbienceVolume(val);
	}

	private static void ChangeMidiVolume(float val)
	{
		SoundManager.SetMidiVolume(val);
	}

	private static void ChangeBgmVolume(float val)
	{
		if (Durango.Utils.Singleton<BgmManager>.HasInstance())
		{
			Durango.Utils.Singleton<BgmManager>.Instance().SetMute(val == 0f);
		}
		SoundManager.SetBgmVolume(val);
	}

	private static void ChangeMaxFrameRate(float val)
	{
		Application.targetFrameRate = Mathf.FloorToInt(val);
	}

	private static void ConnectSnsAccount()
	{
		if (Platform.Instance.IsLoginTypeGuest)
		{
			Platform.Instance.ShowAccountMenu();
		}
		else
		{
			UIManager.MessageBox.Show(T._("이미 연동된 계정입니다."));
		}
	}

	private static void DeleteAccount()
	{
		string mainText = ((!Platform.Instance.IsLoginTypeGuest) ? T._("탈퇴하시겠습니까?\n탈퇴하시면 <alert>모든 게임 데이터</alert>가 삭제됩니다.\n(계정 내 모든 캐릭터, 캐시샵 구매 내역 등)\n\n탈퇴 후 7일 안에 재접속하시면 복구할 수 있습니다.") : T._("탈퇴하시겠습니까?\n탈퇴하시면 <alert>모든 게임 데이터</alert>가 삭제됩니다.\n(계정 내 모든 캐릭터, 캐시샵 구매 내역 등)\n게스트 계정은 복구가 불가능합니다."));
		UIManager.MessageBox.Show(mainText, delegate(bool ok)
		{
			if (ok)
			{
				Connections.Frontend.Send(default(DeregisterUser)).On<OK>(delegate
				{
					Platform.Instance.Leave(delegate(bool success)
					{
						if (success)
						{
							Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
						}
					});
				}).Rest(delegate
				{
					UIManager.MessageBox.Show(T._("요청을 처리하지 못했습니다."));
				});
			}
		});
	}

	public static void Logout()
	{
		string mainText = ((!Platform.Instance.IsLoginTypeGuest) ? T._("정말로 로그아웃하시겠습니까?") : T._("계정 연동을 하지 않은 상태로 로그아웃하면,\n다시 로그인할 수 없습니다.\n\n정말로 로그아웃하시겠습니까?"));
		UIManager.MessageBox.Show(mainText, delegate(bool ok)
		{
			if (ok)
			{
				Platform.Instance.Logout(OnLogout);
			}
		});
	}

	private static void OnLogout(bool success)
	{
		if (success)
		{
			Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
			return;
		}
		MessageBox messageBox = UIManager.MessageBox;
		if (messageBox != null)
		{
			messageBox.Show(T._("로그아웃에 실패했습니다."));
		}
	}

	public static string GetPresetValue(PresetValue value)
	{
		return value switch
		{
			PresetValue.NPA => Platform.Instance.NPA,
			PresetValue.PlayerName => PlayerBehavior.LocalPlayer.PlayerName,
			PresetValue.Facebook => Platform.Instance.IsConnectFacebook.ToString(),
			PresetValue.GooglePlus => Platform.Instance.IsConnectGooglePlus.ToString(),
			_ => null,
		};
	}

	public static void OpenOfficialCommunityUrl()
	{
		OpenUrl("official_community", GetValue("official_community"));
	}

	private static void OpenUrl(string key, ValueSetting op)
	{
		if (op == null)
		{
			return;
		}
		if (op.Value is JToken jToken)
		{
			op.Value = Json.Read<Dictionary<string, string>>(jToken);
		}
		if (op.Value is Dictionary<string, string> dict)
		{
			string text = dict.Get(LocalizeSystem.Locale);
			if (string.IsNullOrEmpty(text))
			{
				text = dict.Get("default");
			}
			if (!string.IsNullOrEmpty(text))
			{
				Platform.Instance.ShowWeb(LocalizeSystem.Get("#config_" + key), text);
			}
		}
	}

	private static void ShowTerms()
	{
		SimpleTextListPopup simpleTextListPopup = UIManager.Popup.Tooltip<SimpleTextListPopup>();
		simpleTextListPopup.Set("Durango: Community Edition v1.0", new string[3]
		{
			"Preservation Project\nDurango: Community Edition is an unofficial fan preservation initiative.",
			"Non-Commercial Use\nThis software is provided free of charge for non-commercial preservation and educational purposes without warranty of any kind.",
			"Offline & Community Servers\nDesigned to run locally or connect to community-operated game servers."
		});
		simpleTextListPopup.Show();
	}
}

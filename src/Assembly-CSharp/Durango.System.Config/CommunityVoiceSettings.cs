using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.UI;
using UnityEngine;

namespace Durango.System.Config;

public static class CommunityVoiceSettings
{
	public const string Category = "community_voice";

	public const string ModeKey = "community_voice_mode";

	public const string LevelKey = "community_voice_level";

	public const string VolumeKey = "community_voice_volume";

	public const string SensitivityKey = "community_voice_sensitivity";

	public const string DeafKey = "community_voice_deaf";

	public const string PanelKey = "community_voice_panel";

	private static readonly string[] ModeOptions = new string[2] { "hold", "auto" };

	private static readonly string[] LevelOptions = new string[3] { "0", "1", "2" };

	private static readonly string[] VolumeOptions = new string[9] { "0", "25", "50", "75", "100", "125", "150", "175", "200" };

	public static bool Visible => CommunityVoice.Available;

	public static string TabLabel => CommunityVoiceUI.Text("tab");

	public static List<Setting> BuildSettings()
	{
		List<Setting> list = new List<Setting>();
		list.Add(MakeToggle("community_voice_mode", ModeOptions, "hold"));
		list.Add(MakeToggle("community_voice_level", LevelOptions, "1"));
		list.Add(MakeToggle("community_voice_volume", VolumeOptions, "100"));
		string[] array = new string[CommunityVoice.SensitivityDb.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (i + 1).ToString(CultureInfo.InvariantCulture);
		}
		list.Add(MakeToggle("community_voice_sensitivity", array, "4"));
		ValueSetting valueSetting = new ValueSetting();
		valueSetting.Key = "community_voice_deaf";
		valueSetting.Type = SettingType.Switch;
		valueSetting.Default = "false";
		valueSetting.Value = false;
		valueSetting.HideOnOffline = true;
		valueSetting.PrepareLabelText = Label("community_voice_deaf");
		list.Add(valueSetting);
		ValueSetting valueSetting2 = new ValueSetting();
		valueSetting2.Key = "community_voice_panel";
		valueSetting2.Type = SettingType.ButtonBox;
		valueSetting2.HideOnOffline = true;
		valueSetting2.PrepareLabelText = Label("community_voice_panel");
		list.Add(valueSetting2);
		SyncValues(list);
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
			Value = defaultValue,
			HideOnOffline = true,
			PrepareLabelText = Label(key)
		};
	}

	public static void SyncValues(List<Setting> list)
	{
		if (list == null)
		{
			return;
		}
		foreach (Setting item in list)
		{
			if (item is ValueSetting valueSetting)
			{
				switch (item.Key)
				{
				case "community_voice_mode":
					valueSetting.Value = (CommunityVoice.AutoMode ? "auto" : "hold");
					break;
				case "community_voice_level":
					valueSetting.Value = CommunityVoice.Level.ToString(CultureInfo.InvariantCulture);
					break;
				case "community_voice_volume":
					valueSetting.Value = (Mathf.RoundToInt(CommunityVoice.Volume * 4f) * 25).ToString(CultureInfo.InvariantCulture);
					break;
				case "community_voice_sensitivity":
					valueSetting.Value = (CommunityVoice.Sensitivity + 1).ToString(CultureInfo.InvariantCulture);
					break;
				case "community_voice_deaf":
					valueSetting.Value = CommunityVoice.Deaf;
					break;
				}
			}
		}
	}

	public static bool Apply(string key, string value)
	{
		int result;
		switch (key)
		{
		case "community_voice_mode":
			CommunityVoice.SetAutoMode(value == "auto");
			return true;
		case "community_voice_level":
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				CommunityVoice.SetLevel(result);
				CommunityVoiceUI.FlashRange();
			}
			return true;
		case "community_voice_volume":
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				CommunityVoice.SetVolume((float)result / 100f);
			}
			return true;
		case "community_voice_sensitivity":
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				CommunityVoice.SetSensitivity(result - 1);
			}
			return true;
		default:
			return false;
		}
	}

	public static bool Apply(string key, bool value)
	{
		if (key != "community_voice_deaf")
		{
			return false;
		}
		CommunityVoice.SetDeaf(value);
		return true;
	}

	public static bool HandleAction(string key)
	{
		if (key != "community_voice_panel")
		{
			return false;
		}
		try
		{
			ConfigGroup configGroup = UIManager.FindScript<ConfigGroup>();
			if (configGroup != null && configGroup.gameObject.activeInHierarchy)
			{
				configGroup.Close();
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
		CommunityVoiceUI.OpenPanel();
		return true;
	}

	public static string Label(string key)
	{
		return key switch
		{
			"community_voice_mode" => CommunityVoiceUI.Text("mode"),
			"community_voice_level" => CommunityVoiceUI.Text("range"),
			"community_voice_volume" => CommunityVoiceUI.Text("volume"),
			"community_voice_sensitivity" => CommunityVoiceUI.Text("sens"),
			"community_voice_deaf" => CommunityVoiceUI.Text("deaf"),
			"community_voice_panel" => CommunityVoiceUI.Text("nearby_button"),
			_ => null,
		};
	}

	public static string OptionLabel(string key, string value)
	{
		switch (key)
		{
		case "community_voice_mode":
			return CommunityVoiceUI.Text((value == "auto") ? "mode_auto" : "mode_hold");
		case "community_voice_level":
		{
			if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return value;
			}
			return CommunityVoiceUI.LevelLabel(result);
		}
		case "community_voice_volume":
			return value + "%";
		case "community_voice_sensitivity":
			return value + "/" + CommunityVoice.SensitivityDb.Length.ToString(CultureInfo.InvariantCulture);
		default:
			return null;
		}
	}
}

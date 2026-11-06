using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.UI;
using UnityEngine;

namespace Durango.System.Config;

public static class OffServerVoiceSettings
{
	public const string Category = "offserver_voice";

	public const string ModeKey = "offserver_voice_mode";

	public const string LevelKey = "offserver_voice_level";

	public const string VolumeKey = "offserver_voice_volume";

	public const string SensitivityKey = "offserver_voice_sensitivity";

	public const string DeafKey = "offserver_voice_deaf";

	public const string PanelKey = "offserver_voice_panel";

	private static readonly string[] ModeOptions = new string[2] { "hold", "auto" };

	private static readonly string[] LevelOptions = new string[3] { "0", "1", "2" };

	private static readonly string[] VolumeOptions = new string[9] { "0", "25", "50", "75", "100", "125", "150", "175", "200" };

	public static bool Visible => OffServerVoice.Available;

	public static string TabLabel => OffServerVoiceUI.Text("tab");

	public static List<Setting> BuildSettings()
	{
		List<Setting> list = new List<Setting>();
		list.Add(MakeToggle("offserver_voice_mode", ModeOptions, "hold"));
		list.Add(MakeToggle("offserver_voice_level", LevelOptions, "1"));
		list.Add(MakeToggle("offserver_voice_volume", VolumeOptions, "100"));
		string[] array = new string[OffServerVoice.SensitivityDb.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (i + 1).ToString(CultureInfo.InvariantCulture);
		}
		list.Add(MakeToggle("offserver_voice_sensitivity", array, "4"));
		ValueSetting valueSetting = new ValueSetting();
		valueSetting.Key = "offserver_voice_deaf";
		valueSetting.Type = SettingType.Switch;
		valueSetting.Default = "false";
		valueSetting.Value = false;
		valueSetting.HideOnOffline = true;
		valueSetting.PrepareLabelText = Label("offserver_voice_deaf");
		list.Add(valueSetting);
		ValueSetting valueSetting2 = new ValueSetting();
		valueSetting2.Key = "offserver_voice_panel";
		valueSetting2.Type = SettingType.ButtonBox;
		valueSetting2.HideOnOffline = true;
		valueSetting2.PrepareLabelText = Label("offserver_voice_panel");
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
				case "offserver_voice_mode":
					valueSetting.Value = (OffServerVoice.AutoMode ? "auto" : "hold");
					break;
				case "offserver_voice_level":
					valueSetting.Value = OffServerVoice.Level.ToString(CultureInfo.InvariantCulture);
					break;
				case "offserver_voice_volume":
					valueSetting.Value = (Mathf.RoundToInt(OffServerVoice.Volume * 4f) * 25).ToString(CultureInfo.InvariantCulture);
					break;
				case "offserver_voice_sensitivity":
					valueSetting.Value = (OffServerVoice.Sensitivity + 1).ToString(CultureInfo.InvariantCulture);
					break;
				case "offserver_voice_deaf":
					valueSetting.Value = OffServerVoice.Deaf;
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
		case "offserver_voice_mode":
			OffServerVoice.SetAutoMode(value == "auto");
			return true;
		case "offserver_voice_level":
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				OffServerVoice.SetLevel(result);
				OffServerVoiceUI.FlashRange();
			}
			return true;
		case "offserver_voice_volume":
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				OffServerVoice.SetVolume((float)result / 100f);
			}
			return true;
		case "offserver_voice_sensitivity":
			if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
			{
				OffServerVoice.SetSensitivity(result - 1);
			}
			return true;
		default:
			return false;
		}
	}

	public static bool Apply(string key, bool value)
	{
		if (key != "offserver_voice_deaf")
		{
			return false;
		}
		OffServerVoice.SetDeaf(value);
		return true;
	}

	public static bool HandleAction(string key)
	{
		if (key != "offserver_voice_panel")
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
		OffServerVoiceUI.OpenPanel();
		return true;
	}

	public static string Label(string key)
	{
		return key switch
		{
			"offserver_voice_mode" => OffServerVoiceUI.Text("mode"),
			"offserver_voice_level" => OffServerVoiceUI.Text("range"),
			"offserver_voice_volume" => OffServerVoiceUI.Text("volume"),
			"offserver_voice_sensitivity" => OffServerVoiceUI.Text("sens"),
			"offserver_voice_deaf" => OffServerVoiceUI.Text("deaf"),
			"offserver_voice_panel" => OffServerVoiceUI.Text("nearby_button"),
			_ => null,
		};
	}

	public static string OptionLabel(string key, string value)
	{
		switch (key)
		{
		case "offserver_voice_mode":
			return OffServerVoiceUI.Text((value == "auto") ? "mode_auto" : "mode_hold");
		case "offserver_voice_level":
		{
			if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return value;
			}
			return OffServerVoiceUI.LevelLabel(result);
		}
		case "offserver_voice_volume":
			return value + "%";
		case "offserver_voice_sensitivity":
			return value + "/" + OffServerVoice.SensitivityDb.Length.ToString(CultureInfo.InvariantCulture);
		default:
			return null;
		}
	}
}

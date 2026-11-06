using System;
using Durango.Logic.Clusters;
using Durango.System;
using UnityEngine;

public static class OffServerMobileMove
{
	public const string SettingKey = "mobile_move_mode";

	private static string _selected;

	public static string Selected
	{
		get
		{
			if (_selected == null)
			{
				string value = null;
				try
				{
					value = PlayerPrefs.GetString("option:mobile_move_mode", "both");
				}
				catch (Exception)
				{
				}
				_selected = OffServerMobileMoveRules.Normalize(value);
			}
			return _selected;
		}
	}

	public static bool ShowSettingRow => !Platform.Instance.UsePCUI;

	public static string OnSetting(string value)
	{
		_selected = OffServerMobileMoveRules.Normalize(value);
		return _selected;
	}

	public static string Current(bool drawMode)
	{
		return OffServerMobileMoveRules.Resolve(Selected, Platform.Instance.UsePCUI, GameManager.IsPrologueMode, drawMode);
	}

	public static string Current()
	{
		return Current(GameSystem<InputSystem>.HasInstance() && GameSystem<InputSystem>.Instance().DrawMode);
	}

	public static bool TapWalkEnabled(InputTouch.TouchEvent touchEvent)
	{
		return OffServerMobileMoveRules.TapWalkEnabled(Current(), touchEvent != null && touchEvent.TouchId == -10);
	}

	public static bool PickingWalkEnabled(InputCommandMessage message)
	{
		if (message == null || message.MouseButton == KeyCode.None)
		{
			return true;
		}
		return OffServerMobileMoveRules.MouseButtonWalkEnabled(Current());
	}

	public static string Label()
	{
		return OffServerMobileMoveRules.Label(OffServerDiscordLink.Lang);
	}

	public static string OptionLabel(string value)
	{
		return OffServerMobileMoveRules.OptionLabel(OffServerDiscordLink.Lang, value);
	}
}

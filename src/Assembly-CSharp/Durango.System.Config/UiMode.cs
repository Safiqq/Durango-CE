using System;
using L10N;
using UnityEngine;

namespace Durango.System.Config;

public static class UiMode
{
	public const string PC = "PC";

	public const string Mobile = "Mobile";

	private const string PrefKey = "option:ui_mode";

	private static bool? _cachedUsePC;

	private static bool? _pcUiRequested;

	public static bool IsWindows
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

	public static bool PcUiRequested
	{
		get
		{
			if (!_pcUiRequested.HasValue)
			{
				bool value = false;
				try
				{
					if (IsWindows)
					{
						value = string.Equals(PlayerPrefs.GetString("option:ui_mode", "Mobile"), "PC", StringComparison.OrdinalIgnoreCase);
						bool flag = false;
						string[] commandLineArgs = global::System.Environment.GetCommandLineArgs();
						foreach (string a in commandLineArgs)
						{
							if (string.Equals(a, "-pcui", StringComparison.OrdinalIgnoreCase))
							{
								value = true;
							}
							else if (string.Equals(a, "-mobileui", StringComparison.OrdinalIgnoreCase))
							{
								flag = true;
							}
						}
						if (flag)
						{
							value = false;
						}
					}
				}
				catch (Exception)
				{
				}
				_pcUiRequested = value;
				_cachedUsePC = value;
			}
			return _pcUiRequested.Value;
		}
	}

	public static bool UsePCUI
	{
		get
		{
			if (PcUiRequested)
			{
				return GameManager.IsMainScene;
			}
			return false;
		}
	}

	public static string Normalize(string value)
	{
		if (!string.Equals(value, "Mobile", StringComparison.OrdinalIgnoreCase))
		{
			return "PC";
		}
		return "Mobile";
	}

	public static void ApplyChanged(string value)
	{
		bool flag = !string.Equals(Normalize(value), "Mobile", StringComparison.OrdinalIgnoreCase);
		if (_cachedUsePC.HasValue && _cachedUsePC.Value == flag)
		{
			return;
		}
		try
		{
			PlayerPrefs.Save();
		}
		catch (Exception)
		{
		}
		string text = (flag ? "PC" : "Mobile");
		try
		{
			UIManager.MessageBox.Show(T._("เปล\u0e35\u0e48ยนโหมด UI"), T._("เปล\u0e35\u0e48ยนเป\u0e47นโหมด <em>{0}</em> แล\u0e49ว\nกร\u0e38ณาป\u0e34ดเกมแล\u0e49วเป\u0e34ดใหม\u0e48เพ\u0e37\u0e48อให\u0e49การเปล\u0e35\u0e48ยนแปลงม\u0e35ผล", text), (Action)null, T._("ตกลง"));
		}
		catch (Exception)
		{
		}
	}
}

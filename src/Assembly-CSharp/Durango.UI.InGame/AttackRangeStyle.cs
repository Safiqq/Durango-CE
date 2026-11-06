using Durango.System.Config;
using UnityEngine;

namespace Durango.UI.InGame;

public static class AttackRangeStyle
{
	public const string IntensityKey = "attack_range_intensity";

	public const string WidthKey = "attack_range_width";

	public const string ReduceMotionKey = "attack_range_reduce_motion";

	public const string ColorAssistKey = "attack_range_color_assist";

	public const string QualityKey = "attack_range_quality";

	public static readonly string[] Levels = new string[3] { "low", "normal", "high" };

	public static readonly string[] Widths = new string[3] { "thin", "normal", "thick" };

	public static float Intensity = 1f;

	public static float Width = 1f;

	public static bool ReduceMotion;

	public static bool ColorAssist;

	public static string Quality = "normal";

	private static float _readAfter = -1f;

	public static float Level(string value)
	{
		if (!(value == "low"))
		{
			if (!(value == "high"))
			{
				return 1f;
			}
			return 1.3f;
		}
		return 0.65f;
	}

	public static float LineWidth(string value)
	{
		if (!(value == "thin"))
		{
			if (!(value == "thick"))
			{
				return 1f;
			}
			return 1.4f;
		}
		return 0.7f;
	}

	public static string Phase(float now, float contactAt, float until)
	{
		if (!(now > until))
		{
			if (!(now >= contactAt))
			{
				return "warning";
			}
			return "active";
		}
		return "fade";
	}

	public static float UpdateInterval(int active)
	{
		if (!(Quality == "low") && active <= 32)
		{
			if (!(Quality == "high") || active > 12)
			{
				return 1f / 30f;
			}
			return 1f / 45f;
		}
		return 1f / 15f;
	}

	public static void Refresh(float now)
	{
		if (!(now < _readAfter) && ConfigInstance.Settings != null)
		{
			_readAfter = now + 0.1f;
			Intensity = Level(ConfigInstance.GetValue("attack_range_intensity", "normal"));
			Width = LineWidth(ConfigInstance.GetValue("attack_range_width", "normal"));
			ReduceMotion = ConfigInstance.GetValue("attack_range_reduce_motion", defaultValue: false);
			ColorAssist = ConfigInstance.GetValue("attack_range_color_assist", defaultValue: false);
			string value = ConfigInstance.GetValue("attack_range_quality", "normal");
			Quality = ((value == "low" || value == "high") ? value : "normal");
		}
	}

	public static Color Palette(Color native, bool enemy)
	{
		if (!ColorAssist)
		{
			return native;
		}
		Color result = Color.Lerp(native, enemy ? Color.yellow : Color.cyan, 0.75f);
		result.a = native.a;
		return result;
	}
}

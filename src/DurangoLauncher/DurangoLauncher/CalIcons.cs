using System;
using System.Linq;

namespace DurangoLauncher;

public static class CalIcons
{
	public static readonly string[] Known = new string[6] { "Icon_battle_emblem", "icon_mainhud_shop", "icon_mainhud_pet", "icon_mainhud_pvpisland", "icon_mainhud_warprush", "act_Craft" };

	public static string? Resource(string? icon)
	{
		if (string.IsNullOrWhiteSpace(icon))
		{
			return null;
		}
		string text = Known.FirstOrDefault((string n) => n.Equals(icon.Trim(), StringComparison.OrdinalIgnoreCase));
		if (text != null)
		{
			return "cal_" + text + ".png";
		}
		return null;
	}
}

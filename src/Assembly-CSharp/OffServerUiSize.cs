using Durango.Logic.Clusters;

public static class OffServerUiSize
{
	public const string SettingKey = "ui_size";

	public const string VeryLarge = "1000";

	public const string Large = "1140";

	public const string Normal = "1280";

	public const string Small = "1420";

	public const string VerySmall = "1600";

	public static string OptionLabel(string value)
	{
		string lang = OffServerDiscordLink.Lang;
		return value switch
		{
			"1000" => Pick(lang, "ใหญ\u0e48มาก", "Very large", "Sangat besar", "매우 크게"),
			"1140" => Pick(lang, "ใหญ\u0e48", "Large", "Besar", "크게"),
			"1280" => Pick(lang, "ปกต\u0e34", "Normal", "Normal", "보통"),
			"1420" => Pick(lang, "เล\u0e47ก", "Small", "Kecil", "작게"),
			"1600" => Pick(lang, "เล\u0e47กมาก", "Very small", "Sangat kecil", "매우 작게"),
			_ => null,
		};
	}

	private static string Pick(string lang, string th, string en, string id, string ko)
	{
		return lang switch
		{
			"th" => th,
			"id" => id,
			"ko" => ko,
			_ => en,
		};
	}
}

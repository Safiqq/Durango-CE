namespace DurangoLauncher;

public static class LoginLayout
{
	public const int TitleH = 30;

	public const int LabelH = 19;

	public const int LabelGap = 6;

	public const int RowH = 22;

	public const int ButtonH = 46;

	public const int OrLineH = 20;

	public const int HintH = 18;

	public const int AltH = 22;

	public const int NoteMinGap = 36;

	public static readonly LoginSpacing[] Levels = new LoginSpacing[4]
	{
		new LoginSpacing(0, 16, 12, 16, 10, 12, 8, 16, 44, Sub: true),
		new LoginSpacing(1, 10, 6, 10, 6, 6, 4, 10, 44, Sub: true),
		new LoginSpacing(2, 10, 6, 10, 4, 4, 4, 8, 40, Sub: true),
		new LoginSpacing(3, 10, 6, 10, 4, 4, 4, 8, 40, Sub: false)
	};

	public static int Height(LoginSpacing sp, int subH, int bannerH, bool discord, int noteH = 0)
	{
		int num = 30;
		if (sp.Sub)
		{
			num += 4 + subH + sp.SubGap;
		}
		num += bannerH;
		num += 2 * (25 + sp.BoxH + sp.FieldGap);
		num += 24 + sp.RowGap;
		num += 46;
		if (discord)
		{
			num += sp.OrTop + 20 + sp.OrGap + 46 + sp.HintGap + 18;
		}
		num += sp.AltGap + 22;
		if (!discord && noteH > 0)
		{
			num += 36 + noteH - 22;
		}
		return num;
	}

	public static LoginSpacing Pick(int avail, int subH, int bannerH, bool discord, int noteH = 0)
	{
		LoginSpacing[] levels = Levels;
		foreach (LoginSpacing loginSpacing in levels)
		{
			if (Height(loginSpacing, subH, bannerH, discord, noteH) <= avail)
			{
				return loginSpacing;
			}
		}
		return Levels[^1];
	}
}

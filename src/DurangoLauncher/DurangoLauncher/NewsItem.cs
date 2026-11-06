namespace DurangoLauncher;

public sealed record NewsItem(string Date, string TitleTh, string BodyTh, string TitleEn = "", string BodyEn = "", string Tag = "")
{
	public string Title
	{
		get
		{
			if (!L.En || TitleEn.Length <= 0)
			{
				return TitleTh;
			}
			return TitleEn;
		}
	}

	public string Body
	{
		get
		{
			if (!L.En || BodyEn.Length <= 0)
			{
				return BodyTh;
			}
			return BodyEn;
		}
	}
}

using System;

namespace DurangoLauncher;

public static class SortChip
{
	public static readonly string[] Sorts = new string[4] { "price_asc", "price_desc", "time", "level" };

	public static string Text(string sort)
	{
		return sort switch
		{
			"price_desc" => L.T("ราคาส\u0e39ง → ต\u0e48ำ", "Price high → low"), 
			"time" => L.T("ใกล\u0e49หมดเวลา", "Ending soon"), 
			"level" => L.T("เลเวล", "Level"), 
			_ => L.T("ราคาต\u0e48ำ → ส\u0e39ง", "Price low → high"), 
		};
	}

	public static string Next(string sort)
	{
		return Sorts[(Array.IndexOf(Sorts, sort) + 1 + Sorts.Length) % Sorts.Length];
	}
}

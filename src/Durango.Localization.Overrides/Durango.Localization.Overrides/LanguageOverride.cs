using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Durango.Localization.Overrides;

public static class LanguageOverride
{
	public static byte[] GetOfflineCatalogBytes(string locale)
	{
		try
		{
			string path = Path.GetDirectoryName(Application.dataPath) ?? ".";
			string path2 = Path.Combine(Path.Combine(Path.Combine(path, "Languages"), locale), "messages.po");
			if (File.Exists(path2))
			{
				string poText = File.ReadAllText(path2, Encoding.UTF8);
				List<PoEntry> entries = PoParser.Parse(poText);
				return MoCompiler.Compile(entries);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("LanguageOverride: failed to load override for locale '" + locale + "': " + ex);
		}
		TextAsset textAsset = Resources.Load("offline/i18n/" + locale) as TextAsset;
		return (textAsset != null) ? textAsset.bytes : null;
	}
}

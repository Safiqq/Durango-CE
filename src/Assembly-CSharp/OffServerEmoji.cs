using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using UnityEngine;

public static class OffServerEmoji
{
	public const string Caps = "nametag-emoji, item-link, server-popup, voice-chat, i18n-keys, pet-scale, pvp-ranked, pvp-duel, gather-check, rift-coin, rift-scope";

	public const string CapsHeader = "X-OffServer-Caps";

	public const float Scale = 1f;

	private const string AtlasPath = "OffServerEmoji/twemoji_atlas";

	private const string IndexPath = "OffServerEmoji/twemoji_atlas_index";

	private static Dictionary<long, int> _cells;

	private static Rect[] _uv;

	private static Texture2D _texture;

	private static bool _loaded;

	public const string ShowNameTagsKey = "show_name_flags";

	private static readonly int[] NameTagToggleFromVersion = new int[3] { 3, 1, 7 };

	private const string NameTagTogglePrefKey = "OffServerEmoji.NameTagToggle";

	private static bool _hideNameTags;

	private static int _toggleSupported = -1;

	public static bool HideNameTags => _hideNameTags;

	public static bool NameTagToggleSupported
	{
		get
		{
			if (_toggleSupported < 0)
			{
				_toggleSupported = (ResolveNameTagToggleGate(out var why) ? 1 : 0);
				OffServerLog.Info("[Emoji] name tag switch " + ((_toggleSupported == 1) ? "on" : "off") + " (" + why + ")");
			}
			return _toggleSupported == 1;
		}
	}

	public static bool Available
	{
		get
		{
			Load();
			return _texture != null;
		}
	}

	public static Texture2D Texture
	{
		get
		{
			Load();
			return _texture;
		}
	}

	public static bool SetShowNameTags(bool show)
	{
		bool flag = !show && NameTagToggleSupported;
		if (flag == _hideNameTags)
		{
			return false;
		}
		_hideNameTags = flag;
		return true;
	}

	private static bool ResolveNameTagToggleGate(out string why)
	{
		try
		{
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			foreach (string a in commandLineArgs)
			{
				if (string.Equals(a, "-flagtoggle", StringComparison.OrdinalIgnoreCase))
				{
					why = "-flagtoggle";
					return true;
				}
				if (string.Equals(a, "-noflagtoggle", StringComparison.OrdinalIgnoreCase))
				{
					why = "-noflagtoggle";
					return false;
				}
			}
		}
		catch
		{
		}
		string clientVersion = OffServerLink.ClientVersion;
		why = "client " + clientVersion;
		return VersionAtLeast(clientVersion, NameTagToggleFromVersion);
	}

	private static bool VersionAtLeast(string version, int[] min)
	{
		if (string.IsNullOrEmpty(version))
		{
			return false;
		}
		string[] array = version.Trim().Split('.');
		for (int i = 0; i < min.Length; i++)
		{
			int result = 0;
			if (i < array.Length)
			{
				string text = array[i];
				int j;
				for (j = 0; j < text.Length && char.IsDigit(text[j]); j++)
				{
				}
				if (j == 0 || !int.TryParse(text.Substring(0, j), out result))
				{
					return false;
				}
			}
			if (result != min[i])
			{
				return result > min[i];
			}
		}
		return true;
	}

	private static bool IsRegionalIndicator(int cp)
	{
		if (cp >= 127462)
		{
			return cp <= 127487;
		}
		return false;
	}

	private static int CodePointAt(string text, int index)
	{
		if (index + 1 < text.Length && char.IsHighSurrogate(text[index]) && char.IsLowSurrogate(text[index + 1]))
		{
			return char.ConvertToUtf32(text[index], text[index + 1]);
		}
		return -1;
	}

	public static int Match(string text, int index, out int cell)
	{
		cell = -1;
		int num = CodePointAt(text, index);
		if (num < 0)
		{
			return 1;
		}
		int i = 2;
		long num2 = num;
		bool flag = num == 128241 || num == 128187;
		if (IsRegionalIndicator(num))
		{
			int num3 = CodePointAt(text, index + 2);
			if (IsRegionalIndicator(num3))
			{
				num2 = ((long)num << 21) | (uint)num3;
				i = 4;
				flag = true;
			}
			else
			{
				num2 = -1L;
			}
		}
		for (; index + i < text.Length && (text[index + i] == '\ufe0f' || text[index + i] == '\ufe0e'); i++)
		{
		}
		if (flag && _hideNameTags)
		{
			if (index + i < text.Length && text[index + i] == ' ')
			{
				i++;
			}
			return i;
		}
		if (num2 >= 0 && Available && _cells.TryGetValue(num2, out var value))
		{
			cell = value;
		}
		return i;
	}

	public static string StripNameTags(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}
		int num = 0;
		while (num < name.Length)
		{
			char c = name[num];
			if (c == ' ' || c == '\ufe0f')
			{
				num++;
				continue;
			}
			int num2 = CodePointAt(name, num);
			if (num2 == 128241 || num2 == 128187 || IsRegionalIndicator(num2))
			{
				num += 2;
				continue;
			}
			if (c == '[' && num + 2 < name.Length && name[num + 1] == 'M' && name[num + 2] == ']')
			{
				num += 3;
				continue;
			}
			if (c != '[' || num + 3 >= name.Length || !char.IsUpper(name[num + 1]) || !char.IsUpper(name[num + 2]) || name[num + 3] != ']' || name[num + 1] >= '\u0080' || name[num + 2] >= '\u0080')
			{
				break;
			}
			num += 4;
		}
		if (num != 0)
		{
			return name.Substring(num);
		}
		return name;
	}

	public static Rect GetUV(int cell)
	{
		Load();
		if (_uv == null || cell < 0 || cell >= _uv.Length)
		{
			return new Rect(0f, 0f, 0f, 0f);
		}
		return _uv[cell];
	}

	private static void Load()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		try
		{
			TextAsset textAsset = Resources.Load<TextAsset>("OffServerEmoji/twemoji_atlas_index");
			TextAsset textAsset2 = Resources.Load<TextAsset>("OffServerEmoji/twemoji_atlas");
			if (textAsset == null || textAsset2 == null)
			{
				OffServerLog.Info("[Emoji] Twemoji atlas missing in Resources/OffServerEmoji - emoji are not drawn");
				return;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			Dictionary<long, int> dictionary = new Dictionary<long, int>();
			string[] array = textAsset.text.Split('\n');
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length == 0 || text[0] == '#')
				{
					continue;
				}
				if (text.StartsWith("size "))
				{
					string[] array2 = text.Split(' ');
					num = int.Parse(array2[1]);
					num2 = int.Parse(array2[2]);
					num3 = int.Parse(array2[4]);
					num4 = int.Parse(array2[6]);
					num5 = int.Parse(array2[8]);
					num6 = int.Parse(array2[10]);
					continue;
				}
				string[] array3 = text.Split('-');
				long num7 = Convert.ToInt32(array3[0], 16);
				if (array3.Length == 2)
				{
					num7 = (num7 << 21) | (uint)Convert.ToInt32(array3[1], 16);
				}
				else if (array3.Length != 1)
				{
					continue;
				}
				dictionary[num7] = dictionary.Count;
			}
			if (num <= 0 || num2 <= 0 || num6 <= 0 || dictionary.Count == 0)
			{
				OffServerLog.Info("[Emoji] bad twemoji_atlas_index header - emoji are not drawn");
				return;
			}
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			if (!texture2D.LoadImage(textAsset2.bytes) || texture2D.width != num || texture2D.height != num2)
			{
				OffServerLog.Info("[Emoji] twemoji_atlas.bytes does not decode to " + num + "x" + num2 + " - emoji are not drawn");
				UnityEngine.Object.Destroy(texture2D);
				return;
			}
			Texture2D texture2D2 = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: true);
			texture2D2.name = "OffServerEmoji";
			texture2D2.wrapMode = TextureWrapMode.Clamp;
			texture2D2.filterMode = FilterMode.Trilinear;
			texture2D2.SetPixels32(texture2D.GetPixels32());
			texture2D2.Apply(updateMipmaps: true, makeNoLongerReadable: true);
			texture2D2.hideFlags = HideFlags.DontUnloadUnusedAsset;
			UnityEngine.Object.Destroy(texture2D);
			Resources.UnloadAsset(textAsset2);
			Rect[] array4 = new Rect[dictionary.Count];
			for (int j = 0; j < array4.Length; j++)
			{
				int num8 = j % num6 * num3 + num5;
				int num9 = j / num6 * num3 + num5;
				array4[j] = new Rect((float)num8 / (float)num, 1f - (float)(num9 + num4) / (float)num2, (float)num4 / (float)num, (float)num4 / (float)num2);
			}
			_cells = dictionary;
			_uv = array4;
			_texture = texture2D2;
			OffServerLog.Info("[Emoji] Twemoji atlas loaded: " + dictionary.Count + " emoji " + num + "x" + num2);
		}
		catch (Exception ex)
		{
			OffServerLog.Info("[Emoji] Twemoji atlas load failed: " + ex.Message);
			_texture = null;
		}
	}
}

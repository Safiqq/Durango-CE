using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Durango.Logic.Clusters;
using UnityEngine;

public static class OffServerI18n
{
	public struct PackInfo
	{
		public string Locale;

		public string Name;

		public string Version;

		public bool HasClient;

		public bool HasServer;
	}

	public sealed class PoEntry
	{
		public string Context;

		public string MsgId = "";

		public string MsgStr = "";

		public bool Fuzzy;

		public string Key
		{
			get
			{
				if (Context != null)
				{
					return Context + "\u0004" + MsgId;
				}
				return MsgId;
			}
		}
	}

	public static class Po
	{
		public static List<PoEntry> Parse(string text)
		{
			List<PoEntry> list = new List<PoEntry>();
			if (string.IsNullOrEmpty(text))
			{
				return list;
			}
			if (text[0] == '\ufeff')
			{
				text = text.Substring(1);
			}
			PoEntry cur = new PoEntry();
			string field = null;
			StringBuilder sb = new StringBuilder();
			bool any = false;
			Action flushField = delegate
			{
				if (field != null)
				{
					string text4 = sb.ToString();
					if (field == "msgctxt")
					{
						cur.Context = text4;
					}
					else if (field == "msgid")
					{
						cur.MsgId = text4;
					}
					else if (field == "msgstr" || field == "msgstr[0]")
					{
						cur.MsgStr = text4;
					}
					field = null;
					sb.Length = 0;
				}
			};
			Action action = delegate
			{
				flushField();
				if (any && (cur.MsgId.Length > 0 || cur.Context != null))
				{
					list.Add(cur);
				}
				cur = new PoEntry();
				any = false;
			};
			string[] array = text.Replace("\r\n", "\n").Split('\n');
			for (int num = 0; num < array.Length; num++)
			{
				string text2 = array[num].Trim();
				if (text2.Length == 0)
				{
					if (field != null || any)
					{
						action();
					}
					continue;
				}
				if (text2[0] == '#')
				{
					if (field != null && field.StartsWith("msgstr"))
					{
						action();
					}
					if (text2.StartsWith("#,") && text2.Contains("fuzzy"))
					{
						cur.Fuzzy = true;
					}
					continue;
				}
				if (text2[0] == '"')
				{
					if (field != null)
					{
						sb.Append(Unquote(text2));
					}
					continue;
				}
				int num2 = text2.IndexOf(' ');
				if (num2 >= 0)
				{
					string text3 = text2.Substring(0, num2);
					if ((text3 == "msgctxt" || (text3 == "msgid" && field != "msgctxt")) && field != null && field.StartsWith("msgstr"))
					{
						action();
					}
					flushField();
					field = text3;
					any = true;
					sb.Append(Unquote(text2.Substring(num2 + 1)));
				}
			}
			action();
			return list;
		}

		private static string Unquote(string q)
		{
			q = q.Trim();
			if (q.Length < 2 || q[0] != '"' || q[q.Length - 1] != '"')
			{
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder(q.Length);
			for (int i = 1; i < q.Length - 1; i++)
			{
				char c = q[i];
				if (c == '\\' && i + 1 < q.Length - 1)
				{
					char c2 = q[++i];
					stringBuilder.Append(c2 switch
					{
						'r' => '\r',
						't' => '\t',
						'n' => '\n',
						_ => c2,
					});
				}
				else
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}
	}

	public static class Mo
	{
		public static List<KeyValuePair<string, string>> Read(byte[] b)
		{
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			if (BitConverter.ToUInt32(b, 0) != 2500072158u)
			{
				throw new InvalidDataException("not a little-endian .mo");
			}
			int num = BitConverter.ToInt32(b, 8);
			int num2 = BitConverter.ToInt32(b, 12);
			int num3 = BitConverter.ToInt32(b, 16);
			for (int i = 0; i < num; i++)
			{
				int count = BitConverter.ToInt32(b, num2 + 8 * i);
				int index = BitConverter.ToInt32(b, num2 + 8 * i + 4);
				int count2 = BitConverter.ToInt32(b, num3 + 8 * i);
				int index2 = BitConverter.ToInt32(b, num3 + 8 * i + 4);
				list.Add(new KeyValuePair<string, string>(Encoding.UTF8.GetString(b, index, count), Encoding.UTF8.GetString(b, index2, count2)));
			}
			return list;
		}

		public static byte[] Write(List<KeyValuePair<string, string>> pairs)
		{
			List<byte[]> list = pairs.Select((KeyValuePair<string, string> p) => Encoding.UTF8.GetBytes(p.Key)).ToList();
			List<byte[]> list2 = pairs.Select((KeyValuePair<string, string> p) => Encoding.UTF8.GetBytes(p.Value)).ToList();
			int count = pairs.Count;
			int num = 28;
			int num2 = num + 8 * count;
			int num3 = num2 + 8 * count;
			MemoryStream memoryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(2500072158u);
			binaryWriter.Write(0);
			binaryWriter.Write(count);
			binaryWriter.Write(num);
			binaryWriter.Write(num2);
			binaryWriter.Write(0);
			binaryWriter.Write(0);
			int[] array = new int[count];
			for (int num4 = 0; num4 < count; num4++)
			{
				array[num4] = num3;
				num3 += list[num4].Length + 1;
			}
			int[] array2 = new int[count];
			for (int num5 = 0; num5 < count; num5++)
			{
				array2[num5] = num3;
				num3 += list2[num5].Length + 1;
			}
			for (int num6 = 0; num6 < count; num6++)
			{
				binaryWriter.Write(list[num6].Length);
				binaryWriter.Write(array[num6]);
			}
			for (int num7 = 0; num7 < count; num7++)
			{
				binaryWriter.Write(list2[num7].Length);
				binaryWriter.Write(array2[num7]);
			}
			for (int num8 = 0; num8 < count; num8++)
			{
				binaryWriter.Write(list[num8]);
				binaryWriter.Write((byte)0);
			}
			for (int num9 = 0; num9 < count; num9++)
			{
				binaryWriter.Write(list2[num9]);
				binaryWriter.Write((byte)0);
			}
			binaryWriter.Flush();
			return memoryStream.ToArray();
		}
	}

	public const string Caps = "i18n-keys";

	public const char Open = '\ue000';

	public const char Sep = '\ue001';

	public const char Close = '\ue002';

	public const char Fb = '\ue003';

	public const char CtxSep = '\ue004';

	public const string ServerFile = "server.po";

	public const string ClientFile = "messages.po";

	public const string MetaFile = "language.json";

	private static Dictionary<string, string> _server = new Dictionary<string, string>(StringComparer.Ordinal);

	private static readonly UTF8Encoding Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	public static string LanguagesDir => Path.Combine(OffServerLink.WritableRoot ?? ".", "Languages");

	public static byte[] BuiltInCatalog(string locale)
	{
		try
		{
			TextAsset textAsset = Resources.Load<TextAsset>("offline/i18n/" + locale);
			return (textAsset != null) ? textAsset.bytes : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static string SafeCultureLocale(string locale)
	{
		try
		{
			new CultureInfo((locale ?? "").Replace('_', '-'));
			return locale;
		}
		catch (Exception)
		{
			return "en_US";
		}
	}

	public static string PackDir(string locale)
	{
		return Path.Combine(LanguagesDir, locale);
	}

	public static bool IsSafeLocale(string locale)
	{
		if (string.IsNullOrEmpty(locale) || locale.Length > 16)
		{
			return false;
		}
		foreach (char c in locale)
		{
			if ((!char.IsLetterOrDigit(c) || c >= '\u0080') && c != '_')
			{
				return false;
			}
		}
		return true;
	}

	public static List<PackInfo> DiscoverPacks()
	{
		List<PackInfo> list = new List<PackInfo>();
		try
		{
			string languagesDir = LanguagesDir;
			if (!Directory.Exists(languagesDir))
			{
				return list;
			}
			string[] directories = Directory.GetDirectories(languagesDir);
			foreach (string text in directories)
			{
				string fileName = Path.GetFileName(text);
				if (IsSafeLocale(fileName) && !fileName.StartsWith("_"))
				{
					bool flag = File.Exists(Path.Combine(text, "messages.po"));
					bool flag2 = File.Exists(Path.Combine(text, "server.po"));
					if (flag || flag2)
					{
						PackInfo info = new PackInfo
						{
							Locale = fileName,
							Name = fileName,
							Version = "",
							HasClient = flag,
							HasServer = flag2
						};
						ReadMeta(text, ref info);
						list.Add(info);
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	private static void ReadMeta(string dir, ref PackInfo info)
	{
		try
		{
			string path = Path.Combine(dir, "language.json");
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				string text = JsonField(json, "name");
				if (!string.IsNullOrEmpty(text))
				{
					info.Name = text;
				}
				info.Version = JsonField(json, "version") ?? "";
				return;
			}
			string[] array = new string[2] { "messages.po", "server.po" };
			foreach (string path2 in array)
			{
				string path3 = Path.Combine(dir, path2);
				if (!File.Exists(path3))
				{
					continue;
				}
				string text2 = ReadHead(path3, 4096);
				int num = text2.IndexOf("X-Language-Name:", StringComparison.OrdinalIgnoreCase);
				if (num >= 0)
				{
					int num2 = text2.IndexOf("\\n", num, StringComparison.Ordinal);
					string text3 = text2.Substring(num + 16, ((num2 < 0) ? text2.Length : num2) - (num + 16)).Trim();
					if (text3.Length > 0)
					{
						info.Name = text3;
						break;
					}
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static string ReadHead(string path, int n)
	{
		using FileStream fileStream = File.OpenRead(path);
		byte[] array = new byte[Math.Min(n, (int)fileStream.Length)];
		int count = fileStream.Read(array, 0, array.Length);
		return Encoding.UTF8.GetString(array, 0, count);
	}

	public static string JsonField(string json, string key)
	{
		if (json == null)
		{
			return null;
		}
		string text = "\"" + key + "\"";
		int num = json.IndexOf(text, StringComparison.Ordinal);
		if (num < 0)
		{
			return null;
		}
		num = json.IndexOf(':', num + text.Length);
		if (num < 0)
		{
			return null;
		}
		for (num++; num < json.Length && char.IsWhiteSpace(json[num]); num++)
		{
		}
		if (num >= json.Length)
		{
			return null;
		}
		if (json[num] != '"')
		{
			int i;
			for (i = num; i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']'; i++)
			{
			}
			return json.Substring(num, i - num).Trim();
		}
		StringBuilder stringBuilder = new StringBuilder();
		for (num++; num < json.Length; num++)
		{
			char c = json[num];
			if (c == '"')
			{
				break;
			}
			if (c == '\\' && num + 1 < json.Length)
			{
				char c2 = json[++num];
				switch (c2)
				{
				case 'n':
					stringBuilder.Append('\n');
					continue;
				case 't':
					stringBuilder.Append('\t');
					continue;
				case 'u':
					if (num + 4 < json.Length)
					{
						stringBuilder.Append((char)Convert.ToInt32(json.Substring(num + 1, 4), 16));
						num += 4;
						continue;
					}
					break;
				}
				stringBuilder.Append(c2);
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	public static void LoadServerCatalog(string locale)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		try
		{
			string path = Path.Combine(PackDir(locale), "server.po");
			if (IsSafeLocale(locale) && File.Exists(path))
			{
				foreach (PoEntry item in Po.Parse(File.ReadAllText(path)))
				{
					if (item.MsgStr.Length > 0 && !item.Fuzzy)
					{
						dictionary[item.Key] = item.MsgStr;
					}
				}
			}
		}
		catch (Exception)
		{
		}
		_server = dictionary;
	}

	public static byte[] MergedClientCatalog(byte[] builtIn, string locale)
	{
		string text = (IsSafeLocale(locale) ? Path.Combine(PackDir(locale), "messages.po") : null);
		if (text == null || !File.Exists(text))
		{
			return builtIn;
		}
		try
		{
			List<KeyValuePair<string, string>> list = ((builtIn != null) ? Mo.Read(builtIn) : new List<KeyValuePair<string, string>>());
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < list.Count; i++)
			{
				dictionary[list[i].Key] = i;
			}
			int num = 0;
			int num2 = 0;
			foreach (PoEntry item in Po.Parse(File.ReadAllText(text)))
			{
				if (!item.Fuzzy && item.MsgStr.Length != 0 && item.MsgId.Length != 0)
				{
					if (dictionary.TryGetValue(item.Key, out var value))
					{
						list[value] = new KeyValuePair<string, string>(item.Key, item.MsgStr);
						num2++;
					}
					else
					{
						dictionary[item.Key] = list.Count;
						list.Add(new KeyValuePair<string, string>(item.Key, item.MsgStr));
						num++;
					}
				}
			}
			return Mo.Write(list);
		}
		catch (Exception)
		{
			return builtIn;
		}
	}

	public static bool HasToken(string s)
	{
		if (s != null)
		{
			return s.IndexOf('\ue000') >= 0;
		}
		return false;
	}

	public static string Render(string s)
	{
		if (!HasToken(s))
		{
			return s;
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		int num = 0;
		while (num < s.Length)
		{
			char c = s[num];
			if (c == '\ue000')
			{
				int num2 = MatchClose(s, num);
				if (num2 < 0)
				{
					stringBuilder.Append(Clean(s.Substring(num + 1)));
					break;
				}
				stringBuilder.Append(RenderToken(s, num + 1, num2));
				num = num2 + 1;
			}
			else
			{
				if (c < '\ue000' || c > '\ue004')
				{
					stringBuilder.Append(c);
				}
				num++;
			}
		}
		return stringBuilder.ToString();
	}

	private static int MatchClose(string s, int open)
	{
		int num = 0;
		for (int i = open; i < s.Length; i++)
		{
			if (s[i] == '\ue000')
			{
				num++;
			}
			else if (s[i] == '\ue002' && --num == 0)
			{
				return i;
			}
		}
		return -1;
	}

	private static string Clean(string s)
	{
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		foreach (char c in s)
		{
			if (c < '\ue000' || c > '\ue004')
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private static string RenderToken(string s, int from, int to)
	{
		List<string> list = new List<string>();
		string text = null;
		int num = 0;
		int num2 = from;
		bool flag = false;
		for (int i = from; i < to; i++)
		{
			char c = s[i];
			switch (c)
			{
			case '\ue000':
				num++;
				continue;
			case '\ue002':
				num--;
				continue;
			}
			if (num == 0 && (c == '\ue001' || c == '\ue003') && !flag)
			{
				list.Add(s.Substring(num2, i - num2));
				num2 = i + 1;
				if (c == '\ue003')
				{
					flag = true;
				}
			}
		}
		if (flag)
		{
			text = s.Substring(num2, to - num2);
		}
		else
		{
			list.Add(s.Substring(num2, to - num2));
		}
		string text2 = list[0];
		string key = text2.Replace('\ue004', '\u0004');
		string text3 = text2;
		int num3 = text2.IndexOf('\ue004');
		if (num3 >= 0)
		{
			text3 = text2.Substring(num3 + 1);
		}
		if (_server.TryGetValue(key, out var value))
		{
			if (list.Count == 1)
			{
				return value;
			}
			string[] array = new string[list.Count - 1];
			for (int j = 1; j < list.Count; j++)
			{
				array[j - 1] = Render(list[j]);
			}
			return FormatArgs(value, array, text3);
		}
		if (text != null)
		{
			return text;
		}
		if (list.Count == 1)
		{
			return text3;
		}
		string[] array2 = new string[list.Count - 1];
		for (int k = 1; k < list.Count; k++)
		{
			array2[k - 1] = Render(list[k]);
		}
		return FormatArgs(text3, array2, null);
	}

	public static string FormatArgs(string template, string[] args, string msgidFallback)
	{
		StringBuilder stringBuilder = new StringBuilder(template.Length + 32);
		for (int i = 0; i < template.Length; i++)
		{
			char c = template[i];
			switch (c)
			{
			case '{':
			{
				if (i + 1 < template.Length && template[i + 1] == '{')
				{
					stringBuilder.Append('{');
					i++;
					continue;
				}
				int num = i + 1;
				int num2 = 0;
				bool flag = false;
				while (num < template.Length && template[num] >= '0' && template[num] <= '9')
				{
					num2 = num2 * 10 + (template[num] - 48);
					num++;
					flag = true;
				}
				int num3 = template.IndexOf('}', num);
				if (!flag || num3 < 0 || (num < template.Length && template[num] != '}' && template[num] != ',' && template[num] != ':'))
				{
					if (msgidFallback != null && msgidFallback != template)
					{
						return FormatArgs(msgidFallback, args, null);
					}
					stringBuilder.Append(c);
				}
				else
				{
					stringBuilder.Append((num2 < args.Length) ? args[num2] : template.Substring(i, num3 - i + 1));
					i = num3;
				}
				continue;
			}
			case '}':
				if (i + 1 < template.Length && template[i + 1] == '}')
				{
					stringBuilder.Append('}');
					i++;
					continue;
				}
				break;
			}
			stringBuilder.Append(c);
		}
		return stringBuilder.ToString();
	}

	public static void Localize(ref byte[] buf, ref int count)
	{
		try
		{
			if (HasMarker(buf, count))
			{
				byte[] array = Transcode(buf, count);
				if (array != null)
				{
					buf = array;
					count = array.Length;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool HasMarker(byte[] b, int count)
	{
		for (int i = 0; i + 2 < count; i++)
		{
			if (b[i] == 238 && b[i + 1] == 128 && b[i + 2] == 128)
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasMarkerAt(byte[] b, int off, int len)
	{
		for (int i = off; i + 2 < off + len; i++)
		{
			if (b[i] == 238 && b[i + 1] == 128 && b[i + 2] == 128)
			{
				return true;
			}
		}
		return false;
	}

	private static byte[] Transcode(byte[] p, int count)
	{
		MemoryStream memoryStream = new MemoryStream(count + 64);
		int num = 0;
		while (num < count)
		{
			byte b = p[num];
			int num2 = -1;
			int num3 = 0;
			if (b >= 160 && b <= 191)
			{
				num2 = b & 0x1F;
				num3 = 1;
			}
			else
			{
				switch (b)
				{
				case 217:
					if (num + 2 > count)
					{
						return null;
					}
					num2 = p[num + 1];
					num3 = 2;
					break;
				case 218:
					if (num + 3 > count)
					{
						return null;
					}
					num2 = (p[num + 1] << 8) | p[num + 2];
					num3 = 3;
					break;
				case 219:
					if (num + 5 > count)
					{
						return null;
					}
					num2 = (p[num + 1] << 24) | (p[num + 2] << 16) | (p[num + 3] << 8) | p[num + 4];
					num3 = 5;
					break;
				}
			}
			if (num2 >= 0)
			{
				if (num2 < 0 || num + num3 + num2 > count)
				{
					return null;
				}
				string text = null;
				if (HasMarkerAt(p, num + num3, num2))
				{
					try
					{
						text = Strict.GetString(p, num + num3, num2);
					}
					catch (Exception)
					{
						text = null;
					}
				}
				if (text != null && text.IndexOf('\ue000') >= 0 && text.IndexOf('\ue002') > text.IndexOf('\ue000'))
				{
					WriteStr(memoryStream, Encoding.UTF8.GetBytes(Render(text)));
				}
				else
				{
					memoryStream.Write(p, num, num3 + num2);
				}
				num += num3 + num2;
			}
			else
			{
				int num4 = SizeOf(p, num, count);
				if (num4 <= 0 || num + num4 > count)
				{
					return null;
				}
				memoryStream.Write(p, num, num4);
				num += num4;
			}
		}
		return memoryStream.ToArray();
	}

	private static int SizeOf(byte[] p, int i, int count)
	{
		byte b = p[i];
		if (b <= 127 || b >= 224)
		{
			return 1;
		}
		if (b >= 128 && b <= 159)
		{
			return 1;
		}
		int num = count - i;
		switch (b)
		{
		case 192:
		case 194:
		case 195:
			return 1;
		case 196:
			if (num >= 2)
			{
				return 2 + p[i + 1];
			}
			return -1;
		case 197:
			if (num >= 3)
			{
				return 3 + ((p[i + 1] << 8) | p[i + 2]);
			}
			return -1;
		case 198:
			if (num >= 5)
			{
				return 5 + ((p[i + 1] << 24) | (p[i + 2] << 16) | (p[i + 3] << 8) | p[i + 4]);
			}
			return -1;
		case 199:
			if (num >= 3)
			{
				return 3 + p[i + 1];
			}
			return -1;
		case 200:
			if (num >= 4)
			{
				return 4 + ((p[i + 1] << 8) | p[i + 2]);
			}
			return -1;
		case 201:
			if (num >= 6)
			{
				return 6 + ((p[i + 1] << 24) | (p[i + 2] << 16) | (p[i + 3] << 8) | p[i + 4]);
			}
			return -1;
		case 202:
			return 5;
		case 203:
			return 9;
		case 204:
			return 2;
		case 205:
			return 3;
		case 206:
			return 5;
		case 207:
			return 9;
		case 208:
			return 2;
		case 209:
			return 3;
		case 210:
			return 5;
		case 211:
			return 9;
		case 212:
			return 3;
		case 213:
			return 4;
		case 214:
			return 6;
		case 215:
			return 10;
		case 216:
			return 18;
		case 220:
		case 222:
			return 3;
		case 221:
		case 223:
			return 5;
		default:
			return -1;
		}
	}

	private static void WriteStr(MemoryStream w, byte[] utf8)
	{
		int num = utf8.Length;
		if (num < 32)
		{
			w.WriteByte((byte)(0xA0 | num));
		}
		else if (num <= 65535)
		{
			w.WriteByte(218);
			w.WriteByte((byte)(num >> 8));
			w.WriteByte((byte)num);
		}
		else
		{
			w.WriteByte(219);
			w.WriteByte((byte)(num >> 24));
			w.WriteByte((byte)(num >> 16));
			w.WriteByte((byte)(num >> 8));
			w.WriteByte((byte)num);
		}
		w.Write(utf8, 0, num);
	}
}

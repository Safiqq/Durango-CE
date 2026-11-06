using System;
using System.Collections.Generic;
using System.Text;

namespace Durango.Localization.Overrides;

public static class PoParser
{
	public static List<PoEntry> Parse(string poText)
	{
		List<PoEntry> list = new List<PoEntry>();
		string[] array = poText.Replace("\r\n", "\n").Split(new char[1] { '\n' });
		int i = 0;
		PoEntry poEntry = null;
		while (i < array.Length)
		{
			string text = array[i].TrimEnd(new char[0]);
			i++;
			if (text.Length == 0 || text.StartsWith("#"))
			{
				continue;
			}
			if (text.StartsWith("msgctxt "))
			{
				Flush(list, poEntry);
				poEntry = new PoEntry();
				poEntry.Context = ReadQuoted(text.Substring("msgctxt ".Length), array, ref i);
			}
			else if (text.StartsWith("msgid_plural "))
			{
				if (poEntry == null)
				{
					poEntry = new PoEntry();
				}
				poEntry.MsgIdPlural = ReadQuoted(text.Substring("msgid_plural ".Length), array, ref i);
			}
			else if (text.StartsWith("msgid "))
			{
				if (poEntry == null || poEntry.MsgId.Length > 0)
				{
					Flush(list, poEntry);
					poEntry = new PoEntry();
				}
				poEntry.MsgId = ReadQuoted(text.Substring("msgid ".Length), array, ref i);
			}
			else if (text.StartsWith("msgstr["))
			{
				if (poEntry == null)
				{
					poEntry = new PoEntry();
				}
				int num = text.IndexOf(']');
				int num2 = int.Parse(text.Substring(7, num - 7));
				string value = ReadQuoted(text.Substring(num + 2), array, ref i);
				while (poEntry.MsgStrs.Count <= num2)
				{
					poEntry.MsgStrs.Add("");
				}
				poEntry.MsgStrs[num2] = value;
			}
			else if (text.StartsWith("msgstr "))
			{
				if (poEntry == null)
				{
					poEntry = new PoEntry();
				}
				string text2 = ReadQuoted(text.Substring("msgstr ".Length), array, ref i);
				if (poEntry.MsgStrs.Count == 0)
				{
					poEntry.MsgStrs.Add(text2);
				}
				else
				{
					poEntry.MsgStrs[0] = text2;
				}
			}
		}
		Flush(list, poEntry);
		list.RemoveAll((PoEntry e) => e.MsgId.Length == 0 && e.Context == null);
		return list;
	}

	private static void Flush(List<PoEntry> entries, PoEntry cur)
	{
		if (cur != null && (cur.MsgId.Length > 0 || cur.MsgStrs.Count > 0))
		{
			entries.Add(cur);
		}
	}

	private static string ReadQuoted(string firstPart, string[] lines, ref int i)
	{
		StringBuilder stringBuilder = new StringBuilder();
		AppendQuotedLine(stringBuilder, firstPart.Trim());
		while (i < lines.Length)
		{
			string text = lines[i].Trim();
			if (text.StartsWith("\""))
			{
				AppendQuotedLine(stringBuilder, text);
				i++;
				continue;
			}
			break;
		}
		return stringBuilder.ToString();
	}

	private static void AppendQuotedLine(StringBuilder sb, string quotedLine)
	{
		if (quotedLine.Length < 2 || quotedLine[0] != '"' || quotedLine[quotedLine.Length - 1] != '"')
		{
			throw new FormatException("Malformed .po quoted string: " + quotedLine);
		}
		string text = quotedLine.Substring(1, quotedLine.Length - 2);
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (c == '\\' && i + 1 < text.Length)
			{
				switch (text[i + 1])
				{
				case 'n':
					sb.Append('\n');
					i++;
					break;
				case 't':
					sb.Append('\t');
					i++;
					break;
				case 'r':
					sb.Append('\r');
					i++;
					break;
				case '"':
					sb.Append('"');
					i++;
					break;
				case '\\':
					sb.Append('\\');
					i++;
					break;
				default:
					sb.Append(c);
					break;
				}
			}
			else
			{
				sb.Append(c);
			}
		}
	}
}

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MML;

public struct MMLCommand
{
	public MMLCommandType Type;

	public List<string> Args;

	public static MMLCommand Parse(string token)
	{
		MMLCommand result = default(MMLCommand);
		MMLCommandType mMLCommandType = MMLCommandType.Unknown;
		List<string> list = new List<string>();
		switch (token.ToLowerInvariant()[0])
		{
		case 't':
			mMLCommandType = MMLCommandType.Tempo;
			AddPart(list, token, "\\d{1,3}");
			break;
		case 'l':
			mMLCommandType = MMLCommandType.Length;
			AddPart(list, token, "(16|2|4|8|1|32|64)");
			AddPart(list, token, "\\.");
			break;
		case 'v':
			mMLCommandType = MMLCommandType.Volume;
			AddPart(list, token, "\\d+");
			break;
		case 'o':
			mMLCommandType = MMLCommandType.Octave;
			AddPart(list, token, "\\d");
			break;
		case '<':
			mMLCommandType = MMLCommandType.OctaveDown;
			break;
		case '>':
			mMLCommandType = MMLCommandType.OctaveUp;
			break;
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
		case 'g':
			mMLCommandType = MMLCommandType.Note;
			AddPart(list, token, "[a-gA-G]");
			AddPart(list, token, "(\\+|#|-)");
			AddPart(list, token, "(16|2|4|8|1|32|64)");
			AddPart(list, token, "\\.");
			break;
		case 'r':
			mMLCommandType = MMLCommandType.Rest;
			AddPart(list, token, "(16|2|4|8|1|32|64)");
			AddPart(list, token, "\\.");
			break;
		case 'n':
			mMLCommandType = MMLCommandType.NoteNumber;
			AddPart(list, token, "\\d+");
			AddPart(list, token, "\\.");
			break;
		case '&':
			mMLCommandType = MMLCommandType.Tie;
			break;
		default:
			mMLCommandType = MMLCommandType.Unknown;
			list.Add(token);
			break;
		}
		result.Type = mMLCommandType;
		result.Args = list;
		return result;
	}

	private static void AddPart(List<string> args, string token, string pattern)
	{
		string value = Regex.Match(token, pattern).Value;
		args.Add(value);
	}
}

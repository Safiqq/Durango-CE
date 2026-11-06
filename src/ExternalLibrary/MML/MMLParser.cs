using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MML;

public class MMLParser
{
	public const double Tick = 1.0 / 128.0;

	private const string MMLPatterns = "[tT]\\d{1,3}|[lL](16|2|4|8|1|32|64)\\.?|[vV]\\d+|[oO]\\d|<|>|[a-gA-G](\\+|#|-)?(16|2|4|8|1|32|64)?\\.?|[rR](16|2|4|8|1|32|64)?\\.?|[nN]\\d+\\.?|&";

	public static List<KeyValuePair<double, Note>> ToNotes(string str, out int tempo)
	{
		List<MMLCommand> list = new List<MMLCommand>();
		string input = str.Replace("\n", string.Empty).Replace("\r", string.Empty);
		MatchCollection matchCollection = Regex.Matches(input, "[tT]\\d{1,3}|[lL](16|2|4|8|1|32|64)\\.?|[vV]\\d+|[oO]\\d|<|>|[a-gA-G](\\+|#|-)?(16|2|4|8|1|32|64)?\\.?|[rR](16|2|4|8|1|32|64)?\\.?|[nN]\\d+\\.?|&");
		for (int i = 0; i < matchCollection.Count; i++)
		{
			list.Add(MMLCommand.Parse(matchCollection[i].Value));
		}
		return ToNotes(list, out tempo);
	}

	private static List<KeyValuePair<double, Note>> ToNotes(IEnumerable<MMLCommand> commands, out int tempo)
	{
		List<KeyValuePair<double, Note>> list = new List<KeyValuePair<double, Note>>();
		double num = 0.0;
		MMLLength defaultLength = new MMLLength(4, dotted: false);
		int num2 = 4;
		tempo = 120;
		double num3 = 60.0 / ((double)tempo / 4.0);
		int volume = 8;
		foreach (MMLCommand command in commands)
		{
			double measureLength = 0.0;
			switch (command.Type)
			{
			case MMLCommandType.Note:
			case MMLCommandType.NoteNumber:
			{
				Note note = GetNote(command, out measureLength, defaultLength, volume, num2, num3);
				list.Add(new KeyValuePair<double, Note>(num * num3, note));
				break;
			}
			case MMLCommandType.Rest:
				GetRest(command, out measureLength, defaultLength);
				break;
			case MMLCommandType.Tempo:
				tempo = Convert.ToInt32(command.Args[0]);
				num3 = 60.0 / ((double)tempo / 4.0);
				break;
			case MMLCommandType.Length:
				defaultLength = new MMLLength(Convert.ToInt32(command.Args[0]), command.Args[1] != string.Empty);
				break;
			case MMLCommandType.Volume:
				volume = Convert.ToInt32(command.Args[0]);
				break;
			case MMLCommandType.Octave:
				num2 = Convert.ToInt32(command.Args[0]);
				break;
			case MMLCommandType.OctaveDown:
				num2--;
				break;
			case MMLCommandType.OctaveUp:
				num2++;
				break;
			}
			num += measureLength;
		}
		return list;
	}

	private static void GetRest(MMLCommand cmd, out double measureLength, MMLLength defaultLength)
	{
		MMLLength length = GetLength(cmd.Args[0], cmd.Args[1], defaultLength);
		measureLength = 1.0 / (double)length.Length * ((!length.Dotted) ? 1.0 : 1.5);
	}

	private static Note GetNote(MMLCommand cmd, out double measureLength, MMLLength defaultLength, int volume, int octave, double spm)
	{
		if (cmd.Type == MMLCommandType.Note)
		{
			return GetNoteNormal(cmd, out measureLength, defaultLength, volume, octave, spm);
		}
		return GetNoteNumber(cmd, out measureLength, defaultLength, volume, spm);
	}

	private static Note GetNoteNormal(MMLCommand cmd, out double measureLength, MMLLength defaultLength, int volume, int octave, double spm)
	{
		Note note = new Note
		{
			Volume = (float)volume / 15f,
			Octave = octave,
			Type = cmd.Args[0].ToLowerInvariant()[0]
		};
		switch (cmd.Args[1])
		{
		case "#":
		case "+":
			Step(ref note, 1);
			break;
		case "-":
			Step(ref note, -1);
			break;
		default:
			note.Sharp = false;
			break;
		}
		MMLLength length = GetLength(cmd.Args[2], cmd.Args[3], defaultLength);
		note.Length = length.ToTimeSpan(spm);
		measureLength = 1.0 / (double)length.Length;
		if (length.Dotted)
		{
			measureLength *= 1.5;
		}
		return note;
	}

	private static Note GetNoteNumber(MMLCommand cmd, out double measureLength, MMLLength defaultLength, int volume, double spm)
	{
		Note note = new Note
		{
			Volume = (float)volume / 15f,
			Octave = 1,
			Type = 'c'
		};
		int num = Convert.ToInt32(cmd.Args[0]) - 12;
		int num2 = num / 12;
		int steps = ((num2 == 0) ? num : (num % (num2 * 12)));
		note.Octave += num2;
		Step(ref note, steps);
		MMLLength length = GetLength(string.Empty, cmd.Args[1], defaultLength);
		note.Length = length.ToTimeSpan(spm);
		measureLength = 1.0 / (double)length.Length;
		if (length.Dotted)
		{
			measureLength *= 1.5;
		}
		return note;
	}

	private static MMLLength GetLength(string number, string dot, MMLLength defaultLength)
	{
		MMLLength result = defaultLength;
		if (!string.IsNullOrEmpty(number))
		{
			result = new MMLLength(Convert.ToInt32(number), dot != string.Empty);
		}
		else if (!string.IsNullOrEmpty(dot))
		{
			result.Dotted = true;
		}
		return result;
	}

	private static void Step(ref Note note, int steps)
	{
		if (steps == 0)
		{
			return;
		}
		if (steps > 0)
		{
			for (int i = 0; i < steps; i++)
			{
				switch (note.Type)
				{
				case 'a':
					if (!note.Sharp)
					{
						note.Sharp = true;
						break;
					}
					note.Type = 'b';
					note.Sharp = false;
					break;
				case 'b':
					note.Type = 'c';
					note.Octave++;
					break;
				case 'c':
					if (!note.Sharp)
					{
						note.Sharp = true;
						break;
					}
					note.Type = 'd';
					note.Sharp = false;
					break;
				case 'd':
					if (!note.Sharp)
					{
						note.Sharp = true;
						break;
					}
					note.Type = 'e';
					note.Sharp = false;
					break;
				case 'e':
					note.Type = 'f';
					break;
				case 'f':
					if (!note.Sharp)
					{
						note.Sharp = true;
						break;
					}
					note.Type = 'g';
					note.Sharp = false;
					break;
				case 'g':
					if (!note.Sharp)
					{
						note.Sharp = true;
						break;
					}
					note.Type = 'a';
					note.Sharp = false;
					break;
				}
			}
			return;
		}
		for (int j = 0; j < Math.Abs(steps); j++)
		{
			switch (note.Type)
			{
			case 'a':
				if (note.Sharp)
				{
					note.Sharp = false;
					break;
				}
				note.Type = 'g';
				note.Sharp = true;
				break;
			case 'b':
				note.Type = 'a';
				note.Sharp = true;
				break;
			case 'c':
				if (note.Sharp)
				{
					note.Sharp = false;
					break;
				}
				note.Type = 'b';
				note.Octave--;
				break;
			case 'd':
				if (note.Sharp)
				{
					note.Sharp = false;
					break;
				}
				note.Type = 'c';
				note.Sharp = true;
				break;
			case 'e':
				note.Type = 'd';
				note.Sharp = true;
				break;
			case 'f':
				if (note.Sharp)
				{
					note.Sharp = false;
				}
				else
				{
					note.Type = 'e';
				}
				break;
			case 'g':
				if (note.Sharp)
				{
					note.Sharp = false;
					break;
				}
				note.Type = 'f';
				note.Sharp = true;
				break;
			}
		}
	}
}

using System;

namespace MML;

public struct Note
{
	public int Octave;

	public TimeSpan Length;

	public char Type;

	public bool Sharp;

	public float Volume;

	public int GetStep()
	{
		return Type switch
		{
			'a' => Sharp ? 10 : 9,
			'b' => 11,
			'c' => Sharp ? 1 : 0,
			'd' => Sharp ? 3 : 2,
			'e' => 4,
			'f' => Sharp ? 6 : 5,
			_ => Sharp ? 8 : 7,
		} + Octave * 12;
	}
}

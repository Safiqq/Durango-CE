using System;

namespace MML;

public struct MMLLength
{
	public int Length;

	public bool Dotted;

	public MMLLength(int length, bool dotted)
	{
		Length = length;
		Dotted = dotted;
	}

	public TimeSpan ToTimeSpan(double secondsPerMeasure)
	{
		double num = 1.0 / (double)Length;
		if (Dotted)
		{
			num *= 1.5;
		}
		return new TimeSpan((long)(secondsPerMeasure * num * 10000000.0));
	}
}

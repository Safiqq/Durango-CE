using System.Collections.Generic;
using System.Diagnostics;

namespace Durango.Utils;

public static class SimpleProfiler
{
	private static readonly Stack<KeyValuePair<string, long>> ProfileStack;

	static SimpleProfiler()
	{
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	public static void Begin(string text)
	{
		KeyValuePair<string, long> item = new KeyValuePair<string, long>(text, Stopwatch.GetTimestamp());
		ProfileStack.Push(item);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	public static void End()
	{
		KeyValuePair<string, long> keyValuePair = ProfileStack.Pop();
		double num = Stopwatch.GetTimestamp() - keyValuePair.Value;
		double num2 = ((!Stopwatch.IsHighResolution) ? (num / 10000.0) : (num / (double)Stopwatch.Frequency * 1000.0));
		_ = $"{keyValuePair.Key} time: {num2:F2}(ms)";
	}
}

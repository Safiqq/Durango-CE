using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SANITIZED: the original Raven/Sentry client uploaded crash reports, including
/// device and player metadata, to a remote DSN. The network code and DSN are gone.
/// This type remains only so <see cref="ErrorReporter"/> and other call sites compile.
/// </summary>
public static class RavenClient
{
	public static void CaptureUntiyLog(string log, string stack, LogType logType, Dictionary<string, string> tags = null, object extra = null)
	{
		// Telemetry disabled intentionally.
	}
}

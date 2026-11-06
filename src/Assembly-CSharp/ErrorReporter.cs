using System;
using UnityEngine;

/// <summary>
/// SANITIZED: the original reporter collected processor, GPU, device UID, device
/// name/model and player ID, then forwarded them to the Raven/Sentry client.
///
/// No telemetry is collected or transmitted now. The method is kept as a no-op so
/// <see cref="GameManager"/> can keep its log callback registration unchanged.
/// </summary>
public static class ErrorReporter
{
	public static void HandleLog(string log, string stack, DateTime time, LogType type)
	{
		// Telemetry disabled intentionally.
	}
}

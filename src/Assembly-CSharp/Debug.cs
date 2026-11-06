using System;
using System.Diagnostics;
using UnityEngine;

public class Debug
{
	public static bool developerConsoleVisible
	{
		get
		{
			return UnityEngine.Debug.developerConsoleVisible;
		}
		set
		{
			UnityEngine.Debug.developerConsoleVisible = value;
		}
	}

	public static bool isDebugBuild => UnityEngine.Debug.isDebugBuild;

	public static void ClearDeveloperConsole()
	{
		UnityEngine.Debug.ClearDeveloperConsole();
	}

	public static void DrawLine(Vector3 start, Vector3 end)
	{
		UnityEngine.Debug.DrawLine(start, end);
	}

	public static void DrawLine(Vector3 start, Vector3 end, Color color)
	{
		UnityEngine.Debug.DrawLine(start, end, color);
	}

	public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration)
	{
		UnityEngine.Debug.DrawLine(start, end, color, duration);
	}

	public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration, bool depthTest)
	{
		UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);
	}

	public static void DrawRay(Vector3 start, Vector3 dir)
	{
		UnityEngine.Debug.DrawRay(start, dir);
	}

	public static void DrawRay(Vector3 start, Vector3 dir, Color color)
	{
		UnityEngine.Debug.DrawRay(start, dir, color);
	}

	public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration)
	{
		UnityEngine.Debug.DrawRay(start, dir, color, duration);
	}

	public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration, bool depthTest)
	{
		UnityEngine.Debug.DrawRay(start, dir, color, duration, depthTest);
	}

	public static void Break()
	{
		UnityEngine.Debug.Break();
	}

	public static void DebugBreak()
	{
		UnityEngine.Debug.DebugBreak();
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	public static void Log(object message)
	{
		UnityEngine.Debug.Log(message);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	public static void Log(object message, UnityEngine.Object context)
	{
		UnityEngine.Debug.Log(message, context);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	public static void LogFormat(string format, params object[] args)
	{
		UnityEngine.Debug.LogFormat(format, args);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	public static void LogFormat(UnityEngine.Object context, string format, params object[] args)
	{
		UnityEngine.Debug.LogFormat(context, format, args);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	[Conditional("DEBUG_LEVEL_WARN")]
	public static void LogWarning(object message)
	{
		UnityEngine.Debug.LogWarning(message);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	[Conditional("DEBUG_LEVEL_WARN")]
	public static void LogWarning(object message, UnityEngine.Object context)
	{
		UnityEngine.Debug.LogWarning(message, context);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	[Conditional("DEBUG_LEVEL_WARN")]
	public static void LogWarningFormat(string format, params object[] args)
	{
		UnityEngine.Debug.LogWarningFormat(format, args);
	}

	[Conditional("DEBUG_LEVEL_LOG")]
	[Conditional("DEBUG_LEVEL_WARN")]
	public static void LogWarningFormat(UnityEngine.Object context, string format, params object[] args)
	{
		UnityEngine.Debug.LogWarningFormat(context, format, args);
	}

	public static void LogError(object message)
	{
		UnityEngine.Debug.LogError(message);
	}

	public static void LogError(object message, UnityEngine.Object context)
	{
		UnityEngine.Debug.LogError(message, context);
	}

	public static void LogErrorFormat(string format, params object[] args)
	{
		UnityEngine.Debug.LogErrorFormat(format, args);
	}

	public static void LogErrorFormat(UnityEngine.Object context, string format, params object[] args)
	{
		UnityEngine.Debug.LogErrorFormat(context, format, args);
	}

	public static void LogException(Exception exception)
	{
		UnityEngine.Debug.LogException(exception);
	}

	public static void LogException(Exception exception, UnityEngine.Object context)
	{
		UnityEngine.Debug.LogException(exception, context);
	}

	public static void LogException(Exception exception, UnityEngine.Object context, bool unused)
	{
		UnityEngine.Debug.LogException(exception, context);
	}

	[Conditional("UNITY_ASSERTIONS")]
	public static void Assert(bool condition)
	{
	}

	[Conditional("UNITY_ASSERTIONS")]
	public static void Assert(bool condition, string assertString)
	{
	}

	[Conditional("UNITY_ASSERTIONS")]
	public static void Assert(bool condition, string format, params object[] args)
	{
	}

	public static void Assert(bool condition, object message)
	{
	}

	public static void Assert(bool condition, object message, UnityEngine.Object context)
	{
	}

	public static void Assert(bool condition, string message, UnityEngine.Object context)
	{
	}

	[Conditional("UNITY_ASSERTIONS")]
	public static void LogAssertion(object message)
	{
	}

	[Conditional("UNITY_ASSERTIONS")]
	public static void LogAssertion(object message, UnityEngine.Object context)
	{
	}

	[Conditional("UNITY_ASSERTIONS")]
	public static void LogAssertionFormat(string format, params object[] args)
	{
	}

	public static void LogAssertionFormat(UnityEngine.Object context, string format, params object[] args)
	{
	}
}

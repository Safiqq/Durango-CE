using UnityEngine;

public static class OffServerLog
{
	public static void Info(string message)
	{
		if (Application.isEditor)
		{
			UnityEngine.Debug.Log(message);
		}
		else
		{
			UnityEngine.Debug.LogError(message);
		}
	}
}

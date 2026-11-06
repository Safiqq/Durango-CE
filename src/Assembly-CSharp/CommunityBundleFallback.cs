using System;
using System.Collections.Generic;
using UnityEngine;

public static class CommunityBundleFallback
{
	public const string Folder = "BundleFallback/";

	private static readonly Dictionary<string, UnityEngine.Object> _cache = new Dictionary<string, UnityEngine.Object>();

	private static readonly HashSet<string> _missing = new HashSet<string>();

	private static readonly HashSet<string> _gaveUp = new HashSet<string>();

	public static int Served;

	public static string ResourceName(string uniqueName)
	{
		if (string.IsNullOrEmpty(uniqueName))
		{
			return null;
		}
		string text = uniqueName.ToLowerInvariant();
		if (text.EndsWith(".bundle"))
		{
			text = text.Substring(0, text.Length - ".bundle".Length);
		}
		if (text.EndsWith(".prefab"))
		{
			text = text.Substring(0, text.Length - ".prefab".Length);
			return text.Replace("$", "__");
		}
		return null;
	}

	public static void MarkGaveUp(string parentName)
	{
		if (!string.IsNullOrEmpty(parentName))
		{
			_gaveUp.Add(parentName);
		}
	}

	public static bool GaveUp(string parentName)
	{
		if (!string.IsNullOrEmpty(parentName))
		{
			return _gaveUp.Contains(parentName);
		}
		return false;
	}

	public static bool Has(string uniqueName)
	{
		return Load(uniqueName, null) != null;
	}

	public static UnityEngine.Object Load(string uniqueName, Type type)
	{
		string text = ResourceName(uniqueName);
		if (text == null || _missing.Contains(text))
		{
			return null;
		}
		if (!_cache.TryGetValue(text, out var value) || value == null)
		{
			try
			{
				value = Resources.Load("BundleFallback/" + text);
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.Log("[ABM] fallback " + text + " ล\u0e49ม: " + ex.Message);
				value = null;
			}
			if (value == null)
			{
				_missing.Add(text);
				return null;
			}
			_cache[text] = value;
			UnityEngine.Debug.Log("[ABM] fallback ใช\u0e49 prefab ในเกมแทน bundle: " + uniqueName);
		}
		if (type != null && !type.IsInstanceOfType(value))
		{
			return null;
		}
		Served++;
		return value;
	}
}

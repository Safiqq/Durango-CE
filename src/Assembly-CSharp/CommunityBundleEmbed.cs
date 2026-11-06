using System;
using System.Collections;
using System.IO;
using Durango.Logic.Clusters;
using UnityEngine;
using UnityEngine.Networking;

public static class CommunityBundleEmbed
{
	public const string Folder = "AssetBundles";

	public const string MarkerName = "CommunityEmbed.json";

	public const string IndexName = "Info.5.2.1.json";

	private static bool _probeStarted;

	private static bool _probeDone;

	private static bool _active;

	private static string _marker;

	private static byte[] _index;

	private static string _serverIndexUrl;

	private static string _serverUrlRoot;

	private static string _testRoot;

	private static int _localLoads;

	private static int _serverLoads;

	public static bool ProbeDone
	{
		get
		{
			StartProbe();
			return _probeDone;
		}
	}

	public static bool Active
	{
		get
		{
			if (ProbeDone)
			{
				return _active;
			}
			return false;
		}
	}

	public static string LocalRoot => Application.streamingAssetsPath + "/AssetBundles/";

	public static string ServerIndexUrl
	{
		get
		{
			if (!string.IsNullOrEmpty(_testRoot))
			{
				return _testRoot + "Info.5.2.1.json";
			}
			return _serverIndexUrl;
		}
	}

	public static string ServerUrlRoot
	{
		get
		{
			if (!string.IsNullOrEmpty(_testRoot))
			{
				return _testRoot;
			}
			return _serverUrlRoot;
		}
	}

	public static string Marker => _marker;

	public static void SetServer(string indexUrl, string urlRoot)
	{
		_serverIndexUrl = ((string.IsNullOrEmpty(indexUrl) || string.IsNullOrEmpty(urlRoot)) ? null : indexUrl);
		_serverUrlRoot = ((_serverIndexUrl == null) ? null : (urlRoot.EndsWith("/") ? urlRoot : (urlRoot + "/")));
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void StartProbe()
	{
		if (_probeStarted)
		{
			return;
		}
		_probeStarted = true;
		if (Application.isEditor)
		{
			_probeDone = true;
			return;
		}
		string text = LocalRoot + "CommunityEmbed.json";
		if (Application.platform != RuntimePlatform.Android)
		{
			try
			{
				if (File.Exists(text))
				{
					_marker = File.ReadAllText(text);
					_active = true;
					ReadTestOverride();
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("[ABM] embed marker: " + ex.Message);
			}
			_probeDone = true;
			Log();
			return;
		}
		UnityWebRequest request = UnityWebRequest.Get(text);
		request.SendWebRequest().completed += delegate
		{
			try
			{
				if (request.result == UnityWebRequest.Result.Success && request.downloadHandler.data != null && request.downloadHandler.data.Length != 0)
				{
					_marker = request.downloadHandler.text;
					_active = true;
					ReadTestOverride();
				}
			}
			catch (Exception ex2)
			{
				UnityEngine.Debug.LogError("[ABM] embed marker: " + ex2.Message);
			}
			request.Dispose();
			_probeDone = true;
			Log();
		};
	}

	private static void Log()
	{
		if (_active)
		{
			CommunityLog.Info("[ABM] embed: bundles in game (" + LocalRoot + ") " + (_marker ?? "").Replace("\r", "").Replace("\n", " ") + (string.IsNullOrEmpty(_testRoot) ? "" : (" · test server index " + _testRoot)));
		}
	}

	private static void ReadTestOverride()
	{
		try
		{
			string configPath = CommunityLink.ConfigPath;
			if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
			{
				return;
			}
			string[] array = File.ReadAllLines(configPath);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.StartsWith("bundle_embed=", StringComparison.OrdinalIgnoreCase))
				{
					string text2 = text.Substring("bundle_embed=".Length).Trim();
					if (text2.Length > 0)
					{
						_testRoot = (text2.EndsWith("/") ? text2 : (text2 + "/"));
					}
				}
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("[ABM] embed test override: " + ex.Message);
		}
	}

	public static IEnumerator CoReadIndex(Action<byte[]> done)
	{
		if (_index != null)
		{
			done(_index);
			yield break;
		}
		string path = LocalRoot + "Info.5.2.1.json";
		if (Application.platform != RuntimePlatform.Android)
		{
			try
			{
				_index = File.ReadAllBytes(path);
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("[ABM] embed index " + path + ": " + ex.Message);
			}
			done(_index);
			yield break;
		}
		using (UnityWebRequest request = UnityWebRequest.Get(path))
		{
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success && request.downloadHandler.data != null && request.downloadHandler.data.Length != 0)
			{
				_index = request.downloadHandler.data;
			}
			else
			{
				UnityEngine.Debug.LogError("[ABM] embed index " + path + ": " + request.error);
			}
		}
		done(_index);
	}

	public static string LocalPath(string bundleFileName, string crc)
	{
		return AssetBundleItemInfo.GetCrcName(bundleFileName, crc, LocalRoot);
	}

	public static void NoteLoad(string name, bool local)
	{
		if (local)
		{
			_localLoads++;
			if (_localLoads <= 3 || _localLoads % 200 == 0)
			{
				CommunityLog.Info("[ABM] embed: local bundle #" + _localLoads + " " + name + " (server " + _serverLoads + ")");
			}
		}
		else
		{
			_serverLoads++;
			CommunityLog.Info("[ABM] embed: server bundle #" + _serverLoads + " " + name + " (local " + _localLoads + ")");
		}
	}
}

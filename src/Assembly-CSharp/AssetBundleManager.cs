using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Durango.System;
using Durango.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

public class AssetBundleManager : Singleton<AssetBundleManager>
{
	public enum Status
	{
		None,
		LoadInfo,
		LoadPreload,
		Ready,
		Failed,
		Cached
	}

	private class AssetBundleItemToLoad
	{
		public int RetryCount;

		public AssetBundleItem Item;

		public Action<UnityEngine.Object> Finished;

		public Type ReqType;

		public string UniqueName;
	}

	private enum LoadStatus
	{
		Failed = -1,
		Wait,
		Complete
	}

	private class RequestLimiter
	{
		private int _maxCount;

		public void Clear()
		{
			_maxCount = 0;
		}

		public bool Acquire()
		{
			if (_maxCount > 30)
			{
				return false;
			}
			_maxCount++;
			return true;
		}

		public void Release()
		{
			_maxCount--;
		}
	}

	public static int PrerequsitePriority;

	private static readonly WaitForSeconds InternetCheckWaitTime;

	[SerializeField]
	private string _infoHolderName;

	[SerializeField]
	private int _maxRetryCount = 1;

	private Hash128 _preloadHash;

	private string _preloadCrc;

	private string _prevInfoHolderPath;

	private readonly Dictionary<string, AssetBundleItem> _assetBundleItemDict = new Dictionary<string, AssetBundleItem>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, AssetBundleFile> _assetBundleFileDict = new Dictionary<string, AssetBundleFile>(StringComparer.OrdinalIgnoreCase);

	private readonly List<AssetBundleItemToLoad> _itemToLoad = new List<AssetBundleItemToLoad>();

	private readonly List<AssetBundleFile> _prerequisites = new List<AssetBundleFile>();

	private bool _isBackgroundDownloading;

	private AssetBundle _preloadBundle;

	private readonly DictionaryIgnoreCase<UnityEngine.Object> _precachedAssets = new DictionaryIgnoreCase<UnityEngine.Object>();

	private int _precachedAssetsCount;

	private readonly RequestLimiter _requestLimiter = new RequestLimiter();

	private static string[] _playerClipPath;

	private bool _infoHolderLoaded;

	private ulong _infoHolderHash;

	private readonly List<UnityWebRequest> _activeRequests = new List<UnityWebRequest>();

	private readonly Dictionary<string, string> _localFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private string _localPreloadPath;

	private bool _embedMode;

	private bool _embedRemoteOk;

	private readonly List<AssetBundleCreateRequest> _activeLocalLoads = new List<AssetBundleCreateRequest>();

	private static bool KeepAssets => !Application.isMobilePlatform;

	public int PendingRequestCount => _itemToLoad.Count;

	public string AssetBundleLoadPath { get; private set; }

	public static bool UseBundle => Platform.Instance.UseAssetBundle;

	public Status CurrentStatus { get; private set; }

	public int TotalFileCount => _assetBundleFileDict.Count;

	public int PrerequsitesCount => _prerequisites.Count;

	public event Action BackgroundDownloadStarted;

	public event Action BackgroundDownloadCompleted;

	static AssetBundleManager()
	{
		PrerequsitePriority = 500;
		InternetCheckWaitTime = new WaitForSeconds(2f);
		_playerClipPath = new string[2] { "models/pc/male/_anim/m_reference_clips.asset", "models/pc/female/_anim/f_reference_clips.asset" };
		GameManager.Reset += delegate
		{
			Singleton<AssetBundleManager>.Instance().StopBackgroundDownloading();
			Singleton<AssetBundleManager>.Instance().ClearAll();
		};
		Application.lowMemory += delegate
		{
			if (Singleton<AssetBundleManager>.HasInstance())
			{
				Singleton<AssetBundleManager>.Instance().ReleaseKeptAssets("low memory");
			}
		};
	}

	protected override bool CheckDontDestroyOnLoad()
	{
		return true;
	}

	public void Initialize(string infoHolderPath, string urlRoot)
	{
		if (UseBundle && (!Application.isEditor || OffServerBundleEmbed.Active))
		{
			if (!OffServerBundleEmbed.ProbeDone)
			{
				StartCoroutine(CoInitializeAfterProbe(infoHolderPath, urlRoot));
				return;
			}
			if (OffServerBundleEmbed.Active)
			{
				StartCoroutine(CoLoadEmbedded(infoHolderPath, urlRoot));
				return;
			}
		}
		InitializeLegacy(infoHolderPath, urlRoot);
	}

	private IEnumerator CoInitializeAfterProbe(string infoHolderPath, string urlRoot)
	{
		while (!OffServerBundleEmbed.ProbeDone)
		{
			yield return null;
		}
		Initialize(infoHolderPath, urlRoot);
	}

	private void InitializeLegacy(string infoHolderPath, string urlRoot)
	{
		_embedMode = false;
		_embedRemoteOk = false;
		_localFiles.Clear();
		_localPreloadPath = null;
		if (UseBundle)
		{
			bool flag = (Application.platform == RuntimePlatform.Android || Application.isEditor) && !string.IsNullOrEmpty(infoHolderPath) && !string.IsNullOrEmpty(urlRoot);
			OffServerLog.Info("[ABM] Initialize local=" + (!flag && !string.IsNullOrEmpty(_infoHolderName)) + " index='" + infoHolderPath + "' root='" + urlRoot + "'");
			if (!string.IsNullOrEmpty(_infoHolderName) && !flag)
			{
				RuntimePlatform platform = Application.platform;
				if (platform == RuntimePlatform.WindowsEditor || platform == RuntimePlatform.OSXEditor)
				{
					AssetBundleLoadPath = "file://" + Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/", StringComparison.Ordinal)) + "/AssetBundles/";
				}
				else
				{
					AssetBundleLoadPath = "file://" + Application.streamingAssetsPath + "/AssetBundles/";
				}
				infoHolderPath = AssetBundleLoadPath + _infoHolderName + ".json";
			}
			else
			{
				if (string.IsNullOrEmpty(infoHolderPath) || string.IsNullOrEmpty(urlRoot))
				{
					CurrentStatus = Status.Failed;
					return;
				}
				AssetBundleLoadPath = urlRoot;
			}
			bool num = _prevInfoHolderPath == infoHolderPath;
			_prevInfoHolderPath = infoHolderPath;
			bool skipIfCached = num && (CurrentStatus == Status.Ready || CurrentStatus == Status.Cached);
			StartCoroutine(CoLoadAssetBundleInfoHolder(infoHolderPath, skipIfCached));
		}
		else
		{
			CurrentStatus = Status.Ready;
		}
	}

	private void Update()
	{
		if (CurrentStatus != Status.Ready)
		{
			return;
		}
		for (int num = _itemToLoad.Count - 1; num >= 0; num--)
		{
			AssetBundleItemToLoad assetBundleItemToLoad = _itemToLoad[num];
			AssetBundleItem item = _itemToLoad[num].Item;
			if (OffServerBundleFallback.GaveUp(item.Parent.Name))
			{
				UnityEngine.Object obj = OffServerBundleFallback.Load(assetBundleItemToLoad.UniqueName, assetBundleItemToLoad.ReqType);
				if (obj != null)
				{
					_itemToLoad.RemoveAt(num);
					assetBundleItemToLoad.Finished(obj);
					assetBundleItemToLoad.Finished = null;
					continue;
				}
			}
			switch (TryLoadBundleFile(item.Parent))
			{
			case LoadStatus.Failed:
				assetBundleItemToLoad.RetryCount++;
				if (assetBundleItemToLoad.RetryCount > _maxRetryCount)
				{
					UnityEngine.Debug.LogError("[ABM] giving up on " + item.Name + " (parent " + item.Parent.Name + ") after " + assetBundleItemToLoad.RetryCount + " retries");
					_itemToLoad.RemoveAt(num);
					UnityEngine.Object obj2 = OffServerBundleFallback.Load(assetBundleItemToLoad.UniqueName, assetBundleItemToLoad.ReqType);
					if (obj2 != null)
					{
						OffServerBundleFallback.MarkGaveUp(item.Parent.Name);
					}
					assetBundleItemToLoad.Finished(obj2);
					assetBundleItemToLoad.Finished = null;
				}
				break;
			case LoadStatus.Complete:
				if (!(item.Parent.Bundle != null))
				{
					break;
				}
				if (item.Request == null && item.Asset != null && (assetBundleItemToLoad.ReqType == null || assetBundleItemToLoad.ReqType.IsInstanceOfType(item.Asset)))
				{
					_itemToLoad.RemoveAt(num);
					assetBundleItemToLoad.Finished(item.Asset);
					assetBundleItemToLoad.Finished = null;
					break;
				}
				if (item.Request == null)
				{
					item.Request = item.Parent.Bundle.LoadAssetAsync(item.Name, assetBundleItemToLoad.ReqType);
				}
				if (item.Request.isDone)
				{
					_itemToLoad.RemoveAt(num);
					OffServerShaderRemap.Fix(item.Request.asset);
					if (KeepAssets)
					{
						item.Asset = item.Request.asset;
					}
					assetBundleItemToLoad.Finished(item.Request.asset);
					assetBundleItemToLoad.Finished = null;
				}
				break;
			}
		}
	}

	private IEnumerator CoLoadAssetBundleInfoHolder(string infoHolderPath, bool skipIfCached)
	{
		CurrentStatus = Status.LoadInfo;
		OffServerLog.Info("[ABM] load info holder url='" + infoHolderPath + "' loadPath='" + AssetBundleLoadPath + "'");
		UnityWebRequest request = UnityWebRequest.Get(infoHolderPath);
		request.SetRequestHeader("Accept-Encoding", "gzip");
		request.SetRequestHeader("Accept", "application/json");
		request.SetRequestHeader("Accept-Language", LocalizeSystem.Locale);
		request.SetRequestHeader("X-K1-System-Language", LocalizeSystem.SystemLanguage);
		request.SetRequestHeader("cache-control", "max-age=0");
		yield return request.SendWebRequest();
		byte[] data = request.downloadHandler.data;
		if (KUtility.GetSize(data) == 0)
		{
			CurrentStatus = Status.Failed;
			yield break;
		}
		ulong num = ComputeHash(data);
		if (skipIfCached && _infoHolderLoaded && num == _infoHolderHash && _preloadBundle != null)
		{
			OffServerLog.Info("[ABM] index unchanged - keeping " + CountOpenBundles() + " open bundles");
			CurrentStatus = Status.Ready;
			yield break;
		}
		_infoHolderLoaded = false;
		AssetBundleInfoHolder assetBundleInfoHolder = Json.Read<AssetBundleInfoHolder>(data);
		if (assetBundleInfoHolder == null)
		{
			CurrentStatus = Status.Failed;
			yield break;
		}
		_infoHolderHash = num;
		_infoHolderLoaded = true;
		FixAndroidSoundBankNames(assetBundleInfoHolder);
		_preloadHash = Hash128.Parse(assetBundleInfoHolder.PreloadHash);
		_preloadCrc = assetBundleInfoHolder.PreloadCrc;
		LoadAssetBundeFiles(assetBundleInfoHolder);
		LoadAssetBundleItems(assetBundleInfoHolder);
		yield return StartCoroutine(CoLoadPreloadFile());
	}

	private IEnumerator CoLoadEmbedded(string infoHolderPath, string urlRoot)
	{
		string serverIndexUrl = OffServerBundleEmbed.ServerIndexUrl;
		string serverRoot = OffServerBundleEmbed.ServerUrlRoot;
		string text = "embed|" + (serverIndexUrl ?? "-");
		bool flag = _prevInfoHolderPath == text;
		_prevInfoHolderPath = text;
		bool skipIfCached = flag && (CurrentStatus == Status.Ready || CurrentStatus == Status.Cached);
		CurrentStatus = Status.LoadInfo;
		byte[] localData = null;
		yield return StartCoroutine(OffServerBundleEmbed.CoReadIndex(delegate(byte[] d)
		{
			localData = d;
		}));
		AssetBundleInfoHolder local = ((KUtility.GetSize(localData) == 0) ? null : Json.Read<AssetBundleInfoHolder>(localData));
		if (local == null || local.FileList == null || local.ItemList == null || local.FileList.Count == 0)
		{
			UnityEngine.Debug.LogError("[ABM] embed: index in the game unreadable - old path (server index)");
			_prevInfoHolderPath = null;
			InitializeLegacy(infoHolderPath, urlRoot);
			yield break;
		}
		byte[] serverData = null;
		if (!string.IsNullOrEmpty(serverIndexUrl) && !string.IsNullOrEmpty(serverRoot))
		{
			OffServerLog.Info("[ABM] embed: server index url='" + serverIndexUrl + "' root='" + serverRoot + "'");
			using UnityWebRequest request = UnityWebRequest.Get(serverIndexUrl);
			request.SetRequestHeader("Accept-Encoding", "gzip");
			request.SetRequestHeader("Accept", "application/json");
			request.SetRequestHeader("Accept-Language", LocalizeSystem.Locale);
			request.SetRequestHeader("X-K1-System-Language", LocalizeSystem.SystemLanguage);
			request.SetRequestHeader("cache-control", "max-age=0");
			yield return request.SendWebRequest();
			if (request.result == UnityWebRequest.Result.Success && KUtility.GetSize(request.downloadHandler.data) > 0)
			{
				serverData = request.downloadHandler.data;
			}
			else
			{
				UnityEngine.Debug.LogError("[ABM] embed: server index failed (http=" + request.responseCode + " " + request.error + ") - bundles in the game only");
			}
		}
		AssetBundleInfoHolder assetBundleInfoHolder = ((serverData == null) ? null : Json.Read<AssetBundleInfoHolder>(serverData));
		if (assetBundleInfoHolder != null && (assetBundleInfoHolder.FileList == null || assetBundleInfoHolder.ItemList == null || assetBundleInfoHolder.FileList.Count == 0))
		{
			UnityEngine.Debug.LogError("[ABM] embed: server index unreadable - bundles in the game only");
			assetBundleInfoHolder = null;
		}
		ulong num = ((assetBundleInfoHolder != null) ? (ComputeHash(serverData) ^ 0x9E3779B97F4A7C15uL) : ComputeHash(localData));
		if (skipIfCached && _infoHolderLoaded && num == _infoHolderHash && _preloadBundle != null)
		{
			OffServerLog.Info("[ABM] index unchanged - keeping " + CountOpenBundles() + " open bundles");
			CurrentStatus = Status.Ready;
			yield break;
		}
		_infoHolderLoaded = false;
		_embedMode = true;
		_localFiles.Clear();
		_localPreloadPath = null;
		FixAndroidSoundBankNames(local);
		AssetBundleInfoHolder assetBundleInfoHolder2 = local;
		if (assetBundleInfoHolder != null)
		{
			FixAndroidSoundBankNames(assetBundleInfoHolder);
			assetBundleInfoHolder2 = assetBundleInfoHolder;
			Dictionary<string, AssetBundleFileInfo> dictionary = new Dictionary<string, AssetBundleFileInfo>(StringComparer.OrdinalIgnoreCase);
			foreach (AssetBundleFileInfo file in local.FileList)
			{
				if (file.Name != null)
				{
					dictionary[file.Name] = file;
				}
			}
			int num2 = 0;
			StringBuilder stringBuilder = new StringBuilder();
			foreach (AssetBundleFileInfo file2 in assetBundleInfoHolder.FileList)
			{
				if (file2.Name != null && dictionary.TryGetValue(file2.Name, out var value) && SameText(value.Hash, file2.Hash) && SameText(value.Crc, file2.Crc))
				{
					_localFiles[file2.Name] = OffServerBundleEmbed.LocalPath(file2.Name, file2.Crc);
					continue;
				}
				num2++;
				if (num2 <= 20)
				{
					stringBuilder.Append(' ').Append(file2.Name);
				}
			}
			if (SameText(local.PreloadHash, assetBundleInfoHolder.PreloadHash) && SameText(local.PreloadCrc, assetBundleInfoHolder.PreloadCrc))
			{
				_localPreloadPath = OffServerBundleEmbed.LocalPath("preload.bundle", assetBundleInfoHolder.PreloadCrc);
			}
			_embedRemoteOk = true;
			AssetBundleLoadPath = serverRoot;
			OffServerLog.Info("[ABM] embed: in game " + _localFiles.Count + " · from server " + num2 + "/" + assetBundleInfoHolder.FileList.Count + " · preload " + ((_localPreloadPath != null) ? "in game" : "server") + ((num2 > 0) ? (" ·" + stringBuilder?.ToString() + ((num2 > 20) ? " …" : "")) : ""));
		}
		else
		{
			foreach (AssetBundleFileInfo file3 in local.FileList)
			{
				if (file3.Name != null)
				{
					_localFiles[file3.Name] = OffServerBundleEmbed.LocalPath(file3.Name, file3.Crc);
				}
			}
			_localPreloadPath = OffServerBundleEmbed.LocalPath("preload.bundle", local.PreloadCrc);
			_embedRemoteOk = false;
			AssetBundleLoadPath = OffServerBundleEmbed.LocalRoot;
			OffServerLog.Info("[ABM] embed: no server embed index - all " + _localFiles.Count + " bundles from the game");
		}
		_infoHolderHash = num;
		_infoHolderLoaded = true;
		_preloadHash = Hash128.Parse(assetBundleInfoHolder2.PreloadHash);
		_preloadCrc = assetBundleInfoHolder2.PreloadCrc;
		LoadAssetBundeFiles(assetBundleInfoHolder2);
		LoadAssetBundleItems(assetBundleInfoHolder2);
		yield return StartCoroutine(CoLoadPreloadFile());
	}

	private static bool SameText(string a, string b)
	{
		if (!string.IsNullOrEmpty(a))
		{
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool IsPrerequsite(AssetBundleFile file)
	{
		return file.Priority > PrerequsitePriority;
	}

	private static ulong ComputeHash(byte[] data)
	{
		ulong num = 14695981039346656037uL;
		for (int i = 0; i < data.Length; i++)
		{
			num = (num ^ data[i]) * 1099511628211L;
		}
		return num;
	}

	private int CountOpenBundles()
	{
		int num = 0;
		foreach (KeyValuePair<string, AssetBundleFile> item in _assetBundleFileDict)
		{
			if (item.Value.Bundle != null)
			{
				num++;
			}
		}
		return num;
	}

	private static void FixAndroidSoundBankNames(AssetBundleInfoHolder holder)
	{
		if (Application.platform != RuntimePlatform.Android || holder == null)
		{
			return;
		}
		int num = 0;
		if (holder.FileList != null)
		{
			foreach (AssetBundleFileInfo file in holder.FileList)
			{
				if (file.Name != null && file.Name.StartsWith("soundbanks$windows$", StringComparison.OrdinalIgnoreCase))
				{
					file.Name = "soundbanks$android$" + file.Name.Substring("soundbanks$windows$".Length);
					num++;
				}
				if (file.Dependencies == null)
				{
					continue;
				}
				for (int i = 0; i < file.Dependencies.Length; i++)
				{
					string text = file.Dependencies[i];
					if (text != null && text.StartsWith("soundbanks$windows$", StringComparison.OrdinalIgnoreCase))
					{
						file.Dependencies[i] = "soundbanks$android$" + text.Substring("soundbanks$windows$".Length);
					}
				}
			}
		}
		if (holder.ItemList != null)
		{
			foreach (AssetBundleItemInfo item in holder.ItemList)
			{
				if (item.Name != null && item.Name.StartsWith("soundbanks$windows$", StringComparison.OrdinalIgnoreCase))
				{
					item.Name = "soundbanks$android$" + item.Name.Substring("soundbanks$windows$".Length);
					num++;
				}
			}
		}
		OffServerLog.Info("[ABM] android sound bank names fixed: " + num);
	}

	private void LoadAssetBundeFiles(AssetBundleInfoHolder holder)
	{
		UnloadPrecachedAssets();
		foreach (KeyValuePair<string, AssetBundleFile> item in _assetBundleFileDict)
		{
			if (item.Value.Bundle != null)
			{
				item.Value.Bundle.Unload(unloadAllLoadedObjects: false);
			}
		}
		_assetBundleFileDict.Clear();
		_prerequisites.Clear();
		for (int i = 0; i < holder.FileList.Count; i++)
		{
			AssetBundleFile assetBundleFile = new AssetBundleFile(holder.FileList[i]);
			_assetBundleFileDict.Add(assetBundleFile.Name, assetBundleFile);
			if (IsPrerequsite(assetBundleFile))
			{
				_prerequisites.Add(assetBundleFile);
			}
		}
		LoadDependencies(holder);
	}

	private void LoadDependencies(AssetBundleInfoHolder holder)
	{
		for (int i = 0; i < holder.FileList.Count; i++)
		{
			AssetBundleFileInfo assetBundleFileInfo = holder.FileList[i];
			if (assetBundleFileInfo.Dependencies != null)
			{
				AssetBundleFile assetBundleFile = _assetBundleFileDict[assetBundleFileInfo.Name];
				assetBundleFile.Dependencies = new AssetBundleFile[assetBundleFileInfo.Dependencies.Length];
				for (int j = 0; j < assetBundleFileInfo.Dependencies.Length; j++)
				{
					string key = assetBundleFileInfo.Dependencies[j];
					_assetBundleFileDict.TryGetValue(key, out assetBundleFile.Dependencies[j]);
				}
			}
		}
	}

	private void LoadAssetBundleItems(AssetBundleInfoHolder holder)
	{
		_assetBundleItemDict.Clear();
		for (int i = 0; i < holder.ItemList.Count; i++)
		{
			AssetBundleItemInfo assetBundleItemInfo = holder.ItemList[i];
			string assetName = AssetBundleItemInfo.GetAssetName(assetBundleItemInfo.Name);
			string key = ((!assetBundleItemInfo.SavePerDirectory) ? assetBundleItemInfo.Name : AssetBundleItemInfo.GetParentName(assetBundleItemInfo.Name));
			AssetBundleFile assetBundleFile = _assetBundleFileDict.Get(key);
			if (assetBundleFile != null)
			{
				AssetBundleItem value = new AssetBundleItem(assetName, assetBundleFile);
				_assetBundleItemDict.Add(assetBundleItemInfo.Name, value);
			}
		}
	}

	private IEnumerator CoLoadPreloadFile()
	{
		CurrentStatus = Status.LoadPreload;
		if (_preloadBundle != null)
		{
			_preloadBundle.Unload(unloadAllLoadedObjects: false);
			_preloadBundle = null;
		}
		if (_localPreloadPath != null)
		{
			AssetBundleCreateRequest localRequest = AssetBundle.LoadFromFileAsync(_localPreloadPath);
			_activeLocalLoads.Add(localRequest);
			yield return localRequest;
			_activeLocalLoads.Remove(localRequest);
			_preloadBundle = localRequest.assetBundle;
			if (_preloadBundle != null)
			{
				OffServerBundleEmbed.NoteLoad("preload.bundle", local: true);
				while (!Caching.ready)
				{
					yield return null;
				}
				CurrentStatus = Status.Ready;
				yield break;
			}
			UnityEngine.Debug.LogError("[ABM] embed: preload in the game did not open: " + _localPreloadPath + (_embedRemoteOk ? " - trying the server" : ""));
			if (!_embedRemoteOk)
			{
				CurrentStatus = Status.Failed;
				yield break;
			}
		}
		using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(CreateTargetUrl("preload.bundle", _preloadCrc), _preloadHash))
		{
			yield return request.SendWebRequest();
			DownloadHandlerAssetBundle downloadHandlerAssetBundle = request.downloadHandler as DownloadHandlerAssetBundle;
			if (request.result == UnityWebRequest.Result.ConnectionError || downloadHandlerAssetBundle == null)
			{
				CurrentStatus = Status.Failed;
				yield break;
			}
			_preloadBundle = downloadHandlerAssetBundle.assetBundle;
			if (_preloadBundle == null)
			{
				CurrentStatus = Status.Failed;
			}
		}
		while (!Caching.ready)
		{
			yield return null;
		}
		CurrentStatus = Status.Ready;
	}

	private string CreateTargetUrl(string fileName, string crc)
	{
		return AssetBundleItemInfo.GetCrcName(fileName, crc, AssetBundleLoadPath);
	}

	private IEnumerator CoLoadFile(AssetBundleFile file, Action<float> progressCallback = null, bool isPreload = false)
	{
		if (file.CurrentStatus == AssetBundleFile.Status.Loading)
		{
			yield break;
		}
		file.CurrentStatus = AssetBundleFile.Status.Loading;
		while (!_requestLimiter.Acquire())
		{
			yield return null;
		}
		try
		{
			if (_localFiles.TryGetValue(file.Name, out var localPath))
			{
				if (isPreload)
				{
					file.CurrentStatus = AssetBundleFile.Status.None;
					yield break;
				}
				AssetBundleCreateRequest localRequest = AssetBundle.LoadFromFileAsync(localPath);
				_activeLocalLoads.Add(localRequest);
				while (!localRequest.isDone)
				{
					yield return null;
					progressCallback?.Invoke(localRequest.progress);
				}
				_activeLocalLoads.Remove(localRequest);
				if (localRequest.assetBundle != null)
				{
					if (file.Bundle != null)
					{
						file.Bundle.Unload(unloadAllLoadedObjects: false);
					}
					file.Bundle = localRequest.assetBundle;
					OffServerBundleEmbed.NoteLoad(file.Name, local: true);
					yield break;
				}
				UnityEngine.Debug.LogError("[ABM] embed: bundle in the game did not open: " + localPath + (_embedRemoteOk ? " - trying the server" : ""));
				if (!_embedRemoteOk)
				{
					file.CurrentStatus = AssetBundleFile.Status.Failed;
					file.Bundle = null;
					yield break;
				}
				_localFiles.Remove(file.Name);
			}
			string uri = CreateTargetUrl(file.Name, file.Crc);
			using UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(uri, file.Hash);
			_activeRequests.Add(request);
			request.SendWebRequest();
			while (!request.isDone)
			{
				yield return null;
				progressCallback?.Invoke(request.downloadProgress);
			}
			_activeRequests.Remove(request);
			DownloadHandlerAssetBundle downloadHandlerAssetBundle = request.downloadHandler as DownloadHandlerAssetBundle;
			if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || downloadHandlerAssetBundle == null || downloadHandlerAssetBundle.assetBundle == null)
			{
				string[] obj = new string[12]
				{
					"[ABM] bundle download failed: ",
					uri,
					" http=",
					request.responseCode.ToString(),
					" error=",
					request.error,
					" handler=",
					(downloadHandlerAssetBundle == null) ? "null" : ((downloadHandlerAssetBundle.assetBundle == null) ? "no bundle" : "ok"),
					" size=",
					null,
					null,
					null
				};
				int size = file.Size;
				obj[9] = size.ToString();
				obj[10] = " cacheReady=";
				obj[11] = Caching.ready.ToString();
				UnityEngine.Debug.LogError(string.Concat(obj));
				file.CurrentStatus = AssetBundleFile.Status.Failed;
				file.Bundle = null;
				yield break;
			}
			if (isPreload && !file.Queued)
			{
				file.CurrentStatus = AssetBundleFile.Status.None;
				file.Bundle = null;
				yield break;
			}
			if (file.Bundle != null)
			{
				file.Bundle.Unload(unloadAllLoadedObjects: false);
				file.Bundle = null;
			}
			file.Bundle = downloadHandlerAssetBundle.assetBundle;
			if (file.Bundle == null)
			{
				file.CurrentStatus = AssetBundleFile.Status.Failed;
			}
			else if (_embedMode)
			{
				OffServerBundleEmbed.NoteLoad(file.Name, local: false);
			}
		}
		finally
		{
			AssetBundleManager assetBundleManager = this;
			if (file.Bundle == null && file.CurrentStatus == AssetBundleFile.Status.Loading)
			{
				file.CurrentStatus = AssetBundleFile.Status.Failed;
			}
			assetBundleManager._requestLimiter.Release();
		}
	}

	private LoadStatus TryLoadBundleFile(AssetBundleFile file)
	{
		file.Queued = true;
		if (file.Dependencies != null)
		{
			LoadStatus loadStatus = LoadStatus.Complete;
			for (int i = 0; i < file.Dependencies.Length; i++)
			{
				AssetBundleFile file2 = file.Dependencies[i];
				LoadStatus loadStatus2 = TryLoadBundleFile(file2);
				if (loadStatus2 != LoadStatus.Complete && loadStatus != LoadStatus.Failed)
				{
					loadStatus = loadStatus2;
				}
			}
			if (loadStatus != LoadStatus.Complete)
			{
				return loadStatus;
			}
		}
		switch (file.CurrentStatus)
		{
		case AssetBundleFile.Status.None:
			StartCoroutine(CoLoadFile(file));
			break;
		default:
			file.CurrentStatus = AssetBundleFile.Status.None;
			return LoadStatus.Failed;
		case AssetBundleFile.Status.Loading:
			break;
		}
		if (!(file.Bundle != null))
		{
			return LoadStatus.Wait;
		}
		return LoadStatus.Complete;
	}

	public bool RequestAsset(string assetPath, Type type, Action<UnityEngine.Object> callback)
	{
		if (string.IsNullOrEmpty(assetPath))
		{
			callback(null);
			return false;
		}
		if (CurrentStatus != Status.Ready && Application.isPlaying)
		{
			callback(null);
			return false;
		}
		string uniqueName = AssetBundleItemInfo.GetUniqueName(assetPath);
		AssetBundleItem assetBundleItem = _assetBundleItemDict.Get(uniqueName);
		if (assetBundleItem == null)
		{
			UnityEngine.Object obj = OffServerBundleFallback.Load(uniqueName, type);
			callback(obj);
			return obj != null;
		}
		AssetBundleItemToLoad assetBundleItemToLoad = null;
		int i = 0;
		for (int count = _itemToLoad.Count; i < count; i++)
		{
			if (_itemToLoad[i].Item == assetBundleItem && _itemToLoad[i].ReqType == type)
			{
				assetBundleItemToLoad = _itemToLoad[i];
				break;
			}
		}
		if (assetBundleItemToLoad == null)
		{
			assetBundleItemToLoad = new AssetBundleItemToLoad();
			assetBundleItemToLoad.Item = assetBundleItem;
			assetBundleItemToLoad.ReqType = type;
			assetBundleItemToLoad.UniqueName = uniqueName;
			_itemToLoad.Add(assetBundleItemToLoad);
		}
		AssetBundleItemToLoad assetBundleItemToLoad2 = assetBundleItemToLoad;
		assetBundleItemToLoad2.Finished = (Action<UnityEngine.Object>)Delegate.Combine(assetBundleItemToLoad2.Finished, callback);
		assetBundleItemToLoad.RetryCount = 0;
		return true;
	}

	public void UnloadAsset(string assetPath)
	{
		if ((!Application.isEditor || (UseBundle && Application.isPlaying)) && CurrentStatus == Status.Ready)
		{
			string uniqueName = AssetBundleItemInfo.GetUniqueName(assetPath);
			AssetBundleItem assetBundleItem = _assetBundleItemDict.Get(uniqueName);
			if (assetBundleItem != null && assetBundleItem.Parent.Bundle != null)
			{
				assetBundleItem.Parent.Bundle.Unload(unloadAllLoadedObjects: true);
				assetBundleItem.Parent.Bundle = null;
				assetBundleItem.Parent.CurrentStatus = AssetBundleFile.Status.None;
				assetBundleItem.Request = null;
				assetBundleItem.Asset = null;
			}
		}
	}

	public bool Contains(string assetPath)
	{
		string uniqueName = AssetBundleItemInfo.GetUniqueName(assetPath);
		return _assetBundleItemDict.ContainsKey(uniqueName);
	}

	public void ClearRequests()
	{
		_requestLimiter.Clear();
		for (int num = _itemToLoad.Count - 1; num >= 0; num--)
		{
			_itemToLoad[num].Finished(null);
		}
		_itemToLoad.Clear();
	}

	public void ClearAll()
	{
		_requestLimiter.Clear();
		_itemToLoad.Clear();
		StopAllCoroutines();
		foreach (KeyValuePair<string, AssetBundleItem> item in _assetBundleItemDict)
		{
			item.Value.Request = null;
		}
		for (int i = 0; i < _activeRequests.Count; i++)
		{
			UnityWebRequest unityWebRequest = _activeRequests[i];
			try
			{
				DownloadHandlerAssetBundle downloadHandlerAssetBundle = unityWebRequest.downloadHandler as DownloadHandlerAssetBundle;
				if (unityWebRequest.isDone && unityWebRequest.result == UnityWebRequest.Result.Success && downloadHandlerAssetBundle != null && downloadHandlerAssetBundle.assetBundle != null)
				{
					downloadHandlerAssetBundle.assetBundle.Unload(unloadAllLoadedObjects: false);
				}
				unityWebRequest.Abort();
				unityWebRequest.Dispose();
			}
			catch (Exception)
			{
			}
		}
		_activeRequests.Clear();
		for (int j = 0; j < _activeLocalLoads.Count; j++)
		{
			AssetBundleCreateRequest assetBundleCreateRequest = _activeLocalLoads[j];
			if (assetBundleCreateRequest.isDone)
			{
				if (assetBundleCreateRequest.assetBundle != null)
				{
					assetBundleCreateRequest.assetBundle.Unload(unloadAllLoadedObjects: false);
				}
				continue;
			}
			assetBundleCreateRequest.completed += delegate(AsyncOperation op)
			{
				AssetBundle assetBundle = ((AssetBundleCreateRequest)op).assetBundle;
				if (assetBundle != null)
				{
					assetBundle.Unload(unloadAllLoadedObjects: false);
				}
			};
		}
		_activeLocalLoads.Clear();
		foreach (KeyValuePair<string, AssetBundleFile> item2 in _assetBundleFileDict)
		{
			item2.Value.Queued = false;
			if (item2.Value.Bundle == null)
			{
				item2.Value.CurrentStatus = AssetBundleFile.Status.None;
			}
		}
		UnloadPrecachedAssets();
		if (KeepAssets && IsMemoryTight())
		{
			ReleaseKeptAssets("memory budget");
		}
		CurrentStatus = ((CurrentStatus == Status.Ready) ? Status.Cached : Status.None);
		this.BackgroundDownloadStarted = null;
		this.BackgroundDownloadCompleted = null;
	}

	private void ReleaseKeptAssets(string reason)
	{
		int num = 0;
		foreach (KeyValuePair<string, AssetBundleItem> item in _assetBundleItemDict)
		{
			if (item.Value.Asset != null)
			{
				num++;
			}
			item.Value.Asset = null;
		}
		if (num > 0)
		{
			OffServerLog.Info("[ABM] released " + num + " kept assets (" + reason + ")");
		}
	}

	private static bool IsMemoryTight()
	{
		long num = Profiler.GetTotalAllocatedMemoryLong() + (long)Texture.currentTextureMemory;
		long num2 = (long)SystemInfo.systemMemorySize * 1048576L * 35 / 100;
		if (num2 > 0)
		{
			return num > num2;
		}
		return false;
	}

	public void PrecacheAssets()
	{
		UnloadPrecachedAssets();
		List<string> precachedAssets = new List<string>();
		precachedAssets.AddRange(_playerClipPath);
		precachedAssets.Add("Water/River/River.prefab");
		string[] array = new string[2] { "Water/Ocean/Ocean.prefab", "Water/Lake/Lake.prefab" };
		if (Platform.Instance.UsePCRenderer)
		{
			precachedAssets.AddRange(array.Select(GetHqAssetPath));
		}
		else
		{
			precachedAssets.AddRange(array);
		}
		_precachedAssetsCount = precachedAssets.Count;
		for (int i = 0; i < precachedAssets.Count; i++)
		{
			int index = i;
			RequestAsset(precachedAssets[index], typeof(UnityEngine.Object), delegate(UnityEngine.Object asset)
			{
				if (asset != null)
				{
					_precachedAssets[precachedAssets[index]] = asset;
				}
				else
				{
					Debug.LogError("Precache Asset is invalid : " + precachedAssets[index]);
				}
			});
		}
	}

	private void UnloadPrecachedAssets()
	{
		_precachedAssetsCount = 0;
		_precachedAssets.Clear();
	}

	public bool IsPrecachedAssetsReady()
	{
		return _precachedAssetsCount == _precachedAssets.Count;
	}

	public AnimationClipResource GetPlayerClip(bool male)
	{
		return GetPrecachedAsset<AnimationClipResource>(_playerClipPath[(!male) ? 1u : 0u]);
	}

	public T GetPrecachedAsset<T>(string path) where T : UnityEngine.Object
	{
		return _precachedAssets.Get(path) as T;
	}

	public void StartBackgroundDownloading(Action<int, int, string> progressCallback, Action<float> detailedProgressCallback, Action<bool> completeCallback)
	{
		if (_isBackgroundDownloading)
		{
			return;
		}
		_isBackgroundDownloading = true;
		List<AssetBundleFile> list = new List<AssetBundleFile>(_assetBundleFileDict.Values);
		list.Sort(delegate(AssetBundleFile a, AssetBundleFile b)
		{
			if (a.Priority == b.Priority)
			{
				return 0;
			}
			return (a.Priority < b.Priority) ? 1 : (-1);
		});
		StartCoroutine(CoBackgroundDownload(list, allow3G: false, progressCallback, detailedProgressCallback, completeCallback));
	}

	public void StopBackgroundDownloading()
	{
		_isBackgroundDownloading = false;
	}

	public void StartPrerequisiteLoading(Action<int, int, string> progressCallback, Action<float> detailedProgressCallback, Action<bool> completeCallback, Action<int, int> filterCallback)
	{
		if (!_isBackgroundDownloading)
		{
			_isBackgroundDownloading = true;
			StartCoroutine(CoBackgroundDownload(_prerequisites, allow3G: true, progressCallback, detailedProgressCallback, completeCallback, filterCallback));
		}
	}

	private List<AssetBundleFile> FilterCached(List<AssetBundleFile> list, out int sum)
	{
		List<AssetBundleFile> list2 = new List<AssetBundleFile>();
		sum = 0;
		int i = 0;
		for (int count = list.Count; i < count; i++)
		{
			AssetBundleFile assetBundleFile = list[i];
			if (!_localFiles.ContainsKey(assetBundleFile.Name) && !Caching.IsVersionCached(CreateTargetUrl(assetBundleFile.Name, assetBundleFile.Crc), assetBundleFile.Hash))
			{
				list2.Add(assetBundleFile);
				sum += assetBundleFile.Size;
			}
		}
		sum = Mathf.CeilToInt((float)sum / 1000000f);
		return list2;
	}

	private IEnumerator CoBackgroundDownload(List<AssetBundleFile> list, bool allow3G, Action<int, int, string> progressCallback, Action<float> detailedProgressCallback, Action<bool> completeCallback, Action<int, int> filterCallback = null)
	{
		if (this.BackgroundDownloadStarted != null)
		{
			this.BackgroundDownloadStarted();
		}
		if (!UseBundle)
		{
			_isBackgroundDownloading = false;
			completeCallback?.Invoke(obj: true);
			yield break;
		}
		if (filterCallback != null)
		{
			list = FilterCached(list, out var sum);
			filterCallback(sum, list.Count);
		}
		int count = list.Count;
		int retryCount = 0;
		bool succeed = true;
		for (int i = 0; i < count; i++)
		{
			if (!_isBackgroundDownloading)
			{
				break;
			}
			AssetBundleFile file = list[i];
			if (_localFiles.ContainsKey(file.Name))
			{
				retryCount = 0;
				continue;
			}
			string url = CreateTargetUrl(file.Name, file.Crc);
			if (Caching.IsVersionCached(url, file.Hash))
			{
				retryCount = 0;
				continue;
			}
			progressCallback?.Invoke(i + 1, retryCount, file.Name);
			if (allow3G || Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork || IsPrerequsite(file))
			{
				yield return StartCoroutine(CoLoadFile(file, detailedProgressCallback, isPreload: true));
				if (Caching.IsVersionCached(url, file.Hash) || file.CurrentStatus == AssetBundleFile.Status.None)
				{
					continue;
				}
				i--;
				if (file.CurrentStatus == AssetBundleFile.Status.Failed)
				{
					retryCount++;
					if (retryCount >= _maxRetryCount)
					{
						succeed = false;
						break;
					}
				}
				yield return InternetCheckWaitTime;
			}
			else
			{
				yield return InternetCheckWaitTime;
			}
		}
		_isBackgroundDownloading = false;
		completeCallback?.Invoke(succeed);
		if (this.BackgroundDownloadCompleted != null)
		{
			this.BackgroundDownloadCompleted();
		}
	}

	public static string GetHqAssetPath(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
		string extension = Path.GetExtension(path);
		return directoryName + "/" + fileNameWithoutExtension + "_HQ" + extension;
	}
}

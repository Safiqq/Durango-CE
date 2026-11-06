using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class CommunityRebornTitle
{
	[Serializable]
	private class Config
	{
		public bool video_has_logo;

		public string video_align = "center";

		public bool s02_layout;
	}

	public const string Folder = "Reborn";

	private static Config _config;

	private static bool _loading;

	private static readonly Dictionary<UITexture, Vector2Int> _logoBoxes = new Dictionary<UITexture, Vector2Int>();

	public const string LoadingLogoSprite = "bg_loading_ment";

	private static Material _loadingLogoMaterial;

	public static bool Loaded { get; private set; }

	public static Texture2D Logo { get; private set; }

	public static Texture2D Watermark { get; private set; }

	public static Texture2D Background { get; private set; }

	public static bool S02Layout
	{
		get
		{
			if (_config != null)
			{
				return _config.s02_layout;
			}
			return false;
		}
	}

	public static bool VideoHasLogo
	{
		get
		{
			if (_config != null)
			{
				return _config.video_has_logo;
			}
			return false;
		}
	}

	public static bool AlignRight
	{
		get
		{
			if (_config != null)
			{
				return string.Equals(_config.video_align, "right", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}

	public static IEnumerator CoLoad()
	{
		if (!Loaded && !_loading)
		{
			_loading = true;
			GameObject obj = new GameObject("CommunityRebornTitle")
			{
				hideFlags = HideFlags.HideInHierarchy
			};
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<CommunityRebornTitleHost>().StartCoroutine(CoReadFiles());
		}
		float giveUp = Time.realtimeSinceStartup + 10f;
		while (!Loaded)
		{
			if (Time.realtimeSinceStartup > giveUp)
			{
				Loaded = true;
				CommunityLog.Info("[Community] reborn title: StreamingAssets/Reborn not read in 10 s - originals");
				break;
			}
			yield return null;
		}
	}

	private static IEnumerator CoReadFiles()
	{
		byte[] bytes = null;
		yield return CoRead("title.json", delegate(byte[] data)
		{
			bytes = data;
		});
		if (bytes != null)
		{
			try
			{
				_config = JsonUtility.FromJson<Config>(Encoding.UTF8.GetString(bytes).TrimStart('\ufeff'));
			}
			catch (Exception ex)
			{
				_config = null;
				CommunityLog.Info("[Community] reborn title.json unreadable (" + ex.Message + ") - original title");
			}
		}
		bytes = null;
		yield return CoRead("logo.png", delegate(byte[] data)
		{
			bytes = data;
		});
		Logo = MakeTexture(bytes, "reborn_logo");
		bytes = null;
		yield return CoRead("img_logo.png", delegate(byte[] data)
		{
			bytes = data;
		});
		Watermark = MakeTexture(bytes, "reborn_img_logo");
		bytes = null;
		yield return CoRead("background.png", delegate(byte[] data)
		{
			bytes = data;
		});
		Background = MakeTexture(bytes, "s02_title_background");
		Loaded = true;
		_loading = false;
		CommunityLog.Info("[Community] reborn title: logo=" + Describe(Logo) + " watermark=" + Describe(Watermark) + " video_has_logo=" + VideoHasLogo + " align=" + (AlignRight ? "right" : "center"));
	}

	public static void ApplyTitleLogo(GameObject[] objects, bool isTitleVideo, bool videoFailed)
	{
		if (objects == null)
		{
			return;
		}
		bool flag = (Loaded ? CommunityRebornTitleMath.HideLogoOverlay(VideoHasLogo, isTitleVideo, videoFailed) : isTitleVideo);
		foreach (GameObject gameObject in objects)
		{
			if (gameObject == null)
			{
				continue;
			}
			UITexture[] componentsInChildren = gameObject.GetComponentsInChildren<UITexture>(includeInactive: true);
			foreach (UITexture uITexture in componentsInChildren)
			{
				if (uITexture.gameObject.name != "Logo")
				{
					continue;
				}
				if (Logo != null && uITexture.mainTexture != Logo)
				{
					UseRebornLogo(uITexture);
				}
				if (S02Layout && Logo != null)
				{
					GameObject gameObject2 = GameObject.Find("UI Root/TitleMenuGroup/Contents/MainContentPage");
					UIWidget uIWidget = ((gameObject2 == null) ? null : gameObject2.GetComponent<UIWidget>());
					if (uIWidget != null && uIWidget.width > 0)
					{
						int num = Mathf.RoundToInt(Mathf.Min((float)uIWidget.width * 0.38f, 590f));
						int num2 = Mathf.RoundToInt((float)num * (float)Logo.height / (float)Logo.width);
						uITexture.keepAspectRatio = UIWidget.AspectRatioSource.Free;
						uITexture.SetAnchor(uIWidget.gameObject, 0.5f, -num / 2, 0.93f, -num2, 0.5f, num / 2, 0.93f, 0);
						uITexture.UpdateAnchors();
					}
				}
				if (uITexture.enabled == flag)
				{
					uITexture.enabled = !flag;
					CommunityLog.Info("[Community] reborn title logo overlay " + (flag ? "hidden (the video shows the logo)" : "shown") + " · " + Describe(uITexture.mainTexture) + " " + uITexture.width + "x" + uITexture.height);
				}
			}
		}
	}

	public static bool IsLoadingLogo(UISprite sprite)
	{
		if (sprite != null && !string.IsNullOrEmpty(sprite.spriteName))
		{
			return sprite.spriteName.StartsWith("bg_loading_ment", StringComparison.Ordinal);
		}
		return false;
	}

	public static IEnumerator CoLoadingLogo(UISprite sprite)
	{
		yield return CoLoad();
		if (sprite != null && Watermark != null)
		{
			UseRebornLoadingLogo(sprite);
		}
	}

	private static void UseRebornLoadingLogo(UISprite sprite)
	{
		Transform cachedTransform = sprite.cachedTransform;
		Transform transform = cachedTransform.Find("RebornLogo");
		UITexture uITexture = ((transform != null) ? transform.GetComponent<UITexture>() : null);
		if (uITexture == null)
		{
			GameObject gameObject = new GameObject("RebornLogo");
			gameObject.layer = sprite.gameObject.layer;
			gameObject.transform.SetParent(cachedTransform, worldPositionStays: false);
			uITexture = gameObject.AddComponent<UITexture>();
			CommunityLog.Info("[Community] reborn loading logo: " + ((cachedTransform.parent != null) ? (cachedTransform.parent.name + "/") : "") + cachedTransform.name + " (" + sprite.spriteName + ")");
		}
		uITexture.cachedTransform.localPosition = Vector3.zero;
		uITexture.cachedTransform.localRotation = Quaternion.identity;
		uITexture.cachedTransform.localScale = Vector3.one;
		if (_loadingLogoMaterial == null)
		{
			Shader shader = Shader.Find("Durango/NGUI/Transparent");
			if (shader != null)
			{
				_loadingLogoMaterial = new Material(shader);
				_loadingLogoMaterial.name = "reborn_loading_logo";
			}
		}
		if (_loadingLogoMaterial != null)
		{
			uITexture.material = _loadingLogoMaterial;
		}
		uITexture.mainTexture = Watermark;
		CommunityRebornTitleMath.FitInside(sprite.width, sprite.height, Watermark.width, Watermark.height, out var width, out var height);
		uITexture.pivot = sprite.pivot;
		uITexture.depth = sprite.depth;
		uITexture.color = Color.white;
		uITexture.width = width;
		uITexture.height = height;
		uITexture.enabled = true;
		sprite.enabled = false;
	}

	public static Texture WatermarkFor(Texture original, int boxWidth, int boxHeight, out int width, out int height)
	{
		width = boxWidth;
		height = boxHeight;
		if (Watermark == null)
		{
			return original;
		}
		CommunityRebornTitleMath.FitInside(boxWidth, boxHeight, Watermark.width, Watermark.height, out width, out height);
		return Watermark;
	}

	private static void UseRebornLogo(UITexture texture)
	{
		if (!_logoBoxes.TryGetValue(texture, out var value))
		{
			List<UITexture> list = new List<UITexture>();
			foreach (UITexture key in _logoBoxes.Keys)
			{
				if (key == null)
				{
					list.Add(key);
				}
			}
			foreach (UITexture item in list)
			{
				_logoBoxes.Remove(item);
			}
			value = new Vector2Int(texture.width, texture.height);
			_logoBoxes[texture] = value;
		}
		UILocalizeWidget component = texture.GetComponent<UILocalizeWidget>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		CommunityRebornTitleMath.FitInside(value.x, value.y, Logo.width, Logo.height, out var width, out var height);
		UIWidget.AspectRatioSource keepAspectRatio = texture.keepAspectRatio;
		texture.keepAspectRatio = UIWidget.AspectRatioSource.Free;
		texture.mainTexture = Logo;
		texture.width = width;
		texture.height = height;
		texture.aspectRatio = (float)width / (float)height;
		texture.keepAspectRatio = keepAspectRatio;
	}

	private static IEnumerator CoRead(string name, Action<byte[]> done)
	{
		string text = Application.streamingAssetsPath + "/Reborn/" + name;
		if (!text.Contains("://"))
		{
			if (!File.Exists(text))
			{
				yield break;
			}
			text = "file:///" + text.Replace('\\', '/').TrimStart('/');
		}
		UnityWebRequest request;
		try
		{
			request = UnityWebRequest.Get(text);
		}
		catch (Exception ex)
		{
			CommunityLog.Info("[Community] reborn " + name + " unreadable (" + ex.Message + ") - original kept");
			yield break;
		}
		using (request)
		{
			yield return request.SendWebRequest();
			byte[] array = ((request.result == UnityWebRequest.Result.Success) ? request.downloadHandler.data : null);
			if (array != null && array.Length != 0)
			{
				done(array);
			}
		}
	}

	private static Texture2D MakeTexture(byte[] bytes, string name)
	{
		if (bytes == null)
		{
			return null;
		}
		Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
		texture2D.name = name;
		if (!texture2D.LoadImage(bytes, markNonReadable: true))
		{
			UnityEngine.Object.Destroy(texture2D);
			CommunityLog.Info("[Community] reborn " + name + " is not a PNG/JPG - original kept");
			return null;
		}
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
		return texture2D;
	}

	private static string Describe(Texture texture)
	{
		if (!(texture == null))
		{
			return texture.name + " " + texture.width + "x" + texture.height;
		}
		return "original";
	}
}

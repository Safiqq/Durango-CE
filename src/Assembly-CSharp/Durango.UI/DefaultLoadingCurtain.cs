using System;
using System.Collections;
using Durango.Logic.Clusters;
using Durango.Logic.Encyclopedia;
using Durango.Logic.Explore;
using Durango.Terrain;
using Durango.Utils;
using L10N;
using Messages;
using Shared.Ability;
using UnityEngine;
using Yaml;
using Yaml.Util;

namespace Durango.UI;

public class DefaultLoadingCurtain : LoadingCurtainBase
{
	[Serializable]
	private struct RegionInfo
	{
		public UIWidget Parent;

		public Transform Icon;

		public UILabel Lv;

		public UILabel Region;

		public UIWidget SubtitlesWidget;

		public ListObjectPool Nodes;
	}

	[Serializable]
	private struct GameTipInfo
	{
		public UIWidget Parent;

		public UILabel Contents;

		public void Set(string text)
		{
			Contents.text = text;
		}
	}

	private const int MinimumUnstableFactor = 2;

	private const float CurtainWaitingTime = 10f;

	[SerializeField]
	private RegionInfo _regionInfo;

	[SerializeField]
	private GameTipInfo _gameTipInfo;

	[SerializeField]
	private UITexture _bg;

	[SerializeField]
	private UILabel _continueLabel;

	[SerializeField]
	private GameObject _loadingIcon;

	private bool _isRegionLoaded;

	private bool _isTap;

	private string _continueOriginalText;

	private bool _progressShown;

	private UISprite _progressBar;

	private int _peakPendingAssets;

	private void OnEnable()
	{
		UICamera.onPress = (UICamera.BoolDelegate)Delegate.Combine(UICamera.onPress, new UICamera.BoolDelegate(OnTouchScreen));
		UICamera.onKey = (UICamera.KeyCodeDelegate)Delegate.Combine(UICamera.onKey, new UICamera.KeyCodeDelegate(OnPressAnyKey));
		SetState(LoadingState.Open);
		StartCoroutine(CoShowRoutine());
	}

	private void OnDisable()
	{
		UICamera.onPress = (UICamera.BoolDelegate)Delegate.Remove(UICamera.onPress, new UICamera.BoolDelegate(OnTouchScreen));
		UICamera.onKey = (UICamera.KeyCodeDelegate)Delegate.Remove(UICamera.onKey, new UICamera.KeyCodeDelegate(OnPressAnyKey));
	}

	private void Update()
	{
		if (!_isRegionLoaded)
		{
			_isRegionLoaded = TryUpdateRegionInfo();
		}
		UpdateDownloadProgress();
	}

	private void UpdateDownloadProgress()
	{
		if (_loadingIcon == null || _continueLabel == null)
		{
			return;
		}
		if (_loadingIcon.activeSelf)
		{
			if (!_progressShown)
			{
				_continueOriginalText = _continueLabel.text;
				_progressShown = true;
				_continueLabel.gameObject.SetActive(value: true);
				_peakPendingAssets = 0;
				CommunityLoadProgress.EnterIsland();
			}
			float value = CommunityLoadProgress.Tick(CommunityLoadProgress.Stage.Island, IslandLoadProgress());
			UpdateProgressBar(value);
			_continueLabel.text = CommunityLoadProgress.Text();
		}
		else if (_progressShown)
		{
			_progressShown = false;
			_continueLabel.text = _continueOriginalText;
			if (_progressBar != null)
			{
				_progressBar.enabled = false;
			}
		}
	}

	private float IslandLoadProgress()
	{
		float num = 0f;
		if (Durango.Utils.Singleton<TerrainBase>.HasInstance() && TerrainBase.IsPlayerInitialized)
		{
			TerrainBase terrainBase = Durango.Utils.Singleton<TerrainBase>.Instance();
			if (terrainBase.IsReady)
			{
				return 1f;
			}
			int num2 = 0;
			int i = 0;
			for (int num3 = terrainBase.ChunkCount(); i < num3; i++)
			{
				if (terrainBase.GetChunk(i).LoadingStatus == TerrainChunkBase.TerrainChunkLoadingStatus.Loaded)
				{
					num2++;
				}
			}
			int pendingRequestCount = Durango.Utils.Singleton<AssetBundleManager>.Instance().PendingRequestCount;
			_peakPendingAssets = Mathf.Max(_peakPendingAssets, pendingRequestCount);
			float num4 = ((_peakPendingAssets <= 0) ? 0f : (1f - (float)pendingRequestCount / (float)_peakPendingAssets));
			num = 0.1f + 0.6f * Mathf.Clamp01((float)num2 / 9f) + 0.25f * num4;
		}
		return 0.45f + 0.55f * num;
	}

	private void UpdateProgressBar(float value)
	{
		if (_progressBar == null)
		{
			UIAtlas atlas;
			UISpriteData spriteData;
			string spriteName = (ResourceSingleton<UISpriteManager>.Instance().TryGet("bg_skill_gauge", out atlas, out spriteData) ? "bg_skill_gauge" : "bg_white");
			_progressBar = base.gameObject.AddWidget<UISprite>(_continueLabel.depth + 10);
			_progressBar.name = "LoadProgressBar";
			_progressBar.spriteName = spriteName;
			_progressBar.type = UIBasicSprite.Type.Sliced;
			_progressBar.pivot = UIWidget.Pivot.BottomLeft;
			_progressBar.color = CommunityLoadProgress.BarColor;
		}
		UIPanel uIPanel = NGUITools.FindInParents<UIPanel>(base.gameObject);
		if (!(uIPanel == null))
		{
			Vector3[] worldCorners = uIPanel.worldCorners;
			Transform obj = base.transform;
			Vector3 vector = obj.InverseTransformPoint(worldCorners[0]);
			int num = Mathf.RoundToInt((obj.InverseTransformPoint(worldCorners[3]).x - vector.x) * Mathf.Clamp01(value));
			_progressBar.enabled = num >= 2;
			_progressBar.cachedTransform.localPosition = new Vector3(vector.x, vector.y, 0f);
			_progressBar.width = Mathf.Max(2, num);
			_progressBar.height = 18;
		}
	}

	private IEnumerator CoShowRoutine()
	{
		_isRegionLoaded = false;
		base.Widget.alpha = 1f;
		_loadingIcon.gameObject.SetActive(value: true);
		_continueLabel.gameObject.SetActive(value: false);
		_bg.color = Color.black;
		UpdateMemoText();
		yield return null;
		TweenColor.Begin(_bg.gameObject, 1f, PresetColor.LoadingColor);
		TweenAlpha.Begin(_gameTipInfo.Parent.gameObject, 0.5f, 1f).delay = 1f;
		if (_regionInfo.Parent.gameObject.activeSelf)
		{
			_regionInfo.Parent.alpha = 0f;
			TweenAlpha.Begin(_regionInfo.Parent.gameObject, 0.5f, 1f).delay = 2f;
		}
		yield return WaitForChunkLoading();
		if (!LoadingCurtainBase.IsChunkLoadFailed)
		{
			_loadingIcon.gameObject.SetActive(value: false);
			_continueLabel.gameObject.SetActive(value: true);
			_continueLabel.text = ContinueText();
			_continueOriginalText = _continueLabel.text;
			CommunityTransition.Prepare();
			_isTap = false;
			while (!_isTap)
			{
				yield return null;
			}
			bool covered = false;
			CommunityTransition.Cover(delegate
			{
				covered = true;
			});
			while (!covered)
			{
				yield return null;
			}
			SetState(LoadingState.Closing);
			base.Widget.alpha = 0f;
			SetState(LoadingState.Closed);
			CommunityTransition.Reveal();
		}
	}

	private static string ContinueText()
	{
		return CommunityDiscordLink.Lang switch
		{
			"th" => "แตะหน\u0e49าจอเพ\u0e37\u0e48อเข\u0e49าเกาะ",
			"id" => "Ketuk layar untuk masuk ke pulau",
			"ko" => "화면을 터치하면 섬에 입장합니다",
			_ => "Tap the screen to enter the island",
		};
	}

	private void OnTouchScreen(GameObject obj, bool press)
	{
		_isTap = true;
	}

	private void OnPressAnyKey(GameObject go, KeyCode key)
	{
		OnTouchScreen(null, press: true);
	}

	private void UpdateMemoText()
	{
		_gameTipInfo.Parent.alpha = 0f;
		MemoType type = MemoType.Fiction;
		if (LoadingCurtainGroup.LoadingCount > 0)
		{
			type = MemoType.Tooltip;
		}
		_gameTipInfo.Parent.gameObject.SetActive(value: true);
		int randomMemo = MemoSystem.GetRandomMemo(type);
		string text = ((randomMemo != -1) ? MemoSystem.GetMemoFullText(type, randomMemo) : string.Empty);
		_gameTipInfo.Set(text);
	}

	private bool TryUpdateRegionInfo()
	{
		Durango.Logic.Explore.Region region = GameManager.Region;
		if (region.Template == null)
		{
			_regionInfo.Parent.gameObject.SetActive(value: false);
			return false;
		}
		_regionInfo.Parent.gameObject.SetActive(value: true);
		_regionInfo.Parent.alpha = 0f;
		TweenAlpha.Begin(_regionInfo.Parent.gameObject, 0.5f, 1f).delay = 2f;
		Durango.Logic.Explore.Region.InstantiateIcon(_regionInfo.Icon, region.GetEmblem());
		_regionInfo.Lv.text = T._("{0:lv:} {1}", region.Level, LocalizeUtil.Get(region.Role()));
		_regionInfo.Region.text = region.Name;
		Messages.Archipelago? archipelago = GameManager.Archipelago;
		_regionInfo.Nodes.BeginLoad();
		if (archipelago.HasValue && archipelago.Value.UnstableFactor >= 2)
		{
			_regionInfo.Nodes.GetNext().GetComponent<UILabel>().text = $"<em>[icon=icon_unstable_factor] {archipelago.Value.UnstableFactor}</em>";
			_regionInfo.Nodes.GetNext().GetComponent<UILabel>().text = LocalizeUtil.Get(region.MajorBiome());
			Recommends recommends = SingletonDict<int, Recommends>.Instance.Get(archipelago.Value.UnstableFactor);
			Derived derived = Yaml.Util.Singleton<Constants>.Instance.Resistance.TypeByBiome.Get(region.MajorBiome(), Derived.Invalid);
			if (derived != Derived.Invalid && recommends != null)
			{
				string text = LocalizeUtil.Get(derived);
				string text2 = LocalizeUtil.FormatLevel(recommends.ResistanceLevel);
				_regionInfo.Nodes.GetNext().GetComponent<UILabel>().text = text + " " + text2;
			}
		}
		else
		{
			_regionInfo.Nodes.GetNext().GetComponent<UILabel>().text = LocalizeUtil.Get(region.MajorBiome());
		}
		_regionInfo.Nodes.EndLoad();
		UIUtility.WidgetsReposition(_regionInfo.Nodes, _regionInfo.SubtitlesWidget, Vector3.right, 18f);
		return true;
	}
}

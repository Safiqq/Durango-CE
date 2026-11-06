using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.UI.Control;
using JetBrains.Annotations;
using Messages;
using OffServer.ClanPillar;
using Shared.Region;
using UnityEngine;
using Yaml;

namespace Durango.UI;

public class WorldRoutesViewer : AnimationWidget
{
	public static readonly Biome[][] BiomeGrid = new Biome[6][]
	{
		new Biome[1] { Biome.Desert },
		new Biome[2]
		{
			Biome.TropicalForest,
			Biome.SwampMud
		},
		new Biome[1] { Biome.Grassland },
		new Biome[1],
		new Biome[2]
		{
			Biome.Tundra,
			Biome.SnowField
		},
		new Biome[1] { Biome.Volcanic }
	};

	private const float RandomPositionOffset = 10f;

	[SerializeField]
	private ExploreRegionNode _regionNodebase;

	[SerializeField]
	private ExploreAreaNode _areaNodeBase;

	[SerializeField]
	private KWidgetScrollView _scrollView;

	[SerializeField]
	private WorldRoutesBeginnerStableArea _beginnerStableArea;

	[SerializeField]
	private WorldRoutesUnstableArea _beginnerUnstableArea;

	[SerializeField]
	private WorldRoutesExpertStableArea _expertStableArea;

	[SerializeField]
	private WorldRoutesUnstableArea _expertUnstableArea;

	[SerializeField]
	private SharedRoutesInfo _sharedRoutesInfo;

	[SerializeField]
	private RecentlyVisit _recentlyVisit;

	[SerializeField]
	private GameObject _currentCursor;

	[SerializeField]
	private UIPanel[] _backgroundPanels;

	[SerializeField]
	private WorldRoutesBackground _background;

	[SerializeField]
	private UISprite _pointSpriteBase;

	[SerializeField]
	private WorldRoutesViewerShadow _shadowSprite;

	[SerializeField]
	private TweenerPlayer _selectingEffect;

	[SerializeField]
	private UIWidget _sunsetEffects;

	[SerializeField]
	private SunsetClouds _sunsetClouds;

	private float _scrollOffset;

	private int _randomSeed;

	private bool _isValidCurrentCursor;

	private ListObjectPool<UISprite> _pointSprites;

	private EstateLicenses _estateLicenses;

	private PersonalRegionInfo _personalRegionInfo;

	private bool _resetViewerScroll;

	private bool _isInit;

	private readonly List<GameObject> _riftGateHidden = new List<GameObject>();

	private bool _hideRiftAreas;

	public ExploreRegionNode RegionNodeBase => _regionNodebase;

	public ExploreAreaNode AreaNodeBase => _areaNodeBase;

	public UIScrollView ScrollView => _scrollView.ScrollView;

	private void Init()
	{
		if (!_isInit)
		{
			_isInit = true;
			List<UIWidget> widgets = _scrollView.Widgets;
			int i = 0;
			for (int size = KUtility.GetSize(widgets); i < size; i++)
			{
				widgets[i].gameObject.SetActive(value: true);
			}
			_beginnerUnstableArea.Init(-1, 39);
			_expertUnstableArea.Init(40, -1);
			_pointSprites = new ListObjectPool<UISprite>();
			_pointSprites.BaseObject = _pointSpriteBase;
			_pointSprites.UseBase = true;
			_pointSprites.Clear();
			_regionNodebase.gameObject.SetActive(value: false);
			_areaNodeBase.gameObject.SetActive(value: false);
			_shadowSprite.Initialize(_background.GetHashCode());
		}
	}

	private void OnEnable()
	{
		_scrollOffset = -1f;
		_selectingEffect.gameObject.SetActive(value: false);
	}

	private void LateUpdate()
	{
		float currentOffset = _scrollView.CurrentOffset;
		if (_scrollOffset != currentOffset)
		{
			_scrollOffset = currentOffset;
			SyncOffset();
		}
	}

	private void SyncOffset()
	{
		float scrollOffset = _scrollOffset;
		Vector3 localPosition = Vector3.left * scrollOffset;
		for (int i = 0; i < _backgroundPanels.Length; i++)
		{
			_backgroundPanels[i].transform.localPosition = localPosition;
			_backgroundPanels[i].clipOffset = Vector2.right * scrollOffset;
		}
	}

	private void UpdateLayout()
	{
		List<UIWidget> widgets = _scrollView.Widgets;
		int i = 0;
		for (int size = KUtility.GetSize(widgets); i < size; i++)
		{
			widgets[i].height = base.Widget.height;
		}
		global::System.Random rand = new global::System.Random(_randomSeed);
		if (_beginnerStableArea.gameObject.activeSelf)
		{
			_beginnerStableArea.UpdateLayout(rand);
		}
		_beginnerUnstableArea.UpdateLayout(rand);
		if (_expertStableArea.gameObject.activeSelf)
		{
			_expertStableArea.UpdateLayout(rand);
		}
		_expertUnstableArea.UpdateLayout(rand);
		ArrangePillarRoutes();
		if (_hideRiftAreas)
		{
			HideRiftAreas();
		}
		_scrollView.UpdateLayout();
		UpdateSunsetEffects();
		UpdateBackground();
		if (_resetViewerScroll)
		{
			_resetViewerScroll = false;
			if (_isValidCurrentCursor)
			{
				MoveScrollTo(_currentCursor.transform.position);
			}
		}
		ApplyShadow();
	}

	private void ArrangePillarRoutes()
	{
		List<ExploreRegionNode> list = _expertStableArea.PillarNodes();
		if (!_expertStableArea.gameObject.activeSelf)
		{
			foreach (ExploreRegionNode item in list)
			{
				item.gameObject.SetActive(value: false);
			}
			return;
		}
		if (list.Count == 0)
		{
			return;
		}
		UIWidget component = _expertUnstableArea.GetComponent<UIWidget>();
		List<ExploreAreaNode> list2 = new List<ExploreAreaNode>();
		foreach (ExploreAreaNode item2 in _expertUnstableArea.AreaNodes())
		{
			if (item2.Template != null && item2.Template.Level > 60)
			{
				list2.Add(item2);
			}
		}
		float num = component.localCorners[0].x + (float)component.width - _expertUnstableArea.XMargin * 0.5f;
		foreach (ExploreAreaNode item3 in list2)
		{
			num = Mathf.Min(num, item3.transform.localPosition.x - _expertUnstableArea.XMargin * 0.5f);
		}
		foreach (ExploreAreaNode item4 in list2)
		{
			item4.transform.localPosition += Vector3.right * 400f;
		}
		component.width += 400;
		for (int i = 0; i < list.Count; i++)
		{
			ExploreRegionNode exploreRegionNode = list[i];
			exploreRegionNode.transform.SetParent(_expertUnstableArea.transform, worldPositionStays: false);
			exploreRegionNode.transform.localPosition = new Vector3(num + 105f + (float)(i % 2) * 180f, component.localCorners[0].y + (float)component.height * ((i < 2) ? 0.73f : 0.27f), 0f);
			exploreRegionNode.gameObject.name = "PillarRoute_" + exploreRegionNode.Route.RegionId;
		}
	}

	private void HideRiftAreas()
	{
		foreach (ExploreAreaNode item in _expertUnstableArea.AreaNodes())
		{
			if (RiftGateClient.IsRiftTemplate(item.Template))
			{
				HideForRiftGate(item.gameObject);
			}
		}
		UIWidget component = _expertUnstableArea.GetComponent<UIWidget>();
		float x = component.localCorners[0].x;
		float num = x;
		foreach (Transform item2 in _expertUnstableArea.transform)
		{
			if (item2.gameObject.activeSelf && (item2.GetComponent<ExploreAreaNode>() != null || item2.GetComponent<ExploreRegionNode>() != null))
			{
				num = Mathf.Max(num, item2.localPosition.x);
			}
		}
		if (num > x)
		{
			component.width = Mathf.Min(component.width, (int)(num - x + _expertUnstableArea.XMargin));
		}
	}

	private void HideForRiftGate(GameObject go)
	{
		if (go.activeSelf)
		{
			go.SetActive(value: false);
			_riftGateHidden.Add(go);
		}
	}

	private void RestoreRiftGateHidden()
	{
		foreach (GameObject item in _riftGateHidden)
		{
			if (item != null)
			{
				item.SetActive(value: true);
			}
		}
		_riftGateHidden.Clear();
	}

	public void ApplyRiftGateOnly()
	{
		_hideRiftAreas = false;
		RestoreRiftGateHidden();
		HideForRiftGate(_beginnerStableArea.gameObject);
		HideForRiftGate(_beginnerUnstableArea.gameObject);
		foreach (ExploreAreaNode item in _expertUnstableArea.AreaNodes())
		{
			if (!RiftGateClient.IsRiftTemplate(item.Template))
			{
				HideForRiftGate(item.gameObject);
			}
		}
		ExploreRegionNode exploreRegionNode = null;
		foreach (ExploreRegionNode item2 in _expertStableArea.PillarNodes())
		{
			if (item2.Route.RegionId != null && item2.Route.RegionId != GameManager.Region.Id)
			{
				HideForRiftGate(item2.gameObject);
			}
			else
			{
				exploreRegionNode = item2;
			}
		}
		HideForRiftGate(_expertStableArea.gameObject);
		UIWidget component = _expertUnstableArea.GetComponent<UIWidget>();
		float x = component.localCorners[0].x;
		float y = component.localCorners[0].y;
		float num = component.height;
		float num2 = x + 140f;
		if (exploreRegionNode != null)
		{
			exploreRegionNode.transform.localPosition = new Vector3(num2, y + num * 0.5f, 0f);
			SetCurrentCursor(exploreRegionNode.transform);
			num2 += 260f;
		}
		List<ExploreAreaNode> list = new List<ExploreAreaNode>();
		foreach (ExploreAreaNode item3 in _expertUnstableArea.AreaNodes())
		{
			if (item3.gameObject.activeSelf && RiftGateClient.IsRiftTemplate(item3.Template))
			{
				list.Add(item3);
			}
		}
		list.Sort((ExploreAreaNode a, ExploreAreaNode b) => (a.Template.Level == b.Template.Level) ? string.CompareOrdinal(a.Template.Id, b.Template.Id) : a.Template.Level.CompareTo(b.Template.Level));
		int num3 = 0;
		while (num3 < list.Count)
		{
			int level = list[num3].Template.Level;
			int count = list.FindAll((ExploreAreaNode n) => n.Template.Level == level).Count;
			for (int num4 = 0; num4 < count; num4++)
			{
				float num5 = ((count == 1) ? 0.5f : ((num4 == 0) ? 0.7f : 0.3f));
				list[num3 + num4].transform.localPosition = new Vector3(num2, y + num * num5, 0f);
			}
			num3 += count;
			num2 += 240f;
		}
		component.width = Mathf.Max(component.width / 4, (int)(num2 - x + 60f));
		_scrollView.UpdateLayout();
		_scrollView.MoveTo(0f, instant: true);
	}

	public void SelectExploreArea([CanBeNull] RegionTemplate template)
	{
		if (template == null)
		{
			_selectingEffect.gameObject.SetActive(value: false);
			return;
		}
		Transform unstableRoutesTransform = GetUnstableRoutesTransform(template.Role, template.MajorBiome(), template.Level);
		if (unstableRoutesTransform == null)
		{
			_selectingEffect.gameObject.SetActive(value: false);
			return;
		}
		MoveScrollTo(unstableRoutesTransform.position);
		_selectingEffect.transform.position = unstableRoutesTransform.position;
		_selectingEffect.gameObject.SetActive(value: true);
		_selectingEffect.Play();
	}

	private void MoveScrollTo(Vector3 position)
	{
		position = _scrollView.transform.InverseTransformPoint(position);
		Vector3 vector = position - _scrollView.GetBasePosition();
		float num = 0f;
		switch (_scrollView.Dir)
		{
		case KScrollViewBase.Direction.Vertical:
			num = Mathf.Abs(vector.y);
			break;
		case KScrollViewBase.Direction.Horizontal:
			num = Mathf.Abs(vector.x);
			break;
		}
		_scrollView.MoveTo(num - _scrollView.ViewLength * 0.5f, instant: true);
	}

	public void SetArchipelagoOnly(bool reset)
	{
		Init();
		RestoreRiftGateHidden();
		_hideRiftAreas = true;
		_sharedRoutesInfo.gameObject.SetActive(value: true);
		_sharedRoutesInfo.Set(GameSystem<PartySystem>.Instance().GetLeaderInfo());
		_recentlyVisit.Set();
		_currentCursor.gameObject.SetActive(value: false);
		_beginnerStableArea.gameObject.SetActive(value: false);
		_beginnerUnstableArea.Set(riskyOnly: true);
		_expertStableArea.gameObject.SetActive(value: false);
		_expertUnstableArea.Set(riskyOnly: true);
		_randomSeed = GameManager.Region.Id.GetHashCode();
		_resetViewerScroll = reset;
		UpdateLayout();
		_pointSprites.Clear();
	}

	public void Set(bool reset)
	{
		Init();
		RestoreRiftGateHidden();
		_hideRiftAreas = true;
		_sharedRoutesInfo.gameObject.SetActive(value: false);
		_recentlyVisit.Set();
		_isValidCurrentCursor = false;
		_beginnerStableArea.gameObject.SetActive(value: true);
		_expertStableArea.gameObject.SetActive(value: true);
		_beginnerStableArea.Set();
		_beginnerUnstableArea.Set();
		_expertStableArea.Set();
		_expertUnstableArea.Set();
		_randomSeed = GameManager.Region.Id.GetHashCode();
		_resetViewerScroll = reset;
		UpdateLayout();
		if (!_isValidCurrentCursor)
		{
			_currentCursor.gameObject.SetActive(value: false);
		}
		EstateSystem.GetEstateLicenses(delegate(EstateLicenses licenses)
		{
			_estateLicenses = licenses;
			EstateSystem.GetPersonalRegionInfo(delegate(PersonalRegionInfo info)
			{
				_personalRegionInfo = info;
				_beginnerStableArea.SetPersonal(info.PersonalRegion);
				RefreshRegionPoint();
			});
		});
	}

	public void Show(float duration, float delay)
	{
		base.Delay = delay;
		base.Duration = duration;
		base.Alpha = 1f;
	}

	public void Hide(float duration)
	{
		base.Duration = duration;
		base.Alpha = 0f;
	}

	private void UpdateSunsetEffects()
	{
		if (_scrollView.Widgets.Count > 0)
		{
			_sunsetEffects.transform.localPosition = _scrollView.Widgets[0].transform.localPosition;
		}
		_sunsetEffects.width = (int)_scrollView.ContentsLength;
		_sunsetEffects.height = base.Widget.height;
		UIUtility.UpdateAnchors(_sunsetEffects.transform);
		_sunsetClouds.ArrangeRandomClouds();
	}

	private void UpdateBackground()
	{
		Point2 point = new Point2((int)_scrollView.ContentsLength + 16, base.Widget.height);
		Vector3 localPosition = Vector3.left * (_scrollView.ViewLength * 0.5f + 8f);
		_background.SetDimensions(point.x, point.y);
		_background.transform.localPosition = localPosition;
		_shadowSprite.SetDimensions(point.x, point.y);
		_shadowSprite.transform.localPosition = localPosition;
	}

	public void SetCurrentCursor(Transform node)
	{
		_currentCursor.transform.parent = node;
		_currentCursor.transform.localPosition = Vector3.zero;
		_currentCursor.gameObject.SetActive(value: true);
		_isValidCurrentCursor = true;
	}

	private void RefreshRegionPoint()
	{
		_pointSprites.BeginLoad();
		_beginnerStableArea.ProcessRegionNode(RefreshRegionPoint);
		_expertStableArea.ProcessRegionNode(RefreshRegionPoint);
		_pointSprites.EndLoad();
	}

	private void RefreshRegionPoint(Transform node, [CanBeNull] string regionId)
	{
		if (!string.IsNullOrEmpty(regionId))
		{
			int num = 0;
			EntityTile? homePoint = GameSystem<MapSystem>.Instance().Points.HomePoint;
			Region obj = ((!homePoint.HasValue) ? GameSystem<MapSystem>.Instance().Points.ReturningPoint.Region : homePoint.Value.Region);
			if (regionId == obj.Id)
			{
				DrawRegionPoint(node, "icon_map_home", num++);
			}
			if (_personalRegionInfo.PersonalEstate.HasValue && _personalRegionInfo.PersonalRegion.HasValue && _personalRegionInfo.PersonalRegion.Value.Region.Id == regionId)
			{
				DrawRegionPoint(node, "icon_map_domain", num++);
			}
			if (_estateLicenses.UrbanEstate.HasValue && _estateLicenses.UrbanEstate.Value.RegionId == regionId)
			{
				DrawRegionPoint(node, "icon_map_domain", num++);
			}
			if (_estateLicenses.ClanEstate.HasValue && _estateLicenses.ClanEstate.Value.RegionId == regionId)
			{
				DrawRegionPoint(node, "icon_map_clan", num++);
			}
		}
	}

	private void DrawRegionPoint(Transform node, string sprite, int index)
	{
		Vector2 zero = Vector2.zero;
		switch (index)
		{
		default:
			return;
		case 0:
			zero = new Vector2(-1f, 1f);
			break;
		case 1:
			zero = new Vector2(1f, 1f);
			break;
		case 2:
			zero = new Vector2(-1f, -1f);
			break;
		case 3:
			zero = new Vector2(1f, -1f);
			break;
		}
		UISprite next = _pointSprites.GetNext();
		next.spriteName = sprite;
		next.MakePixelPerfect();
		Vector2 vector = new Vector2(40f, 40f) + Vector2.Scale(zero, new Vector2(next.width, next.height) * 0.5f);
		vector = next.transform.parent.InverseTransformPoint(node.TransformPoint(vector));
		next.SetPosition(vector, 0.5f, 0.5f);
	}

	private void ApplyShadow()
	{
		float shadowOffset = _expertUnstableArea.GetShadowOffset();
		float num;
		if (shadowOffset > 0f)
		{
			num = (float)_expertUnstableArea.GetComponent<UIWidget>().width - shadowOffset;
		}
		else
		{
			num = _expertUnstableArea.GetComponent<UIWidget>().width;
			if (!ExploreSystem.ReadyToUrbanExplore)
			{
				num += (float)_expertStableArea.GetComponent<UIWidget>().width;
				shadowOffset = _beginnerUnstableArea.GetShadowOffset();
				num += (float)_beginnerUnstableArea.GetComponent<UIWidget>().width - shadowOffset;
			}
		}
		_shadowSprite.Set(400f, num);
	}

	public static Vector2 GetRandomPositionOffset(global::System.Random rand)
	{
		return new Vector2(Mathf.Lerp(-10f, 10f, (float)rand.NextDouble()), Mathf.Lerp(-10f, 10f, (float)rand.NextDouble()));
	}

	[CanBeNull]
	public Transform GetIslandTransform(Role role, Biome biome, int level)
	{
		Transform transform = _beginnerStableArea.FindRegionNode(role);
		if (transform != null)
		{
			return transform;
		}
		ExploreRegionNode exploreRegionNode = _expertStableArea.FindRegionNode(role, biome, level);
		if (exploreRegionNode != null)
		{
			return exploreRegionNode.transform;
		}
		return null;
	}

	[CanBeNull]
	public Transform GetUnstableRoutesTransform(Role role, Biome biome, int level)
	{
		ExploreAreaNode exploreAreaNode = _beginnerUnstableArea.FindRoutesArea(role, biome, level);
		if (exploreAreaNode != null)
		{
			return exploreAreaNode.transform;
		}
		exploreAreaNode = _expertUnstableArea.FindRoutesArea(role, biome, level);
		if (exploreAreaNode != null)
		{
			return exploreAreaNode.transform;
		}
		return null;
	}
}

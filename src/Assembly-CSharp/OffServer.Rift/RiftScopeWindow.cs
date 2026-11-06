using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using UnityEngine;

namespace OffServer.Rift;

public sealed class RiftScopeWindow : UIBase
{
	private static readonly string[] UIBaseFields = new string[8] { "_background", "_softOpen", "_gameBlur", "_closeWhenPlayerMoveStart", "_lockScreenOrientation", "_uiType", "_anchorType", "_avatarMotion" };

	private static readonly string[] Filters = new string[4] { null, "aggro", "shy", "calm" };

	private static RiftScopeWindow window;

	private UITitle _title;

	private IconTabList _tabs;

	private KInfiniteScrollView _scroll;

	private KInfiniteScrollView.View<ScopeAnimal, RiftScopeRow> _view;

	private UILabel _empty;

	private const int CanvasWidth = 1140;

	private const int CanvasHeight = 700;

	private ScopeResult _result;

	private int _filter;

	private bool _guide;

	private bool _filling;

	private int _species = -1;

	private ScopeAnimal _selected;

	private KGridScrollView _grid;

	private UIWidget _canvas;

	private UIInput _search;

	private UILabel _heading;

	private UILabel _details;

	private UILabel _stats;

	private UILabel _hint;

	private SelectableButton _track;

	private SelectableButton _nearbyTab;

	private SelectableButton _guideTab;

	private float _nextRefresh;

	private string _signature;

	private Vector2 _lastSize;

	public static bool IsOpen
	{
		get
		{
			if (window != null)
			{
				return window.IsOpened;
			}
			return false;
		}
	}

	private static string Title => RiftText.T("กล\u0e49องส\u0e48องส\u0e31ตว\u0e4c", "Animal Scope");

	public static void ResetSession()
	{
		CloseWindow();
		if (!(window == null))
		{
			window._result = null;
			window._selected = null;
			window._species = -1;
			window._signature = null;
		}
	}

	public static Dictionary<string, object> DebugPresentation()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		if (window == null)
		{
			return dictionary;
		}
		List<object> list = new List<object>();
		UITitleWidget[] componentsInChildren = window.GetComponentsInChildren<UITitleWidget>(includeInactive: true);
		foreach (UITitleWidget obj in componentsInChildren)
		{
			UISprite uISprite = typeof(UITitleWidget).GetField("Background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj) as UISprite;
			if (uISprite != null)
			{
				UISprite[] componentsInChildren2 = uISprite.GetComponentsInChildren<UISprite>(includeInactive: true);
				foreach (UISprite uISprite2 in componentsInChildren2)
				{
					list.Add(new Dictionary<string, object>
					{
						{ "name", uISprite2.name },
						{ "sprite", uISprite2.spriteName },
						{ "enabled", uISprite2.enabled }
					});
				}
			}
		}
		dictionary["titleBackgrounds"] = list;
		List<object> list2 = new List<object>();
		RiftScopeCard[] componentsInChildren3 = window.GetComponentsInChildren<RiftScopeCard>(includeInactive: true);
		foreach (RiftScopeCard riftScopeCard in componentsInChildren3)
		{
			if (!riftScopeCard.gameObject.activeInHierarchy)
			{
				continue;
			}
			List<string> list3 = new List<string>();
			UISprite[] componentsInChildren2 = riftScopeCard.GetComponentsInChildren<UISprite>();
			foreach (UISprite uISprite3 in componentsInChildren2)
			{
				if (uISprite3.enabled && (uISprite3.spriteName ?? "").Contains("circle"))
				{
					list3.Add(uISprite3.spriteName);
				}
			}
			UIWidget component = riftScopeCard.GetComponent<UIWidget>();
			Camera camera = NGUITools.FindCameraForLayer(riftScopeCard.gameObject.layer);
			Vector3 vector = ((camera == null) ? Vector3.zero : camera.WorldToScreenPoint(component.transform.TransformPoint(component.localCenter)));
			list2.Add(new Dictionary<string, object>
			{
				{ "name", riftScopeCard.name },
				{ "rings", list3 },
				{ "x", vector.x },
				{
					"y",
					(float)(Screen.height - 1) - vector.y
				}
			});
		}
		dictionary["cards"] = list2;
		return dictionary;
	}

	private void Awake()
	{
		_openCloseSound = UISound.GroupType.Quest;
	}

	public static void ShowResult(ScopeResult result)
	{
		try
		{
			if (window == null)
			{
				window = Build();
			}
			if (!(window == null))
			{
				window._result = result;
				window._filter = 0;
				window._selected = null;
				window._species = -1;
				window._guide = false;
				window._signature = null;
				if (window.IsOpened)
				{
					window.Fill();
				}
				else
				{
					window.Open();
				}
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public static void CloseWindow()
	{
		if (window != null && window.IsOpened)
		{
			window.ForceClose();
		}
	}

	public static bool SelectTab(int index)
	{
		if (!IsOpen)
		{
			return false;
		}
		window.OnTab(index);
		return true;
	}

	protected override bool TryOpen()
	{
		if (!base.TryOpen())
		{
			return false;
		}
		if (_title != null && _title.Object != null)
		{
			_title.Object.SetTitle(Title);
		}
		RemoveDecorations(base.gameObject);
		Fill();
		return true;
	}

	private void Fill()
	{
		if (_result == null || _filling || _grid == null)
		{
			return;
		}
		_filling = true;
		try
		{
			List<ScopeAnimal> list = _result.Animals.FindAll(RiftScope.IsEligible);
			string query = ((_search == null) ? string.Empty : (_search.value ?? string.Empty).Trim());
			int[] array = new int[Filters.Length];
			foreach (ScopeAnimal item in list)
			{
				if (Matches(item, query))
				{
					array[0]++;
					int num = Array.IndexOf(Filters, item.Role);
					if (num > 0)
					{
						array[num]++;
					}
				}
			}
			string[] array2 = new string[4]
			{
				RiftText.T("ท\u0e31\u0e49งหมด", "All"),
				RiftScope.RoleName("aggro"),
				RiftScope.RoleName("shy"),
				RiftScope.RoleName("calm")
			};
			_tabs.BeginLoad();
			for (int i = 0; i < array2.Length; i++)
			{
				_tabs.Add(null, array2[i] + " (" + array[i] + ")");
			}
			_tabs.EndLoad();
			_tabs.Select(_filter);
			List<ScopeAnimal> list2 = list.FindAll((ScopeAnimal a) => (_filter == 0 || a.Role == Filters[_filter]) && Matches(a, query));
			SortedDictionary<int, ScopeAnimal> sortedDictionary = new SortedDictionary<int, ScopeAnimal>();
			foreach (ScopeAnimal item2 in list2)
			{
				sortedDictionary[item2.Type] = item2;
			}
			if (_guide)
			{
				foreach (ScopeAnimal value2 in RiftScope.Discoveries.Values)
				{
					if ((_filter == 0 || value2.Role == Filters[_filter]) && Matches(value2, query) && !sortedDictionary.ContainsKey(value2.Type))
					{
						sortedDictionary[value2.Type] = value2;
					}
				}
			}
			if (!sortedDictionary.ContainsKey(_species))
			{
				_species = -1;
				_selected = null;
			}
			_grid.Nodes.BeginLoad();
			foreach (KeyValuePair<int, ScopeAnimal> pair in sortedDictionary)
			{
				int count = list.FindAll((ScopeAnimal a) => a.Type == pair.Key).Count;
				_grid.Nodes.GetNext().GetComponent<RiftScopeCard>().Set(pair.Value, count, _species == pair.Key, SelectSpecies);
			}
			_grid.Nodes.EndLoad();
			_grid.Reposition(resetPosition: false, tween: false);
			List<ScopeAnimal> list3 = list2.FindAll((ScopeAnimal a) => a.Type == _species);
			if (_selected != null && !list3.Contains(_selected))
			{
				_selected = null;
			}
			_view.SetList(list3);
			_scroll.Panel.alpha = 1f;
			_empty.gameObject.SetActive(sortedDictionary.Count == 0);
			_empty.text = RiftText.T("ไม\u0e48พบส\u0e31ตว\u0e4cท\u0e35\u0e48มองเห\u0e47นในระยะ 9 ไทล\u0e4c\nลองเปล\u0e35\u0e48ยนต\u0e31วกรองหร\u0e37อคำค\u0e49น", "No visible animals within 9 tiles\nTry another filter or search");
			_heading.text = (_guide ? RiftText.T("บ\u0e31นท\u0e36กการส\u0e48องคร\u0e31\u0e49งน\u0e35\u0e49 · {0} ชน\u0e34ด", "This session's sightings · {0} species", RiftScope.Discoveries.Count) : RiftText.T("ใกล\u0e49ต\u0e31ว · {0} ต\u0e31ว · ระยะไม\u0e48เก\u0e34น 9 ไทล\u0e4c", "Nearby · {0} animals · within 9 tiles", list.Count));
			_details.text = (sortedDictionary.TryGetValue(_species, out var value) ? (value.DisplayName + "\n" + RiftText.T("เล\u0e37อกส\u0e31ตว\u0e4cรายต\u0e31วด\u0e49านล\u0e48างก\u0e48อนต\u0e34ดตาม", "Select an individual below before tracking")) : RiftText.T("เล\u0e37อกการ\u0e4cดชน\u0e34ดส\u0e31ตว\u0e4c", "Select a species card"));
			if (_guide && list3.Count == 0 && _species >= 0)
			{
				UILabel details = _details;
				details.text = details.text + "\n" + RiftText.T("บ\u0e31นท\u0e36กเก\u0e48า — ไม\u0e48ม\u0e35เป\u0e49าท\u0e35\u0e48ต\u0e34ดตามได\u0e49", "Past sighting — no eligible target");
			}
			_nearbyTab.Text = RiftText.T("ใกล\u0e49ต\u0e31ว", "Nearby");
			_guideTab.Text = RiftText.T("สม\u0e38ดส\u0e31ตว\u0e4c", "Field guide");
			RefreshSelected();
			Layout();
		}
		finally
		{
			_filling = false;
		}
	}

	private static bool Matches(ScopeAnimal animal, string query)
	{
		if (!string.IsNullOrEmpty(query))
		{
			return (animal.DisplayName ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
		}
		return true;
	}

	private void SelectSpecies(int type)
	{
		_species = type;
		_selected = null;
		_scroll.ResetPosition();
		Fill();
	}

	private void SelectIndividual(ScopeAnimal animal)
	{
		if (_result != null && _result.Animals.Contains(animal) && RiftScope.IsEligible(animal) && animal.Type == _species)
		{
			_selected = animal;
			RefreshSelected();
		}
	}

	private void RefreshSelected()
	{
		if (_selected != null && !RiftScope.IsEligible(_selected))
		{
			_selected = null;
		}
		_track.Disabled = _selected == null;
		_track.Text = ((_selected != null && RiftScope.IsTracked(_selected.Id)) ? RiftText.T("เล\u0e34กต\u0e34ดตาม", "Untrack") : RiftText.T("ต\u0e34ดตาม", "Track"));
		_stats.text = ((_selected == null) ? RiftText.T("ย\u0e31งไม\u0e48ได\u0e49เล\u0e37อกส\u0e31ตว\u0e4cรายต\u0e31ว", "No individual selected") : (_selected.DisplayName + " " + LocalizeUtil.FormatLevel(_selected.Level))) + "\n" + RiftText.T("ดาเมจ (พล\u0e31งโจมต\u0e35)", "Damage (attack)") + "  " + RiftScope.StatText((_selected == null) ? ((double?)null) : _selected.Attack) + "\n" + RiftText.T("เจาะเกราะ", "Armor penetration") + "  " + RiftScope.StatText((_selected == null) ? ((double?)null) : _selected.AttackRating) + "\n" + RiftText.T("เกราะ", "Armor") + "  " + RiftScope.StatText((_selected == null) ? ((double?)null) : _selected.Defense) + "\n" + RiftText.T("พล\u0e31งป\u0e49องก\u0e31น", "Defense") + "  " + RiftScope.StatText((_selected == null) ? ((double?)null) : _selected.Defense);
	}

	private void Update()
	{
		if (!base.IsOpened || _result == null || Time.unscaledTime < _nextRefresh)
		{
			return;
		}
		_nextRefresh = Time.unscaledTime + 0.25f;
		string text = string.Empty;
		foreach (ScopeAnimal animal in _result.Animals)
		{
			if (RiftScope.IsEligible(animal))
			{
				text = text + animal.Id + "|";
			}
		}
		if (text != _signature)
		{
			_signature = text;
			Fill();
		}
		RefreshSelected();
		Layout();
	}

	private void OnTab(int index)
	{
		if (index >= 0 && index < Filters.Length && index != _filter)
		{
			_filter = index;
			Fill();
		}
	}

	private static RiftScopeWindow Build()
	{
		QuestGroup questGroup = UIManager.FindScript<QuestGroup>();
		EncyclopediaGroup encyclopediaGroup = UIManager.FindScript<EncyclopediaGroup>();
		KGridScrollView kGridScrollView = null;
		if (encyclopediaGroup != null)
		{
			KGridScrollView[] componentsInChildren = encyclopediaGroup.GetComponentsInChildren<KGridScrollView>(includeInactive: true);
			foreach (KGridScrollView kGridScrollView2 in componentsInChildren)
			{
				if (kGridScrollView2.Nodes.BaseObject != null && kGridScrollView2.Nodes.BaseObject.GetComponent<EncyclopediaCropWidget>() != null)
				{
					kGridScrollView = kGridScrollView2;
					break;
				}
			}
		}
		if (questGroup == null || kGridScrollView == null)
		{
			UIManager.SystemMsg(RiftText.T("หน\u0e49าต\u0e48างสม\u0e38ดส\u0e31ตว\u0e4cย\u0e31งโหลดไม\u0e48พร\u0e49อม กร\u0e38ณาลองส\u0e48องอ\u0e35กคร\u0e31\u0e49ง", "Native field guide is not loaded yet. Please scan again."));
			return null;
		}
		GameObject gameObject = new GameObject("RiftScopeHolder");
		gameObject.SetActive(value: false);
		try
		{
			GameObject gameObject2 = UnityEngine.Object.Instantiate(questGroup.gameObject, gameObject.transform, worldPositionStays: false);
			gameObject2.name = "RiftScopeWindow";
			RiftScopeWindow w = gameObject2.AddComponent<RiftScopeWindow>();
			string[] uIBaseFields = UIBaseFields;
			foreach (string text in uIBaseFields)
			{
				FieldInfo field = typeof(UIBase).GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
				if (!(field == null))
				{
					object obj = field.GetValue(questGroup);
					Component component = obj as Component;
					if (component != null)
					{
						obj = Remap(component, questGroup.transform, gameObject2.transform);
					}
					field.SetValue(w, obj);
				}
			}
			UnityEngine.Object.DestroyImmediate(gameObject2.GetComponent<QuestGroup>());
			QuestMainWidget[] componentsInChildren2 = gameObject2.GetComponentsInChildren<QuestMainWidget>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				UnityEngine.Object.DestroyImmediate(componentsInChildren2[i]);
			}
			QuestMenuTabs[] componentsInChildren3 = gameObject2.GetComponentsInChildren<QuestMenuTabs>(includeInactive: true);
			for (int i = 0; i < componentsInChildren3.Length; i++)
			{
				componentsInChildren3[i].enabled = false;
			}
			w._scroll = gameObject2.GetComponentInChildren<KInfiniteScrollView>(includeInactive: true);
			Transform transform = w._scroll.transform.Find("QuestBaseWidget");
			for (int num = w._scroll.transform.childCount - 1; num >= 0; num--)
			{
				Transform child = w._scroll.transform.GetChild(num);
				if (child != transform)
				{
					UnityEngine.Object.DestroyImmediate(child.gameObject);
				}
			}
			QuestNodeWidget component2 = transform.GetComponent<QuestNodeWidget>();
			RiftScopeRow riftScopeRow = RiftScopeRow.Adopt(component2);
			UnityEngine.Object.DestroyImmediate(component2);
			QuestItemWidget[] componentsInChildren4 = transform.GetComponentsInChildren<QuestItemWidget>(includeInactive: true);
			for (int i = 0; i < componentsInChildren4.Length; i++)
			{
				componentsInChildren4[i].gameObject.SetActive(value: false);
			}
			Transform transform2 = gameObject2.transform.Find("Main/SeasonBanner");
			Transform transform3 = gameObject2.transform.Find("Main/MainView/QuestContainer/Bottom");
			Transform transform4 = gameObject2.transform.Find("Main/MainView/QuestContainer/Items/LoadingPrefab");
			Transform[] array = new Transform[3] { transform2, transform3, transform4 };
			foreach (Transform transform5 in array)
			{
				if (transform5 != null)
				{
					transform5.gameObject.SetActive(value: false);
				}
			}
			w._empty = gameObject2.transform.Find("Main/MainView/QuestContainer/Items/NoQuestLabel").GetComponent<UILabel>();
			Transform contents = gameObject2.transform.Find("Main/MainView/QuestContainer");
			ConfigureShell(gameObject2);
			w._title = gameObject2.GetComponentInChildren<UITitle>(includeInactive: true);
			FieldInfo field2 = typeof(UITitle).GetField("_titleName", BindingFlags.Instance | BindingFlags.NonPublic);
			if (w._title != null && field2 != null)
			{
				field2.SetValue(w._title, Title);
			}
			w.BuildDex(kGridScrollView, riftScopeRow, contents);
			riftScopeRow.Init();
			if (w._title != null)
			{
				w._title.ShowBackButton(show: true);
				w._title.ShowCloseButton(show: true);
			}
			gameObject2.transform.SetParent(questGroup.transform.parent, worldPositionStays: false);
			gameObject2.transform.localPosition = questGroup.transform.localPosition;
			gameObject2.transform.localScale = questGroup.transform.localScale;
			gameObject2.layer = questGroup.gameObject.layer;
			gameObject2.SetActive(value: true);
			w.Init(UIRootAnchor.GetRootAnchor(w.Anchor));
			RemoveDecorations(gameObject2);
			w.SetChildrenActive(activated: false);
			w._tabs = gameObject2.GetComponentInChildren<QuestMenuTabs>(includeInactive: true).Object;
			w._tabs.Clicked += w.OnTab;
			w._view = w._scroll.Initialize(delegate(RiftScopeRow row, ScopeAnimal animal)
			{
				row.Set(animal, w.SelectIndividual, (string id) => w._selected != null && w._selected.Id == id);
			}, delegate(RiftScopeRow row)
			{
				row.Init();
			});
			return w;
		}
		finally
		{
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private void BuildDex(KGridScrollView source, RiftScopeRow row, Transform contents)
	{
		GameObject gameObject = new GameObject("NativeAnimalDex", typeof(UIWidget));
		gameObject.transform.SetParent(contents, worldPositionStays: false);
		_canvas = gameObject.GetComponent<UIWidget>();
		_canvas.width = 1140;
		_canvas.height = 700;
		for (int i = 0; i < contents.childCount; i++)
		{
			if (contents.GetChild(i) != gameObject.transform)
			{
				contents.GetChild(i).gameObject.SetActive(value: false);
			}
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(source.gameObject, _canvas.transform, worldPositionStays: false);
		_grid = gameObject2.GetComponent<KGridScrollView>();
		_grid.Panel.depth = _scroll.Panel.depth;
		GameObject baseObject = _grid.Nodes.BaseObject;
		for (int num = gameObject2.transform.childCount - 1; num >= 0; num--)
		{
			if (gameObject2.transform.GetChild(num).gameObject != baseObject)
			{
				UnityEngine.Object.DestroyImmediate(gameObject2.transform.GetChild(num).gameObject);
			}
		}
		RiftScopeCard.Adopt(baseObject.GetComponent<EncyclopediaCropWidget>(), row.PortraitTemplate);
		_grid.Nodes.Init(null);
		_grid.Nodes.Clear();
		RectLayoutComponent[] components = gameObject2.GetComponents<RectLayoutComponent>();
		for (int j = 0; j < components.Length; j++)
		{
			components[j].enabled = false;
		}
		Viewport(_grid, "SpeciesGrid", 20, 132, 520, 548);
		_grid.gameObject.SetActive(value: true);
		_scroll.transform.SetParent(_canvas.transform, worldPositionStays: false);
		Viewport(_scroll, "Individuals", 570, 212, 550, 200);
		_scroll.gameObject.SetActive(value: true);
		_empty.gameObject.SetActive(value: false);
		_empty = Label(row.LabelTemplate, "NoAnimals", 44, 336, 472, 128, 24);
		_empty.alignment = NGUIText.Alignment.Center;
		_empty.maxLineCount = 4;
		_heading = Label(row.LabelTemplate, "Heading", 20, 14, 1100, 36, 24);
		_nearbyTab = Button(row.ButtonTemplate, "Nearby", 20, 62, 170, 48, delegate
		{
			_guide = false;
			Fill();
		});
		_guideTab = Button(row.ButtonTemplate, "Guide", 202, 62, 170, 48, delegate
		{
			_guide = true;
			Fill();
		});
		UILabel uILabel = Label(row.LabelTemplate, "Search", 400, 68, 470, 36, 22);
		uILabel.text = RiftText.T("ค\u0e49นหาช\u0e37\u0e48อส\u0e31ตว\u0e4c…", "Search animal name…");
		uILabel.color = Color.white;
		uILabel.supportEncoding = false;
		uILabel.overflowMethod = UILabel.Overflow.ClampContent;
		uILabel.maxLineCount = 1;
		_search = InitializeSearch(uILabel);
		NGUITools.AddWidgetCollider(uILabel.gameObject);
		EventDelegate.Add(_search.onChange, Fill);
		uILabel.gameObject.SetActive(value: true);
		Button(row.ButtonTemplate, "ClearSearch", 930, 62, 190, 48, delegate
		{
			_search.value = string.Empty;
		}).Text = RiftText.T("ล\u0e49างคำค\u0e49น", "Clear");
		_details = Label(row.LabelTemplate, "SpeciesDetails", 570, 132, 550, 72, 22);
		_stats = Label(row.LabelTemplate, "IndividualStats", 570, 428, 550, 145, 20);
		_hint = Label(row.LabelTemplate, "StatMeaning", 570, 580, 550, 54, 14);
		_hint.text = RiftText.T("พล\u0e31งโจมต\u0e35ไม\u0e48ใช\u0e48ดาเมจส\u0e38ดท\u0e49ายต\u0e48อคร\u0e31\u0e49ง · เจาะเกราะเป\u0e47นคะแนน\nเกราะและพล\u0e31งป\u0e49องก\u0e31นใช\u0e49ค\u0e48า Defense เด\u0e35ยวก\u0e31น\nบ\u0e31นท\u0e36กการส\u0e48องคร\u0e31\u0e49งน\u0e35\u0e49เท\u0e48าน\u0e31\u0e49น ไม\u0e48บ\u0e31นท\u0e36กถาวร", "Attack is not final hit damage · Penetration is a rating\nArmor and defense share the same Defense value\nSession sightings only; not saved permanently");
		_track = Button(row.ButtonTemplate, "Track", 570, 646, 250, 46, delegate
		{
			if (_selected != null && RiftScope.IsEligible(_selected))
			{
				RiftScope.ToggleTrack(_selected);
			}
			RefreshSelected();
		});
		NGUITools.SetLayer(gameObject, base.gameObject.layer);
	}

	private static void ConfigureShell(GameObject copy)
	{
		Transform transform = copy.transform.Find("Main");
		Transform transform2 = transform.Find("MainView");
		Transform transform3 = transform2.Find("QuestContainer");
		Transform[] array = new Transform[2] { transform, transform3 };
		for (int i = 0; i < array.Length; i++)
		{
			RectLayoutComponent[] components = array[i].GetComponents<RectLayoutComponent>();
			for (int j = 0; j < components.Length; j++)
			{
				UnityEngine.Object.DestroyImmediate(components[j]);
			}
		}
		transform.GetComponent<UIWidget>().SetAnchor(copy, 12, 12, -12, -88);
		transform.Find("Tabs").GetComponent<UIWidget>().SetAnchor(transform.gameObject, 0f, 0, 0f, 0, 0f, 180, 1f, 0);
		transform2.GetComponent<UIWidget>().SetAnchor(transform.gameObject, 196, 0, 0, 0);
		transform3.GetComponent<UIWidget>().SetAnchor(transform2.gameObject, 12, 12, -12, -12);
	}

	private static void RemoveDecorations(GameObject copy)
	{
		UISprite[] componentsInChildren = copy.GetComponentsInChildren<UISprite>(includeInactive: true);
		foreach (UISprite uISprite in componentsInChildren)
		{
			if ((uISprite.spriteName ?? string.Empty).IndexOf("grunge", StringComparison.OrdinalIgnoreCase) >= 0 || uISprite.gameObject.name.IndexOf("grunge", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				uISprite.enabled = false;
			}
		}
		UITitleWidget[] componentsInChildren2 = copy.GetComponentsInChildren<UITitleWidget>(includeInactive: true);
		foreach (UITitleWidget obj in componentsInChildren2)
		{
			UISprite uISprite2 = typeof(UITitleWidget).GetField("Background", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj) as UISprite;
			if (uISprite2 != null)
			{
				componentsInChildren = uISprite2.GetComponentsInChildren<UISprite>(includeInactive: true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					componentsInChildren[j].enabled = false;
				}
			}
		}
	}

	private static UIInput InitializeSearch(UILabel label)
	{
		label.gameObject.SetActive(value: false);
		string text = label.text;
		UIInput uIInput = label.gameObject.AddComponent<UIInput>();
		uIInput.label = label;
		uIInput.characterLimit = 80;
		uIInput.savedAs = string.Empty;
		uIInput.Set(string.Empty, notify: false);
		uIInput.defaultText = text;
		return uIInput;
	}

	private void Viewport(KScrollViewBase scroll, string name, int x, int y, int width, int height)
	{
		GameObject gameObject = new GameObject(name + "Viewport", typeof(UIWidget));
		gameObject.transform.SetParent(_canvas.transform, worldPositionStays: false);
		Position(gameObject.GetComponent<UIWidget>(), x, y, width, height);
		scroll.transform.SetParent(gameObject.transform, worldPositionStays: false);
		scroll.transform.localPosition = Vector3.zero;
		scroll.transform.localScale = Vector3.one;
		scroll.Panel.SetAnchor(gameObject, 0, 0, 0, 0);
		scroll.Panel.clipOffset = Vector2.zero;
		scroll.Panel.clipping = UIDrawCall.Clipping.SoftClip;
		scroll.Panel.clipSoftness = new Vector2(2f, 2f);
		scroll.Panel.alpha = 1f;
		scroll.Panel.UpdateAnchors();
		scroll.UpdateViewSize();
	}

	private UILabel Label(UILabel source, string name, int x, int y, int width, int height, int size)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(source.gameObject, _canvas.transform, worldPositionStays: false);
		gameObject.name = name;
		UILabel component = gameObject.GetComponent<UILabel>();
		ConfigureText(component, size);
		Position(component, x, y, width, height);
		gameObject.SetActive(value: true);
		return component;
	}

	private static void ConfigureText(UILabel label, int size)
	{
		label.overflowMethod = UILabel.Overflow.ClampContent;
		label.maxLineCount = 0;
		label.fontSize = size;
		label.color = Color.white;
		label.text = string.Empty;
		label.spacingY = 2;
		label.supportEncoding = false;
		label.alignment = NGUIText.Alignment.Left;
	}

	private SelectableButton Button(SelectableButton source, string name, int x, int y, int width, int height, Action clicked)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(source.gameObject, _canvas.transform, worldPositionStays: false);
		gameObject.name = name;
		SelectableButton component = gameObject.GetComponent<SelectableButton>();
		Position(component.Widget, x, y, width, height);
		component.ShortcutCommand = InputCommand.None;
		component.Text = " ";
		component.Clicked = clicked;
		component.SubClicked = null;
		component.Disabled = false;
		component.ClearEffect();
		gameObject.SetActive(value: true);
		return component;
	}

	private static void Position(UIWidget widget, int x, int y, int width, int height)
	{
		widget.SetAnchor((Transform)null);
		widget.pivot = UIWidget.Pivot.TopLeft;
		widget.transform.localPosition = new Vector3((float)x - 570f, 350f - (float)y, 0f);
		widget.transform.localScale = Vector3.one;
		widget.width = width;
		widget.height = height;
	}

	private void Layout()
	{
		if (_canvas == null)
		{
			return;
		}
		UIWidget component = _canvas.transform.parent.GetComponent<UIWidget>();
		if (!(component == null))
		{
			UIUtility.UpdateAnchors(base.transform.Find("Main"));
			component.UpdateAnchors();
			Vector2 localSize = component.localSize;
			if (!(localSize == _lastSize))
			{
				_lastSize = localSize;
				float b = Mathf.Min(localSize.x / 1140f, localSize.y / 700f);
				_canvas.transform.localScale = Vector3.one * Mathf.Max(0.01f, b);
				_canvas.transform.localPosition = component.localCenter;
				UIUtility.UpdateAnchors(_canvas.transform);
				_grid.UpdateViewSize();
				_scroll.UpdateViewSize();
				_grid.Reposition(resetPosition: false, tween: false);
				_scroll.Reposition(resetPosition: false, tween: false);
			}
		}
	}

	protected override void OnScreenResized()
	{
		base.OnScreenResized();
		_lastSize = Vector2.zero;
		Layout();
	}

	private void OnDestroy()
	{
		UIManager.ScreenResized -= OnScreenResized;
		if (_tabs != null)
		{
			_tabs.Clicked -= OnTab;
		}
		if (_search != null)
		{
			EventDelegate.Remove(_search.onChange, Fill);
		}
		if (window == this)
		{
			window = null;
		}
	}

	private static Component Remap(Component c, Transform srcRoot, Transform dstRoot)
	{
		string text = string.Empty;
		Transform parent = c.transform;
		while (parent != null && parent != srcRoot)
		{
			text = ((text.Length == 0) ? parent.name : (parent.name + "/" + text));
			parent = parent.parent;
		}
		if (parent != srcRoot)
		{
			return c;
		}
		Transform transform = ((text.Length == 0) ? dstRoot : dstRoot.Find(text));
		if (!(transform == null))
		{
			return transform.GetComponent(c.GetType());
		}
		return null;
	}
}

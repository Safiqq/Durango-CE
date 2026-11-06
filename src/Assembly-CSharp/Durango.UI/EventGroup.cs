using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.Event;
using Durango.Logic.Notification;
using Durango.System;
using Durango.UI.Control;
using Durango.Utils.Extensions;
using L10N;
using NestedPrefab;
using Shared.Attendance;
using UnityEngine;

namespace Durango.UI;

[Uri("Event")]
public class EventGroup : UIBase, INotificationable
{
	private enum AttendanceType
	{
		Monthly,
		Weekly,
		Event
	}

	[SerializeField]
	private UITitle _titleWidget;

	[SerializeField]
	private NestedPrefabLinker _tabLinker;

	[SerializeField]
	[EnumList(typeof(AttendanceType), false, 0, -1)]
	private CalendarWidget[] _attendanceList;

	private static bool _isRewardChecked;

	private int _selectedTab;

	private Countable _notification;

	private IconTabList _tabList;

	private EventSystem _calendarSystem;

	private bool HasEventCalendar => CommunityLink.Active;

	private int CalendarOffset => HasEventCalendar ? 1 : 0;

	public Notification Notification => _notification;

	private void Awake()
	{
		_openCloseSound = UISound.GroupType.Event;
		_notification = new Countable(Type.Important);
	}

	private void Start()
	{
		_titleWidget.Object.SetTitle(T._("이벤트"));
		if (HasEventCalendar)
		{
			_selectedTab = -1;
		}
		_tabList = _tabLinker.Object.GetComponent<IconTabList>();
		_tabList.Clicked += OnClickTab;
		base.OnOpenSucceed += OnOpened;
		_calendarSystem = GameSystem<EventSystem>.Instance();
		_calendarSystem.CalendarUpdated += OnCalendarUpdate;
		OnCalendarUpdate();
		SetChildrenActive(activated: false);
	}

	private void OnCalendarUpdate()
	{
		Calendar[] calendars = GameSystem<EventSystem>.Instance().Calendars;
		int size = KUtility.GetSize(calendars);
		if (size == 0)
		{
			return;
		}
		if (_selectedTab >= 0)
		{
			_selectedTab = Mathf.Clamp(_selectedTab, 0, size - 1);
		}
		int num = 0;
		int? num2 = null;
		for (int i = 0; i < size; i++)
		{
			if (calendars[i].HasTodayReward())
			{
				_tabList.SetNotification(i + CalendarOffset, on: true, Type.Important);
				num++;
				if (!num2.HasValue)
				{
					num2 = i;
				}
			}
			else
			{
				_tabList.SetNotification(i + CalendarOffset, on: false, Type.Important);
			}
		}
		_notification.Count = num;
		if (!_isRewardChecked)
		{
			_isRewardChecked = true;
			if (num2.HasValue && !CommunityLink.Active)
			{
				_selectedTab = num2.Value;
				Open();
			}
		}
	}

	private void OnOpened()
	{
		RefreshTabs();
		if (HasEventCalendar && _selectedTab < 0)
		{
			ShowEventCalendarTab();
		}
		else
		{
			SelectTab(_selectedTab);
		}
	}

	private void RefreshTabs()
	{
		Calendar[] calendars = GameSystem<EventSystem>.Instance().Calendars;
		_tabList.BeginLoad();
		if (HasEventCalendar)
		{
			_tabList.Add(null, "ปฏ\u0e34ท\u0e34นอ\u0e35เวนต\u0e4c");
		}
		int i = 0;
		for (int size = KUtility.GetSize(calendars); i < size; i++)
		{
			_tabList.Add(null, calendars[i].TabName);
		}
		_tabList.EndLoad();
	}

	private void OnClickTab(int index)
	{
		if (HasEventCalendar && index == 0)
		{
			ShowEventCalendarTab();
			return;
		}
		index -= CalendarOffset;
		if (_selectedTab != index)
		{
			SelectTab(index);
		}
	}

	private void SelectTab(int index)
	{
		CommunityEventCalendar.HideEmbedded();
		Calendar[] calendars = GameSystem<EventSystem>.Instance().Calendars;
		if (index >= 0 && index < KUtility.GetSize(calendars))
		{
			_selectedTab = index;
			_tabList.Select(index + CalendarOffset);
			Calendar calendar = calendars[index];
			AttendanceType attendanceType = GetAttendanceType(calendar.Category);
			for (int i = 0; i < _attendanceList.Length; i++)
			{
				bool active = i == (int)attendanceType;
				_attendanceList[i].gameObject.SetActive(active);
			}
			UIUtility.UpdateAnchors(base.transform);
			_attendanceList[(int)attendanceType].Set(calendar);
		}
	}

	public bool OpenEventCalendar()
	{
		if (!HasEventCalendar || _tabList == null)
		{
			return false;
		}
		_selectedTab = -1;
		if (!base.IsOpened)
		{
			Open();
		}
		if (!base.IsOpened)
		{
			return false;
		}
		RefreshTabs();
		ShowEventCalendarTab();
		return true;
	}

	private void ShowEventCalendarTab()
	{
		_selectedTab = -1;
		_tabList.Select(0);
		UIUtility.UpdateAnchors(base.transform);
		for (int i = 0; i < _attendanceList.Length; i++)
		{
			_attendanceList[i].gameObject.SetActive(value: false);
		}
		CommunityEventCalendar.ShowEmbedded(_attendanceList[0]);
	}

	public bool TryGetCalendarBounds(Transform space, out Rect bounds)
	{
		bounds = default(Rect);
		if (!Platform.Instance.UsePCUI || _tabList == null || base.Rect == null)
		{
			return false;
		}
		Vector3[] worldCorners = base.Rect.worldCorners;
		Vector3[] worldCorners2 = _tabList.worldCorners;
		Vector3 vector = space.InverseTransformPoint(worldCorners[0]);
		Vector3 vector2 = space.InverseTransformPoint(worldCorners[2]);
		Vector3 vector3 = space.InverseTransformPoint(worldCorners2[2]);
		float num = vector3.x + 12f;
		float num2 = vector2.x - 12f;
		float num3 = vector.y + 12f;
		float y = vector3.y;
		if (num2 <= num || y <= num3)
		{
			return false;
		}
		bounds = UnityEngine.Rect.MinMaxRect(num, num3, num2, y);
		return true;
	}

	private void OnDestroy()
	{
		if (_calendarSystem != null)
		{
			_calendarSystem.CalendarUpdated -= OnCalendarUpdate;
		}
	}

	private static AttendanceType GetAttendanceType(CategoryType category)
	{
		switch (category)
		{
		case CategoryType.Event1:
		case CategoryType.Event2:
		case CategoryType.Event3:
		case CategoryType.Event4:
		case CategoryType.Event5:
			return AttendanceType.Event;
		case CategoryType.Returner:
			return AttendanceType.Weekly;
		default:
			return AttendanceType.Monthly;
		}
	}

	public Transform GetCategoryTransform(CategoryType category)
	{
		int num = -1;
		Calendar[] calendars = GameSystem<EventSystem>.Instance().Calendars;
		int i = 0;
		for (int size = KUtility.GetSize(calendars); i < size; i++)
		{
			if (calendars[i].Category == category)
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			return null;
		}
		IconTabWidget iconTabWidget = _tabList.Get(num + CalendarOffset);
		if (iconTabWidget == null)
		{
			return null;
		}
		return iconTabWidget.transform;
	}

	public CategoryType GetCurrenCategoryType()
	{
		Calendar[] calendars = GameSystem<EventSystem>.Instance().Calendars;
		if (_selectedTab < 0 || _selectedTab >= KUtility.GetSize(calendars))
		{
			return CategoryType.Monthly;
		}
		return calendars[_selectedTab].Category;
	}

	public Transform GetCategoryNodeWidget(int index)
	{
		if (_selectedTab < 0)
		{
			return null;
		}
		AttendanceType attendanceType = GetAttendanceType(GetCurrenCategoryType());
		CalendarNodeWidget nodeWidget = _attendanceList[(int)attendanceType].GetNodeWidget(index);
		if (nodeWidget != null)
		{
			return nodeWidget.transform;
		}
		return null;
	}

	protected override void DefaultUri()
	{
		if (HasEventCalendar && UriParser.GetArgument("calendar") == "1")
		{
			OpenEventCalendar();
			return;
		}
		base.DefaultUri();
		if (!UriParser.GetArgument("Tab").TryEnum<AttendanceType>(out var type))
		{
			return;
		}
		Calendar[] calendars = GameSystem<EventSystem>.Instance().Calendars;
		if (calendars != null)
		{
			int num = calendars.IndexOf((Calendar x) => GetAttendanceType(x.Category) == type);
			if (num != -1)
			{
				SelectTab(num);
			}
		}
	}
}

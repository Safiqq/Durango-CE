using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.Notification;
using Durango.Logic.Quest;
using Durango.UI.Control;
using Messages;
using NestedPrefab;
using UnityEngine;

namespace Durango.UI;

public class QuestMenuTabs : NestedPrefabLinker<IconTabList>
{
	private bool _isDirty = true;

	private readonly List<Category> _categories = new List<Category>();

	private string _selectedCategory;

	private string _visibilityKey;

	private float _nextVisibilityCheck;

	private readonly Dictionary<Category, float> _questRequests = new Dictionary<Category, float>();

	private readonly Dictionary<Category, float> _scoreRequests = new Dictionary<Category, float>();

	public event Action<string> TabClicked;

	private static bool ShowCategory(Category category)
	{
		if (!OffServerLink.Active || !category.HasLoadedQuestList)
		{
			return true;
		}
		foreach (QuestToDo cachedQuest in category.GetCachedQuestList())
		{
			if (!cachedQuest.Finished)
			{
				return true;
			}
		}
		if (category.HasQuestScore.HasValue)
		{
			return category.HasScoreReward;
		}
		return true;
	}

	private void Update()
	{
		if (!OffServerLink.Active || Time.realtimeSinceStartup < _nextVisibilityCheck)
		{
			return;
		}
		QuestGroup questGroup = UIManager.FindScript<QuestGroup>();
		if (questGroup == null || !questGroup.IsOpened)
		{
			return;
		}
		_nextVisibilityCheck = Time.realtimeSinceStartup + 0.25f;
		QuestSystem questSystem = GameSystem<QuestSystem>.Instance();
		List<string> list = new List<string>();
		foreach (Category visibleCategory in questSystem.VisibleCategories)
		{
			float value;
			if (!visibleCategory.HasLoadedQuestList)
			{
				if (!_questRequests.TryGetValue(visibleCategory, out value) || Time.realtimeSinceStartup >= value)
				{
					_questRequests[visibleCategory] = Time.realtimeSinceStartup + 15f;
					visibleCategory.GetQuestList(delegate
					{
					});
				}
			}
			else if (!visibleCategory.HasQuestScore.HasValue)
			{
				bool flag = false;
				foreach (QuestToDo cachedQuest in visibleCategory.GetCachedQuestList())
				{
					if (!cachedQuest.Finished)
					{
						flag = true;
						break;
					}
				}
				if (!flag && (!_scoreRequests.TryGetValue(visibleCategory, out value) || Time.realtimeSinceStartup >= value))
				{
					_scoreRequests[visibleCategory] = Time.realtimeSinceStartup + 15f;
					questSystem.GetQuestScoreInfos(visibleCategory.Key);
				}
			}
			if (ShowCategory(visibleCategory))
			{
				list.Add(visibleCategory.Key);
			}
		}
		string text = string.Join("\n", list.ToArray());
		if (_visibilityKey != text)
		{
			_visibilityKey = text;
			_isDirty = true;
			RefreshTabList();
		}
		if (_categories.FindIndex((Category category) => category.Key == _selectedCategory) < 0 && _categories.Count > 0)
		{
			_selectedCategory = _categories[0].Key;
			if (this.TabClicked != null)
			{
				this.TabClicked(_selectedCategory);
			}
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (Application.isPlaying)
		{
			_isDirty = true;
		}
	}

	protected override void OnLinked()
	{
		base.OnLinked();
		if (Application.isPlaying)
		{
			base.Object.Clicked += OnClickTab;
		}
	}

	private void RefreshTabList()
	{
		if (!_isDirty)
		{
			return;
		}
		_isDirty = false;
		base.Object.BeginLoad();
		_categories.Clear();
		foreach (Category visibleCategory in GameSystem<QuestSystem>.Instance().VisibleCategories)
		{
			if (ShowCategory(visibleCategory))
			{
				_categories.Add(visibleCategory);
				base.Object.Add(null, visibleCategory.Name);
			}
		}
		base.Object.EndLoad();
		base.Object.Select(_categories.FindIndex((Category category) => category.Key == _selectedCategory));
		UpdateNotification();
	}

	public void SelectTab(string category)
	{
		_selectedCategory = category;
		RefreshTabList();
		int index = -1;
		for (int i = 0; i < _categories.Count; i++)
		{
			if (_categories[i].Key == category)
			{
				index = i;
				break;
			}
		}
		base.Object.Select(index);
	}

	public void UpdateNotification()
	{
		for (int i = 0; i < _categories.Count; i++)
		{
			base.Object.SetNotification(i, _categories[i].HasNotification(), Durango.Logic.Notification.Type.Important);
		}
	}

	public Transform GetQuestMenuTab(string category)
	{
		RefreshTabList();
		for (int i = 0; i < _categories.Count; i++)
		{
			if (_categories[i].Key == category)
			{
				IconTabWidget iconTabWidget = base.Object.Get(i);
				if (iconTabWidget == null)
				{
					return null;
				}
				return iconTabWidget.transform;
			}
		}
		return null;
	}

	private void OnClickTab(int index)
	{
		if (!Selectable.Current.Selected && this.TabClicked != null)
		{
			string obj = ((index >= 0 && index < _categories.Count) ? _categories[index].Key : null);
			this.TabClicked(obj);
		}
	}
}

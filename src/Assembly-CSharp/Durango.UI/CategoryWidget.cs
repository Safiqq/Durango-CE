using System;
using Crafting;
using Durango.UI.Control;
using JetBrains.Annotations;
using L10N;
using UnityEngine;

namespace Durango.UI;

public class CategoryWidget : SelectableWidget
{
	[SerializeField]
	private UILabel _textLabel;

	[SerializeField]
	private UISprite _iconSprite;

	[SerializeField]
	private UISprite _notificationObject;

	public const string RiftId = "_dgs02";

	public const string RiftPrefix = "dgs02_";

	public bool IsRift { get; private set; }

	[CanBeNull]
	public string Id
	{
		get
		{
			if (Category != null)
			{
				return Category.Id;
			}
			if (!IsRift)
			{
				return null;
			}
			return "_dgs02";
		}
	}

	[CanBeNull]
	public Category Category { get; private set; }

	public void SetEntireCategory()
	{
		if (Category != null)
		{
			Category.Notification.Changed -= OnChangeNewState;
		}
		Category = null;
		IsRift = false;
		_textLabel.text = T._("전체");
		_iconSprite.spriteName = "icon_search_big";
	}

	public void SetRiftCategory()
	{
		SetEntireCategory();
		IsRift = true;
		string text = T._("คราฟต\u0e4cรอยแยก");
		bool flag = (LocalizeSystem.Locale ?? string.Empty).StartsWith("th", StringComparison.OrdinalIgnoreCase);
		_textLabel.text = ((text == "คราฟต\u0e4cรอยแยก" && !flag) ? "Rift Crafting" : text);
		_iconSprite.spriteName = "warp_book";
	}

	public void SetCategory([NotNull] Category category)
	{
		if (Category != null)
		{
			Category.Notification.Changed -= OnChangeNewState;
		}
		Category = category;
		IsRift = false;
		SetNotification(Category.Notification.On);
		_textLabel.text = category.Name;
		_iconSprite.spriteName = IconMap.Get(category.Id, "icon_question");
		category.Notification.Changed += OnChangeNewState;
	}

	public void SetNotification(bool on)
	{
		_notificationObject.gameObject.SetActive(on);
	}

	protected override void OnInit()
	{
		ClickSound = UISound.ClickType.ButtonMedium;
	}

	private void OnChangeNewState()
	{
		SetNotification(Category != null && Category.Notification.On);
	}
}

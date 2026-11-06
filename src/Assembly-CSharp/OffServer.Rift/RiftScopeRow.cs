using System;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using L10N;
using UnityEngine;
using Yaml;

namespace OffServer.Rift;

public sealed class RiftScopeRow : MonoBehaviour
{
	private static readonly string[] Fields = new string[13]
	{
		"_mainLayout", "_infoLayout", "_fgSprite", "_titleLabel", "_subscriptionLabel", "_iconSprite", "_scoreLabel", "_rewardLabel", "_remainTimeLabel", "_progress",
		"_progressCountLabel", "_receiveButton", "_itemRoot"
	};

	[SerializeField]
	private RectLayoutComponent _mainLayout;

	[SerializeField]
	private RectLayoutComponent _infoLayout;

	[SerializeField]
	private UISprite _fgSprite;

	[SerializeField]
	private UISprite _iconSprite;

	[SerializeField]
	private UILabel _titleLabel;

	[SerializeField]
	private UILabel _subscriptionLabel;

	[SerializeField]
	private UILabel _scoreLabel;

	[SerializeField]
	private UILabel _rewardLabel;

	[SerializeField]
	private UILabel _remainTimeLabel;

	[SerializeField]
	private UILabel _progressCountLabel;

	[SerializeField]
	private UIProgressBar _progress;

	[SerializeField]
	private SelectableButton _receiveButton;

	[SerializeField]
	private UIWidget _itemRoot;

	private ScopeAnimal _animal;

	private Action<ScopeAnimal> _select;

	private Func<string, bool> _selected;

	private float _nextLive;

	public UILabel LabelTemplate => _titleLabel;

	public UISprite PortraitTemplate => _iconSprite;

	public SelectableButton ButtonTemplate => _receiveButton;

	public static RiftScopeRow Adopt(QuestNodeWidget node)
	{
		RiftScopeRow riftScopeRow = node.gameObject.AddComponent<RiftScopeRow>();
		string[] fields = Fields;
		foreach (string text in fields)
		{
			FieldInfo field = typeof(QuestNodeWidget).GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo field2 = typeof(RiftScopeRow).GetField(text, BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null && field2 != null)
			{
				field2.SetValue(riftScopeRow, field.GetValue(node));
			}
		}
		return riftScopeRow;
	}

	public void Init()
	{
		RectLayoutComponent[] componentsInChildren = GetComponentsInChildren<RectLayoutComponent>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		_receiveButton.Clicked = OnSelect;
		_receiveButton.ShortcutCommand = InputCommand.None;
		_receiveButton.ClearEffect();
		_itemRoot.gameObject.SetActive(value: false);
		_fgSprite.gameObject.SetActive(value: false);
		_scoreLabel.gameObject.SetActive(value: false);
		_rewardLabel.gameObject.SetActive(value: false);
		_remainTimeLabel.gameObject.SetActive(value: false);
		UIWidget component = GetComponent<UIWidget>();
		component.SetAnchor((Transform)null);
		component.pivot = UIWidget.Pivot.Center;
		component.width = 540;
		component.height = 134;
		Place(_iconSprite, 38f, -40f, 54, 54);
		Place(_titleLabel, 94f, -26f, 270, 30);
		Place(_subscriptionLabel, 94f, -61f, 290, 44);
		_titleLabel.overflowMethod = UILabel.Overflow.ClampContent;
		_titleLabel.maxLineCount = 1;
		_titleLabel.overflowEllipsis = true;
		_subscriptionLabel.overflowMethod = UILabel.Overflow.ClampContent;
		_subscriptionLabel.maxLineCount = 2;
		_subscriptionLabel.spacingY = 2;
		_subscriptionLabel.fontSize = 18;
		Place(_progress.GetComponent<UIWidget>(), 239f, -107f, 290, 22);
		_progress.foregroundWidget.color = new Color32(134, 12, 10, byte.MaxValue);
		_receiveButton.transform.SetParent(base.transform, worldPositionStays: false);
		Place(_receiveButton.Widget, 455f, -65f, 120, 58);
		_receiveButton.gameObject.SetActive(value: true);
		Transform transform = base.transform.Find("QuestBasePane");
		if (transform != null)
		{
			foreach (Transform item in transform)
			{
				item.gameObject.SetActive(value: false);
			}
			Transform transform2 = transform.Find("BottomLine");
			if (transform2 != null)
			{
				transform2.SetParent(base.transform, worldPositionStays: false);
				transform2.gameObject.SetActive(value: true);
				UIWidget component2 = transform2.GetComponent<UIWidget>();
				if (component2 != null)
				{
					component2.SetAnchor(base.gameObject, 0f, 0, 0f, 0, 1f, 0, 0f, 2);
				}
			}
		}
		UIUtility.UpdateAnchors(base.transform);
	}

	private void Place(UIWidget widget, float x, float y, int width, int height)
	{
		widget.transform.SetParent(base.transform, worldPositionStays: false);
		widget.SetAnchor((Transform)null);
		bool flag = widget is UILabel;
		widget.pivot = ((!flag) ? UIWidget.Pivot.Center : UIWidget.Pivot.TopLeft);
		widget.transform.localPosition = new Vector3(x - 270f, y + 67f, 0f);
		widget.width = width;
		widget.height = height;
		widget.gameObject.SetActive(value: true);
	}

	public void Set(ScopeAnimal animal, Action<ScopeAnimal> select, Func<string, bool> selected)
	{
		_animal = animal;
		_select = select;
		_selected = selected;
		string portrait = AnimalYaml.GetPortrait(animal.Type);
		bool flag = !string.IsNullOrEmpty(portrait) && ResourceSingleton<UISpriteManager>.Instance().GetSprite(portrait) != null;
		_iconSprite.spriteName = (flag ? portrait : RiftScope.MapIcon(animal));
		_iconSprite.color = (flag ? Color.white : RiftScope.RoleColor(animal.Role));
		_titleLabel.text = animal.DisplayName + " " + LocalizeUtil.FormatLevel(animal.Level);
		Refresh();
	}

	private void Refresh()
	{
		bool flag = RiftScope.IsEligible(_animal);
		_receiveButton.Disabled = !flag;
		_receiveButton.Text = ((_selected != null && _selected(_animal.Id)) ? RiftText.T("เล\u0e37อกแล\u0e49ว", "Selected") : RiftText.T("เล\u0e37อกต\u0e31วน\u0e35\u0e49", "Select"));
		RiftScope.Life(_animal, out var life, out var maxLife);
		_progress.value = ((maxLife > 0f) ? Mathf.Clamp01(life / maxLife) : 0f);
		_progressCountLabel.text = ((flag && maxLife > 0f) ? string.Format(T.Culture, "{0:N0} / {1:N0}", life, maxLife) : "—");
		_subscriptionLabel.text = (flag ? RiftScope.Describe(_animal, life) : RiftText.T("ไม\u0e48อย\u0e39\u0e48ในระยะท\u0e35\u0e48มองเห\u0e47นแล\u0e49ว", "No longer visible in range"));
	}

	private void OnSelect()
	{
		if (_select != null && RiftScope.IsEligible(_animal))
		{
			_select(_animal);
		}
	}

	private void Update()
	{
		if (_animal != null && !(Time.unscaledTime < _nextLive))
		{
			_nextLive = Time.unscaledTime + 0.25f;
			Refresh();
		}
	}
}

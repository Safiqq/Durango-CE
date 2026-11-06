using System.Collections.Generic;
using Durango.System.Config;
using Durango.UI.Popup;
using UnityEngine;

namespace Durango.UI;

[Uri("Config")]
public class ConfigGroup : UIBase
{
	[SerializeField]
	private ConfigTabWidget _tabWidget;

	[SerializeField]
	private ConfigMainWidget _mainWidget;

	private readonly Dictionary<GameObject, bool> _couponChildren = new Dictionary<GameObject, bool>();

	private Transform _couponContent;

	public UIWidget CouponHost => _mainWidget.CouponLayoutWidget;

	protected override bool IsSoundOcclusion => false;

	public void ShowCouponContent(Transform page)
	{
		RestoreCouponContent();
		foreach (Transform item in _mainWidget.transform)
		{
			if (!(item == page))
			{
				_couponChildren[item.gameObject] = item.gameObject.activeSelf;
				item.gameObject.SetActive(value: false);
			}
		}
		_couponContent = page;
		page.SetParent(_mainWidget.transform, worldPositionStays: false);
	}

	public void RestoreCouponContent()
	{
		foreach (KeyValuePair<GameObject, bool> couponChild in _couponChildren)
		{
			if (couponChild.Key != null)
			{
				couponChild.Key.SetActive(couponChild.Value);
			}
		}
		_couponChildren.Clear();
		_couponContent = null;
	}

	private void Awake()
	{
		_openCloseSound = UISound.GroupType.Default;
		SetChildrenActive(activated: false);
	}

	private void Start()
	{
		_tabWidget.TabClicked = TabClicked;
		base.OnOpenSucceed += ConfigGroup_OnOpenSucceed;
		base.OnCloseSucceed += ConfigGroup_OnCloseSucceed;
		GameSystem<InputSystem>.Instance().On(InputCommand.LogOut, delegate
		{
			ConfigInstance.Logout();
		});
	}

	public void Open(string category)
	{
		Open();
		_tabWidget.SelectTab(category);
	}

	protected override bool TryOpen()
	{
		UIBase.CloseAllUI();
		return base.TryOpen();
	}

	private void InitConfigTabs()
	{
		_tabWidget.Init();
	}

	private void ConfigGroup_OnOpenSucceed()
	{
		if (!_tabWidget.IsInit)
		{
			InitConfigTabs();
		}
		Reposition();
	}

	private void ConfigGroup_OnCloseSucceed()
	{
		OffServerCouponUI.Close();
		KUtility.DelayedCall(this, delegate
		{
			_mainWidget.ApplyChangedLocale();
		}, 0f);
	}

	private void Reposition()
	{
		_mainWidget.Reposition();
		_tabWidget.Reposition();
	}

	private void TabClicked(string category)
	{
		OffServerCouponUI.Close();
		_mainWidget.SetConfigLayout(category);
	}

	protected override void OnScreenResized()
	{
		base.OnScreenResized();
		if (base.IsOpened)
		{
			Reposition();
		}
	}

	[Uri("ServerStatus")]
	private void ShowServerStatus()
	{
		SendReportPopup sendReportPopup = UIManager.Popup.Tooltip<SendReportPopup>();
		sendReportPopup.SetForServerStatus();
		sendReportPopup.Show();
	}

	[Uri("Suggestion")]
	private void ShowSuggestion()
	{
		SendReportPopup sendReportPopup = UIManager.Popup.Tooltip<SendReportPopup>();
		sendReportPopup.SetForSuggestion();
		sendReportPopup.Show();
	}
}

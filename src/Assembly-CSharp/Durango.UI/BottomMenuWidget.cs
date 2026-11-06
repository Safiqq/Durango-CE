using Durango.System.Config;
using Durango.UI.Control;
using UnityEngine;

namespace Durango.UI;

public class BottomMenuWidget : BottomMenuWidgetBase
{
	[SerializeField]
	private RectLayoutComponent _layout;

	[SerializeField]
	private GameObject _spaceWidget;

	[SerializeField]
	private GameObject _sttButton;

	[SerializeField]
	private CommunicationButtonBase _keyboardButton;

	protected override void Start()
	{
		base.Start();
		UIManager.FindScript<MenuListGroupBase>().VisibleController.Changed += UpdateLayout;
		UIManager.AddOnScreenResized(OnScreenResize);
		if (_quickChatButton != null && _quickChatSelector != null)
		{
			_quickChatButton.Initailize(delegate
			{
				UIManager.Open<ChattingGroupBase>();
			}, _quickChatSelector.Show);
			_quickChatSelector.QuickChatClicked += OnClickQuickChat;
		}
		CommunityHudLayout.Register("chat_keyboard", _keyboardButton.transform, "พ\u0e34มพ\u0e4cแชท / Keyboard", "social");
		_keyboardButton.Initailize(delegate
		{
			UIManager.Open<ChatInputGroup>();
		}, null);
		_keyboardButton.gameObject.SetActive(Application.isMobilePlatform);
		bool active = Application.isEditor || Application.platform == RuntimePlatform.Android;
		_sttButton.gameObject.SetActive(active);
		CommunityVoiceUI.AttachMicButton(_sttButton, delegate
		{
			if (this != null)
			{
				OnScreenResize();
			}
		});
		CommunityRunButtonUI.Attach(() => this != null && base.gameObject.activeInHierarchy);
		if (CommunityHotkeys.Supported)
		{
			GameSystem<InputSystem>.Instance().On(InputCommand.CommunicationMenuButtonAction, delegate
			{
				OnClickCommunicationButton();
			});
		}
	}

	private void OnScreenResize()
	{
		_layout.UpdateLayout();
		UIUtility.UpdateAnchors(base.transform);
	}

	private void UpdateLayout(bool showSpace)
	{
		_spaceWidget.SetActive(showSpace);
		_layout.UpdateLayout();
		UIUtility.UpdateAnchors(base.transform);
	}
}

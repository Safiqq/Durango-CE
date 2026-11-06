using System;
using Durango.Network;
using Durango.System.Config;
using Durango.Utils.Extensions;
using UnityEngine;

namespace Durango.UI;

public class ToggleWidget : MonoBehaviour
{
	[SerializeField]
	public UILabel Text;

	[SerializeField]
	public GameObject Left;

	[SerializeField]
	public GameObject Right;

	public Action<string> ValueChanged;

	private string[] _options;

	public int Index
	{
		get
		{
			if (_options != null)
			{
				int num = ((Parent == null) ? _options.IndexOf(Text.text) : _options.IndexOf(Parent.Value as string));
				if (num == -1)
				{
					return 0;
				}
				return num;
			}
			return 0;
		}
	}

	public SettingItem Parent { get; set; }

	private void Start()
	{
		UIEventListener.Get(Left).onClick = delegate
		{
			UISound.PlayClick(UISound.ClickType.ButtonDefault);
			MoveIndex(-1);
		};
		UIEventListener.Get(Right).onClick = delegate
		{
			UISound.PlayClick(UISound.ClickType.ButtonDefault);
			MoveIndex(1);
		};
	}

	public void SetOptions(string[] options)
	{
		if (options != null && options.Length != 0)
		{
			_options = options;
			Text.text = _options[Index];
		}
	}

	public void OnLocalize(SettingType type = SettingType.Toggle)
	{
		if (Parent == null)
		{
			return;
		}
		if (type == SettingType.Locale)
		{
			string locale = Parent.Value.ToString();
			Text.text = ((!(Parent.Key == "locale")) ? LocalizeSystem.GetVoiceLocaleName(locale) : LocalizeSystem.GetLocaleName(locale));
			return;
		}
		string key = Parent.Key;
		object value = Parent.Value;
		string text = CommunitySettingsText.OptionLabel(key, value?.ToString());
		if (text != null)
		{
			Text.text = text;
			return;
		}
		if (key == "max_visible_players")
		{
			Text.text = CommunityGameplayOptions.MaxVisiblePlayersOptionLabel(value?.ToString());
			return;
		}
		string text2 = CommunityGameplayOptions.BloodOptionLabel(key, value?.ToString());
		if (text2 != null)
		{
			Text.text = text2;
			return;
		}
		switch (key)
		{
		case "net_transport":
			Text.text = CommunityUdp.OptionLabel(value?.ToString());
			return;
		case "combat_move_cancel":
			Text.text = CommunityCombatMoveCancel.OptionLabel(value?.ToString());
			return;
		case "mobile_move_mode":
			Text.text = CommunityMobileMove.OptionLabel(value?.ToString());
			return;
		case "ui_size":
		{
			string text3 = CommunityUiSize.OptionLabel(value?.ToString());
			if (text3 != null)
			{
				Text.text = text3;
				return;
			}
			break;
		}
		}
		string text4 = CommunityVoiceSettings.OptionLabel(key, value?.ToString());
		if (text4 != null)
		{
			Text.text = text4;
			return;
		}
		string text5 = "#config_" + key + "_" + value;
		string text6 = LocalizeSystem.Get(text5);
		Text.text = ((string.IsNullOrEmpty(text6) || text6 == text5 || text6.StartsWith("#config_")) ? Parent.Value.ToString() : text6);
	}

	public void MoveIndex(int offset)
	{
		int num = (Index + offset + _options.Length) % _options.Length;
		string text = _options[num];
		if (Parent != null)
		{
			if (ValueChanged != null)
			{
				ValueChanged(text);
			}
		}
		else
		{
			Text.text = text;
		}
	}
}

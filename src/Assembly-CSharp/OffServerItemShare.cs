using System;
using System.IO;
using Durango.Logic.Clusters;
using Durango.Logic.Item;
using Durango.UI.Control;
using Durango.UI.Popup;
using L10N;
using Messages;
using MsgPack;
using Shared.Chat;
using Yaml;

public static class OffServerItemShare
{
	public const string Caps = "item-link";

	public const string ItemKey = "item";

	public const string ProtoKey = "proto";

	public const string LevelKey = "lv";

	public static bool CanShare(ItemData item)
	{
		if (item != null && GameManager.ClusterMode == Mode.Online)
		{
			if (string.IsNullOrEmpty(item.Id))
			{
				return !string.IsNullOrEmpty(item.PrototypeId);
			}
			return true;
		}
		return false;
	}

	public static string CommandFor(ItemData item)
	{
		if (!string.IsNullOrEmpty(item.Id) && item.Id.IndexOf(' ') < 0)
		{
			return "/item @" + item.Id;
		}
		return "/item " + item.PrototypeId;
	}

	public static void PickAndShare()
	{
		PopupItemSelector popupItemSelector = UIManager.Popup.Tooltip<PopupItemSelector>();
		popupItemSelector.MyInventory().Filter(CanShare).SelectableCount(1)
			.ConfirmText(T._("공유하기"))
			.OnConfirmed(delegate(ItemData item)
			{
				if (item != null)
				{
					Share(item, toast: false);
				}
			})
			.Title(T._("공유할 아이템을 선택해주세요."));
		popupItemSelector.Show();
	}

	public static bool Share(ItemData item, bool toast)
	{
		if (!CanShare(item))
		{
			return false;
		}
		SocialSystem socialSystem = GameSystem<SocialSystem>.Instance();
		if (socialSystem == null)
		{
			return false;
		}
		bool flag = socialSystem.CanSay();
		ChannelType? channelType = null;
		if (!SocialSystem.IsAllowedChannel(socialSystem.CurrentChannel.Type))
		{
			channelType = ChannelType.Region;
		}
		socialSystem.SystemSay(new RadioTalk
		{
			Text = CommandFor(item)
		}, channelType);
		if (!flag)
		{
			UIManager.SystemMsg(T._("채팅 서버와 연결이 끊어졌습니다."));
			return false;
		}
		if (toast)
		{
			UIManager.SystemMsg(T._("아이템을 채팅에 공유했습니다."));
		}
		return true;
	}

	public static bool TryOpen(ParamsDictionary param)
	{
		if (param == null)
		{
			return false;
		}
		param.TryGetValue("item", out var value);
		param.TryGetValue("proto", out var value2);
		if (string.IsNullOrEmpty(value) && string.IsNullOrEmpty(value2))
		{
			return false;
		}
		ItemData itemData = Decode(value);
		int level = Math.Max(1, param.GetInt("lv", 1));
		if (itemData == null && (string.IsNullOrEmpty(value2) || PrototypeYaml.GetItemPrototype(value2, level) == null))
		{
			UIManager.SystemMsg(T._("아이템 정보를 불러올 수 없습니다."));
			return true;
		}
		ItemInfoTooltip itemInfoTooltip = UIManager.Popup.Tooltip<ItemInfoTooltip>();
		if (itemData != null)
		{
			itemInfoTooltip.Set(itemData);
		}
		else
		{
			itemInfoTooltip.Set(value2, level);
		}
		itemInfoTooltip.Show();
		return true;
	}

	public static ItemData Decode(string payload)
	{
		if (string.IsNullOrEmpty(payload))
		{
			return null;
		}
		try
		{
			string text = payload.Trim().Replace('-', '+').Replace('_', '/');
			switch (text.Length % 4)
			{
			case 1:
				return null;
			case 2:
				text += "==";
				break;
			case 3:
				text += "=";
				break;
			}
			using MemoryStream stream = new MemoryStream(Convert.FromBase64String(text));
			using Unpacker unpacker = Unpacker.Create(stream);
			if (!unpacker.Read() || !unpacker.IsArrayHeader)
			{
				return null;
			}
			Item itemInfo = Item.Unpack(unpacker);
			if (string.IsNullOrEmpty(itemInfo.Prototype) || itemInfo.Tags == null || itemInfo.TagModifications == null)
			{
				return null;
			}
			return new ItemData(itemInfo);
		}
		catch (Exception ex)
		{
			OffServerLog.Info("[OffServer] CHAT-SHARE item link decode failed: " + ex.Message);
			return null;
		}
	}
}

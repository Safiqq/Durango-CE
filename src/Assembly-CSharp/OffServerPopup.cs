using System;
using Durango.Network;
using Messages;

public static class OffServerPopup
{
	public const string Caps = "server-popup";

	public const byte LevelNotice = 100;

	public const byte LevelConfirm = 101;

	public const char Separator = '\u001f';

	public static bool IsPopup(Announce msg)
	{
		if (msg.Level != 100)
		{
			return msg.Level == 101;
		}
		return true;
	}

	public static bool TryShow(Announce msg, PacketHeader header, Action<bool> onClose = null)
	{
		if (!IsPopup(msg) || UIManager.MessageBox == null)
		{
			return false;
		}
		string[] parts = (msg.Text ?? string.Empty).Split('\u001f');
		string mainText = Part(parts, 0) ?? string.Empty;
		string subText = Part(parts, 1);
		string confirm = Part(parts, 2);
		string cancel = Part(parts, 3);
		uint replyOf = header.ReplyOf;
		if (msg.Level == 101)
		{
			UIManager.MessageBox.Show(mainText, subText, delegate(bool accepted)
			{
				if (replyOf != 0)
				{
					Connections.Frontend.Send(new Confirm
					{
						Confirmation = accepted
					}, noReply: false, replyOf);
				}
				if (onClose != null)
				{
					onClose(accepted);
				}
			}, confirm, cancel);
		}
		else
		{
			UIManager.MessageBox.Show(mainText, subText, (Action)delegate
			{
				if (onClose != null)
				{
					onClose(obj: true);
				}
			}, confirm);
		}
		return true;
	}

	private static string Part(string[] parts, int index)
	{
		if (index >= parts.Length || string.IsNullOrEmpty(parts[index]))
		{
			return null;
		}
		return parts[index];
	}
}

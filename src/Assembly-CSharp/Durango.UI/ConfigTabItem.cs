using Durango.Logic.Clusters;
using Durango.System.Config;
using Durango.UI.Control;
using UnityEngine;

namespace Durango.UI;

public class ConfigTabItem : SelectableWidget
{
	[SerializeField]
	private UILabel _nameLabel;

	public string Category { get; private set; }

	protected override void OnInit()
	{
		ClickSound = UISound.ClickType.ButtonMedium;
	}

	public void Set(string category)
	{
		Category = category;
		_nameLabel.maxLineCount = 2;
		_nameLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
		_nameLabel.text = CommunitySettingsMenu.Label(category);
		if (CommunityDiscordLink.Lang == "th")
		{
			if (category == "screen")
			{
				_nameLabel.text = "ภาพและ\nประส\u0e34ทธ\u0e34ภาพ";
			}
			if (category == "community_combat")
			{
				_nameLabel.text = "ต\u0e48อส\u0e39\u0e49และ\nเอฟเฟกต\u0e4c";
			}
			if (category == "community_chat")
			{
				_nameLabel.text = "แชทและ\nการแสดงผล";
			}
		}
	}
}

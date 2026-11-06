using System;
using System.Collections.Generic;
using Durango.Network;
using Durango.UI.Popup;
using JetBrains.Annotations;
using L10N;
using Messages;
using Shared.Faction;
using UnityEngine;

namespace Durango.UI;

public class MissionBonusInfoWidget : UIWidget
{
	[SerializeField]
	private UIWidget _iconSprite;

	[SerializeField]
	private UIWidget _bgSprite;

	[SerializeField]
	private UILabel _infoLabel;

	[SerializeField]
	private UILabel _countLabel;

	[UsedImplicitly]
	private void OnClick()
	{
		WidgetTooltipControl widgetTooltipControl = UIManager.Popup.Tooltip<WidgetTooltipControl>();
		widgetTooltipControl.Set(null, T._("보너스 보상은 <em>매일 5회</em> 충전됩니다"));
		widgetTooltipControl.Sign = 1;
		widgetTooltipControl.Direction = TooltipBase.TooltipDirection.Vertical;
		widgetTooltipControl.Show(_bgSprite, Vector2.zero, 60f);
	}

	public void Set(MissionBonusReward? bonusReward, FactionType type)
	{
		int value = 0;
		int num;
		if (bonusReward.HasValue)
		{
			Dictionary<FactionType, int> friendshipPoint = bonusReward.Value.Rewards.FriendshipPoint;
			num = ((friendshipPoint != null && friendshipPoint.TryGetValue(type, out value)) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		bool flag = (byte)num != 0;
		if (flag)
		{
			MissionBonusReward bonus = bonusReward.Value;
			if (bonus.LeftCount > 0)
			{
				_infoLabel.text = $"<em>{value}</em> [icon=bg_line_height] ";
				_countLabel.gameObject.SetActive(value: true);
				_countLabel.text = $"<em>{bonus.LeftCount}</em>/{bonus.MaxCount}[icon=img_loading_unknown_question2]";
				UpdateLayout();
			}
			else
			{
				_countLabel.gameObject.SetActive(value: false);
				_infoLabel.SetText(new SyncString(delegate(out string text, out float period)
				{
					double num2 = Math.Max(0.0, bonus.ValidUntil - Connections.Frontend.GetPredictedServerTime());
					text = TimedeltaFormatter.ColonFormat(num2) + "[icon=img_loading_unknown_question2]";
					period = ((!(num2 > 0.0)) ? 0f : ((float)(num2 % 1.0)));
					UpdateLayout();
				}));
			}
		}
		base.gameObject.SetActive(flag);
	}

	private void UpdateLayout()
	{
		int num = (int)((float)(_iconSprite.width + 6) + _infoLabel.printedSize.x + 6f);
		if (_countLabel.gameObject.activeSelf)
		{
			num += (int)_countLabel.printedSize.x;
		}
		_bgSprite.width = num;
		_countLabel.transform.localPosition = new Vector3(_infoLabel.transform.localPosition.x + _infoLabel.printedSize.x, 0f, 0f);
		base.width = _bgSprite.width + 20;
	}
}

using System.Collections.Generic;
using Durango.Logic.InputSystem;
using Durango.System.Config;
using Durango.UI.Control;
using Durango.Utils;
using InteractionData;
using UnityEngine;

namespace Durango.UI;

public class ContextActionButtons : ContextActionButtonsBase
{
	protected override void Start()
	{
		base.Start();
		GameSystem<InteractionSystem>.Instance().RegisterContextActionFinder(ContextActionFinder);
	}

	public override void SetActions(List<InteractionMenuData> menus)
	{
		base.SetActions(menus);
		if (OffServerHotkeys.Supported)
		{
			InputSystem inputSystem = GameSystem<InputSystem>.Instance();
			for (int i = 0; i < 8; i++)
			{
				inputSystem.Off((InputCommand)(70 + i), OnDoContextAction);
			}
			int num = ((menus != null && _actionSlotPositions != null) ? Mathf.Min(Mathf.Min(menus.Count, _actionSlotPositions.Length), 8) : 0);
			for (int j = 0; j < num; j++)
			{
				inputSystem.On((InputCommand)(70 + j), OnDoContextAction);
			}
		}
	}

	private void OnDoContextAction(InputCommandMessage message)
	{
		int num = (int)(message.Command - 70);
		if (num < 0 || num >= _actionButtons.Count)
		{
			return;
		}
		ContextActionButtonBase contextActionButtonBase = _actionButtons[num];
		SelectableWidget component = contextActionButtonBase.GetComponent<SelectableWidget>();
		switch (message.CurrentTrigger)
		{
		case Trigger.Down:
			if (component != null)
			{
				component.SetState(Selectable.State.Pressed);
			}
			contextActionButtonBase.OnPress(press: true);
			break;
		case Trigger.Up:
			if (component != null)
			{
				component.SetState(Selectable.State.Normal);
			}
			contextActionButtonBase.OnPress(press: false);
			OnClickActionButton(contextActionButtonBase);
			break;
		}
	}

	private void ContextActionFinder(List<InteractionMenuData> result)
	{
		if (Singleton<PlayerController>.Instance().CanTryJump())
		{
			result.Add(Interaction.Dash);
		}
	}
}

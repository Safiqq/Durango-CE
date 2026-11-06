using System;
using Durango.UI;
using Durango.UI.Popup;

public static class OffServerMoveClose
{
	private const int MaxSweep = 32;

	public static void OnPlayerMoveStart()
	{
		try
		{
			CloseWindows();
			TooltipBase.HideNonModal(KeepTooltip);
			MenuListGroupBase menuListGroupBase = UIManager.FindScript<MenuListGroupBase>();
			if (menuListGroupBase != null && menuListGroupBase.IsOpened)
			{
				menuListGroupBase.Close();
			}
			InteractionGroup interactionGroup = UIManager.FindScript<InteractionGroup>();
			if (interactionGroup != null && interactionGroup.InteractionMenu != null)
			{
				interactionGroup.InteractionMenu.CloseMenus();
			}
			if (OffServerVoiceUI.PanelOpen)
			{
				OffServerVoiceUI.ClosePanel();
			}
		}
		catch (Exception)
		{
		}
	}

	private static void CloseWindows()
	{
		for (int i = 0; i < 32; i++)
		{
			UIBase currentUI = UIBase.CurrentUI;
			if (currentUI == null || !currentUI.IsOpened || Keep(currentUI))
			{
				break;
			}
			currentUI.ForceClose();
			if (UIBase.CurrentUI == currentUI)
			{
				break;
			}
		}
	}

	public static bool Keep(UIBase ui)
	{
		if (!(ui is ChattingGroupBase) && !(ui is BuildGridGroupBase) && !(ui is EstateGridGroup) && !(ui is ScreenCaptureGroup) && !(ui is FullScreenMovieGroupBase))
		{
			return ui is CutsceneGroup;
		}
		return true;
	}

	private static bool KeepTooltip(TooltipBase tooltip)
	{
		return tooltip is TutorialBoatTooltip;
	}
}

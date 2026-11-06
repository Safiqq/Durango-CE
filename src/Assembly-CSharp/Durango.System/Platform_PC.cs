using Durango.Logic.Clusters;
using Durango.Network;
using Durango.System.Config;
using Durango.UI;
using Durango.UI.Popup;
using Messages;
using UnityEngine;

namespace Durango.System;

public class Platform_PC : Platform
{
	public override string NPSN
	{
		get
		{
			if (OffServerLink.Active)
			{
				return OffServerLink.AccountId;
			}
			return string.Empty;
		}
	}

	public override string NPA
	{
		get
		{
			if (!string.IsNullOrEmpty(OffServerLink.DiscordName))
			{
				return "Discord: " + OffServerLink.DiscordName;
			}
			return "Guest / Unlinked";
		}
	}

	public override bool IsLoginTypeGuest => true;

	public override bool IsPCStore => true;

	public override string AppBundleId => "com.nexon.durango.wildlands";

	public override bool IsConnectFacebook => false;

	public override bool IsConnectGooglePlus => false;

	public override bool IsAvailableOfferwall => false;

	public override bool UsePCUI => UiMode.UsePCUI;

	public override int DefaultUISize => 1280;

	public override bool UsePCRenderer => Application.platform != RuntimePlatform.Android;

	public override bool SupportPortrait => false;

	public override int DefaultRenderTargetSize => 1024;

	public override string PrologueMovieUrl => "https://d1skbslnewf3os.cloudfront.net/prologue_movie_pc.mp4";

	public override void ShowAccountMenu()
	{
		UnityEngine.Debug.Log("[OffServer] ShowAccountMenu — Open Account Transfer (Key=" + OffServerLink.AccountId + " · Discord=" + OffServerLink.DiscordName + ")");
		UIManager.Popup.Tooltip<TextInputPopup>().Show(delegate(string step1)
		{
			step1 = (step1 ?? string.Empty).Trim();
			if (step1.Length != 0)
			{
				UIManager.Popup.Tooltip<TextInputPopup>().Show(delegate(string text2)
				{
					string text3 = (text2 ?? string.Empty).Trim();
					if (text3.Length != 0)
					{
						Connections.Frontend.Send(new AcceptTENCoupon
						{
							CouponNum = step1 + " " + text3,
							ToyToken = OffServerLink.AccountId
						}).On<OK>(delegate
						{
							UIManager.Alarm.ShowNotify("Success — Return to Title to load characters for this account", "icon_mainhud_shop", major: false);
						});
					}
				}, "Step 2/2 — Enter Account Password (8+ characters; set new password if not configured)");
			}
		}, "Step 1/2 — Enter 6-digit Bot Code or Full Account Number");
	}

	public override bool GetScreenResolution(bool isPortrait, out int width, out int height)
	{
		Point2 screenResolution = GetScreenResolution();
		width = screenResolution.x;
		height = screenResolution.y;
		return true;
	}

	public static Point2 GetScreenResolution()
	{
		Point2 result = new Point2
		{
			x = (int)((float)Screen.width * 96f / Screen.dpi),
			y = (int)((float)Screen.height * 96f / Screen.dpi)
		};
		int b = (int)(1600f * (float)Mathf.Min(UIManager.UISize, 1280) / 1280f);
		int num = Mathf.Max((int)((float)(result.x * UIManager.UISize) / 1280f), b);
		int y = Mathf.RoundToInt((float)num * UIAnchorPolicy.DefaultAspectRatio);
		result.x = num;
		result.y = y;
		return result;
	}
}

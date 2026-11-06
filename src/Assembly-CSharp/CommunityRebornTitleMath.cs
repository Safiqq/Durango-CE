using System;

public static class CommunityRebornTitleMath
{
	public static void FitInside(int boxWidth, int boxHeight, int imageWidth, int imageHeight, out int width, out int height)
	{
		if (boxWidth <= 0 || boxHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
		{
			width = boxWidth;
			height = boxHeight;
		}
		else
		{
			double num = Math.Min((double)boxWidth / (double)imageWidth, (double)boxHeight / (double)imageHeight);
			width = Math.Max(1, Math.Min(boxWidth, (int)Math.Round((double)imageWidth * num)));
			height = Math.Max(1, Math.Min(boxHeight, (int)Math.Round((double)imageHeight * num)));
		}
	}

	public static float RightAlignShift(int videoWidth, int screenWidth)
	{
		if (screenWidth <= 0 || videoWidth <= screenWidth)
		{
			return 0f;
		}
		return (float)(-(videoWidth - screenWidth)) * 0.5f;
	}

	public static bool HideLogoOverlay(bool videoHasLogo, bool isTitleVideo, bool videoFailed)
	{
		if (videoHasLogo && isTitleVideo)
		{
			return !videoFailed;
		}
		return false;
	}

	public static bool IsTitleVideo(string videoName)
	{
		if (string.IsNullOrEmpty(videoName))
		{
			return false;
		}
		string text = videoName.Replace('\\', '/');
		int num = text.LastIndexOf('/');
		return string.Equals((num >= 0) ? text.Substring(num + 1) : text, "title.mp4", StringComparison.OrdinalIgnoreCase);
	}
}

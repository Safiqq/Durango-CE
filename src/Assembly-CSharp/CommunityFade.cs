using UnityEngine;

public class CommunityFade : UITweener
{
	public float from;

	public float to = 1f;

	public float rest = 1f;

	private UIRect _rect;

	private UIRect Rect
	{
		get
		{
			if (_rect == null)
			{
				_rect = GetComponent<UIRect>();
			}
			return _rect;
		}
	}

	public void CaptureRest()
	{
		if (Rect != null)
		{
			rest = Rect.alpha;
		}
	}

	public void Restore()
	{
		if (Rect != null)
		{
			Rect.alpha = rest;
		}
	}

	protected override void OnUpdate(float factor, bool isFinished)
	{
		if (Rect != null)
		{
			Rect.alpha = rest * Mathf.Lerp(from, to, factor);
		}
	}
}

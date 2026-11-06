using UnityEngine;

public class CommunityVoicePcMicPlacer : MonoBehaviour
{
	public Transform Chat;

	public Transform Emote;

	private Vector3 _step = new Vector3(0f, 42f, 0f);

	private void LateUpdate()
	{
		Place();
	}

	public void Place()
	{
		if (Chat == null)
		{
			return;
		}
		if (Emote != null && Emote.gameObject.activeSelf && Chat.gameObject.activeSelf)
		{
			Vector3 step = Chat.localPosition - Emote.localPosition;
			if (step.y > 10f)
			{
				_step = step;
			}
		}
		Vector3 vector = Chat.localPosition + _step * 2f;
		if ((base.transform.localPosition - vector).sqrMagnitude > 0.01f)
		{
			base.transform.localPosition = vector;
		}
	}
}

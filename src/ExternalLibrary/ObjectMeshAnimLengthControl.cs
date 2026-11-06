using System.Collections;
using UnityEngine;

public class ObjectMeshAnimLengthControl : MonoBehaviour
{
	public GameObject target;

	public float timer = 1f;

	private Animation anim;

	private AnimationClip animClip;

	private void Awake()
	{
		if (target.GetComponent<Animation>() != null)
		{
			anim = target.GetComponent<Animation>();
			anim["Take 001"].speed = 1f / timer;
			anim.Play();
			StartCoroutine(DelayDeactive(1f / anim["Take 001"].speed));
		}
	}

	private void Enable()
	{
		if (!target.activeInHierarchy)
		{
			target.SetActive(value: true);
		}
	}

	private IEnumerator DelayDeactive(float playTime)
	{
		yield return new WaitForSeconds(playTime);
		target.SetActive(value: false);
	}
}

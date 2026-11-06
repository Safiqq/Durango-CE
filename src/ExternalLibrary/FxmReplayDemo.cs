using UnityEngine;

public class FxmReplayDemo : MonoBehaviour
{
	public GameObject m_TargetPrefab;

	public GameObject m_InstanceObj;

	private void Start()
	{
		CreateEffect();
	}

	private void Update()
	{
	}

	private void CreateEffect()
	{
		if (!(m_TargetPrefab == null))
		{
			m_InstanceObj = NsEffectManager.CreateReplayEffect(m_TargetPrefab);
			NsEffectManager.PreloadResource(m_InstanceObj);
		}
	}

	private void Replay(bool bClearOldParticle)
	{
		NsEffectManager.RunReplayEffect(m_InstanceObj, bClearOldParticle);
	}

	private void OnGUI()
	{
		if (GUI.Button(GetButtonRect(0), "Replay"))
		{
			Replay(bClearOldParticle: false);
		}
		if (GUI.Button(GetButtonRect(1), "Replay(ClearParticle)"))
		{
			Replay(bClearOldParticle: true);
		}
	}

	public static Rect GetButtonRect(int nIndex)
	{
		return new Rect(Screen.width - Screen.width / 8 * (nIndex + 1), Screen.height - Screen.height / 10, Screen.width / 8, Screen.height / 10);
	}

	public static void SetActiveRecursively(GameObject target, bool bActive)
	{
		int num = target.transform.childCount - 1;
		while (0 <= num)
		{
			if (num < target.transform.childCount)
			{
				SetActiveRecursively(target.transform.GetChild(num).gameObject, bActive);
			}
			num--;
		}
		target.SetActive(bActive);
	}
}

using UnityEngine;

public class NcDetachObject : NcEffectBehaviour
{
	public GameObject m_LinkGameObject;

	public static NcDetachObject Create(GameObject parentObj, GameObject linkObject)
	{
		NcDetachObject ncDetachObject = parentObj.AddComponent<NcDetachObject>();
		ncDetachObject.m_LinkGameObject = linkObject;
		return ncDetachObject;
	}

	public override void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)
	{
		if (bRuntime)
		{
			NsEffectManager.AdjustSpeedRuntime(m_LinkGameObject, fSpeedRate);
		}
	}

	public override void OnSetActiveRecursively(bool bActive)
	{
		if (m_LinkGameObject != null)
		{
			NsEffectManager.SetActiveRecursively(m_LinkGameObject, bActive);
		}
	}
}

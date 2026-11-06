using System;
using System.Collections.Generic;
using UnityEngine;

public class NsEffectManager : MonoBehaviour
{
	public static Texture[] PreloadResource(GameObject tarObj)
	{
		if (tarObj == null)
		{
			return new Texture[0];
		}
		List<GameObject> list = new List<GameObject>();
		list.Add(tarObj);
		return PreloadResource(tarObj, list);
	}

	public static Component GetComponentInChildren(GameObject tarObj, Type findType)
	{
		if (tarObj == null)
		{
			return null;
		}
		List<GameObject> list = new List<GameObject>();
		list.Add(tarObj);
		return GetComponentInChildren(tarObj, findType, list);
	}

	public static GameObject CreateReplayEffect(GameObject tarPrefab)
	{
		if (tarPrefab == null)
		{
			return null;
		}
		if (!NcEffectBehaviour.IsSafe())
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(tarPrefab);
		SetReplayEffect(gameObject);
		return gameObject;
	}

	public static void SetReplayEffect(GameObject instanceObj)
	{
		PreloadResource(instanceObj);
		SetActiveRecursively(instanceObj, bActive: false);
		NcEffectBehaviour[] componentsInChildren = instanceObj.GetComponentsInChildren<NcEffectBehaviour>(includeInactive: true);
		NcEffectBehaviour[] array = componentsInChildren;
		foreach (NcEffectBehaviour ncEffectBehaviour in array)
		{
			ncEffectBehaviour.OnSetReplayState();
		}
	}

	public static void RunReplayEffect(GameObject instanceObj, bool bClearOldParticle)
	{
		SetActiveRecursively(instanceObj, bActive: true);
		NcEffectBehaviour[] componentsInChildren = instanceObj.GetComponentsInChildren<NcEffectBehaviour>(includeInactive: true);
		NcEffectBehaviour[] array = componentsInChildren;
		foreach (NcEffectBehaviour ncEffectBehaviour in array)
		{
			ncEffectBehaviour.OnResetReplayStage(bClearOldParticle);
		}
	}

	public static void AdjustSpeedRuntime(GameObject target, float fSpeedRate)
	{
		NcEffectBehaviour[] componentsInChildren = target.GetComponentsInChildren<NcEffectBehaviour>(includeInactive: true);
		NcEffectBehaviour[] array = componentsInChildren;
		foreach (NcEffectBehaviour ncEffectBehaviour in array)
		{
			ncEffectBehaviour.OnUpdateEffectSpeed(fSpeedRate, bRuntime: true);
		}
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

	public static bool IsActive(GameObject target)
	{
		return target.activeInHierarchy && target.activeSelf;
	}

	protected static void SetActiveRecursivelyEffect(GameObject target, bool bActive)
	{
		NcEffectBehaviour[] componentsInChildren = target.GetComponentsInChildren<NcEffectBehaviour>(includeInactive: true);
		NcEffectBehaviour[] array = componentsInChildren;
		foreach (NcEffectBehaviour ncEffectBehaviour in array)
		{
			ncEffectBehaviour.OnSetActiveRecursively(bActive);
		}
	}

	protected static Texture[] PreloadResource(GameObject tarObj, List<GameObject> parentPrefabList)
	{
		if (!NcEffectBehaviour.IsSafe())
		{
			return null;
		}
		Renderer[] componentsInChildren = tarObj.GetComponentsInChildren<Renderer>(includeInactive: true);
		List<Texture> list = new List<Texture>();
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length <= 0)
			{
				continue;
			}
			Material[] sharedMaterials = renderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if (material != null && material.mainTexture != null)
				{
					list.Add(material.mainTexture);
				}
			}
		}
		NcAttachPrefab[] componentsInChildren2 = tarObj.GetComponentsInChildren<NcAttachPrefab>(includeInactive: true);
		NcAttachPrefab[] array2 = componentsInChildren2;
		foreach (NcAttachPrefab ncAttachPrefab in array2)
		{
			if (ncAttachPrefab.m_AttachPrefab != null)
			{
				Texture[] array3 = PreloadPrefab(ncAttachPrefab.m_AttachPrefab, parentPrefabList, bCheckDup: true);
				if (array3 == null)
				{
					ncAttachPrefab.m_AttachPrefab = null;
				}
				else
				{
					list.AddRange(array3);
				}
			}
		}
		NcParticleSystem[] componentsInChildren3 = tarObj.GetComponentsInChildren<NcParticleSystem>(includeInactive: true);
		NcParticleSystem[] array4 = componentsInChildren3;
		foreach (NcParticleSystem ncParticleSystem in array4)
		{
			if (ncParticleSystem.m_AttachPrefab != null)
			{
				Texture[] array5 = PreloadPrefab(ncParticleSystem.m_AttachPrefab, parentPrefabList, bCheckDup: true);
				if (array5 == null)
				{
					ncParticleSystem.m_AttachPrefab = null;
				}
				else
				{
					list.AddRange(array5);
				}
			}
		}
		NcSpriteTexture[] componentsInChildren4 = tarObj.GetComponentsInChildren<NcSpriteTexture>(includeInactive: true);
		NcSpriteTexture[] array6 = componentsInChildren4;
		foreach (NcSpriteTexture ncSpriteTexture in array6)
		{
			if (ncSpriteTexture.m_NcSpriteFactoryPrefab != null)
			{
				Texture[] array7 = PreloadPrefab(ncSpriteTexture.m_NcSpriteFactoryPrefab, parentPrefabList, bCheckDup: false);
				if (array7 != null)
				{
					list.AddRange(array7);
				}
			}
		}
		NcParticleEmit[] componentsInChildren5 = tarObj.GetComponentsInChildren<NcParticleEmit>(includeInactive: true);
		NcParticleEmit[] array8 = componentsInChildren5;
		foreach (NcParticleEmit ncParticleEmit in array8)
		{
			if (ncParticleEmit.m_ParticlePrefab != null)
			{
				Texture[] array9 = PreloadPrefab(ncParticleEmit.m_ParticlePrefab, parentPrefabList, bCheckDup: false);
				if (array9 != null)
				{
					list.AddRange(array9);
				}
			}
		}
		NcAttachSound[] componentsInChildren6 = tarObj.GetComponentsInChildren<NcAttachSound>(includeInactive: true);
		NcAttachSound[] array10 = componentsInChildren6;
		foreach (NcAttachSound ncAttachSound in array10)
		{
			if (ncAttachSound.m_AudioClip != null)
			{
			}
		}
		NcSpriteFactory[] componentsInChildren7 = tarObj.GetComponentsInChildren<NcSpriteFactory>(includeInactive: true);
		NcSpriteFactory[] array11 = componentsInChildren7;
		foreach (NcSpriteFactory ncSpriteFactory in array11)
		{
			if (ncSpriteFactory.m_SpriteList == null)
			{
				continue;
			}
			for (int num3 = 0; num3 < ncSpriteFactory.m_SpriteList.Count; num3++)
			{
				if (ncSpriteFactory.m_SpriteList[num3].m_EffectPrefab != null)
				{
					Texture[] array12 = PreloadPrefab(ncSpriteFactory.m_SpriteList[num3].m_EffectPrefab, parentPrefabList, bCheckDup: true);
					if (array12 == null)
					{
						ncSpriteFactory.m_SpriteList[num3].m_EffectPrefab = null;
					}
					else
					{
						list.AddRange(array12);
					}
					if (!(ncSpriteFactory.m_SpriteList[num3].m_AudioClip != null))
					{
					}
				}
			}
		}
		return list.ToArray();
	}

	protected static Texture[] PreloadPrefab(GameObject tarObj, List<GameObject> parentPrefabList, bool bCheckDup)
	{
		if (parentPrefabList.Contains(tarObj))
		{
			if (bCheckDup)
			{
				string text = string.Empty;
				for (int i = 0; i < parentPrefabList.Count; i++)
				{
					text = text + parentPrefabList[i].name + "/";
				}
				return null;
			}
			return null;
		}
		parentPrefabList.Add(tarObj);
		Texture[] result = PreloadResource(tarObj, parentPrefabList);
		parentPrefabList.Remove(tarObj);
		return result;
	}

	protected static Component GetComponentInChildren(GameObject tarObj, Type findType, List<GameObject> parentPrefabList)
	{
		Component[] componentsInChildren = tarObj.GetComponentsInChildren(findType, includeInactive: true);
		Component[] array = componentsInChildren;
		foreach (Component component in array)
		{
			if (component.GetComponent<NcDontActive>() == null)
			{
				return component;
			}
		}
		NcAttachPrefab[] componentsInChildren2 = tarObj.GetComponentsInChildren<NcAttachPrefab>(includeInactive: true);
		NcAttachPrefab[] array2 = componentsInChildren2;
		foreach (NcAttachPrefab ncAttachPrefab in array2)
		{
			if (ncAttachPrefab.m_AttachPrefab != null)
			{
				Component validComponentInChildren = GetValidComponentInChildren(ncAttachPrefab.m_AttachPrefab, findType, parentPrefabList, bCheckDup: true);
				if (validComponentInChildren != null)
				{
					return validComponentInChildren;
				}
			}
		}
		NcParticleSystem[] componentsInChildren3 = tarObj.GetComponentsInChildren<NcParticleSystem>(includeInactive: true);
		NcParticleSystem[] array3 = componentsInChildren3;
		foreach (NcParticleSystem ncParticleSystem in array3)
		{
			if (ncParticleSystem.m_AttachPrefab != null)
			{
				Component validComponentInChildren = GetValidComponentInChildren(ncParticleSystem.m_AttachPrefab, findType, parentPrefabList, bCheckDup: true);
				if (validComponentInChildren != null)
				{
					return validComponentInChildren;
				}
			}
		}
		NcSpriteTexture[] componentsInChildren4 = tarObj.GetComponentsInChildren<NcSpriteTexture>(includeInactive: true);
		NcSpriteTexture[] array4 = componentsInChildren4;
		foreach (NcSpriteTexture ncSpriteTexture in array4)
		{
			if (ncSpriteTexture.m_NcSpriteFactoryPrefab != null)
			{
				Component validComponentInChildren = GetValidComponentInChildren(ncSpriteTexture.m_NcSpriteFactoryPrefab, findType, parentPrefabList, bCheckDup: false);
				if (validComponentInChildren != null)
				{
					return validComponentInChildren;
				}
			}
		}
		NcParticleEmit[] componentsInChildren5 = tarObj.GetComponentsInChildren<NcParticleEmit>(includeInactive: true);
		NcParticleEmit[] array5 = componentsInChildren5;
		foreach (NcParticleEmit ncParticleEmit in array5)
		{
			if (ncParticleEmit.m_ParticlePrefab != null)
			{
				Component validComponentInChildren = GetValidComponentInChildren(ncParticleEmit.m_ParticlePrefab, findType, parentPrefabList, bCheckDup: false);
				if (validComponentInChildren != null)
				{
					return validComponentInChildren;
				}
			}
		}
		NcSpriteFactory[] componentsInChildren6 = tarObj.GetComponentsInChildren<NcSpriteFactory>(includeInactive: true);
		NcSpriteFactory[] array6 = componentsInChildren6;
		foreach (NcSpriteFactory ncSpriteFactory in array6)
		{
			if (ncSpriteFactory.m_SpriteList == null)
			{
				continue;
			}
			for (int num = 0; num < ncSpriteFactory.m_SpriteList.Count; num++)
			{
				if (ncSpriteFactory.m_SpriteList[num].m_EffectPrefab != null)
				{
					Component validComponentInChildren = GetValidComponentInChildren(ncSpriteFactory.m_SpriteList[num].m_EffectPrefab, findType, parentPrefabList, bCheckDup: true);
					if (validComponentInChildren != null)
					{
						return validComponentInChildren;
					}
				}
			}
		}
		return null;
	}

	protected static Component GetValidComponentInChildren(GameObject tarObj, Type findType, List<GameObject> parentPrefabList, bool bCheckDup)
	{
		if (parentPrefabList.Contains(tarObj))
		{
			if (bCheckDup)
			{
				string text = string.Empty;
				for (int i = 0; i < parentPrefabList.Count; i++)
				{
					text = text + parentPrefabList[i].name + "/";
				}
				return null;
			}
			return null;
		}
		parentPrefabList.Add(tarObj);
		Component componentInChildren = GetComponentInChildren(tarObj, findType, parentPrefabList);
		parentPrefabList.Remove(tarObj);
		return componentInChildren;
	}
}

using System.Collections.Generic;
using UnityEngine;

public class NcEffectBehaviour : MonoBehaviour
{
	public class _RuntimeIntance
	{
		public GameObject m_ParentGameObject;

		public GameObject m_ChildGameObject;

		public _RuntimeIntance(GameObject parentGameObject, GameObject childGameObject)
		{
			m_ParentGameObject = parentGameObject;
			m_ChildGameObject = childGameObject;
		}
	}

	private static bool m_bShuttingDown;

	private static GameObject m_RootInstance;

	public float m_fUserTag;

	protected MeshFilter m_MeshFilter;

	protected List<Material> m_RuntimeMaterials;

	protected bool m_bReplayState;

	protected NcEffectInitBackup m_NcEffectInitBackup;

	public NcEffectBehaviour()
	{
		m_MeshFilter = null;
	}

	public float GetEngineTime()
	{
		if (Time.time == 0f)
		{
			return 1E-06f;
		}
		return Time.time;
	}

	public static float GetEngineDeltaTime()
	{
		return Time.deltaTime;
	}

	public virtual int GetAnimationState()
	{
		return -1;
	}

	public static GameObject GetRootInstanceEffect()
	{
		if (!IsSafe())
		{
			return null;
		}
		if (m_RootInstance == null)
		{
			m_RootInstance = GameObject.Find("_InstanceObject");
			if (m_RootInstance == null)
			{
				m_RootInstance = new GameObject("_InstanceObject");
			}
		}
		return m_RootInstance;
	}

	public static Texture[] PreloadTexture(GameObject tarObj)
	{
		return NsEffectManager.PreloadResource(tarObj);
	}

	protected static void SetActive(GameObject target, bool bActive)
	{
		target.SetActive(bActive);
	}

	protected static void SetActiveRecursively(GameObject target, bool bActive)
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

	protected static bool IsActive(GameObject target)
	{
		return target.activeInHierarchy && target.activeSelf;
	}

	protected static void RemoveAllChildObject(GameObject parent, bool bImmediate)
	{
		int num = parent.transform.childCount - 1;
		while (0 <= num)
		{
			if (num < parent.transform.childCount)
			{
				Transform child = parent.transform.GetChild(num);
				if (bImmediate)
				{
					Object.DestroyImmediate(child.gameObject);
				}
				else
				{
					Object.Destroy(child.gameObject);
				}
			}
			num--;
		}
	}

	public static void HideNcDelayActive(GameObject tarObj)
	{
		SetActiveRecursively(tarObj, bActive: false);
	}

	protected void AddRuntimeMaterial(Material addMaterial)
	{
		if (m_RuntimeMaterials == null)
		{
			m_RuntimeMaterials = new List<Material>();
		}
		if (!m_RuntimeMaterials.Contains(addMaterial))
		{
			m_RuntimeMaterials.Add(addMaterial);
		}
	}

	public static string GetMaterialColorName(Material mat)
	{
		string[] array = new string[3] { "_Color", "_TintColor", "_EmisColor" };
		if (mat != null)
		{
			string[] array2 = array;
			foreach (string result in array2)
			{
				if (mat.HasProperty(result))
				{
					return result;
				}
			}
		}
		return null;
	}

	protected void DisableEmit()
	{
		NcParticleSystem[] componentsInChildren = base.gameObject.GetComponentsInChildren<NcParticleSystem>(includeInactive: true);
		NcParticleSystem[] array = componentsInChildren;
		foreach (NcParticleSystem ncParticleSystem in array)
		{
			if (ncParticleSystem != null)
			{
				ncParticleSystem.SetDisableEmit();
			}
		}
		NcAttachPrefab[] componentsInChildren2 = base.gameObject.GetComponentsInChildren<NcAttachPrefab>(includeInactive: true);
		NcAttachPrefab[] array2 = componentsInChildren2;
		foreach (NcAttachPrefab ncAttachPrefab in array2)
		{
			if (ncAttachPrefab != null)
			{
				ncAttachPrefab.enabled = false;
			}
		}
		ParticleSystem[] componentsInChildren3 = base.gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
		ParticleSystem[] array3 = componentsInChildren3;
		foreach (ParticleSystem particleSystem in array3)
		{
			if (particleSystem != null)
			{
				particleSystem.enableEmission = false;
			}
		}
	}

	public static bool IsSafe()
	{
		return !m_bShuttingDown && !Application.isLoadingLevel;
	}

	protected GameObject CreateEditorGameObject(GameObject srcGameObj)
	{
		if (srcGameObj.name.Contains("flare 24"))
		{
			return srcGameObj;
		}
		return srcGameObj;
	}

	public GameObject CreateGameObject(string name)
	{
		if (!IsSafe())
		{
			return null;
		}
		return CreateEditorGameObject(new GameObject(name));
	}

	public GameObject CreateGameObject(GameObject original)
	{
		if (!IsSafe())
		{
			return null;
		}
		return CreateEditorGameObject(Object.Instantiate(original));
	}

	public GameObject CreateGameObject(GameObject prefabObj, Vector3 position, Quaternion rotation)
	{
		if (!IsSafe())
		{
			return null;
		}
		return CreateEditorGameObject(Object.Instantiate(prefabObj, position, rotation));
	}

	public GameObject CreateGameObject(GameObject parentObj, GameObject prefabObj)
	{
		if (!IsSafe())
		{
			return null;
		}
		GameObject gameObject = CreateGameObject(prefabObj);
		if (parentObj != null)
		{
			ChangeParent(parentObj.transform, gameObject.transform, bKeepingLocalTransform: true, null);
		}
		return gameObject;
	}

	public GameObject CreateGameObject(GameObject parentObj, Transform parentTrans, GameObject prefabObj)
	{
		if (!IsSafe())
		{
			return null;
		}
		GameObject gameObject = CreateGameObject(prefabObj);
		if (parentObj != null)
		{
			ChangeParent(parentObj.transform, gameObject.transform, bKeepingLocalTransform: true, parentTrans);
		}
		return gameObject;
	}

	protected TT AddNcComponentToObject<TT>(GameObject toObj) where TT : NcEffectBehaviour
	{
		NcEffectBehaviour ncEffectBehaviour = toObj.AddComponent<TT>();
		if (m_bReplayState)
		{
			ncEffectBehaviour.OnSetReplayState();
		}
		return (TT)ncEffectBehaviour;
	}

	protected void ChangeParent(Transform newParent, Transform child, bool bKeepingLocalTransform, Transform addTransform)
	{
		NcTransformTool ncTransformTool = null;
		if (bKeepingLocalTransform)
		{
			ncTransformTool = new NcTransformTool(child.transform);
			if (addTransform != null)
			{
				ncTransformTool.AddTransform(addTransform);
			}
		}
		child.parent = newParent;
		if (bKeepingLocalTransform)
		{
			ncTransformTool.CopyToLocalTransform(child.transform);
		}
		if (bKeepingLocalTransform)
		{
			NcBillboard[] componentsInChildren = child.GetComponentsInChildren<NcBillboard>();
			NcBillboard[] array = componentsInChildren;
			foreach (NcBillboard ncBillboard in array)
			{
				ncBillboard.UpdateBillboard();
			}
		}
	}

	protected void UpdateMeshColors(Color color)
	{
		if (m_MeshFilter == null)
		{
			m_MeshFilter = (MeshFilter)base.gameObject.GetComponent(typeof(MeshFilter));
		}
		if (!(m_MeshFilter == null) && !(m_MeshFilter.sharedMesh == null) && !(m_MeshFilter.mesh == null))
		{
			Color[] array = new Color[m_MeshFilter.mesh.vertexCount];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = color;
			}
			m_MeshFilter.mesh.colors = array;
		}
	}

	protected virtual void OnDestroy()
	{
		if (m_RuntimeMaterials == null)
		{
			return;
		}
		foreach (Material runtimeMaterial in m_RuntimeMaterials)
		{
			Object.Destroy(runtimeMaterial);
		}
		m_RuntimeMaterials = null;
	}

	public void OnApplicationQuit()
	{
		m_bShuttingDown = true;
	}

	public virtual void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)
	{
	}

	public virtual void OnSetActiveRecursively(bool bActive)
	{
	}

	public virtual void OnUpdateToolData()
	{
	}

	public virtual void OnSetReplayState()
	{
		m_bReplayState = true;
	}

	public virtual void OnResetReplayStage(bool bClearOldParticle)
	{
	}
}

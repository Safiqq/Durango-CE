using UnityEngine;

public class NcAttachPrefab : NcEffectBehaviour
{
	public enum AttachType
	{
		Active,
		Destroy
	}

	public AttachType m_AttachType;

	public float m_fDelayTime;

	public float m_fRepeatTime;

	public int m_nRepeatCount;

	public GameObject m_AttachPrefab;

	public float m_fPrefabSpeed = 1f;

	public float m_fPrefabLifeTime;

	public bool m_bWorldSpace;

	public Vector3 m_AddStartPos = Vector3.zero;

	public Vector3 m_AccumStartRot = Vector3.zero;

	public Vector3 m_RandomRange = Vector3.zero;

	public int m_nSpriteFactoryIndex = -1;

	[HideInInspector]
	public bool m_bDetachParent;

	protected float m_fStartTime;

	protected int m_nCreateCount;

	protected bool m_bStartAttach;

	protected GameObject[] m_CreateGameObjects;

	protected bool m_bEnabled;

	public override int GetAnimationState()
	{
		if (m_bEnabled && base.enabled && NcEffectBehaviour.IsActive(base.gameObject) && m_AttachPrefab != null)
		{
			if (m_AttachType == AttachType.Active && ((m_nRepeatCount == 0 && m_nCreateCount < 1) || (0f < m_fRepeatTime && m_nRepeatCount == 0) || (0 < m_nRepeatCount && m_nCreateCount < m_nRepeatCount)))
			{
				return 1;
			}
			if (m_AttachType == AttachType.Destroy)
			{
				return 1;
			}
		}
		return 0;
	}

	public void UpdateImmediately()
	{
		Update();
	}

	public void CreateAttachInstance()
	{
		CreateAttachGameObject();
	}

	public GameObject GetInstanceObject()
	{
		if (m_CreateGameObjects == null)
		{
			UpdateImmediately();
		}
		return (m_CreateGameObjects != null && m_CreateGameObjects.Length >= 1) ? m_CreateGameObjects[0] : null;
	}

	public virtual GameObject[] GetInstanceObjects()
	{
		if (m_CreateGameObjects == null)
		{
			UpdateImmediately();
		}
		return m_CreateGameObjects;
	}

	public void SetEnable(bool bEnable)
	{
		m_bEnabled = bEnable;
	}

	protected virtual void Awake()
	{
		m_bEnabled = base.enabled && NcEffectBehaviour.IsActive(base.gameObject) && GetComponent<NcDontActive>() == null;
	}

	protected virtual void Start()
	{
	}

	protected virtual void Update()
	{
		if (m_AttachPrefab == null || m_AttachType != AttachType.Active)
		{
			return;
		}
		if (!m_bStartAttach)
		{
			m_fStartTime = GetEngineTime();
			m_bStartAttach = true;
		}
		if (m_fStartTime + m_fDelayTime <= GetEngineTime())
		{
			CreateAttachPrefab();
			if ((0f < m_fRepeatTime && m_nRepeatCount == 0) || m_nCreateCount < m_nRepeatCount)
			{
				m_fStartTime = GetEngineTime();
				m_fDelayTime = m_fRepeatTime;
			}
			else
			{
				base.enabled = false;
			}
		}
	}

	protected override void OnDestroy()
	{
		if (m_bEnabled && NcEffectBehaviour.IsSafe() && m_AttachType == AttachType.Destroy && m_AttachPrefab != null)
		{
			CreateAttachPrefab();
		}
		base.OnDestroy();
	}

	private void CreateAttachPrefab()
	{
		CreateAttachGameObject();
		if ((m_fRepeatTime == 0f || m_AttachType == AttachType.Destroy) && 0 < m_nRepeatCount && m_nCreateCount < m_nRepeatCount)
		{
			CreateAttachPrefab();
		}
	}

	private void CreateAttachGameObject()
	{
		GameObject gameObject = CreateGameObject(GetTargetGameObject(), (!(GetTargetGameObject() == base.gameObject)) ? base.transform : null, m_AttachPrefab);
		if (m_bReplayState)
		{
			NsEffectManager.SetReplayEffect(gameObject);
		}
		if (gameObject == null)
		{
			return;
		}
		if (m_AttachType == AttachType.Active)
		{
			if (m_CreateGameObjects == null)
			{
				m_CreateGameObjects = new GameObject[Mathf.Max(1, m_nRepeatCount)];
			}
			for (int i = 0; i < m_CreateGameObjects.Length; i++)
			{
				if (m_CreateGameObjects[i] == null)
				{
					m_CreateGameObjects[i] = gameObject;
					break;
				}
			}
		}
		m_nCreateCount++;
		Vector3 position = gameObject.transform.position;
		gameObject.transform.position = m_AddStartPos + new Vector3(Random.Range(0f - m_RandomRange.x, m_RandomRange.x) + position.x, Random.Range(0f - m_RandomRange.y, m_RandomRange.y) + position.y, Random.Range(0f - m_RandomRange.z, m_RandomRange.z) + position.z);
		gameObject.transform.localRotation *= Quaternion.Euler(m_AccumStartRot.x * (float)m_nCreateCount, m_AccumStartRot.y * (float)m_nCreateCount, m_AccumStartRot.z * (float)m_nCreateCount);
		gameObject.name = gameObject.name + " " + m_nCreateCount;
		NcEffectBehaviour.SetActiveRecursively(gameObject, bActive: true);
		NsEffectManager.AdjustSpeedRuntime(gameObject, m_fPrefabSpeed);
		if (0f < m_fPrefabLifeTime)
		{
			NcAutoDestruct ncAutoDestruct = gameObject.GetComponent<NcAutoDestruct>();
			if (ncAutoDestruct == null)
			{
				ncAutoDestruct = AddNcComponentToObject<NcAutoDestruct>(gameObject);
			}
			ncAutoDestruct.m_fLifeTime = m_fPrefabLifeTime;
		}
		if (m_bDetachParent)
		{
			NcDetachParent component = gameObject.GetComponent<NcDetachParent>();
			if (component == null)
			{
				component = AddNcComponentToObject<NcDetachParent>(gameObject);
			}
		}
		if (0 <= m_nSpriteFactoryIndex)
		{
			NcSpriteFactory component2 = gameObject.GetComponent<NcSpriteFactory>();
			if ((bool)component2)
			{
				component2.SetSprite(m_nSpriteFactoryIndex, bRunImmediate: false);
			}
		}
		OnCreateAttachGameObject();
	}

	protected virtual void OnCreateAttachGameObject()
	{
	}

	private GameObject GetTargetGameObject()
	{
		if (m_bWorldSpace || m_AttachType == AttachType.Destroy)
		{
			return NcEffectBehaviour.GetRootInstanceEffect();
		}
		return base.gameObject;
	}

	public override void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)
	{
		m_fDelayTime /= fSpeedRate;
		m_fRepeatTime /= fSpeedRate;
		m_fPrefabLifeTime /= fSpeedRate;
		m_fPrefabSpeed *= fSpeedRate;
	}

	public override void OnSetActiveRecursively(bool bActive)
	{
		if (m_CreateGameObjects == null)
		{
			return;
		}
		for (int i = 0; i < m_CreateGameObjects.Length; i++)
		{
			if (m_CreateGameObjects[i] != null)
			{
				NsEffectManager.SetActiveRecursively(m_CreateGameObjects[i], bActive);
			}
		}
	}

	public static void Ng_ChangeLayerWithChild(GameObject rootObj, int nLayer)
	{
		if (!(rootObj == null))
		{
			rootObj.layer = nLayer;
			for (int i = 0; i < rootObj.transform.childCount; i++)
			{
				Ng_ChangeLayerWithChild(rootObj.transform.GetChild(i).gameObject, nLayer);
			}
		}
	}
}

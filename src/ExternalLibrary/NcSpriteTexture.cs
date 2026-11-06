using UnityEngine;

public class NcSpriteTexture : NcEffectBehaviour
{
	public GameObject m_NcSpriteFactoryPrefab;

	protected NcSpriteFactory m_NcSpriteFactoryCom;

	public NcSpriteFactory.NcFrameInfo[] m_NcSpriteFrameInfos;

	public float m_fUvScale = 1f;

	public int m_nSpriteFactoryIndex;

	public int m_nFrameIndex;

	public NcSpriteFactory.MESH_TYPE m_MeshType;

	public NcSpriteFactory.ALIGN_TYPE m_AlignType = NcSpriteFactory.ALIGN_TYPE.CENTER;

	public float m_fShowRate = 1f;

	protected GameObject m_EffectObject;

	private void Awake()
	{
		if (m_MeshFilter == null)
		{
			m_MeshFilter = base.gameObject.GetComponent<MeshFilter>();
			if (m_MeshFilter == null)
			{
				m_MeshFilter = base.gameObject.AddComponent<MeshFilter>();
			}
		}
		if (m_NcSpriteFactoryPrefab == null && base.gameObject.GetComponent<NcSpriteFactory>() != null)
		{
			m_NcSpriteFactoryPrefab = base.gameObject;
		}
		UpdateFactoryInfo(m_nSpriteFactoryIndex);
	}

	private void Start()
	{
		UpdateSpriteTexture(bShowEffect: true);
	}

	public void SetSpriteFactoryIndex(string spriteName, int nFrameIndex, bool bRunImmediate)
	{
		if (m_NcSpriteFactoryCom == null)
		{
			if (!m_NcSpriteFactoryPrefab || !(m_NcSpriteFactoryPrefab.GetComponent<NcSpriteFactory>() != null))
			{
				return;
			}
			m_NcSpriteFactoryCom = m_NcSpriteFactoryPrefab.GetComponent<NcSpriteFactory>();
		}
		m_nSpriteFactoryIndex = m_NcSpriteFactoryCom.GetSpriteNodeIndex(spriteName);
		SetSpriteFactoryIndex(m_nSpriteFactoryIndex, nFrameIndex, bRunImmediate);
	}

	public void SetSpriteFactoryIndex(int nSpriteFactoryIndex, int nFrameIndex, bool bRunImmediate)
	{
		if (UpdateFactoryInfo(nSpriteFactoryIndex))
		{
			SetFrameIndex(nFrameIndex);
			if (bRunImmediate)
			{
				UpdateSpriteTexture(bRunImmediate);
			}
		}
	}

	public void SetFrameIndex(int nFrameIndex)
	{
		m_nFrameIndex = ((0 > nFrameIndex) ? m_nFrameIndex : nFrameIndex);
		if (m_NcSpriteFrameInfos != null)
		{
			m_nFrameIndex = ((m_NcSpriteFrameInfos.Length != 0) ? ((m_NcSpriteFrameInfos.Length > m_nFrameIndex) ? m_nFrameIndex : (m_NcSpriteFrameInfos.Length - 1)) : 0);
		}
	}

	public void SetShowRate(float fShowRate)
	{
		m_fShowRate = fShowRate;
		UpdateSpriteTexture(bShowEffect: true);
	}

	private bool UpdateFactoryInfo(int nSpriteFactoryIndex)
	{
		m_nSpriteFactoryIndex = nSpriteFactoryIndex;
		if (m_NcSpriteFactoryCom == null)
		{
			if (!m_NcSpriteFactoryPrefab || !(m_NcSpriteFactoryPrefab.GetComponent<NcSpriteFactory>() != null))
			{
				return false;
			}
			m_NcSpriteFactoryCom = m_NcSpriteFactoryPrefab.GetComponent<NcSpriteFactory>();
		}
		if (!m_NcSpriteFactoryCom.IsValidFactory())
		{
			return false;
		}
		m_NcSpriteFrameInfos = m_NcSpriteFactoryCom.GetSpriteNode(m_nSpriteFactoryIndex).m_FrameInfos;
		m_fUvScale = m_NcSpriteFactoryCom.m_fUvScale;
		return true;
	}

	private void UpdateSpriteTexture(bool bShowEffect)
	{
		if (!UpdateSpriteMaterial() || !m_NcSpriteFactoryCom.IsValidFactory())
		{
			return;
		}
		if (m_NcSpriteFrameInfos.Length == 0)
		{
			SetSpriteFactoryIndex(m_nSpriteFactoryIndex, m_nFrameIndex, bRunImmediate: false);
		}
		if (m_MeshFilter == null)
		{
			if (base.gameObject.GetComponent<MeshFilter>() != null)
			{
				m_MeshFilter = base.gameObject.GetComponent<MeshFilter>();
			}
			else
			{
				m_MeshFilter = base.gameObject.AddComponent<MeshFilter>();
			}
		}
		NcSpriteFactory.CreatePlane(m_MeshFilter, m_fUvScale, m_NcSpriteFrameInfos[m_nFrameIndex], bTrimCenterAlign: false, m_AlignType, m_MeshType, m_fShowRate);
		NcSpriteFactory.UpdateMeshUVs(m_MeshFilter, m_NcSpriteFrameInfos[m_nFrameIndex].m_TextureUvOffset, m_AlignType, m_fShowRate);
		if (bShowEffect)
		{
			m_EffectObject = m_NcSpriteFactoryCom.CreateSpriteEffect(m_nSpriteFactoryIndex, base.transform);
		}
	}

	public bool UpdateSpriteMaterial()
	{
		if (m_NcSpriteFactoryPrefab == null)
		{
			return false;
		}
		if (m_NcSpriteFactoryPrefab.GetComponent<Renderer>() == null || m_NcSpriteFactoryPrefab.GetComponent<Renderer>().sharedMaterial == null || m_NcSpriteFactoryPrefab.GetComponent<Renderer>().sharedMaterial.mainTexture == null)
		{
			return false;
		}
		if (GetComponent<Renderer>() == null)
		{
			return false;
		}
		if (m_NcSpriteFactoryCom == null)
		{
			return false;
		}
		if (m_nSpriteFactoryIndex < 0 || m_NcSpriteFactoryCom.GetSpriteNodeCount() <= m_nSpriteFactoryIndex)
		{
			return false;
		}
		if (m_NcSpriteFactoryCom.m_SpriteType != NcSpriteFactory.SPRITE_TYPE.NcSpriteTexture && m_NcSpriteFactoryCom.m_SpriteType != NcSpriteFactory.SPRITE_TYPE.Auto)
		{
			return false;
		}
		GetComponent<Renderer>().sharedMaterial = m_NcSpriteFactoryPrefab.GetComponent<Renderer>().sharedMaterial;
		return true;
	}

	public override void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)
	{
	}

	public override void OnUpdateToolData()
	{
	}
}

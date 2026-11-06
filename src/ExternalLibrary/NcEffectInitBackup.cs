using UnityEngine;

public class NcEffectInitBackup
{
	protected enum SAVE_TYPE
	{
		NONE,
		ONE,
		RECURSIVELY
	}

	protected SAVE_TYPE m_SavedMaterialColor;

	protected Renderer m_MaterialColorRenderer;

	protected string m_MaterialColorColorName;

	protected Vector4 m_MaterialColorSaveValue;

	protected Renderer[] m_MaterialColorRenderers;

	protected string[] m_MaterialColorColorNames;

	protected Vector4[] m_MaterialColorSaveValues;

	protected SAVE_TYPE m_SavedMeshColor;

	protected MeshFilter m_MeshColorMeshFilter;

	protected Vector4 m_MeshColorSaveValue;

	protected MeshFilter[] m_MeshColorMeshFilters;

	protected Vector4[] m_MeshColorSaveValues;

	protected NcUvAnimation m_NcUvAnimation;

	protected Vector2 m_UvAniSaveValue;

	protected Transform m_Transform;

	protected NcTransformTool m_NcTansform;

	public void BackupTransform(Transform targetTrans)
	{
		m_Transform = targetTrans;
		m_NcTansform = new NcTransformTool(m_Transform);
	}

	public void RestoreTransform()
	{
		m_NcTansform.CopyToLocalTransform(m_Transform);
	}

	public void BackupMaterialColor(GameObject targetObj, bool bRecursively)
	{
		if (targetObj == null)
		{
			return;
		}
		if (bRecursively)
		{
			m_SavedMaterialColor = SAVE_TYPE.RECURSIVELY;
		}
		else
		{
			m_SavedMaterialColor = SAVE_TYPE.ONE;
		}
		Transform transform = targetObj.transform;
		if (m_SavedMaterialColor == SAVE_TYPE.RECURSIVELY)
		{
			m_MaterialColorRenderers = transform.GetComponentsInChildren<Renderer>(includeInactive: true);
			m_MaterialColorColorNames = new string[m_MaterialColorRenderers.Length];
			m_MaterialColorSaveValues = new Vector4[m_MaterialColorRenderers.Length];
			for (int i = 0; i < m_MaterialColorRenderers.Length; i++)
			{
				Renderer renderer = m_MaterialColorRenderers[i];
				m_MaterialColorColorNames[i] = GetMaterialColorName(renderer.sharedMaterial);
				if (m_MaterialColorColorNames[i] != null)
				{
					ref Vector4 reference = ref m_MaterialColorSaveValues[i];
					reference = renderer.material.GetColor(m_MaterialColorColorNames[i]);
				}
			}
			return;
		}
		m_MaterialColorRenderer = transform.GetComponent<Renderer>();
		if (m_MaterialColorRenderer != null)
		{
			m_MaterialColorColorName = GetMaterialColorName(m_MaterialColorRenderer.sharedMaterial);
			if (m_MaterialColorColorName != null)
			{
				m_MaterialColorSaveValue = m_MaterialColorRenderer.material.GetColor(m_MaterialColorColorName);
			}
		}
	}

	public void RestoreMaterialColor()
	{
		if (m_SavedMaterialColor == SAVE_TYPE.NONE)
		{
			return;
		}
		if (m_SavedMaterialColor == SAVE_TYPE.RECURSIVELY)
		{
			for (int i = 0; i < m_MaterialColorRenderers.Length; i++)
			{
				if (m_MaterialColorRenderers[i] != null && m_MaterialColorColorNames[i] != null)
				{
					m_MaterialColorRenderers[i].material.SetColor(m_MaterialColorColorNames[i], m_MaterialColorSaveValues[i]);
				}
			}
		}
		else if (m_MaterialColorRenderers != null)
		{
			m_MaterialColorColorName = GetMaterialColorName(m_MaterialColorRenderer.sharedMaterial);
			if (m_MaterialColorColorName != null)
			{
				m_MaterialColorRenderer.material.SetColor(m_MaterialColorColorName, m_MaterialColorSaveValue);
			}
		}
	}

	public void BackupUvAnimation(NcUvAnimation uvAniCom)
	{
		if (!(uvAniCom == null))
		{
			m_NcUvAnimation = uvAniCom;
			m_UvAniSaveValue = new Vector2(m_NcUvAnimation.m_fScrollSpeedX, m_NcUvAnimation.m_fScrollSpeedY);
		}
	}

	public void RestoreUvAnimation()
	{
		if (!(m_NcUvAnimation == null))
		{
			m_NcUvAnimation.m_fScrollSpeedX = m_UvAniSaveValue.x;
			m_NcUvAnimation.m_fScrollSpeedY = m_UvAniSaveValue.y;
		}
	}

	public void BackupMeshColor(GameObject targetObj, bool bRecursively)
	{
		if (targetObj == null)
		{
			return;
		}
		if (bRecursively)
		{
			m_SavedMeshColor = SAVE_TYPE.RECURSIVELY;
		}
		else
		{
			m_SavedMeshColor = SAVE_TYPE.ONE;
		}
		if (m_SavedMeshColor == SAVE_TYPE.RECURSIVELY)
		{
			m_MeshColorMeshFilters = targetObj.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			m_MeshColorSaveValues = new Vector4[m_MeshColorMeshFilters.Length];
			if (m_MeshColorMeshFilters != null && m_MeshColorMeshFilters.Length >= 0)
			{
				for (int i = 0; i < m_MeshColorMeshFilters.Length; i++)
				{
					ref Vector4 reference = ref m_MeshColorSaveValues[i];
					reference = GetMeshColor(m_MeshColorMeshFilters[i]);
				}
			}
		}
		else
		{
			m_MeshColorMeshFilter = targetObj.GetComponent<MeshFilter>();
			m_MeshColorSaveValue = GetMeshColor(m_MeshColorMeshFilter);
		}
	}

	public void RestoreMeshColor()
	{
		if (m_SavedMeshColor == SAVE_TYPE.NONE)
		{
			return;
		}
		if (m_SavedMeshColor == SAVE_TYPE.RECURSIVELY)
		{
			if (m_MeshColorMeshFilters != null && m_MeshColorMeshFilters.Length >= 0)
			{
				for (int i = 0; i < m_MeshColorMeshFilters.Length; i++)
				{
					SetMeshColor(m_MeshColorMeshFilters[i], m_MeshColorSaveValues[i]);
				}
			}
		}
		else
		{
			SetMeshColor(m_MeshColorMeshFilter, m_MeshColorSaveValue);
		}
	}

	protected Color GetMeshColor(MeshFilter mFilter)
	{
		if (mFilter == null || mFilter.mesh == null)
		{
			return Color.white;
		}
		Color[] colors = mFilter.mesh.colors;
		if (colors.Length == 0)
		{
			colors = new Color[mFilter.mesh.vertices.Length];
			for (int i = 0; i < colors.Length; i++)
			{
				ref Color reference = ref colors[i];
				reference = Color.white;
			}
			mFilter.mesh.colors = colors;
			return Color.white;
		}
		return colors[0];
	}

	protected void SetMeshColor(MeshFilter mFilter, Color tarColor)
	{
		if (mFilter == null || mFilter.mesh == null)
		{
			return;
		}
		Color[] array = mFilter.mesh.colors;
		if (array.Length == 0)
		{
			array = new Color[mFilter.mesh.vertices.Length];
			for (int i = 0; i < array.Length; i++)
			{
				ref Color reference = ref array[i];
				reference = Color.white;
			}
		}
		for (int j = 0; j < array.Length; j++)
		{
			array[j] = tarColor;
		}
		mFilter.mesh.colors = array;
	}

	protected static string GetMaterialColorName(Material mat)
	{
		string[] array = new string[3] { "_Color", "_TintColor", "_EmisColor" };
		if (mat != null)
		{
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (mat.HasProperty(text))
				{
					return text;
				}
			}
		}
		return null;
	}
}

using UnityEngine;

public class NcChangeAlpha : NcEffectBehaviour
{
	public enum TARGET_TYPE
	{
		MeshColor,
		MaterialColor
	}

	public enum CHANGE_MODE
	{
		FromTo
	}

	public TARGET_TYPE m_TargetType;

	public float m_fDelayTime = 2f;

	public float m_fChangeTime = 1f;

	public bool m_bRecursively = true;

	public CHANGE_MODE m_ChangeMode;

	public float m_fFromAlphaValue = 1f;

	public float m_fToMeshValue;

	public bool m_bAutoDeactive = true;

	protected float m_fStartTime;

	protected float m_fStartChangeTime;

	public static NcChangeAlpha SetChangeTime(GameObject baseGameObject, float fLifeTime, float fChangeTime, float fFromMeshAlphaValue, float fToMeshAlphaValue)
	{
		NcChangeAlpha ncChangeAlpha = baseGameObject.AddComponent<NcChangeAlpha>();
		ncChangeAlpha.SetChangeTime(fLifeTime, fChangeTime, fFromMeshAlphaValue, fToMeshAlphaValue);
		return ncChangeAlpha;
	}

	public void SetChangeTime(float fDelayTime, float fChangeTime, float fFromAlphaValue, float fToAlphaValue)
	{
		m_fDelayTime = fDelayTime;
		m_fChangeTime = fChangeTime;
		m_fFromAlphaValue = fFromAlphaValue;
		m_fToMeshValue = fToAlphaValue;
		if (NcEffectBehaviour.IsActive(base.gameObject))
		{
			Start();
			Update();
		}
	}

	public void Restart()
	{
		m_fStartTime = GetEngineTime();
		m_fStartChangeTime = 0f;
		ChangeToAlpha(0f);
	}

	private void Awake()
	{
		m_fStartTime = 0f;
		m_fStartChangeTime = 0f;
	}

	private void Start()
	{
		Restart();
	}

	private void Update()
	{
		if (0f < m_fStartChangeTime)
		{
			if (0f < m_fChangeTime)
			{
				float num = (GetEngineTime() - m_fStartChangeTime) / m_fChangeTime;
				if (1f < num)
				{
					num = 1f;
					if (m_bAutoDeactive && m_fToMeshValue <= 0f)
					{
						NcEffectBehaviour.SetActiveRecursively(base.gameObject, bActive: false);
					}
				}
				ChangeToAlpha(num);
			}
			else
			{
				ChangeToAlpha(1f);
			}
		}
		else if (0f < m_fStartTime && m_fStartTime + m_fDelayTime <= GetEngineTime())
		{
			StartChange();
		}
	}

	private void StartChange()
	{
		m_fStartChangeTime = GetEngineTime();
	}

	private void ChangeToAlpha(float fElapsedRate)
	{
		float num = Mathf.Lerp(m_fFromAlphaValue, m_fToMeshValue, fElapsedRate);
		if (m_TargetType == TARGET_TYPE.MeshColor)
		{
			MeshFilter[] array = ((!m_bRecursively) ? base.transform.GetComponents<MeshFilter>() : base.transform.GetComponentsInChildren<MeshFilter>(includeInactive: true));
			for (int i = 0; i < array.Length; i++)
			{
				Color[] array2 = array[i].mesh.colors;
				if (array2.Length == 0)
				{
					if (array[i].mesh.vertices.Length == 0)
					{
						NcSpriteFactory.CreateEmptyMesh(array[i]);
					}
					array2 = new Color[array[i].mesh.vertices.Length];
					for (int j = 0; j < array2.Length; j++)
					{
						ref Color reference = ref array2[j];
						reference = Color.white;
					}
				}
				for (int k = 0; k < array2.Length; k++)
				{
					Color color = array2[k];
					color.a = num;
					array2[k] = color;
				}
				array[i].mesh.colors = array2;
			}
		}
		else
		{
			Renderer[] array3 = ((!m_bRecursively) ? base.transform.GetComponents<Renderer>() : base.transform.GetComponentsInChildren<Renderer>(includeInactive: true));
			foreach (Renderer renderer in array3)
			{
				string materialColorName = NcEffectBehaviour.GetMaterialColorName(renderer.sharedMaterial);
				if (materialColorName != null)
				{
					Color color2 = renderer.material.GetColor(materialColorName);
					color2.a = num;
					renderer.material.SetColor(materialColorName, color2);
				}
			}
		}
		if (fElapsedRate == 1f && num == 0f)
		{
			NcEffectBehaviour.SetActiveRecursively(base.gameObject, bActive: false);
		}
	}

	public override void OnUpdateEffectSpeed(float fSpeedRate, bool bRuntime)
	{
		m_fDelayTime /= fSpeedRate;
		m_fChangeTime /= fSpeedRate;
	}

	public override void OnSetReplayState()
	{
		base.OnSetReplayState();
		m_NcEffectInitBackup = new NcEffectInitBackup();
		if (m_TargetType == TARGET_TYPE.MeshColor)
		{
			m_NcEffectInitBackup.BackupMeshColor(base.gameObject, m_bRecursively);
		}
		else
		{
			m_NcEffectInitBackup.BackupMaterialColor(base.gameObject, m_bRecursively);
		}
	}

	public override void OnResetReplayStage(bool bClearOldParticle)
	{
		base.OnResetReplayStage(bClearOldParticle);
		m_fStartTime = GetEngineTime();
		m_fStartChangeTime = 0f;
		if (m_NcEffectInitBackup != null)
		{
			if (m_TargetType == TARGET_TYPE.MeshColor)
			{
				m_NcEffectInitBackup.RestoreMeshColor();
			}
			else
			{
				m_NcEffectInitBackup.RestoreMaterialColor();
			}
		}
	}
}

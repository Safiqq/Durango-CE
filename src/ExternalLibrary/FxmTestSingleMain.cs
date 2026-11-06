using UnityEngine;

public class FxmTestSingleMain : MonoBehaviour
{
	public GameObject[] m_EffectPrefabs = new GameObject[1];

	public int m_nIndex;

	public float m_fCreateScale = 1f;

	public int m_nCreateCount = 1;

	public float m_fRandomRange = 1f;

	public FxmTestSingleMouse m_FXMakerMouse;

	private void Awake()
	{
	}

	private void OnEnable()
	{
	}

	private void Start()
	{
		Resources.UnloadUnusedAssets();
		Invoke("CreateEffect", 1f);
	}

	private void CreateEffect()
	{
	}

	private void Update()
	{
	}

	private void OnGUI()
	{
		float num = Screen.height / 10;
		float num2 = GUI.VerticalSlider(new Rect(10f, num + 10f + 30f, 25f, (float)Screen.height - (num + 10f + 50f)), GetFXMakerMouse().m_fDistance, GetFXMakerMouse().m_fDistanceMin, GetFXMakerMouse().m_fDistanceMax);
		if (num2 != GetFXMakerMouse().m_fDistance)
		{
			GetFXMakerMouse().SetDistance(num2);
		}
		if (GUI.Button(GetButtonRect(0), "Next"))
		{
			if (m_nIndex < m_EffectPrefabs.Length - 1)
			{
				m_nIndex++;
			}
			else
			{
				m_nIndex = 0;
			}
			CreateEffect();
		}
		if (GUI.Button(GetButtonRect(1), "Recreate"))
		{
			CreateEffect();
		}
	}

	public FxmTestSingleMouse GetFXMakerMouse()
	{
		if (m_FXMakerMouse == null)
		{
			m_FXMakerMouse = GetComponentInChildren<FxmTestSingleMouse>();
		}
		return m_FXMakerMouse;
	}

	public GameObject GetInstanceRoot()
	{
		return NcEffectBehaviour.GetRootInstanceEffect();
	}

	public static Rect GetButtonRect()
	{
		int num = 2;
		return new Rect(Screen.width - Screen.width / 10 * num, Screen.height - Screen.height / 10, Screen.width / 10 * num, Screen.height / 10);
	}

	public static Rect GetButtonRect(int nIndex)
	{
		return new Rect(Screen.width - Screen.width / 10 * (nIndex + 1), Screen.height - Screen.height / 10, Screen.width / 10, Screen.height / 10);
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

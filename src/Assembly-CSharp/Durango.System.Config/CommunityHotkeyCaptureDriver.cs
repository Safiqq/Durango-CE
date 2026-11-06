using UnityEngine;

namespace Durango.System.Config;

public class CommunityHotkeyCaptureDriver : MonoBehaviour
{
	private static CommunityHotkeyCaptureDriver s_instance;

	public static void Ensure()
	{
		if (!(s_instance != null))
		{
			GameObject obj = new GameObject("CommunityHotkeyCapture");
			Object.DontDestroyOnLoad(obj);
			s_instance = obj.AddComponent<CommunityHotkeyCaptureDriver>();
		}
	}

	private void Update()
	{
		CommunityHotkeys.UpdateCapture();
	}
}

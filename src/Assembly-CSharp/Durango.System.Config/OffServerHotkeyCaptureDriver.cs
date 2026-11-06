using UnityEngine;

namespace Durango.System.Config;

public class OffServerHotkeyCaptureDriver : MonoBehaviour
{
	private static OffServerHotkeyCaptureDriver s_instance;

	public static void Ensure()
	{
		if (!(s_instance != null))
		{
			GameObject obj = new GameObject("OffServerHotkeyCapture");
			Object.DontDestroyOnLoad(obj);
			s_instance = obj.AddComponent<OffServerHotkeyCaptureDriver>();
		}
	}

	private void Update()
	{
		OffServerHotkeys.UpdateCapture();
	}
}

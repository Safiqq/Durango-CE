using UnityEngine;
using UnityEngine.SceneManagement;

public class CommunityShaderRemapDriver : MonoBehaviour
{
	private static readonly int WorldLightColorId = Shader.PropertyToID("_WorldLightColor");

	private static readonly int SkyAmbientId = Shader.PropertyToID("_SkyAmbient");

	private float _nextLightCheck;

	private int _worldLightReports;

	private int _skyAmbientReports;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (CommunityShaderRemap.Enabled)
		{
			CommunityShaderRemap.CacheEngineShaders();
			SceneManager.sceneLoaded += delegate
			{
				CommunityShaderRemap.MarkBusy();
			};
			CommunityShaderRemap.MarkBusy();
			GameObject obj = new GameObject("CommunityShaderRemapDriver");
			Object.DontDestroyOnLoad(obj);
			obj.AddComponent<CommunityShaderRemapDriver>();
		}
	}

	private void Update()
	{
		CommunityShaderRemap.SweepAll();
		if (!(Time.realtimeSinceStartup >= _nextLightCheck))
		{
			return;
		}
		_nextLightCheck = Time.realtimeSinceStartup + 2f;
		Color globalColor = Shader.GetGlobalColor(WorldLightColorId);
		if (globalColor.r + globalColor.g + globalColor.b <= 0.001f)
		{
			Shader.SetGlobalColor(WorldLightColorId, Color.white);
			if (_worldLightReports++ < 3)
			{
				CommunityShaderRemap.Report("[ShaderRemap] _WorldLightColor was " + globalColor.ToString() + " -> forced white (CustomColorCorrectionEffect not running?)");
			}
		}
		Color globalColor2 = Shader.GetGlobalColor(SkyAmbientId);
		if (globalColor2.r + globalColor2.g + globalColor2.b <= 0.001f)
		{
			Shader.SetGlobalColor(SkyAmbientId, new Color(0.6f, 0.6f, 0.6f, 1f));
			if (_skyAmbientReports++ < 3)
			{
				CommunityShaderRemap.Report("[ShaderRemap] _SkyAmbient was " + globalColor2.ToString() + " -> forced 0.6 grey");
			}
		}
	}
}

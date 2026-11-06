using System;
using System.Collections.Generic;
using System.Text;
using Durango.Render.Screen;
using Durango.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OffServerRenderDiag : MonoBehaviour
{
	private float _next = 25f;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (Application.isEditor || (Debug.isDebugBuild && Application.platform == RuntimePlatform.Android))
		{
			GameObject obj = new GameObject("OffServerRenderDiag");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<OffServerRenderDiag>();
		}
	}

	private static void DumpTopRightWidgets()
	{
		try
		{
			int num = 0;
			UIWidget[] array = Resources.FindObjectsOfTypeAll<UIWidget>();
			foreach (UIWidget uIWidget in array)
			{
				if (uIWidget == null || !uIWidget.gameObject.activeInHierarchy || !uIWidget.enabled || uIWidget.panel == null)
				{
					continue;
				}
				Camera camera = null;
				try
				{
					camera = NGUITools.FindCameraForLayer(uIWidget.gameObject.layer);
				}
				catch
				{
				}
				if (camera == null)
				{
					continue;
				}
				Vector3[] worldCorners = uIWidget.worldCorners;
				Vector3 vector = camera.WorldToScreenPoint(worldCorners[0]);
				Vector3 vector2 = camera.WorldToScreenPoint(worldCorners[2]);
				float num2 = Mathf.Min(vector.x, vector2.x);
				float num3 = Mathf.Max(vector.x, vector2.x);
				float num4 = Mathf.Min(vector.y, vector2.y);
				float num5 = Mathf.Max(vector.y, vector2.y);
				if (!(num3 < (float)Screen.width * 0.66f) && !(num4 < (float)Screen.height * 0.78f) && !(num3 - num2 < 40f) && !(num5 - num4 < 20f))
				{
					Texture mainTexture = uIWidget.mainTexture;
					Shader shader = uIWidget.shader;
					OffServerShaderRemap.Report("[RenderDiag] widget " + uIWidget.GetType().Name + " " + uIWidget.name + " rect=(" + (int)num2 + "," + (int)num4 + ")-(" + (int)num3 + "," + (int)num5 + ") depth=" + uIWidget.depth + " tex=" + ((mainTexture == null) ? "null" : (mainTexture.name + " " + mainTexture.width + "x" + mainTexture.height + " " + mainTexture.GetType().Name)) + " shader=" + ((shader == null) ? "null" : shader.name) + " mat=" + ((uIWidget.material == null) ? "null" : uIWidget.material.name) + " color=" + uIWidget.color.ToString() + " path=" + Path(uIWidget.transform));
					if (++num >= 12)
					{
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			OffServerShaderRemap.Report("[RenderDiag] widget dump err=" + ex.Message);
		}
	}

	private static string Path(Transform t)
	{
		string text = t.name;
		int num = 0;
		while (t.parent != null && num++ < 6)
		{
			t = t.parent;
			text = t.name + "/" + text;
		}
		return text;
	}

	private void Update()
	{
		if (Time.realtimeSinceStartup < _next)
		{
			return;
		}
		_next = Time.realtimeSinceStartup + 20f;
		try
		{
			Dump();
		}
		catch (Exception ex)
		{
			OffServerShaderRemap.Report("[RenderDiag] " + ex.Message);
		}
	}

	private void Dump()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		Dictionary<string, bool> dictionary2 = new Dictionary<string, bool>();
		Dictionary<string, string> dictionary3 = new Dictionary<string, string>();
		Renderer[] array = UnityEngine.Object.FindObjectsByType<Renderer>();
		int num = 0;
		Renderer[] array2 = array;
		foreach (Renderer renderer in array2)
		{
			if (!renderer.isVisible)
			{
				continue;
			}
			num++;
			Material[] sharedMaterials = renderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if (!(material == null))
				{
					string key = ((material.shader == null) ? "<null shader>" : material.shader.name);
					dictionary.TryGetValue(key, out var value);
					dictionary[key] = value + 1;
					if (!dictionary2.ContainsKey(key))
					{
						dictionary2[key] = material.shader != null && material.shader.isSupported;
						dictionary3[key] = renderer.gameObject.name + " (" + material.name + ")";
					}
				}
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[RenderDiag] scene=").Append(SceneManager.GetActiveScene().name).Append(" gfx=")
			.Append(SystemInfo.graphicsDeviceType)
			.Append(" renderers=")
			.Append(array.Length)
			.Append(" visible=")
			.Append(num)
			.Append(" fps~")
			.Append((1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f)).ToString("0"));
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera camera in allCameras)
		{
			stringBuilder.Append(" | cam ").Append(camera.name).Append(" clear=")
				.Append(camera.clearFlags)
				.Append(" bg=")
				.Append(camera.backgroundColor)
				.Append(" depth=")
				.Append(camera.depth)
				.Append(" mask=")
				.Append(camera.cullingMask);
		}
		stringBuilder.Append(" | _WorldLightColor=").Append(Shader.GetGlobalColor("_WorldLightColor")).Append(" _WorldLightVector=")
			.Append(Shader.GetGlobalVector("_WorldLightVector"));
		try
		{
			bool flag = Singleton<CustomColorCorrectionEffect>.HasInstance();
			stringBuilder.Append(" colorCorrection=").Append(flag ? "instance" : "none");
			if (flag)
			{
				CustomColorCorrectionEffect customColorCorrectionEffect = Singleton<CustomColorCorrectionEffect>.Instance();
				stringBuilder.Append(" enabled=").Append(customColorCorrectionEffect.enabled).Append(" on=")
					.Append(customColorCorrectionEffect.gameObject.name)
					.Append(" active=")
					.Append(customColorCorrectionEffect.gameObject.activeInHierarchy)
					.Append(" cam=")
					.Append(customColorCorrectionEffect.GetComponent<Camera>() != null);
			}
		}
		catch (Exception ex)
		{
			stringBuilder.Append(" colorCorrection err=").Append(ex.Message);
		}
		stringBuilder.Append(" _SkyAmbient=").Append(Shader.GetGlobalColor("_SkyAmbient"));
		OffServerShaderRemap.Report(stringBuilder.ToString());
		DumpTopRightWidgets();
		array2 = array;
		foreach (Renderer renderer2 in array2)
		{
			if (!renderer2.isVisible)
			{
				continue;
			}
			Material material2 = null;
			Material[] sharedMaterials = renderer2.sharedMaterials;
			foreach (Material material3 in sharedMaterials)
			{
				if (material3 != null && material3.shader != null && (material3.shader.name.StartsWith("LitSphere/Skin") || material3.shader.name.StartsWith("LitSphere/ThreeColor")))
				{
					material2 = material3;
					break;
				}
			}
			if (material2 == null)
			{
				continue;
			}
			StringBuilder stringBuilder2 = new StringBuilder("[RenderDiag] LitSphere material " + material2.name + " shader=" + material2.shader.name + " kw=" + string.Join(",", material2.shaderKeywords));
			string[] array3 = new string[6] { "_Color", "_ThreeColor_1", "_ThreeColor_2", "_ThreeColor_3", "_GroundAmbient", "_SkinColor" };
			foreach (string text in array3)
			{
				if (material2.shader.FindPropertyIndex(text) >= 0)
				{
					stringBuilder2.Append(" ").Append(text).Append("=")
						.Append(material2.GetColor(text));
				}
			}
			array3 = new string[5] { "_LitAmount", "_SpecularPower", "_RimIntensity", "_Wetness", "_Cutoff" };
			foreach (string text2 in array3)
			{
				if (material2.shader.FindPropertyIndex(text2) >= 0)
				{
					stringBuilder2.Append(" ").Append(text2).Append("=")
						.Append(material2.GetFloat(text2));
				}
			}
			array3 = new string[5] { "_MainTex", "_SubTex", "_LitSphereTex", "_DirtyTex", "_MetalicTex" };
			foreach (string text3 in array3)
			{
				if (material2.shader.FindPropertyIndex(text3) >= 0)
				{
					Texture texture = material2.GetTexture(text3);
					stringBuilder2.Append(" ").Append(text3).Append("=")
						.Append((texture == null) ? "null" : (texture.name + "(" + texture.width + "x" + texture.height + ")"));
				}
			}
			OffServerShaderRemap.Report(stringBuilder2.ToString());
			break;
		}
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(dictionary);
		list.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) => b.Value.CompareTo(a.Value));
		int num2 = 0;
		foreach (KeyValuePair<string, int> item in list)
		{
			if (num2++ >= 25)
			{
				break;
			}
			OffServerShaderRemap.Report("[RenderDiag]   " + item.Value + "x " + item.Key + (dictionary2[item.Key] ? "" : "  **UNSUPPORTED**") + "  e.g. " + dictionary3[item.Key]);
		}
	}
}

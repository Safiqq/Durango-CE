using System;
using System.Collections.Generic;
using UnityEngine;

public static class OffServerShaderRemap
{
	private static readonly Dictionary<string, Shader> _cache = new Dictionary<string, Shader>();

	private static readonly HashSet<EntityId> _done = new HashSet<EntityId>();

	private static int _remapped;

	private static int _missing;

	private static readonly Dictionary<string, Shader> _builtinCache = new Dictionary<string, Shader>();

	private static readonly string[] EngineShaderNames = new string[14]
	{
		"Standard", "Sprites/Default", "Skybox/Procedural", "Unlit/Transparent", "Unlit/Texture", "Legacy Shaders/Diffuse", "Legacy Shaders/Transparent/Diffuse", "Legacy Shaders/Transparent/VertexLit", "Particles/Additive", "Particles/Additive (Soft)",
		"Particles/Alpha Blended", "Particles/Alpha Blended Premultiply", "Particles/Multiply", "UI/Default"
	};

	private static Dictionary<string, Shader> _projectShaders;

	private static Dictionary<string, List<KeyValuePair<string[], Shader>>> _projectVariants;

	private static readonly Dictionary<EntityId, string> _lastKeywords = new Dictionary<EntityId, string>();

	private const float BusySweepInterval = 1.5f;

	private const float IdleSweepInterval = 8f;

	private const float BusyWindow = 12f;

	private const int MaterialsPerFrame = 256;

	private static float _nextSweep;

	private static float _busyUntil;

	private static Material[] _sweepList;

	private static int _sweepIndex;

	private static int _sweepsTimed;

	private static readonly HashSet<EntityId> _settled = new HashSet<EntityId>();

	private static readonly Dictionary<EntityId, Shader> _applied = new Dictionary<EntityId, Shader>();

	private static int _reapplied;

	public static bool Enabled => true;

	public static void CacheEngineShaders()
	{
		string[] engineShaderNames = EngineShaderNames;
		foreach (string text in engineShaderNames)
		{
			if (!_builtinCache.ContainsKey(text))
			{
				Shader shader = Shader.Find(text);
				if ((shader == null || !shader.isSupported) && text.StartsWith("Particles/"))
				{
					shader = Shader.Find("Legacy Shaders/" + text);
				}
				if (shader != null && shader.isSupported)
				{
					_builtinCache[text] = shader;
				}
			}
		}
		Report("[ShaderRemap] engine shaders cached: " + _builtinCache.Count + "/" + EngineShaderNames.Length);
	}

	private static Shader FindSupportedEngineShader(string name, Shader broken)
	{
		if (_builtinCache.TryGetValue(name, out var value))
		{
			return value;
		}
		if (name.StartsWith("Particles/") && _builtinCache.TryGetValue("Legacy Shaders/" + name, out value))
		{
			return value;
		}
		value = null;
		Shader[] array = Resources.FindObjectsOfTypeAll<Shader>();
		foreach (Shader shader in array)
		{
			if (shader != null && shader != broken && shader.name == name && shader.isSupported)
			{
				value = shader;
				break;
			}
		}
		if (value == null)
		{
			Shader shader2 = Shader.Find(name);
			if (shader2 != null && shader2 != broken && shader2.isSupported)
			{
				value = shader2;
			}
		}
		_builtinCache[name] = value;
		return value;
	}

	public static void Report(string message)
	{
		if (Application.isEditor)
		{
			UnityEngine.Debug.Log(message);
		}
		else
		{
			UnityEngine.Debug.LogError(message);
		}
	}

	public static void Fix(UnityEngine.Object asset)
	{
		if (asset == null || !Enabled)
		{
			return;
		}
		MarkBusy();
		try
		{
			Material material = asset as Material;
			if (material != null)
			{
				FixMaterial(material);
				return;
			}
			GameObject gameObject = asset as GameObject;
			if (gameObject != null)
			{
				FixGameObject(gameObject);
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("[ShaderRemap] " + asset.name + ": " + ex.Message);
		}
	}

	public static void FixGameObject(GameObject gameObject)
	{
		if (gameObject == null)
		{
			return;
		}
		Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Material[] sharedMaterials = componentsInChildren[i].sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				FixMaterial(sharedMaterials[j]);
			}
		}
		Projector[] componentsInChildren2 = gameObject.GetComponentsInChildren<Projector>(includeInactive: true);
		for (int k = 0; k < componentsInChildren2.Length; k++)
		{
			FixMaterial(componentsInChildren2[k].material);
		}
	}

	private static void LoadProjectShaders()
	{
		_projectShaders = new Dictionary<string, Shader>();
		_projectVariants = new Dictionary<string, List<KeyValuePair<string[], Shader>>>();
		Shader[] array = Resources.LoadAll<Shader>("Shaders");
		foreach (Shader shader in array)
		{
			if (shader == null)
			{
				continue;
			}
			int num = shader.name.IndexOf('|');
			if (num < 0)
			{
				if (!_projectShaders.ContainsKey(shader.name))
				{
					_projectShaders[shader.name] = shader;
				}
				continue;
			}
			string key = shader.name.Substring(0, num);
			string[] key2 = shader.name.Substring(num + 1).Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			if (!_projectVariants.TryGetValue(key, out var value))
			{
				value = new List<KeyValuePair<string[], Shader>>();
				_projectVariants[key] = value;
			}
			value.Add(new KeyValuePair<string[], Shader>(key2, shader));
		}
		Report("[ShaderRemap] project shaders loaded from Resources/Shaders: " + _projectShaders.Count + " (+" + _projectVariants.Count + " with keyword variants)");
	}

	public static Shader FindProjectShader(string name)
	{
		return FindProjectShader(name, null);
	}

	public static Shader FindProjectShader(string name, string[] materialKeywords)
	{
		if (_projectShaders == null)
		{
			LoadProjectShaders();
		}
		_projectShaders.TryGetValue(name, out var value);
		if (materialKeywords != null && materialKeywords.Length != 0 && _projectVariants.TryGetValue(name, out var value2))
		{
			int num = 0;
			foreach (KeyValuePair<string[], Shader> item in value2)
			{
				bool flag = true;
				string[] key = item.Key;
				foreach (string value3 in key)
				{
					if (Array.IndexOf(materialKeywords, value3) < 0)
					{
						flag = false;
						break;
					}
				}
				if (flag && item.Key.Length > num)
				{
					num = item.Key.Length;
					value = item.Value;
				}
			}
		}
		return value;
	}

	public static void MarkBusy()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		_busyUntil = realtimeSinceStartup + 12f;
		if (_sweepList == null && _nextSweep > realtimeSinceStartup + 1.5f)
		{
			_nextSweep = realtimeSinceStartup + 1.5f;
		}
	}

	public static void SweepAll()
	{
		if (!Enabled)
		{
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_sweepList == null)
		{
			if (realtimeSinceStartup < _nextSweep)
			{
				return;
			}
			float realtimeSinceStartup2 = Time.realtimeSinceStartup;
			try
			{
				_sweepList = Resources.FindObjectsOfTypeAll<Material>();
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("[ShaderRemap] sweep: " + ex.Message);
				_nextSweep = realtimeSinceStartup + 8f;
				return;
			}
			_sweepIndex = 0;
			if (_sweepsTimed < 3)
			{
				_sweepsTimed++;
				UnityEngine.Debug.Log("[ShaderRemap] sweep " + _sweepList.Length + " materials, FindObjectsOfTypeAll " + ((Time.realtimeSinceStartup - realtimeSinceStartup2) * 1000f).ToString("0.0") + " ms, settled " + _settled.Count);
			}
		}
		int num = Mathf.Min(_sweepIndex + 256, _sweepList.Length);
		while (_sweepIndex < num)
		{
			Material material = _sweepList[_sweepIndex++];
			try
			{
				FixMaterial(material);
			}
			catch (Exception ex2)
			{
				UnityEngine.Debug.LogError("[ShaderRemap] sweep: " + ex2.Message);
			}
		}
		if (_sweepIndex >= _sweepList.Length)
		{
			_sweepList = null;
			_nextSweep = realtimeSinceStartup + ((realtimeSinceStartup < _busyUntil) ? 1.5f : 8f);
		}
	}

	public static void FixMaterial(Material material)
	{
		if (material == null)
		{
			return;
		}
		EntityId entityId = material.GetEntityId();
		Shader shader = material.shader;
		if (shader == null)
		{
			return;
		}
		Shader value;
		bool flag = _applied.TryGetValue(entityId, out value) && value != null && shader != value;
		if (!flag && _settled.Contains(entityId))
		{
			return;
		}
		string text = string.Join(" ", material.shaderKeywords);
		if (_done.Add(entityId))
		{
			_lastKeywords[entityId] = text;
		}
		else
		{
			if (!flag && (!_lastKeywords.TryGetValue(entityId, out var value2) || value2 == text))
			{
				return;
			}
			_lastKeywords[entityId] = text;
		}
		string text2 = material.shader.name;
		int num = text2.IndexOf('|');
		if (num >= 0)
		{
			text2 = text2.Substring(0, num);
		}
		if (_projectVariants == null)
		{
			LoadProjectShaders();
		}
		if (!_projectVariants.ContainsKey(text2))
		{
			_settled.Add(entityId);
			_lastKeywords.Remove(entityId);
		}
		string key = text2 + "|" + text;
		if (!_cache.TryGetValue(key, out var value3))
		{
			value3 = FindProjectShader(text2, material.shaderKeywords);
			if (value3 == null && !text2.StartsWith("Hidden/"))
			{
				value3 = FindSupportedEngineShader(text2, material.shader);
			}
			_cache[key] = value3;
			if (value3 == null && !text2.StartsWith("Hidden/"))
			{
				_missing++;
				Report("[ShaderRemap] no project shader for '" + text2 + "' (bundle copy kept)");
			}
		}
		_applied[entityId] = value3;
		if (flag)
		{
			_reapplied++;
			if (_reapplied <= 5 || _reapplied % 200 == 0)
			{
				Report("[ShaderRemap] " + material.name + " came back with the bundle shader -> remapped again (" + _reapplied + " so far)");
			}
		}
		if (value3 != null && value3 != shader)
		{
			int renderQueue = material.renderQueue;
			bool flag2 = renderQueue != shader.renderQueue && (renderQueue > 2500 || value3.renderQueue <= 2500);
			material.shader = value3;
			material.renderQueue = (flag2 ? renderQueue : (-1));
			_remapped++;
			if (_remapped <= 20 || _remapped % 100 == 0)
			{
				Report("[ShaderRemap] " + material.name + " -> " + text2 + " (" + _remapped + " remapped, " + _missing + " missing)");
			}
		}
	}
}

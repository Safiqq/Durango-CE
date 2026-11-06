using System;
using UnityEngine;

namespace Durango.Utils;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
	private static bool _hasInstance;

	private static T _instance;

	protected bool Used;

	protected void Awake()
	{
		CheckDuplication();
		if (Used && CheckDontDestroyOnLoad())
		{
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		OnAwake();
	}

	protected void OnDestroy()
	{
		if (Used)
		{
			_hasInstance = false;
			_instance = null;
		}
		OnDestroyed();
	}

	protected virtual bool CheckDontDestroyOnLoad()
	{
		return false;
	}

	protected virtual void OnAwake()
	{
	}

	protected virtual void OnDestroyed()
	{
	}

	private void CheckDuplication()
	{
		Singleton<T> singleton = this;
		T[] array = UnityEngine.Object.FindObjectsOfType<T>();
		int num = array.Length;
		if (num >= 2)
		{
			for (int i = 0; i < num; i++)
			{
				Singleton<T> singleton2 = array[i] as Singleton<T>;
				if (singleton2.Used)
				{
					singleton = singleton2;
					break;
				}
				if (this != singleton2)
				{
					UnityEngine.Object.Destroy(singleton2.gameObject);
				}
			}
		}
		if (singleton != this)
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
		if (_instance == null)
		{
			_instance = singleton as T;
			_hasInstance = _instance != null;
			singleton.Used = true;
		}
	}

	private static void FindOrCreateInstance(bool showError)
	{
		T[] array = UnityEngine.Object.FindObjectsOfType<T>();
		if (array.Length >= 1)
		{
			SetInstance(array[0]);
		}
		else if (GameManager.IsSceneClosing && showError)
		{
			Type typeFromHandle = typeof(T);
			Debug.LogError("Try to acesss deleted singleton: " + ((typeFromHandle != null) ? typeFromHandle.ToString() : null));
		}
		else
		{
			Create(typeof(T).ToString());
		}
	}

	protected static void SetInstance(T instance)
	{
		_instance = instance;
		_hasInstance = _instance != null;
		(_instance as Singleton<T>).Used = true;
	}

	public static bool Exist()
	{
		if (!_hasInstance)
		{
			FindOrCreateInstance(showError: false);
		}
		return _hasInstance;
	}

	public static T Create(string name)
	{
		if (_hasInstance)
		{
			return _instance;
		}
		GameObject gameObject = new GameObject(name);
		if (!Application.isPlaying)
		{
			gameObject.hideFlags = HideFlags.HideAndDontSave;
		}
		T val = gameObject.AddComponent<T>();
		SetInstance(val);
		return val;
	}

	public static T Instance()
	{
		if (!_hasInstance)
		{
			FindOrCreateInstance(showError: true);
		}
		return _instance;
	}

	public static bool HasInstance()
	{
		return _hasInstance;
	}
}

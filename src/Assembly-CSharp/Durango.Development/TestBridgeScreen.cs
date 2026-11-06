using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Durango.UI.Control;
using UnityEngine;

namespace Durango.Development;

public static class TestBridgeScreen
{
	public static bool Handles(string[] args)
	{
		if (args.Length > 1)
		{
			if (!(args[1] == "tap") && !(args[1] == "hit") && !(args[1] == "elements"))
			{
				return args[1] == "models";
			}
			return true;
		}
		return false;
	}

	public static IEnumerator Execute(string[] args, TestBridge.Result result)
	{
		result.Set("width", Screen.width).Set("height", Screen.height).Set("origin", "top-left");
		if (args[1] == "elements")
		{
			List<object> list = new List<object>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			string filter = ((args.Length > 2) ? args[2] : "");
			UIEventListener[] array = UnityEngine.Object.FindObjectsByType<UIEventListener>();
			for (int i = 0; i < array.Length; i++)
			{
				AddElement(array[i].gameObject, filter, seen, list);
			}
			UIButton[] array2 = UnityEngine.Object.FindObjectsByType<UIButton>();
			for (int i = 0; i < array2.Length; i++)
			{
				AddElement(array2[i].gameObject, filter, seen, list);
			}
			Selectable[] array3 = UnityEngine.Object.FindObjectsByType<Selectable>();
			for (int i = 0; i < array3.Length; i++)
			{
				AddElement(array3[i].gameObject, filter, seen, list);
			}
			result.Set("elements", list).Set("count", list.Count);
			yield break;
		}
		if (args[1] == "models")
		{
			List<object> list2 = new List<object>();
			UIModelViewer[] array4 = UnityEngine.Object.FindObjectsByType<UIModelViewer>();
			foreach (UIModelViewer uIModelViewer in array4)
			{
				if (uIModelViewer.gameObject.activeInHierarchy)
				{
					list2.Add(new Dictionary<string, object>
					{
						{
							"path",
							Path(uIModelViewer.transform)
						},
						{
							"hasModel",
							uIModelViewer.ModelObject != null
						},
						{
							"hasRender",
							uIModelViewer.ModelRender != null
						},
						{
							"hasTexture",
							uIModelViewer.mainTexture != null
						},
						{
							"renderers",
							(!(uIModelViewer.ModelObject == null)) ? uIModelViewer.ModelObject.GetComponentsInChildren<Renderer>().Length : 0
						}
					});
				}
			}
			result.Set("models", list2);
			yield break;
		}
		if (args.Length != 4 || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) || float.IsNaN(result2) || float.IsNaN(result3) || result2 < 0f || result3 < 0f || result2 >= (float)Screen.width || result3 >= (float)Screen.height)
		{
			result.Fail("ui tap|hit <x> <y>: use top-left screenshot pixels within width/height");
			yield break;
		}
		UICamera current = UICamera.current;
		Camera currentCamera = UICamera.currentCamera;
		UICamera.MouseOrTouch currentTouch = UICamera.currentTouch;
		int currentTouchID = UICamera.currentTouchID;
		KeyCode currentKey = UICamera.currentKey;
		UICamera.ControlScheme currentScheme = UICamera.currentScheme;
		try
		{
			UICamera.MouseOrTouch mouseOrTouch = new UICamera.MouseOrTouch
			{
				pos = new Vector2(result2, (float)(Screen.height - 1) - result3),
				lastPos = new Vector2(result2, (float)(Screen.height - 1) - result3),
				key = KeyCode.Mouse0,
				pressTime = RealTime.time,
				clickNotification = UICamera.ClickNotification.Always
			};
			UICamera.Raycast(mouseOrTouch);
			GameObject current2 = mouseOrTouch.current;
			if (current2 == null || NGUITools.FindInParents<UIRoot>(current2) == null)
			{
				result.Fail("No visible native UI at this point; world input is not dispatched");
				yield break;
			}
			string value = Path(current2.transform);
			result.Set("target", value).Set("x", result2).Set("y", result3);
			UIButton component = current2.GetComponent<UIButton>();
			if (component != null && !component.isEnabled)
			{
				result.Fail("Native button is disabled");
				yield break;
			}
			if (args[1] == "hit")
			{
				yield break;
			}
			UICamera uICamera = UICamera.FindCameraForLayer(current2.layer);
			if (uICamera == null)
			{
				result.Fail("No native event camera for target");
				yield break;
			}
			UICamera.current = uICamera;
			UICamera.currentCamera = uICamera.cachedCamera;
			UICamera.currentTouch = mouseOrTouch;
			UICamera.currentTouchID = -1;
			UICamera.currentKey = KeyCode.Mouse0;
			UICamera.currentScheme = UICamera.ControlScheme.Mouse;
			uICamera.ProcessTouch(pressed: true, released: false);
			uICamera.ProcessTouch(pressed: false, released: true);
			result.Set("clicked", value).Set("route", "UICamera.Raycast/ProcessTouch");
		}
		finally
		{
			UICamera.current = current;
			UICamera.currentCamera = currentCamera;
			UICamera.currentTouch = currentTouch;
			UICamera.currentTouchID = currentTouchID;
			UICamera.currentKey = currentKey;
			UICamera.currentScheme = currentScheme;
		}
		yield return null;
	}

	private static void AddElement(GameObject go, string filter, HashSet<GameObject> seen, List<object> rows)
	{
		if (!go.activeInHierarchy || !seen.Add(go))
		{
			return;
		}
		string text = Path(go.transform);
		UILabel componentInChildren = go.GetComponentInChildren<UILabel>();
		string text2 = ((componentInChildren == null) ? "" : TestBridge.StripTags(componentInChildren.text));
		if (text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 && text2.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return;
		}
		UIWidget uIWidget = go.GetComponent<UIWidget>();
		if (uIWidget == null)
		{
			uIWidget = go.GetComponentInChildren<UIWidget>();
		}
		UICamera uICamera = UICamera.FindCameraForLayer(go.layer);
		if (!(uIWidget == null) && !(uICamera == null) && uIWidget.isVisible)
		{
			Vector3[] worldCorners = uIWidget.worldCorners;
			float num = float.PositiveInfinity;
			float num2 = float.NegativeInfinity;
			float num3 = float.PositiveInfinity;
			float num4 = float.NegativeInfinity;
			Vector3[] array = worldCorners;
			foreach (Vector3 position in array)
			{
				Vector3 vector = uICamera.cachedCamera.WorldToScreenPoint(position);
				num = Mathf.Min(num, vector.x);
				num2 = Mathf.Max(num2, vector.x);
				num3 = Mathf.Min(num3, (float)(Screen.height - 1) - vector.y);
				num4 = Mathf.Max(num4, (float)(Screen.height - 1) - vector.y);
			}
			UIButton component = go.GetComponent<UIButton>();
			rows.Add(new Dictionary<string, object>
			{
				{ "path", text },
				{ "label", text2 },
				{
					"enabled",
					component == null || component.isEnabled
				},
				{ "left", num },
				{ "top", num3 },
				{ "right", num2 },
				{ "bottom", num4 },
				{
					"x",
					(num + num2) / 2f
				},
				{
					"y",
					(num3 + num4) / 2f
				}
			});
		}
	}

	private static string Path(Transform target)
	{
		string text = target.name;
		while (target.parent != null)
		{
			target = target.parent;
			text = target.name + "/" + text;
		}
		return text;
	}
}

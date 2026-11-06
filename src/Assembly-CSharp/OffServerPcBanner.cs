using System;
using Durango.System;
using Durango.UI;
using UnityEngine;

public static class OffServerPcBanner
{
	public const int Height = 82;

	public static MenuBannerList Create(MenuListGroupBase group)
	{
		try
		{
			if (group == null || !Platform.Instance.UsePCUI)
			{
				return null;
			}
			UIPrefabMap uIPrefabMap = ResourceSingleton<UIPrefabMap>.Instance();
			GameObject[] array = ((uIPrefabMap != null) ? uIPrefabMap.GetUIList(UIPrefabMap.Type.Mobile, UIPrefabMap.Category.Main) : null);
			MenuBannerList menuBannerList = null;
			if (array != null)
			{
				GameObject[] array2 = array;
				foreach (GameObject gameObject in array2)
				{
					if (gameObject != null && gameObject.GetComponent<MenuListGroupBase>() != null)
					{
						menuBannerList = gameObject.GetComponentInChildren<MenuBannerList>(includeInactive: true);
						if (menuBannerList != null)
						{
							break;
						}
					}
				}
			}
			if (menuBannerList == null)
			{
				return null;
			}
			GameObject gameObject2 = UnityEngine.Object.Instantiate(menuBannerList.gameObject, group.transform);
			gameObject2.name = menuBannerList.gameObject.name;
			NGUITools.SetLayer(gameObject2, group.gameObject.layer);
			gameObject2.transform.localScale = Vector3.one;
			MenuBannerList component = gameObject2.GetComponent<MenuBannerList>();
			UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(UIBase.AnchorType.Base);
			if (rootAnchor != null)
			{
				component.SetAnchor(rootAnchor.gameObject, 0f, 0, 0f, 0, 1f, 0, 0f, 82);
			}
			else
			{
				component.SetAnchor((GameObject)null);
				component.width = 1280;
				component.height = 82;
			}
			Transform transform = gameObject2.transform.Find("SubMenu");
			if (transform != null)
			{
				transform.gameObject.SetActive(value: false);
			}
			gameObject2.SetActive(value: false);
			return component;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			return null;
		}
	}
}

using UnityEngine;

public class AndroidPermissionsManager
{
	private static AndroidJavaObject m_Activity;

	private static AndroidJavaObject m_PermissionService;

	private static AndroidJavaObject GetActivity()
	{
		if (m_Activity == null)
		{
			AndroidJavaClass androidJavaClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			m_Activity = androidJavaClass.GetStatic<AndroidJavaObject>("currentActivity");
		}
		return m_Activity;
	}

	private static AndroidJavaObject GetPermissionsService()
	{
		return m_PermissionService ?? (m_PermissionService = new AndroidJavaObject("com.unity3d.plugin.UnityAndroidPermissions"));
	}

	public static bool IsPermissionGranted(string permissionName)
	{
		return GetPermissionsService().Call<bool>("IsPermissionGranted", new object[2]
		{
			GetActivity(),
			permissionName
		});
	}

	public static void RequestPermission(string permissionName, AndroidPermissionCallback callback)
	{
		RequestPermission(new string[1] { permissionName }, callback);
	}

	public static void RequestPermission(string[] permissionNames, AndroidPermissionCallback callback)
	{
		GetPermissionsService().Call("RequestPermissionAsync", GetActivity(), permissionNames, callback);
	}
}

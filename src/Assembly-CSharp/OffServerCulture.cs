using System.Globalization;
using System.Threading;
using UnityEngine;

public static class OffServerCulture
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Boot()
	{
		CultureInfo currentCulture = (CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture);
		Thread.CurrentThread.CurrentCulture = currentCulture;
	}
}

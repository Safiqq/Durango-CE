using System.Globalization;
using System.Threading;
using UnityEngine;

public static class CommunityCulture
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Boot()
	{
		CultureInfo currentCulture = (CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture);
		Thread.CurrentThread.CurrentCulture = currentCulture;
		Durango.Offline.LocalGatewayServer.EnsureStarted();
	}
}

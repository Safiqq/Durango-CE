using System;
using System.Globalization;
using System.Windows.Forms;

namespace DurangoLauncher;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
		ApplicationConfiguration.Initialize();
		L.Load();
		Application.Run((Form)(object)new LauncherForm());
	}
}

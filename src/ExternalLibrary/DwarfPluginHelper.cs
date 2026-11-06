using System;
using System.Text;

// SANITIZED: Replaced native DwarfUnityPlugin.dll P/Invokes with safe managed no-op stubs.
// The legacy in-game browser UI is inactive on community servers, so eliminating native
// P/Invokes prevents loading DwarfUnityPlugin.dll and Dwarf.dll into process memory.
public class DwarfPluginHelper
{
	public void Invoke_InitializeDwarf(string dwarfAxePath, bool isRelativePath = false)
	{
	}

	public void Invoke_CleanupDwarf()
	{
	}

	public int Invoke_StartBrowser(int width, int height, string url)
	{
		return -1;
	}

	public void Invoke_StopBrowser(int id)
	{
	}

	public bool Invoke_GetTextureView(int id, ref IntPtr textureView)
	{
		textureView = IntPtr.Zero;
		return false;
	}

	public bool Invoke_SetCurrentUrl(int id, string url)
	{
		return false;
	}

	public bool Invoke_GetCurrentUrl(int id, StringBuilder urlBuffer, int bufferLength)
	{
		return false;
	}

	public void Invoke_SendMouseClick(int id, DWARF_MOUSE_BTN_TYPE buttonType, int x, int y, bool btnDown)
	{
	}

	public void Invoke_SendMouseMove(int id, int x, int y)
	{
	}

	public void Invoke_SendMouseWheel(int id, int x, int y, float delta)
	{
	}

	public void Invoke_SendKeyboardEvent(int id, int keyCode, bool keyDown)
	{
	}

	public void Invoke_RegisterBrowserFinishCallback(int id, FuncType_OnBrowserFinished proc)
	{
	}

	public void Invoke_RegisterPopupCallback(int id, FuncType_OnBrowserPopup proc)
	{
	}

	public void Invoke_RegisterJSObjectCallback(int id, FuncType_OnBrowserJSObjectCallback proc)
	{
	}

	public void Invoke_RegisterUrlChangedCallback(int id, FuncType_OnBrowserUrlChanged proc)
	{
	}
}

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DurangoLauncher;

internal static class Dpapi
{
	private struct Blob
	{
		public int Size;

		public nint Data;
	}

	private const int UiForbidden = 1;

	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DurangoLauncher/account/v1");

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptProtectData(ref Blob dataIn, string? descr, ref Blob entropy, nint reserved, nint prompt, int flags, ref Blob dataOut);

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptUnprotectData(ref Blob dataIn, nint descr, ref Blob entropy, nint reserved, nint prompt, int flags, ref Blob dataOut);

	[DllImport("kernel32.dll")]
	private static extern nint LocalFree(nint h);

	public static byte[] Protect(byte[] data)
	{
		return Run(data, protect: true);
	}

	public static byte[] Unprotect(byte[] data)
	{
		return Run(data, protect: false);
	}

	private static byte[] Run(byte[] data, bool protect)
	{
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		GCHandle gCHandle2 = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
		try
		{
			Blob dataIn = new Blob
			{
				Size = data.Length,
				Data = gCHandle.AddrOfPinnedObject()
			};
			Blob entropy = new Blob
			{
				Size = Entropy.Length,
				Data = gCHandle2.AddrOfPinnedObject()
			};
			Blob dataOut = default(Blob);
			if (!(protect ? CryptProtectData(ref dataIn, "DurangoLauncher", ref entropy, IntPtr.Zero, IntPtr.Zero, 1, ref dataOut) : CryptUnprotectData(ref dataIn, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, ref dataOut)))
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			try
			{
				byte[] array = new byte[dataOut.Size];
				Marshal.Copy(dataOut.Data, array, 0, dataOut.Size);
				return array;
			}
			finally
			{
				LocalFree(dataOut.Data);
			}
		}
		finally
		{
			gCHandle.Free();
			gCHandle2.Free();
		}
	}
}

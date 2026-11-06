using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durango.Localization.Overrides;

public static class MoCompiler
{
	public static byte[] Compile(List<PoEntry> entries)
	{
		int count = entries.Count;
		byte[][] array = new byte[count][];
		byte[][] array2 = new byte[count][];
		for (int i = 0; i < count; i++)
		{
			PoEntry poEntry = entries[i];
			string text = poEntry.MsgId;
			if (poEntry.MsgIdPlural != null)
			{
				text = text + "\0" + poEntry.MsgIdPlural;
			}
			if (poEntry.Context != null)
			{
				text = poEntry.Context + "\u0004" + text;
			}
			array[i] = Encoding.UTF8.GetBytes(text);
			array2[i] = Encoding.UTF8.GetBytes(string.Join("\0", poEntry.MsgStrs.ToArray()));
		}
		int num = 28;
		int num2 = num + count * 8;
		int num3 = num2 + count * 8;
		using MemoryStream memoryStream = new MemoryStream();
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write(2500072158u);
		binaryWriter.Write(0u);
		binaryWriter.Write((uint)count);
		binaryWriter.Write((uint)num);
		binaryWriter.Write((uint)num2);
		binaryWriter.Write(0u);
		binaryWriter.Write((uint)num3);
		int[] array3 = new int[count];
		int[] array4 = new int[count];
		int num4 = num3;
		for (int j = 0; j < count; j++)
		{
			array3[j] = num4;
			num4 += array[j].Length + 1;
		}
		for (int k = 0; k < count; k++)
		{
			array4[k] = num4;
			num4 += array2[k].Length + 1;
		}
		for (int l = 0; l < count; l++)
		{
			binaryWriter.Write((uint)array[l].Length);
			binaryWriter.Write((uint)array3[l]);
		}
		for (int m = 0; m < count; m++)
		{
			binaryWriter.Write((uint)array2[m].Length);
			binaryWriter.Write((uint)array4[m]);
		}
		for (int n = 0; n < count; n++)
		{
			binaryWriter.Write(array[n]);
			binaryWriter.Write((byte)0);
		}
		for (int num5 = 0; num5 < count; num5++)
		{
			binaryWriter.Write(array2[num5]);
			binaryWriter.Write((byte)0);
		}
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}
}

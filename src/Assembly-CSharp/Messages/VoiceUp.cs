using MsgPack;

namespace Messages;

public struct VoiceUp
{
	public const uint TypeCode = 7300001u;

	public byte Kind;

	public byte Level;

	public uint Seq;

	public byte[] Data;

	public string Text;

	public static void Pack(Packer packer, VoiceUp val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(6);
			packer.Pack(7300001u);
		}
		else
		{
			packer.PackArrayHeader(5);
		}
		packer.Pack(val.Kind);
		packer.Pack(val.Level);
		packer.Pack(val.Seq);
		packer.PackBinary(val.Data ?? new byte[0]);
		packer.PackString(val.Text ?? string.Empty);
	}

	public static VoiceUp Unpack(Unpacker unpacker)
	{
		VoiceUp result = default(VoiceUp);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Level = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Seq = unpacker.LastReadData.AsUInt32();
		unpacker.Read();
		result.Data = (unpacker.LastReadData.IsNil ? new byte[0] : unpacker.LastReadData.AsBinary());
		unpacker.Read();
		result.Text = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		return result;
	}

	public override string ToString()
	{
		return $"<VoiceUp Kind={Kind} Level={Level} Seq={Seq} Data={((Data != null) ? Data.Length : 0)}>";
	}
}

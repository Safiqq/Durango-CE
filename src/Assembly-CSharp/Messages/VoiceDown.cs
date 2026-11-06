using MsgPack;

namespace Messages;

public struct VoiceDown
{
	public const uint TypeCode = 7300002u;

	public byte Kind;

	public string Speaker;

	public byte Level;

	public uint Seq;

	public byte[] Data;

	public float Dist;

	public string Text;

	public static void Pack(Packer packer, VoiceDown val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(8);
			packer.Pack(7300002u);
		}
		else
		{
			packer.PackArrayHeader(7);
		}
		packer.Pack(val.Kind);
		packer.PackString(val.Speaker ?? string.Empty);
		packer.Pack(val.Level);
		packer.Pack(val.Seq);
		packer.PackBinary(val.Data ?? new byte[0]);
		packer.Pack(val.Dist);
		packer.PackString(val.Text ?? string.Empty);
	}

	public static VoiceDown Unpack(Unpacker unpacker)
	{
		VoiceDown result = default(VoiceDown);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Speaker = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		unpacker.Read();
		result.Level = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Seq = unpacker.LastReadData.AsUInt32();
		unpacker.Read();
		result.Data = (unpacker.LastReadData.IsNil ? new byte[0] : unpacker.LastReadData.AsBinary());
		unpacker.Read();
		result.Dist = (unpacker.LastReadData.IsNil ? 0f : unpacker.LastReadData.AsSingle());
		unpacker.Read();
		result.Text = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		return result;
	}

	public override string ToString()
	{
		return $"<VoiceDown Kind={Kind} Speaker={Speaker} Level={Level} Seq={Seq} Data={((Data != null) ? Data.Length : 0)} Dist={Dist}>";
	}
}

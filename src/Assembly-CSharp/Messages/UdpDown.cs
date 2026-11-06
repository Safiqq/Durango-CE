using MsgPack;

namespace Messages;

public struct UdpDown
{
	public const uint TypeCode = 7300023u;

	public const byte KindOffer = 1;

	public const byte KindSwitch = 2;

	public const byte KindFallback = 3;

	public const byte KindMoveAck = 4;

	public byte Kind;

	public string Json;

	public byte[] Bin;

	public static void Pack(Packer packer, UdpDown val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(4);
			packer.Pack(7300023u);
		}
		else
		{
			packer.PackArrayHeader(3);
		}
		packer.Pack(val.Kind);
		packer.PackString(val.Json ?? string.Empty);
		packer.PackBinary(val.Bin ?? new byte[0]);
	}

	public static UdpDown Unpack(Unpacker unpacker)
	{
		UdpDown result = default(UdpDown);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Json = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		unpacker.Read();
		result.Bin = (unpacker.LastReadData.IsNil ? new byte[0] : unpacker.LastReadData.AsBinary());
		return result;
	}

	public override string ToString()
	{
		return $"<UdpDown Kind={Kind} Json={Json} Bin={((Bin != null) ? Bin.Length : 0)}>";
	}
}

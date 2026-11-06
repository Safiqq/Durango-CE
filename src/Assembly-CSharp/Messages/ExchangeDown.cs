using MsgPack;

namespace Messages;

public struct ExchangeDown
{
	public const uint TypeCode = 7300008u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer packer, ExchangeDown val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(3);
			packer.Pack(7300008u);
		}
		else
		{
			packer.PackArrayHeader(2);
		}
		packer.Pack(val.Kind);
		packer.PackString(val.Json ?? string.Empty);
	}

	public static ExchangeDown Unpack(Unpacker unpacker)
	{
		ExchangeDown result = default(ExchangeDown);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Json = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		return result;
	}

	public override string ToString()
	{
		return $"<ExchangeDown Kind={Kind} Json={Json}>";
	}
}

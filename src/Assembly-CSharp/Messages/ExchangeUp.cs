using MsgPack;

namespace Messages;

public struct ExchangeUp
{
	public const uint TypeCode = 7300007u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer packer, ExchangeUp val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(3);
			packer.Pack(7300007u);
		}
		else
		{
			packer.PackArrayHeader(2);
		}
		packer.Pack(val.Kind);
		packer.PackString(val.Json ?? string.Empty);
	}

	public static ExchangeUp Unpack(Unpacker unpacker)
	{
		ExchangeUp result = default(ExchangeUp);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Json = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		return result;
	}

	public override string ToString()
	{
		return $"<ExchangeUp Kind={Kind} Json={Json}>";
	}
}

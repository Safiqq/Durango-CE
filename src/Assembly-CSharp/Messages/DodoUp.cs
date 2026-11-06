using MsgPack;

namespace Messages;

public struct DodoUp
{
	public const uint TypeCode = 7300005u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer packer, DodoUp val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(3);
			packer.Pack(7300005u);
		}
		else
		{
			packer.PackArrayHeader(2);
		}
		packer.Pack(val.Kind);
		packer.PackString(val.Json ?? string.Empty);
	}

	public static DodoUp Unpack(Unpacker unpacker)
	{
		DodoUp result = default(DodoUp);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Json = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		return result;
	}

	public override string ToString()
	{
		return $"<DodoUp Kind={Kind} Json={Json}>";
	}
}

using MsgPack;

namespace Messages;

public struct DodoDown
{
	public const uint TypeCode = 7300006u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer packer, DodoDown val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(3);
			packer.Pack(7300006u);
		}
		else
		{
			packer.PackArrayHeader(2);
		}
		packer.Pack(val.Kind);
		packer.PackString(val.Json ?? string.Empty);
	}

	public static DodoDown Unpack(Unpacker unpacker)
	{
		DodoDown result = default(DodoDown);
		unpacker.Read();
		result.Kind = unpacker.LastReadData.AsByte();
		unpacker.Read();
		result.Json = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		return result;
	}

	public override string ToString()
	{
		return string.Format("<DodoDown Kind={0} Json={1}>", Kind, (Json != null && Json.Length > 120) ? (Json.Substring(0, 120) + "…") : Json);
	}
}

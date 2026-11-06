using MsgPack;

namespace Messages;

public struct RankedDown
{
	public const uint TypeCode = 7300022u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, RankedDown value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300022u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static RankedDown Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new RankedDown
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

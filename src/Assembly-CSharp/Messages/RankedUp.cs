using MsgPack;

namespace Messages;

public struct RankedUp
{
	public const uint TypeCode = 7300021u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, RankedUp value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300021u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static RankedUp Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new RankedUp
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

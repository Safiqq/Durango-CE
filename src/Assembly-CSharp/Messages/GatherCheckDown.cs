using MsgPack;

namespace Messages;

public struct GatherCheckDown
{
	public const uint TypeCode = 7300025u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, GatherCheckDown value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300025u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static GatherCheckDown Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new GatherCheckDown
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

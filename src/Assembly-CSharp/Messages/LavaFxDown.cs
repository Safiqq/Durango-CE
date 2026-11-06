using MsgPack;

namespace Messages;

public struct LavaFxDown
{
	public const uint TypeCode = 7300012u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, LavaFxDown value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300012u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static LavaFxDown Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new LavaFxDown
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

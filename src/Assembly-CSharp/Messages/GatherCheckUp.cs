using MsgPack;

namespace Messages;

public struct GatherCheckUp
{
	public const uint TypeCode = 7300026u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, GatherCheckUp value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300026u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static GatherCheckUp Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new GatherCheckUp
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

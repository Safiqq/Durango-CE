using MsgPack;

namespace Messages;

public struct RunDown
{
	public const uint TypeCode = 7300014u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, RunDown value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300014u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static RunDown Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new RunDown
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

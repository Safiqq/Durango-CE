using MsgPack;

namespace Messages;

public struct CareerDown
{
	public const uint TypeCode = 7300010u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, CareerDown value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300010u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static CareerDown Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new CareerDown
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

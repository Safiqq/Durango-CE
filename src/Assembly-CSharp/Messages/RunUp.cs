using MsgPack;

namespace Messages;

public struct RunUp
{
	public const uint TypeCode = 7300013u;

	public byte Kind;

	public int On;

	public static void Pack(Packer p, RunUp value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300013u);
		}
		p.Pack(value.Kind);
		p.Pack(value.On);
	}

	public static RunUp Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new RunUp
		{
			Kind = kind,
			On = u.LastReadData.AsInt32()
		};
	}
}

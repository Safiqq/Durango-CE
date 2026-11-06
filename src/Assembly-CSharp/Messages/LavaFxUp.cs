using MsgPack;

namespace Messages;

public struct LavaFxUp
{
	public const uint TypeCode = 7300011u;

	public byte Kind;

	public int On;

	public static void Pack(Packer p, LavaFxUp value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300011u);
		}
		p.Pack(value.Kind);
		p.Pack(value.On);
	}

	public static LavaFxUp Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new LavaFxUp
		{
			Kind = kind,
			On = u.LastReadData.AsInt32()
		};
	}
}

using MsgPack;

namespace Messages;

public struct CareerUp
{
	public const uint TypeCode = 7300009u;

	public byte Kind;

	public int JobId;

	public static void Pack(Packer p, CareerUp value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300009u);
		}
		p.Pack(value.Kind);
		p.Pack(value.JobId);
	}

	public static CareerUp Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new CareerUp
		{
			Kind = kind,
			JobId = u.LastReadData.AsInt32()
		};
	}
}

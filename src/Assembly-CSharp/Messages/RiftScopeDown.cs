using MsgPack;

namespace Messages;

public struct RiftScopeDown
{
	public const uint TypeCode = 7300027u;

	public byte Kind;

	public string Json;

	public static void Pack(Packer p, RiftScopeDown value, bool hint = false)
	{
		p.PackArrayHeader(hint ? 3 : 2);
		if (hint)
		{
			p.Pack(7300027u);
		}
		p.Pack(value.Kind);
		p.PackString(value.Json ?? "{}");
	}

	public static RiftScopeDown Unpack(Unpacker u)
	{
		u.Read();
		byte kind = u.LastReadData.AsByte();
		u.Read();
		return new RiftScopeDown
		{
			Kind = kind,
			Json = u.LastReadData.AsString()
		};
	}
}

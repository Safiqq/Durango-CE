using MsgPack;

namespace Messages;

public struct CancelFiredProjectile
{
	public const uint TypeCode = 312098u;

	public string EntityId;

	public bool MoveCancel;

	public double StartAt;

	public int HitsPassed;

	public static void Pack(Packer packer, CancelFiredProjectile val, bool hint = false)
	{
		int num = ((!val.MoveCancel) ? 1 : 3);
		if (hint)
		{
			packer.PackArrayHeader(num + 1);
			packer.Pack(312098u);
		}
		else
		{
			packer.PackArrayHeader(num);
		}
		if (val.EntityId == null)
		{
			packer.PackString(string.Empty);
		}
		else
		{
			packer.PackString(val.EntityId);
		}
		if (val.MoveCancel)
		{
			packer.Pack(val.StartAt);
			packer.Pack(val.HitsPassed);
		}
	}

	public static CancelFiredProjectile Unpack(Unpacker unpacker)
	{
		unpacker.Read();
		return new CancelFiredProjectile
		{
			EntityId = unpacker.LastReadData.AsString()
		};
	}

	public override string ToString()
	{
		return "<CancelFiredProjectile EntityId=" + EntityId + ">";
	}
}

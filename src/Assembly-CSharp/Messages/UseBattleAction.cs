using MsgPack;

namespace Messages;

public struct UseBattleAction
{
	public const uint TypeCode = 3440u;

	public string ActionId;

	public double StartAt;

	public string TargetEntityId;

	public Point2? TargetTile;

	public int? MoveCancelMode;

	public static void Pack(Packer packer, UseBattleAction val, bool hint = false)
	{
		int num = (val.MoveCancelMode.HasValue ? 5 : 4);
		if (hint)
		{
			packer.PackArrayHeader(num + 1);
			packer.Pack(3440u);
		}
		else
		{
			packer.PackArrayHeader(num);
		}
		if (val.ActionId == null)
		{
			packer.PackString(string.Empty);
		}
		else
		{
			packer.PackString(val.ActionId);
		}
		packer.Pack(val.StartAt);
		if (val.TargetEntityId == null)
		{
			packer.PackNull();
		}
		else if (val.TargetEntityId == null)
		{
			packer.PackString(string.Empty);
		}
		else
		{
			packer.PackString(val.TargetEntityId);
		}
		if (!val.TargetTile.HasValue)
		{
			packer.PackNull();
		}
		else
		{
			packer.PackArrayHeader(2);
			packer.Pack((ushort)val.TargetTile.Value.x);
			packer.Pack((ushort)val.TargetTile.Value.y);
		}
		if (val.MoveCancelMode.HasValue)
		{
			packer.Pack(val.MoveCancelMode.Value);
		}
	}

	public static UseBattleAction Unpack(Unpacker unpacker)
	{
		unpacker.Read();
		UseBattleAction result = new UseBattleAction
		{
			ActionId = unpacker.LastReadData.AsString()
		};
		unpacker.Read();
		result.StartAt = unpacker.LastReadData.AsDouble();
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.TargetEntityId = null;
		}
		else
		{
			string targetEntityId = unpacker.LastReadData.AsString();
			result.TargetEntityId = targetEntityId;
		}
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.TargetTile = null;
		}
		else
		{
			unpacker.ReadUInt16(out var result2);
			Point2 value = new Point2
			{
				x = result2
			};
			unpacker.ReadUInt16(out result2);
			value.y = result2;
			result.TargetTile = value;
		}
		return result;
	}

	public override string ToString()
	{
		return $"<UseBattleAction ActionId={ActionId} StartAt={StartAt} TargetEntityId={TargetEntityId} TargetTile={TargetTile}>";
	}
}

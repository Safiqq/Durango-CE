using System.IO;
using MsgPack;
using Shared.Battle;
using UnityEngine;

namespace Messages;

public struct AttackAlerted
{
	public const uint TypeCode = 3928u;

	public string EntityId;

	public double EventAt;

	public double AttackTime;

	public DamageType DamageType;

	public float? Radius;

	public Vector2? RectSizeHalves;

	public WorldPosition? Center;

	public float? Yaw;

	public Vector2? Angles;

	public AttackRangeMask GroundMask;

	public AttackRangeContext RangeContext;

	public static void Pack(Packer packer, AttackAlerted val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader((val.RangeContext != null) ? 12 : ((val.GroundMask == null) ? 10 : 11));
			packer.Pack(3928u);
		}
		else
		{
			packer.PackArrayHeader((val.RangeContext != null) ? 11 : ((val.GroundMask == null) ? 9 : 10));
		}
		if (val.EntityId == null)
		{
			packer.PackString(string.Empty);
		}
		else
		{
			packer.PackString(val.EntityId);
		}
		packer.Pack(val.EventAt);
		packer.Pack(val.AttackTime);
		packer.Pack((int)val.DamageType);
		if (!val.Radius.HasValue)
		{
			packer.PackNull();
		}
		else
		{
			packer.Pack(val.Radius.Value);
		}
		if (!val.RectSizeHalves.HasValue)
		{
			packer.PackNull();
		}
		else
		{
			packer.PackArrayHeader(2);
			packer.Pack(val.RectSizeHalves.Value.x);
			packer.Pack(val.RectSizeHalves.Value.y);
		}
		if (!val.Center.HasValue)
		{
			packer.PackNull();
		}
		else
		{
			packer.PackArrayHeader(2);
			packer.Pack((uint)Mathf.RoundToInt(val.Center.Value.x));
			packer.Pack((uint)Mathf.RoundToInt(val.Center.Value.y));
		}
		if (!val.Yaw.HasValue)
		{
			packer.PackNull();
		}
		else
		{
			packer.Pack(val.Yaw.Value);
		}
		if (!val.Angles.HasValue)
		{
			packer.PackNull();
		}
		else
		{
			packer.PackArrayHeader(2);
			packer.Pack(val.Angles.Value.x);
			packer.Pack(val.Angles.Value.y);
		}
		if (val.GroundMask != null)
		{
			AttackRangeMask groundMask = val.GroundMask;
			packer.PackArrayHeader(7);
			packer.Pack(groundMask.ActiveUntil);
			packer.Pack(groundMask.X);
			packer.Pack(groundMask.Z);
			packer.Pack(groundMask.Cell);
			packer.Pack(groundMask.Width);
			packer.Pack(groundMask.Height);
			packer.PackArrayHeader(groundMask.Runs.Length);
			int[] runs = groundMask.Runs;
			foreach (int value in runs)
			{
				packer.Pack(value);
			}
		}
		else if (val.RangeContext != null)
		{
			packer.PackNull();
		}
		if (val.RangeContext != null)
		{
			AttackRangeContext rangeContext = val.RangeContext;
			packer.PackArrayHeader(5);
			packer.PackString(rangeContext.OwnerId);
			packer.Pack(rangeContext.AttackStartAt);
			packer.PackString(rangeContext.WindowId);
			packer.Pack(rangeContext.RevisionAt);
			packer.Pack(rangeContext.Cancel);
		}
	}

	public static AttackAlerted Unpack(Unpacker unpacker)
	{
		unpacker.Read();
		AttackAlerted result = new AttackAlerted
		{
			EntityId = unpacker.LastReadData.AsString()
		};
		unpacker.Read();
		result.EventAt = unpacker.LastReadData.AsDouble();
		unpacker.Read();
		result.AttackTime = unpacker.LastReadData.AsDouble();
		unpacker.Read();
		int num = unpacker.LastReadData.AsInt32();
		if (num < 0 || 3 < num)
		{
			result.DamageType = DamageType.Invalid;
		}
		else
		{
			result.DamageType = (DamageType)num;
		}
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.Radius = null;
		}
		else
		{
			float value = unpacker.LastReadData.AsSingle();
			result.Radius = value;
		}
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.RectSizeHalves = null;
		}
		else
		{
			Vector2 value2 = default(Vector2);
			unpacker.ReadSingle(out value2.x);
			unpacker.ReadSingle(out value2.y);
			result.RectSizeHalves = value2;
		}
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.Center = null;
		}
		else
		{
			WorldPosition value3 = default(WorldPosition);
			unpacker.ReadSingle(out value3.x);
			unpacker.ReadSingle(out value3.y);
			result.Center = value3;
		}
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.Yaw = null;
		}
		else
		{
			float value4 = unpacker.LastReadData.AsSingle();
			result.Yaw = value4;
		}
		unpacker.Read();
		if (unpacker.LastReadData.IsNil)
		{
			result.Angles = null;
		}
		else
		{
			Vector2 value5 = default(Vector2);
			unpacker.ReadSingle(out value5.x);
			unpacker.ReadSingle(out value5.y);
			result.Angles = value5;
		}
		if (unpacker.Read() && !unpacker.LastReadData.IsNil)
		{
			AttackRangeMask attackRangeMask = new AttackRangeMask();
			unpacker.Read();
			attackRangeMask.ActiveUntil = unpacker.LastReadData.AsDouble();
			unpacker.Read();
			attackRangeMask.X = unpacker.LastReadData.AsSingle();
			unpacker.Read();
			attackRangeMask.Z = unpacker.LastReadData.AsSingle();
			unpacker.Read();
			attackRangeMask.Cell = unpacker.LastReadData.AsSingle();
			unpacker.Read();
			attackRangeMask.Width = unpacker.LastReadData.AsInt32();
			unpacker.Read();
			attackRangeMask.Height = unpacker.LastReadData.AsInt32();
			unpacker.Read();
			int num2 = (int)unpacker.LastReadData.AsInt64();
			if (attackRangeMask.Width < 1 || attackRangeMask.Width > 512 || attackRangeMask.Height < 1 || attackRangeMask.Height > 512 || num2 < 0 || num2 > attackRangeMask.Width * attackRangeMask.Height * 3 || num2 % 3 != 0)
			{
				throw new InvalidDataException("Invalid attack range mask");
			}
			attackRangeMask.Runs = new int[num2];
			for (int i = 0; i < num2; i++)
			{
				unpacker.Read();
				attackRangeMask.Runs[i] = unpacker.LastReadData.AsInt32();
			}
			if (!attackRangeMask.IsValid())
			{
				throw new InvalidDataException("Invalid attack range run");
			}
			result.GroundMask = attackRangeMask;
		}
		if (unpacker.Read() && !unpacker.LastReadData.IsNil)
		{
			AttackRangeContext attackRangeContext = new AttackRangeContext();
			unpacker.Read();
			attackRangeContext.OwnerId = unpacker.LastReadData.AsString();
			unpacker.Read();
			attackRangeContext.AttackStartAt = unpacker.LastReadData.AsDouble();
			unpacker.Read();
			attackRangeContext.WindowId = unpacker.LastReadData.AsString();
			unpacker.Read();
			attackRangeContext.RevisionAt = unpacker.LastReadData.AsDouble();
			unpacker.Read();
			attackRangeContext.Cancel = unpacker.LastReadData.AsBoolean();
			if (!attackRangeContext.IsValid())
			{
				throw new InvalidDataException("Invalid attack range context");
			}
			result.RangeContext = attackRangeContext;
		}
		return result;
	}

	public override string ToString()
	{
		return $"<AttackAlerted EntityId={EntityId} EventAt={EventAt} AttackTime={AttackTime} DamageType={DamageType} Radius={Radius} RectSizeHalves={RectSizeHalves} Center={Center} Yaw={Yaw} Angles={Angles}>";
	}
}

using System.Collections.Generic;
using MsgPack;
using Shared.Item;

namespace Messages;

public struct Equipments
{
	public const uint TypeCode = 111u;

	public EquipSlotType CurrentType;

	public Dictionary<EquipSlotType, EquipmentSlot> Presets;

	public static void Pack(Packer packer, Equipments val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(3);
			packer.Pack(111u);
		}
		else
		{
			packer.PackArrayHeader(2);
		}
		packer.Pack((int)val.CurrentType);
		if (val.Presets == null)
		{
			packer.PackMapHeader(0);
			return;
		}
		packer.PackMapHeader(val.Presets.Count);
		foreach (KeyValuePair<EquipSlotType, EquipmentSlot> preset in val.Presets)
		{
			packer.Pack((int)preset.Key);
			EquipmentSlot.Pack(packer, preset.Value);
		}
	}

	public static Equipments Unpack(Unpacker unpacker)
	{
		return CommunityNinePresets.ReadEquipments(unpacker);
	}

	public override string ToString()
	{
		return $"<Equipments CurrentType={CurrentType} Presets={Presets}>";
	}
}

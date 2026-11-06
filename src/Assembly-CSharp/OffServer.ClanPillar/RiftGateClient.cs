using System;
using Durango.Logic.Item;
using InteractionData;
using UnityEngine;
using Yaml;

namespace OffServer.ClanPillar;

public static class RiftGateClient
{
	private const string Suffix = "_s02_riftgate";

	public const string Label = "ประต\u0e39รอยแยก";

	public const string IconSprite = "warphole_personal";

	public const int MapIconSize = 36;

	public static bool RoutesFromGate;

	public static Color IconColor => Color.white;

	public static bool IsGate(string entityId)
	{
		return entityId?.EndsWith("_s02_riftgate", StringComparison.Ordinal) ?? false;
	}

	public static bool IsRiftTemplate(RegionTemplate template)
	{
		if (template != null && template.Id != null)
		{
			return template.Id.StartsWith("dgs02_", StringComparison.Ordinal);
		}
		return false;
	}

	public static void MenuLook(string entityId, ref InteractionMenuData data)
	{
		if (data.Action == Interaction.SailingRoutes && IsGate(entityId))
		{
			data.Name = InteractionMenuData.GetActionName(Interaction.Warp);
			data.Icon = new ItemIcon("warphole_personal");
			data.SetColor(IconColor);
		}
	}
}

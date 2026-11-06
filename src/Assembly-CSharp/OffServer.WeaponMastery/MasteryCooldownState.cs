using System.Collections.Generic;
using Durango.Logic.Combat;

namespace OffServer.WeaponMastery;

public sealed class MasteryCooldownState
{
	private sealed class Snapshot
	{
		public BattleAction Action;

		public double Until;
	}

	private readonly Dictionary<string, Snapshot> snapshots = new Dictionary<string, Snapshot>();

	public void Sync(BattleAction action, double until)
	{
		if (!snapshots.TryGetValue(action.Data.Id, out var value))
		{
			value = (snapshots[action.Data.Id] = new Snapshot());
		}
		if (value.Action != action || value.Until != until)
		{
			action.CooldownUntil = until;
			value.Action = action;
			value.Until = until;
		}
	}
}

using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Logic.Combat;
using Durango.Network;
using Durango.Utils;
using Messages;
using UnityEngine;

public static class OffServerCombatMoveCancel
{
	public sealed class Swing
	{
		public BattleAction Action;

		public string ActionId;

		public double StartAt;

		public float StartTime;

		public int Mode;

		public float[] HitTimes;

		public float LastHit;

		public readonly List<KeyValuePair<BattleAction, double[]>> Before = new List<KeyValuePair<BattleAction, double[]>>();

		public readonly List<double[]> After = new List<double[]>();

		public bool Sent;

		public int StartFrame;

		public int LastTryFrame = -1;

		public bool Held;
	}

	public const string SettingKey = "combat_move_cancel";

	public const string OptionEnabled = "combat.move_cancel.enabled";

	public const string OptionDefaultMode = "combat.move_cancel.default_mode";

	public const string OptionPvp = "pvp.move_cancel_enabled";

	public static readonly string[] Options = new string[3] { "0", "1", "2" };

	private static readonly HashSet<string> DashMotions = new HashSet<string> { "Twohand_AttackDash", "Lance_Attack_Dash", "Barehand_Attack_Pursuit", "Onehand_AttackStrong", "Barehand_AttackStrong", "Barehand_Attack_Kick_A", "Barehand_Attack_Kick_B", "Twohand_AttackSwing_Lower" };

	public static bool Enabled => OptionSystem.GetBool("combat.move_cancel.enabled");

	public static int SelectedMode
	{
		get
		{
			long num = OptionSystem.GetLong("combat.move_cancel.default_mode", 0L);
			if (num < 0 || num > 2)
			{
				return 0;
			}
			return (int)num;
		}
	}

	public static int? ReportedMode
	{
		get
		{
			if (!Enabled)
			{
				return null;
			}
			return SelectedMode;
		}
	}

	public static int Parse(string value)
	{
		if (string.IsNullOrEmpty(value) || !int.TryParse(value, out var result) || result < 0 || result > 2)
		{
			return 0;
		}
		return result;
	}

	public static void OnSetting(string value, bool save)
	{
	}

	public static void Install()
	{
		Singleton<GameManager>.Instance().WelcomeReceived += delegate
		{
			OptionSystem.ResetServerValue("combat.move_cancel.enabled", value: false);
			OptionSystem.ResetServerValue("combat.move_cancel.default_mode", 0L);
			OptionSystem.ResetServerValue("pvp.move_cancel_enabled", value: false);
		};
	}

	public static bool InScope(BattleAction action)
	{
		if (action == null || KUtility.GetSize(action.Data.AttackInfo) <= 0)
		{
			return false;
		}
		string motion = action.Data.Meta.Motion;
		if (!string.IsNullOrEmpty(motion))
		{
			return !DashMotions.Contains(motion);
		}
		return true;
	}

	private static bool IsPlayerId(string id)
	{
		if (id != null && id.Length == 12)
		{
			return id.StartsWith("p_", StringComparison.Ordinal);
		}
		return false;
	}

	public static int ModeFor(BattleAction action, DamageableEntity target)
	{
		if (action == null || !Enabled)
		{
			return 0;
		}
		int selectedMode = SelectedMode;
		if (selectedMode == 0 || !InScope(action))
		{
			return 0;
		}
		if (target != null && IsPlayerId(target.GetEntityId()) && !OptionSystem.GetBool("pvp.move_cancel_enabled"))
		{
			return 0;
		}
		return selectedMode;
	}

	private static double[] Snap(BattleAction a)
	{
		return new double[4]
		{
			a.CooldownSince,
			a.CooldownUntil,
			a.ProhibitedUntil,
			a.ActivatedUntil.HasValue ? a.ActivatedUntil.Value : double.NaN
		};
	}

	public static Swing Begin(BattleAction action, DamageableEntity target, double startAt, float startTime)
	{
		int num = ModeFor(action, target);
		if (num == 0)
		{
			return null;
		}
		Swing swing = new Swing();
		swing.Action = action;
		swing.ActionId = action.Data.Id;
		swing.StartAt = startAt;
		swing.StartTime = startTime;
		swing.StartFrame = Time.frameCount;
		swing.Mode = num;
		int size = KUtility.GetSize(action.Data.AttackInfo);
		swing.HitTimes = new float[size];
		float num2 = Mathf.Max(0.1f, action.PlaybackRate);
		for (int i = 0; i < size; i++)
		{
			swing.HitTimes[i] = action.Data.AttackInfo[i].AttackTime / num2;
			swing.LastHit = Mathf.Max(swing.LastHit, swing.HitTimes[i]);
		}
		foreach (BattleAction currentBattleAction in GameSystem<CombatSystem>.Instance().GetCurrentBattleActions())
		{
			swing.Before.Add(new KeyValuePair<BattleAction, double[]>(currentBattleAction, Snap(currentBattleAction)));
		}
		return swing;
	}

	public static void AfterUse(Swing swing)
	{
		if (swing == null)
		{
			return;
		}
		foreach (KeyValuePair<BattleAction, double[]> item in swing.Before)
		{
			swing.After.Add(Snap(item.Key));
		}
	}

	public static bool Cancel(Swing swing, float now)
	{
		if (swing == null || swing.Sent || !Enabled)
		{
			return false;
		}
		float num = now - swing.StartTime;
		int frameCount = Time.frameCount;
		if (swing.LastTryFrame < 0 && frameCount - swing.StartFrame <= 2)
		{
			swing.Held = true;
		}
		else if (swing.Held && frameCount - swing.LastTryFrame > 2)
		{
			swing.Held = false;
		}
		swing.LastTryFrame = frameCount;
		int num2 = (swing.Held ? 1 : swing.Mode);
		if (num2 == 1 && num < swing.LastHit)
		{
			return false;
		}
		if (num2 != 1 && num2 != 2)
		{
			return false;
		}
		int num3 = 0;
		for (int i = 0; i < swing.HitTimes.Length; i++)
		{
			if (swing.HitTimes[i] <= num)
			{
				num3++;
			}
		}
		swing.Sent = true;
		try
		{
			Connections.Frontend.Send(new CancelFiredProjectile
			{
				EntityId = swing.ActionId,
				MoveCancel = true,
				StartAt = swing.StartAt,
				HitsPassed = num3
			}).On<OK>(delegate
			{
				Refund(swing);
			}).On<Abort>(delegate
			{
			});
		}
		catch (Exception)
		{
		}
		return true;
	}

	private static void Refund(Swing swing)
	{
		for (int i = 0; i < swing.Before.Count && i < swing.After.Count; i++)
		{
			BattleAction key = swing.Before[i].Key;
			double[] value = swing.Before[i].Value;
			double[] array = swing.After[i];
			if (key.CooldownSince == array[0] && key.CooldownUntil == array[1])
			{
				key.CooldownSince = value[0];
				key.CooldownUntil = value[1];
			}
			if (key.ProhibitedUntil == array[2])
			{
				key.ProhibitedUntil = value[2];
			}
			if ((key.ActivatedUntil.HasValue ? key.ActivatedUntil.Value : double.NaN).Equals(array[3]))
			{
				key.ActivatedUntil = (double.IsNaN(value[3]) ? ((double?)null) : new double?(value[3]));
			}
		}
	}

	public static string Label()
	{
		string text = OffServerDiscordLink.Lang switch
		{
			"th" => "ท\u0e48าต\u0e35 (เด\u0e34นต\u0e31ดท\u0e48า)",
			"id" => "Serangan (jalan untuk membatalkan)",
			"ko" => "공격 (이동으로 동작 취소)",
			_ => "Attacks (walk to cancel)",
		};
		if (!Enabled)
		{
			text = OffServerDiscordLink.Lang switch
			{
				"th" => text + " · เซ\u0e34ร\u0e4cฟป\u0e34ดอย\u0e39\u0e48",
				"id" => text + " · dimatikan server",
				"ko" => text + " · 서버 꺼짐",
				_ => text + " · off on this server",
			};
		}
		return text;
	}

	public static string OptionLabel(string value)
	{
		string lang = OffServerDiscordLink.Lang;
		return Parse(value) switch
		{
			1 => lang switch
			{
				"th" => "ต\u0e31ดท\u0e48าหล\u0e31งฟ\u0e31นโดน",
				"id" => "Batal setelah kena",
				"ko" => "타격 후 취소",
				_ => "Cancel after hit",
			},
			2 => lang switch
			{
				"th" => "แบบ ROV",
				"id" => "Gaya ROV",
				"ko" => "ROV 방식",
				_ => "ROV style",
			},
			_ => lang switch
			{
				"th" => "แบบเด\u0e34ม",
				"id" => "Klasik",
				"ko" => "기본",
				_ => "Classic",
			},
		};
	}
}

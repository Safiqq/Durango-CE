using System;
using Durango.Logic.Combat;
using Durango.Network;
using Messages;
using UnityEngine;

namespace OffServer.WeaponMastery;

public static class SwordMasteryClient
{
	public const string Focus = "onehand_mastery_focus";

	public const string Flurry = "onehand_mastery_flurry";

	public static Action Changed;

	public static string Weapon = "sword_onehand";

	private static Action opened;

	private static long request;

	private static float sent;

	private static readonly MasteryCooldownState cooldowns = new MasteryCooldownState();

	public static bool Loaded { get; private set; }

	public static bool Pending { get; private set; }

	public static int Level { get; private set; }

	public static int Mask { get; private set; }

	public static long Revision { get; private set; }

	public static bool Live(string tag)
	{
		if (OptionSystem.GetBool("combat.mastery.sword.enabled"))
		{
			if (!(tag == "sword_onehand") && !(tag == "sword_twohand"))
			{
				return tag == "axe_onehand";
			}
			return true;
		}
		return false;
	}

	private static long Value(string key)
	{
		return OptionSystem.GetLong("combat.mastery.sword." + key, 0L);
	}

	private static void Send(string kind, string payload)
	{
		if (!Pending)
		{
			request = DateTime.UtcNow.Ticks % 1000000000 + 1;
			sent = Time.realtimeSinceStartup;
			Pending = true;
			Connections.Frontend.Send(new UseBattleAction
			{
				ActionId = "weapon_mastery:" + kind + ":" + request + payload,
				StartAt = Connections.Frontend.GetPredictedServerTime()
			});
		}
	}

	public static void Open(Action callback)
	{
		Open("sword_onehand", callback);
	}

	public static void Open(string weapon, Action callback)
	{
		if (!Pending)
		{
			Weapon = weapon;
			opened = callback;
			Send("get", ":" + weapon);
		}
	}

	public static void Choose(int index)
	{
		if (Loaded && !Pending)
		{
			Send("choose", ":" + Revision + ":" + index + ":" + Weapon);
		}
	}

	public static void Tick(CombatSystem combat)
	{
		string[] array = new string[6] { "onehand_mastery_focus", "onehand_mastery_flurry", "twohand_mastery_cleave", "twohand_mastery_moon", "axe_mastery_break", "axe_mastery_whirl" };
		foreach (string text in array)
		{
			BattleAction battleAction = combat.GetBattleAction(text);
			string text2 = (text.StartsWith("twohand_mastery") ? "sword_twohand" : (text.StartsWith("axe_mastery") ? "axe_onehand" : "sword"));
			double until = OptionSystem.GetDouble("combat.mastery." + text2 + ".cooldown." + text);
			if (battleAction != null)
			{
				cooldowns.Sync(battleAction, until);
			}
		}
		if (!Pending)
		{
			return;
		}
		if (Value("ack") != request)
		{
			if (Time.realtimeSinceStartup - sent > 10f)
			{
				Pending = false;
				opened = null;
				UIManager.SystemMsg("โหลดความชำนาญไม\u0e48สำเร\u0e47จ กร\u0e38ณาเป\u0e34ดใหม\u0e48");
			}
			return;
		}
		Pending = false;
		long num = Value("status");
		if (Value("version") != 1 || !Live(Weapon))
		{
			Loaded = false;
			opened = null;
			UIManager.SystemMsg("เซ\u0e34ร\u0e4cฟเวอร\u0e4cย\u0e31งไม\u0e48เป\u0e34ดระบบสก\u0e34ลพ\u0e34เศษ");
			return;
		}
		if (Value("version") == 1 && num >= 0)
		{
			Loaded = true;
			string text3 = "combat.mastery." + ((Weapon == "sword_onehand") ? "sword" : Weapon) + ".";
			Level = (int)OptionSystem.GetLong(text3 + "level", 0L);
			Mask = (int)OptionSystem.GetLong(text3 + "mask", 0L);
			Revision = Value("revision");
		}
		if (num < 0)
		{
			opened = null;
			UIManager.SystemMsg(num switch
			{
				-2L => "ข\u0e49อม\u0e39ลสก\u0e34ลเปล\u0e35\u0e48ยนแล\u0e49ว กร\u0e38ณาเป\u0e34ดใหม\u0e48",
				-3L => "ออกจากโหมดต\u0e48อส\u0e39\u0e49ก\u0e48อนเปล\u0e35\u0e48ยนสก\u0e34ล",
				_ => "บ\u0e31นท\u0e36กความชำนาญไม\u0e48สำเร\u0e47จ",
			});
			return;
		}
		Action action = opened;
		opened = null;
		if (action != null)
		{
			action();
		}
		else if (Changed != null)
		{
			Changed();
		}
	}
}

using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.Skill;
using Shared.Skill;
using Yaml;

public static class CommunityWeaponMasteryTab
{
	public static Shared.Skill.Category TabOf(Dictionary<string, Yaml.Skill[]> subs, Shared.Skill.Category category)
	{
		if (subs == null)
		{
			return category;
		}
		foreach (KeyValuePair<string, Yaml.Skill[]> sub in subs)
		{
			Yaml.Skill[] value = sub.Value;
			if (value == null)
			{
				continue;
			}
			for (int i = 0; i < value.Length; i++)
			{
				if (value[i] != null && value[i].TabCategory != Shared.Skill.Category.Invalid)
				{
					return value[i].TabCategory;
				}
			}
		}
		return category;
	}

	public static int Level(SkillSystem skills)
	{
		return Math.Max(skills.GetCategoryLevel(Shared.Skill.Category.MeleeCombat), skills.GetCategoryLevel(Shared.Skill.Category.RangedCombat));
	}

	public static bool ShowTab(Shared.Skill.Category type)
	{
		if (type != Shared.Skill.Category.WeaponMastery)
		{
			return true;
		}
		List<Bundle> skills = GameSystem<SkillSystem>.Instance().Skills;
		for (int i = 0; i < skills.Count; i++)
		{
			if (skills[i].Tab == Shared.Skill.Category.WeaponMastery)
			{
				return true;
			}
		}
		return false;
	}

	public static string Name()
	{
		return Pick(CommunityDiscordLink.Lang, "การชำนาญอาว\u0e38ธ", "Weapon Mastery", "Penguasaan Senjata", "무기 숙련");
	}

	public static string Description()
	{
		return Pick(CommunityDiscordLink.Lang, "รวมสก\u0e34ลฝ\u0e36กอาว\u0e38ธ ความเร\u0e47วเด\u0e34นตอนต\u0e48อส\u0e39\u0e49 และความชำนาญอาว\u0e38ธประช\u0e34ด/ระยะไกลไว\u0e49ท\u0e35\u0e48เด\u0e35ยว\nสายดาบ ขวาน กระบอง หอก ใช\u0e49เลเวลหมวดการต\u0e48อส\u0e39\u0e49ระยะประช\u0e34ด · สายธน\u0e39 หน\u0e49าไม\u0e49 ใช\u0e49เลเวลหมวดการย\u0e34งธน\u0e39\nเลเวลของแท\u0e47บน\u0e35\u0e49 = เลเวลท\u0e35\u0e48ส\u0e39งกว\u0e48าของสองหมวดน\u0e31\u0e49น (แท\u0e47บน\u0e35\u0e49ไม\u0e48ม\u0e35 EXP ของต\u0e31วเอง)", "Weapon training, combat move speed and melee/ranged weapon mastery in one place.\nSword, axe, blunt and spear skills use your Melee level · bow and crossbow skills use your Ranged level.\nThis tab shows the higher of the two levels (it has no EXP of its own).", "Latihan senjata, kecepatan gerak saat bertarung, dan penguasaan senjata jarak dekat/jauh di satu tempat.\nPedang, kapak, gada, dan tombak memakai level Jarak Dekat · busur dan busur silang memakai level Jarak Jauh.\nTab ini menampilkan level tertinggi dari keduanya (tanpa EXP sendiri).", "무기 훈련, 전투 이동 속도, 근접/원거리 무기 숙련을 한곳에 모았습니다.\n검·도끼·둔기·창은 근접전 레벨, 활·석궁은 궁술 레벨을 따릅니다.\n이 탭의 레벨은 두 계열 중 높은 레벨입니다 (자체 경험치 없음).");
	}

	private static string Pick(string lang, string th, string en, string id, string ko)
	{
		return lang switch
		{
			"th" => th,
			"id" => id,
			"ko" => ko,
			_ => en,
		};
	}
}

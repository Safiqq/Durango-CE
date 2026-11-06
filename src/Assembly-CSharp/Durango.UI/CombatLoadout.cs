using System;
using System.Collections.Generic;
using Durango.Logic.Clusters;
using Durango.Logic.Combat;
using Durango.Logic.Item;
using UnityEngine;

namespace Durango.UI;

internal static class CombatLoadout
{
	[Serializable]
	private class Profile
	{
		public string[] slots;
	}

	internal const int Count = 6;

	private static string character;

	private static string key;

	private static PlayerBehavior.WeaponFramework weapon;

	private static string[] slots;

	private static float nextBagRead;

	private static readonly Dictionary<string, ItemData> bag = new Dictionary<string, ItemData>();

	private static readonly string[] actions = new string[6] { "s0", "s1", "s2", "s3", "s4", "s5" };

	internal static bool Enabled => OffServerLink.CombatLoadout;

	internal static string ProfileKey
	{
		get
		{
			Current();
			return key;
		}
	}

	internal static string Skill(int index)
	{
		return actions[index];
	}

	internal static int ActionSlot(string binding)
	{
		return Array.IndexOf(actions, binding);
	}

	internal static bool IsItem(string binding)
	{
		return binding?.StartsWith("i:", StringComparison.Ordinal) ?? false;
	}

	internal static bool Consumable(ItemData item)
	{
		if (item != null)
		{
			if (item.GetTagData("eatable") == null && item.GetTagData("drinkable") == null)
			{
				return item.GetTagData("usable") != null;
			}
			return true;
		}
		return false;
	}

	internal static string[] Current()
	{
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (localPlayer == null || string.IsNullOrEmpty(GameManager.PlayerId))
		{
			return null;
		}
		if (character == GameManager.PlayerId && weapon == localPlayer.CurrentWeaponFramework)
		{
			return slots;
		}
		character = GameManager.PlayerId;
		weapon = localPlayer.CurrentWeaponFramework;
		key = "combat_loadout_v1_" + character + "_" + weapon;
		bag.Clear();
		nextBagRead = 0f;
		slots = null;
		if (PlayerPrefs.HasKey(key))
		{
			try
			{
				Profile profile = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(key));
				if (profile != null && profile.slots != null && profile.slots.Length == 6)
				{
					slots = profile.slots;
				}
			}
			catch (ArgumentException)
			{
			}
		}
		return slots;
	}

	internal static string[] Draft()
	{
		string[] array = Current();
		if (array != null)
		{
			return (string[])array.Clone();
		}
		string[] array2 = new string[6];
		BattleAction[] actionSlots = GameSystem<CombatSystem>.Instance().ActionSlots;
		int num = 0;
		while (actionSlots != null && num < 6 && num < actionSlots.Length)
		{
			if (actionSlots[num] != null)
			{
				array2[num] = Skill(num);
			}
			num++;
		}
		return array2;
	}

	internal static void Save(string[] draft)
	{
		slots = (string[])draft.Clone();
		PlayerPrefs.SetString(key, JsonUtility.ToJson(new Profile
		{
			slots = slots
		}));
		PlayerPrefs.Save();
	}

	internal static ItemData FindCached(string binding)
	{
		if (Time.unscaledTime >= nextBagRead)
		{
			nextBagRead = Time.unscaledTime + 0.4f;
			bag.Clear();
			foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
			{
				if (Consumable(playerItem) && !bag.ContainsKey("i:" + playerItem.PrototypeId))
				{
					bag.Add("i:" + playerItem.PrototypeId, playerItem);
				}
			}
		}
		if (!bag.TryGetValue(binding, out var value))
		{
			return null;
		}
		return value;
	}

	internal static bool HandleItem(BattleActionButton button, bool press)
	{
		string id = button.Id;
		if (!IsItem(id))
		{
			return false;
		}
		if (!press || OffServerRanked.InMatch || PlayerBehavior.LocalPlayer == null || !PlayerBehavior.LocalPlayer.IsAlive || !button.IsClickable || OffServerHudLayout.BlocksInput || !GameManager.IsReady)
		{
			return true;
		}
		InventorySystem inventory = GameSystem<InventorySystem>.Instance();
		foreach (ItemData item in inventory.PlayerItemList)
		{
			if (!Consumable(item) || id != "i:" + item.PrototypeId)
			{
				continue;
			}
			UIManager.MessageBox.ShowLockConfirm(item, delegate
			{
				if (inventory.FindItem(item.Id) == item)
				{
					inventory.UseItem(item);
				}
			});
			break;
		}
		return true;
	}
}

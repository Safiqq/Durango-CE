using System;
using System.Collections.Generic;
using System.Linq;
using Durango.Logic;
using Durango.Network;
using Durango.Render.Particle;
using Durango.Utils;
using Newtonsoft.Json;
using Shared.Battle;
using UnityEngine;
using Yaml;
using Yaml.Util;

namespace Community.WeaponMastery;

public static class MasteryBloodFx
{
	private sealed class Entry
	{
		public string Id;

		public double Until;

		public int Level;

		public int Particle;

		public float NextBurst;
	}

	public const string Status = "combat_deep_wound";

	private const string Loop = "Particle/Prefabs/Durango_01/01_State/FX_State_Bleeding_persist_01_Loop.prefab";

	private static readonly Dictionary<string, Entry> active = new Dictionary<string, Entry>();

	private static StatusEffectSystem subscribed;

	private static void Definitions()
	{
		Dictionary<string, StatusEffectTemplate[]> instance = SingletonDict<string, StatusEffectTemplate[]>.Instance;
		if (instance == null || instance.ContainsKey("combat_deep_wound"))
		{
			return;
		}
		StatusEffectTemplate[] array = SingletonDict<string, StatusEffectTemplate[]>.Get("life_decr");
		if (array != null)
		{
			List<StatusEffectTemplate> list = new List<StatusEffectTemplate>();
			for (int i = 1; i <= 2; i++)
			{
				StatusEffectTemplate statusEffectTemplate = JsonConvert.DeserializeObject<StatusEffectTemplate>(JsonConvert.SerializeObject(array[0]));
				statusEffectTemplate.MinLevel = (statusEffectTemplate.MaxLevel = i);
				statusEffectTemplate.Name = "แผลล\u0e36ก • เล\u0e37อดไหล";
				statusEffectTemplate.Description = "เส\u0e35ยเล\u0e37อด " + ((i == 2) ? "10" : "5") + "% ATK ของผ\u0e39\u0e49โจมต\u0e35ต\u0e48อว\u0e34นาท\u0e35 • ไม\u0e48สะสมช\u0e31\u0e49น • ระยะเวลาตามสก\u0e34ล";
				statusEffectTemplate.Icon = "icon_se_bleeding_inner";
				statusEffectTemplate.IconColor = "negative";
				statusEffectTemplate.SkinEffect = "";
				statusEffectTemplate.Effects = new EffectDetail[0];
				statusEffectTemplate.Duration = "6";
				list.Add(statusEffectTemplate);
			}
			instance["combat_deep_wound"] = list.ToArray();
		}
	}

	private static void Stop(Entry e)
	{
		if (e.Particle != 0 && Durango.Utils.Singleton<ParticleManager>.HasInstance())
		{
			ParticleManager.Stop(e.Particle, immediately: false);
		}
		e.Particle = 0;
	}

	private static void Updated(StatusEffects effects)
	{
		StatusEffect statusEffect = effects.List.FirstOrDefault((StatusEffect e) => e.Id == "combat_deep_wound" && e.IsValid);
		Entry value;
		if (statusEffect == null)
		{
			if (active.TryGetValue(effects.EntityId, out value))
			{
				Stop(value);
				active.Remove(effects.EntityId);
			}
			return;
		}
		if (!active.TryGetValue(effects.EntityId, out value))
		{
			Dictionary<string, Entry> dictionary = active;
			string entityId = effects.EntityId;
			Entry obj = new Entry
			{
				Id = effects.EntityId
			};
			value = obj;
			dictionary[entityId] = obj;
		}
		value.Until = statusEffect.Until;
		value.Level = statusEffect.Level;
	}

	public static void Tick()
	{
		try
		{
			Definitions();
			if (PlayerBehavior.LocalPlayer == null)
			{
				foreach (Entry value in active.Values)
				{
					Stop(value);
				}
				active.Clear();
				return;
			}
			StatusEffectSystem statusEffectSystem = GameSystem<StatusEffectSystem>.Instance();
			if (statusEffectSystem != subscribed)
			{
				if (subscribed != null)
				{
					subscribed.StatusEffectsUpdated -= Updated;
				}
				subscribed = statusEffectSystem;
				subscribed.StatusEffectsUpdated += Updated;
			}
			double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
			Entry[] array = active.Values.ToArray();
			foreach (Entry entry in array)
			{
				GameObject gameObject = Durango.Utils.Singleton<ObjectManager>.Instance().FindObject(entry.Id);
				AnimalBehavior animalBehavior = ((gameObject != null) ? gameObject.GetComponent<AnimalBehavior>() : null);
				if (animalBehavior == null || !animalBehavior.IsAlive || !animalBehavior.isActiveAndEnabled || entry.Until <= predictedServerTime)
				{
					Stop(entry);
					active.Remove(entry.Id);
					continue;
				}
				if (!CommunityBloodFx.Enabled)
				{
					Stop(entry);
					continue;
				}
				Transform transform = animalBehavior.GetBodyPartTransform(BodyPart.Body) ?? animalBehavior.Bip001Transform ?? animalBehavior.transform;
				if (entry.Particle == 0 && Durango.Utils.Singleton<ParticleManager>.HasInstance())
				{
					entry.Particle = ParticleManager.EmitFollow("Particle/Prefabs/Durango_01/01_State/FX_State_Bleeding_persist_01_Loop.prefab", Vector3.zero, Quaternion.identity, transform);
				}
				if (Time.unscaledTime >= entry.NextBurst)
				{
					entry.NextBurst = Time.unscaledTime + 0.55f;
					_ = CommunityBloodFx.Hits;
					CommunityBloodFx.Splash(transform.position, animalBehavior.transform.position.y, animalBehavior.transform.right, (entry.Level == 2) ? 0.75f : 0.5f, CommunityBloodFx.SizeMul(animalBehavior.Size));
				}
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}
}

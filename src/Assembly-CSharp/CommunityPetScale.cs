using System.Runtime.CompilerServices;
using Messages;
using UnityEngine;

public static class CommunityPetScale
{
	private sealed class State
	{
		public Vector3 BaseScale;

		public float Mul = 1f;
	}

	public const string Caps = "pet-scale";

	private static readonly ConditionalWeakTable<AnimalBehavior, State> States = new ConditionalWeakTable<AnimalBehavior, State>();

	public static float FromPercent(int percent)
	{
		if (percent <= 0)
		{
			return 1f;
		}
		return Mathf.Clamp((float)percent / 100f, 0.5f, 3f);
	}

	public static void Apply(AnimalBehavior animal, PetStats stat)
	{
		if (animal == null)
		{
			return;
		}
		float num = FromPercent(stat.CommunityScale);
		if (!States.TryGetValue(animal, out var value))
		{
			if (Mathf.Approximately(num, 1f))
			{
				return;
			}
			value = new State
			{
				BaseScale = animal.transform.localScale
			};
			States.Add(animal, value);
		}
		if (!Mathf.Approximately(value.Mul, num))
		{
			value.Mul = num;
			animal.transform.localScale = value.BaseScale * num;
		}
	}
}

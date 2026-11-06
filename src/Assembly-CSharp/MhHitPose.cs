using System.Collections.Generic;
using Shared.Battle;
using UnityEngine;

internal sealed class MhHitPose
{
	private const float Hold = 0.045f;

	private const float Blend = 0.06f;

	private readonly List<Transform> bones = new List<Transform>();

	private readonly Quaternion[] contact;

	private readonly Quaternion[] animated;

	private string clip;

	private bool pending;

	private bool applied;

	private float started;

	internal MhHitPose(Transform root)
	{
		Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>();
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			dictionary[transform.name.Replace(' ', '_')] = transform;
		}
		string[] array = new string[11]
		{
			"Spine", "Spine1", "Spine2", "L_Clavicle", "L_UpperArm", "L_Forearm", "L_Hand", "R_Clavicle", "R_UpperArm", "R_Forearm",
			"R_Hand"
		};
		foreach (string text in array)
		{
			if (dictionary.TryGetValue("Bip001_" + text, out var value))
			{
				bones.Add(value);
			}
		}
		contact = new Quaternion[bones.Count];
		animated = new Quaternion[bones.Count];
	}

	internal static bool IsMelee(AttackType type)
	{
		if (type < AttackType.BareHands || type > AttackType.Spear)
		{
			return type == AttackType.Chainsaw;
		}
		return true;
	}

	internal void Begin(string swing)
	{
		if (bones.Count != 0 && !string.IsNullOrEmpty(swing) && !(clip == swing))
		{
			clip = swing;
			pending = true;
		}
	}

	internal void Apply(string playing)
	{
		if (clip == null)
		{
			return;
		}
		if (pending)
		{
			started = Time.unscaledTime;
			for (int i = 0; i < bones.Count; i++)
			{
				contact[i] = bones[i].localRotation;
			}
			pending = false;
		}
		float num = Time.unscaledTime - started;
		if (clip != playing || num >= 0.105000004f)
		{
			clip = null;
			return;
		}
		float t = Mathf.SmoothStep(0f, 1f, (num - 0.045f) / 0.06f);
		for (int j = 0; j < bones.Count; j++)
		{
			animated[j] = bones[j].localRotation;
			bones[j].localRotation = Quaternion.Slerp(contact[j], animated[j], t);
		}
		applied = true;
	}

	internal void Restore()
	{
		if (applied)
		{
			for (int i = 0; i < bones.Count; i++)
			{
				bones[i].localRotation = animated[i];
			}
			applied = false;
		}
	}

	internal void Reset()
	{
		Restore();
		clip = null;
		pending = false;
	}
}

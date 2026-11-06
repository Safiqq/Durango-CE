using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Durango.Player.Animation;
using Durango.Utils;
using Newtonsoft.Json;
using PigeonCoopToolkit.Effects.Trails;
using UnityEngine;

namespace Community.WeaponMastery;

public static class NativeComboAnimation
{
	private sealed class Key
	{
		public float time;

		public float value;

		public float inTangent;

		public float outTangent;
	}

	private sealed class Curve
	{
		public string path;

		public string property;

		public Key[] keys;
	}

	private sealed class Motion
	{
		public string tag;

		public string name;

		public string authoring;

		public float length;

		public float[] impacts;

		public string[] sourceMotionClips;

		public Curve[] curves;
	}

	private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
	{
		{ "sword_onehand", "ULT_TripleSwing_SlashOnlySix" },
		{ "sword_twohand", "ULT_Twohand_LeftDownRightUpDown" },
		{ "axe_onehand", "ULT_AxeOnehand_NativeComboSix" }
	};

	private static readonly Dictionary<string, DateTime> stamps = new Dictionary<string, DateTime>();

	private static Animation installed;

	private static float retry;

	public static bool IsMotion(string motion)
	{
		return Names.Values.Contains(motion);
	}

	public static bool Ready(string motion)
	{
		if (installed != null)
		{
			return installed.GetClip(PlayerBehavior.LocalPlayer.MotionPrefix + motion) != null;
		}
		return false;
	}

	public static void Ensure(PlayerBehavior player)
	{
		if (player == null || player.Anim == null || Time.realtimeSinceStartup < retry)
		{
			return;
		}
		retry = Time.realtimeSinceStartup + 1f;
		if (installed != player.Anim)
		{
			installed = player.Anim;
			stamps.Clear();
		}
		foreach (KeyValuePair<string, string> name in Names)
		{
			try
			{
				string text = "CommunityMastery/NativeCombos/" + name.Key;
				TextAsset textAsset = Resources.Load<TextAsset>(text);
				if (textAsset == null)
				{
					continue;
				}
				DateTime minValue = DateTime.MinValue;
				if ((stamps.TryGetValue(text, out var value) && value == minValue && Ready(name.Value)) || player.Anim.IsPlaying(player.MotionPrefix + name.Value))
				{
					continue;
				}
				Motion data = JsonConvert.DeserializeObject<Motion>(textAsset.text);
				int num = ((name.Key == "sword_twohand") ? 3 : 6);
				if (data.tag != name.Key || data.name != name.Value || !data.authoring.StartsWith("native-segment-", StringComparison.Ordinal) || data.curves == null || data.curves.Length < 40 || data.impacts.Length != num || data.length <= 0f || data.impacts.Any((float t) => t < 0f || t > data.length))
				{
					throw new Exception("Invalid native combo package: " + name.Key);
				}
				AnimationClip animationClip = new AnimationClip
				{
					name = data.name,
					legacy = true,
					frameRate = 60f,
					wrapMode = WrapMode.Once
				};
				Curve[] curves = data.curves;
				foreach (Curve curve in curves)
				{
					if (!curve.path.StartsWith("Bip001", StringComparison.Ordinal) || (!curve.property.StartsWith("localPosition.") && !curve.property.StartsWith("localRotation.")) || curve.keys == null || curve.keys.Length < 2 || curve.keys.Any((Key k) => float.IsNaN(k.value) || float.IsInfinity(k.value) || k.time < 0f || k.time > data.length + 0.001f))
					{
						throw new Exception("Invalid player curve");
					}
					bool planar = curve.path == "Bip001" && (curve.property == "localPosition.x" || curve.property == "localPosition.z");
					animationClip.SetCurve(curve.path, typeof(Transform), curve.property, new AnimationCurve(curve.keys.Select((Key k) => new Keyframe(k.time, planar ? 0f : k.value, planar ? 0f : k.inTangent, planar ? 0f : k.outTangent)).ToArray()));
				}
				animationClip.EnsureQuaternionContinuity();
				string text2 = player.MotionPrefix + data.name;
				if (player.Anim.GetClip(text2) != null)
				{
					player.Anim.RemoveClip(text2);
				}
				player.Anim.AddClip(animationClip, text2);
				string text3 = ((name.Key == "sword_twohand") ? "Twohand" : "Onehand");
				PlayerAnimationClipManager playerAnimationClipManager = Singleton<PlayerAnimationClipManager>.Instance();
				PlayerAnimationClipInfo playerAnimationClipInfo = JsonConvert.DeserializeObject<PlayerAnimationClipInfo>(JsonConvert.SerializeObject(playerAnimationClipManager.GetPlayerAnimationClipInfo(text3 + "_Attack_A")));
				playerAnimationClipInfo.Clip = data.name;
				playerAnimationClipInfo.Length = data.length;
				playerAnimationClipInfo.IsLoop = false;
				playerAnimationClipInfo.EquipAnimation = null;
				playerAnimationClipInfo.Path = null;
				playerAnimationClipInfo.Tag = PlayerAnimationClipTag.Once;
				playerAnimationClipInfo.FadeInTime = 0.05f;
				playerAnimationClipInfo.FadeOutTime = 0.1f;
				((Dictionary<string, PlayerAnimationClipInfo>)typeof(PlayerAnimationClipManager).GetField("_animClipsDict", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playerAnimationClipManager))[data.name] = playerAnimationClipInfo;
				InstallEffects(player, text2, data, text3);
				stamps[text] = minValue;
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}

	private static object InstallEffects(PlayerBehavior player, string key, Motion data, string prefix)
	{
		AnimationEventController obj = player.GetComponent<AnimationEventController>() ?? player.Anim.GetComponent<AnimationEventController>() ?? player.GetComponentInChildren<AnimationEventController>();
		if (obj == null)
		{
			throw new Exception("Native animation event controller unavailable");
		}
		Dictionary<string, List<AnimationEventInfo>> animationEvents = obj.AnimationEvents;
		AnimationEventInfo[] source = animationEvents.Where((KeyValuePair<string, List<AnimationEventInfo>> p) => p.Key.Contains(prefix + "_Attack") && !p.Key.Contains("Lab")).SelectMany((KeyValuePair<string, List<AnimationEventInfo>> p) => p.Value).ToArray();
		AnimationEventInfo animationEventInfo = source.FirstOrDefault((AnimationEventInfo e) => e.animEventCmd == AnimEventCmd.TrailOn && !string.IsNullOrEmpty(e.gameObjectPath));
		AnimationEventInfo animationEventInfo2 = source.FirstOrDefault((AnimationEventInfo e) => e.animEventCmd == AnimEventCmd.SoundEvent && !string.IsNullOrEmpty(e.gameObjectPath) && e.gameObjectPath.StartsWith(prefix.ToLowerInvariant() + "_attack", StringComparison.Ordinal));
		List<AnimationEventInfo> list = new List<AnimationEventInfo>();
		float[] impacts = data.impacts;
		foreach (float num2 in impacts)
		{
			if (animationEventInfo != null)
			{
				AnimationEventInfo animationEventInfo3 = animationEventInfo.CopySerializable();
				animationEventInfo3.enumID = animationEventInfo.enumID;
				animationEventInfo3.time = Mathf.Max(0f, num2 - 0.14f);
				animationEventInfo3.frame = Mathf.RoundToInt(animationEventInfo3.time * 60f);
				animationEventInfo3.isOnce = true;
				animationEventInfo3.isFinally = false;
				animationEventInfo3.BoneName = "Attachment_RH";
				animationEventInfo3.TrailTipOverrideRequired = true;
				animationEventInfo3.TrailFixOption = PlaneTrail.FixOption.None;
				animationEventInfo3.EmitPosition = Vector3.zero;
				animationEventInfo3.BakedTrailData = "";
				animationEventInfo3.TrailData = null;
				list.Add(animationEventInfo3);
				list.Add(new AnimationEventInfo(0)
				{
					animEventCmd = AnimEventCmd.TrailOff,
					time = num2 + 0.12f,
					frame = Mathf.RoundToInt((num2 + 0.12f) * 60f),
					gameObjectPath = animationEventInfo3.gameObjectPath,
					isOnce = true,
					isFinally = true
				});
			}
			if (animationEventInfo2 != null)
			{
				AnimationEventInfo animationEventInfo4 = animationEventInfo2.CopySerializable();
				animationEventInfo4.enumID = animationEventInfo2.enumID;
				animationEventInfo4.time = Mathf.Max(0f, num2 - 0.06f);
				animationEventInfo4.frame = Mathf.RoundToInt(animationEventInfo4.time * 60f);
				animationEventInfo4.isOnce = true;
				animationEventInfo4.isFinally = false;
				list.Add(animationEventInfo4);
			}
		}
		list.Sort();
		animationEvents[key] = list;
		animationEvents[data.name] = list;
		return new
		{
			events = list.Count,
			trail = animationEventInfo?.gameObjectPath,
			sound = animationEventInfo2?.gameObjectPath
		};
	}
}

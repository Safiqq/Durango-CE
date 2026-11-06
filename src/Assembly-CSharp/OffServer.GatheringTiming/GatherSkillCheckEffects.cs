using System;
using System.Linq;
using UnityEngine;

namespace OffServer.GatheringTiming;

internal sealed class GatherSkillCheckEffects
{
	private readonly Transform parent;

	private readonly Transform shakeRoot;

	private readonly SkillCheckGraphic dial;

	private readonly UILabel label;

	private readonly UISprite shade;

	private readonly UISprite halo;

	private readonly UISprite burst;

	private readonly UISprite glint;

	private readonly UISprite[] sparks = new UISprite[6];

	private float began;

	private float finished = -100f;

	private bool success;

	private float lastShakeSample;

	private const string ReadySound = "Action_HeartBeat_01";

	private const string GreatSound = "Action_CPR_Perfect_01";

	private const string MissSound = "Action_CPR_Miss_01";

	public GatherSkillCheckEffects(Transform parent, Transform shakeRoot, SkillCheckGraphic dial, UILabel label)
	{
		this.parent = parent;
		this.shakeRoot = shakeRoot;
		this.dial = dial;
		this.label = label;
		ResourceSingleton<UISpriteManager>.Instance().TryGet("effect_circle_glow", out var atlas, out var _);
		if (atlas == null)
		{
			throw new InvalidOperationException("Native registered effect sprites are not loaded");
		}
		shade = Sprite(atlas, "circle_black_glow", 250, 250, 198);
		halo = Sprite(atlas, "effect_circle_glow", 242, 242, 199);
		burst = Sprite(atlas, "effect_circle_glow", 238, 238, 203);
		glint = Sprite(atlas, "effect_glow", 58, 58, 204);
		for (int i = 0; i < sparks.Length; i++)
		{
			sparks[i] = Sprite(atlas, "skillup_glow", 10, 10, 204);
		}
		string[] array = new string[3] { "Action_HeartBeat_01", "Action_CPR_Perfect_01", "Action_CPR_Miss_01" };
		for (int j = 0; j < array.Length; j++)
		{
			SoundManager.PrepareEvent(array[j]);
		}
		Reset();
	}

	private UISprite Sprite(UIAtlas atlas, string name, int width, int height, int depth)
	{
		if (ResourceSingleton<UISpriteManager>.Instance().GetSprite(name) == null)
		{
			throw new InvalidOperationException("Missing native effect sprite: " + name);
		}
		GameObject gameObject = new GameObject("Native skill check FX / " + name);
		gameObject.layer = parent.gameObject.layer;
		gameObject.transform.SetParent(shakeRoot, worldPositionStays: false);
		gameObject.transform.localPosition = new Vector3(0f, 35f, 0f);
		UISprite uISprite = gameObject.AddComponent<UISprite>();
		uISprite.spriteName = name;
		uISprite.width = width;
		uISprite.height = height;
		uISprite.depth = depth;
		return uISprite;
	}

	private static void KillTweens(GameObject go)
	{
		UITweener[] components = go.GetComponents<UITweener>();
		for (int i = 0; i < components.Length; i++)
		{
			components[i].enabled = false;
		}
	}

	private void Reset()
	{
		KillTweens(shakeRoot.gameObject);
		shakeRoot.localPosition = Vector3.zero;
		shakeRoot.localRotation = Quaternion.identity;
		shakeRoot.localScale = Vector3.one;
		foreach (UISprite item in new UISprite[4] { shade, halo, burst, glint }.Concat(sparks))
		{
			KillTweens(item.gameObject);
			item.alpha = 0f;
			item.transform.localScale = Vector3.one;
		}
		KillTweens(label.gameObject);
		label.alpha = 1f;
		label.transform.localScale = Vector3.one;
		label.transform.localPosition = new Vector3(0f, 175f, 0f);
		dial.transform.localPosition = Vector3.zero;
		dial.Visibility = 1f;
	}

	private static void Play(string sound)
	{
		SoundManager.PlayEvent(sound);
	}

	public void Begin()
	{
		Reset();
		began = Time.unscaledTime;
		finished = -100f;
		shade.color = Color.black;
		shade.alpha = 0.4f;
		halo.color = PresetColor.UIBeige;
		parent.localScale = Vector3.one * 0.55f;
		TweenScale.Begin(parent.gameObject, 0.18f, Vector3.one * 0.6f).method = UITweener.Method.EaseOut;
		label.color = PresetColor.UINomad;
		Play("Action_HeartBeat_01");
	}

	public void Feedback(CheckResult result, double angle)
	{
		finished = Time.unscaledTime;
		success = result == CheckResult.Good || result == CheckResult.Great;
		bool flag = result == CheckResult.Great;
		Shake(result);
		Color color = ((!success) ? PresetColor.UILightRed : (flag ? PresetColor.UIYellow : PresetColor.UIBeige));
		label.color = color;
		label.alpha = 1f;
		label.transform.localScale = Vector3.one * (flag ? 1.2f : 1.1f);
		TweenScale.Begin(label.gameObject, 0.2f, Vector3.one).method = UITweener.Method.EaseOut;
		label.transform.localPosition = new Vector3(0f, 175f, 0f);
		TweenPosition.Begin(label.gameObject, 0.85f, new Vector3(0f, 193f, 0f)).method = UITweener.Method.EaseOut;
		TweenAlpha.Begin(label.gameObject, 0.36f, 0f).delay = 0.58f;
		burst.color = color;
		burst.alpha = (flag ? 0.24f : (success ? 0.15f : 0.1f));
		burst.transform.localScale = Vector3.one * 0.92f;
		TweenScale.Begin(burst.gameObject, 0.55f, Vector3.one * (flag ? 1.36f : 1.22f)).method = UITweener.Method.EaseOut;
		TweenAlpha.Begin(burst.gameObject, 0.5f, 0f);
		if (success)
		{
			Vector3 vector = new Vector3((float)Math.Cos(angle * Math.PI / 180.0) * 108f, 35f + (float)Math.Sin(angle * Math.PI / 180.0) * 108f, 0f);
			glint.transform.localPosition = vector;
			glint.color = color;
			glint.alpha = (flag ? 0.72f : 0.48f);
			glint.transform.localScale = Vector3.one * 0.65f;
			TweenScale.Begin(glint.gameObject, 0.35f, Vector3.one * 1.45f);
			TweenAlpha.Begin(glint.gameObject, 0.4f, 0f);
			int num = (flag ? 6 : 3);
			for (int i = 0; i < num; i++)
			{
				UISprite obj = sparks[i];
				obj.color = color;
				obj.alpha = 0.85f;
				obj.transform.localPosition = vector;
				obj.transform.localScale = Vector3.one * (flag ? 0.9f : 0.65f);
				double num2 = (angle + ((double)i - (double)(num - 1) * 0.5) * 28.0) * Math.PI / 180.0;
				TweenPosition.Begin(pos: vector + new Vector3((float)Math.Cos(num2), (float)Math.Sin(num2), 0f) * (flag ? 60 : 38), go: obj.gameObject, duration: 0.48f).method = UITweener.Method.EaseOut;
				TweenAlpha.Begin(obj.gameObject, 0.45f, 0f);
			}
			Play(flag ? "Action_CPR_Perfect_01" : "Action_HeartBeat_01");
		}
		else
		{
			halo.color = PresetColor.UILightRed;
			Play("Action_CPR_Miss_01");
		}
	}

	private void Shake(CheckResult result)
	{
		bool flag = result == CheckResult.Great;
		bool flag2 = result == CheckResult.Good;
		float duration = (flag2 ? 0.28f : (flag ? 0.4f : 0.46f));
		Vector3 pos = (flag2 ? new Vector3(12f, 7f, 0f) : (flag ? new Vector3(22f, 12f, 0f) : new Vector3(28f, 9f, 0f)));
		AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.14f, -0.86f), new Keyframe(0.29f, 0.64f), new Keyframe(0.45f, -0.46f), new Keyframe(0.62f, 0.28f), new Keyframe(0.8f, -0.12f), new Keyframe(1f, 0f));
		shakeRoot.localPosition = Vector3.zero;
		shakeRoot.localRotation = Quaternion.identity;
		shakeRoot.localScale = Vector3.one;
		TweenPosition tweenPosition = TweenPosition.Begin(shakeRoot.gameObject, duration, pos);
		tweenPosition.from = Vector3.zero;
		tweenPosition.method = UITweener.Method.Linear;
		tweenPosition.animationCurve = animationCurve;
		tweenPosition.Sample(0f, isFinished: false);
		TweenRotation tweenRotation = TweenRotation.Begin(shakeRoot.gameObject, duration, Quaternion.Euler(0f, 0f, flag2 ? 1.4f : (flag ? 2.6f : 3.6f)));
		tweenRotation.from = Vector3.zero;
		tweenRotation.method = UITweener.Method.Linear;
		tweenRotation.animationCurve = animationCurve;
		tweenRotation.Sample(0f, isFinished: false);
		TweenScale tweenScale = TweenScale.Begin(shakeRoot.gameObject, duration, Vector3.one * (flag ? 1.1f : 1.06f));
		tweenScale.from = Vector3.one;
		tweenScale.method = UITweener.Method.Linear;
		tweenScale.animationCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.22f, -0.3f), new Keyframe(0.48f, 0.15f), new Keyframe(1f, 0f));
		tweenScale.Sample(0f, isFinished: false);
		lastShakeSample = -100f;
	}

	public void Animate(CheckPhase phase)
	{
		float num = Time.unscaledTime - began;
		if (phase == CheckPhase.Result)
		{
			float num2 = Time.unscaledTime - finished;
			if (num2 < 0.56f && num2 - lastShakeSample > 0.035f)
			{
				lastShakeSample = num2;
			}
			halo.alpha = (success ? 0.13f : 0.16f) * Mathf.Clamp01(1f - num2 / 0.42f);
			dial.Visibility = 1f - Mathf.Clamp01((num2 - 0.4f) / 0.5f);
			shade.alpha = 0.4f * dial.Visibility;
		}
		else
		{
			halo.alpha = ((phase == CheckPhase.Warning) ? (0.07f + 0.09f * (0.5f + 0.5f * Mathf.Sin(num * 13f))) : 0.045f);
			dial.Visibility = Mathf.Clamp01(num / 0.14f);
		}
	}

	public void Cancel()
	{
		Reset();
	}
}

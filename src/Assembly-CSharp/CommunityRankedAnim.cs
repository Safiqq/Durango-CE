using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using UnityEngine;

public static class CommunityRankedAnim
{
	public enum War
	{
		Victory,
		Defeat,
		Start
	}

	public sealed class Anim
	{
		private readonly List<UITweener> _tweens = new List<UITweener>();

		private readonly List<Vector3> _delta = new List<Vector3>();

		private readonly List<Vector3> _rest = new List<Vector3>();

		private readonly List<bool> _relative = new List<bool>();

		public bool Empty => _tweens.Count == 0;

		public float Length
		{
			get
			{
				float num = 0f;
				foreach (UITweener tween in _tweens)
				{
					if (tween != null && tween.style == UITweener.Style.Once)
					{
						num = Mathf.Max(num, tween.delay + tween.duration);
					}
				}
				return num;
			}
		}

		internal void Add(UITweener tween, Vector3 delta, bool relative = true)
		{
			tween.enabled = false;
			_tweens.Add(tween);
			_delta.Add(delta);
			_rest.Add(tween.transform.localPosition);
			_relative.Add(relative);
		}

		public Anim With(Anim other)
		{
			if (other != null && other != this)
			{
				for (int i = 0; i < other._tweens.Count; i++)
				{
					_tweens.Add(other._tweens[i]);
					_delta.Add(other._delta[i]);
					_rest.Add(other._rest[i]);
					_relative.Add(other._relative[i]);
				}
			}
			return this;
		}

		public void Play()
		{
			for (int i = 0; i < _tweens.Count; i++)
			{
				UITweener uITweener = _tweens[i];
				if (uITweener == null)
				{
					continue;
				}
				try
				{
					if (uITweener is TweenPosition tweenPosition && _relative[i])
					{
						if (!uITweener.enabled)
						{
							_rest[i] = uITweener.transform.localPosition;
						}
						tweenPosition.to = _rest[i];
						tweenPosition.from = _rest[i] + _delta[i];
					}
					else if (uITweener is CommunityFade communityFade && !uITweener.enabled)
					{
						communityFade.CaptureRest();
					}
					uITweener.ResetToBeginning();
					uITweener.PlayForward();
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
		}

		public void Finish()
		{
			for (int i = 0; i < _tweens.Count; i++)
			{
				UITweener uITweener = _tweens[i];
				if (!(uITweener == null) && uITweener.enabled)
				{
					uITweener.enabled = false;
					if (uITweener.style == UITweener.Style.Once)
					{
						uITweener.tweenFactor = 1f;
						uITweener.Sample(1f, isFinished: true);
					}
					else if (uITweener is CommunityFade communityFade)
					{
						communityFade.Restore();
					}
				}
			}
		}
	}

	private sealed class Banner
	{
		public GameObject Go;

		public TweenerPlayer Player;

		public UILabel Subject;

		public UILabel Comment;

		public string Sound;

		public float Length;
	}

	public static bool Sound = true;

	private static bool _init;

	private static readonly GameObject[] _warPrefab = new GameObject[3];

	private static readonly string[] _warSound = new string[3];

	private static readonly Banner[] _banners = new Banner[3];

	private static Transform _chapter;

	private static string _chapterSound;

	private static readonly HashSet<string> SkipFields = new HashSet<string> { "onFinished", "eventReceiver", "callWhenFinished" };

	private static void Init()
	{
		if (_init)
		{
			return;
		}
		try
		{
			AlarmWar alarmWar = ((UIManager.Alarm != null) ? UIManager.Alarm.War : null);
			if (alarmWar == null)
			{
				return;
			}
			FieldInfo field = typeof(AlarmWar).GetField("_effectPrefabs", BindingFlags.Instance | BindingFlags.NonPublic);
			Array array = ((field != null) ? (field.GetValue(alarmWar) as Array) : null);
			for (int i = 0; i < 3; i++)
			{
				if (array == null)
				{
					break;
				}
				if (i >= array.Length)
				{
					break;
				}
				object value = array.GetValue(i);
				Type type = value.GetType();
				Component component = type.GetField("Obj")?.GetValue(value) as Component;
				_warPrefab[i] = ((component != null) ? component.gameObject : null);
				object obj = type.GetField("Sound")?.GetValue(value);
				_warSound[i] = ((obj is SoundEventType soundEventType) ? soundEventType.Path : null);
			}
			ChapterGroup chapterGroup = UIManager.FindScript<ChapterGroup>();
			_chapter = ((chapterGroup != null) ? chapterGroup.transform : null);
			Transform transform = ((_chapter != null) ? _chapter.Find("ChapterEffect00") : null);
			ChapterEffect chapterEffect = ((transform != null) ? transform.GetComponent<ChapterEffect>() : null);
			FieldInfo field2 = typeof(ChapterEffect).GetField("_sound", BindingFlags.Instance | BindingFlags.NonPublic);
			if (chapterEffect != null && field2 != null && field2.GetValue(chapterEffect) is SoundEventType soundEventType2)
			{
				_chapterSound = soundEventType2.Path;
			}
			_init = true;
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			_init = true;
		}
	}

	public static Transform WarPart(War war, string path)
	{
		Init();
		GameObject gameObject = _warPrefab[(int)war];
		if (gameObject == null)
		{
			return null;
		}
		if (!string.IsNullOrEmpty(path))
		{
			return gameObject.transform.Find(path);
		}
		return gameObject.transform;
	}

	public static Transform ChapterPart(string path)
	{
		Init();
		if (!(_chapter != null))
		{
			return null;
		}
		return _chapter.Find(path);
	}

	public static void PlayChapterSound()
	{
		Init();
		if (Sound && !string.IsNullOrEmpty(_chapterSound))
		{
			SoundManager.PlayEvent(_chapterSound);
		}
	}

	public static Anim Own(GameObject root)
	{
		Anim anim = new Anim();
		if (root == null)
		{
			return anim;
		}
		UITweener[] componentsInChildren = root.GetComponentsInChildren<UITweener>(includeInactive: true);
		foreach (UITweener uITweener in componentsInChildren)
		{
			if (uITweener.tweenGroup != 0)
			{
				uITweener.enabled = false;
			}
			else if (uITweener.gameObject == root && uITweener is TweenAlpha tweenAlpha && tweenAlpha.to < tweenAlpha.from)
			{
				UnityEngine.Object.DestroyImmediate(uITweener);
			}
			else
			{
				anim.Add(uITweener, Vector3.zero, relative: false);
			}
		}
		return anim;
	}

	public static Anim Copy(Transform src, GameObject dst, bool scaleOnly = false, bool noFade = false)
	{
		Anim anim = new Anim();
		if (src == null || dst == null)
		{
			return anim;
		}
		UITweener[] components = src.GetComponents<UITweener>();
		foreach (UITweener uITweener in components)
		{
			try
			{
				if (uITweener.tweenGroup != 0)
				{
					continue;
				}
				if (uITweener is TweenAlpha tweenAlpha)
				{
					if (!(scaleOnly || noFade) && !(tweenAlpha.to < tweenAlpha.from))
					{
						CommunityFade tween = AddFade(dst, tweenAlpha.from, tweenAlpha.to, uITweener);
						anim.Add(tween, Vector3.zero);
					}
				}
				else if (!scaleOnly || uITweener is TweenScale)
				{
					UITweener uITweener2 = (UITweener)dst.AddComponent(uITweener.GetType());
					CopyFields(uITweener, uITweener2);
					Vector3 delta = ((uITweener is TweenPosition tweenPosition) ? (tweenPosition.from - tweenPosition.to) : Vector3.zero);
					anim.Add(uITweener2, delta);
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
		return anim;
	}

	private static void CopyFields(UITweener src, UITweener dst)
	{
		FieldInfo[] fields = src.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!SkipFields.Contains(fieldInfo.Name))
			{
				object obj = fieldInfo.GetValue(src);
				if (obj is AnimationCurve curve)
				{
					obj = CopyCurve(curve);
				}
				fieldInfo.SetValue(dst, obj);
			}
		}
	}

	private static AnimationCurve CopyCurve(AnimationCurve curve)
	{
		return new AnimationCurve(curve.keys)
		{
			preWrapMode = curve.preWrapMode,
			postWrapMode = curve.postWrapMode
		};
	}

	private static CommunityFade AddFade(GameObject dst, float from, float to, UITweener timing)
	{
		CommunityFade communityFade = dst.AddComponent<CommunityFade>();
		communityFade.from = from;
		communityFade.to = to;
		if (timing != null)
		{
			communityFade.method = timing.method;
			communityFade.style = timing.style;
			communityFade.animationCurve = CopyCurve(timing.animationCurve);
			communityFade.delay = timing.delay;
			communityFade.duration = timing.duration;
			communityFade.steeperCurves = timing.steeperCurves;
		}
		return communityFade;
	}

	public static Anim SoftOpen(GameObject dst)
	{
		Anim anim = new Anim();
		if (dst == null)
		{
			return anim;
		}
		CommunityFade communityFade = AddFade(dst, 0f, 1f, null);
		communityFade.duration = 0.3f;
		anim.Add(communityFade, Vector3.zero);
		return anim;
	}

	public static Anim Selector(GameObject dst)
	{
		Anim anim = new Anim();
		if (dst == null)
		{
			return anim;
		}
		TweenScale tweenScale = dst.AddComponent<TweenScale>();
		tweenScale.from = new Vector3(0.85f, 0.85f, 0.85f);
		tweenScale.to = Vector3.one;
		tweenScale.duration = 0.3f;
		tweenScale.animationCurve = new AnimationCurve(new Keyframe(0f, 0f, 2f, 2f), new Keyframe(1f, 1f, 0f, 0f));
		anim.Add(tweenScale, Vector3.zero);
		return anim;
	}

	public static Anim Pulse(GameObject dst)
	{
		Anim anim = new Anim();
		if (dst == null)
		{
			return anim;
		}
		CommunityFade communityFade = AddFade(dst, 0.7f, 1f, null);
		communityFade.duration = 0.5f;
		communityFade.style = UITweener.Style.PingPong;
		communityFade.animationCurve = new AnimationCurve(new Keyframe(-0.0010345081f, 0.39591494f, 0f, 0f), new Keyframe(0.6714657f, 0.7769536f, 1.7520734f, 1.7520734f), new Keyframe(1f, 1f, 0f, 0f));
		anim.Add(communityFade, Vector3.zero);
		return anim;
	}

	public static void Tint(UIWidget w, Color c)
	{
		if (!(w == null))
		{
			CommunityFade component = w.GetComponent<CommunityFade>();
			if (component != null && component.enabled)
			{
				component.rest = c.a;
				c.a = w.color.a;
			}
			w.color = c;
		}
	}

	public static float ShowWar(War war, GameObject parent, string subject, string comment, Action done = null)
	{
		Init();
		Banner banner = _banners[(int)war];
		if (banner == null || banner.Go == null)
		{
			GameObject gameObject = _warPrefab[(int)war];
			if (gameObject == null || parent == null)
			{
				return 0f;
			}
			banner = new Banner();
			banner.Go = parent.AddChild(gameObject);
			banner.Go.name = "War" + war;
			NGUITools.SetLayer(banner.Go, parent.layer);
			banner.Player = banner.Go.GetComponent<TweenerPlayer>();
			banner.Subject = Find<UILabel>(banner.Go.transform, "Label_Subject");
			banner.Comment = Find<UILabel>(banner.Go.transform, "Label_Comment");
			banner.Sound = _warSound[(int)war];
			if (banner.Player != null)
			{
				foreach (UITweener tweener in banner.Player.GetTweeners())
				{
					banner.Length = Mathf.Max(banner.Length, tweener.delay + tweener.duration);
				}
			}
			banner.Go.SetActive(value: false);
			_banners[(int)war] = banner;
		}
		for (int i = 0; i < _banners.Length; i++)
		{
			Banner banner2 = _banners[i];
			if (banner2 != null && banner2 != banner && banner2.Go != null && banner2.Go.activeSelf)
			{
				if (banner2.Player != null)
				{
					banner2.Player.Stop();
				}
				banner2.Go.SetActive(value: false);
			}
		}
		if (banner.Subject != null)
		{
			banner.Subject.text = subject ?? string.Empty;
		}
		if (banner.Comment != null)
		{
			banner.Comment.text = comment ?? string.Empty;
		}
		banner.Go.SetActive(value: true);
		Banner captured = banner;
		if (banner.Player != null)
		{
			banner.Player.Play(delegate
			{
				if (captured.Go != null)
				{
					captured.Go.SetActive(value: false);
				}
				done?.Invoke();
			});
		}
		if (Sound && !string.IsNullOrEmpty(banner.Sound))
		{
			SoundManager.PlayEvent(banner.Sound);
		}
		return banner.Length;
	}

	public static void HideWar()
	{
		Banner[] banners = _banners;
		foreach (Banner banner in banners)
		{
			if (banner != null && banner.Go != null && banner.Go.activeSelf)
			{
				if (banner.Player != null)
				{
					banner.Player.Stop();
				}
				banner.Go.SetActive(value: false);
			}
		}
	}

	private static T Find<T>(Transform root, string path) where T : Component
	{
		Transform transform = ((root != null) ? root.Find(path) : null);
		if (!(transform != null))
		{
			return null;
		}
		return transform.GetComponent<T>();
	}

	public static string Describe()
	{
		Init();
		return "anim war " + (_warPrefab[0] != null) + "/" + (_warPrefab[1] != null) + "/" + (_warPrefab[2] != null) + " chapter " + (_chapter != null) + " sound " + Sound;
	}
}

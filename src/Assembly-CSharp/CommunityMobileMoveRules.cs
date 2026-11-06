public static class CommunityMobileMoveRules
{
	public sealed class DragState
	{
		public enum Step
		{
			None,
			Drag,
			Release,
			Cancel
		}

		private bool _active;

		private int _touchId;

		public bool Active => _active;

		public int TouchId => _touchId;

		public void Reset()
		{
			_active = false;
			_touchId = 0;
		}

		public Step Next(bool hasTouch, int touchId, bool touching, float movedSq, float thresholdSq, bool multiTouch)
		{
			if (multiTouch || !hasTouch)
			{
				if (_active)
				{
					Reset();
					return Step.Cancel;
				}
				return Step.None;
			}
			if (_active && touchId != _touchId)
			{
				Reset();
			}
			if (touching)
			{
				if (_active)
				{
					return Step.Drag;
				}
				if (movedSq > thresholdSq)
				{
					_active = true;
					_touchId = touchId;
					return Step.Drag;
				}
				return Step.None;
			}
			if (_active)
			{
				Reset();
				return Step.Release;
			}
			if (movedSq > thresholdSq)
			{
				return Step.Release;
			}
			return Step.None;
		}
	}

	public const string Click = "click";

	public const string Stick = "stick";

	public const string Both = "both";

	public static readonly string[] Options = new string[3] { "click", "stick", "both" };

	public static string Normalize(string value)
	{
		if (value != null)
		{
			string text = value.Trim().ToLowerInvariant();
			if (text == "click" || text == "stick")
			{
				return text;
			}
		}
		return "both";
	}

	public static string Resolve(string selected, bool usePCUI, bool prologue, bool drawMode)
	{
		if (usePCUI || prologue || drawMode)
		{
			return "both";
		}
		return Normalize(selected);
	}

	public static bool StickEnabled(string mode)
	{
		return mode != "click";
	}

	public static bool TapWalkEnabled(string mode, bool mouseFinger)
	{
		if (mode == "stick")
		{
			return false;
		}
		if (mode == "click")
		{
			return !mouseFinger;
		}
		return true;
	}

	public static bool DragWalkEnabled(string mode, bool mouseFinger)
	{
		if (mode == "click")
		{
			return !mouseFinger;
		}
		return false;
	}

	public static bool MouseButtonWalkEnabled(string mode)
	{
		return mode != "stick";
	}

	public static string Label(string lang)
	{
		return lang switch
		{
			"th" => "การเด\u0e34น (ม\u0e37อถ\u0e37อ)",
			"id" => "Cara berjalan (ponsel)",
			"ko" => "이동 방식 (모바일)",
			_ => "Movement (mobile)",
		};
	}

	public static string OptionLabel(string lang, string value)
	{
		string text = Normalize(value);
		if (!(text == "click"))
		{
			if (text == "stick")
			{
				return lang switch
				{
					"th" => "จอยสต\u0e34\u0e4aก",
					"id" => "Joystik",
					"ko" => "조이스틱",
					_ => "Joystick",
				};
			}
			return lang switch
			{
				"th" => "ท\u0e31\u0e49งสอง",
				"id" => "Keduanya",
				"ko" => "둘 다",
				_ => "Both",
			};
		}
		return lang switch
		{
			"th" => "คล\u0e34กเด\u0e34น",
			"id" => "Ketuk untuk jalan",
			"ko" => "터치 이동",
			_ => "Tap to walk",
		};
	}
}

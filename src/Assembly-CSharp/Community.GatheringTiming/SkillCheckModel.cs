using System;

namespace Community.GatheringTiming;

public sealed class SkillCheckModel
{
	public const double WarningSeconds = 0.65;

	public const double GoodHalfWidth = 25.0;

	public const double GreatHalfWidth = 4.5;

	private double startedAt;

	public CheckPhase Phase { get; private set; }

	public CheckResult Result { get; private set; }

	public double TargetDegrees { get; private set; }

	public double GoodHalf { get; private set; } = 25.0;

	public double GreatHalf { get; private set; } = 4.5;

	public double GreatTargetDegrees => Normalize(TargetDegrees + GoodHalf - GreatHalf);

	public double SpinSeconds { get; private set; } = 1.8;

	public double AngleDegrees { get; private set; } = 90.0;

	public double LastError { get; private set; }

	public double SpinStartedAt { get; private set; }

	public double FinishedAt { get; private set; }

	public int Rounds { get; private set; }

	public int Good { get; private set; }

	public int Great { get; private set; }

	public int Miss { get; private set; }

	public void Prepare()
	{
		Phase = CheckPhase.Ready;
		Result = CheckResult.None;
		AngleDegrees = 90.0;
		LastError = 0.0;
	}

	public static double Normalize(double angle)
	{
		return (angle % 360.0 + 360.0) % 360.0;
	}

	public static double Distance(double a, double b)
	{
		double num = Normalize(a - b);
		return Math.Min(num, 360.0 - num);
	}

	public void Start(double now, double targetDegrees, double seconds, double goodHalf = 25.0, double greatHalf = 4.5)
	{
		GoodHalf = goodHalf;
		GreatHalf = Math.Min(greatHalf, goodHalf);
		if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(targetDegrees) || double.IsInfinity(targetDegrees) || seconds < 0.5 || seconds > 10.0 || double.IsNaN(seconds))
		{
			throw new ArgumentOutOfRangeException();
		}
		startedAt = now;
		SpinStartedAt = now + 0.65;
		TargetDegrees = Normalize(targetDegrees);
		SpinSeconds = seconds;
		AngleDegrees = 90.0;
		LastError = 0.0;
		Result = CheckResult.None;
		Phase = CheckPhase.Warning;
	}

	public void Tick(double now)
	{
		if (Phase == CheckPhase.Warning && now >= SpinStartedAt)
		{
			Phase = CheckPhase.Spinning;
		}
		if (Phase == CheckPhase.Spinning)
		{
			AngleDegrees = Normalize(90.0 - Math.Max(0.0, now - SpinStartedAt) / SpinSeconds * 360.0);
			if (now >= SpinStartedAt + SpinSeconds)
			{
				Finish(CheckResult.Miss, now);
			}
		}
	}

	public bool Press(double now)
	{
		Tick(now);
		if (Phase == CheckPhase.Warning)
		{
			Finish(CheckResult.Early, now);
			return true;
		}
		if (Phase != CheckPhase.Spinning)
		{
			return false;
		}
		LastError = Distance(AngleDegrees, TargetDegrees);
		Finish((Distance(AngleDegrees, GreatTargetDegrees) <= GreatHalf + 1E-07) ? CheckResult.Great : ((LastError <= GoodHalf + 1E-07) ? CheckResult.Good : CheckResult.Miss), now);
		return true;
	}

	public void Cancel(double now)
	{
		if (Phase == CheckPhase.Warning || Phase == CheckPhase.Spinning)
		{
			Finish(CheckResult.Cancelled, now);
		}
	}

	private void Finish(CheckResult result, double now)
	{
		Result = result;
		FinishedAt = now;
		Phase = CheckPhase.Result;
		if (result != CheckResult.Cancelled)
		{
			Rounds++;
			switch (result)
			{
			case CheckResult.Great:
				Great++;
				break;
			case CheckResult.Good:
				Good++;
				break;
			default:
				Miss++;
				break;
			}
		}
	}
}

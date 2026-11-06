using System;

namespace DurangoLauncher;

public sealed class CouponState
{
	public enum Btn
	{
		Use,
		Used,
		Busy,
		Wait,
		Off
	}

	private bool _used;

	public string Text { get; private set; } = "";

	public CouponCode.Result Parsed { get; private set; } = new CouponCode.Result(CouponCode.Kind.Empty);

	public CouponResult? Ok { get; private set; }

	public ApiError? Err { get; private set; }

	public bool Busy { get; private set; }

	public DateTime RetryAtUtc { get; private set; }

	public bool ShowMove
	{
		get
		{
			if (Parsed.Kind != CouponCode.Kind.Move)
			{
				return Err?.Code == "move_code";
			}
			return true;
		}
	}

	public bool SetText(string? text)
	{
		string text2 = text ?? "";
		if (text2 == Text)
		{
			return false;
		}
		Text = text2;
		CouponCode.Result result = CouponCode.Classify(text2);
		if (result == Parsed && Ok == null && Err == null)
		{
			return false;
		}
		Parsed = result;
		Ok = null;
		Err = null;
		_used = false;
		return true;
	}

	public void Reset()
	{
		Text = "";
		Parsed = new CouponCode.Result(CouponCode.Kind.Empty);
		Ok = null;
		Err = null;
		Busy = false;
		_used = false;
		RetryAtUtc = default(DateTime);
	}

	public int RetryLeft(DateTime nowUtc)
	{
		if (!(nowUtc >= RetryAtUtc))
		{
			return (int)Math.Ceiling((RetryAtUtc - nowUtc).TotalSeconds);
		}
		return 0;
	}

	public Btn Button(DateTime nowUtc, bool hasReceiver)
	{
		if (Busy)
		{
			return Btn.Busy;
		}
		if (_used)
		{
			return Btn.Used;
		}
		if (RetryLeft(nowUtc) > 0)
		{
			return Btn.Wait;
		}
		if (!(Parsed.CanSend && hasReceiver))
		{
			return Btn.Off;
		}
		return Btn.Use;
	}

	public bool CanSubmit(DateTime nowUtc, bool hasReceiver)
	{
		return Button(nowUtc, hasReceiver) == Btn.Use;
	}

	public void Begin()
	{
		Busy = true;
		Ok = null;
		Err = null;
	}

	public void End()
	{
		Busy = false;
	}

	public void Success(CouponResult r)
	{
		Ok = r;
		Err = null;
		_used = true;
	}

	public void Fail(ApiError e, DateTime nowUtc)
	{
		Ok = null;
		Err = e;
		if (e.Code == "rate_limited")
		{
			RetryAtUtc = nowUtc.AddSeconds(Math.Clamp(e.RetryAfter ?? 12, 1, 300));
		}
	}
}

using System;
using System.Diagnostics;
using System.Threading;

namespace Sanford.Multimedia.Midi;

public abstract class PpqnClock : IClock
{
	public const int DefaultTempo = 500000;

	public const int PpqnMinValue = 24;

	private const int MicrosecondsPerMillisecond = 1000;

	private int ppqn = 24;

	private int tempo = 500000;

	private int periodResolution;

	private int ticksPerClock;

	private int fractionalTicks;

	private readonly int timerPeriod;

	protected bool running;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler Tick__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler Started__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler Continued__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler Stopped__BackingField;

	public int Ppqn
	{
		get
		{
			return ppqn;
		}
		set
		{
			if (value < 24)
			{
				throw new ArgumentOutOfRangeException("Ppqn", value, "Pulses per quarter note out of range.");
			}
			if (value % 24 != 0)
			{
				throw new ArgumentException("Pulses per quarter note is not a multiple of 24.");
			}
			ppqn = value;
			CalculatePeriodResolution();
			CalculateTicksPerClock();
		}
	}

	public abstract int Ticks { get; }

	public int TicksPerClock => ticksPerClock;

	public bool IsRunning => running;

	public event EventHandler Tick
	{
		add
		{
			EventHandler eventHandler = Tick__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Tick__BackingField, (EventHandler)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler eventHandler = Tick__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Tick__BackingField, (EventHandler)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler Started
	{
		add
		{
			EventHandler eventHandler = Started__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Started__BackingField, (EventHandler)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler eventHandler = Started__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Started__BackingField, (EventHandler)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler Continued
	{
		add
		{
			EventHandler eventHandler = Continued__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Continued__BackingField, (EventHandler)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler eventHandler = Continued__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Continued__BackingField, (EventHandler)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler Stopped
	{
		add
		{
			EventHandler eventHandler = Stopped__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Stopped__BackingField, (EventHandler)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler eventHandler = Stopped__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref Stopped__BackingField, (EventHandler)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	protected PpqnClock(int timerPeriod)
	{
		if (timerPeriod < 1)
		{
			throw new ArgumentOutOfRangeException("timerPeriod", timerPeriod, "Timer period cannot be less than one.");
		}
		this.timerPeriod = timerPeriod;
		CalculatePeriodResolution();
		CalculateTicksPerClock();
	}

	protected int GetTempo()
	{
		return tempo;
	}

	protected void SetTempo(int tempo)
	{
		if (tempo < 1)
		{
			throw new ArgumentOutOfRangeException("Tempo out of range.");
		}
		this.tempo = tempo;
	}

	protected void Reset()
	{
		fractionalTicks = 0;
	}

	protected int GenerateTicks()
	{
		int num = (fractionalTicks + periodResolution) / tempo;
		fractionalTicks += periodResolution - num * tempo;
		return num;
	}

	private void CalculatePeriodResolution()
	{
		periodResolution = ppqn * timerPeriod * 1000;
	}

	private void CalculateTicksPerClock()
	{
		ticksPerClock = ppqn / 24;
	}

	protected virtual void OnTick(EventArgs e)
	{
		Tick__BackingField?.Invoke(this, EventArgs.Empty);
	}

	protected virtual void OnStarted(EventArgs e)
	{
		Started__BackingField?.Invoke(this, e);
	}

	protected virtual void OnStopped(EventArgs e)
	{
		Stopped__BackingField?.Invoke(this, e);
	}

	protected virtual void OnContinued(EventArgs e)
	{
		Continued__BackingField?.Invoke(this, e);
	}
}

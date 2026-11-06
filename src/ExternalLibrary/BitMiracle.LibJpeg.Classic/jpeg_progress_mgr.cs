using System;
using System.Diagnostics;
using System.Threading;

namespace BitMiracle.LibJpeg.Classic;

public class jpeg_progress_mgr
{
	private int m_passCounter;

	private int m_passLimit;

	private int m_completedPasses;

	private int m_totalPasses;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler OnProgress__BackingField;

	public int Pass_counter
	{
		get
		{
			return m_passCounter;
		}
		set
		{
			m_passCounter = value;
		}
	}

	public int Pass_limit
	{
		get
		{
			return m_passLimit;
		}
		set
		{
			m_passLimit = value;
		}
	}

	public int Completed_passes
	{
		get
		{
			return m_completedPasses;
		}
		set
		{
			m_completedPasses = value;
		}
	}

	public int Total_passes
	{
		get
		{
			return m_totalPasses;
		}
		set
		{
			m_totalPasses = value;
		}
	}

	public event EventHandler OnProgress
	{
		add
		{
			EventHandler eventHandler = OnProgress__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref OnProgress__BackingField, (EventHandler)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler eventHandler = OnProgress__BackingField;
			EventHandler eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref OnProgress__BackingField, (EventHandler)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public void Updated()
	{
		OnProgress__BackingField?.Invoke(this, new EventArgs());
	}
}

using System;
using System.Diagnostics;
using System.Threading;

namespace Sanford.Multimedia.Midi;

public class MessageDispatcher
{
	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler<ChannelMessageEventArgs> ChannelMessageDispatched__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler<SysExMessageEventArgs> SysExMessageDispatched__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler<SysCommonMessageEventArgs> SysCommonMessageDispatched__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler<SysRealtimeMessageEventArgs> SysRealtimeMessageDispatched__BackingField;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private EventHandler<MetaMessageEventArgs> MetaMessageDispatched__BackingField;

	public event EventHandler<ChannelMessageEventArgs> ChannelMessageDispatched
	{
		add
		{
			EventHandler<ChannelMessageEventArgs> eventHandler = ChannelMessageDispatched__BackingField;
			EventHandler<ChannelMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref ChannelMessageDispatched__BackingField, (EventHandler<ChannelMessageEventArgs>)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler<ChannelMessageEventArgs> eventHandler = ChannelMessageDispatched__BackingField;
			EventHandler<ChannelMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref ChannelMessageDispatched__BackingField, (EventHandler<ChannelMessageEventArgs>)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler<SysExMessageEventArgs> SysExMessageDispatched
	{
		add
		{
			EventHandler<SysExMessageEventArgs> eventHandler = SysExMessageDispatched__BackingField;
			EventHandler<SysExMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref SysExMessageDispatched__BackingField, (EventHandler<SysExMessageEventArgs>)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler<SysExMessageEventArgs> eventHandler = SysExMessageDispatched__BackingField;
			EventHandler<SysExMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref SysExMessageDispatched__BackingField, (EventHandler<SysExMessageEventArgs>)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler<SysCommonMessageEventArgs> SysCommonMessageDispatched
	{
		add
		{
			EventHandler<SysCommonMessageEventArgs> eventHandler = SysCommonMessageDispatched__BackingField;
			EventHandler<SysCommonMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref SysCommonMessageDispatched__BackingField, (EventHandler<SysCommonMessageEventArgs>)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler<SysCommonMessageEventArgs> eventHandler = SysCommonMessageDispatched__BackingField;
			EventHandler<SysCommonMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref SysCommonMessageDispatched__BackingField, (EventHandler<SysCommonMessageEventArgs>)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler<SysRealtimeMessageEventArgs> SysRealtimeMessageDispatched
	{
		add
		{
			EventHandler<SysRealtimeMessageEventArgs> eventHandler = SysRealtimeMessageDispatched__BackingField;
			EventHandler<SysRealtimeMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref SysRealtimeMessageDispatched__BackingField, (EventHandler<SysRealtimeMessageEventArgs>)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler<SysRealtimeMessageEventArgs> eventHandler = SysRealtimeMessageDispatched__BackingField;
			EventHandler<SysRealtimeMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref SysRealtimeMessageDispatched__BackingField, (EventHandler<SysRealtimeMessageEventArgs>)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public event EventHandler<MetaMessageEventArgs> MetaMessageDispatched
	{
		add
		{
			EventHandler<MetaMessageEventArgs> eventHandler = MetaMessageDispatched__BackingField;
			EventHandler<MetaMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref MetaMessageDispatched__BackingField, (EventHandler<MetaMessageEventArgs>)Delegate.Combine(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
		remove
		{
			EventHandler<MetaMessageEventArgs> eventHandler = MetaMessageDispatched__BackingField;
			EventHandler<MetaMessageEventArgs> eventHandler2;
			do
			{
				eventHandler2 = eventHandler;
				eventHandler = Interlocked.CompareExchange(ref MetaMessageDispatched__BackingField, (EventHandler<MetaMessageEventArgs>)Delegate.Remove(eventHandler2, value), eventHandler);
			}
			while ((object)eventHandler != eventHandler2);
		}
	}

	public void Dispatch(IMidiMessage message)
	{
		if (message == null)
		{
			throw new ArgumentNullException("message");
		}
		switch (message.MessageType)
		{
		case MessageType.Channel:
			OnChannelMessageDispatched(new ChannelMessageEventArgs((ChannelMessage)message));
			break;
		case MessageType.SystemExclusive:
			OnSysExMessageDispatched(new SysExMessageEventArgs((SysExMessage)message));
			break;
		case MessageType.Meta:
			OnMetaMessageDispatched(new MetaMessageEventArgs((MetaMessage)message));
			break;
		case MessageType.SystemCommon:
			OnSysCommonMessageDispatched(new SysCommonMessageEventArgs((SysCommonMessage)message));
			break;
		case MessageType.SystemRealtime:
			switch (((SysRealtimeMessage)message).SysRealtimeType)
			{
			case SysRealtimeType.ActiveSense:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.ActiveSense);
				break;
			case SysRealtimeType.Clock:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.Clock);
				break;
			case SysRealtimeType.Continue:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.Continue);
				break;
			case SysRealtimeType.Reset:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.Reset);
				break;
			case SysRealtimeType.Start:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.Start);
				break;
			case SysRealtimeType.Stop:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.Stop);
				break;
			case SysRealtimeType.Tick:
				OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs.Tick);
				break;
			case (SysRealtimeType)253:
				break;
			}
			break;
		}
	}

	protected virtual void OnChannelMessageDispatched(ChannelMessageEventArgs e)
	{
		ChannelMessageDispatched__BackingField?.Invoke(this, e);
	}

	protected virtual void OnSysExMessageDispatched(SysExMessageEventArgs e)
	{
		SysExMessageDispatched__BackingField?.Invoke(this, e);
	}

	protected virtual void OnSysCommonMessageDispatched(SysCommonMessageEventArgs e)
	{
		SysCommonMessageDispatched__BackingField?.Invoke(this, e);
	}

	protected virtual void OnSysRealtimeMessageDispatched(SysRealtimeMessageEventArgs e)
	{
		SysRealtimeMessageDispatched__BackingField?.Invoke(this, e);
	}

	protected virtual void OnMetaMessageDispatched(MetaMessageEventArgs e)
	{
		MetaMessageDispatched__BackingField?.Invoke(this, e);
	}
}

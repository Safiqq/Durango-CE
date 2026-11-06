using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Sanford.Multimedia.Midi;

public sealed class Track
{
	[CompilerGenerated]
	private sealed class _003CDispatcherIterator_003Ec__Iterator1 : IEnumerable, IEnumerable<int>, IEnumerator, IDisposable, IEnumerator<int>
	{
		internal IEnumerator<MidiEvent> _003Cenumerator_003E__0;

		internal MessageDispatcher dispatcher;

		internal Track _0024this;

		internal int _0024current;

		internal bool _0024disposing;

		internal int _0024PC;

		int IEnumerator<int>.Current
		{
			[DebuggerHidden]
			get
			{
				return System_002ECollections_002EGeneric_002EIEnumerator_003Cint_003E_002Eget_Current();
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _0024current;
			}
		}

		[DebuggerHidden]
		public _003CDispatcherIterator_003Ec__Iterator1()
		{
		}

		public bool MoveNext()
		{
			uint num = (uint)_0024PC;
			_0024PC = -1;
			switch (num)
			{
			case 0u:
				_003Cenumerator_003E__0 = _0024this.Iterator().GetEnumerator();
				goto IL_0081;
			case 1u:
				dispatcher.Dispatch(_003Cenumerator_003E__0.Current.MidiMessage);
				goto IL_0081;
			default:
				{
					return false;
				}
				IL_0081:
				if (_003Cenumerator_003E__0.MoveNext())
				{
					_0024current = _003Cenumerator_003E__0.Current.AbsoluteTicks;
					if (!_0024disposing)
					{
						_0024PC = 1;
					}
					break;
				}
				_0024PC = -1;
				goto default;
			}
			return true;
		}

		[DebuggerHidden]
		private int System_002ECollections_002EGeneric_002EIEnumerator_003Cint_003E_002Eget_Current()
		{
			return _0024current;
		}

		[DebuggerHidden]
		public void Dispose()
		{
			_0024disposing = true;
			_0024PC = -1;
		}

		[DebuggerHidden]
		public void Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<int>)this).GetEnumerator();
		}

		[DebuggerHidden]
		IEnumerator<int> IEnumerable<int>.GetEnumerator()
		{
			if (Interlocked.CompareExchange(ref _0024PC, 0, -2) == -2)
			{
				return this;
			}
			return new _003CDispatcherIterator_003Ec__Iterator1
			{
				_0024this = _0024this,
				dispatcher = dispatcher
			};
		}
	}

	[CompilerGenerated]
	private sealed class _003CTickIterator_003Ec__Iterator2 : IEnumerable, IEnumerable<int>, IEnumerator, IDisposable, IEnumerator<int>
	{
		internal int startPosition;

		internal IEnumerator<MidiEvent> _003Cenumerator_003E__0;

		internal bool _003CnotFinished_003E__0;

		internal IMidiMessage _003Cmessage_003E__1;

		internal ChannelChaser chaser;

		internal MessageDispatcher dispatcher;

		internal int _003Cticks_003E__0;

		internal Track _0024this;

		internal int _0024current;

		internal bool _0024disposing;

		internal int _0024PC;

		int IEnumerator<int>.Current
		{
			[DebuggerHidden]
			get
			{
				return System_002ECollections_002EGeneric_002EIEnumerator_003Cint_003E_002Eget_Current();
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _0024current;
			}
		}

		[DebuggerHidden]
		public _003CTickIterator_003Ec__Iterator2()
		{
		}

		public bool MoveNext()
		{
			uint num = (uint)_0024PC;
			_0024PC = -1;
			switch (num)
			{
			case 0u:
				if (startPosition < 0)
				{
					throw new ArgumentOutOfRangeException("startPosition", startPosition, "Start position out of range.");
				}
				_003Cenumerator_003E__0 = _0024this.Iterator().GetEnumerator();
				for (_003CnotFinished_003E__0 = _003Cenumerator_003E__0.MoveNext(); _003CnotFinished_003E__0 && _003Cenumerator_003E__0.Current.AbsoluteTicks < startPosition; _003CnotFinished_003E__0 = _003Cenumerator_003E__0.MoveNext())
				{
					_003Cmessage_003E__1 = _003Cenumerator_003E__0.Current.MidiMessage;
					if (_003Cmessage_003E__1.MessageType == MessageType.Channel)
					{
						chaser.Process((ChannelMessage)_003Cmessage_003E__1);
					}
					else if (_003Cmessage_003E__1.MessageType == MessageType.Meta)
					{
						dispatcher.Dispatch(_003Cmessage_003E__1);
					}
				}
				chaser.Chase();
				_003Cticks_003E__0 = startPosition;
				goto IL_0201;
			case 1u:
				_003Cticks_003E__0++;
				goto IL_0161;
			case 2u:
				{
					while (_003CnotFinished_003E__0 && _003Cenumerator_003E__0.Current.AbsoluteTicks == _003Cticks_003E__0)
					{
						dispatcher.Dispatch(_003Cenumerator_003E__0.Current.MidiMessage);
						_003CnotFinished_003E__0 = _003Cenumerator_003E__0.MoveNext();
					}
					_003Cticks_003E__0++;
					goto IL_0201;
				}
				IL_0161:
				if (_003Cticks_003E__0 < _003Cenumerator_003E__0.Current.AbsoluteTicks)
				{
					_0024current = _003Cticks_003E__0;
					if (!_0024disposing)
					{
						_0024PC = 1;
					}
				}
				else
				{
					_0024current = _003Cticks_003E__0;
					if (!_0024disposing)
					{
						_0024PC = 2;
					}
				}
				return true;
				IL_0201:
				if (_003CnotFinished_003E__0)
				{
					goto IL_0161;
				}
				_0024PC = -1;
				break;
			}
			return false;
		}

		[DebuggerHidden]
		private int System_002ECollections_002EGeneric_002EIEnumerator_003Cint_003E_002Eget_Current()
		{
			return _0024current;
		}

		[DebuggerHidden]
		public void Dispose()
		{
			_0024disposing = true;
			_0024PC = -1;
		}

		[DebuggerHidden]
		public void Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<int>)this).GetEnumerator();
		}

		[DebuggerHidden]
		IEnumerator<int> IEnumerable<int>.GetEnumerator()
		{
			if (Interlocked.CompareExchange(ref _0024PC, 0, -2) == -2)
			{
				return this;
			}
			return new _003CTickIterator_003Ec__Iterator2
			{
				_0024this = _0024this,
				startPosition = startPosition,
				chaser = chaser,
				dispatcher = dispatcher
			};
		}
	}

	private int count = 1;

	private int endOfTrackOffset;

	private MidiEvent head;

	private MidiEvent tail;

	private MidiEvent endOfTrackMidiEvent;

	public int Count => count;

	public int Length
	{
		get
		{
			int num = EndOfTrackOffset;
			if (tail != null)
			{
				num += tail.AbsoluteTicks;
			}
			return num + 1;
		}
	}

	public int EndOfTrackOffset
	{
		get
		{
			return endOfTrackOffset;
		}
		set
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException("EndOfTrackOffset", value, "End of track offset out of range.");
			}
			endOfTrackOffset = value;
			endOfTrackMidiEvent.SetAbsoluteTicks(Length);
		}
	}

	public object SyncRoot => this;

	public Track()
	{
		endOfTrackMidiEvent = new MidiEvent(this, Length, MetaMessage.EndOfTrackMessage);
	}

	public void Insert(int position, IMidiMessage message)
	{
		if (position < 0)
		{
			throw new ArgumentOutOfRangeException("position", position, "IMidiMessage position out of range.");
		}
		if (message == null)
		{
			throw new ArgumentNullException("message");
		}
		MidiEvent midiEvent = new MidiEvent(this, position, message);
		if (head == null)
		{
			head = midiEvent;
			tail = midiEvent;
		}
		else if (position >= tail.AbsoluteTicks)
		{
			midiEvent.Previous = tail;
			tail.Next = midiEvent;
			tail = midiEvent;
			endOfTrackMidiEvent.SetAbsoluteTicks(Length);
			endOfTrackMidiEvent.Previous = tail;
		}
		else
		{
			MidiEvent next = head;
			while (next.AbsoluteTicks < position)
			{
				next = next.Next;
			}
			midiEvent.Next = next;
			midiEvent.Previous = next.Previous;
			if (next.Previous != null)
			{
				next.Previous.Next = midiEvent;
			}
			else
			{
				head = midiEvent;
			}
			next.Previous = midiEvent;
		}
		count++;
	}

	public void Clear()
	{
		head = (tail = null);
		count = 1;
	}

	public void Merge(Track trk)
	{
		if (trk == null)
		{
			throw new ArgumentNullException("trk");
		}
		if (trk == this || trk.Count == 1)
		{
			return;
		}
		count += trk.Count - 1;
		MidiEvent next = head;
		MidiEvent next2 = trk.head;
		MidiEvent midiEvent = null;
		if (next != null && next.AbsoluteTicks <= next2.AbsoluteTicks)
		{
			midiEvent = new MidiEvent(this, next.AbsoluteTicks, next.MidiMessage);
			next = next.Next;
		}
		else
		{
			midiEvent = new MidiEvent(this, next2.AbsoluteTicks, next2.MidiMessage);
			next2 = next2.Next;
		}
		head = midiEvent;
		while (next != null && next2 != null)
		{
			while (next != null && next.AbsoluteTicks <= next2.AbsoluteTicks)
			{
				midiEvent.Next = new MidiEvent(this, next.AbsoluteTicks, next.MidiMessage);
				midiEvent.Next.Previous = midiEvent;
				midiEvent = midiEvent.Next;
				next = next.Next;
			}
			if (next != null)
			{
				while (next2 != null && next2.AbsoluteTicks <= next.AbsoluteTicks)
				{
					midiEvent.Next = new MidiEvent(this, next2.AbsoluteTicks, next2.MidiMessage);
					midiEvent.Next.Previous = midiEvent;
					midiEvent = midiEvent.Next;
					next2 = next2.Next;
				}
			}
		}
		while (next != null)
		{
			midiEvent.Next = new MidiEvent(this, next.AbsoluteTicks, next.MidiMessage);
			midiEvent.Next.Previous = midiEvent;
			midiEvent = midiEvent.Next;
			next = next.Next;
		}
		while (next2 != null)
		{
			midiEvent.Next = new MidiEvent(this, next2.AbsoluteTicks, next2.MidiMessage);
			midiEvent.Next.Previous = midiEvent;
			midiEvent = midiEvent.Next;
			next2 = next2.Next;
		}
		tail = midiEvent;
		endOfTrackMidiEvent.SetAbsoluteTicks(Length);
		endOfTrackMidiEvent.Previous = tail;
	}

	public void RemoveAt(int index)
	{
		if (index < 0)
		{
			throw new ArgumentOutOfRangeException("index", index, "Track index out of range.");
		}
		if (index == Count - 1)
		{
			throw new ArgumentException("Cannot remove the end of track event.", "index");
		}
		MidiEvent midiEvent = GetMidiEvent(index);
		if (midiEvent.Previous != null)
		{
			midiEvent.Previous.Next = midiEvent.Next;
		}
		else
		{
			head = head.Next;
		}
		if (midiEvent.Next != null)
		{
			midiEvent.Next.Previous = midiEvent.Previous;
		}
		else
		{
			tail = tail.Previous;
			endOfTrackMidiEvent.SetAbsoluteTicks(Length);
			endOfTrackMidiEvent.Previous = tail;
		}
		MidiEvent next = (midiEvent.Previous = null);
		midiEvent.Next = next;
		count--;
	}

	public MidiEvent GetMidiEvent(int index)
	{
		if (index < 0 || index >= Count)
		{
			throw new ArgumentOutOfRangeException("index", index, "Track index out of range.");
		}
		MidiEvent midiEvent;
		if (index == Count - 1)
		{
			midiEvent = endOfTrackMidiEvent;
		}
		else if (index < Count / 2)
		{
			midiEvent = head;
			for (int i = 0; i < index; i++)
			{
				midiEvent = midiEvent.Next;
			}
		}
		else
		{
			midiEvent = tail;
			for (int num = Count - 2; num > index; num--)
			{
				midiEvent = midiEvent.Previous;
			}
		}
		return midiEvent;
	}

	public void Move(MidiEvent e, int newPosition)
	{
		if (e.Owner != this)
		{
			throw new ArgumentException("MidiEvent does not belong to this Track.");
		}
		if (newPosition < 0)
		{
			throw new ArgumentOutOfRangeException("newPosition");
		}
		if (e == endOfTrackMidiEvent)
		{
			throw new InvalidOperationException("Cannot move end of track message. Use the EndOfTrackOffset property instead.");
		}
		MidiEvent midiEvent = e.Previous;
		MidiEvent midiEvent2 = e.Next;
		if (e.Previous != null && e.Previous.AbsoluteTicks > newPosition)
		{
			e.Previous.Next = e.Next;
			if (e.Next != null)
			{
				e.Next.Previous = e.Previous;
			}
			while (midiEvent != null && midiEvent.AbsoluteTicks > newPosition)
			{
				midiEvent2 = midiEvent;
				midiEvent = midiEvent.Previous;
			}
		}
		else if (e.Next != null && e.Next.AbsoluteTicks < newPosition)
		{
			e.Next.Previous = e.Previous;
			if (e.Previous != null)
			{
				e.Previous.Next = e.Next;
			}
			while (midiEvent2 != null && midiEvent2.AbsoluteTicks < newPosition)
			{
				midiEvent = midiEvent2;
				midiEvent2 = midiEvent2.Next;
			}
		}
		if (midiEvent != null)
		{
			midiEvent.Next = e;
		}
		if (midiEvent2 != null)
		{
			midiEvent2.Previous = e;
		}
		e.Previous = midiEvent;
		e.Next = midiEvent2;
		e.SetAbsoluteTicks(newPosition);
		if (newPosition < head.AbsoluteTicks)
		{
			head = e;
		}
		if (newPosition > tail.AbsoluteTicks)
		{
			tail = e;
		}
		endOfTrackMidiEvent.SetAbsoluteTicks(Length);
		endOfTrackMidiEvent.Previous = tail;
	}

	public IEnumerable<MidiEvent> Iterator()
	{
		for (MidiEvent current = head; current != null; current = current.Next)
		{
			yield return current;
		}
		yield return endOfTrackMidiEvent;
	}

	[DebuggerHidden]
	public IEnumerable<int> DispatcherIterator(MessageDispatcher dispatcher)
	{
		//yield-return decompiler failed: Could not find currentField
		_003CDispatcherIterator_003Ec__Iterator1 _003CDispatcherIterator_003Ec__Iterator = new _003CDispatcherIterator_003Ec__Iterator1();
		_003CDispatcherIterator_003Ec__Iterator.dispatcher = dispatcher;
		_003CDispatcherIterator_003Ec__Iterator._0024this = this;
		_003CDispatcherIterator_003Ec__Iterator._0024PC = -2;
		return _003CDispatcherIterator_003Ec__Iterator;
	}

	[DebuggerHidden]
	public IEnumerable<int> TickIterator(int startPosition, ChannelChaser chaser, MessageDispatcher dispatcher)
	{
		//yield-return decompiler failed: Could not find currentField
		_003CTickIterator_003Ec__Iterator2 _003CTickIterator_003Ec__Iterator = new _003CTickIterator_003Ec__Iterator2();
		_003CTickIterator_003Ec__Iterator.startPosition = startPosition;
		_003CTickIterator_003Ec__Iterator.chaser = chaser;
		_003CTickIterator_003Ec__Iterator.dispatcher = dispatcher;
		_003CTickIterator_003Ec__Iterator._0024this = this;
		_003CTickIterator_003Ec__Iterator._0024PC = -2;
		return _003CTickIterator_003Ec__Iterator;
	}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Messages;
using Shared.Laboratory;
using Yaml;
using Yaml.Util;

public static class AvailablePersonalResearchExtension
{
	[CompilerGenerated]
	private sealed class _003CResearchableIds_003Ed__1 : IEnumerable<Pair<string, int?>>, IEnumerable, IEnumerator<Pair<string, int?>>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private Pair<string, int?> _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private AvailablePersonalResearch msg;

		public AvailablePersonalResearch _003C_003E3__msg;

		private string[] _003C_003E7__wrap1;

		private int _003C_003E7__wrap2;

		private Pair<string, int>[] _003CunavailableResearchIds_003E5__4;

		Pair<string, int?> IEnumerator<Pair<string, int?>>.Current
		{
			[DebuggerHidden]
			get
			{
				return System_002ECollections_002EGeneric_002EIEnumerator_003CPair_003CSystem_002EString_002CSystem_002EInt32_003F_003E_003E_002Eget_Current();
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CResearchableIds_003Ed__1(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E7__wrap1 = null;
			_003CunavailableResearchIds_003E5__4 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				if (msg.AvailableResearchIds == null)
				{
					goto IL_0079;
				}
				string[] availableResearchIds = msg.AvailableResearchIds;
				_003C_003E7__wrap1 = availableResearchIds;
				_003C_003E7__wrap2 = 0;
				goto IL_00a3;
			}
			case 1:
				_003C_003E1__state = -1;
				_003C_003E7__wrap2++;
				goto IL_00a3;
			case 2:
				{
					_003C_003E1__state = -1;
					_003C_003E7__wrap2++;
					goto IL_00e8;
				}
				IL_0079:
				if (msg.UnavailableResearchIds == null)
				{
					break;
				}
				_003CunavailableResearchIds_003E5__4 = msg.UnavailableResearchIds;
				_003C_003E7__wrap2 = 0;
				goto IL_00e8;
				IL_00a3:
				if (_003C_003E7__wrap2 < _003C_003E7__wrap1.Length)
				{
					string item = _003C_003E7__wrap1[_003C_003E7__wrap2];
					_003C_003E2__current = new Pair<string, int?>(item, null);
					_003C_003E1__state = 1;
					return true;
				}
				_003C_003E7__wrap1 = null;
				goto IL_0079;
				IL_00e8:
				if (_003C_003E7__wrap2 < _003CunavailableResearchIds_003E5__4.Length)
				{
					Pair<string, int> pair = _003CunavailableResearchIds_003E5__4[_003C_003E7__wrap2];
					_003C_003E2__current = new Pair<string, int?>(pair.Item1, pair.Item2);
					_003C_003E1__state = 2;
					return true;
				}
				_003CunavailableResearchIds_003E5__4 = null;
				break;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			return MoveNext();
		}

		[DebuggerHidden]
		private Pair<string, int?> System_002ECollections_002EGeneric_002EIEnumerator_003CPair_003CSystem_002EString_002CSystem_002EInt32_003F_003E_003E_002Eget_Current()
		{
			return _003C_003E2__current;
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<Pair<string, int?>> IEnumerable<Pair<string, int?>>.GetEnumerator()
		{
			_003CResearchableIds_003Ed__1 _003CResearchableIds_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Thread.CurrentThread.ManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CResearchableIds_003Ed__ = this;
			}
			else
			{
				_003CResearchableIds_003Ed__ = new _003CResearchableIds_003Ed__1(0);
			}
			_003CResearchableIds_003Ed__.msg = _003C_003E3__msg;
			return _003CResearchableIds_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<Pair<string, int?>>)this).GetEnumerator();
		}
	}

	public static ResearchCategory GetCategory(this AvailablePersonalResearch msg)
	{
		foreach (Pair<string, int?> item in msg.ResearchableIds())
		{
			PersonalResearch personalResearch = SingletonDict<string, PersonalResearch>.Get(item.Item1);
			if (personalResearch != null)
			{
				return personalResearch.Category;
			}
		}
		return ResearchCategory.Invalid;
	}

	public static IEnumerable<Pair<string, int?>> ResearchableIds(this AvailablePersonalResearch msg)
	{
		//yield-return decompiler failed: Could not find currentField
		return new _003CResearchableIds_003Ed__1(-2)
		{
			_003C_003E3__msg = msg
		};
	}
}

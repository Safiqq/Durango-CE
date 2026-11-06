using System;
using System.Collections.Generic;

namespace DurangoLauncher;

public sealed class TintLru<T> where T : class, IDisposable
{
	private readonly int _max;

	private readonly Dictionary<string, LinkedListNode<(string Key, T Value)>> _map = new Dictionary<string, LinkedListNode<(string, T)>>();

	private readonly LinkedList<(string Key, T Value)> _list = new LinkedList<(string, T)>();

	public int Count
	{
		get
		{
			lock (_map)
			{
				return _list.Count;
			}
		}
	}

	public TintLru(int max)
	{
		_max = Math.Max(1, max);
	}

	public bool Contains(string key)
	{
		lock (_map)
		{
			return _map.ContainsKey(key);
		}
	}

	public T GetOrAdd(string key, Func<T> make)
	{
		lock (_map)
		{
			if (_map.TryGetValue(key, out LinkedListNode<(string, T)> value))
			{
				_list.Remove(value);
				_list.AddFirst(value);
				return value.Value.Item2;
			}
			T val = make();
			_map[key] = _list.AddFirst((key, val));
			while (_list.Count > _max)
			{
				LinkedListNode<(string, T)> last = _list.Last;
				_list.RemoveLast();
				_map.Remove(last.Value.Item1);
				last.Value.Item2.Dispose();
			}
			return val;
		}
	}

	public void Clear()
	{
		lock (_map)
		{
			foreach (var item in _list)
			{
				item.Value.Dispose();
			}
			_list.Clear();
			_map.Clear();
		}
	}
}

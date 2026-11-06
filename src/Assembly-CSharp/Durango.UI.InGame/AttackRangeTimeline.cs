using System;
using System.Collections.Generic;
using Messages;

namespace Durango.UI.InGame;

public sealed class AttackRangeTimeline
{
	private readonly Dictionary<string, (double Start, double Expires)> _latest = new Dictionary<string, (double, double)>();

	private readonly Dictionary<(string Owner, double Start, string Window), (double Revision, double Expires)> _revisions = new Dictionary<(string, double, string), (double, double)>();

	private readonly Dictionary<string, (double Start, double Expires)> _cancelled = new Dictionary<string, (double, double)>();

	private double _pruneAt;

	public bool Accept(AttackRangeContext context, double now, double until, out bool newAttack)
	{
		newAttack = false;
		if (context == null || !context.IsValid())
		{
			return false;
		}
		Prune(now);
		double num = Math.Max(now, until) + 30.0;
		if (context.Cancel)
		{
			if (_cancelled.TryGetValue(context.OwnerId, out (double, double) value))
			{
				_cancelled[context.OwnerId] = (Math.Max(value.Item1, context.AttackStartAt), Math.Max(value.Item2, num));
			}
			else
			{
				_cancelled[context.OwnerId] = (context.AttackStartAt, num);
			}
			return true;
		}
		if (_cancelled.TryGetValue(context.OwnerId, out (double, double) value2) && context.AttackStartAt <= value2.Item1)
		{
			return false;
		}
		if (_latest.TryGetValue(context.OwnerId, out (double, double) value3) && context.AttackStartAt < value3.Item1)
		{
			return false;
		}
		(string, double, string) key = (context.OwnerId, context.AttackStartAt, context.WindowId);
		if (_revisions.TryGetValue(key, out (double, double) value4) && context.RevisionAt <= value4.Item1)
		{
			return false;
		}
		newAttack = !_latest.TryGetValue(context.OwnerId, out value3) || context.AttackStartAt > value3.Item1;
		_latest[context.OwnerId] = (context.AttackStartAt, num);
		_revisions[key] = (context.RevisionAt, num);
		return true;
	}

	public void Clear()
	{
		_latest.Clear();
		_revisions.Clear();
		_cancelled.Clear();
		_pruneAt = 0.0;
	}

	public void Prune(double now)
	{
		if (now < _pruneAt)
		{
			return;
		}
		_pruneAt = now + 1.0;
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, (double, double)> item in _latest)
		{
			if (item.Value.Item2 < now)
			{
				list.Add(item.Key);
			}
		}
		foreach (string item2 in list)
		{
			_latest.Remove(item2);
		}
		List<(string, double, string)> list2 = new List<(string, double, string)>();
		foreach (KeyValuePair<(string, double, string), (double, double)> revision in _revisions)
		{
			if (revision.Value.Item2 < now)
			{
				list2.Add(revision.Key);
			}
		}
		foreach (var item3 in list2)
		{
			_revisions.Remove(item3);
		}
		List<string> list3 = new List<string>();
		foreach (KeyValuePair<string, (double, double)> item4 in _cancelled)
		{
			if (item4.Value.Item2 < now)
			{
				list3.Add(item4.Key);
			}
		}
		foreach (string item5 in list3)
		{
			_cancelled.Remove(item5);
		}
	}
}

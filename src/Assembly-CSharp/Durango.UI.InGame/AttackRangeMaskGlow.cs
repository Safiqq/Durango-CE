using System;
using System.Collections.Generic;
using Messages;

namespace Durango.UI.InGame;

public sealed class AttackRangeMaskGlow
{
	public readonly bool[] Cells;

	public readonly float[] Distances;

	public readonly int Width;

	public readonly int Height;

	public readonly float Cell;

	private readonly int[] _runs;

	private static readonly Dictionary<ulong, LinkedListNode<AttackRangeMaskGlow>> Cache = new Dictionary<ulong, LinkedListNode<AttackRangeMaskGlow>>();

	private static readonly LinkedList<AttackRangeMaskGlow> Lru = new LinkedList<AttackRangeMaskGlow>();

	private readonly ulong _key;

	private static int _units;

	public const int MaxCells = 16384;

	public const float GlowWidth = 60f;

	private const int MaxCacheUnits = 131072;

	public static int Hits { get; private set; }

	public static int Misses { get; private set; }

	public static int Entries => Cache.Count;

	public static int CachedUnits => _units;

	private int Cost => Cells.Length + Distances.Length;

	public static AttackRangeMaskGlow Get(AttackRangeMask mask)
	{
		if (mask.Width * mask.Height > 16384)
		{
			return null;
		}
		ulong key = 14695981039346656037uL;
		Mix(mask.Width);
		Mix(mask.Height);
		Mix(mask.Cell.GetHashCode());
		int[] runs = mask.Runs;
		for (int i = 0; i < runs.Length; i++)
		{
			Mix(runs[i]);
		}
		if (Cache.TryGetValue(key, out var value))
		{
			AttackRangeMaskGlow value2 = value.Value;
			bool flag = value2.Width == mask.Width && value2.Height == mask.Height && value2.Cell == mask.Cell && value2._runs.Length == mask.Runs.Length;
			int num = 0;
			while (flag && num < value2._runs.Length)
			{
				flag = value2._runs[num] == mask.Runs[num];
				num++;
			}
			if (flag)
			{
				Hits++;
				Lru.Remove(value);
				Lru.AddFirst(value);
				return value2;
			}
			Remove(value);
		}
		Misses++;
		AttackRangeMaskGlow attackRangeMaskGlow = new AttackRangeMaskGlow(mask, key);
		while (Cache.Count >= 32 || _units + attackRangeMaskGlow.Cost > 131072)
		{
			Remove(Lru.Last);
		}
		Cache[key] = Lru.AddFirst(attackRangeMaskGlow);
		_units += attackRangeMaskGlow.Cost;
		return attackRangeMaskGlow;
		void Mix(int n)
		{
			key = (key ^ (uint)n) * 1099511628211L;
		}
	}

	private static void Remove(LinkedListNode<AttackRangeMaskGlow> node)
	{
		if (node != null)
		{
			Cache.Remove(node.Value._key);
			_units -= node.Value.Cost;
			Lru.Remove(node);
		}
	}

	public static void Clear()
	{
		Cache.Clear();
		Lru.Clear();
		_units = 0;
		Hits = (Misses = 0);
	}

	private AttackRangeMaskGlow(AttackRangeMask mask, ulong key)
	{
		_key = key;
		Width = mask.Width;
		Height = mask.Height;
		Cell = mask.Cell;
		_runs = (int[])mask.Runs.Clone();
		Cells = new bool[Width * Height];
		for (int i = 0; i < _runs.Length; i += 3)
		{
			for (int j = _runs[i + 1]; j <= _runs[i + 2]; j++)
			{
				Cells[_runs[i] * Width + j] = true;
			}
		}
		int num = Width + 1;
		Distances = new float[num * (Height + 1)];
		for (int k = 0; k <= Height; k++)
		{
			for (int l = 0; l <= Width; l++)
			{
				Distances[k * num + l] = ((Filled(l - 1, k - 1) && Filled(l, k - 1) && Filled(l - 1, k) && Filled(l, k)) ? (60f + Cell) : 0f);
				bool Filled(int x, int z)
				{
					if (x >= 0 && z >= 0 && x < Width && z < Height)
					{
						return Cells[z * Width + x];
					}
					return false;
				}
			}
		}
		float num2 = Cell * 1.4142135f;
		for (int m = 0; m <= Height; m++)
		{
			for (int n = 0; n <= Width; n++)
			{
				int num3 = m * num + n;
				float num4 = Distances[num3];
				if (n > 0)
				{
					num4 = Math.Min(num4, Distances[num3 - 1] + Cell);
				}
				if (m > 0)
				{
					num4 = Math.Min(num4, Distances[num3 - num] + Cell);
					if (n > 0)
					{
						num4 = Math.Min(num4, Distances[num3 - num - 1] + num2);
					}
					if (n < Width)
					{
						num4 = Math.Min(num4, Distances[num3 - num + 1] + num2);
					}
				}
				Distances[num3] = num4;
			}
		}
		for (int num5 = Height; num5 >= 0; num5--)
		{
			for (int num6 = Width; num6 >= 0; num6--)
			{
				int num7 = num5 * num + num6;
				float num8 = Distances[num7];
				if (num6 < Width)
				{
					num8 = Math.Min(num8, Distances[num7 + 1] + Cell);
				}
				if (num5 < Height)
				{
					num8 = Math.Min(num8, Distances[num7 + num] + Cell);
					if (num6 > 0)
					{
						num8 = Math.Min(num8, Distances[num7 + num - 1] + num2);
					}
					if (num6 < Width)
					{
						num8 = Math.Min(num8, Distances[num7 + num + 1] + num2);
					}
				}
				Distances[num7] = num8;
			}
		}
	}
}

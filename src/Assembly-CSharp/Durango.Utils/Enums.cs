using System;
using System.Linq;

namespace Durango.Utils;

public static class Enums<T> where T : struct, IComparable, IFormattable, IConvertible
{
	private static T[] _values;

	private static T[] _greaterValues;

	private static int _greaterInt;

	private static T? _maxValue;

	public static int ToInt(T e)
	{
		return (int)Convert.ToInt64(e);
	}

	public static T ToEnum(int value)
	{
		return (T)Enum.ToObject(typeof(T), value);
	}

	public static T[] All()
	{
		if (_values != null)
		{
			return _values;
		}
		_values = (T[])Enum.GetValues(typeof(T));
		return _values;
	}

	public static T[] Greater(T greater)
	{
		int num = ToInt(greater);
		if (_greaterValues != null && _greaterInt == num)
		{
			return _greaterValues;
		}
		_greaterInt = num;
		_greaterValues = (from T x in Enum.GetValues(typeof(T))
			where ToInt(x) > _greaterInt
			select x).ToArray();
		return _greaterValues;
	}

	public static T Max()
	{
		if (_maxValue.HasValue)
		{
			return _maxValue.Value;
		}
		T[] array = All();
		int? num = null;
		for (int i = 0; i < array.Length; i++)
		{
			int num2 = ToInt(array[i]);
			if (!num.HasValue)
			{
				num = num2;
			}
			else if (num.Value < num2)
			{
				num = num2;
			}
		}
		if (!num.HasValue)
		{
			return ToEnum(0);
		}
		_maxValue = ToEnum(num.Value);
		return _maxValue.Value;
	}
}

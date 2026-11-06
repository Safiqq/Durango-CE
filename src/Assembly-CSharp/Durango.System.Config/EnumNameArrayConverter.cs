using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Durango.System.Config;

public class EnumNameArrayConverter<T> : JsonConverter where T : struct
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(T[]);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.Null)
		{
			return null;
		}
		List<T> list = new List<T>();
		if (reader.TokenType != JsonToken.StartArray)
		{
			Add(list, reader.Value);
			return list.ToArray();
		}
		while (reader.Read() && reader.TokenType != JsonToken.EndArray)
		{
			Add(list, reader.Value);
		}
		return list.ToArray();
	}

	private static void Add(List<T> values, object value)
	{
		if (value != null)
		{
			if (value is string value2)
			{
				values.Add((T)Enum.Parse(typeof(T), value2, ignoreCase: true));
			}
			else
			{
				values.Add((T)Enum.ToObject(typeof(T), Convert.ToInt64(value)));
			}
		}
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		writer.WriteStartArray();
		T[] array = (T[])value;
		for (int i = 0; i < array.Length; i++)
		{
			T val = array[i];
			writer.WriteValue(val.ToString());
		}
		writer.WriteEndArray();
	}
}

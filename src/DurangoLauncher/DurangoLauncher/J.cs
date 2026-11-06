using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace DurangoLauncher;

internal static class J
{
	public static string Str(JsonElement e, string k, string def = "")
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var value))
		{
			return value.ValueKind switch
			{
				JsonValueKind.String => value.GetString() ?? def, 
				JsonValueKind.Number => value.GetRawText(), 
				JsonValueKind.True => "true", 
				JsonValueKind.False => "false", 
				_ => def, 
			};
		}
		return def;
	}

	public static int Int(JsonElement e, string k, int def = 0)
	{
		return IntN(e, k) ?? def;
	}

	public static int? IntN(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value))
		{
			return null;
		}
		if (value.ValueKind == JsonValueKind.Number)
		{
			if (!value.TryGetInt32(out var value2))
			{
				if (!value.TryGetDouble(out var value3))
				{
					return null;
				}
				return (int)Math.Clamp(value3, -2147483648.0, 2147483647.0);
			}
			return value2;
		}
		if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}

	public static long Long(JsonElement e, string k, long def = 0L)
	{
		return LongN(e, k) ?? def;
	}

	public static long? LongN(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value))
		{
			return null;
		}
		if (value.ValueKind == JsonValueKind.Number)
		{
			if (!value.TryGetInt64(out var value2))
			{
				if (!value.TryGetDouble(out var value3))
				{
					return null;
				}
				return (long)value3;
			}
			return value2;
		}
		if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}

	public static double Dbl(JsonElement e, string k, double def = 0.0)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var value2))
		{
			return def;
		}
		return value2;
	}

	public static bool Bool(JsonElement e, string k)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var value))
		{
			if (value.ValueKind != JsonValueKind.True)
			{
				if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var value2))
				{
					return value2 != 0;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool? BoolN(JsonElement e, string k)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var value))
		{
			return value.ValueKind switch
			{
				JsonValueKind.True => true, 
				JsonValueKind.False => false, 
				_ => null, 
			};
		}
		return null;
	}

	public static JsonElement Obj(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Object)
		{
			return default(JsonElement);
		}
		return value;
	}

	public static IEnumerable<JsonElement> Objs(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return Enumerable.Empty<JsonElement>();
		}
		return from x in value.EnumerateArray()
			where x.ValueKind == JsonValueKind.Object
			select x;
	}

	public static string[] StrArr(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value) || value.ValueKind != JsonValueKind.Array)
		{
			return Array.Empty<string>();
		}
		return (from x in value.EnumerateArray()
			where x.ValueKind == JsonValueKind.String
			select x.GetString()).ToArray();
	}

	public static (string Th, string En) Name(JsonElement e, string k)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(k, out var value))
		{
			return (Th: "", En: "");
		}
		if (value.ValueKind == JsonValueKind.String)
		{
			string? obj = value.GetString() ?? "";
			return (Th: obj, En: obj);
		}
		if (value.ValueKind == JsonValueKind.Object)
		{
			return (Th: Str(value, "th"), En: Str(value, "en"));
		}
		return (Th: "", En: "");
	}
}

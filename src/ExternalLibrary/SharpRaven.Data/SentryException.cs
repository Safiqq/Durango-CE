using System;
using Newtonsoft.Json;
using UnityEngine;

namespace SharpRaven.Data;

public class SentryException
{
	[JsonProperty(PropertyName = "type")]
	public string Type;

	[JsonProperty(PropertyName = "value")]
	public string Value;

	[JsonProperty(PropertyName = "module")]
	public string Module;

	public SentryException(Exception e)
	{
		Module = e.Source;
		Type = e.Message;
		Value = e.Message;
	}

	public SentryException(string log, string stack, LogType logType)
	{
		Module = log;
		Type = logType.ToString();
		Value = stack;
	}
}

using System.Collections.Generic;

namespace Durango.Localization.Overrides;

public class PoEntry
{
	public string Context;

	public string MsgId = "";

	public string MsgIdPlural;

	public List<string> MsgStrs = new List<string>();
}

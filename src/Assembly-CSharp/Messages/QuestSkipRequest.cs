using MsgPack;

namespace Messages;

public struct QuestSkipRequest
{
	public const uint TypeCode = 7300003u;

	public string QuestId;

	public long Price;

	public static void Pack(Packer packer, QuestSkipRequest val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(3);
			packer.Pack(7300003u);
		}
		else
		{
			packer.PackArrayHeader(2);
		}
		packer.PackString(val.QuestId ?? string.Empty);
		packer.Pack(val.Price);
	}

	public static QuestSkipRequest Unpack(Unpacker unpacker)
	{
		QuestSkipRequest result = default(QuestSkipRequest);
		unpacker.Read();
		result.QuestId = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		unpacker.Read();
		result.Price = (unpacker.LastReadData.IsNil ? 0 : unpacker.LastReadData.AsInt64());
		return result;
	}

	public override string ToString()
	{
		return $"<QuestSkipRequest QuestId={QuestId} Price={Price}>";
	}
}

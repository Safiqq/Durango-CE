using MsgPack;

namespace Messages;

public struct QuestSkipResult
{
	public const uint TypeCode = 7300004u;

	public string QuestId;

	public bool Done;

	public long Price;

	public long Coin;

	public string Reason;

	public string Text;

	public string[] Ids;

	public long[] Prices;

	public static void Pack(Packer packer, QuestSkipResult val, bool hint = false)
	{
		if (hint)
		{
			packer.PackArrayHeader(9);
			packer.Pack(7300004u);
		}
		else
		{
			packer.PackArrayHeader(8);
		}
		packer.PackString(val.QuestId ?? string.Empty);
		packer.Pack(val.Done);
		packer.Pack(val.Price);
		packer.Pack(val.Coin);
		packer.PackString(val.Reason ?? string.Empty);
		packer.PackString(val.Text ?? string.Empty);
		string[] array = val.Ids ?? new string[0];
		packer.PackArrayHeader(array.Length);
		for (int i = 0; i < array.Length; i++)
		{
			packer.PackString(array[i] ?? string.Empty);
		}
		long[] array2 = val.Prices ?? new long[0];
		packer.PackArrayHeader(array2.Length);
		for (int j = 0; j < array2.Length; j++)
		{
			packer.Pack(array2[j]);
		}
	}

	public static QuestSkipResult Unpack(Unpacker unpacker)
	{
		QuestSkipResult result = default(QuestSkipResult);
		unpacker.Read();
		result.QuestId = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		unpacker.Read();
		result.Done = !unpacker.LastReadData.IsNil && unpacker.LastReadData.AsBoolean();
		unpacker.Read();
		result.Price = (unpacker.LastReadData.IsNil ? 0 : unpacker.LastReadData.AsInt64());
		unpacker.Read();
		result.Coin = (unpacker.LastReadData.IsNil ? (-1) : unpacker.LastReadData.AsInt64());
		unpacker.Read();
		result.Reason = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		unpacker.Read();
		result.Text = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		unpacker.Read();
		int num = ((!unpacker.LastReadData.IsNil) ? unpacker.LastReadData.AsInt32() : 0);
		result.Ids = new string[num];
		for (int i = 0; i < num; i++)
		{
			unpacker.Read();
			result.Ids[i] = (unpacker.LastReadData.IsNil ? string.Empty : unpacker.LastReadData.AsString());
		}
		unpacker.Read();
		int num2 = ((!unpacker.LastReadData.IsNil) ? unpacker.LastReadData.AsInt32() : 0);
		result.Prices = new long[num2];
		for (int j = 0; j < num2; j++)
		{
			unpacker.Read();
			result.Prices[j] = (unpacker.LastReadData.IsNil ? 0 : unpacker.LastReadData.AsInt64());
		}
		return result;
	}

	public override string ToString()
	{
		return $"<QuestSkipResult QuestId={QuestId} Done={Done} Price={Price} Coin={Coin} Reason={Reason} Ids={((Ids != null) ? Ids.Length : 0)}>";
	}
}

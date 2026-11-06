using MsgPack;

namespace Messages;

public struct Leaderboard
{
	public LeaderboardContent[] Contents;

	public static void Pack(Packer packer, Leaderboard val, bool hint = false)
	{
		packer.PackArrayHeader(1);
		if (val.Contents == null)
		{
			packer.PackArrayHeader(0);
			return;
		}
		packer.PackArrayHeader(val.Contents.Length);
		for (int i = 0; i < val.Contents.Length; i++)
		{
			LeaderboardContent.Pack(packer, val.Contents[i]);
		}
	}

	public static Leaderboard Unpack(Unpacker unpacker)
	{
		unpacker.Read();
		int num = unpacker.LastReadData.AsInt32();
		Leaderboard result = new Leaderboard
		{
			Contents = new LeaderboardContent[num]
		};
		for (int i = 0; i < num; i++)
		{
			unpacker.Read();
			result.Contents[i] = LeaderboardContent.Unpack(unpacker);
		}
		return result;
	}

	public override string ToString()
	{
		return $"<Leaderboard Contents={Contents}>";
	}
}

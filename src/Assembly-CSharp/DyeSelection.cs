using System;

public sealed class DyeSelection
{
	private readonly string[] dyes = new string[3];

	public string TargetId { get; private set; }

	public int Channel { get; private set; }

	public string this[int channel] => dyes[channel];

	public int Count
	{
		get
		{
			int num = 0;
			string[] array = dyes;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != null)
				{
					num++;
				}
			}
			return num;
		}
	}

	public void SetTarget(string id)
	{
		if (!(TargetId == id))
		{
			TargetId = id;
			Array.Clear(dyes, 0, dyes.Length);
		}
	}

	public void SelectChannel(int channel)
	{
		if (channel < 0 || channel >= dyes.Length)
		{
			throw new ArgumentOutOfRangeException("channel");
		}
		Channel = channel;
	}

	public void Assign(string id)
	{
		if (id != null)
		{
			Remove(id);
		}
		dyes[Channel] = id;
	}

	public void Remove(string id)
	{
		for (int i = 0; i < dyes.Length; i++)
		{
			if (dyes[i] == id)
			{
				dyes[i] = null;
			}
		}
	}

	public int ChannelOf(string id)
	{
		if (id == null)
		{
			return -1;
		}
		for (int i = 0; i < dyes.Length; i++)
		{
			if (dyes[i] == id)
			{
				return i;
			}
		}
		return -1;
	}

	public string[] Snapshot()
	{
		return (string[])dyes.Clone();
	}
}

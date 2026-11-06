namespace MML;

public class MMLSettings
{
	private int minVolume = 1;

	private int maxVolume = 15;

	private int maxSize = 8192;

	private int minTempo = 32;

	private int maxTempo = 255;

	private int minOctave = 1;

	private int maxOctave = 8;

	public int MaxSize
	{
		get
		{
			return maxSize;
		}
		set
		{
			maxSize = value;
		}
	}

	public int MinTempo
	{
		get
		{
			return minTempo;
		}
		set
		{
			minTempo = value;
		}
	}

	public int MaxTempo
	{
		get
		{
			return maxTempo;
		}
		set
		{
			maxTempo = value;
		}
	}

	public int MinOctave
	{
		get
		{
			return minOctave;
		}
		set
		{
			minOctave = value;
		}
	}

	public int MaxOctave
	{
		get
		{
			return maxOctave;
		}
		set
		{
			maxOctave = value;
		}
	}

	public int MinVolume
	{
		get
		{
			return minVolume;
		}
		set
		{
			minVolume = value;
		}
	}

	public int MaxVolume
	{
		get
		{
			return maxVolume;
		}
		set
		{
			maxVolume = value;
		}
	}
}

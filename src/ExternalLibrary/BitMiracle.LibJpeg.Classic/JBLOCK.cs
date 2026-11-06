namespace BitMiracle.LibJpeg.Classic;

public class JBLOCK
{
	internal short[] data = new short[64];

	public short this[int index]
	{
		get
		{
			return data[index];
		}
		set
		{
			data[index] = value;
		}
	}
}

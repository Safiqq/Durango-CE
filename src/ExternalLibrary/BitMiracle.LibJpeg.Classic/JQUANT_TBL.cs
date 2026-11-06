namespace BitMiracle.LibJpeg.Classic;

public class JQUANT_TBL
{
	private bool m_sent_table;

	internal readonly short[] quantval = new short[64];

	public bool Sent_table
	{
		get
		{
			return m_sent_table;
		}
		set
		{
			m_sent_table = value;
		}
	}

	internal JQUANT_TBL()
	{
	}
}

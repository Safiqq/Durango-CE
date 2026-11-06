namespace BitMiracle.LibJpeg.Classic;

public class JHUFF_TBL
{
	private readonly byte[] m_bits = new byte[17];

	private readonly byte[] m_huffval = new byte[256];

	private bool m_sent_table;

	internal byte[] Bits => m_bits;

	internal byte[] Huffval => m_huffval;

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

	internal JHUFF_TBL()
	{
	}
}

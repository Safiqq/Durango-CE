namespace BitMiracle.LibJpeg.Classic;

public class jpeg_marker_struct
{
	private byte m_marker;

	private int m_originalLength;

	private byte[] m_data;

	public byte Marker => m_marker;

	public int OriginalLength => m_originalLength;

	public byte[] Data => m_data;

	internal jpeg_marker_struct(byte marker, int originalDataLength, int lengthLimit)
	{
		m_marker = marker;
		m_originalLength = originalDataLength;
		m_data = new byte[lengthLimit];
	}
}

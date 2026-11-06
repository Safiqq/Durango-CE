namespace BitMiracle.LibJpeg.Classic.Internal;

internal class jpeg_scan_info
{
	public int comps_in_scan;

	public int[] component_index = new int[4];

	public int Ss;

	public int Se;

	public int Ah;

	public int Al;
}

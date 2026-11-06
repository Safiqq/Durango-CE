using System;
using System.Globalization;

namespace BitMiracle.LibJpeg.Classic;

public class jpeg_error_mgr
{
	internal int m_msg_code;

	internal object[] m_msg_parm;

	internal int m_trace_level;

	internal int m_num_warnings;

	public int Trace_level
	{
		get
		{
			return m_trace_level;
		}
		set
		{
			m_trace_level = value;
		}
	}

	public int Num_warnings => m_num_warnings;

	public virtual void error_exit()
	{
		output_message();
		string message = format_message();
		throw new Exception(message);
	}

	public virtual void emit_message(int msg_level)
	{
		if (msg_level < 0)
		{
			if (m_num_warnings == 0 || m_trace_level >= 3)
			{
				output_message();
			}
			m_num_warnings++;
		}
		else if (m_trace_level >= msg_level)
		{
			output_message();
		}
	}

	public virtual void output_message()
	{
		string value = format_message();
		Console.WriteLine(value);
	}

	public virtual string format_message()
	{
		string messageText = GetMessageText(m_msg_code);
		if (messageText == null)
		{
			m_msg_parm = new object[1] { m_msg_code };
			messageText = GetMessageText(0);
		}
		return string.Format(CultureInfo.CurrentCulture, messageText, m_msg_parm);
	}

	public virtual void reset_error_mgr()
	{
		m_num_warnings = 0;
		m_msg_code = 0;
	}

	protected virtual string GetMessageText(int code)
	{
		return (J_MESSAGE_CODE)code switch
		{
			J_MESSAGE_CODE.JERR_BAD_BUFFER_MODE => "Bogus buffer control mode",
			J_MESSAGE_CODE.JERR_BAD_COMPONENT_ID => "Invalid component ID {0} in SOS",
			J_MESSAGE_CODE.JERR_BAD_CROP_SPEC => "Invalid crop request",
			J_MESSAGE_CODE.JERR_BAD_DCT_COEF => "DCT coefficient out of range",
			J_MESSAGE_CODE.JERR_BAD_DCTSIZE => "DCT scaled output block size {0}x{1} not supported",
			J_MESSAGE_CODE.JERR_BAD_DROP_SAMPLING => "Component index {0}: mismatching sampling ratio {1}:{2}, {3}:{4}, {5}",
			J_MESSAGE_CODE.JERR_BAD_HUFF_TABLE => "Bogus Huffman table definition",
			J_MESSAGE_CODE.JERR_BAD_IN_COLORSPACE => "Bogus input colorspace",
			J_MESSAGE_CODE.JERR_BAD_J_COLORSPACE => "Bogus JPEG colorspace",
			J_MESSAGE_CODE.JERR_BAD_LENGTH => "Bogus marker length",
			J_MESSAGE_CODE.JERR_BAD_MCU_SIZE => "Sampling factors too large for interleaved scan",
			J_MESSAGE_CODE.JERR_BAD_PRECISION => "Unsupported JPEG data precision {0}",
			J_MESSAGE_CODE.JERR_BAD_PROGRESSION => "Invalid progressive parameters Ss={0} Se={1} Ah={2} Al={3}",
			J_MESSAGE_CODE.JERR_BAD_PROG_SCRIPT => "Invalid progressive parameters at scan script entry {0}",
			J_MESSAGE_CODE.JERR_BAD_SAMPLING => "Bogus sampling factors",
			J_MESSAGE_CODE.JERR_BAD_SCAN_SCRIPT => "Invalid scan script at entry {0}",
			J_MESSAGE_CODE.JERR_BAD_STATE => "Improper call to JPEG library in state {0}",
			J_MESSAGE_CODE.JERR_BAD_VIRTUAL_ACCESS => "Bogus virtual array access",
			J_MESSAGE_CODE.JERR_BUFFER_SIZE => "Buffer passed to JPEG library is too small",
			J_MESSAGE_CODE.JERR_CANT_SUSPEND => "Suspension not allowed here",
			J_MESSAGE_CODE.JERR_CCIR601_NOTIMPL => "CCIR601 sampling not implemented yet",
			J_MESSAGE_CODE.JERR_COMPONENT_COUNT => "Too many color components: {0}, max {1}",
			J_MESSAGE_CODE.JERR_CONVERSION_NOTIMPL => "Unsupported color conversion request",
			J_MESSAGE_CODE.JERR_DAC_INDEX => "Bogus DAC index {0}",
			J_MESSAGE_CODE.JERR_DAC_VALUE => "Bogus DAC value 0x{0}",
			J_MESSAGE_CODE.JERR_DHT_INDEX => "Bogus DHT index {0}",
			J_MESSAGE_CODE.JERR_DQT_INDEX => "Bogus DQT index {0}",
			J_MESSAGE_CODE.JERR_EMPTY_IMAGE => "Empty JPEG image (DNL not supported)",
			J_MESSAGE_CODE.JERR_EOI_EXPECTED => "Didn't expect more than one scan",
			J_MESSAGE_CODE.JERR_FILE_WRITE => "Output file write error --- out of disk space?",
			J_MESSAGE_CODE.JERR_FRACT_SAMPLE_NOTIMPL => "Fractional sampling not implemented yet",
			J_MESSAGE_CODE.JERR_HUFF_CLEN_OVERFLOW => "Huffman code size table overflow",
			J_MESSAGE_CODE.JERR_HUFF_MISSING_CODE => "Missing Huffman code table entry",
			J_MESSAGE_CODE.JERR_IMAGE_TOO_BIG => "Maximum supported image dimension is {0} pixels",
			J_MESSAGE_CODE.JERR_INPUT_EMPTY => "Empty input file",
			J_MESSAGE_CODE.JERR_INPUT_EOF => "Premature end of input file",
			J_MESSAGE_CODE.JERR_MISMATCHED_QUANT_TABLE => "Cannot transcode due to multiple use of quantization table {0}",
			J_MESSAGE_CODE.JERR_MISSING_DATA => "Scan script does not transmit all data",
			J_MESSAGE_CODE.JERR_MODE_CHANGE => "Invalid color quantization mode change",
			J_MESSAGE_CODE.JERR_NOTIMPL => "Not implemented yet",
			J_MESSAGE_CODE.JERR_NOT_COMPILED => "Requested feature was omitted at compile time",
			J_MESSAGE_CODE.JERR_NO_ARITH_TABLE => "Arithmetic table 0x{0:X2} was not defined",
			J_MESSAGE_CODE.JERR_NO_HUFF_TABLE => "Huffman table 0x{0:X2} was not defined",
			J_MESSAGE_CODE.JERR_NO_IMAGE => "JPEG datastream contains no image",
			J_MESSAGE_CODE.JERR_NO_QUANT_TABLE => "Quantization table 0x{0:X2} was not defined",
			J_MESSAGE_CODE.JERR_NO_SOI => "Not a JPEG file: starts with 0x{0:X2} 0x{1:X2}",
			J_MESSAGE_CODE.JERR_OUT_OF_MEMORY => "Insufficient memory (case {0})",
			J_MESSAGE_CODE.JERR_QUANT_COMPONENTS => "Cannot quantize more than {0} color components",
			J_MESSAGE_CODE.JERR_QUANT_FEW_COLORS => "Cannot quantize to fewer than {0} colors",
			J_MESSAGE_CODE.JERR_QUANT_MANY_COLORS => "Cannot quantize to more than {0} colors",
			J_MESSAGE_CODE.JERR_SOF_BEFORE => "Invalid JPEG file structure: {0} before SOF",
			J_MESSAGE_CODE.JERR_SOF_DUPLICATE => "Invalid JPEG file structure: two SOF markers",
			J_MESSAGE_CODE.JERR_SOF_NO_SOS => "Invalid JPEG file structure: missing SOS marker",
			J_MESSAGE_CODE.JERR_SOF_UNSUPPORTED => "Unsupported JPEG process: SOF type 0x{0:X2}",
			J_MESSAGE_CODE.JERR_SOI_DUPLICATE => "Invalid JPEG file structure: two SOI markers",
			J_MESSAGE_CODE.JERR_SOS_NO_SOF => "Invalid JPEG file structure: SOS before SOF",
			J_MESSAGE_CODE.JERR_TOO_LITTLE_DATA => "Application transferred too few scanlines",
			J_MESSAGE_CODE.JERR_UNKNOWN_MARKER => "Unsupported marker type 0x{0:X2}",
			J_MESSAGE_CODE.JERR_WIDTH_OVERFLOW => "Image too wide for this implementation",
			J_MESSAGE_CODE.JTRC_16BIT_TABLES => "Caution: quantization tables are too coarse for baseline JPEG",
			J_MESSAGE_CODE.JTRC_ADOBE => "Adobe APP14 marker: version {0}, flags 0x{1:X4} 0x{2:X4}, transform {3}",
			J_MESSAGE_CODE.JTRC_APP0 => "Unknown APP0 marker (not JFIF), length {0}",
			J_MESSAGE_CODE.JTRC_APP14 => "Unknown APP14 marker (not Adobe), length {0}",
			J_MESSAGE_CODE.JTRC_DAC => "Define Arithmetic Table 0x{0:X2}: 0x{1:X2}",
			J_MESSAGE_CODE.JTRC_DHT => "Define Huffman Table 0x{0:X2}",
			J_MESSAGE_CODE.JTRC_DQT => "Define Quantization Table {0} precision {1}",
			J_MESSAGE_CODE.JTRC_DRI => "Define Restart Interval {0}",
			J_MESSAGE_CODE.JTRC_EOI => "End Of Image",
			J_MESSAGE_CODE.JTRC_HUFFBITS => "        {0:D3} {1:D3} {2:D3} {3:D3} {4:D3} {5:D3} {6:D3} {7:D3}",
			J_MESSAGE_CODE.JTRC_JFIF => "JFIF APP0 marker: version {0}.{1:D2}, density {2}x{3}  {4}",
			J_MESSAGE_CODE.JTRC_JFIF_BADTHUMBNAILSIZE => "Warning: thumbnail image size does not match data length {0}",
			J_MESSAGE_CODE.JTRC_JFIF_EXTENSION => "JFIF extension marker: type 0x{0:X2}, length {1}",
			J_MESSAGE_CODE.JTRC_JFIF_THUMBNAIL => "    with {0} x {1} thumbnail image",
			J_MESSAGE_CODE.JTRC_MISC_MARKER => "Miscellaneous marker 0x{0:X2}, length {1}",
			J_MESSAGE_CODE.JTRC_PARMLESS_MARKER => "Unexpected marker 0x{0:X2}",
			J_MESSAGE_CODE.JTRC_QUANTVALS => "        {0:D4} {1:D4} {2:D4} {3:D4} {4:D4} {5:D4} {6:D4} {7:D4}",
			J_MESSAGE_CODE.JTRC_QUANT_3_NCOLORS => "Quantizing to {0} = {1}*{2}*{3} colors",
			J_MESSAGE_CODE.JTRC_QUANT_NCOLORS => "Quantizing to {0} colors",
			J_MESSAGE_CODE.JTRC_QUANT_SELECTED => "Selected {0} colors for quantization",
			J_MESSAGE_CODE.JTRC_RECOVERY_ACTION => "At marker 0x{0:X2}, recovery action {1}",
			J_MESSAGE_CODE.JTRC_RST => "RST{0}",
			J_MESSAGE_CODE.JTRC_SMOOTH_NOTIMPL => "Smoothing not supported with nonstandard sampling ratios",
			J_MESSAGE_CODE.JTRC_SOF => "Start Of Frame 0x{0:X2}: width={1}, height={2}, components={3}",
			J_MESSAGE_CODE.JTRC_SOF_COMPONENT => "    Component {0}: {1}hx{2}v q={3}",
			J_MESSAGE_CODE.JTRC_SOI => "Start of Image",
			J_MESSAGE_CODE.JTRC_SOS => "Start Of Scan: {0} components",
			J_MESSAGE_CODE.JTRC_SOS_COMPONENT => "    Component {0}: dc={1} ac={2}",
			J_MESSAGE_CODE.JTRC_SOS_PARAMS => "  Ss={0}, Se={1}, Ah={2}, Al={3}",
			J_MESSAGE_CODE.JTRC_THUMB_JPEG => "JFIF extension marker: JPEG-compressed thumbnail image, length {0}",
			J_MESSAGE_CODE.JTRC_THUMB_PALETTE => "JFIF extension marker: palette thumbnail image, length {0}",
			J_MESSAGE_CODE.JTRC_THUMB_RGB => "JFIF extension marker: RGB thumbnail image, length {0}",
			J_MESSAGE_CODE.JTRC_UNKNOWN_IDS => "Unrecognized component IDs {0} {1} {2}, assuming YCbCr",
			J_MESSAGE_CODE.JWRN_ADOBE_XFORM => "Unknown Adobe color transform code {0}",
			J_MESSAGE_CODE.JWRN_ARITH_BAD_CODE => "Corrupt JPEG data: bad arithmetic code",
			J_MESSAGE_CODE.JWRN_BOGUS_PROGRESSION => "Inconsistent progression sequence for component {0} coefficient {1}",
			J_MESSAGE_CODE.JWRN_EXTRANEOUS_DATA => "Corrupt JPEG data: {0} extraneous bytes before marker 0x{1:X2}",
			J_MESSAGE_CODE.JWRN_HIT_MARKER => "Corrupt JPEG data: premature end of data segment",
			J_MESSAGE_CODE.JWRN_HUFF_BAD_CODE => "Corrupt JPEG data: bad Huffman code",
			J_MESSAGE_CODE.JWRN_JFIF_MAJOR => "Warning: unknown JFIF revision number {0}.{1:D2}",
			J_MESSAGE_CODE.JWRN_JPEG_EOF => "Premature end of JPEG file",
			J_MESSAGE_CODE.JWRN_MUST_RESYNC => "Corrupt JPEG data: found marker 0x{0:X2} instead of RST{1}",
			J_MESSAGE_CODE.JWRN_NOT_SEQUENTIAL => "Invalid SOS parameters for sequential JPEG",
			J_MESSAGE_CODE.JWRN_TOO_MUCH_DATA => "Application transferred too many scanlines",
			J_MESSAGE_CODE.JMSG_UNKNOWNMSGCODE => "Unknown message code (possibly it is an error from application)",
			_ => "Bogus message code {0}",
		};
	}
}

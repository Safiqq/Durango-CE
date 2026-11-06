using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DurangoLauncher;

public static class CouponCode
{
	public enum Kind
	{
		Empty,
		Reward,
		Coin,
		CoinBad,
		Move,
		Invalid
	}

	public sealed record Result(Kind Kind, string Code = "", string Reason = "")
	{
		public bool CanSend
		{
			get
			{
				Kind kind = Kind;
				if ((uint)(kind - 1) <= 1u)
				{
					return true;
				}
				return false;
			}
		}
	}

	public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

	public const string VoucherPrefix = "DC-";

	public const int RewardMin = 8;

	public const int RewardMax = 16;

	public const int MaxInput = 200;

	public const int MoveDigits = 9;

	public const int KeyChars = 12;

	private static readonly Regex VoucherInText = new Regex("(?<![A-Z0-9])DC[\\s\\-]*[A-Z0-9](?:[\\s\\-]*[A-Z0-9]){7}(?![A-Z0-9])", RegexOptions.CultureInvariant);

	public static Result Classify(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return new Result(Kind.Empty);
		}
		string text = raw.Trim();
		if (text.Length > 200)
		{
			return new Result(Kind.Invalid, "", "long");
		}
		string text2 = FindVoucher(text);
		if (text2 != null)
		{
			return new Result(Kind.Coin, text2);
		}
		if (LooksLikeMoveInput(text))
		{
			return new Result(Kind.Move);
		}
		if (LooksLikeVoucherAttempt(text))
		{
			return new Result(Kind.CoinBad);
		}
		string text3 = NormalizeReward(text);
		if (text3 != null && ShapeConflict(text3) == null)
		{
			return new Result(Kind.Reward, text3);
		}
		string text4 = new string(text.Where((char ch) => !char.IsWhiteSpace(ch)).ToArray());
		bool flag = text4.All(delegate(char ch)
		{
			switch (char.ToUpperInvariant(ch))
			{
			case '-':
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case 'A':
			case 'B':
			case 'C':
			case 'D':
			case 'E':
			case 'F':
			case 'G':
			case 'H':
			case 'I':
			case 'J':
			case 'K':
			case 'L':
			case 'M':
			case 'N':
			case 'O':
			case 'P':
			case 'Q':
			case 'R':
			case 'S':
			case 'T':
			case 'U':
			case 'V':
			case 'W':
			case 'X':
			case 'Y':
			case 'Z':
				return true;
			default:
				return false;
			}
		});
		return new Result(Kind.Invalid, "", (!flag) ? "chars" : ((text4.Length < 8) ? "short" : "long"));
	}

	public static string? NormalizeVoucher(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = text.Trim().ToUpperInvariant();
		foreach (char c in text2)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
			}
		}
		string text3 = stringBuilder.ToString();
		if (text3.Length != 10 || !text3.StartsWith("DC", StringComparison.Ordinal))
		{
			return null;
		}
		text2 = text3;
		text3 = text2.Substring(2, text2.Length - 2);
		if (!text3.All((char ch) => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(ch)))
		{
			return null;
		}
		string text4 = text3.Substring(0, 4);
		text2 = text3;
		return "DC-" + text4 + "-" + text2.Substring(4, text2.Length - 4);
	}

	public static string? FindVoucher(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length > 200)
		{
			return null;
		}
		string text = NormalizeVoucher(raw);
		if (text != null)
		{
			return text;
		}
		if (LooksLikeMoveInput(raw))
		{
			return null;
		}
		Match match = VoucherInText.Match(raw.ToUpperInvariant());
		if (!match.Success)
		{
			return null;
		}
		return NormalizeVoucher(match.Value);
	}

	public static bool LooksLikeVoucherAttempt(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || LooksLikeMoveInput(raw))
		{
			return false;
		}
		string text = AsciiCompact(raw);
		if (text.StartsWith("DC", StringComparison.Ordinal))
		{
			int length = text.Length;
			if (length >= 8)
			{
				return length <= 14;
			}
			return false;
		}
		return false;
	}

	public static bool LooksLikeMoveInput(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return false;
		}
		string text = raw.Trim();
		if (NormalizeMoveCode(text) != null || NormalizeTransferKey(text) != null)
		{
			return true;
		}
		if (text.Length >= 20 && text.Contains('-') && !text.Any(char.IsWhiteSpace))
		{
			return true;
		}
		if (text.Length == 6)
		{
			return text.All(char.IsAsciiLetterOrDigit);
		}
		return false;
	}

	public static string? NormalizeMoveCode(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length > 32)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder(9);
		foreach (char c in raw)
		{
			if (c >= '0' && c <= '9')
			{
				stringBuilder.Append(c);
			}
			else if (c != '-' && !char.IsWhiteSpace(c))
			{
				return null;
			}
		}
		if (stringBuilder.Length != 9)
		{
			return null;
		}
		return stringBuilder.ToString();
	}

	public static string? NormalizeTransferKey(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length > 40)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder(12);
		string text = raw.Trim();
		foreach (char c in text)
		{
			bool flag;
			switch (c)
			{
			case ' ':
			case '-':
			case '.':
			case '_':
			case '–':
			case '—':
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				char value = char.ToUpperInvariant(c);
				if ("ABCDEFGHJKLMNPQRSTUVWXYZ23456789".IndexOf(value) < 0)
				{
					return null;
				}
				stringBuilder.Append(value);
				if (stringBuilder.Length > 12)
				{
					return null;
				}
			}
		}
		string text2 = stringBuilder.ToString();
		if (text2.Length != 12)
		{
			return null;
		}
		return $"{text2.Substring(0, 4)}-{text2.Substring(4, 4)}-{text2.Substring(8, 4)}";
	}

	public static string? NormalizeReward(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string text = raw.Trim();
		foreach (char c in text)
		{
			if (!char.IsWhiteSpace(c))
			{
				char c2 = char.ToUpperInvariant(c);
				bool flag;
				switch (c2)
				{
				case '-':
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
				case 'A':
				case 'B':
				case 'C':
				case 'D':
				case 'E':
				case 'F':
				case 'G':
				case 'H':
				case 'I':
				case 'J':
				case 'K':
				case 'L':
				case 'M':
				case 'N':
				case 'O':
				case 'P':
				case 'Q':
				case 'R':
				case 'S':
				case 'T':
				case 'U':
				case 'V':
				case 'W':
				case 'X':
				case 'Y':
				case 'Z':
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					return null;
				}
				stringBuilder.Append(c2);
				if (stringBuilder.Length > 16)
				{
					return null;
				}
			}
		}
		if (stringBuilder.Length < 8)
		{
			return null;
		}
		return stringBuilder.ToString();
	}

	public static string? ShapeConflict(string? code)
	{
		if (string.IsNullOrEmpty(code))
		{
			return "empty";
		}
		if (NormalizeVoucher(code) != null || code.StartsWith("DC-", StringComparison.Ordinal))
		{
			return "voucher";
		}
		if (NormalizeTransferKey(code) != null)
		{
			return "transfer_key";
		}
		if (NormalizeMoveCode(code) != null)
		{
			return "move_code";
		}
		if (code.Replace("-", "").Length <= 6)
		{
			return "discord";
		}
		if (code.Length >= 20 && code.Contains('-'))
		{
			return "uuid";
		}
		return null;
	}

	public static string Mask(string? raw)
	{
		string text = new string((from ch in (raw ?? "").Trim()
			where !char.IsControl(ch)
			select ch).Take(40).ToArray());
		if (text.Length <= 5)
		{
			return new string('*', text.Length);
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		for (int num = 0; num < text.Length; num++)
		{
			StringBuilder stringBuilder2 = stringBuilder;
			bool flag = num < 3 || num >= text.Length - 2;
			if (!flag)
			{
				char c = text[num];
				bool flag2 = ((c == ' ' || c == '-') ? true : false);
				flag = flag2;
			}
			stringBuilder2.Append(flag ? text[num] : '*');
		}
		return stringBuilder.ToString();
	}

	private static string AsciiCompact(string raw)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = raw.ToUpperInvariant();
		foreach (char c in text)
		{
			bool flag;
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case 'A':
			case 'B':
			case 'C':
			case 'D':
			case 'E':
			case 'F':
			case 'G':
			case 'H':
			case 'I':
			case 'J':
			case 'K':
			case 'L':
			case 'M':
			case 'N':
			case 'O':
			case 'P':
			case 'Q':
			case 'R':
			case 'S':
			case 'T':
			case 'U':
			case 'V':
			case 'W':
			case 'X':
			case 'Y':
			case 'Z':
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}
}

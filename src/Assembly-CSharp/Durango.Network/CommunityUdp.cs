using System;
using System.Globalization;
using System.Text;
using Durango.Logic.Clusters;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Durango.Network;

public static class CommunityUdp
{
	public sealed class Offer
	{
		public int Port;

		public byte[] Key;

		public string KeyHex;

		public int TtlSec;

		public int FallbackSec;

		public float ReceivedAt;

		public bool Used;

		public bool Expired => Time.realtimeSinceStartup - ReceivedAt > (float)TtlSec;
	}

	public const string SettingKey = "net_transport";

	public const string Auto = "auto";

	public const string TcpOnly = "tcp";

	public static readonly string[] Options;

	private const string BlockUntilPref = "community:udp_block_until";

	public const double BlockHours = 1.0;

	public const int KeepaliveSec = 10;

	public const int DisconnectTimeoutSec = 15;

	public const int DefaultFallbackSec = 3;

	public const int TcpKeepaliveSec = 20;

	public const int KeySize = 16;

	public const int MaxEntityIdLength = 64;

	public const byte RequestVersion = 1;

	public const int Channels = 3;

	public const byte ChannelReliable = 0;

	public const byte ChannelLossy = 2;

	public const uint MoveTypeCode = 4u;

	public const uint GetClockTypeCode = 4000u;

	public const uint VoiceUpTypeCode = 7300001u;

	public const int MoveResendMs = 250;

	public const int MoveResendMax = 20;

	public const int HelloIntervalSec = 10;

	public const int HelloBackoffCapSec = 30;

	public const int LossyReplyTimeoutSec = 10;

	public const int ClockWaitTimeoutSec = 3;

	public static float LastFallbackAt;

	public const float BadgeDimSec = 5f;

	public static Offer Pending { get; private set; }

	public static string LastEntryNote { get; private set; }

	public static bool RecentFallback => Time.realtimeSinceStartup - LastFallbackAt < 5f;

	public static bool IsTcpOnly
	{
		get
		{
			try
			{
				return PlayerPrefs.GetString("option:net_transport", "auto") == "tcp";
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static event Action TransportChanged;

	public static byte OutChannelOf(uint typeCode, bool lossyHint, int peerChannels)
	{
		if (peerChannels < 3)
		{
			return 0;
		}
		switch (typeCode)
		{
		case 4000u:
			return 2;
		case 4u:
		case 7300001u:
			if (!lossyHint)
			{
				return 0;
			}
			return 2;
		default:
			return 0;
		}
	}

	internal static void NotifyTransport(bool fellBack)
	{
		if (fellBack)
		{
			LastFallbackAt = Time.realtimeSinceStartup;
		}
		Action transportChanged = CommunityUdp.TransportChanged;
		if (transportChanged != null)
		{
			try
			{
				transportChanged();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
	}

	public static string BadgeText()
	{
		Connection frontendIfAny = Connections.FrontendIfAny;
		if (frontendIfAny == null || !frontendIfAny.Connected())
		{
			return string.Empty;
		}
		if (!frontendIfAny.UdpActive)
		{
			return "TCP";
		}
		return "UDP";
	}

	static CommunityUdp()
	{
		Options = new string[2] { "auto", "tcp" };
		LastFallbackAt = -1000f;
		LastEntryNote = "no /entry yet";
	}

	public static void Log(string message)
	{
		CommunityLog.Info("[Community][udp] " + message);
	}

	public static void ReadEntry(JObject entry)
	{
		Pending = null;
		if (entry == null)
		{
			LastEntryNote = "entry null";
			return;
		}
		if (!(entry["udp"] is JObject jObject))
		{
			LastEntryNote = "no udp in /entry (server switch off or not offered)";
			Log(LastEntryNote);
			return;
		}
		int num = ReadInt(jObject, "port", 0);
		int num2 = ReadInt(jObject, "ttl", 60);
		int num3 = ReadInt(jObject, "fallback_sec", 3);
		string text = ((jObject["key"] != null) ? jObject["key"].ToString() : null);
		byte[] array = KeyFromHex(text);
		if (num <= 0 || num > 65535 || array == null)
		{
			LastEntryNote = "udp in /entry malformed (port " + num + " · key " + (text?.Length ?? 0) + " hex)";
			Log(LastEntryNote);
			return;
		}
		if (num3 <= 0)
		{
			num3 = 3;
		}
		Pending = new Offer
		{
			Port = num,
			Key = array,
			KeyHex = text,
			TtlSec = ((num2 > 0) ? num2 : 60),
			FallbackSec = num3,
			ReceivedAt = Time.realtimeSinceStartup
		};
		LastEntryNote = "udp offer port " + num + "/udp ttl " + Pending.TtlSec + "s fallback " + num3 + "s";
		Log(LastEntryNote);
	}

	private static int ReadInt(JObject obj, string key, int fallback)
	{
		try
		{
			JToken jToken = obj[key];
			if (jToken == null || jToken.Type == JTokenType.Null)
			{
				return fallback;
			}
			if (jToken.Type == JTokenType.Integer || jToken.Type == JTokenType.Float)
			{
				return jToken.Value<int>();
			}
			if (int.TryParse(jToken.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
		}
		catch (Exception)
		{
		}
		return fallback;
	}

	public static int ReadJsonInt(string json, string key, int fallback)
	{
		try
		{
			if (string.IsNullOrEmpty(json))
			{
				return fallback;
			}
			return ReadInt(JObject.Parse(json), key, fallback);
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	public static uint ReadJsonUInt(string json, string key)
	{
		try
		{
			if (string.IsNullOrEmpty(json))
			{
				return 0u;
			}
			JToken jToken = JObject.Parse(json)[key];
			if (jToken == null || jToken.Type == JTokenType.Null)
			{
				return 0u;
			}
			long num = ((jToken.Type == JTokenType.Integer || jToken.Type == JTokenType.Float) ? jToken.Value<long>() : long.Parse(jToken.ToString(), CultureInfo.InvariantCulture));
			if (num <= 0)
			{
				return 0u;
			}
			return (uint)((num >= uint.MaxValue) ? uint.MaxValue : num);
		}
		catch (Exception)
		{
			return 0u;
		}
	}

	public static string ReadJsonString(string json, string key)
	{
		try
		{
			if (string.IsNullOrEmpty(json))
			{
				return null;
			}
			JToken jToken = JObject.Parse(json)[key];
			if (jToken == null || jToken.Type == JTokenType.Null)
			{
				return null;
			}
			return jToken.ToString();
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static void OnReady(Connection frontend, string entityId)
	{
		if (frontend != null)
		{
			Offer pending = Pending;
			double remainSec;
			if (pending == null)
			{
				Log("ready: no udp offer — stays on TCP (" + LastEntryNote + ")");
			}
			else if (IsTcpOnly)
			{
				Log("ready: setting «TCP only» — UDP not tried");
			}
			else if (IsBlocked(out remainSec))
			{
				Log("ready: UDP skipped on this machine for another " + (int)remainSec + " s (earlier connect failed)");
			}
			else if (string.IsNullOrEmpty(entityId))
			{
				Log("ready: no entity id — stays on TCP");
			}
			else if (pending.Used || pending.Expired)
			{
				Log("ready: udp key " + (pending.Used ? "already used (reconnect without /entry)" : ("expired (ttl " + pending.TtlSec + " s)")) + " — asking the frontend for a new one (Hello)");
				frontend.BeginUdpHello(entityId, pending.FallbackSec);
			}
			else
			{
				pending.Used = true;
				frontend.BeginUdp(pending, entityId);
			}
		}
	}

	public static void OnSetting(string value, bool save)
	{
		if (save)
		{
			Log("setting net_transport = " + value);
			if (value == "tcp")
			{
				Connections.FrontendIfAny?.UdpForceFallback("setting tcp-only");
			}
		}
	}

	public static bool IsBlocked(out double remainSec)
	{
		remainSec = 0.0;
		try
		{
			string text = PlayerPrefs.GetString("community:udp_block_until", null);
			if (string.IsNullOrEmpty(text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return false;
			}
			double num = Times.UnixTimeNow();
			if (result <= num)
			{
				return false;
			}
			remainSec = result - num;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public static void BlockForAWhile(string reason)
	{
		try
		{
			PlayerPrefs.SetString("community:udp_block_until", (Times.UnixTimeNow() + 3600.0).ToString("F0", CultureInfo.InvariantCulture));
			PlayerPrefs.Save();
		}
		catch (Exception)
		{
		}
		Log("UDP not available (" + reason + ") — TCP only on this machine for the next " + 1.0 + " h");
	}

	public static void ClearBlock()
	{
		try
		{
			if (PlayerPrefs.HasKey("community:udp_block_until"))
			{
				PlayerPrefs.DeleteKey("community:udp_block_until");
			}
		}
		catch (Exception)
		{
		}
	}

	public static byte[] KeyFromHex(string hex)
	{
		if (string.IsNullOrEmpty(hex) || hex.Length != 32)
		{
			return null;
		}
		byte[] array = new byte[16];
		for (int i = 0; i < 16; i++)
		{
			int num = HexValue(hex[i * 2]);
			int num2 = HexValue(hex[i * 2 + 1]);
			if (num < 0 || num2 < 0)
			{
				return null;
			}
			array[i] = (byte)((num << 4) | num2);
		}
		return array;
	}

	private static int HexValue(char c)
	{
		if (c >= '0' && c <= '9')
		{
			return c - 48;
		}
		if (c >= 'a' && c <= 'f')
		{
			return c - 97 + 10;
		}
		if (c >= 'A' && c <= 'F')
		{
			return c - 65 + 10;
		}
		return -1;
	}

	public static byte[] BuildRequest(byte[] key, string entityId)
	{
		if (key == null || key.Length != 16)
		{
			throw new ArgumentException("udp key must be 16 bytes");
		}
		byte[] bytes = Encoding.ASCII.GetBytes(entityId ?? string.Empty);
		if (bytes.Length == 0 || bytes.Length > 64)
		{
			throw new ArgumentException("entity id empty or longer than 64");
		}
		for (int i = 0; i < bytes.Length; i++)
		{
			if (bytes[i] < 33 || bytes[i] > 126)
			{
				throw new ArgumentException("entity id has a character outside ASCII 0x21-0x7e");
			}
		}
		byte[] array = new byte[22 + bytes.Length];
		int num = 0;
		array[num++] = 68;
		array[num++] = 85;
		array[num++] = 68;
		array[num++] = 80;
		array[num++] = 1;
		Buffer.BlockCopy(key, 0, array, num, 16);
		num += 16;
		array[num++] = (byte)bytes.Length;
		Buffer.BlockCopy(bytes, 0, array, num, bytes.Length);
		return array;
	}

	public static string Label()
	{
		return CommunityDiscordLink.Lang switch
		{
			"th" => "การเช\u0e37\u0e48อมต\u0e48อ (UDP เร\u0e47วกว\u0e48าเม\u0e37\u0e48อเน\u0e47ตแกว\u0e48ง)",
			"id" => "Koneksi (UDP lebih cepat saat jaringan tidak stabil)",
			"ko" => "연결 방식 (UDP: 불안정한 네트워크에서 더 빠름)",
			_ => "Connection (UDP is faster on a shaky network)",
		};
	}

	public static string OptionLabel(string value)
	{
		string lang = CommunityDiscordLink.Lang;
		if (value == "tcp")
		{
			return lang switch
			{
				"th" => "TCP เท\u0e48าน\u0e31\u0e49น",
				"id" => "Hanya TCP",
				"ko" => "TCP만",
				_ => "TCP only",
			};
		}
		return lang switch
		{
			"th" => "อ\u0e31ตโนม\u0e31ต\u0e34 (UDP)",
			"id" => "Otomatis (UDP)",
			"ko" => "자동 (UDP)",
			_ => "Automatic (UDP)",
		};
	}
}

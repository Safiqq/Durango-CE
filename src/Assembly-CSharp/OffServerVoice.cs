using System;
using System.Collections.Generic;
using System.Globalization;
using Concentus.Enums;
using Concentus.Structs;
using Durango.Logic.InputSystem;
using Durango.Network;
using Durango.Render.Camera;
using Durango.System.Config;
using Durango.Utils;
using Messages;
using UnityEngine;

public class OffServerVoice : GameSystem<OffServerVoice>
{
	public const string Caps = "voice-chat";

	public const byte UpAudio = 0;

	public const byte UpEnd = 1;

	public const byte UpMuteList = 2;

	public const byte UpHello = 3;

	public const byte DownAudio = 0;

	public const byte DownEnd = 1;

	public const byte DownConfig = 2;

	public const byte DownMuted = 3;

	public const int SampleRate = 16000;

	public const int FrameSamples = 320;

	public const float TileUnits = 200f;

	private const float HelloEverySec = 60f;

	private const float MicKeepSec = 10f;

	private const float HoldDelaySec = 0.25f;

	private const int PreRollFrames = 15;

	private const float VadHangSec = 0.5f;

	private const float StreamIdleDestroySec = 15f;

	private const string PrefMode = "offserver.voice.mode";

	private const string PrefLevel = "offserver.voice.level";

	private const string PrefVolume = "offserver.voice.volume";

	private const string PrefSens = "offserver.voice.sens";

	private const string PrefMuted = "offserver.voice.muted";

	private const string PrefDeaf = "offserver.voice.deaf";

	public static readonly float[] SensitivityDb = new float[5] { -30f, -36f, -42f, -48f, -54f };

	public static float[] Ranges = new float[3] { 1f, 3f, 5f };

	private static int _serverDefaultLevel = 1;

	private static int _bitrate = 16000;

	private static int _framesPerPacket = 2;

	private static int _level = -1;

	private static HashSet<string> _muted;

	private static bool _prefsLoaded;

	private bool _wasReady;

	private float _nextHelloAt;

	private bool _pttKey;

	private bool _pttButton;

	private float _buttonDownAt = -1f;

	private bool _buttonHeld;

	private float _micKeepUntil;

	private AudioClip _micClip;

	private string _micDevice;

	private int _micFreq;

	private int _micReadPos;

	private float[] _micTemp = new float[0];

	private readonly List<float> _pcm = new List<float>(4096);

	private double _resamplePos;

	private float _resampleLast;

	private float _boxSum;

	private int _boxCount;

	private readonly Queue<float[]> _preRoll = new Queue<float[]>();

	private int _vadOverFrames;

	private float _vadLastVoiceAt = -100f;

	private OpusEncoder _encoder;

	private readonly byte[] _encodeBuf = new byte[512];

	private readonly List<byte> _packet = new List<byte>(512);

	private int _packetFrames;

	private uint _seq;

	private bool _permissionAsked;

	private bool _permissionPending;

	private volatile int _permissionResult;

	private float _micRetryAt;

	private float _listenerCheckAt;

	private AudioListener _ownListener;

	private float _lastRemoteAudioAt = -100f;

	private readonly Dictionary<string, OffServerVoiceStream> _streams = new Dictionary<string, OffServerVoiceStream>(StringComparer.Ordinal);

	private bool _debugTalk;

	private const float AgcTargetRms = 0.1f;

	private const float AgcMaxGain = 20f;

	private const float AgcNoiseRms = 0.0032f;

	private float _agcGain = 6f;

	public static bool ConfigReceived { get; private set; }

	public static bool ServerEnabled { get; private set; }

	public static int MaxSpeakers { get; private set; }

	public static int GmMutedHours { get; private set; }

	public static string GmMutedText { get; private set; }

	public static bool AutoMode { get; private set; }

	public static float Volume { get; private set; }

	public static int Sensitivity { get; private set; }

	public static bool Deaf { get; private set; }

	public static bool Talking { get; private set; }

	public static float MicLevelDb { get; private set; }

	public static string MicProblem { get; private set; }

	public static int Level => Mathf.Clamp((_level >= 0) ? _level : _serverDefaultLevel, 0, 2);

	public static float RangeTiles => Ranges[Level];

	public static bool Available
	{
		get
		{
			if (ConfigReceived && ServerEnabled)
			{
				return GameManager.IsReady;
			}
			return false;
		}
	}

	public static IEnumerable<OffServerVoiceStream> Streams
	{
		get
		{
			if (!GameSystem<OffServerVoice>.HasInstance())
			{
				return new OffServerVoiceStream[0];
			}
			return GameSystem<OffServerVoice>.Instance()._streams.Values;
		}
	}

	private bool PttHeld
	{
		get
		{
			if (!_pttKey && !_pttButton)
			{
				return _debugTalk;
			}
			return true;
		}
	}

	public static event Action Changed;

	public static bool IsMuted(string entityId)
	{
		LoadPrefs();
		if (!string.IsNullOrEmpty(entityId))
		{
			return _muted.Contains(entityId);
		}
		return false;
	}

	public static bool IsSpeaking(string entityId)
	{
		if (GameSystem<OffServerVoice>.HasInstance() && !string.IsNullOrEmpty(entityId) && GameSystem<OffServerVoice>.Instance()._streams.TryGetValue(entityId, out var value) && value != null)
		{
			return value.Speaking;
		}
		return false;
	}

	public static Dictionary<string, object> DebugState()
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary["available"] = Available;
		dictionary["config"] = ConfigReceived;
		dictionary["serverEnabled"] = ServerEnabled;
		dictionary["ranges"] = string.Join("/", Array.ConvertAll(Ranges, (float r) => r.ToString(CultureInfo.InvariantCulture)));
		dictionary["level"] = Level;
		dictionary["auto"] = AutoMode;
		dictionary["talking"] = Talking;
		dictionary["micDb"] = Math.Round(MicLevelDb, 1);
		dictionary["micProblem"] = MicProblem ?? string.Empty;
		dictionary["gmMutedHours"] = GmMutedHours;
		dictionary["deaf"] = Deaf;
		dictionary["volume"] = Volume;
		dictionary["panel"] = OffServerVoiceUI.PanelOpen;
		OffServerVoice offServerVoice = (GameSystem<OffServerVoice>.HasInstance() ? GameSystem<OffServerVoice>.Instance() : null);
		dictionary["micOn"] = offServerVoice != null && offServerVoice._micClip != null;
		dictionary["micFreq"] = ((offServerVoice != null) ? offServerVoice._micFreq : 0);
		dictionary["sent"] = (long)((offServerVoice != null) ? offServerVoice._seq : 0);
		dictionary["devices"] = string.Join(", ", Microphone.devices ?? new string[0]);
		dictionary["listener"] = UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null;
		List<object> list = new List<object>();
		foreach (OffServerVoiceStream stream in Streams)
		{
			if (stream != null)
			{
				list.Add(stream.Debug());
			}
		}
		dictionary["streams"] = list;
		dictionary["hudButton"] = OffServerVoiceUI.DebugMicButton();
		dictionary["headIcons"] = OffServerVoiceUI.DebugHeadIcons();
		return dictionary;
	}

	public static string DebugCommand(string[] args)
	{
		if (args == null || args.Length == 0)
		{
			return null;
		}
		string text = ((args.Length > 1) ? args[1] : string.Empty);
		switch (args[0])
		{
		case "panel":
			OffServerVoiceUI.TogglePanel();
			return "panel " + OffServerVoiceUI.PanelOpen;
		case "auto":
			SetAutoMode(text == "1");
			return "auto " + AutoMode;
		case "level":
		{
			if (int.TryParse(text, out var result2))
			{
				SetLevel(result2);
			}
			return "level " + Level;
		}
		case "talk":
			if (GameSystem<OffServerVoice>.HasInstance())
			{
				OffServerVoice offServerVoice = GameSystem<OffServerVoice>.Instance();
				offServerVoice._debugTalk = text == "1";
				if (offServerVoice._debugTalk)
				{
					offServerVoice.StartWanted();
				}
			}
			return "talk " + text;
		case "mute":
			if (args.Length > 2)
			{
				SetMuted(args[1], args[2] == "1");
				return "mute " + args[1] + " " + IsMuted(args[1]);
			}
			return "usage: mute <entityId> 0|1";
		case "deaf":
			SetDeaf(text == "1");
			return "deaf " + Deaf;
		case "volume":
		{
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				SetVolume(result);
			}
			return "volume " + Volume;
		}
		default:
			return "unknown: " + args[0];
		}
	}

	private void Awake()
	{
		LoadPrefs();
		MaxSpeakers = 4;
		GmMutedText = string.Empty;
		ConfigReceived = false;
		ServerEnabled = false;
		Connections.Frontend.On<VoiceDown>(OnVoiceDown);
		InputSystem inputSystem = GameSystem<InputSystem>.Instance();
		inputSystem.On(InputCommand.VoicePushToTalk, OnTalkKey);
		inputSystem.On(InputCommand.VoicePanel, OnPanelKey);
		inputSystem.On(InputCommand.VoiceRange, OnRangeKey);
	}

	private void OnDestroy()
	{
		StopMic();
		foreach (OffServerVoiceStream value in _streams.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
		_streams.Clear();
		Talking = false;
		ConfigReceived = false;
		ServerEnabled = false;
		RaiseChanged();
	}

	private static void LoadPrefs()
	{
		if (_prefsLoaded)
		{
			return;
		}
		_prefsLoaded = true;
		AutoMode = PlayerPrefs.GetInt("offserver.voice.mode", 0) == 1;
		_level = PlayerPrefs.GetInt("offserver.voice.level", -1);
		Volume = Mathf.Clamp(PlayerPrefs.GetFloat("offserver.voice.volume", 1f), 0f, 2f);
		Sensitivity = Mathf.Clamp(PlayerPrefs.GetInt("offserver.voice.sens", 3), 0, SensitivityDb.Length - 1);
		Deaf = PlayerPrefs.GetInt("offserver.voice.deaf", 0) == 1;
		_muted = new HashSet<string>(StringComparer.Ordinal);
		string[] array = PlayerPrefs.GetString("offserver.voice.muted", string.Empty).Split(',');
		foreach (string text in array)
		{
			if (text.Trim().Length > 0)
			{
				_muted.Add(text.Trim());
			}
		}
	}

	private static void RaiseChanged()
	{
		if (OffServerVoice.Changed != null)
		{
			try
			{
				OffServerVoice.Changed();
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
	}

	public static void SetAutoMode(bool auto)
	{
		LoadPrefs();
		AutoMode = auto;
		PlayerPrefs.SetInt("offserver.voice.mode", auto ? 1 : 0);
		PlayerPrefs.Save();
		if (auto && GameSystem<OffServerVoice>.HasInstance())
		{
			GameSystem<OffServerVoice>.Instance().EnsurePermission();
		}
		RaiseChanged();
	}

	public static void SetLevel(int level)
	{
		LoadPrefs();
		_level = Mathf.Clamp(level, 0, 2);
		PlayerPrefs.SetInt("offserver.voice.level", _level);
		PlayerPrefs.Save();
		RaiseChanged();
	}

	public static void SetVolume(float volume)
	{
		LoadPrefs();
		Volume = Mathf.Clamp(Mathf.Round(volume * 4f) / 4f, 0f, 2f);
		PlayerPrefs.SetFloat("offserver.voice.volume", Volume);
		PlayerPrefs.Save();
		RaiseChanged();
	}

	public static void SetSensitivity(int sensitivity)
	{
		LoadPrefs();
		Sensitivity = Mathf.Clamp(sensitivity, 0, SensitivityDb.Length - 1);
		PlayerPrefs.SetInt("offserver.voice.sens", Sensitivity);
		PlayerPrefs.Save();
		RaiseChanged();
	}

	public static void SetDeaf(bool deaf)
	{
		LoadPrefs();
		Deaf = deaf;
		PlayerPrefs.SetInt("offserver.voice.deaf", deaf ? 1 : 0);
		PlayerPrefs.Save();
		if (GameSystem<OffServerVoice>.HasInstance())
		{
			OffServerVoice offServerVoice = GameSystem<OffServerVoice>.Instance();
			if (deaf)
			{
				offServerVoice.ClearStreams();
			}
			offServerVoice.SendMuteList();
		}
		RaiseChanged();
	}

	public static void SetMuted(string entityId, bool muted)
	{
		LoadPrefs();
		if (string.IsNullOrEmpty(entityId) || entityId == GameManager.PlayerId || !(muted ? _muted.Add(entityId) : _muted.Remove(entityId)))
		{
			return;
		}
		while (_muted.Count > 200)
		{
			IEnumerator<string> enumerator = _muted.GetEnumerator();
			enumerator.MoveNext();
			_muted.Remove(enumerator.Current);
		}
		PlayerPrefs.SetString("offserver.voice.muted", string.Join(",", new List<string>(_muted).ToArray()));
		PlayerPrefs.Save();
		if (GameSystem<OffServerVoice>.HasInstance())
		{
			OffServerVoice offServerVoice = GameSystem<OffServerVoice>.Instance();
			if (muted)
			{
				offServerVoice.DropStream(entityId);
			}
			offServerVoice.SendMuteList();
		}
		RaiseChanged();
	}

	private void OnTalkKey(InputCommandMessage msg)
	{
		if ((msg.CurrentTrigger & Trigger.Down) != Trigger.None)
		{
			_pttKey = true;
			StartWanted();
		}
		else if ((msg.CurrentTrigger & Trigger.Up) != Trigger.None)
		{
			_pttKey = false;
		}
	}

	private void OnPanelKey(InputCommandMessage msg)
	{
		if ((msg.CurrentTrigger & Trigger.Down) != Trigger.None && Available)
		{
			OffServerVoiceUI.TogglePanel();
		}
	}

	private void OnRangeKey(InputCommandMessage msg)
	{
		if ((msg.CurrentTrigger & Trigger.Down) != Trigger.None && Available)
		{
			SetLevel((Level + 1) % 3);
			OffServerVoiceUI.FlashRange();
		}
	}

	public static void ButtonPress(bool pressed)
	{
		if (!GameSystem<OffServerVoice>.HasInstance())
		{
			return;
		}
		OffServerVoice offServerVoice = GameSystem<OffServerVoice>.Instance();
		if (pressed)
		{
			offServerVoice._buttonDownAt = Time.realtimeSinceStartup;
			offServerVoice._buttonHeld = true;
			offServerVoice.StartWanted();
			return;
		}
		bool pttButton = offServerVoice._pttButton;
		offServerVoice._buttonHeld = false;
		offServerVoice._pttButton = false;
		offServerVoice._buttonDownAt = -1f;
		if (!pttButton)
		{
			OffServerVoiceUI.TogglePanel();
		}
	}

	private void StartWanted()
	{
		if (Available)
		{
			if (GmMutedHours > 0)
			{
				UIManager.SystemMsg(string.Format(OffServerVoiceUI.Text("gm_muted"), GmMutedHours));
				return;
			}
			_micKeepUntil = Time.realtimeSinceStartup + 10f;
			EnsurePermission();
		}
	}

	private void Update()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		bool isReady = GameManager.IsReady;
		if (isReady != _wasReady)
		{
			_nextHelloAt = realtimeSinceStartup + 60f;
			if (!isReady)
			{
				StopTalking(sendEnd: true);
				ClearStreams();
			}
			_wasReady = isReady;
			RaiseChanged();
		}
		else if (isReady && ConfigReceived && realtimeSinceStartup >= _nextHelloAt)
		{
			SendHello();
		}
		if (_pttKey && (!Application.isFocused || !OffServerHotkeys.IsHeld("voice_talk", KeyCode.X)))
		{
			_pttKey = false;
		}
		if (_buttonHeld && !_pttButton && _buttonDownAt >= 0f && realtimeSinceStartup - _buttonDownAt >= 0.25f)
		{
			_pttButton = true;
		}
		bool num = Available && (AutoMode || PttHeld || realtimeSinceStartup < _micKeepUntil);
		if (PttHeld)
		{
			_micKeepUntil = realtimeSinceStartup + 10f;
		}
		int permissionResult = _permissionResult;
		if (permissionResult != 0)
		{
			_permissionResult = 0;
			_permissionPending = false;
			_micRetryAt = 0f;
			SetMicProblem((permissionResult == 1) ? null : OffServerVoiceUI.Text("perm_denied"));
			if (permissionResult == 2)
			{
				UIManager.SystemMsg(MicProblem, 4f);
			}
		}
		if (num && !HasPermission() && !_permissionAsked)
		{
			EnsurePermission();
		}
		if (num && HasPermission())
		{
			if (_micClip == null && realtimeSinceStartup >= _micRetryAt)
			{
				_micRetryAt = realtimeSinceStartup + 5f;
				StartMic();
			}
			PumpMic(realtimeSinceStartup);
		}
		else
		{
			if (_micClip != null)
			{
				StopMic();
			}
			if (Talking)
			{
				StopTalking(sendEnd: true);
			}
		}
		UpdateStreams(realtimeSinceStartup);
		if (realtimeSinceStartup >= _listenerCheckAt)
		{
			_listenerCheckAt = realtimeSinceStartup + 2f;
			CheckListener();
		}
	}

	private void LateUpdate()
	{
		try
		{
			OffServerVoiceUI.TickRings();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private bool HasPermission()
	{
		return true;
	}

	private void EnsurePermission()
	{
	}

	private void StartMic()
	{
		try
		{
			string[] devices = Microphone.devices;
			if (devices == null || devices.Length == 0)
			{
				SetMicProblem(OffServerVoiceUI.Text("no_mic"));
				_micKeepUntil = 0f;
				return;
			}
			_micDevice = null;
			Microphone.GetDeviceCaps(_micDevice, out var minFreq, out var maxFreq);
			_micFreq = (((minFreq == 0 && maxFreq == 0) || (minFreq <= 16000 && 16000 <= maxFreq)) ? 16000 : ((maxFreq > 0) ? maxFreq : 48000));
			_micClip = Microphone.Start(_micDevice, loop: true, 1, _micFreq);
			if (_micClip == null)
			{
				SetMicProblem(OffServerVoiceUI.Text("no_mic"));
				return;
			}
			_micFreq = _micClip.frequency;
			_micReadPos = Microphone.GetPosition(_micDevice);
			if (_micReadPos < 0)
			{
				_micReadPos = 0;
			}
			_pcm.Clear();
			_preRoll.Clear();
			_resamplePos = 0.0;
			if (_encoder == null)
			{
				_encoder = OpusEncoder.Create(16000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
				_encoder.Complexity = (Application.isMobilePlatform ? 3 : 5);
				_encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
				_encoder.UseVBR = true;
			}
			_encoder.Bitrate = _bitrate;
			SetMicProblem(null);
			OffServerLog.Info("[Voice] mic on (" + _micFreq + " Hz, " + devices[0] + ")");
		}
		catch (Exception ex)
		{
			_micClip = null;
			SetMicProblem(OffServerVoiceUI.Text("no_mic"));
			OffServerLog.Info("[Voice] mic start failed: " + ex.Message);
		}
	}

	private void StopMic()
	{
		if (_micClip != null)
		{
			try
			{
				Microphone.End(_micDevice);
			}
			catch (Exception)
			{
			}
			_micClip = null;
		}
		_pcm.Clear();
		_preRoll.Clear();
		_vadOverFrames = 0;
		MicLevelDb = -100f;
	}

	private static void SetMicProblem(string problem)
	{
		if (MicProblem != problem)
		{
			MicProblem = problem;
			RaiseChanged();
		}
	}

	private void PumpMic(float now)
	{
		if (_micClip == null)
		{
			return;
		}
		int position = Microphone.GetPosition(_micDevice);
		if (position < 0)
		{
			return;
		}
		int samples = _micClip.samples;
		int num = position - _micReadPos;
		if (num < 0)
		{
			num += samples;
		}
		if (num > 0)
		{
			if (_micTemp.Length != num)
			{
				_micTemp = new float[num];
			}
			_micClip.GetData(_micTemp, _micReadPos);
			_micReadPos = position;
			AppendResampled(_micTemp, num);
		}
		while (_pcm.Count >= 320)
		{
			float[] array = new float[320];
			_pcm.CopyTo(0, array, 0, 320);
			_pcm.RemoveRange(0, 320);
			ProcessFrame(array, now);
		}
	}

	private void AppendResampled(float[] input, int count)
	{
		if (_micFreq == 16000)
		{
			for (int i = 0; i < count; i++)
			{
				_pcm.Add(input[i]);
			}
			return;
		}
		if (_micFreq % 16000 == 0)
		{
			int num = _micFreq / 16000;
			for (int j = 0; j < count; j++)
			{
				_boxSum += input[j];
				if (++_boxCount >= num)
				{
					_pcm.Add(_boxSum / (float)num);
					_boxSum = 0f;
					_boxCount = 0;
				}
			}
			return;
		}
		double num2 = (double)_micFreq / 16000.0;
		while (_resamplePos < (double)count)
		{
			int num3 = (int)_resamplePos;
			double num4 = _resamplePos - (double)num3;
			float num5 = ((num3 == 0) ? _resampleLast : input[num3 - 1]);
			float num6 = input[num3];
			_pcm.Add((float)((double)num5 + (double)(num6 - num5) * num4));
			_resamplePos += num2;
		}
		_resamplePos -= count;
		_resampleLast = input[count - 1];
	}

	private void ProcessFrame(float[] frame, float now)
	{
		double num = 0.0;
		for (int i = 0; i < frame.Length; i++)
		{
			num += (double)(frame[i] * frame[i]);
		}
		float num2 = (float)Math.Sqrt(num / (double)frame.Length);
		MicLevelDb = ((num2 > 1E-05f) ? (20f * Mathf.Log10(num2)) : (-100f));
		bool flag = PttHeld;
		if (!flag && AutoMode)
		{
			float num3 = SensitivityDb[Sensitivity];
			if (now - _lastRemoteAudioAt < 0.3f)
			{
				num3 += 8f;
			}
			if (MicLevelDb > num3)
			{
				_vadOverFrames++;
				if (_vadOverFrames >= 2)
				{
					_vadLastVoiceAt = now;
				}
			}
			else
			{
				_vadOverFrames = 0;
			}
			flag = now - _vadLastVoiceAt < 0.5f;
		}
		if (!flag || !Available || GmMutedHours != 0)
		{
			if (Talking)
			{
				StopTalking(sendEnd: true);
			}
			_preRoll.Enqueue(frame);
			while (_preRoll.Count > (_buttonHeld ? 15 : 3))
			{
				_preRoll.Dequeue();
			}
			return;
		}
		if (!Talking)
		{
			Talking = true;
			RaiseChanged();
			while (_preRoll.Count > 0)
			{
				EncodeAndQueue(_preRoll.Dequeue());
			}
		}
		EncodeAndQueue(frame);
	}

	private void ApplyAgc(float[] frame)
	{
		double num = 0.0;
		for (int i = 0; i < frame.Length; i++)
		{
			num += (double)(frame[i] * frame[i]);
		}
		float num2 = (float)Math.Sqrt(num / (double)frame.Length);
		if (num2 > 0.0032f)
		{
			float num3 = Mathf.Clamp(0.1f / num2, 1f, 20f);
			_agcGain = ((num3 < _agcGain) ? Mathf.Lerp(_agcGain, num3, 0.5f) : Mathf.Lerp(_agcGain, num3, 0.06f));
		}
		for (int j = 0; j < frame.Length; j++)
		{
			float num4 = frame[j] * _agcGain;
			float num5 = Mathf.Abs(num4);
			if (num5 > 0.7f)
			{
				num4 = Mathf.Sign(num4) * (0.7f + 0.3f * (1f - Mathf.Exp((0.7f - num5) / 0.3f)));
			}
			frame[j] = num4;
		}
	}

	private void EncodeAndQueue(float[] frame)
	{
		ApplyAgc(frame);
		int num;
		try
		{
			num = _encoder.Encode(frame, 0, 320, _encodeBuf, 0, 255);
		}
		catch (Exception ex)
		{
			OffServerLog.Info("[Voice] encode failed: " + ex.Message);
			return;
		}
		if (num > 0 && num <= 255)
		{
			_packet.Add((byte)num);
			for (int i = 0; i < num; i++)
			{
				_packet.Add(_encodeBuf[i]);
			}
			_packetFrames++;
			if (_packetFrames >= Mathf.Max(1, _framesPerPacket))
			{
				FlushPacket();
			}
		}
	}

	private void FlushPacket()
	{
		if (_packetFrames != 0)
		{
			Send(new VoiceUp
			{
				Kind = 0,
				Level = (byte)Level,
				Seq = ++_seq,
				Data = _packet.ToArray(),
				Text = string.Empty
			});
			_packet.Clear();
			_packetFrames = 0;
		}
	}

	private void StopTalking(bool sendEnd)
	{
		if (!Talking)
		{
			_packet.Clear();
			_packetFrames = 0;
			return;
		}
		FlushPacket();
		Talking = false;
		if (sendEnd)
		{
			Send(new VoiceUp
			{
				Kind = 1,
				Level = (byte)Level,
				Seq = _seq,
				Data = new byte[0],
				Text = string.Empty
			});
		}
		RaiseChanged();
	}

	private static void Send(VoiceUp msg)
	{
		try
		{
			Connection frontend = Connections.Frontend;
			if (frontend != null && frontend.Connected())
			{
				if (msg.Kind == 0)
				{
					frontend.SendLossy(msg, noReply: true);
				}
				else
				{
					frontend.Send(msg, noReply: true);
				}
			}
		}
		catch (Exception ex)
		{
			OffServerLog.Info("[Voice] send failed: " + ex.Message);
		}
	}

	private void SendHello()
	{
		_nextHelloAt = Time.realtimeSinceStartup + 60f;
		Send(new VoiceUp
		{
			Kind = 3,
			Data = new byte[0],
			Text = string.Empty
		});
	}

	private void SendMuteList()
	{
		LoadPrefs();
		if (ConfigReceived)
		{
			Send(new VoiceUp
			{
				Kind = 2,
				Level = (Deaf ? ((byte)1) : ((byte)0)),
				Data = new byte[0],
				Text = string.Join(",", new List<string>(_muted).ToArray())
			});
		}
	}

	private void OnVoiceDown(VoiceDown msg, PacketHeader header)
	{
		try
		{
			switch (msg.Kind)
			{
			case 2:
				ApplyConfig(msg.Text);
				SendMuteList();
				break;
			case 3:
				GmMutedHours = msg.Level;
				GmMutedText = msg.Text ?? string.Empty;
				if (GmMutedHours > 0 && Talking)
				{
					StopTalking(sendEnd: false);
				}
				RaiseChanged();
				break;
			case 0:
				OnAudio(msg);
				break;
			case 1:
			{
				if (!string.IsNullOrEmpty(msg.Speaker) && _streams.TryGetValue(msg.Speaker, out var value) && value != null)
				{
					value.End();
				}
				break;
			}
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private void ApplyConfig(string text)
	{
		bool serverEnabled = ServerEnabled;
		string[] array = (text ?? string.Empty).Split(';');
		foreach (string text2 in array)
		{
			int num = text2.IndexOf('=');
			if (num <= 0)
			{
				continue;
			}
			string text3 = text2.Substring(0, num).Trim();
			string text4 = text2.Substring(num + 1).Trim();
			int result;
			switch (text3)
			{
			case "enabled":
				ServerEnabled = text4 == "1";
				break;
			case "ranges":
			{
				string[] array2 = text4.Split(',');
				if (array2.Length == 3)
				{
					float[] array3 = new float[3];
					bool flag = true;
					for (int j = 0; j < 3; j++)
					{
						flag &= float.TryParse(array2[j], NumberStyles.Float, CultureInfo.InvariantCulture, out array3[j]) && array3[j] > 0f;
					}
					if (flag)
					{
						Ranges = array3;
					}
				}
				break;
			}
			case "default":
				if (int.TryParse(text4, out result))
				{
					_serverDefaultLevel = Mathf.Clamp(result, 0, 2);
				}
				break;
			case "bitrate":
				if (int.TryParse(text4, out result))
				{
					_bitrate = Mathf.Clamp(result, 6000, 64000);
					if (_encoder != null)
					{
						_encoder.Bitrate = _bitrate;
					}
				}
				break;
			case "frames":
				if (int.TryParse(text4, out result))
				{
					_framesPerPacket = Mathf.Clamp(result, 1, 6);
				}
				break;
			case "max_speakers":
				if (int.TryParse(text4, out result))
				{
					MaxSpeakers = Mathf.Clamp(result, 1, 16);
				}
				break;
			}
		}
		if (!ConfigReceived || serverEnabled != ServerEnabled)
		{
			OffServerLog.Info("[Voice] config: " + text);
		}
		ConfigReceived = true;
		if (!ServerEnabled)
		{
			StopTalking(sendEnd: true);
			ClearStreams();
		}
		RaiseChanged();
	}

	private void OnAudio(VoiceDown msg)
	{
		LoadPrefs();
		if (!Deaf && !string.IsNullOrEmpty(msg.Speaker) && !(msg.Speaker == GameManager.PlayerId) && !_muted.Contains(msg.Speaker) && msg.Data != null && msg.Data.Length >= 2)
		{
			if (!_streams.TryGetValue(msg.Speaker, out var value) || value == null)
			{
				GameObject obj = new GameObject("Voice " + msg.Speaker);
				obj.transform.SetParent(base.transform, worldPositionStays: false);
				value = obj.AddComponent<OffServerVoiceStream>();
				value.Init(msg.Speaker);
				_streams[msg.Speaker] = value;
				EnsureListener();
			}
			value.Feed(msg.Seq, msg.Level, msg.Dist, msg.Data);
			_lastRemoteAudioAt = Time.realtimeSinceStartup;
		}
	}

	private void UpdateStreams(float now)
	{
		if (_streams.Count == 0)
		{
			return;
		}
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		Vector3 listener = ((localPlayer != null && localPlayer.MainTransform != null) ? localPlayer.MainTransform.position : Vector3.zero);
		Vector3 right = Vector3.right;
		try
		{
			Camera camera = (Singleton<MainCamera>.Exist() ? Singleton<MainCamera>.Instance().Camera : null);
			if (camera != null)
			{
				right = camera.transform.right;
			}
		}
		catch (Exception)
		{
		}
		List<string> list = null;
		foreach (KeyValuePair<string, OffServerVoiceStream> stream in _streams)
		{
			OffServerVoiceStream value = stream.Value;
			if (value == null || now - value.LastPacketAt > 15f)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(stream.Key);
			}
			else
			{
				value.UpdateGains(listener, right, localPlayer != null);
				if (value.Speaking)
				{
					_lastRemoteAudioAt = Mathf.Max(_lastRemoteAudioAt, value.LastPacketAt);
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			DropStream(item);
		}
	}

	private void DropStream(string entityId)
	{
		if (_streams.TryGetValue(entityId, out var value))
		{
			_streams.Remove(entityId);
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
	}

	private void ClearStreams()
	{
		foreach (string item in new List<string>(_streams.Keys))
		{
			DropStream(item);
		}
	}

	private void EnsureListener()
	{
		if (!(_ownListener != null))
		{
			AudioListener audioListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
			if (audioListener == null || !audioListener.isActiveAndEnabled)
			{
				_ownListener = base.gameObject.AddComponent<AudioListener>();
			}
		}
	}

	private void CheckListener()
	{
		if (_ownListener != null)
		{
			if (UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length > 1)
			{
				UnityEngine.Object.Destroy(_ownListener);
				_ownListener = null;
			}
		}
		else if (_streams.Count > 0)
		{
			EnsureListener();
		}
	}
}

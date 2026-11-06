using System;
using System.Collections.Generic;
using Concentus.Structs;
using Durango.Utils;
using UnityEngine;

public class CommunityVoiceStream : MonoBehaviour
{
	private const int Capacity = 16000;

	private const int PrebufferSamples = 960;

	private const int MaxBufferedSamples = 4800;

	private const int KeepAfterSkip = 1920;

	private readonly float[] _ring = new float[16000];

	private readonly object _gate = new object();

	private int _read;

	private int _count;

	private bool _prebuffering = true;

	private double _frac;

	private double _step = 1.0 / 3.0;

	private volatile float _gainL;

	private volatile float _gainR;

	private float _curL;

	private float _curR;

	private volatile int _filterCalls;

	private OpusDecoder _decoder;

	private readonly float[] _decoded = new float[960];

	private bool _hasSeq;

	private uint _lastSeq;

	private int _lastFrames = 2;

	private AudioSource _source;

	private float _startedAt;

	private bool _clipFallback;

	public string SpeakerId { get; private set; }

	public float LastPacketAt { get; private set; }

	public float LastAudibleAt { get; private set; }

	public int Level { get; private set; }

	public float ServerDist { get; private set; }

	public float Distance { get; private set; }

	public bool Ended { get; private set; }

	public int Packets { get; private set; }

	public int FramesDecoded { get; private set; }

	public bool Speaking
	{
		get
		{
			if (!Ended)
			{
				return Time.realtimeSinceStartup - LastPacketAt < 0.5f;
			}
			return false;
		}
	}

	public Dictionary<string, object> Debug()
	{
		int count;
		lock (_gate)
		{
			count = _count;
		}
		return new Dictionary<string, object>
		{
			["speaker"] = SpeakerId,
			["level"] = Level,
			["speaking"] = Speaking,
			["packets"] = Packets,
			["frames"] = FramesDecoded,
			["filterCalls"] = _filterCalls,
			["bufferedMs"] = count * 1000 / 16000,
			["dist"] = Math.Round(Distance, 2),
			["serverDist"] = Math.Round(ServerDist, 2),
			["gainL"] = Math.Round(_gainL, 2),
			["gainR"] = Math.Round(_gainR, 2),
			["clipFallback"] = _clipFallback
		};
	}

	public void Init(string speakerId)
	{
		SpeakerId = speakerId;
		_decoder = OpusDecoder.Create(16000, 1);
		int outputSampleRate = AudioSettings.outputSampleRate;
		_step = 16000.0 / (double)((outputSampleRate > 0) ? outputSampleRate : 48000);
		_source = base.gameObject.AddComponent<AudioSource>();
		_source.playOnAwake = false;
		_source.loop = true;
		_source.spatialBlend = 0f;
		_source.volume = 1f;
		_source.priority = 64;
		_source.Play();
		_startedAt = Time.realtimeSinceStartup;
		LastPacketAt = Time.realtimeSinceStartup;
	}

	public void Feed(uint seq, byte level, float dist, byte[] data)
	{
		Level = Mathf.Clamp(level, 0, 2);
		ServerDist = dist;
		Packets++;
		LastPacketAt = Time.realtimeSinceStartup;
		Ended = false;
		if (_hasSeq && seq > _lastSeq + 1 && seq - _lastSeq < 6)
		{
			for (uint num = _lastSeq + 1; num < seq; num++)
			{
				Decode(null, 0, 0, 320 * _lastFrames);
			}
		}
		if (!_hasSeq || seq > _lastSeq || _lastSeq - seq > 1000)
		{
			_lastSeq = seq;
			_hasSeq = true;
		}
		int num2 = 0;
		int num3 = 0;
		while (num2 < data.Length)
		{
			int num4 = data[num2];
			if (num4 == 0 || num2 + 1 + num4 > data.Length)
			{
				break;
			}
			Decode(data, num2 + 1, num4, _decoded.Length);
			num2 += 1 + num4;
			num3++;
		}
		if (num3 > 0)
		{
			_lastFrames = num3;
		}
		if (!_clipFallback && _filterCalls == 0 && Time.realtimeSinceStartup - _startedAt > 1f)
		{
			_clipFallback = true;
			int outputSampleRate = AudioSettings.outputSampleRate;
			_source.clip = AudioClip.Create("voice-silence", (outputSampleRate > 0) ? outputSampleRate : 48000, 1, (outputSampleRate > 0) ? outputSampleRate : 48000, stream: false);
			_source.Play();
		}
	}

	private void Decode(byte[] data, int offset, int length, int frameSize)
	{
		int num;
		try
		{
			num = _decoder.Decode(data, offset, length, _decoded, 0, Mathf.Min(frameSize, _decoded.Length));
		}
		catch (Exception)
		{
			return;
		}
		if (num <= 0)
		{
			return;
		}
		if (data != null)
		{
			FramesDecoded++;
		}
		lock (_gate)
		{
			for (int i = 0; i < num; i++)
			{
				int num2 = (_read + _count) % 16000;
				_ring[num2] = _decoded[i];
				if (_count < 16000)
				{
					_count++;
				}
				else
				{
					_read = (_read + 1) % 16000;
				}
			}
			if (_count > 4800)
			{
				int num3 = _count - 1920;
				_read = (_read + num3) % 16000;
				_count -= num3;
			}
		}
	}

	public void End()
	{
		Ended = true;
	}

	public void UpdateGains(Vector3 listener, Vector3 cameraRight, bool hasListener)
	{
		float num = ServerDist;
		float num2 = 0f;
		PlayerBehavior playerBehavior = null;
		try
		{
			if (Singleton<PlayerManager>.Exist())
			{
				playerBehavior = Singleton<PlayerManager>.Instance().GetPlayer(SpeakerId);
			}
		}
		catch (Exception)
		{
		}
		if (hasListener && playerBehavior != null && playerBehavior.MainTransform != null)
		{
			Vector3 vector = playerBehavior.MainTransform.position - listener;
			vector.y = 0f;
			num = vector.magnitude / 200f;
			Vector3 vector2 = cameraRight;
			vector2.y = 0f;
			if (vector.sqrMagnitude > 1f && vector2.sqrMagnitude > 1E-06f)
			{
				num2 = Vector3.Dot(vector.normalized, vector2.normalized) * Mathf.Clamp01(num / 1.5f) * 0.85f;
			}
		}
		Distance = num;
		float num3 = CommunityVoice.Ranges[Level] + 0.5f;
		float num4 = ((num <= 0.5f) ? 1f : Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((num - 0.5f) / Mathf.Max(0.5f, num3 - 0.5f)))) * CommunityVoice.Volume;
		_gainL = num4 * Mathf.Min(1f, 1f - num2);
		_gainR = num4 * Mathf.Min(1f, 1f + num2);
		if (Speaking)
		{
			LastAudibleAt = Time.realtimeSinceStartup;
		}
	}

	private void OnAudioFilterRead(float[] data, int channels)
	{
		_filterCalls++;
		if (channels <= 0)
		{
			return;
		}
		int num = data.Length / channels;
		float gainL = _gainL;
		float gainR = _gainR;
		float num2 = (gainL - _curL) / (float)Mathf.Max(1, num);
		float num3 = (gainR - _curR) / (float)Mathf.Max(1, num);
		lock (_gate)
		{
			if (_prebuffering && _count >= 960)
			{
				_prebuffering = false;
			}
			for (int i = 0; i < num; i++)
			{
				float num4 = 0f;
				if (!_prebuffering)
				{
					if (_count >= 2)
					{
						float num5 = _ring[_read];
						float num6 = _ring[(_read + 1) % 16000];
						num4 = num5 + (num6 - num5) * (float)_frac;
						_frac += _step;
						while (_frac >= 1.0 && _count >= 2)
						{
							_frac -= 1.0;
							_read = (_read + 1) % 16000;
							_count--;
						}
					}
					else
					{
						_prebuffering = true;
					}
				}
				_curL += num2;
				_curR += num3;
				int num7 = i * channels;
				if (channels == 1)
				{
					data[num7] = num4 * (_curL + _curR) * 0.5f;
					continue;
				}
				data[num7] = num4 * _curL;
				data[num7 + 1] = num4 * _curR;
				for (int j = 2; j < channels; j++)
				{
					data[num7 + j] = 0f;
				}
			}
		}
		_curL = gainL;
		_curR = gainR;
	}
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using Messages;
using UnityEngine;

namespace Durango.Network;

public sealed class CommunityUdpTransport
{
	public enum Phase
	{
		Connecting,
		Udp,
		Tcp,
		Closed
	}

	private struct HeldFrame
	{
		public uint Tseq;

		public byte Channel;

		public byte[] Data;
	}

	private const int MaxUnacked = 4096;

	private const long MaxReplayBytes = 1048576L;

	private const int MaxInboundHold = 8192;

	private const int SeqSize = 4;

	private const int Channels = 3;

	private readonly Connection _conn;

	private readonly EventBasedNetListener _listener;

	private readonly NetManager _manager;

	private IPEndPoint _target;

	private byte[] _request;

	private readonly float _fallbackSec;

	private readonly string _entityId;

	private NetPeer _peer;

	private Phase _phase = Phase.Closed;

	private float _startedAt;

	private float _connectedAt;

	private float _lastTcpKeepaliveAt;

	private bool _outOnUdp;

	private uint _outTseq;

	private readonly List<KeyValuePair<uint, byte[]>> _unacked = new List<KeyValuePair<uint, byte[]>>();

	private int _peerChannels = 1;

	private byte[] _pendingMove;

	private uint _pendingMoveTseq;

	private float _pendingMoveSentAt;

	private int _pendingMoveTries;

	private bool _helloArmed;

	private float _nextHelloAt;

	private float _lastHelloAt = -1000f;

	private bool _inOnUdp;

	private readonly uint[] _inMaxTseq = new uint[3];

	private readonly List<HeldFrame> _hold = new List<HeldFrame>();

	public long FramesSent;

	public long FramesRecv;

	public long BytesSent;

	public long BytesRecv;

	public long BadRecv;

	public long Replayed;

	public long Held;

	public int Fallbacks;

	public string LastReason = string.Empty;

	public long Ch2Sent;

	public long Ch2Recv;

	public long MoveSent;

	public long MoveResends;

	public long MoveEscalated;

	public long MoveAcks;

	public uint MoveAckTseq;

	public int Hellos;

	public int Offers;

	public string LastOfferNote = string.Empty;

	public int SimLossPct;

	public int SimDelayMs;

	public long SimDropped;

	public long SimDelayed;

	private readonly global::System.Random _simRandom = new global::System.Random(7);

	private readonly List<KeyValuePair<float, byte[]>> _simQueue = new List<KeyValuePair<float, byte[]>>();

	public Phase Current => _phase;

	public bool Active => _phase == Phase.Udp;

	public bool OutOnUdp => _outOnUdp;

	public bool InOnUdp => _inOnUdp;

	public int RttMs
	{
		get
		{
			if (_peer == null)
			{
				return 0;
			}
			return _peer.RoundTripTime;
		}
	}

	public long PacketLoss
	{
		get
		{
			if (_peer == null || _peer.Statistics == null)
			{
				return 0L;
			}
			return _peer.Statistics.PacketLoss;
		}
	}

	public int Unacked => _unacked.Count;

	public IPEndPoint Target => _target;

	public float UpSeconds
	{
		get
		{
			if (_phase != Phase.Udp)
			{
				return 0f;
			}
			return Time.realtimeSinceStartup - _connectedAt;
		}
	}

	public int PeerChannels => _peerChannels;

	public bool MovePending => _pendingMove != null;

	public int MovePendingTries
	{
		get
		{
			if (_pendingMove == null)
			{
				return 0;
			}
			return _pendingMoveTries;
		}
	}

	public bool HelloArmed => _helloArmed;

	public int NextHelloInSec
	{
		get
		{
			if (!_helloArmed)
			{
				return -1;
			}
			return Mathf.Max(0, (int)(_nextHelloAt - Time.realtimeSinceStartup));
		}
	}

	public CommunityUdpTransport(Connection conn, IPEndPoint target, byte[] request, int fallbackSec, int keepaliveSec, int disconnectSec, string entityId)
	{
		_conn = conn;
		_target = target;
		_request = request;
		_entityId = entityId;
		_fallbackSec = Mathf.Max(1f, fallbackSec);
		_listener = new EventBasedNetListener();
		_listener.PeerConnectedEvent += OnPeerConnected;
		_listener.PeerDisconnectedEvent += OnPeerDisconnected;
		_listener.NetworkReceiveEvent += OnNetworkReceive;
		_listener.DeliveryEvent += OnDelivered;
		_listener.NetworkErrorEvent += OnNetworkError;
		_manager = new NetManager(_listener)
		{
			AutoRecycle = true,
			EnableStatistics = true,
			UnconnectedMessagesEnabled = false,
			IPv6Enabled = false,
			UnsyncedEvents = false,
			ChannelsCount = 3,
			PingInterval = Mathf.Max(1000, keepaliveSec * 1000),
			DisconnectTimeout = Mathf.Max(2000, disconnectSec * 1000),
			ReconnectDelay = 500,
			MaxConnectAttempts = Mathf.Max(2, fallbackSec * 2)
		};
	}

	public bool Start()
	{
		if (_phase == Phase.Connecting || _phase == Phase.Udp)
		{
			return false;
		}
		if (_request == null || _target == null || _target.Port <= 0)
		{
			LastReason = "no udp offer to connect with";
			_phase = Phase.Tcp;
			return false;
		}
		try
		{
			_peer = null;
			_hold.Clear();
			_inOnUdp = false;
			_outOnUdp = false;
			_pendingMove = null;
			if (!_manager.Start())
			{
				LastReason = "udp socket start failed";
				CommunityUdp.Log(LastReason + " — stays on TCP");
				_phase = Phase.Tcp;
				CommunityUdp.BlockForAWhile(LastReason);
				CommunityUdp.NotifyTransport(fellBack: false);
				return false;
			}
			_phase = Phase.Connecting;
			_startedAt = Time.realtimeSinceStartup;
			NetDataWriter netDataWriter = new NetDataWriter();
			netDataWriter.Put(_request);
			_peer = _manager.Connect(_target, netDataWriter);
			if (_peer == null)
			{
				GiveUpConnect("connect returned no peer");
				return false;
			}
			string[] obj = new string[5]
			{
				"connecting to ",
				_target?.ToString(),
				" (give up after ",
				null,
				null
			};
			float fallbackSec = _fallbackSec;
			obj[3] = fallbackSec.ToString();
			obj[4] = " s)";
			CommunityUdp.Log(string.Concat(obj));
			CommunityUdp.NotifyTransport(fellBack: false);
			return true;
		}
		catch (Exception ex)
		{
			LastReason = "start failed: " + ex.Message;
			CommunityUdp.Log(LastReason);
			StopManager();
			_phase = Phase.Tcp;
			CommunityUdp.BlockForAWhile(LastReason);
			CommunityUdp.NotifyTransport(fellBack: false);
			return false;
		}
	}

	public void BeginHelloOnly()
	{
		if (_phase == Phase.Closed)
		{
			_phase = Phase.Tcp;
			LastReason = "no key — waiting for an Offer (Hello)";
			ArmHello(0f);
			CommunityUdp.NotifyTransport(fellBack: false);
		}
	}

	public void RequestHelloNow()
	{
		if (_phase == Phase.Tcp)
		{
			ArmHello(0f);
		}
	}

	private void ArmHello(float delaySec)
	{
		_helloArmed = true;
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		_nextHelloAt = Mathf.Max(realtimeSinceStartup + delaySec, _lastHelloAt + 10f);
	}

	public void Poll()
	{
		if (_phase == Phase.Closed)
		{
			return;
		}
		try
		{
			_manager.PollEvents();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_phase == Phase.Connecting && realtimeSinceStartup - _startedAt > _fallbackSec)
		{
			float fallbackSec = _fallbackSec;
			GiveUpConnect("no answer in " + fallbackSec + " s");
		}
		else if (_phase == Phase.Udp)
		{
			if (realtimeSinceStartup - _lastTcpKeepaliveAt >= 20f)
			{
				_lastTcpKeepaliveAt = realtimeSinceStartup;
				_conn.UdpSendOnTcp(default(Keepalive));
			}
			FlushSimQueue(realtimeSinceStartup);
			ResendMoveIfDue(realtimeSinceStartup);
		}
		else if (_phase == Phase.Tcp)
		{
			PollHello(realtimeSinceStartup);
		}
	}

	public void Shutdown()
	{
		if (_phase != Phase.Closed)
		{
			_phase = Phase.Closed;
			_outOnUdp = false;
			_inOnUdp = false;
			_helloArmed = false;
			_pendingMove = null;
			_unacked.Clear();
			_hold.Clear();
			StopManager();
			CommunityUdp.NotifyTransport(fellBack: false);
		}
	}

	public void ForceFallback(string reason)
	{
		if (_phase == Phase.Connecting)
		{
			GiveUpConnect(reason);
		}
		else if (_phase == Phase.Udp)
		{
			FallbackToTcp(reason);
		}
	}

	private void OnPeerConnected(NetPeer peer)
	{
		if (_phase == Phase.Connecting)
		{
			_peer = peer;
			_phase = Phase.Udp;
			_connectedAt = Time.realtimeSinceStartup;
			_lastTcpKeepaliveAt = _connectedAt;
			_helloArmed = false;
			uint num = _outTseq + 1;
			_conn.UdpSendOnTcp(new UdpUp
			{
				Kind = 2,
				Json = "{\"from\":" + num + ",\"ch\":" + 3 + "}",
				Bin = new byte[0]
			});
			_outOnUdp = true;
			CommunityUdp.ClearBlock();
			CommunityUdp.Log("connected to " + _target?.ToString() + " rtt " + peer.RoundTripTime + " ms — sending on UDP from tseq " + num + " (announced " + 3 + " channels)");
			CommunityUdp.NotifyTransport(fellBack: false);
		}
	}

	private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
	{
		string why = "peer lost: " + info.Reason;
		if (_phase == Phase.Connecting)
		{
			GiveUpConnect(why);
		}
		else if (_phase == Phase.Udp)
		{
			FallbackToTcp(why);
		}
		_peer = null;
	}

	private void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
	{
		CommunityUdp.Log("socket error " + socketError.ToString() + " (" + endPoint?.ToString() + ")");
	}

	private void GiveUpConnect(string why)
	{
		if (_phase == Phase.Connecting)
		{
			_phase = Phase.Tcp;
			LastReason = why;
			StopManager();
			CommunityUdp.BlockForAWhile(why);
			CommunityUdp.NotifyTransport(fellBack: false);
		}
	}

	private void FallbackToTcp(string why)
	{
		if (_phase != Phase.Udp)
		{
			return;
		}
		_phase = Phase.Tcp;
		LastReason = why;
		Fallbacks++;
		_outOnUdp = false;
		_inOnUdp = false;
		_peerChannels = 1;
		_hold.Clear();
		_simQueue.Clear();
		if (_pendingMove != null)
		{
			_unacked.Add(new KeyValuePair<uint, byte[]>(_pendingMoveTseq, _pendingMove));
			_unacked.Sort((KeyValuePair<uint, byte[]> a, KeyValuePair<uint, byte[]> b) => a.Key.CompareTo(b.Key));
			_pendingMove = null;
		}
		byte[] bin = PackReplay(_unacked);
		int count = _unacked.Count;
		_unacked.Clear();
		_conn.UdpSendOnTcp(new UdpUp
		{
			Kind = 3,
			Json = "{\"reason\":\"" + JsonEscape(why) + "\"}",
			Bin = bin
		});
		StopManager();
		float num = Mathf.Min(30f, _fallbackSec * (float)Fallbacks);
		ArmHello(num);
		CommunityUdp.Log("back to TCP (" + why + ") — replayed " + count + " frame(s) over TCP · game continues · Hello in " + (int)num + " s");
		CommunityUdp.NotifyTransport(fellBack: true);
	}

	private void StopManager()
	{
		try
		{
			_manager.Stop();
		}
		catch (Exception)
		{
		}
		_peer = null;
	}

	private void PollHello(float now)
	{
		if (_helloArmed && !(now < _nextHelloAt) && _conn.Connected())
		{
			if (CommunityUdp.IsTcpOnly)
			{
				_nextHelloAt = now + 10f;
				return;
			}
			if (CommunityUdp.IsBlocked(out var remainSec))
			{
				_nextHelloAt = now + Mathf.Clamp((float)remainSec, 10f, 300f);
				return;
			}
			_conn.UdpSendOnTcp(new UdpUp
			{
				Kind = 1,
				Json = "{\"v\":1}",
				Bin = new byte[0]
			});
			Hellos++;
			_lastHelloAt = now;
			_nextHelloAt = now + 10f;
			CommunityUdp.Log("Hello sent on TCP (asking the frontend for a new UDP offer · #" + Hellos + ")");
		}
	}

	private void OnOffer(string json)
	{
		int num = CommunityUdp.ReadJsonInt(json, "port", 0);
		int num2 = CommunityUdp.ReadJsonInt(json, "ttl", 60);
		byte[] array = CommunityUdp.KeyFromHex(CommunityUdp.ReadJsonString(json, "key"));
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_phase != Phase.Tcp)
		{
			LastOfferNote = "offer port " + num + " ignored (phase " + _phase.ToString() + ")";
			CommunityUdp.Log(LastOfferNote);
			return;
		}
		if (num <= 0 || num > 65535 || array == null)
		{
			int num3 = Mathf.Max((num2 > 0) ? num2 : 60, 10);
			LastOfferNote = "offer port 0 — server has no UDP to offer right now · Hello again in " + num3 + " s";
			CommunityUdp.Log(LastOfferNote);
			_helloArmed = true;
			_nextHelloAt = realtimeSinceStartup + (float)num3;
			return;
		}
		if (CommunityUdp.IsTcpOnly)
		{
			LastOfferNote = "offer port " + num + " ignored (setting «TCP only»)";
			CommunityUdp.Log(LastOfferNote);
			return;
		}
		if (CommunityUdp.IsBlocked(out var remainSec))
		{
			LastOfferNote = "offer port " + num + " ignored (blocked on this machine for " + (int)remainSec + " s)";
			CommunityUdp.Log(LastOfferNote);
			return;
		}
		byte[] request;
		try
		{
			request = CommunityUdp.BuildRequest(array, _entityId);
		}
		catch (Exception ex)
		{
			LastOfferNote = "offer port " + num + " unusable: " + ex.Message;
			CommunityUdp.Log(LastOfferNote);
			return;
		}
		Offers++;
		_target = new IPEndPoint(_target.Address, num);
		_request = request;
		LastOfferNote = "offer port " + num + "/udp ttl " + num2 + "s (Hello #" + Hellos + ") — connecting again";
		CommunityUdp.Log(LastOfferNote);
		_helloArmed = false;
		Start();
	}

	public bool TrySendFrame(byte[] frame, int length, uint typeCode, bool lossyHint, out byte channel)
	{
		channel = 0;
		if (!_outOnUdp)
		{
			return false;
		}
		if (_peer == null || _peer.ConnectionState != ConnectionState.Connected)
		{
			FallbackToTcp("peer not connected");
			return false;
		}
		channel = CommunityUdp.OutChannelOf(typeCode, lossyHint, _peerChannels);
		if (channel == 2)
		{
			uint num = ++_outTseq;
			byte[] array = Wrap(num, frame, length);
			try
			{
				SendLossyRaw(array);
			}
			catch (Exception ex)
			{
				FallbackToTcp("send failed: " + ex.Message);
				return false;
			}
			FramesSent++;
			BytesSent += array.Length;
			Ch2Sent++;
			if (typeCode == 4)
			{
				byte[] array2 = new byte[length];
				Buffer.BlockCopy(frame, 0, array2, 0, length);
				_pendingMove = array2;
				_pendingMoveTseq = num;
				_pendingMoveSentAt = Time.realtimeSinceStartup;
				_pendingMoveTries = 0;
				MoveSent++;
			}
			return true;
		}
		return SendReliable(frame, length);
	}

	private bool SendReliable(byte[] frame, int length)
	{
		if (_unacked.Count >= 4096)
		{
			FallbackToTcp("udp backlog " + _unacked.Count);
			return false;
		}
		uint tseq = ++_outTseq;
		byte[] array = new byte[length];
		Buffer.BlockCopy(frame, 0, array, 0, length);
		byte[] array2 = Wrap(tseq, frame, length);
		_unacked.Add(new KeyValuePair<uint, byte[]>(tseq, array));
		try
		{
			_peer.SendWithDeliveryEvent(array2, 0, DeliveryMethod.ReliableOrdered, tseq);
			_manager.TriggerUpdate();
		}
		catch (Exception ex)
		{
			_unacked.RemoveAll((KeyValuePair<uint, byte[]> x) => x.Key == tseq);
			FallbackToTcp("send failed: " + ex.Message);
			return false;
		}
		FramesSent++;
		BytesSent += array2.Length;
		return true;
	}

	private void ResendMoveIfDue(float now)
	{
		if (_pendingMove == null || !_outOnUdp)
		{
			return;
		}
		if (MoveAckTseq >= _pendingMoveTseq)
		{
			_pendingMove = null;
		}
		else
		{
			if (now - _pendingMoveSentAt < 0.25f)
			{
				return;
			}
			if (_peer == null || _peer.ConnectionState != ConnectionState.Connected)
			{
				FallbackToTcp("peer not connected");
				return;
			}
			byte[] pendingMove = _pendingMove;
			if (_pendingMoveTries >= 20)
			{
				_pendingMove = null;
				MoveEscalated++;
				CommunityUdp.Log("Move not acknowledged after " + _pendingMoveTries + " lossy tries — sent once on channel 0 (ack " + MoveAckTseq + " < " + _pendingMoveTseq + ")");
				SendReliable(pendingMove, pendingMove.Length);
				return;
			}
			uint num = ++_outTseq;
			byte[] array = Wrap(num, pendingMove, pendingMove.Length);
			try
			{
				SendLossyRaw(array);
			}
			catch (Exception ex)
			{
				FallbackToTcp("send failed: " + ex.Message);
				return;
			}
			_pendingMoveTseq = num;
			_pendingMoveSentAt = now;
			_pendingMoveTries++;
			MoveResends++;
			FramesSent++;
			BytesSent += array.Length;
			Ch2Sent++;
		}
	}

	private void SendLossyRaw(byte[] wrapped)
	{
		if (SimLossPct > 0 && _simRandom.Next(100) < SimLossPct)
		{
			SimDropped++;
		}
		else if (SimDelayMs > 0)
		{
			_simQueue.Add(new KeyValuePair<float, byte[]>(Time.realtimeSinceStartup + (float)_simRandom.Next(0, SimDelayMs + 1) / 1000f, wrapped));
			SimDelayed++;
		}
		else
		{
			_peer.Send(wrapped, 2, DeliveryMethod.Sequenced);
			_manager.TriggerUpdate();
		}
	}

	private void FlushSimQueue(float now)
	{
		if (_simQueue.Count == 0)
		{
			return;
		}
		for (int i = 0; i < _simQueue.Count; i++)
		{
			if (!(_simQueue[i].Key <= now))
			{
				continue;
			}
			byte[] value = _simQueue[i].Value;
			_simQueue.RemoveAt(i);
			i--;
			if (_peer != null && _peer.ConnectionState == ConnectionState.Connected)
			{
				try
				{
					_peer.Send(value, 2, DeliveryMethod.Sequenced);
					_manager.TriggerUpdate();
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private void OnDelivered(NetPeer peer, object userData)
	{
		if (userData is uint)
		{
			uint delivered = (uint)userData;
			_unacked.RemoveAll((KeyValuePair<uint, byte[]> x) => x.Key <= delivered);
		}
	}

	private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
	{
		byte[] remainingBytes;
		try
		{
			remainingBytes = reader.GetRemainingBytes();
		}
		catch (Exception)
		{
			BadRecv++;
			return;
		}
		if (remainingBytes == null)
		{
			return;
		}
		BytesRecv += remainingBytes.Length;
		if (!TryReadFrame(remainingBytes, 4, out var header))
		{
			BadRecv++;
			CommunityUdp.Log("datagram malformed (" + remainingBytes.Length + " B) — dropped");
			return;
		}
		if (channel >= 3)
		{
			BadRecv++;
			return;
		}
		FramesRecv++;
		if (channel == 2)
		{
			Ch2Recv++;
			if (SimLossPct > 0 && _simRandom.Next(100) < SimLossPct)
			{
				SimDropped++;
				return;
			}
		}
		uint num = ReadUint(remainingBytes, 0);
		if (num <= _inMaxTseq[channel])
		{
			return;
		}
		if (!_inOnUdp)
		{
			if (_hold.Count < 8192)
			{
				_hold.Add(new HeldFrame
				{
					Tseq = num,
					Channel = channel,
					Data = remainingBytes
				});
				Held++;
			}
		}
		else
		{
			_inMaxTseq[channel] = num;
			_conn.UdpEnqueueFrame(header, remainingBytes, 28);
		}
	}

	public void OnServerControl(UdpDown msg)
	{
		if (_phase == Phase.Closed)
		{
			return;
		}
		if (msg.Kind == 2)
		{
			int value = CommunityUdp.ReadJsonInt(msg.Json, "ch", 1);
			_peerChannels = Mathf.Clamp(value, 1, 64);
			_inOnUdp = true;
			int num = 0;
			if (_hold.Count > 0)
			{
				_hold.Sort((HeldFrame a, HeldFrame b) => a.Tseq.CompareTo(b.Tseq));
				for (int num2 = 0; num2 < _hold.Count; num2++)
				{
					HeldFrame heldFrame = _hold[num2];
					if (heldFrame.Tseq > _inMaxTseq[heldFrame.Channel] && TryReadFrame(heldFrame.Data, 4, out var header))
					{
						_inMaxTseq[heldFrame.Channel] = heldFrame.Tseq;
						_conn.UdpDispatchFrameNow(header, heldFrame.Data, 28);
						num++;
					}
				}
				_hold.Clear();
			}
			CommunityUdp.Log("server sends on UDP now (" + msg.Json + ") — " + _peerChannels + " channel(s) · released " + num + " held frame(s)" + ((_peerChannels < 3) ? " · channel 2 not used (server announced < 3)" : string.Empty));
		}
		else if (msg.Kind == 3)
		{
			_inOnUdp = false;
			_hold.Clear();
			int num3 = 0;
			byte[] array = msg.Bin ?? new byte[0];
			int num4 = 0;
			while (num4 + 8 <= array.Length)
			{
				uint num5 = ReadUint(array, num4);
				int num6 = (int)ReadUint(array, num4 + 4);
				num4 += 8;
				if (num6 < 24 || num6 > array.Length - num4)
				{
					break;
				}
				byte[] array2 = new byte[num6];
				Buffer.BlockCopy(array, num4, array2, 0, num6);
				num4 += num6;
				if (num5 > _inMaxTseq[0] && TryReadFrame(array2, 0, out var header2))
				{
					_inMaxTseq[0] = num5;
					_conn.UdpDispatchFrameNow(header2, array2, 24);
					num3++;
				}
			}
			Replayed += num3;
			CommunityUdp.Log("server went back to TCP (" + msg.Json + ") — replayed " + num3 + " frame(s)");
		}
		else if (msg.Kind == 4)
		{
			uint num7 = CommunityUdp.ReadJsonUInt(msg.Json, "tseq");
			MoveAcks++;
			if (num7 > MoveAckTseq)
			{
				MoveAckTseq = num7;
			}
			if (_pendingMove != null && _pendingMoveTseq <= MoveAckTseq)
			{
				_pendingMove = null;
			}
		}
		else if (msg.Kind == 1)
		{
			OnOffer(msg.Json);
		}
	}

	private static bool TryReadFrame(byte[] data, int headerOffset, out PacketHeader header)
	{
		header = default(PacketHeader);
		if (data == null || data.Length - headerOffset < 24)
		{
			return false;
		}
		header.Size = 24;
		header.Time = (double)BitConverter.ToUInt64(data, headerOffset) / 1000.0;
		header.Seq = BitConverter.ToUInt32(data, headerOffset + 8);
		header.ReplyOf = BitConverter.ToUInt32(data, headerOffset + 12);
		header.TypeCode = BitConverter.ToUInt32(data, headerOffset + 16);
		uint num = BitConverter.ToUInt32(data, headerOffset + 20);
		if (num > int.MaxValue)
		{
			return false;
		}
		header.PayloadSize = (int)num;
		return headerOffset + 24 + header.PayloadSize == data.Length;
	}

	private static byte[] Wrap(uint tseq, byte[] frame, int length)
	{
		byte[] array = new byte[4 + length];
		WriteUint(tseq, array, 0);
		Buffer.BlockCopy(frame, 0, array, 4, length);
		return array;
	}

	private static byte[] PackReplay(List<KeyValuePair<uint, byte[]>> frames)
	{
		if (frames == null || frames.Count == 0)
		{
			return new byte[0];
		}
		long num = 0L;
		for (int i = 0; i < frames.Count; i++)
		{
			num += 8 + frames[i].Value.Length;
		}
		if (num > 1048576)
		{
			CommunityUdp.Log("fallback replay " + num + " B exceeds " + 1048576L + " B — Fallback sent without frames");
			return new byte[0];
		}
		byte[] array = new byte[num];
		int num2 = 0;
		for (int j = 0; j < frames.Count; j++)
		{
			byte[] value = frames[j].Value;
			WriteUint(frames[j].Key, array, num2);
			WriteUint((uint)value.Length, array, num2 + 4);
			num2 += 8;
			Buffer.BlockCopy(value, 0, array, num2, value.Length);
			num2 += value.Length;
		}
		return array;
	}

	private static void WriteUint(uint value, byte[] bytes, int offset)
	{
		for (int i = 0; i < 4; i++)
		{
			bytes[offset + i] = (byte)(value >> i * 8);
		}
	}

	private static uint ReadUint(byte[] bytes, int offset)
	{
		return BitConverter.ToUInt32(bytes, offset);
	}

	private static string JsonEscape(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ")
			.Replace("\r", " ");
	}
}

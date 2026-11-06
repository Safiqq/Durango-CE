using System;
using System.Collections.Generic;
using System.Globalization;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.System.Config;
using Durango.Utils;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CommunityLavaShieldNet : GameSystem<CommunityLavaShieldNet>
{
	private sealed class Request
	{
		public string Id;

		public string Why;

		public float Until;
	}

	private const float FindSec = 3f;

	private const float CurtainSec = 8f;

	private readonly List<Request> _requests = new List<Request>();

	private bool _helloPending;

	private float _readyAt;

	private bool _applyingServer;

	public static string LastReceived = string.Empty;

	public static bool ServerEnabled { get; private set; }

	public static bool ServerOn { get; private set; }

	public static bool StateKnown { get; private set; }

	public static int Received { get; private set; }

	private void Awake()
	{
		Connections.Frontend.On<LavaFxDown>(OnDown);
		Singleton<GameManager>.Instance().WelcomeReceived += OnWelcome;
	}

	private void OnDestroy()
	{
		if (Singleton<GameManager>.HasInstance())
		{
			Singleton<GameManager>.Instance().WelcomeReceived -= OnWelcome;
		}
	}

	private void OnWelcome(Welcome welcome)
	{
		_helloPending = true;
		_readyAt = 0f;
		_requests.Clear();
		StateKnown = false;
	}

	private void Update()
	{
		if (!GameManager.IsReady)
		{
			_readyAt = 0f;
			return;
		}
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		if (_readyAt == 0f)
		{
			_readyAt = realtimeSinceStartup + 1f;
		}
		if (_helloPending && realtimeSinceStartup >= _readyAt)
		{
			_helloPending = false;
			Send(0, 0);
		}
		for (int num = _requests.Count - 1; num >= 0; num--)
		{
			Request request = _requests[num];
			if (TryPlay(request) || realtimeSinceStartup > request.Until)
			{
				_requests.RemoveAt(num);
			}
		}
	}

	private void OnDown(LavaFxDown message, PacketHeader header)
	{
		try
		{
			JObject jObject = JObject.Parse(string.IsNullOrEmpty(message.Json) ? "{}" : message.Json);
			Received++;
			LastReceived = message.Kind + " " + message.Json;
			if (message.Kind == 0)
			{
				ServerOn = (bool?)jObject["on"] == true;
				ServerEnabled = (bool?)jObject["enabled"] == true;
				StateKnown = true;
				_applyingServer = true;
				try
				{
					ConfigInstance.ChangeValue("lava_shield_fx", ServerOn, save: false);
					return;
				}
				finally
				{
					_applyingServer = false;
				}
			}
			if (message.Kind == 1)
			{
				string text = (string)jObject["id"];
				string text2 = ((string)jObject["why"]) ?? "arrive";
				if (!string.IsNullOrEmpty(text))
				{
					float until = Time.realtimeSinceStartup + ((text2 == "arrive") ? 8f : 3f);
					_requests.Add(new Request
					{
						Id = text,
						Why = text2,
						Until = until
					});
				}
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static PlayerBehavior FindPlayer(string id)
	{
		if (!Singleton<PlayerManager>.HasInstance())
		{
			return null;
		}
		return Singleton<PlayerManager>.Instance().GetPlayerIncludeLocalPlayer(id);
	}

	private static bool TryPlay(Request r)
	{
		PlayerBehavior playerBehavior = FindPlayer(r.Id);
		if (playerBehavior == null || playerBehavior.gameObject == null)
		{
			return false;
		}
		if (playerBehavior.IsLocalPlayer)
		{
			if (UIManager.IsLoadingCurtain && r.Why == "arrive")
			{
				return false;
			}
		}
		else if (!playerBehavior.GetVisible())
		{
			return true;
		}
		PlayOn(playerBehavior, r.Why);
		return true;
	}

	public static CommunityLavaShield PlayOn(PlayerBehavior p, string why)
	{
		if (p == null)
		{
			return null;
		}
		CommunityLavaShield communityLavaShield = CommunityLavaShield.Play((why == "begin") ? null : p.transform, p.transform.position, RadiusOf(p), why + " " + p.EntityId);
		if (communityLavaShield != null)
		{
			communityLavaShield.gameObject.layer = p.gameObject.layer;
			foreach (Transform item in communityLavaShield.transform)
			{
				item.gameObject.layer = p.gameObject.layer;
			}
		}
		return communityLavaShield;
	}

	private static float RadiusOf(PlayerBehavior p)
	{
		bool flag = false;
		Bounds bounds = default(Bounds);
		SkinnedMeshRenderer[] componentsInChildren = p.GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			if (skinnedMeshRenderer.enabled)
			{
				if (!flag)
				{
					bounds = skinnedMeshRenderer.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(skinnedMeshRenderer.bounds);
				}
			}
		}
		if (!flag)
		{
			return 150f;
		}
		float a = (bounds.max.y - p.transform.position.y) * 0.9f;
		float b = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.25f;
		return Mathf.Clamp(Mathf.Max(a, b), 90f, 320f);
	}

	private static void Send(byte kind, int on)
	{
		if (CommunityLink.Active && GameManager.IsReady)
		{
			Connections.Frontend.Send(new LavaFxUp
			{
				Kind = kind,
				On = on
			}, noReply: true);
		}
	}

	public static void OnSettingChanged(bool value)
	{
		if (!GameSystem<CommunityLavaShieldNet>.HasInstance() || !GameSystem<CommunityLavaShieldNet>.Instance()._applyingServer)
		{
			ServerOn = value;
			Send(1, value ? 1 : 0);
		}
	}

	public static string DebugCommand(string[] args)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "state");
		switch (text)
		{
		case "play":
		{
			string text2 = ((args.Length > 2) ? args[2] : "self");
			PlayerBehavior p = ((text2 == "self") ? PlayerBehavior.LocalPlayer : FindPlayer(text2));
			string why = ((args.Length > 3) ? args[3] : "arrive");
			return "play " + text2 + " " + ((PlayOn(p, why) != null) ? "ok" : "no player / no shader");
		}
		case "on":
		case "off":
			ConfigInstance.ChangeValue("lava_shield_fx", text == "on");
			return "setting " + text;
		case "mul":
		{
			if (args.Length > 2 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				CommunityLavaShield.SizeMul = Mathf.Clamp(result, 0.3f, 4f);
			}
			return "size × " + CommunityLavaShield.SizeMul;
		}
		case "stop":
			CommunityLavaShield.StopAll();
			return "stopped";
		default:
			return "known " + StateKnown + " on " + ServerOn + " enabled " + ServerEnabled + " setting " + ConfigInstance.GetValue("lava_shield_fx", defaultValue: false) + " received " + Received + " last " + LastReceived + " played " + CommunityLavaShield.Played + " live " + CommunityLavaShield.LiveCount + " lastPlayed " + CommunityLavaShield.LastPlayed;
		}
	}
}

using System;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.System.Config;
using Durango.Utils;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class OffServerRunButton : GameSystem<OffServerRunButton>
{
	private bool _helloPending;

	private float _readyAt;

	private bool _shiftWas;

	public static string LastReceived = string.Empty;

	public static string LastReason = string.Empty;

	public static string ShiftBlocked = string.Empty;

	public static bool StateKnown { get; private set; }

	public static bool ServerEnabled { get; private set; }

	public static bool Running { get; private set; }

	public static bool Desired { get; private set; }

	public static float Mult { get; private set; } = 1f;

	public static int Received { get; private set; }

	public static bool Available
	{
		get
		{
			if (StateKnown && ServerEnabled && GameManager.IsReady && !GameManager.IsPrologueMode)
			{
				return OffServerLink.Active;
			}
			return false;
		}
	}

	public static bool HoldMode
	{
		get
		{
			try
			{
				return ConfigInstance.GetValue("run_hold", defaultValue: false);
			}
			catch (Exception)
			{
				return false;
			}
		}
	}

	public static float AnimRate
	{
		get
		{
			if (!Running || Mult <= 1f)
			{
				return 1f;
			}
			PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
			if (localPlayer == null || localPlayer.IsRiding || localPlayer.IsTired)
			{
				return 1f;
			}
			return Mult;
		}
	}

	public static bool ShiftDown { get; private set; }

	public static int ShiftPresses { get; private set; }

	public static event Action Changed;

	private void Awake()
	{
		Connections.Frontend.On<RunDown>(OnDown);
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
		StateKnown = false;
		SetRunning(running: false);
		Desired = false;
		RaiseChanged();
	}

	private void Update()
	{
		if (OffServerHudLayout.BlocksInput)
		{
			return;
		}
		if (!GameManager.IsReady)
		{
			_readyAt = 0f;
			_shiftWas = false;
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
		UpdateShift();
	}

	private void UpdateShift()
	{
		bool flag = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool shiftWas = _shiftWas;
		_shiftWas = flag;
		ShiftDown = flag;
		if (flag == shiftWas)
		{
			return;
		}
		if (!Available)
		{
			ShiftBlocked = "not available";
			return;
		}
		if (flag && UIInput.selection != null && UIInput.selection.gameObject.activeInHierarchy)
		{
			ShiftBlocked = "typing in " + UIInput.selection.name;
			return;
		}
		ShiftBlocked = string.Empty;
		if (flag)
		{
			ShiftPresses++;
		}
		Press(flag);
	}

	public static void Press(bool down)
	{
		if (!OffServerHudLayout.BlocksInput)
		{
			if (HoldMode)
			{
				Want(down);
			}
			else if (down)
			{
				Want(!Desired);
			}
		}
	}

	public static void Want(bool on)
	{
		if (Available)
		{
			Desired = on;
			Send(1, on ? 1 : 0);
			RaiseChanged();
		}
	}

	private void OnDown(RunDown message, PacketHeader header)
	{
		try
		{
			JObject jObject = JObject.Parse(string.IsNullOrEmpty(message.Json) ? "{}" : message.Json);
			Received++;
			LastReceived = message.Kind + " " + message.Json;
			if (message.Kind != 0)
			{
				return;
			}
			ServerEnabled = (bool?)jObject["enabled"] == true;
			float value = ((float?)jObject["mult"]) ?? 1f;
			float mult = Mult;
			Mult = Mathf.Clamp(value, 1f, 3f);
			StateKnown = true;
			bool valueOrDefault = (bool?)jObject["running"] == true;
			string text = (LastReason = ((string)jObject["reason"]) ?? string.Empty);
			SetRunning(valueOrDefault);
			if (valueOrDefault && Mathf.Abs(mult - Mult) > 0.001f)
			{
				RefreshAnim();
			}
			Desired = valueOrDefault;
			if (text.Length > 0)
			{
				string text2 = ReasonText(text);
				if (!string.IsNullOrEmpty(text2))
				{
					UIManager.SystemMsg(text2);
				}
			}
			RaiseChanged();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static void SetRunning(bool running)
	{
		if (Running != running)
		{
			Running = running;
			RefreshAnim();
		}
	}

	private static void RefreshAnim()
	{
		try
		{
			if (Singleton<PlayerController>.HasInstance() && PlayerController.MotionUpdater != null)
			{
				PlayerController.MotionUpdater.RefreshMotion();
			}
		}
		catch (Exception)
		{
		}
	}

	private static void RaiseChanged()
	{
		try
		{
			OffServerRunButton.Changed?.Invoke();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static void Send(byte kind, int on)
	{
		if (OffServerLink.Active && GameManager.IsReady)
		{
			Connections.Frontend.Send(new RunUp
			{
				Kind = kind,
				On = on
			}, noReply: true);
		}
	}

	public static string ReasonText(string reason)
	{
		string lang = OffServerDiscordLink.Lang;
		return reason switch
		{
			"stamina" => lang switch
			{
				"ko" => "기력이 다해 걷기로 돌아갑니다",
				"id" => "Stamina habis — kembali berjalan",
				"th" => "แรงหมด — กล\u0e31บมาเด\u0e34น",
				_ => "Out of stamina — walking again",
			},
			"low" => lang switch
			{
				"ko" => "달리기에 기력이 부족합니다",
				"id" => "Stamina tidak cukup untuk berlari",
				"th" => "แรงไม\u0e48พอว\u0e34\u0e48ง — พ\u0e31กให\u0e49แรงฟ\u0e37\u0e49นก\u0e48อน",
				_ => "Not enough stamina to run",
			},
			"disabled" => lang switch
			{
				"ko" => "달리기 버튼이 잠시 꺼져 있습니다",
				"id" => "Tombol lari sedang nonaktif",
				"th" => "ป\u0e38\u0e48มว\u0e34\u0e48งป\u0e34ดอย\u0e39\u0e48ช\u0e31\u0e48วคราว",
				_ => "Running is turned off for now",
			},
			_ => null,
		};
	}

	public static string Label()
	{
		return OffServerDiscordLink.Lang switch
		{
			"ko" => "달리기",
			"id" => "Lari",
			"th" => "ว\u0e34\u0e48ง",
			_ => "Run",
		};
	}

	public static string DebugCommand(string[] args)
	{
		string text = ((args.Length > 1) ? args[1].ToLowerInvariant() : "state");
		switch (text)
		{
		case "off":
		case "on":
			Want(text == "on");
			return "want " + text + (Available ? string.Empty : " (not available)");
		case "toggle":
			Press(down: true);
			Press(down: false);
			return "toggle → desired " + Desired;
		case "press":
			Press(args.Length > 2 && args[2] == "1");
			return "press " + ((args.Length > 2) ? args[2] : "0") + " → desired " + Desired;
		case "hold":
			ConfigInstance.ChangeValue("run_hold", args.Length > 2 && args[2] == "1");
			return "hold " + HoldMode;
		case "hello":
			Send(0, 0);
			return "hello sent";
		case "pos":
		{
			if (args.Length > 3 && int.TryParse(args[2], out var result) && int.TryParse(args[3], out var result2))
			{
				OffServerRunButtonUI.SetPlacement(result, result2, (args.Length > 4 && int.TryParse(args[4], out var result3)) ? result3 : 0);
			}
			return OffServerRunButtonUI.Describe();
		}
		case "reset":
			OffServerRunButtonUI.SetPlacement(int.MinValue, int.MinValue, 0);
			return OffServerRunButtonUI.Describe();
		default:
			return "known " + StateKnown + " enabled " + ServerEnabled + " available " + Available + " running " + Running + " desired " + Desired + " mult " + Mult + " animRate " + AnimRate + " hold " + HoldMode + " shift " + ShiftDown + " presses " + ShiftPresses + " blocked " + ShiftBlocked + " selection " + ((UIInput.selection != null) ? (UIInput.selection.name + "/" + UIInput.selection.gameObject.activeInHierarchy) : "none") + " received " + Received + " last " + LastReceived + " reason " + LastReason + " | " + OffServerRunButtonUI.Describe();
		}
	}
}

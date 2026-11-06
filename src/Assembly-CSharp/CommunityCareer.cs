using System;
using Durango.Logic.Clusters;
using Durango.Network;
using Durango.UI;
using Durango.Utils;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class CommunityCareer : GameSystem<CommunityCareer>
{
	private bool _helloPending;

	private bool _assigned;

	private float _nextCheck;

	private float _readyAt;

	private float _retryAt;

	private float _sentAt;

	private JObject _pending;

	public static bool Busy { get; private set; }

	public static int Generation { get; private set; }

	private void Awake()
	{
		Connections.Frontend.On<CareerDown>(OnDown);
		Singleton<GameManager>.Instance().WelcomeReceived += OnWelcome;
		foreach (InputCommand value in Enum.GetValues(typeof(InputCommand)))
		{
			GameSystem<InputSystem>.Instance().On(value, GuardInput, InputSystem.Priority.Highest);
		}
	}

	private void OnDestroy()
	{
		if (Singleton<GameManager>.HasInstance())
		{
			Singleton<GameManager>.Instance().WelcomeReceived -= OnWelcome;
		}
		if (GameSystem<InputSystem>.HasInstance())
		{
			foreach (InputCommand value in Enum.GetValues(typeof(InputCommand)))
			{
				GameSystem<InputSystem>.Instance().Off(value, GuardInput, InputSystem.Priority.Highest);
			}
		}
		CommunityCareerUI.CloseWindow();
		Busy = false;
		Generation++;
	}

	private void GuardInput(InputCommandMessage message)
	{
		if (CommunityCareerUI.IsOpen && !UIManager.MessageBox.IsShow && message.Command != InputCommand.ScreenCapture)
		{
			if (message.Command == InputCommand.Back || message.Command == InputCommand.CancelModalPopup)
			{
				CommunityCareerUI.Back();
			}
			GameSystem<InputSystem>.Instance().StopPropagation();
		}
	}

	private void OnWelcome(Welcome welcome)
	{
		Generation++;
		Busy = false;
		_pending = null;
		_readyAt = 0f;
		_helloPending = true;
		_assigned = false;
		_nextCheck = 0f;
		CommunityCareerUI.CloseWindow();
	}

	private void Update()
	{
		if (!GameManager.IsReady)
		{
			_readyAt = 0f;
			CommunityCareerUI.CloseWindow();
			return;
		}
		if (_readyAt == 0f)
		{
			_readyAt = Time.realtimeSinceStartup + 1f;
		}
		if (_helloPending && Time.realtimeSinceStartup >= _readyAt)
		{
			_helloPending = false;
			_nextCheck = Time.realtimeSinceStartup + 10f;
			Send(0);
		}
		if (PlayerBehavior.LocalPlayer != null && !PlayerBehavior.LocalPlayer.IsAlive)
		{
			CommunityCareerUI.CloseWindow();
			_pending = null;
			return;
		}
		if (!_assigned && !CommunityCareerUI.IsOpen && _pending == null && Time.realtimeSinceStartup >= _nextCheck)
		{
			_nextCheck = Time.realtimeSinceStartup + 10f;
			Send(0);
		}
		if (_pending != null && Time.realtimeSinceStartup >= _retryAt && !UIManager.MessageBox.IsShow)
		{
			_retryAt = Time.realtimeSinceStartup + 1f;
			LoadingCurtainGroup loadingCurtainGroup = UIManager.FindScript<LoadingCurtainGroup>();
			if ((loadingCurtainGroup == null || !loadingCurtainGroup.IsVisible) && CommunityCareerUI.ShowWindow(_pending))
			{
				_pending = null;
			}
		}
		if (Busy && Time.realtimeSinceStartup - _sentAt > 15f)
		{
			Busy = false;
			CommunityCareerUI.Result("ย\u0e31งไม\u0e48ได\u0e49ร\u0e31บผลย\u0e37นย\u0e31น กดอ\u0e35กคร\u0e31\u0e49งเพ\u0e37\u0e48อตรวจสอบได\u0e49", success: false);
		}
	}

	private void OnDown(CareerDown message, PacketHeader header)
	{
		if (!GameManager.IsReady)
		{
			return;
		}
		try
		{
			JObject jObject = JObject.Parse(message.Json);
			if ((int?)jObject["jobId"] >= 0)
			{
				_assigned = true;
				_pending = null;
				Busy = false;
				CommunityCareerUI.CloseWindow();
				if (message.Kind == 2)
				{
					UIManager.SystemMsg(((string)jObject["text"]) ?? "เล\u0e37อกอาช\u0e35พแล\u0e49ว");
				}
			}
			else if (message.Kind == 0)
			{
				if ((bool?)jObject["open"] == true)
				{
					_pending = jObject;
				}
			}
			else if (message.Kind == 2)
			{
				Busy = false;
				CommunityCareerUI.Result(((string)jObject["text"]) ?? "เล\u0e37อกอาช\u0e35พไม\u0e48สำเร\u0e47จ", success: false);
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	private static void Send(byte kind, int jobId = -1)
	{
		if (CommunityLink.Active && GameManager.IsReady)
		{
			Connections.Frontend.Send(new CareerUp
			{
				Kind = kind,
				JobId = jobId
			});
		}
	}

	public static void Choose(int jobId)
	{
		if (!Busy && GameManager.IsReady)
		{
			Busy = true;
			GameSystem<CommunityCareer>.Instance()._sentAt = Time.realtimeSinceStartup;
			Send(2, jobId);
		}
	}

	public static void Reopen()
	{
		Send(3);
	}
}

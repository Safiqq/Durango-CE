using Durango.Utils;
using UnityEngine;

public static class OffServerCounterMotion
{
	private static float _lockUntil;

	public static bool Locked => Time.time < _lockUntil;

	public static void Play(string motion, float lockSec)
	{
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (lockSec <= 0f || localPlayer == null || !localPlayer.IsAlive || localPlayer.IsRiding)
		{
			PlayerController.MotionUpdater.Motion(motion);
			return;
		}
		_lockUntil = Time.time + lockSec;
		Singleton<PlayerController>.Instance().StopMove();
		GameSystem<InputSystem>.Instance().MoveLockTimer(lockSec);
		PlayerController.MotionUpdater.Motion(motion, lockSec, 1f, forceTransition: true);
	}
}

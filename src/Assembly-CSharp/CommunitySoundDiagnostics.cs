using UnityEngine;

public static class CommunitySoundDiagnostics
{
	private const int MaxLoggedEvents = 40;

	private const int MaxProbedFailures = 3;

	private static int s_loggedEvents;

	private static int s_probedFailures;

	public static void OnEngineReady()
	{
		AkCallbackManager.SetMonitoringCallback(AkMonitorErrorLevel.ErrorLevel_All, LogWwiseMessage);
		AkChannelConfig speakerConfiguration = AkSoundEngine.GetSpeakerConfiguration();
		CommunityLog.Info("[Sound] speaker config channels=" + speakerConfiguration.uNumChannels + " mask=" + speakerConfiguration.uChannelMask + " type=" + speakerConfiguration.eConfigType);
	}

	public static void OnEventPosted(string eventName, uint playingId, GameObject soundObject)
	{
		if (s_loggedEvents < 40)
		{
			s_loggedEvents++;
			CommunityLog.Info("[Sound] PostEvent " + eventName + " -> playingId=" + playingId + " obj=" + ((soundObject == null) ? "null" : soundObject.name) + " bgm=" + SoundManager.VolumeForBgm + " sfx=" + SoundManager.VolumeForSfx);
			if (playingId == 0 && soundObject != null && s_probedFailures < 3)
			{
				s_probedFailures++;
				ProbeRefusedEvent(eventName, soundObject);
			}
		}
	}

	private static void ProbeRefusedEvent(string eventName, GameObject soundObject)
	{
		bool flag = AkSoundEngine.IsGameObjectRegistered(soundObject);
		uint num = AkSoundEngine.PostEvent(eventName, soundObject);
		AKRESULT aKRESULT = AkSoundEngine.RegisterGameObj(soundObject, soundObject.name);
		uint num2 = AkSoundEngine.PostEvent(eventName, soundObject);
		CommunityLog.Info("[Sound] probe " + eventName + " id=" + AkSoundEngine.GetIDFromString(eventName) + " registered=" + flag + " postWithoutCallback=" + num + " register=" + aKRESULT.ToString() + " postAfterRegister=" + num2);
	}

	private static void LogWwiseMessage(AkMonitorErrorCode code, AkMonitorErrorLevel level, uint playingId, ulong gameObjectId, string message)
	{
		CommunityLog.Info("[Wwise] " + level.ToString() + " " + code.ToString() + " playingId=" + playingId + " " + message);
	}
}

using UnityEngine;

public class NcEffectAniBehaviour : NcEffectBehaviour
{
	protected NcTimerTool m_Timer;

	protected bool m_TimerInitialized;

	protected GameObject m_OnEndAniGameObject;

	protected bool m_bEndAnimation;

	public string m_OnEndAniFunction = "OnEndAnimation";

	public void SetCallBackEndAnimation(GameObject callBackGameObj)
	{
		m_OnEndAniGameObject = callBackGameObj;
	}

	public void SetCallBackEndAnimation(GameObject callBackGameObj, string nameFunction)
	{
		m_OnEndAniGameObject = callBackGameObj;
		m_OnEndAniFunction = nameFunction;
	}

	public bool IsStartAnimation()
	{
		return m_Timer != null && m_Timer.IsEnable();
	}

	public bool IsEndAnimation()
	{
		return m_bEndAnimation;
	}

	protected void InitAnimationTimer()
	{
		if (m_Timer == null)
		{
			m_Timer = new NcTimerTool();
		}
		m_bEndAnimation = false;
		m_Timer.Start();
		m_TimerInitialized = true;
	}

	public virtual void ResetAnimation()
	{
		m_bEndAnimation = false;
		if (m_Timer != null)
		{
			m_Timer.Reset(0f);
		}
		m_TimerInitialized = false;
	}

	public virtual void PauseAnimation()
	{
		if (m_Timer != null)
		{
			m_Timer.Pause();
		}
	}

	public virtual void ResumeAnimation()
	{
		if (m_Timer != null)
		{
			m_Timer.Resume();
		}
	}

	public virtual void MoveAnimation(float deltaTime)
	{
		if (m_Timer != null)
		{
			m_Timer.Reset(m_Timer.GetTime() + deltaTime);
		}
	}

	protected void OnEndAnimation()
	{
		m_bEndAnimation = true;
		if (m_OnEndAniGameObject != null)
		{
			m_OnEndAniGameObject.SendMessage(m_OnEndAniFunction, this, SendMessageOptions.DontRequireReceiver);
		}
	}
}

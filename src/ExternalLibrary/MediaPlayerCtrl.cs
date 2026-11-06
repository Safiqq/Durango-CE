using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FFmpeg.AutoGen;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class MediaPlayerCtrl : MonoBehaviour
{
	public delegate void VideoEnd();

	public delegate void VideoReady();

	public delegate void VideoError(MEDIAPLAYER_ERROR errorCode, MEDIAPLAYER_ERROR errorCodeExtra);

	public delegate void VideoFirstFrameReady();

	public delegate void VideoResize();

	public delegate void VideoBuffering();

	public delegate void VideoBufferingEnd();

	public enum MEDIAPLAYER_ERROR
	{
		MEDIA_ERROR_NOT_VALID_FOR_PROGRESSIVE_PLAYBACK = 200,
		MEDIA_ERROR_IO = -1004,
		MEDIA_ERROR_MALFORMED = -1007,
		MEDIA_ERROR_TIMED_OUT = -110,
		MEDIA_ERROR_UNSUPPORTED = -1010,
		MEDIA_ERROR_SERVER_DIED = 100,
		MEDIA_ERROR_UNKNOWN = 1
	}

	public enum MEDIAPLAYER_STATE
	{
		NOT_READY,
		READY,
		END,
		PLAYING,
		PAUSED,
		STOPPED,
		ERROR
	}

	public enum MEDIA_SCALE
	{
		SCALE_X_TO_Y,
		SCALE_X_TO_Z,
		SCALE_Y_TO_X,
		SCALE_Y_TO_Z,
		SCALE_Z_TO_X,
		SCALE_Z_TO_Y,
		SCALE_X_TO_Y_2
	}

	public delegate void VideoInterrupt();

	private const int BUFFER_SIZE = 256;

	public string m_strFileName;

	public GameObject[] m_TargetMaterial = new GameObject[1];

	private Texture2D m_VideoTexture;

	private Texture2D m_VideoTextureDummy;

	private Texture2D m_VideoTextureY;

	private Texture2D m_VideoTextureU;

	private Texture2D m_VideoTextureV;

	private Texture2D m_VideoTextureDummyY;

	private Texture2D m_VideoTextureDummyU;

	private Texture2D m_VideoTextureDummyV;

	private MEDIAPLAYER_STATE m_CurrentState;

	private int m_iCurrentSeekPosition;

	private float m_fVolume = 1f;

	private int m_iWidth;

	private int m_iHeight;

	private float m_fSpeed = 1f;

	public bool m_bFullScreen;

	public bool m_bSupportRockchip = true;

	public bool m_bPC_FastMode;

	public VideoResize OnResize;

	public VideoReady OnReady;

	public VideoEnd OnEnd;

	public VideoError OnVideoError;

	public VideoFirstFrameReady OnVideoFirstFrameReady;

	public Action<Texture> OnVideoTextureUpdated;

	public VideoBuffering OnVideoBuffering;

	public VideoBuffering OnVideoBufferingEnd;

	private IntPtr m_texPtr;

	private IntPtr m_texPtrY;

	private IntPtr m_texPtrU;

	private IntPtr m_texPtrV;

	public Shader m_shaderYUV;

	private int m_iPauseFrame;

	private bool m_bBuffring;

	private int m_iAndroidMgrID;

	private bool m_bIsFirstFrameReady;

	private bool m_bFirst;

	public MEDIA_SCALE m_ScaleValue;

	public GameObject[] m_objResize;

	public bool m_bLoop;

	public bool m_bAutoPlay = true;

	public UnityEvent m_eventOnResize;

	public UnityEvent m_eventOnReady;

	public UnityEvent m_eventOnEnd;

	public UnityEvent m_eventOnVideoError;

	public UnityEvent m_eventOnVideoFirstFrameReady;

	public UnityEvent m_eventOnVideoBuffering;

	public UnityEvent m_eventOnVideoBufferingEnd;

	private bool m_bInit;

	private bool m_bCheckFBO;

	private bool _destroyed;

	private bool m_bPause;

	private bool m_bPlaying;

	private bool m_bReadyPlay;

	private unsafe AVFrame* pConvertedFrame;

	private unsafe sbyte* pConvertedFrameBuffer;

	private unsafe SwsContext* pConvertContext;

	private unsafe AVCodecContext* pCodecContext;

	private unsafe AVCodecContext* pAudioCodecContext;

	private unsafe AVFrame* pDecodedFrame;

	private unsafe AVFrame* pDecodedAudioFrame;

	private unsafe AVFormatContext* pFormatContext;

	private unsafe AVPacket* pPacket;

	private unsafe AVStream* pStream;

	private unsafe AVStream* pStreamAudio;

	private Queue<IntPtr> m_queuePacketVideo;

	private Queue<IntPtr> m_queuePacketSound;

	private int iStreamAudioIndex;

	private int iStreamIndex;

	private AudioClip audioClip;

	private AudioSource audioSource;

	private long iSoundCount;

	private int iInitCount;

	private double pts;

	private bool bVideoFirstFrameReady;

	private int m_iID;

	private Thread loader;

	private bool bUnloadVideo;

	private bool bDirect9;

	private GraphicsDeviceType m_typeGraphic;

	private VideoInterrupt action;

	private bool bInterrupt;

	private float fLastFrameTime;

	private float fCurrentSeekTime;

	private float[] fAudioData;

	private bool bEnd;

	private Thread threadVideo;

	private Thread threadVideo2;

	private List<float[]> listAudio;

	private Queue<byte[]> listVideo;

	private List<double> listAudioPts;

	private List<double> listAudioPtsTime;

	private Queue<float> listVideoPts;

	private bool bBuffering = true;

	private bool bEndCheck;

	private bool bReadPacket;

	private unsafe AVPacket* Packet2;

	private int iSoundBufferCount;

	private int m_LastAudioPartSetted = -1;

	private Material material;

	private bool m_bSetTextureFirst;

	private bool bSeekTo;

	static MediaPlayerCtrl()
	{
	}

	[DllImport("EasyMovieTexture")]
	private static extern int SetTexture();

	[DllImport("EasyMovieTexture")]
	private static extern void ReleaseTexture(int iID);

	[DllImport("EasyMovieTexture")]
	private static extern void SetTextureFromUnity(int iID, IntPtr texture, IntPtr textureY, IntPtr textureU, IntPtr textureV, int w, int h, byte[] data);

	[DllImport("EasyMovieTexture")]
	private static extern IntPtr GetRenderEventFunc();

	private void Awake()
	{
		if (!m_bInit)
		{
			InitPlayer();
		}
	}

	private void Start()
	{
	}

	private void InitPlayer()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
		string text = Application.dataPath + Path.DirectorySeparatorChar + "Plugins";
		if (!environmentVariable.Contains(text))
		{
			Environment.SetEnvironmentVariable("PATH", environmentVariable + Path.PathSeparator + text, EnvironmentVariableTarget.Process);
		}
		if (SystemInfo.deviceModel.Contains("rockchip"))
		{
			m_bSupportRockchip = true;
		}
		else
		{
			m_bSupportRockchip = false;
		}
		if (m_TargetMaterial != null)
		{
			for (int i = 0; i < m_TargetMaterial.Length; i++)
			{
				if (!(m_TargetMaterial[i] != null))
				{
					continue;
				}
				if (m_TargetMaterial[i].GetComponent<MeshFilter>() != null)
				{
					Vector2[] uv = m_TargetMaterial[i].GetComponent<MeshFilter>().mesh.uv;
					for (int j = 0; j < uv.Length; j++)
					{
						ref Vector2 reference = ref uv[j];
						reference = new Vector2(uv[j].x, 1f - uv[j].y);
					}
					m_TargetMaterial[i].GetComponent<MeshFilter>().mesh.uv = uv;
				}
				if (m_TargetMaterial[i].GetComponent<RawImage>() != null)
				{
					m_TargetMaterial[i].GetComponent<RawImage>().uvRect = new Rect(0f, 1f, 1f, -1f);
				}
			}
		}
		if (m_queuePacketVideo == null)
		{
			m_queuePacketVideo = new Queue<IntPtr>();
		}
		if (m_queuePacketSound == null)
		{
			m_queuePacketSound = new Queue<IntPtr>();
		}
		Call_SetUnityActivity();
		m_bInit = true;
	}

	private void OnApplicationQuit()
	{
	}

	private void OnDisable()
	{
		if (GetCurrentState() == MEDIAPLAYER_STATE.PLAYING)
		{
			Pause();
		}
	}

	private void OnEnable()
	{
		if (GetCurrentState() == MEDIAPLAYER_STATE.PAUSED)
		{
			Play();
		}
	}

	private void Update()
	{
		if (string.IsNullOrEmpty(m_strFileName))
		{
			return;
		}
		if (m_bAutoPlay)
		{
			if (!m_bFirst)
			{
				string strFileName = m_strFileName.Trim();
				Call_Load(strFileName, 0);
				Call_SetLooping(m_bLoop);
				m_bFirst = true;
			}
		}
		else
		{
			m_bFirst = true;
		}
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
		{
			if (!m_bCheckFBO)
			{
				if (Call_GetVideoWidth() <= 0 || Call_GetVideoHeight() <= 0)
				{
					return;
				}
				m_iWidth = Call_GetVideoWidth();
				m_iHeight = Call_GetVideoHeight();
				Resize();
				if (m_VideoTexture != null)
				{
					if (m_VideoTextureDummy != null)
					{
						UnityEngine.Object.Destroy(m_VideoTextureDummy);
						m_VideoTextureDummy = null;
					}
					m_VideoTextureDummy = m_VideoTexture;
					m_VideoTexture = null;
				}
				if (m_VideoTextureY != null)
				{
					if (m_VideoTextureDummyY != null)
					{
						UnityEngine.Object.Destroy(m_VideoTextureDummyY);
						m_VideoTextureDummyY = null;
					}
					m_VideoTextureDummyY = m_VideoTextureY;
					m_VideoTextureY = null;
				}
				if (m_VideoTextureU != null)
				{
					if (m_VideoTextureDummyU != null)
					{
						UnityEngine.Object.Destroy(m_VideoTextureDummyU);
						m_VideoTextureDummyU = null;
					}
					m_VideoTextureDummyU = m_VideoTextureU;
					m_VideoTextureU = null;
				}
				if (m_VideoTextureV != null)
				{
					if (m_VideoTextureDummyV != null)
					{
						UnityEngine.Object.Destroy(m_VideoTextureDummyV);
						m_VideoTextureDummyV = null;
					}
					m_VideoTextureDummyV = m_VideoTextureV;
					m_VideoTextureV = null;
				}
				if (m_bPC_FastMode)
				{
					m_VideoTextureY = new Texture2D(Call_GetVideoWidth(), Call_GetVideoHeight(), TextureFormat.Alpha8, mipChain: false);
					m_VideoTextureU = new Texture2D(Call_GetVideoWidth() / 2, Call_GetVideoHeight() / 2, TextureFormat.Alpha8, mipChain: false);
					m_VideoTextureV = new Texture2D(Call_GetVideoWidth() / 2, Call_GetVideoHeight() / 2, TextureFormat.Alpha8, mipChain: false);
					m_VideoTextureY.filterMode = FilterMode.Bilinear;
					m_VideoTextureY.wrapMode = TextureWrapMode.Clamp;
					m_VideoTextureU.filterMode = FilterMode.Bilinear;
					m_VideoTextureU.wrapMode = TextureWrapMode.Clamp;
					m_VideoTextureV.filterMode = FilterMode.Bilinear;
					m_VideoTextureV.wrapMode = TextureWrapMode.Clamp;
				}
				else
				{
					if (m_bSupportRockchip)
					{
						m_VideoTexture = new Texture2D(Call_GetVideoWidth(), Call_GetVideoHeight(), TextureFormat.RGB565, mipChain: false);
					}
					else
					{
						m_VideoTexture = new Texture2D(Call_GetVideoWidth(), Call_GetVideoHeight(), TextureFormat.RGBA32, mipChain: false);
					}
					m_VideoTexture.filterMode = FilterMode.Bilinear;
					m_VideoTexture.wrapMode = TextureWrapMode.Clamp;
				}
				if (m_bPC_FastMode)
				{
					m_texPtrY = m_VideoTextureY.GetNativeTexturePtr();
					m_texPtrU = m_VideoTextureU.GetNativeTexturePtr();
					m_texPtrV = m_VideoTextureV.GetNativeTexturePtr();
				}
				else
				{
					m_texPtr = m_VideoTexture.GetNativeTexturePtr();
				}
				Call_SetUnityTexture((int)m_texPtr);
				Call_SetWindowSize();
				m_bCheckFBO = true;
				if (OnResize != null)
				{
					OnResize();
				}
				if (m_eventOnResize != null)
				{
					m_eventOnResize.Invoke();
				}
			}
			else if (Call_GetVideoWidth() != m_iWidth || Call_GetVideoHeight() != m_iHeight)
			{
				m_iWidth = Call_GetVideoWidth();
				m_iHeight = Call_GetVideoHeight();
				if (OnResize != null)
				{
					OnResize();
				}
				if (m_eventOnResize != null)
				{
					m_eventOnResize.Invoke();
				}
				ResizeTexture();
			}
			Call_UpdateVideoTexture();
			if (m_iCurrentSeekPosition == Call_GetSeekPosition())
			{
				if (!m_bBuffring)
				{
					m_bBuffring = true;
					if (OnVideoBuffering != null)
					{
						OnVideoBuffering();
					}
					if (m_eventOnVideoBuffering != null)
					{
						m_eventOnVideoBuffering.Invoke();
					}
				}
			}
			else
			{
				if (m_bBuffring)
				{
					if (OnVideoBufferingEnd != null)
					{
						OnVideoBufferingEnd();
					}
					if (m_eventOnVideoBufferingEnd != null)
					{
						m_eventOnVideoBufferingEnd.Invoke();
					}
				}
				m_bBuffring = false;
			}
			m_iCurrentSeekPosition = Call_GetSeekPosition();
		}
		if (m_CurrentState != Call_GetStatus())
		{
			m_CurrentState = Call_GetStatus();
			if (m_CurrentState == MEDIAPLAYER_STATE.READY)
			{
				if (m_bPause)
				{
					m_bPause = false;
					Call_SetSeekPosition(m_iPauseFrame);
				}
				else if (m_bPlaying)
				{
					m_bPlaying = false;
					Call_SetSeekPosition(m_iPauseFrame);
					Call_Play(m_iPauseFrame);
				}
				else
				{
					if (OnReady != null)
					{
						OnReady();
					}
					if (m_eventOnReady != null)
					{
						m_eventOnReady.Invoke();
					}
					if (m_bAutoPlay)
					{
						Call_Play(0);
					}
					if (m_bReadyPlay)
					{
						Call_Play(0);
						m_bReadyPlay = false;
					}
					SetVolume(m_fVolume);
				}
			}
			else if (m_CurrentState == MEDIAPLAYER_STATE.END)
			{
				if (OnEnd != null)
				{
					OnEnd();
				}
				if (m_eventOnEnd != null)
				{
					m_eventOnEnd.Invoke();
				}
				if (m_bLoop)
				{
					Call_Play(0);
				}
			}
			else if (m_CurrentState == MEDIAPLAYER_STATE.ERROR)
			{
				OnError((MEDIAPLAYER_ERROR)Call_GetError(), (MEDIAPLAYER_ERROR)Call_GetErrorExtra());
			}
		}
		GL.InvalidateState();
	}

	public void DeleteVideoTexture()
	{
		if (m_VideoTextureDummy != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureDummy);
			m_VideoTextureDummy = null;
		}
		if (m_VideoTexture != null)
		{
			UnityEngine.Object.Destroy(m_VideoTexture);
			m_VideoTexture = null;
		}
		if (m_VideoTextureDummyY != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureDummyY);
			m_VideoTextureDummyY = null;
		}
		if (m_VideoTextureY != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureY);
			m_VideoTextureY = null;
		}
		if (m_VideoTextureDummyU != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureDummyU);
			m_VideoTextureDummyU = null;
		}
		if (m_VideoTextureU != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureU);
			m_VideoTextureU = null;
		}
		if (m_VideoTextureDummyV != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureDummyV);
			m_VideoTextureDummyV = null;
		}
		if (m_VideoTextureV != null)
		{
			UnityEngine.Object.Destroy(m_VideoTextureV);
			m_VideoTextureV = null;
		}
	}

	public void ResizeTexture()
	{
		if (m_iWidth == 0 || m_iHeight == 0)
		{
			return;
		}
		if (m_VideoTexture != null)
		{
			if (m_VideoTextureDummy != null)
			{
				UnityEngine.Object.Destroy(m_VideoTextureDummy);
				m_VideoTextureDummy = null;
			}
			m_VideoTextureDummy = m_VideoTexture;
			m_VideoTexture = null;
		}
		if (m_VideoTextureY != null)
		{
			if (m_VideoTextureDummyY != null)
			{
				UnityEngine.Object.Destroy(m_VideoTextureDummyY);
				m_VideoTextureDummyY = null;
			}
			m_VideoTextureDummyY = m_VideoTextureY;
			m_VideoTextureY = null;
		}
		if (m_VideoTextureU != null)
		{
			if (m_VideoTextureDummyU != null)
			{
				UnityEngine.Object.Destroy(m_VideoTextureDummyU);
				m_VideoTextureDummyU = null;
			}
			m_VideoTextureDummyU = m_VideoTextureU;
			m_VideoTextureU = null;
		}
		if (m_VideoTextureV != null)
		{
			if (m_VideoTextureDummyV != null)
			{
				UnityEngine.Object.Destroy(m_VideoTextureDummyV);
				m_VideoTextureDummyV = null;
			}
			m_VideoTextureDummyV = m_VideoTextureV;
			m_VideoTextureV = null;
		}
		if (m_bPC_FastMode)
		{
			m_VideoTextureY = new Texture2D(Call_GetVideoWidth(), Call_GetVideoHeight(), TextureFormat.Alpha8, mipChain: false);
			m_VideoTextureU = new Texture2D(Call_GetVideoWidth() / 2, Call_GetVideoHeight() / 2, TextureFormat.Alpha8, mipChain: false);
			m_VideoTextureV = new Texture2D(Call_GetVideoWidth() / 2, Call_GetVideoHeight() / 2, TextureFormat.Alpha8, mipChain: false);
		}
		else if (m_bSupportRockchip)
		{
			m_VideoTexture = new Texture2D(Call_GetVideoWidth(), Call_GetVideoHeight(), TextureFormat.RGB565, mipChain: false);
		}
		else
		{
			m_VideoTexture = new Texture2D(Call_GetVideoWidth(), Call_GetVideoHeight(), TextureFormat.RGBA32, mipChain: false);
		}
		if (m_VideoTexture != null)
		{
			m_VideoTexture.filterMode = FilterMode.Bilinear;
			m_VideoTexture.wrapMode = TextureWrapMode.Clamp;
		}
		if (m_bPC_FastMode)
		{
			m_texPtrY = m_VideoTextureY.GetNativeTexturePtr();
			m_texPtrU = m_VideoTextureU.GetNativeTexturePtr();
			m_texPtrV = m_VideoTextureV.GetNativeTexturePtr();
		}
		else
		{
			m_texPtr = m_VideoTexture.GetNativeTexturePtr();
		}
		Call_SetUnityTexture((int)m_texPtr);
		Call_SetWindowSize();
	}

	public void Resize()
	{
		if (m_CurrentState != MEDIAPLAYER_STATE.PLAYING || Call_GetVideoWidth() <= 0 || Call_GetVideoHeight() <= 0 || m_objResize == null)
		{
			return;
		}
		int width = Screen.width;
		int height = Screen.height;
		float num = (float)height / (float)width;
		int num2 = Call_GetVideoWidth();
		int num3 = Call_GetVideoHeight();
		float num4 = (float)num3 / (float)num2;
		float num5 = num / num4;
		for (int i = 0; i < m_objResize.Length; i++)
		{
			if (m_objResize[i] == null)
			{
				continue;
			}
			if (m_bFullScreen)
			{
				m_objResize[i].transform.localScale = new Vector3(20f / num, 20f / num, 1f);
				if (num4 < 1f)
				{
					if (num < 1f && num4 > num)
					{
						m_objResize[i].transform.localScale *= num5;
					}
					m_ScaleValue = MEDIA_SCALE.SCALE_X_TO_Y;
				}
				else
				{
					if (num > 1f)
					{
						if (num4 >= num)
						{
							m_objResize[i].transform.localScale *= num5;
						}
					}
					else
					{
						m_objResize[i].transform.localScale *= num5;
					}
					m_ScaleValue = MEDIA_SCALE.SCALE_X_TO_Y;
				}
			}
			if (m_ScaleValue == MEDIA_SCALE.SCALE_X_TO_Y)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.x, m_objResize[i].transform.localScale.x * num4, m_objResize[i].transform.localScale.z);
			}
			else if (m_ScaleValue == MEDIA_SCALE.SCALE_X_TO_Y_2)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.x, m_objResize[i].transform.localScale.x * num4 / 2f, m_objResize[i].transform.localScale.z);
			}
			else if (m_ScaleValue == MEDIA_SCALE.SCALE_X_TO_Z)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.x, m_objResize[i].transform.localScale.y, m_objResize[i].transform.localScale.x * num4);
			}
			else if (m_ScaleValue == MEDIA_SCALE.SCALE_Y_TO_X)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.y / num4, m_objResize[i].transform.localScale.y, m_objResize[i].transform.localScale.z);
			}
			else if (m_ScaleValue == MEDIA_SCALE.SCALE_Y_TO_Z)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.x, m_objResize[i].transform.localScale.y, m_objResize[i].transform.localScale.y / num4);
			}
			else if (m_ScaleValue == MEDIA_SCALE.SCALE_Z_TO_X)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.z * num4, m_objResize[i].transform.localScale.y, m_objResize[i].transform.localScale.z);
			}
			else if (m_ScaleValue == MEDIA_SCALE.SCALE_Z_TO_Y)
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.x, m_objResize[i].transform.localScale.z * num4, m_objResize[i].transform.localScale.z);
			}
			else
			{
				m_objResize[i].transform.localScale = new Vector3(m_objResize[i].transform.localScale.x, m_objResize[i].transform.localScale.y, m_objResize[i].transform.localScale.z);
			}
		}
	}

	private void OnError(MEDIAPLAYER_ERROR iCode, MEDIAPLAYER_ERROR iCodeExtra)
	{
		string empty = string.Empty;
		empty = iCode switch
		{
			MEDIAPLAYER_ERROR.MEDIA_ERROR_NOT_VALID_FOR_PROGRESSIVE_PLAYBACK => "MEDIA_ERROR_NOT_VALID_FOR_PROGRESSIVE_PLAYBACK",
			MEDIAPLAYER_ERROR.MEDIA_ERROR_SERVER_DIED => "MEDIA_ERROR_SERVER_DIED",
			MEDIAPLAYER_ERROR.MEDIA_ERROR_UNKNOWN => "MEDIA_ERROR_UNKNOWN",
			_ => "Unknown error " + iCode,
		} + " ";
		switch (iCodeExtra)
		{
		case MEDIAPLAYER_ERROR.MEDIA_ERROR_IO:
			empty += "MEDIA_ERROR_IO";
			break;
		case MEDIAPLAYER_ERROR.MEDIA_ERROR_MALFORMED:
			empty += "MEDIA_ERROR_MALFORMED";
			break;
		case MEDIAPLAYER_ERROR.MEDIA_ERROR_TIMED_OUT:
			empty += "MEDIA_ERROR_TIMED_OUT";
			break;
		case MEDIAPLAYER_ERROR.MEDIA_ERROR_UNSUPPORTED:
			empty += "MEDIA_ERROR_UNSUPPORTED";
			break;
		default:
			empty = "Unknown error " + iCode;
			break;
		}
		if (OnVideoError != null)
		{
			OnVideoError(iCode, iCodeExtra);
		}
		if (m_eventOnVideoError != null)
		{
			m_eventOnVideoError.Invoke();
		}
	}

	public void Destroy()
	{
		OnDestroy();
	}

	private void OnDestroy()
	{
		if (!_destroyed)
		{
			_destroyed = true;
			Call_UnLoad();
			DeleteVideoTexture();
			Call_Destroy();
		}
	}

	private void OnApplicationPause(bool bPause)
	{
		if (!m_bPlaying && !m_bPause && (m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.NOT_READY))
		{
			return;
		}
		if (bPause)
		{
			if (m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
			{
				m_bPause = true;
			}
			Call_Pause();
		}
		else
		{
			Call_RePlay();
		}
	}

	public MEDIAPLAYER_STATE GetCurrentState()
	{
		return m_CurrentState;
	}

	public Texture2D GetVideoTexture()
	{
		return m_VideoTexture;
	}

	public void Play()
	{
		if (!m_bInit)
		{
			return;
		}
		if (m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
		{
			Call_RePlay();
		}
		else if (m_CurrentState == MEDIAPLAYER_STATE.STOPPED)
		{
			Load(m_strFileName);
		}
		else if (m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.STOPPED || m_CurrentState == MEDIAPLAYER_STATE.END)
		{
			Call_Play(0);
		}
		else if (m_CurrentState == MEDIAPLAYER_STATE.NOT_READY)
		{
			if (!string.IsNullOrEmpty(m_strFileName))
			{
				Load(m_strFileName);
			}
			m_bReadyPlay = true;
		}
	}

	public void Stop()
	{
		if (m_bInit)
		{
			if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING)
			{
				Call_Pause();
			}
			UnLoad();
			m_CurrentState = MEDIAPLAYER_STATE.STOPPED;
			m_iCurrentSeekPosition = 0;
		}
	}

	public void Pause()
	{
		if (m_bInit && m_CurrentState == MEDIAPLAYER_STATE.PLAYING)
		{
			Call_Pause();
			m_CurrentState = MEDIAPLAYER_STATE.PAUSED;
		}
	}

	public void Load(string strFileName)
	{
		if (!m_bInit)
		{
			InitPlayer();
		}
		if (GetCurrentState() != MEDIAPLAYER_STATE.NOT_READY)
		{
			UnLoad();
		}
		m_bIsFirstFrameReady = false;
		m_bCheckFBO = false;
		m_strFileName = strFileName;
		if (m_bInit)
		{
			Call_Load(strFileName, 0);
			m_CurrentState = MEDIAPLAYER_STATE.NOT_READY;
		}
	}

	public void SetVolume(float fVolume)
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.STOPPED)
		{
			m_fVolume = fVolume;
			Call_SetVolume(fVolume);
		}
	}

	public int GetSeekPosition()
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END)
		{
			return m_iCurrentSeekPosition;
		}
		return 0;
	}

	public void SeekTo(int iSeek)
	{
		if (m_bInit && (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.PAUSED))
		{
			Call_SetSeekPosition(iSeek);
		}
	}

	public void SetSpeed(float fSpeed)
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.STOPPED)
		{
			m_fSpeed = fSpeed;
			Call_SetSpeed(fSpeed);
		}
	}

	public int GetDuration()
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.STOPPED)
		{
			return Call_GetDuration();
		}
		return 0;
	}

	public float GetSeekBarValue()
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.STOPPED)
		{
			if (GetDuration() == 0)
			{
				return 0f;
			}
			return (float)GetSeekPosition() / (float)GetDuration();
		}
		return 0f;
	}

	public void SetSeekBarValue(float fValue)
	{
		if ((m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.STOPPED) && GetDuration() != 0)
		{
			SeekTo((int)((float)GetDuration() * fValue));
		}
	}

	public int GetCurrentSeekPercent()
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING || m_CurrentState == MEDIAPLAYER_STATE.PAUSED || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.READY)
		{
			return Call_GetCurrentSeekPercent();
		}
		return 0;
	}

	public int GetVideoWidth()
	{
		return Call_GetVideoWidth();
	}

	public int GetVideoHeight()
	{
		return Call_GetVideoHeight();
	}

	public void UnLoad()
	{
		if (m_bInit)
		{
			m_bCheckFBO = false;
			Call_UnLoad();
			m_CurrentState = MEDIAPLAYER_STATE.NOT_READY;
		}
	}

	public void SelectSoundTrack(int iIndex)
	{
		Call_SelectSoundTrack(iIndex);
	}

	public int[] GetSoundTrack()
	{
		return Call_GetSoundTrack();
	}

	private void Call_Destroy()
	{
		ReleaseLoader();
		if (threadVideo != null)
		{
			while (threadVideo.IsAlive)
			{
				threadVideo.Abort();
			}
			threadVideo = null;
		}
		if (threadVideo2 != null)
		{
			while (threadVideo2.IsAlive)
			{
				threadVideo2.Abort();
			}
			threadVideo2 = null;
		}
		ffmpeg.avformat_network_deinit();
		ReleaseTexture(m_iID);
	}

	private void ReleaseLoader()
	{
		Thread thread = loader;
		if (thread != null)
		{
			while (thread.IsAlive)
			{
				thread.Abort();
			}
		}
		loader = null;
	}

	private unsafe void Call_UnLoad()
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.NOT_READY)
		{
			return;
		}
		m_CurrentState = MEDIAPLAYER_STATE.NOT_READY;
		bUnloadVideo = true;
		m_bSetTextureFirst = false;
		ReleaseLoader();
		if (threadVideo != null)
		{
			while (threadVideo.IsAlive)
			{
				threadVideo.Abort();
			}
			threadVideo = null;
		}
		if (threadVideo2 != null)
		{
			while (threadVideo2.IsAlive)
			{
				threadVideo2.Abort();
			}
			threadVideo2 = null;
		}
		if (listAudio != null)
		{
			listAudio.Clear();
			listAudio = null;
		}
		if (listVideo != null)
		{
			listVideo.Clear();
			listVideo = null;
		}
		if (listAudioPts != null)
		{
			listAudioPts.Clear();
			listAudioPts = null;
		}
		if (listAudioPtsTime != null)
		{
			listAudioPtsTime.Clear();
			listAudioPtsTime = null;
		}
		if (listVideoPts != null)
		{
			listVideoPts.Clear();
			listVideoPts = null;
		}
		fCurrentSeekTime = 0f;
		fLastFrameTime = 0f;
		if (pPacket != null)
		{
			ffmpeg.av_free_packet(pPacket);
			Marshal.FreeCoTaskMem((IntPtr)pPacket);
			pPacket = null;
		}
		int count = m_queuePacketVideo.Count;
		for (int i = 0; i < count; i++)
		{
			if (m_queuePacketVideo.Count > 0)
			{
				pPacket = (AVPacket*)(void*)m_queuePacketVideo.Dequeue();
			}
			if (pPacket != null)
			{
				ffmpeg.av_free_packet(pPacket);
				Marshal.FreeCoTaskMem((IntPtr)pPacket);
				pPacket = null;
			}
		}
		count = m_queuePacketSound.Count;
		for (int j = 0; j < count; j++)
		{
			if (m_queuePacketSound.Count > 0)
			{
				pPacket = (AVPacket*)(void*)m_queuePacketSound.Dequeue();
			}
			if (pPacket != null)
			{
				ffmpeg.av_free_packet(pPacket);
				Marshal.FreeCoTaskMem((IntPtr)pPacket);
				pPacket = null;
			}
		}
		if (pConvertedFrame != null)
		{
			ffmpeg.av_free(pConvertedFrame);
			pConvertedFrame = null;
		}
		if (pConvertedFrameBuffer != null)
		{
			ffmpeg.av_free(pConvertedFrameBuffer);
			pConvertedFrameBuffer = null;
		}
		if (pConvertContext != null)
		{
			ffmpeg.sws_freeContext(pConvertContext);
			pConvertContext = null;
		}
		if (pDecodedFrame != null)
		{
			ffmpeg.av_free(pDecodedFrame);
			pDecodedFrame = null;
		}
		if (pDecodedAudioFrame != null)
		{
			ffmpeg.av_free(pDecodedAudioFrame);
		}
		pDecodedAudioFrame = null;
		if (pCodecContext != null)
		{
			ffmpeg.avcodec_close(pCodecContext);
		}
		pCodecContext = null;
		if (pAudioCodecContext != null)
		{
			ffmpeg.avcodec_close(pAudioCodecContext);
		}
		pAudioCodecContext = null;
		if (pFormatContext != null)
		{
			AVFormatContext* ptr = pFormatContext;
			ffmpeg.avformat_close_input(&ptr);
		}
		pFormatContext = null;
		if (audioSource != null)
		{
			audioSource.Stop();
		}
		if (audioClip != null)
		{
			UnityEngine.Object.Destroy(audioClip);
			audioClip = null;
		}
		bUnloadVideo = false;
	}

	private unsafe bool Call_Load(string strFileName, int iSeek)
	{
		if (loader != null)
		{
			return false;
		}
		m_bFirst = true;
		if (m_queuePacketVideo == null)
		{
			m_queuePacketVideo = new Queue<IntPtr>();
		}
		if (m_queuePacketSound == null)
		{
			m_queuePacketSound = new Queue<IntPtr>();
		}
		fCurrentSeekTime = 0f;
		fLastFrameTime = 0f;
		iSoundCount = 0L;
		iInitCount = 0;
		bSeekTo = true;
		bEnd = false;
		bEndCheck = false;
		if (audioSource != null)
		{
			audioSource.Stop();
			audioSource.time = 0f;
		}
		if (audioClip != null)
		{
			UnityEngine.Object.Destroy(audioClip);
			audioClip = null;
		}
		pFormatContext = ffmpeg.avformat_alloc_context();
		if (!strFileName.Contains("://"))
		{
			strFileName = Application.streamingAssetsPath + "/" + strFileName;
		}
		else if (strFileName.Contains("file://"))
		{
			strFileName = strFileName.Replace("file://", string.Empty);
		}
		m_typeGraphic = SystemInfo.graphicsDeviceType;
		loader = new Thread((ThreadStart)delegate
		{
			if (!bUnloadVideo)
			{
				AVFormatContext* ptr = ffmpeg.avformat_alloc_context();
				if (ffmpeg.avformat_open_input(&ptr, strFileName, null, null) != 0)
				{
					pFormatContext = null;
					m_CurrentState = MEDIAPLAYER_STATE.ERROR;
					throw new ApplicationException("Could not open file");
				}
				pFormatContext = ptr;
				if (ffmpeg.avformat_find_stream_info(pFormatContext, null) != 0)
				{
					m_CurrentState = MEDIAPLAYER_STATE.ERROR;
					throw new ApplicationException("Could not find stream info");
				}
				LoadVideoPart2();
			}
		});
		loader.Priority = System.Threading.ThreadPriority.AboveNormal;
		loader.IsBackground = true;
		loader.Start();
		return true;
	}

	private unsafe void LoadVideoPart2()
	{
		pStream = null;
		pStreamAudio = null;
		bool flag = false;
		bool flag2 = false;
		if (pFormatContext == null)
		{
			return;
		}
		for (int i = 0; i < pFormatContext->nb_streams; i++)
		{
			if (pFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
			{
				if (!flag)
				{
					flag = true;
					pStream = pFormatContext->streams[i];
					iStreamIndex = i;
				}
			}
			else if (pFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO && !flag2)
			{
				flag2 = true;
				pStreamAudio = pFormatContext->streams[i];
				iStreamAudioIndex = i;
			}
		}
		if (pStream == null)
		{
			m_CurrentState = MEDIAPLAYER_STATE.ERROR;
			throw new ApplicationException("Could not found video stream");
		}
		if (pStreamAudio == null)
		{
		}
		AVCodecContext codec = *pStream->codec;
		m_iWidth = codec.width;
		m_iHeight = codec.height;
		AVPixelFormat pix_fmt = codec.pix_fmt;
		AVCodecID codec_id = codec.codec_id;
		AVPixelFormat aVPixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;
		if (pix_fmt == AVPixelFormat.AV_PIX_FMT_NONE)
		{
			pConvertContext = ffmpeg.sws_getContext(m_iWidth, m_iHeight, AVPixelFormat.AV_PIX_FMT_YUV420P, m_iWidth, m_iHeight, aVPixelFormat, 1, null, null, null);
		}
		else
		{
			pConvertContext = ffmpeg.sws_getContext(m_iWidth, m_iHeight, pix_fmt, m_iWidth, m_iHeight, aVPixelFormat, 1, null, null, null);
		}
		if (pConvertContext == null)
		{
			m_CurrentState = MEDIAPLAYER_STATE.ERROR;
			throw new ApplicationException("Could not initialize the conversion context");
		}
		pConvertedFrame = ffmpeg.av_frame_alloc();
		int num = ffmpeg.avpicture_get_size(aVPixelFormat, m_iWidth, m_iHeight);
		pConvertedFrameBuffer = (sbyte*)ffmpeg.av_malloc((ulong)num);
		AVPicture* picture = (AVPicture*)pConvertedFrame;
		ffmpeg.avpicture_fill(picture, pConvertedFrameBuffer, aVPixelFormat, m_iWidth, m_iHeight);
		AVCodec* ptr = ffmpeg.avcodec_find_decoder(codec_id);
		if (ptr == null)
		{
			m_CurrentState = MEDIAPLAYER_STATE.ERROR;
			throw new ApplicationException("Unsupported codec");
		}
		pCodecContext = pStream->codec;
		pCodecContext->hwaccel = ff_find_hwaccel(pCodecContext->codec_id, pix_fmt);
		if ((ptr->capabilities & 8) == 8)
		{
		}
		pCodecContext->flags |= 524288;
		pCodecContext->flags2 |= 1;
		if (ffmpeg.avcodec_open2(pCodecContext, ptr, null) < 0)
		{
			m_CurrentState = MEDIAPLAYER_STATE.ERROR;
			throw new ApplicationException("Could not open codec");
		}
		if (pStreamAudio != null)
		{
			AVCodecContext codec2 = *pStreamAudio->codec;
			AVCodec* ptr2 = ffmpeg.avcodec_find_decoder(codec2.codec_id);
			if (ptr2 == null)
			{
				m_CurrentState = MEDIAPLAYER_STATE.ERROR;
				throw new ApplicationException("Unsupported codec");
			}
			pAudioCodecContext = pStreamAudio->codec;
			if (ffmpeg.avcodec_open2(pAudioCodecContext, ptr2, null) < 0)
			{
				m_CurrentState = MEDIAPLAYER_STATE.ERROR;
				throw new ApplicationException("Could not open codec");
			}
		}
		pDecodedFrame = ffmpeg.av_frame_alloc();
		pDecodedAudioFrame = ffmpeg.av_frame_alloc();
		listAudio = new List<float[]>();
		listVideo = new Queue<byte[]>();
		listAudioPts = new List<double>();
		listAudioPtsTime = new List<double>();
		listVideoPts = new Queue<float>();
		if (!m_strFileName.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
		{
			action = Interrupt1;
		}
		bVideoFirstFrameReady = false;
		if (m_bAutoPlay || m_bReadyPlay)
		{
			m_bReadyPlay = false;
			if (OnReady != null)
			{
				OnReady();
			}
			if (m_eventOnReady != null)
			{
				m_eventOnReady.Invoke();
			}
			m_CurrentState = MEDIAPLAYER_STATE.PLAYING;
		}
		else
		{
			m_CurrentState = MEDIAPLAYER_STATE.READY;
			if (OnReady != null)
			{
				OnReady();
			}
			if (m_eventOnReady != null)
			{
				m_eventOnReady.Invoke();
			}
		}
		loader = null;
	}

	private unsafe AVHWAccel* ff_find_hwaccel(AVCodecID codec_id, AVPixelFormat pix_fmt)
	{
		AVHWAccel* ptr = null;
		while ((ptr = ffmpeg.av_hwaccel_next(ptr)) != null)
		{
			if (ptr->id == codec_id && ptr->pix_fmt == pix_fmt)
			{
				return ptr;
			}
		}
		return null;
	}

	public void Interrupt1()
	{
		bInterrupt = true;
	}

	private static double av_q2d(AVRational a)
	{
		return (double)a.num / (double)a.den;
	}

	private static void DebugMethod(string message)
	{
	}

	private void ThreadUpdate2()
	{
		while (!bUnloadVideo)
		{
			while (listVideo.Count > 30 || (bEnd && m_queuePacketVideo.Count == 0))
			{
				if (bUnloadVideo || (bEnd && m_queuePacketVideo.Count == 0 && m_queuePacketSound.Count == 0))
				{
					return;
				}
				UpdateSound();
				Thread.Sleep(1);
			}
			UpdateVideo();
			UpdateSound();
			UpdateSound();
			Thread.Sleep(1);
		}
	}

	private void ThreadUpdate()
	{
		if (bEnd)
		{
			return;
		}
		while (!bEndCheck && m_CurrentState != MEDIAPLAYER_STATE.ERROR)
		{
			if (bUnloadVideo)
			{
				return;
			}
			if (listVideo != null)
			{
				if (bInterrupt)
				{
					bInterrupt = false;
				}
				if (m_queuePacketVideo.Count < 256)
				{
					ReadPacket();
				}
			}
			Thread.Sleep(5);
		}
		while (bEndCheck && !bUnloadVideo)
		{
			if (m_queuePacketVideo.Count == 0)
			{
				bEnd = true;
				bEndCheck = false;
				break;
			}
		}
	}

	private unsafe void ReadPacket()
	{
		if (bEndCheck)
		{
			if (m_queuePacketVideo.Count == 0)
			{
				bEnd = true;
				bEndCheck = false;
			}
			return;
		}
		AVPacket* ptr = (AVPacket*)(void*)Marshal.AllocCoTaskMem(sizeof(AVPacket));
		ffmpeg.av_init_packet(ptr);
		int num = ffmpeg.av_read_frame(pFormatContext, ptr);
		if (num < 0)
		{
			if (num == -541478725)
			{
				bEndCheck = true;
			}
			else if (ptr != null)
			{
				ffmpeg.av_free_packet(ptr);
				Marshal.FreeCoTaskMem((IntPtr)ptr);
				ptr = null;
			}
			return;
		}
		if (ptr->stream_index == iStreamIndex)
		{
			lock (m_queuePacketVideo)
			{
				m_queuePacketVideo.Enqueue((IntPtr)ptr);
				return;
			}
		}
		lock (m_queuePacketSound)
		{
			m_queuePacketSound.Enqueue((IntPtr)ptr);
		}
	}

	private unsafe void UpdateSound()
	{
		int num = 0;
		if (m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
		{
			return;
		}
		lock (m_queuePacketSound)
		{
			if (m_queuePacketSound.Count > 0)
			{
				pPacket = (AVPacket*)(void*)m_queuePacketSound.Dequeue();
			}
		}
		if (pPacket != null && pStreamAudio != null && pPacket->stream_index == iStreamAudioIndex)
		{
			int num2 = ffmpeg.avcodec_decode_audio4(pAudioCodecContext, pDecodedAudioFrame, &num, pPacket);
			if (num2 >= 0 && num == 1)
			{
				int num3 = ffmpeg.av_samples_get_buffer_size(null, pAudioCodecContext->channels, pDecodedAudioFrame->nb_samples, pAudioCodecContext->sample_fmt, 1);
				int num4 = ffmpeg.av_samples_get_buffer_size(null, pAudioCodecContext->channels, pDecodedAudioFrame->nb_samples, AVSampleFormat.AV_SAMPLE_FMT_FLT, 1);
				sbyte* ptr = (sbyte*)(void*)Marshal.AllocCoTaskMem(num4);
				SwrContext* s = null;
				if (pAudioCodecContext->channel_layout == 0)
				{
					long num5 = ffmpeg.av_get_default_channel_layout(pAudioCodecContext->channels);
					s = ffmpeg.swr_alloc_set_opts(null, num5, AVSampleFormat.AV_SAMPLE_FMT_FLT, pAudioCodecContext->sample_rate, num5, pAudioCodecContext->sample_fmt, pAudioCodecContext->sample_rate, 0, null);
				}
				else
				{
					s = ffmpeg.swr_alloc_set_opts(null, (long)pAudioCodecContext->channel_layout, AVSampleFormat.AV_SAMPLE_FMT_FLT, pAudioCodecContext->sample_rate, (long)pAudioCodecContext->channel_layout, pAudioCodecContext->sample_fmt, pAudioCodecContext->sample_rate, 0, null);
				}
				int num6 = 0;
				if ((num6 = ffmpeg.swr_init(s)) < 0)
				{
				}
				ffmpeg.swr_convert(s, &ptr, num4, pDecodedAudioFrame->extended_data, pDecodedAudioFrame->nb_samples);
				sbyte* value = ptr;
				IntPtr source = new IntPtr(value);
				byte[] array = new byte[num4];
				Marshal.Copy(source, array, 0, num4);
				if (pPacket->dts != long.MinValue)
				{
					pts = ffmpeg.av_frame_get_best_effort_timestamp(pDecodedAudioFrame);
				}
				else
				{
					pts = 0.0;
				}
				pts *= av_q2d(pStreamAudio->time_base);
				if (bSeekTo)
				{
					double num7 = pts * (double)pDecodedAudioFrame->sample_rate;
					iSoundCount = (long)num7;
					fCurrentSeekTime = (float)pts;
					fLastFrameTime = fCurrentSeekTime;
					bSeekTo = false;
				}
				while (pts > (double)(300f * (float)(iInitCount + 1)))
				{
					iSoundCount -= (long)(300.0 * (double)pDecodedAudioFrame->sample_rate);
					iInitCount++;
				}
				fAudioData = new float[array.Length / 4];
				Buffer.BlockCopy(array, 0, fAudioData, 0, array.Length);
				lock (listAudio)
				{
					listAudio.Add(fAudioData);
					lock (listAudioPts)
					{
						lock (listAudioPtsTime)
						{
							iSoundCount += num4 / 4 / pDecodedAudioFrame->channels;
							listAudioPts.Add(iSoundCount);
							listAudioPtsTime.Add(pts);
						}
					}
				}
				ffmpeg.swr_free(&s);
				Marshal.FreeCoTaskMem((IntPtr)ptr);
			}
		}
		if (pPacket != null)
		{
			ffmpeg.av_free_packet(pPacket);
			Marshal.FreeCoTaskMem((IntPtr)pPacket);
			pPacket = null;
		}
	}

	private unsafe void UpdateVideo()
	{
		int num = 0;
		if (m_queuePacketVideo.Count < 3)
		{
			bBuffering = true;
		}
		if (m_queuePacketVideo.Count > 10 || bEndCheck)
		{
			bBuffering = false;
		}
		if (bBuffering || m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
		{
			return;
		}
		lock (m_queuePacketVideo)
		{
			if (m_queuePacketVideo.Count > 0)
			{
				pPacket = (AVPacket*)(void*)m_queuePacketVideo.Dequeue();
			}
		}
		if (pPacket != null && pPacket->stream_index == iStreamIndex)
		{
			int num2 = ffmpeg.avcodec_decode_video2(pCodecContext, pDecodedFrame, &num, pPacket);
			if (num2 < 0)
			{
				return;
			}
			if (pPacket->dts != long.MinValue)
			{
				pts = ffmpeg.av_frame_get_best_effort_timestamp(pDecodedFrame);
			}
			else
			{
				pts = 0.0;
			}
			pts *= av_q2d(pStream->time_base);
			if (num == 1)
			{
				if (pts > 0.0 && listVideo.Count > 2 && !m_bIsFirstFrameReady)
				{
					m_bIsFirstFrameReady = true;
					bVideoFirstFrameReady = true;
				}
				sbyte** data = &pDecodedFrame->data0;
				sbyte** data2 = &pConvertedFrame->data0;
				int* linesize = pDecodedFrame->linesize;
				int* linesize2 = pConvertedFrame->linesize;
				int num3 = *linesize;
				int num4 = linesize[1];
				int num5 = linesize[2];
				if (pCodecContext->width != m_iWidth)
				{
					lock (listVideo)
					{
						listVideo.Clear();
						lock (listVideoPts)
						{
							listVideoPts.Clear();
						}
					}
					if (pConvertedFrame != null)
					{
						ffmpeg.av_free(pConvertedFrame);
						pConvertedFrame = null;
					}
					if (pConvertedFrameBuffer != null)
					{
						ffmpeg.av_free(pConvertedFrameBuffer);
						pConvertedFrameBuffer = null;
					}
					if (pConvertContext != null)
					{
						ffmpeg.sws_freeContext(pConvertContext);
						pConvertContext = null;
					}
					AVPixelFormat aVPixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;
					AVCodecContext codec = *pStream->codec;
					AVPixelFormat pix_fmt = codec.pix_fmt;
					AVCodecID codec_id = codec.codec_id;
					pConvertContext = ffmpeg.sws_getContext(pCodecContext->width, pCodecContext->height, pix_fmt, pCodecContext->width, pCodecContext->height, aVPixelFormat, 1, null, null, null);
					if (pConvertContext == null)
					{
						m_CurrentState = MEDIAPLAYER_STATE.ERROR;
						throw new ApplicationException("Could not initialize the conversion context");
					}
					pConvertedFrame = ffmpeg.av_frame_alloc();
					int num6 = ffmpeg.avpicture_get_size(aVPixelFormat, pCodecContext->width, pCodecContext->height);
					pConvertedFrameBuffer = (sbyte*)ffmpeg.av_malloc((ulong)num6);
					AVPicture* picture = (AVPicture*)pConvertedFrame;
					ffmpeg.avpicture_fill(picture, pConvertedFrameBuffer, aVPixelFormat, pCodecContext->width, pCodecContext->height);
				}
				else if (m_bPC_FastMode)
				{
					int num7 = m_iWidth * m_iHeight + 2 * m_iWidth / 2 * m_iHeight / 2;
					byte[] array = new byte[num7];
					if (num3 != m_iWidth)
					{
						for (int i = 0; i < m_iHeight; i++)
						{
							sbyte* value = pDecodedFrame->data0 + num3 * i;
							IntPtr source = new IntPtr(value);
							Marshal.Copy(source, array, m_iWidth * i, m_iWidth);
						}
						for (int j = 0; j < m_iHeight / 2; j++)
						{
							sbyte* value = pDecodedFrame->data1 + num4 * j;
							IntPtr source = new IntPtr(value);
							Marshal.Copy(source, array, m_iWidth * m_iHeight + m_iWidth / 2 * j, m_iWidth / 2);
							value = pDecodedFrame->data2 + num5 * j;
							source = new IntPtr(value);
							Marshal.Copy(source, array, m_iWidth * m_iHeight + m_iWidth / 2 * m_iHeight / 2 + m_iWidth / 2 * j, m_iWidth / 2);
						}
					}
					else
					{
						sbyte* data3 = pDecodedFrame->data0;
						IntPtr source2 = new IntPtr(data3);
						Marshal.Copy(source2, array, 0, m_iWidth * m_iHeight);
						data3 = pDecodedFrame->data1;
						source2 = new IntPtr(data3);
						Marshal.Copy(source2, array, m_iWidth * m_iHeight, m_iWidth / 2 * m_iHeight / 2);
						data3 = pDecodedFrame->data2;
						source2 = new IntPtr(data3);
						Marshal.Copy(source2, array, m_iWidth * m_iHeight + m_iWidth / 2 * m_iHeight / 2, m_iWidth / 2 * m_iHeight / 2);
					}
					lock (listVideo)
					{
						listVideo.Enqueue(array);
						lock (listVideoPts)
						{
							listVideoPts.Enqueue((float)pts);
						}
					}
				}
				else
				{
					ffmpeg.sws_scale(pConvertContext, data, linesize, 0, m_iHeight, data2, linesize2);
					sbyte* data4 = pConvertedFrame->data0;
					IntPtr source3 = new IntPtr(data4);
					byte[] array2 = new byte[4 * m_iWidth * m_iHeight];
					Marshal.Copy(source3, array2, 0, 4 * m_iWidth * m_iHeight);
					lock (listVideo)
					{
						listVideo.Enqueue(array2);
						lock (listVideoPts)
						{
							listVideoPts.Enqueue((float)pts);
						}
					}
				}
			}
		}
		if (pPacket != null)
		{
			ffmpeg.av_free_packet(pPacket);
			Marshal.FreeCoTaskMem((IntPtr)pPacket);
			pPacket = null;
		}
	}

	private void OnAudioRead(float[] data)
	{
		if (listAudio.Count < 3)
		{
			return;
		}
		if (iSoundBufferCount == 0)
		{
			iSoundBufferCount = listAudio[0].Length;
		}
		if (iSoundBufferCount < data.Length)
		{
			Array.Copy(listAudio[0], listAudio[0].Length - iSoundBufferCount, data, 0, iSoundBufferCount);
			if (data.Length - iSoundBufferCount > listAudio[1].Length)
			{
				Array.Copy(listAudio[1], 0, data, 0, listAudio[1].Length);
				Array.Copy(listAudio[2], 0, data, iSoundBufferCount + listAudio[1].Length, data.Length - iSoundBufferCount - listAudio[1].Length);
				iSoundBufferCount = listAudio[2].Length - (data.Length - iSoundBufferCount - listAudio[1].Length);
				listAudio.RemoveAt(0);
				listAudioPts.RemoveAt(0);
				listAudioPtsTime.RemoveAt(0);
			}
			else
			{
				Array.Copy(listAudio[1], 0, data, iSoundBufferCount, data.Length - iSoundBufferCount);
				iSoundBufferCount = listAudio[1].Length - (data.Length - iSoundBufferCount);
			}
			listAudio.RemoveAt(0);
			listAudioPts.RemoveAt(0);
			listAudioPtsTime.RemoveAt(0);
		}
		else
		{
			Array.Copy(listAudio[0], listAudio[0].Length - iSoundBufferCount, data, 0, data.Length);
			iSoundBufferCount -= data.Length;
		}
		if (iSoundBufferCount == 0)
		{
			listAudio.RemoveAt(0);
			listAudioPts.RemoveAt(0);
			listAudioPtsTime.RemoveAt(0);
			iSoundBufferCount = listAudio[0].Length;
		}
	}

	private unsafe void Call_UpdateVideoTexture()
	{
		if (loader != null)
		{
			return;
		}
		if (bEnd && listVideo.Count == 0)
		{
			m_CurrentState = MEDIAPLAYER_STATE.END;
			if (OnEnd != null)
			{
				OnEnd();
			}
			if (m_eventOnEnd != null)
			{
				m_eventOnEnd.Invoke();
			}
			if (m_bLoop)
			{
				Load(m_strFileName);
				bEnd = false;
			}
			else
			{
				bEnd = false;
			}
			return;
		}
		if (!(audioSource != null) || m_CurrentState != MEDIAPLAYER_STATE.PLAYING || m_bIsFirstFrameReady)
		{
		}
		if (!bEnd)
		{
			if (threadVideo == null && m_CurrentState != MEDIAPLAYER_STATE.END && m_CurrentState != MEDIAPLAYER_STATE.NOT_READY && m_CurrentState != MEDIAPLAYER_STATE.ERROR)
			{
				threadVideo = new Thread(ThreadUpdate);
				threadVideo.IsBackground = true;
				threadVideo.Start();
			}
			if (threadVideo2 == null && m_CurrentState != MEDIAPLAYER_STATE.END && m_CurrentState != MEDIAPLAYER_STATE.NOT_READY && m_CurrentState != MEDIAPLAYER_STATE.ERROR)
			{
				threadVideo2 = new Thread(ThreadUpdate2);
				threadVideo2.IsBackground = true;
				threadVideo2.Start();
			}
		}
		if (m_CurrentState == MEDIAPLAYER_STATE.PLAYING && m_bIsFirstFrameReady)
		{
			if (bBuffering)
			{
			}
			if (listVideo.Count > 0)
			{
				fCurrentSeekTime += Time.deltaTime * m_fSpeed;
			}
		}
		if (fLastFrameTime > fCurrentSeekTime - 0.2f)
		{
			for (int i = 0; i < listAudio.Count; i++)
			{
				if (listAudioPtsTime.Count > i && audioSource == null && (int)((float)pAudioCodecContext->sample_rate * ((float)listAudioPtsTime[i] + (float)Call_GetDuration() / 1000f)) > 0)
				{
					audioSource = base.gameObject.AddComponent<AudioSource>();
					audioSource.volume = m_fVolume;
				}
				if (audioClip == null && audioSource != null)
				{
					audioClip = AudioClip.Create("videoAudio", (int)((float)pAudioCodecContext->sample_rate * 300f), pAudioCodecContext->channels, pAudioCodecContext->sample_rate, _3D: false, stream: false);
					audioSource.clip = audioClip;
				}
				if (audioSource != null && Call_GetDuration() > 0 && listAudioPts.Count > i && listAudioPts[i] >= 0.0)
				{
					OffServerSafeSetData(audioClip, listAudio[i], (int)(listAudioPts[i] % (double)((float)pAudioCodecContext->sample_rate * 300f)));
				}
			}
			if (audioSource != null && audioSource.isPlaying && Call_GetDuration() > 0)
			{
				listAudio.Clear();
				listAudioPts.Clear();
				listAudioPtsTime.Clear();
			}
			return;
		}
		if (listVideo.Count > 0)
		{
			if (audioSource != null && !audioSource.isPlaying)
			{
				audioSource.Play();
			}
			if (m_bPC_FastMode)
			{
				if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
				{
					m_texPtrY = m_VideoTextureY.GetNativeTexturePtr();
					m_texPtrU = m_VideoTextureU.GetNativeTexturePtr();
					m_texPtrV = m_VideoTextureV.GetNativeTexturePtr();
				}
				SetTextureFromUnity(m_iID, (IntPtr)0, m_texPtrY, m_texPtrU, m_texPtrV, m_iWidth, m_iHeight, listVideo.Dequeue());
				if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
				{
					GL.IssuePluginEvent(GetRenderEventFunc(), 8000 + m_iID);
				}
				else
				{
					GL.IssuePluginEvent(GetRenderEventFunc(), 7000 + m_iID);
				}
			}
			else
			{
				if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11)
				{
					m_texPtr = m_VideoTexture.GetNativeTexturePtr();
				}
				SetTextureFromUnity(m_iID, m_texPtr, (IntPtr)0, (IntPtr)0, (IntPtr)0, m_iWidth, m_iHeight, listVideo.Dequeue());
				if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
				{
					GL.IssuePluginEvent(GetRenderEventFunc(), 8000 + m_iID);
				}
				else
				{
					GL.IssuePluginEvent(GetRenderEventFunc(), 7000 + m_iID);
				}
			}
		}
		if (listVideoPts.Count > 0)
		{
			float num = listVideoPts.Dequeue();
			if (num > fCurrentSeekTime + 2f)
			{
				fCurrentSeekTime = num;
				double num2 = pts * (double)pDecodedAudioFrame->sample_rate;
				iSoundCount = (long)num2;
			}
			else if (num < fCurrentSeekTime - 2f)
			{
				fCurrentSeekTime = num;
				double num3 = pts * (double)pDecodedAudioFrame->sample_rate;
				iSoundCount = (long)num3;
			}
			if (fLastFrameTime == 0f)
			{
				if (num < 0f)
				{
					fLastFrameTime = 0f;
				}
				else
				{
					fLastFrameTime = num;
				}
				if (audioSource != null && Call_GetDuration() <= 0)
				{
				}
			}
			else if (num <= 0f)
			{
				fLastFrameTime = fCurrentSeekTime - 0.05f;
			}
			else
			{
				fLastFrameTime = num;
			}
		}
		if (audioSource != null && Call_GetDuration() >= 0)
		{
			if (audioSource.time - fLastFrameTime % 300f > 0.1f)
			{
				double num4 = pts * (double)pDecodedAudioFrame->sample_rate;
				iSoundCount = (long)num4;
				audioSource.time = fLastFrameTime % 300f;
				if (!audioSource.isPlaying)
				{
					audioSource.Play();
				}
			}
			else if (audioSource.time - fLastFrameTime % 300f < -0.3f)
			{
				double num5 = pts * (double)pDecodedAudioFrame->sample_rate;
				iSoundCount = (long)num5;
				audioSource.time = fLastFrameTime % 300f;
				if (!audioSource.isPlaying)
				{
					audioSource.Play();
				}
			}
		}
		if (OnVideoTextureUpdated != null)
		{
			OnVideoTextureUpdated(m_VideoTexture);
		}
		if (!bVideoFirstFrameReady)
		{
			return;
		}
		if (OnVideoFirstFrameReady != null)
		{
			OnVideoFirstFrameReady();
		}
		bVideoFirstFrameReady = false;
		if (m_eventOnVideoFirstFrameReady != null)
		{
			m_eventOnVideoFirstFrameReady.Invoke();
		}
		if (listAudio.Count > 0)
		{
			if (audioSource == null)
			{
				audioSource = base.gameObject.AddComponent<AudioSource>();
			}
			if (audioClip == null && audioSource != null)
			{
				audioClip = AudioClip.Create("videoAudio", (int)((float)pAudioCodecContext->sample_rate * 300f), pAudioCodecContext->channels, pAudioCodecContext->sample_rate, _3D: false, stream: false);
				audioSource.clip = audioClip;
			}
		}
		if (m_LastAudioPartSetted > listAudio.Count)
		{
			m_LastAudioPartSetted = -1;
		}
		for (int j = Math.Max(0, m_LastAudioPartSetted - 1); j < listAudio.Count; j++)
		{
			m_LastAudioPartSetted = j;
			if (((audioSource != null && Call_GetDuration() > 0) || Call_GetDuration() == 0) && listAudioPts.Count > j && listAudioPts[j] >= 0.0)
			{
				OffServerSafeSetData(audioClip, listAudio[j], (int)(listAudioPts[j] % (double)((float)pAudioCodecContext->sample_rate * 300f)));
			}
		}
	}

	private void Call_SetVolume(float fVolume)
	{
		if (audioSource != null)
		{
			audioSource.volume = fVolume;
		}
	}

	private unsafe void Call_SetSeekPosition(int iSeek)
	{
		if (loader != null || bEnd)
		{
			return;
		}
		if (threadVideo != null)
		{
			while (threadVideo.IsAlive)
			{
				threadVideo.Abort();
			}
			threadVideo = null;
		}
		if (threadVideo2 != null)
		{
			while (threadVideo2.IsAlive)
			{
				threadVideo2.Abort();
			}
			threadVideo2 = null;
		}
		bSeekTo = true;
		iInitCount = 0;
		long a = (long)iSeek * 1000L;
		a = ffmpeg.av_rescale_q(a, ffmpeg.av_get_time_base_q(), pStream->time_base);
		if (ffmpeg.av_seek_frame(pFormatContext, iStreamIndex, a, 1) < 0)
		{
		}
		fCurrentSeekTime = (float)iSeek / 1000f;
		listVideo.Clear();
		listVideoPts.Clear();
		listAudio.Clear();
		listAudioPts.Clear();
		listAudioPtsTime.Clear();
		int count = m_queuePacketVideo.Count;
		for (int i = 0; i < count; i++)
		{
			if (m_queuePacketVideo.Count > 0)
			{
				pPacket = (AVPacket*)(void*)m_queuePacketVideo.Dequeue();
			}
			if (pPacket != null)
			{
				ffmpeg.av_free_packet(pPacket);
				Marshal.FreeCoTaskMem((IntPtr)pPacket);
				pPacket = null;
			}
		}
		count = m_queuePacketSound.Count;
		for (int j = 0; j < count; j++)
		{
			if (m_queuePacketSound.Count > 0)
			{
				pPacket = (AVPacket*)(void*)m_queuePacketSound.Dequeue();
			}
			if (pPacket != null)
			{
				ffmpeg.av_free_packet(pPacket);
				Marshal.FreeCoTaskMem((IntPtr)pPacket);
				pPacket = null;
			}
		}
		UnityEngine.Object.Destroy(audioClip);
		audioClip = null;
		ffmpeg.avcodec_flush_buffers(pCodecContext);
	}

	private int Call_GetSeekPosition()
	{
		return (int)(fCurrentSeekTime * 1000f);
	}

	private void Call_Play(int iSeek)
	{
		if (m_CurrentState == MEDIAPLAYER_STATE.READY || m_CurrentState == MEDIAPLAYER_STATE.END || m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
		{
			SeekTo(iSeek);
			if (audioSource != null)
			{
				audioSource.time = (float)iSeek / 1000f;
			}
			bEnd = false;
			m_CurrentState = MEDIAPLAYER_STATE.PLAYING;
		}
	}

	private void Call_Reset()
	{
	}

	private void Call_Stop()
	{
		UnLoad();
		m_CurrentState = MEDIAPLAYER_STATE.STOPPED;
	}

	private void Call_RePlay()
	{
		if (audioSource != null)
		{
		}
		if (m_CurrentState == MEDIAPLAYER_STATE.PAUSED)
		{
			m_CurrentState = MEDIAPLAYER_STATE.PLAYING;
		}
	}

	private void Call_Pause()
	{
		if (audioSource != null)
		{
			audioSource.Pause();
		}
		m_CurrentState = MEDIAPLAYER_STATE.PAUSED;
	}

	private unsafe int Call_GetVideoWidth()
	{
		if (pCodecContext != null)
		{
			return pCodecContext->width;
		}
		return m_iWidth;
	}

	private unsafe int Call_GetVideoHeight()
	{
		if (pCodecContext != null)
		{
			return pCodecContext->height;
		}
		return m_iHeight;
	}

	private void Call_SetUnityTexture(int iTextureID)
	{
	}

	private void Call_SetWindowSize()
	{
	}

	private void Call_SetLooping(bool bLoop)
	{
	}

	private void Call_SetRockchip(bool bValue)
	{
	}

	public void Call_SetUnityActivity()
	{
		ffmpeg.av_register_all();
		ffmpeg.avcodec_register_all();
		ffmpeg.avformat_network_init();
		m_iID = SetTexture();
	}

	private int Call_GetError()
	{
		return 0;
	}

	private int Call_GetErrorExtra()
	{
		return 0;
	}

	private unsafe int Call_GetDuration()
	{
		if (pFormatContext != null)
		{
			return (int)(pFormatContext->duration / 1000);
		}
		return 0;
	}

	private unsafe int Call_GetCurrentSeekPercent()
	{
		if (pStream != null)
		{
			return (int)(((double)fCurrentSeekTime + (double)m_queuePacketVideo.Count / av_q2d(pStream->avg_frame_rate)) / ((double)Call_GetDuration() / 1000.0) * 100.0);
		}
		return -1;
	}

	private void Call_SetSplitOBB(bool bValue, string strOBBName)
	{
	}

	private void Call_SelectSoundTrack(int iIndex)
	{
	}

	private int[] Call_GetSoundTrack()
	{
		return null;
	}

	private void Call_SetSpeed(float fSpeed)
	{
		if (audioSource != null)
		{
			audioSource.pitch = fSpeed;
		}
	}

	private MEDIAPLAYER_STATE Call_GetStatus()
	{
		return m_CurrentState;
	}

	public IEnumerator DownloadStreamingVideoAndLoad(string strURL)
	{
		strURL = strURL.Trim();
		WWW www = new WWW(strURL);
		yield return www;
		if (string.IsNullOrEmpty(www.error))
		{
			if (!Directory.Exists(Application.persistentDataPath + "/Data"))
			{
				Directory.CreateDirectory(Application.persistentDataPath + "/Data");
			}
			string text = Application.persistentDataPath + "/Data" + strURL.Substring(strURL.LastIndexOf("/"));
			File.WriteAllBytes(text, www.bytes);
			Load("file://" + text);
		}
		www.Dispose();
		www = null;
		Resources.UnloadUnusedAssets();
	}

	public IEnumerator DownloadStreamingVideoAndLoad2(string strURL)
	{
		strURL = strURL.Trim();
		string write_path = Application.persistentDataPath + "/Data" + strURL.Substring(strURL.LastIndexOf("/"));
		if (File.Exists(write_path))
		{
			Load("file://" + write_path);
			yield break;
		}
		WWW www = new WWW(strURL);
		yield return www;
		if (string.IsNullOrEmpty(www.error))
		{
			if (!Directory.Exists(Application.persistentDataPath + "/Data"))
			{
				Directory.CreateDirectory(Application.persistentDataPath + "/Data");
			}
			File.WriteAllBytes(write_path, www.bytes);
			Load("file://" + write_path);
		}
		www.Dispose();
		www = null;
		Resources.UnloadUnusedAssets();
	}

	private IEnumerator CopyStreamingAssetVideoAndLoad(string strURL)
	{
		strURL = strURL.Trim();
		string write_path = Application.persistentDataPath + "/" + strURL;
		if (!File.Exists(write_path))
		{
			WWW www = new WWW(Application.streamingAssetsPath + "/" + strURL);
			yield return www;
			if (string.IsNullOrEmpty(www.error))
			{
				File.WriteAllBytes(write_path, www.bytes);
				Load("file://" + write_path);
			}
			www.Dispose();
			www = null;
		}
		else
		{
			Load("file://" + write_path);
		}
	}

	private static bool OffServerSafeSetData(AudioClip clip, float[] data, int offsetSamples)
	{
		if ((object)clip != null && data != null && data.Length != 0)
		{
			return clip.SetData(data, offsetSamples);
		}
		return false;
	}
}

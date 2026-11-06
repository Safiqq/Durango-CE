using UnityEngine;

public class NcBillboard : NcEffectBehaviour
{
	public interface ICameraTransformProvider
	{
		Transform GetTransform();
	}

	private class DefaultCameraTransformProvider : ICameraTransformProvider
	{
		Transform ICameraTransformProvider.GetTransform()
		{
			return null;
		}
	}

	public enum AXIS_TYPE
	{
		AXIS_FORWARD,
		AXIS_BACK,
		AXIS_RIGHT,
		AXIS_LEFT,
		AXIS_UP,
		AXIS_DOWN
	}

	public enum ROTATION
	{
		NONE,
		RND,
		ROTATE,
		INITIAL
	}

	public enum AXIS
	{
		X,
		Y,
		Z
	}

	public bool m_bCameraLookAt;

	public bool m_bFixedObjectUp;

	public bool m_bFixedStand;

	public AXIS_TYPE m_FrontAxis;

	public ROTATION m_RatationMode;

	public AXIS m_RatationAxis = AXIS.Z;

	public float m_fRotationValue = 180f;

	protected float m_fRndValue;

	protected float m_fTotalRotationValue;

	protected Quaternion m_qOiginal;

	public static ICameraTransformProvider CameraTransformProvider = new DefaultCameraTransformProvider();

	private void Awake()
	{
	}

	private void OnEnable()
	{
		UpdateBillboard();
	}

	public void UpdateBillboard()
	{
		m_fRndValue = Random.Range(0f, 360f);
		if (base.enabled)
		{
			Update();
		}
	}

	private void Start()
	{
		m_qOiginal = base.transform.rotation;
	}

	private void OnDisable()
	{
		m_qOiginal = default(Quaternion);
	}

	private void Update()
	{
		Transform transform = CameraTransformProvider.GetTransform();
		if (transform == null)
		{
			transform = Camera.main.transform;
		}
		Vector3 worldUp = ((!m_bFixedObjectUp) ? (transform.rotation * Vector3.up) : base.transform.up);
		if (m_bCameraLookAt)
		{
			base.transform.LookAt(transform, worldUp);
		}
		else
		{
			base.transform.LookAt(base.transform.position + transform.rotation * Vector3.back, worldUp);
		}
		switch (m_FrontAxis)
		{
		case AXIS_TYPE.AXIS_BACK:
			base.transform.Rotate(base.transform.up, 180f, Space.World);
			break;
		case AXIS_TYPE.AXIS_RIGHT:
			base.transform.Rotate(base.transform.up, 270f, Space.World);
			break;
		case AXIS_TYPE.AXIS_LEFT:
			base.transform.Rotate(base.transform.up, 90f, Space.World);
			break;
		case AXIS_TYPE.AXIS_UP:
			base.transform.Rotate(base.transform.right, 90f, Space.World);
			break;
		case AXIS_TYPE.AXIS_DOWN:
			base.transform.Rotate(base.transform.right, 270f, Space.World);
			break;
		}
		if (m_bFixedStand)
		{
			base.transform.rotation = Quaternion.Euler(new Vector3(0f, base.transform.rotation.eulerAngles.y, base.transform.rotation.eulerAngles.z));
		}
		if (m_RatationMode == ROTATION.RND)
		{
			base.transform.localRotation *= Quaternion.Euler((m_RatationAxis != AXIS.X) ? 0f : m_fRndValue, (m_RatationAxis != AXIS.Y) ? 0f : m_fRndValue, (m_RatationAxis != AXIS.Z) ? 0f : m_fRndValue);
		}
		else if (m_RatationMode == ROTATION.ROTATE)
		{
			float fRotationValue = m_fRotationValue;
			base.transform.localRotation *= Quaternion.Euler((m_RatationAxis != AXIS.X) ? 0f : fRotationValue, (m_RatationAxis != AXIS.Y) ? 0f : fRotationValue, (m_RatationAxis != AXIS.Z) ? 0f : fRotationValue);
		}
		else if (m_RatationMode == ROTATION.INITIAL)
		{
			if (m_qOiginal == default(Quaternion))
			{
				m_qOiginal = base.transform.rotation;
			}
			else
			{
				base.transform.rotation = m_qOiginal;
			}
		}
	}
}

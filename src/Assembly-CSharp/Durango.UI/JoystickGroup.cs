using System;
using System.Collections;
using UnityEngine;

namespace Durango.UI;

[DefaultExecutionOrder(32001)]
public class JoystickGroup : UIBase
{
	[SerializeField]
	private Transform _target;

	[SerializeField]
	private Vector3 _scale = Vector3.one;

	[SerializeField]
	private float _radius = 40f;

	[SerializeField]
	private float _followBeginRadius = 80f;

	[SerializeField]
	private float _followEndRadius = 80f;

	[SerializeField]
	private float _followSpeed = 0.5f;

	[SerializeField]
	private float _visibleMagnitude = 1f;

	[SerializeField]
	private bool _normalize;

	[SerializeField]
	private float _deadZone = 2f;

	[SerializeField]
	private float _fadeOutAlpha = 0.2f;

	[SerializeField]
	private float _fadeOutDelay = 1f;

	[SerializeField]
	private UIWidget[] _widgetsToFade;

	[SerializeField]
	private Transform[] _widgetsToCenter;

	[SerializeField]
	private UIWidget _fixedModeContainer;

	private Rect? _fixedModeContainerRect;

	private bool _followBegin;

	private Vector3 _userInitTouchPos;

	private bool _fixedMode;

	private bool _hudCustomized;

	private bool _hudWasEditing;

	private Vector3[] _hudOriginalScales;

	public Vector2 Position { get; private set; }

	public bool IsVisible { get; private set; }

	public bool Pressed { get; private set; }

	public Rect GetFixedModeContainerRect()
	{
		if (OffServerHudLayout.HasPlacement("joystick"))
		{
			Camera camera = NGUITools.FindCameraForLayer(_fixedModeContainer.gameObject.layer);
			Vector3[] worldCorners = _fixedModeContainer.worldCorners;
			Vector2 vector = camera.WorldToScreenPoint(worldCorners[0]);
			Vector2 vector2 = camera.WorldToScreenPoint(worldCorners[2]);
			return UnityEngine.Rect.MinMaxRect(vector.x, vector.y, vector2.x, vector2.y);
		}
		if (_fixedModeContainerRect.HasValue)
		{
			return _fixedModeContainerRect.Value;
		}
		Vector3 vector3 = UICamera.currentCamera.WorldToScreenPoint(_fixedModeContainer.transform.position);
		float num = _fixedModeContainer.width;
		float num2 = _fixedModeContainer.height;
		float x = vector3.x - num / 2f;
		float y = vector3.y - num2 / 2f;
		_fixedModeContainerRect = new Rect(x, y, num, num2);
		return _fixedModeContainerRect.Value;
	}

	private void Start()
	{
		for (int i = 0; i < _widgetsToFade.Length; i++)
		{
			UIWidget obj = _widgetsToFade[i];
			Color color = obj.color;
			color.a = 0f;
			obj.color = color;
		}
		GameSystem<InputSystem>.Instance().DrawModeChanged += SetFixedMode;
		UIWidget rootAnchor = UIRootAnchor.GetRootAnchor(AnchorType.Base);
		if (rootAnchor != null)
		{
			_fixedModeContainer.SetAnchor(rootAnchor.transform);
		}
		_fixedModeContainer.gameObject.SetActive(value: false);
		SetFixedMode(drawMode: false);
		OffServerHudLayout.Register("joystick", _fixedModeContainer.transform, "จอยเด\u0e34น / Joystick", "movement");
		OffServerHudLayout.RegisterVisibilityWidgets("joystick", _widgetsToFade);
	}

	private IEnumerator FadeOutJoystick()
	{
		IsVisible = false;
		yield return new WaitForSeconds(_fadeOutDelay);
		for (int i = 0; i < _widgetsToFade.Length; i++)
		{
			UIWidget obj = _widgetsToFade[i];
			Color color = obj.color;
			color.a = _fadeOutAlpha * OffServerHudLayout.AlphaFor("joystick");
			TweenColor.Begin(obj.gameObject, 0.5f, color).method = UITweener.Method.EaseOut;
		}
		yield return new WaitForSeconds(0.5f);
	}

	private void SetFixedMode(bool drawMode)
	{
		_fixedMode = drawMode;
		_fixedModeContainer.gameObject.SetActive(drawMode);
		if (_fixedMode)
		{
			StopAllCoroutines();
			Vector3 position = _fixedModeContainer.transform.position;
			Transform[] widgetsToCenter = _widgetsToCenter;
			for (int i = 0; i < widgetsToCenter.Length; i++)
			{
				widgetsToCenter[i].position = position;
			}
			for (int j = 0; j < _widgetsToFade.Length; j++)
			{
				UIWidget obj = _widgetsToFade[j];
				Color color = obj.color;
				color.a = OffServerHudLayout.AlphaFor("joystick");
				obj.color = color;
			}
		}
		else
		{
			ResetJoystick();
		}
	}

	public void Press(Vector3 currentPos)
	{
		if (!OffServerHudLayout.BlocksInput && !OffServerHudLayout.IsHidden("joystick") && (!OffServerHudLayout.HasPlacement("joystick") || GetFixedModeContainerRect().Contains(currentPos)) && !(_target == null) && !(UICamera.currentCamera == null))
		{
			Pressed = true;
			currentPos = UICamera.currentCamera.ScreenPointToRay(currentPos).GetPoint(0f);
			currentPos.z = 0f;
			_userInitTouchPos = currentPos;
			if (_fixedMode || OffServerHudLayout.HasPlacement("joystick"))
			{
				currentPos = _fixedModeContainer.transform.position;
			}
			Transform[] widgetsToCenter = _widgetsToCenter;
			for (int i = 0; i < widgetsToCenter.Length; i++)
			{
				widgetsToCenter[i].position = currentPos;
			}
		}
	}

	public void Release()
	{
		if (Pressed)
		{
			Pressed = false;
			ResetJoystick(!_fixedMode);
		}
	}

	private void LateUpdate()
	{
		bool flag = OffServerHudLayout.HasPlacement("joystick") || OffServerHudLayout.Editing;
		if (!flag && !_hudCustomized)
		{
			return;
		}
		bool flag2 = !flag && _hudCustomized;
		bool flag3 = _hudWasEditing && !OffServerHudLayout.Editing;
		_hudWasEditing = OffServerHudLayout.Editing;
		if (_hudOriginalScales == null)
		{
			_hudOriginalScales = new Vector3[_widgetsToCenter.Length];
			for (int i = 0; i < _widgetsToCenter.Length; i++)
			{
				_hudOriginalScales[i] = _widgetsToCenter[i].localScale;
			}
		}
		_hudCustomized = flag;
		_fixedModeContainerRect = null;
		_fixedModeContainer.gameObject.SetActive(flag || _fixedMode);
		for (int j = 0; j < _widgetsToCenter.Length; j++)
		{
			bool flag4 = false;
			for (int k = 0; k < _widgetsToCenter.Length; k++)
			{
				if (j != k && _widgetsToCenter[j].IsChildOf(_widgetsToCenter[k]))
				{
					flag4 = true;
				}
			}
			if (!flag4)
			{
				_widgetsToCenter[j].localScale = _hudOriginalScales[j] * (flag ? OffServerHudLayout.ScaleFor("joystick") : 1f);
			}
			if (flag && !Pressed)
			{
				_widgetsToCenter[j].position = _fixedModeContainer.transform.position;
			}
		}
		if (OffServerHudLayout.Editing || flag2 || flag3)
		{
			StopAllCoroutines();
			UIWidget[] widgetsToFade = _widgetsToFade;
			for (int l = 0; l < widgetsToFade.Length; l++)
			{
				widgetsToFade[l].alpha = OffServerHudLayout.AlphaFor("joystick");
			}
		}
	}

	public bool Drag(Vector3 currentPos)
	{
		if (OffServerHudLayout.BlocksInput || OffServerHudLayout.IsHidden("joystick"))
		{
			return false;
		}
		if (UICamera.currentCamera == null)
		{
			return false;
		}
		Ray ray = UICamera.currentCamera.ScreenPointToRay(currentPos);
		float distance = 0f;
		currentPos = ray.GetPoint(distance);
		currentPos.z = 0f;
		Vector3 vector = currentPos - _userInitTouchPos;
		if (_fixedMode || OffServerHudLayout.HasPlacement("joystick"))
		{
			if (!(Math.Abs(vector.x) > float.Epsilon) && !(Math.Abs(vector.y) > float.Epsilon))
			{
				return false;
			}
			_userInitTouchPos = _fixedModeContainer.transform.position;
			vector = currentPos - _userInitTouchPos;
		}
		if (Math.Abs(vector.x) > float.Epsilon || Math.Abs(vector.y) > float.Epsilon)
		{
			vector = _target.InverseTransformDirection(vector);
			vector.Scale(_scale);
			vector = _target.TransformDirection(vector);
			vector.z = 0f;
		}
		_target.position = _userInitTouchPos + vector;
		float magnitude = _target.localPosition.magnitude;
		if (magnitude < _deadZone)
		{
			Position = Vector2.zero;
			_target.localPosition = Position;
		}
		else
		{
			if (((_followBeginRadius > 0f && magnitude > _followBeginRadius) || _followBegin) && !_fixedMode && !OffServerHudLayout.HasPlacement("joystick"))
			{
				float num = Mathf.Clamp(Mathf.Max(0f, magnitude - _followBeginRadius) / 10f, 1f, 3f);
				Vector3 vector2 = vector;
				vector2.Normalize();
				_userInitTouchPos += Time.deltaTime * vector2 * _followSpeed * num;
				for (int i = 0; i < _widgetsToCenter.Length; i++)
				{
					_widgetsToCenter[i].position = _userInitTouchPos;
				}
				_followBegin = true;
			}
			_target.localPosition = Vector3.ClampMagnitude(_target.localPosition, _radius);
			Position = _target.localPosition;
			if (!IsVisible && Position.magnitude > _visibleMagnitude)
			{
				IsVisible = true;
				StopAllCoroutines();
				Color white = Color.white;
				for (int j = 0; j < _widgetsToFade.Length; j++)
				{
					TweenColor.Begin(_widgetsToFade[j].gameObject, 0.1f, white).method = UITweener.Method.EaseIn;
				}
			}
		}
		if (magnitude <= _followEndRadius && _followBegin)
		{
			_followBegin = false;
		}
		if (_normalize)
		{
			Position = Position / _radius * Mathf.InverseLerp(_radius, _deadZone, 1f);
		}
		return true;
	}

	private void ResetJoystick(bool fadeOut = true)
	{
		Position = Vector2.zero;
		_target.position = ((!_fixedMode) ? _userInitTouchPos : _fixedModeContainer.transform.position);
		if (fadeOut)
		{
			StartCoroutine(FadeOutJoystick());
		}
		else
		{
			IsVisible = false;
		}
	}
}

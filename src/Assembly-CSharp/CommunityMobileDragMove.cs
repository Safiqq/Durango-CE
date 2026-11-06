using System;
using System.Collections.Generic;
using UnityEngine;

public class CommunityMobileDragMove
{
	private readonly CommunityMobileMoveRules.DragState _state = new CommunityMobileMoveRules.DragState();

	private readonly InputCommandMessage _message = new InputCommandMessage();

	private float _dragThreshold;

	private float DragThreshold
	{
		get
		{
			if (_dragThreshold > 0f)
			{
				return _dragThreshold;
			}
			return _dragThreshold = Mathf.Max(32f, Screen.dpi * 0.1f);
		}
	}

	public void Reset()
	{
		_state.Reset();
	}

	public InputCommandMessage Process(List<InputTouch.TouchEvent> touchEvents, Camera mainCamera)
	{
		InputTouch.TouchEvent touchEvent = null;
		int num = 0;
		int count = touchEvents.Count;
		for (int i = 0; i < count; i++)
		{
			InputTouch.TouchEvent touchEvent2 = touchEvents[i];
			if (!(Math.Abs(touchEvent2.LastActivateTime - Time.timeSinceLevelLoad) > float.Epsilon) && !touchEvent2.IsNguiTouched && touchEvent2.Used != InputTouch.TouchEvent.UsedBy.Gesture && touchEvent2.Used != InputTouch.TouchEvent.UsedBy.Draw && touchEvent2.Used != InputTouch.TouchEvent.UsedBy.Joystick && touchEvent2.TapCount < 2)
			{
				if (touchEvent == null || touchEvent2.Used == InputTouch.TouchEvent.UsedBy.Move)
				{
					touchEvent = touchEvent2;
				}
				num++;
			}
		}
		bool flag = touchEvent != null;
		float movedSq = (flag ? (touchEvent.CurrentPos - touchEvent.BeginPos).sqrMagnitude : 0f);
		float dragThreshold = DragThreshold;
		CommunityMobileMoveRules.DragState.Step step = _state.Next(flag, flag ? touchEvent.TouchId : 0, flag && touchEvent.IsTouching, movedSq, dragThreshold * dragThreshold, num >= 2);
		bool flag2 = flag && CommunityMobileMoveRules.DragWalkEnabled("click", touchEvent.TouchId == -10);
		switch (step)
		{
		case CommunityMobileMoveRules.DragState.Step.Drag:
			touchEvent.Used = InputTouch.TouchEvent.UsedBy.Move;
			if (!flag2)
			{
				return null;
			}
			return PickMessage(InputCommand.MoveToPositionByDragging, touchEvent, mainCamera);
		case CommunityMobileMoveRules.DragState.Step.Release:
			if (!flag2)
			{
				return null;
			}
			return PickMessage(InputCommand.MoveToPosition, touchEvent, mainCamera);
		default:
			return null;
		}
	}

	private InputCommandMessage PickMessage(InputCommand command, InputTouch.TouchEvent touchEvent, Camera mainCamera)
	{
		if (mainCamera == null || !mainCamera.pixelRect.Contains(touchEvent.CurrentPos))
		{
			return null;
		}
		_message.Init(command);
		_message.PickingRay = mainCamera.ScreenPointToRay(touchEvent.CurrentPos);
		_message.PickingTouchEvent = touchEvent;
		_message.MouseButton = KeyCode.None;
		return _message;
	}
}

using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Combat;
using Durango.Logic.InputSystem;
using Durango.Logic.Item;
using Durango.Logic.Skill;
using Durango.Network;
using Durango.System;
using Durango.System.Config;
using Durango.Terrain;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using L10N;
using Messages;
using Shared.Pet;
using UnityEngine;
using Yaml;
using Yaml.Util;

namespace Durango.UI;

public class BattleActionButtons : MonoBehaviour, IScreenResizeReceiver
{
	private enum BattleLeaveState
	{
		Deactive,
		Prepare,
		InBattle
	}

	private const string AutoBattleKey = "AutoBattle";

	private const string AutoBattleClikedKey = "AutoBattleClicked";

	public Action<string> ActionUsed;

	public Action<string> PetActionUsed;

	public Action TamingActionUsed;

	[SerializeField]
	private BattleActionButtonContainer _actionButtons;

	[SerializeField]
	private GameObject _toggleButton;

	[SerializeField]
	private GameObject _normalToggle;

	[SerializeField]
	private GameObject _alertToggle;

	[SerializeField]
	private GameObject _disabledToggle;

	[SerializeField]
	private GameObject _leaveButton;

	[SerializeField]
	[WidgetStates.Type(typeof(BattleLeaveState))]
	private WidgetStates _leaveButtonstateSet;

	[SerializeField]
	private SelectableWidget _battleModeLockButton;

	[SerializeField]
	private SelectableWidget _autoButton;

	private float? _petActionDirtyAt = 0f;

	private float? _tamingDirtyAt = 0f;

	private float? _leaveButtonDirtyAt = 0f;

	private readonly List<BattleActionButton> _pressedPlayerActions = new List<BattleActionButton>();

	private Pair<BattleActionButton, float> _lastPressedAction;

	private bool _isShow;

	private CombatGroup _parent;

	private int _targetGaugeChangedFrame;

	private readonly Observable<bool> _autoBattle = new Observable<bool>();

	private readonly Observable<BattleLeaveState> _battleLeaveState = new Observable<BattleLeaveState>();

	private DamageableEntity _target;

	private int _pcHud;

	private bool _togglePcAnchorsCleared;

	private UIWidget _togglePcWidget;

	private Transform _pcContextBackgrounds;

	private float _pcContextLookupAt;

	private Transform[] _pcLeaveLabels;

	private TweenPosition[] _pcLeaveTweens;

	private float _pcLeaveLookupAt;

	private const float PcLeaveRightEdge = -300f;

	private const float PcLeaveGap = 10f;

	public event Action BattleLeaveClicked;

	void IScreenResizeReceiver.OnChangeScreenSize()
	{
		int absolute = 0;
		int absolute2 = 0;
		if (UIManager.IsPortraitWidget(base.gameObject))
		{
			absolute = 60;
			absolute2 = 40;
		}
		else if (GameManager.IsPrologueMode)
		{
			absolute = 100;
			absolute2 = 60;
		}
		GetComponent<UIWidget>().bottomAnchor.absolute = absolute;
		_actionButtons.GetComponent<UIWidget>().bottomAnchor.absolute = absolute2;
		UIUtility.UpdateAnchors(base.transform);
		FixTogglePositionForPC();
		FixLeavePositionForPC();
	}

	private bool IsPcHoneycombHud()
	{
		if (_pcHud == 0)
		{
			_pcHud = ((Platform.Instance.UsePCUI && base.transform.name.IndexOf("_Mobile", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : (-1));
		}
		return _pcHud > 0;
	}

	private void FixTogglePositionForPC()
	{
		if (_toggleButton == null || !IsPcHoneycombHud() || !TryGetPcToggleWorld(out var world))
		{
			return;
		}
		if (!_togglePcAnchorsCleared)
		{
			_togglePcAnchorsCleared = true;
			UIWidget component = _toggleButton.GetComponent<UIWidget>();
			if (component != null)
			{
				component.updateAnchors = UIRect.AnchorUpdate.OnStart;
				component.leftAnchor.target = null;
				component.rightAnchor.target = null;
				component.topAnchor.target = null;
				component.bottomAnchor.target = null;
			}
			_togglePcWidget = component;
		}
		Transform transform = _toggleButton.transform;
		Vector3 vector = transform.parent.InverseTransformPoint(world);
		vector.z = 0f;
		if ((transform.localPosition - vector).sqrMagnitude > 0.01f)
		{
			transform.localPosition = vector;
		}
	}

	private bool TryGetPcToggleWorld(out Vector3 world)
	{
		world = Vector3.zero;
		if (_pcContextBackgrounds == null)
		{
			if (Time.unscaledTime < _pcContextLookupAt)
			{
				return false;
			}
			_pcContextLookupAt = Time.unscaledTime + 1f;
			ContextActionGroupBase contextActionGroupBase = UIManager.FindScript<ContextActionGroupBase>();
			_pcContextBackgrounds = ((contextActionGroupBase != null) ? contextActionGroupBase.transform.Find("Actions/Backgrounds") : null);
			if (_pcContextBackgrounds == null)
			{
				return false;
			}
		}
		int childCount = _pcContextBackgrounds.childCount;
		float num = float.MaxValue;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = _pcContextBackgrounds.GetChild(i);
			if (child.gameObject.activeSelf && child.localPosition.y < num)
			{
				num = child.localPosition.y;
			}
		}
		int num2 = 0;
		float num3 = float.MaxValue;
		float num4 = float.MinValue;
		Transform transform = null;
		for (int j = 0; j < childCount; j++)
		{
			Transform child2 = _pcContextBackgrounds.GetChild(j);
			if (child2.gameObject.activeSelf && !(Mathf.Abs(child2.localPosition.y - num) > 1f))
			{
				num2++;
				if (child2.localPosition.x < num3)
				{
					num3 = child2.localPosition.x;
					transform = child2;
				}
				num4 = Mathf.Max(num4, child2.localPosition.x);
			}
		}
		if (num2 == 0)
		{
			return false;
		}
		float num5 = (num4 - num3) / (float)Mathf.Max(1, num2 - 1);
		if (num2 == 1)
		{
			UIWidget component = transform.GetComponent<UIWidget>();
			num5 = ((component != null) ? ((float)component.width * transform.localScale.x) : 0f);
		}
		if (num5 <= 0f)
		{
			return false;
		}
		world = _pcContextBackgrounds.TransformPoint(new Vector3(num3 - num5, num, 0f));
		return true;
	}

	private void FixLeavePositionForPC()
	{
		if (!IsPcHoneycombHud())
		{
			return;
		}
		if (_pcLeaveLabels == null)
		{
			if (Time.unscaledTime < _pcLeaveLookupAt)
			{
				return;
			}
			_pcLeaveLookupAt = Time.unscaledTime + 1f;
			SideEffectGroup sideEffectGroup = UIManager.FindScript<SideEffectGroup>();
			if (sideEffectGroup == null)
			{
				return;
			}
			_pcLeaveLabels = new Transform[2];
			_pcLeaveTweens = new TweenPosition[2];
			Transform transform = sideEffectGroup.transform.Find("CombatEffect/CombatModeButton");
			if (transform != null)
			{
				string[] array = new string[2] { "LeaveButton", "LeavePreview" };
				for (int i = 0; i < array.Length; i++)
				{
					_pcLeaveLabels[i] = transform.Find(array[i]);
					_pcLeaveTweens[i] = ((_pcLeaveLabels[i] != null) ? _pcLeaveLabels[i].GetComponent<TweenPosition>() : null);
				}
			}
		}
		for (int j = 0; j < _pcLeaveLabels.Length; j++)
		{
			Transform transform2 = _pcLeaveLabels[j];
			if (transform2 == null)
			{
				continue;
			}
			float num = -300f;
			if (_togglePcWidget != null)
			{
				num = transform2.parent.InverseTransformPoint(_togglePcWidget.worldCorners[0]).x - 10f;
			}
			TweenPosition tweenPosition = _pcLeaveTweens[j];
			float num2 = num - ((tweenPosition != null) ? tweenPosition.to.x : transform2.localPosition.x);
			if (!(Mathf.Abs(num2) < 0.1f))
			{
				if (tweenPosition != null)
				{
					tweenPosition.from.x += num2;
					tweenPosition.to.x += num2;
				}
				transform2.localPosition += new Vector3(num2, 0f, 0f);
			}
		}
	}

	private void Start()
	{
		_parent = UIUtility.FindComponentInParent<CombatGroup>(base.gameObject);
		if (_parent == null)
		{
			base.gameObject.SetActive(value: false);
			return;
		}
		FixTogglePositionForPC();
		FixLeavePositionForPC();
		Observable<bool> battleModeLock = _parent.BattleModeLock;
		battleModeLock.Changed = (Action<bool>)Delegate.Combine(battleModeLock.Changed, (Action<bool>)delegate(bool value)
		{
			_battleModeLockButton.Selected = value;
		});
		SelectableWidget battleModeLockButton = _battleModeLockButton;
		battleModeLockButton.Clicked = (Action)Delegate.Combine(battleModeLockButton.Clicked, (Action)delegate
		{
			bool flag = !_parent.BattleModeLock;
			_parent.BattleModeLock.Value = flag;
			ShowBattleModeLockTooltip(flag);
		});
		Observable<bool> autoBattle = _autoBattle;
		autoBattle.Changed = (Action<bool>)Delegate.Combine(autoBattle.Changed, (Action<bool>)delegate(bool value)
		{
			_autoButton.Selected = value;
		});
		_autoBattle.Value = Preferences.GetBool("AutoBattle", defaultValue: true);
		SelectableWidget autoButton = _autoButton;
		autoButton.Clicked = (Action)Delegate.Combine(autoButton.Clicked, (Action)delegate
		{
			_autoBattle.Value = !_autoBattle;
			Preferences.SetBool("AutoBattle", _autoBattle);
			Preferences.SetBool("AutoBattleClicked", value: true);
			if ((bool)_autoBattle)
			{
				ShowAutoBattleTooltip();
			}
		});
		GameSystem<PvpIslandSystem>.Instance().GameStarted += delegate
		{
			SetToggleButton(null);
			_battleModeLockButton.gameObject.SetActive(value: false);
			_leaveButton.gameObject.SetActive(value: false);
		};
		_actionButtons.PlayerActionPressed += OnPlayerActionPress;
		if (CombatLoadout.Enabled && CommunityHotkeys.Supported)
		{
			for (int num = 0; num < 6; num++)
			{
				GameSystem<InputSystem>.Instance().On((InputCommand)(70 + num), OnLoadoutKey, InputSystem.Priority.Higher);
			}
		}
		GameSystem<InputSystem>.Instance().On(InputCommand.PlayerDodge, OnDodgeKey);
		_actionButtons.PetActionPressed += OnPetActionPress;
		_actionButtons.TameActionPressed += OnTameActionPress;
		GameSystem<CombatSystem>.Instance().ChangedCombatMode += delegate
		{
			_petActionDirtyAt = 0f;
			RefreshLeaveButton();
		};
		Observable<DamageableEntity> target = GameSystem<CombatSystem>.Instance().Target;
		target.Changed = (Action<DamageableEntity>)Delegate.Combine(target.Changed, (Action<DamageableEntity>)delegate(DamageableEntity target2)
		{
			_target = target2;
			_tamingDirtyAt = 0f;
		});
		Observable<double> tamingBeginAt = GameSystem<CombatSystem>.Instance().UsingAction.TamingBeginAt;
		tamingBeginAt.Changed = (Action<double>)Delegate.Combine(tamingBeginAt.Changed, (Action<double>)delegate
		{
			_tamingDirtyAt = 0f;
		});
		Observable<float> battleLeaveAvailableAt = GameSystem<CombatSystem>.Instance().BattleLeaveAvailableAt;
		battleLeaveAvailableAt.Changed = (Action<float>)Delegate.Combine(battleLeaveAvailableAt.Changed, (Action<float>)delegate
		{
			_leaveButtonDirtyAt = 0f;
		});
		GameSystem<InventorySystem>.Instance().PlayerInventoryUpdated += delegate
		{
			_tamingDirtyAt = 0f;
		};
		Durango.Utils.Singleton<PetManager>.Instance().PlayerPetSkillStates.StateChanged += delegate
		{
			_petActionDirtyAt = 0f;
		};
		Durango.Utils.Singleton<PetManager>.Instance().PlayerPetSkillStates.PetChanged += delegate
		{
			_petActionDirtyAt = 0f;
		};
		Durango.Utils.Singleton<TerrainBase>.Instance().OnReady(delegate
		{
			OnChangeViewMode(_parent.BattleView);
		});
		_parent.BattleViewChanged += OnChangeViewMode;
		if (GameManager.IsPrologueMode)
		{
			_autoBattle.Value = false;
			_toggleButton.gameObject.SetActive(value: false);
			_autoButton.gameObject.SetActive(value: false);
			_leaveButton.gameObject.SetActive(value: false);
			_battleModeLockButton.gameObject.SetActive(value: false);
			return;
		}
		UIEventListener uIEventListener = UIEventListener.Get(_disabledToggle);
		uIEventListener.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(uIEventListener.onClick, (UIEventListener.VoidDelegate)delegate
		{
			WidgetTooltipControl widgetTooltipControl = UIManager.Popup.Tooltip<WidgetTooltipControl>();
			widgetTooltipControl.Set(null, T._("안개 상태에서는 <alert>전투모드로 진입할 수 없습니다.</alert>"), 350);
			widgetTooltipControl.AutoPosition = true;
			widgetTooltipControl.Show(4f);
		});
		Action toggleButtonFunction = delegate
		{
			if (!GameSystem<CombatSystem>.Instance().CombatMode && PlayerBehavior.LocalPlayer.IsAlive)
			{
				_parent.SetBattleView(CombatGroup.BattleViewMode.Battle);
			}
		};
		UIEventListener uIEventListener2 = UIEventListener.Get(_normalToggle);
		uIEventListener2.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(uIEventListener2.onClick, (UIEventListener.VoidDelegate)delegate
		{
			toggleButtonFunction();
		});
		UIEventListener uIEventListener3 = UIEventListener.Get(_alertToggle);
		uIEventListener3.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(uIEventListener3.onClick, (UIEventListener.VoidDelegate)delegate
		{
			toggleButtonFunction();
		});
		UIEventListener uIEventListener4 = UIEventListener.Get(_leaveButton);
		uIEventListener4.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(uIEventListener4.onClick, (UIEventListener.VoidDelegate)delegate
		{
			UISound.PlayClick(UISound.ClickType.ActionButtonDefault);
			if ((BattleLeaveState)_battleLeaveState == BattleLeaveState.Deactive)
			{
				UIManager.SystemMsg("BattleAction", T._("일정 시간 피해를 받지 않으면 전투에서 벗어날 수 있습니다."));
			}
			else if (this.BattleLeaveClicked != null)
			{
				this.BattleLeaveClicked();
			}
		});
		Observable<BattleLeaveState> battleLeaveState = _battleLeaveState;
		battleLeaveState.Changed = (Action<BattleLeaveState>)Delegate.Combine(battleLeaveState.Changed, (Action<BattleLeaveState>)delegate(BattleLeaveState state)
		{
			_leaveButtonstateSet.Apply((int)state);
		});
	}

	private void OnDisable()
	{
		_pressedPlayerActions.Clear();
	}

	private void Show()
	{
		_isShow = true;
		FixLeavePositionForPC();
		_pressedPlayerActions.Clear();
		_actionButtons.gameObject.SetActive(value: true);
		_toggleButton.SetActive(value: false);
		RefreshDirtyItems();
		if ((bool)_autoBattle && !Preferences.GetBool("AutoBattleClicked"))
		{
			ShowAutoBattleTooltip();
		}
	}

	private void Hide()
	{
		_isShow = false;
		_actionButtons.gameObject.SetActive(value: false);
		_toggleButton.SetActive(value: true);
		UIManager.Popup.FindTooltip<ActionTooltipBase>().Hide();
	}

	private void ShowAutoBattleTooltip()
	{
		if (_autoButton.gameObject.activeSelf)
		{
			WidgetTooltipControl widgetTooltipControl = UIManager.Popup.Tooltip<WidgetTooltipControl>();
			widgetTooltipControl.Direction = TooltipBase.TooltipDirection.Vertical;
			string text = T._("전투시 기본 공격을 자동으로 사용");
			widgetTooltipControl.Set(null, text, 300);
			widgetTooltipControl.Show(5f);
			widgetTooltipControl.SetPosition(_autoButton.Widget, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(30f, -40f));
		}
	}

	private void ShowBattleModeLockTooltip(bool on)
	{
		if (_battleModeLockButton.gameObject.activeSelf)
		{
			WidgetTooltipControl widgetTooltipControl = UIManager.Popup.Tooltip<WidgetTooltipControl>();
			widgetTooltipControl.Direction = TooltipBase.TooltipDirection.Vertical;
			string text = ((!on) ? T._("전투 종료시 전투모드를 자동으로 해제합니다") : T._("전투 종료시 전투모드를 자동으로 해제하지 않습니다"));
			widgetTooltipControl.Set(null, text, 300);
			widgetTooltipControl.Show(5f);
			widgetTooltipControl.SetPosition(_battleModeLockButton.Widget, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), new Vector2(30f, -40f));
		}
	}

	public Transform GetButton(int index)
	{
		BattleActionButton actionButton = _actionButtons.GetActionButton(index);
		if (actionButton == null)
		{
			return null;
		}
		return actionButton.transform;
	}

	private void OnChangeViewMode(CombatGroup.BattleViewMode viewMode)
	{
		if (viewMode == CombatGroup.BattleViewMode.Battle)
		{
			Show();
		}
		else
		{
			Hide();
		}
		RefreshLeaveButton();
	}

	private void Update()
	{
		FixTogglePositionForPC();
		FixLeavePositionForPC();
		RefreshDirtyItems();
	}

	private void RefreshDirtyItems()
	{
		if (_isShow)
		{
			if (_target != null && _targetGaugeChangedFrame != _target.GaugeChanged)
			{
				_targetGaugeChangedFrame = _target.GaugeChanged;
				_tamingDirtyAt = 0f;
			}
			float time = Time.time;
			RefreshPlayerActions();
			ProcessReservedPlayerAction();
			if (_leaveButtonDirtyAt.HasValue && _leaveButtonDirtyAt.Value < time)
			{
				RefreshLeaveButton();
			}
			if (_petActionDirtyAt.HasValue && _petActionDirtyAt.Value < time)
			{
				RefreshPetAction();
			}
			if (_tamingDirtyAt.HasValue && _tamingDirtyAt.Value < time)
			{
				RefershTamingState();
			}
		}
	}

	private bool UsePlayerPressedAction()
	{
		for (int num = _pressedPlayerActions.Count - 1; num >= 0; num--)
		{
			BattleActionButton battleActionButton = _pressedPlayerActions[num];
			if (battleActionButton.IsClickable && DoPlayerAction(battleActionButton.Id))
			{
				return true;
			}
		}
		if (Time.time < _lastPressedAction.Item2 + 0.5f)
		{
			BattleActionButton item = _lastPressedAction.Item1;
			if (item != null && item.IsClickable)
			{
				return DoPlayerAction(item.Id);
			}
		}
		return false;
	}

	private void ProcessReservedPlayerAction()
	{
		if (CommunityHudLayout.BlocksInput)
		{
			_pressedPlayerActions.Clear();
			_lastPressedAction = default(Pair<BattleActionButton, float>);
			return;
		}
		UsingAction usingAction = GameSystem<CombatSystem>.Instance().UsingAction;
		if (usingAction.GetState() != UsingAction.State.None || !usingAction.TamingTimer.Timer.IsStop || UsePlayerPressedAction() || !_autoBattle || PlayerBehavior.LocalPlayer.IsRiding || PlayerController.MoveOperator.MovedByManual || !GameSystem<CombatSystem>.Instance().CombatMode || _target == null)
		{
			return;
		}
		BattleAction battleAction = null;
		BattleAction[] actionSlots = GameSystem<CombatSystem>.Instance().ActionSlots;
		if (actionSlots != null && actionSlots.Length > 1)
		{
			battleAction = actionSlots[1];
		}
		if (battleAction != null && !(Connections.Frontend.GetPredictedServerTime() < battleAction.ProhibitedUntil + 0.3))
		{
			BattleActionButton actionButton = _actionButtons.GetActionButton(battleAction.Data.Id);
			if (!(actionButton == null) && actionButton.IsClickable && DoPlayerAction(battleAction))
			{
				actionButton.ShowClickEffect();
			}
		}
	}

	private void RefreshLeaveButton()
	{
		_leaveButtonDirtyAt = null;
		BattleLeaveState value = BattleLeaveState.Deactive;
		if (_isShow)
		{
			float num = GameSystem<CombatSystem>.Instance().BattleLeaveAvailableAt;
			if (num < Time.time)
			{
				value = ((!GameSystem<CombatSystem>.Instance().CombatMode) ? BattleLeaveState.Prepare : BattleLeaveState.InBattle);
			}
			else
			{
				_leaveButtonDirtyAt = num;
			}
		}
		_battleLeaveState.Value = value;
	}

	public void SetToggleButton(bool? isAlert)
	{
		if (GameManager.Region.IsPvpIsland())
		{
			isAlert = null;
		}
		_disabledToggle.SetActive(!isAlert.HasValue);
		_normalToggle.SetActive(isAlert.HasValue && !isAlert.Value);
		_alertToggle.SetActive(isAlert.HasValue && isAlert.Value);
	}

	private bool DoPlayerAction(string id)
	{
		BattleAction battleAction = GameSystem<CombatSystem>.Instance().GetBattleAction(id);
		return DoPlayerAction(battleAction);
	}

	private bool DoPlayerAction(BattleAction action)
	{
		if (CommunityHudLayout.BlocksInput)
		{
			return false;
		}
		if (GameSystem<CombatSystem>.Instance().IsUsableAction(action))
		{
			if (ActionUsed != null)
			{
				ActionUsed(action.Data.Id);
			}
			return true;
		}
		return false;
	}

	private string GetActionFailReason(PlayerAction action)
	{
		if (action == null)
		{
			return null;
		}
		Node parentSkill = action.GetParentSkill();
		if (parentSkill != null && parentSkill.Parent.Level < parentSkill.Level)
		{
			return T._("{0:을} 배우면 사용 가능합니다.", parentSkill.Name);
		}
		return null;
	}

	private string GetActionFailReason(BattleAction action)
	{
		if (action == null)
		{
			return null;
		}
		if (action.GetDeactivateUntil().HasValue)
		{
			return null;
		}
		if (action.Stamina > 0f)
		{
			Gauge stamina = PlayerBehavior.LocalPlayer.Stamina;
			if (stamina != null && stamina.Get() < action.Stamina)
			{
				return T._("스테미너가 부족합니다.");
			}
		}
		if (Durango.Terrain.Util.IsWater(PlayerBehavior.LocalPlayer.GetBiome()))
		{
			return T._("물속에서는 전투를 할 수 없습니다.");
		}
		return null;
	}

	private void OnDodgeKey(InputCommandMessage message)
	{
		if (this == null || !base.isActiveAndEnabled)
		{
			return;
		}
		BattleAction[] actionSlots = GameSystem<CombatSystem>.Instance().ActionSlots;
		if (actionSlots == null)
		{
			return;
		}
		foreach (BattleAction battleAction in actionSlots)
		{
			if (battleAction == null || battleAction.Data == null || battleAction.Data.Id == null || !battleAction.Data.Id.EndsWith("_dodge", StringComparison.Ordinal))
			{
				continue;
			}
			if (!DoPlayerAction(battleAction))
			{
				string actionFailReason = GetActionFailReason(battleAction);
				if (!string.IsNullOrEmpty(actionFailReason))
				{
					UIManager.SystemMsg("BattleAction", actionFailReason);
				}
			}
			break;
		}
	}

	private void OnLoadoutKey(InputCommandMessage message)
	{
		if (_isShow && base.isActiveAndEnabled && !UIBase.HasOpenedUI && !UICamera.inputHasFocus && !CommunityHudLayout.BlocksInput && !CommunityRanked.InMatch && CombatLoadout.Current() != null && !UIManager.MessageBox.IsOpened && _actionButtons.gameObject.activeInHierarchy)
		{
			BattleActionButton actionButton = _actionButtons.GetActionButton((int)(message.Command - 70));
			GameSystem<InputSystem>.Instance().StopPropagation();
			if (!(actionButton == null) && !string.IsNullOrEmpty(actionButton.Id) && (message.CurrentTrigger == Trigger.Down || message.CurrentTrigger == Trigger.Up))
			{
				bool flag = message.CurrentTrigger == Trigger.Down;
				actionButton.ShowPressEffect(flag);
				OnPlayerActionPress(actionButton, flag);
			}
		}
	}

	private void OnDestroy()
	{
		if (!GameSystem<InputSystem>.HasInstance())
		{
			return;
		}
		if (CombatLoadout.Enabled && CommunityHotkeys.Supported)
		{
			for (int i = 0; i < 6; i++)
			{
				GameSystem<InputSystem>.Instance().Off((InputCommand)(70 + i), OnLoadoutKey, InputSystem.Priority.Higher);
			}
		}
		GameSystem<InputSystem>.Instance().Off(InputCommand.PlayerDodge, OnDodgeKey);
	}

	private void OnPlayerActionPress(BattleActionButton btn, bool press)
	{
		if (CombatLoadout.Enabled && CombatLoadout.HandleItem(btn, press))
		{
			return;
		}
		string id = btn.Id;
		for (int num = _pressedPlayerActions.Count - 1; num >= 0; num--)
		{
			if (_pressedPlayerActions[num].Id == id)
			{
				_pressedPlayerActions.RemoveAt(num);
			}
		}
		if (press)
		{
			BattleAction battleAction = GameSystem<CombatSystem>.Instance().GetBattleAction(id);
			if (!DoPlayerAction(battleAction))
			{
				string text = ((battleAction != null) ? GetActionFailReason(battleAction) : GetActionFailReason(SingletonDict<string, PlayerAction>.Get(id)));
				if (!string.IsNullOrEmpty(text))
				{
					UIManager.SystemMsg("BattleAction", text);
				}
			}
			_lastPressedAction = new Pair<BattleActionButton, float>(btn, Time.time);
			_pressedPlayerActions.Add(btn);
			ActionTooltipBase actionTooltipBase = null;
			if (battleAction == null)
			{
				PlayerAction playerAction = SingletonDict<string, PlayerAction>.Get(id);
				if (playerAction != null)
				{
					actionTooltipBase = UIManager.Popup.Tooltip<ActionTooltipBase>();
					actionTooltipBase.Set(playerAction);
				}
			}
			else
			{
				actionTooltipBase = UIManager.Popup.Tooltip<ActionTooltipBase>();
				actionTooltipBase.Set(battleAction);
			}
			if (!(actionTooltipBase == null))
			{
				ShowTooltip(btn, actionTooltipBase);
			}
		}
		else
		{
			UIManager.Popup.FindTooltip<ActionTooltipBase>().Hide();
		}
	}

	private void OnPetActionPress(BattleActionButton btn, bool press)
	{
		if (press)
		{
			string id = btn.Id;
			if (btn.IsClickable && PetActionUsed != null)
			{
				PetActionUsed(id);
			}
			Yaml.PetActiveSkill playerPetSkill = PetUtil.GetPlayerPetSkill(id);
			ActionTooltipBase actionTooltipBase = UIManager.Popup.Tooltip<ActionTooltipBase>();
			actionTooltipBase.Set(playerPetSkill);
			ShowTooltip(btn, actionTooltipBase);
		}
		else
		{
			UIManager.Popup.FindTooltip<ActionTooltipBase>().Hide();
		}
	}

	private string GetTamingFailReason(DamageableEntity target)
	{
		if (target == null || !target.IsAlive)
		{
			return null;
		}
		int entityTypeId = target.GetEntityTypeId();
		if (TameableHelper.Instance().IsTameableType(entityTypeId))
		{
			bool flag = false;
			foreach (ItemData playerItem in GameSystem<InventorySystem>.Instance().PlayerItemList)
			{
				if (playerItem.IsCapturable())
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				float num = _target.GetLife().Ratio();
				float tamableHpRate = Yaml.Util.Singleton<Constants>.Instance.Taming.TamableHpRate;
				if (num > tamableHpRate)
				{
					return T._("동물의 체력이 낮아야 포획 가능합니다.");
				}
				double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
				double num2 = (double)GameSystem<CombatSystem>.Instance().UsingAction.TamingBeginAt + (double)Yaml.Util.Singleton<Constants>.Instance.Taming.TamingCooltime;
				if (predictedServerTime < num2)
				{
					return T._("포획할 준비가 되지 않았습니다.");
				}
				return null;
			}
			return T._("포획용 아이템이 없습니다.");
		}
		if (target.GameObject.GetComponent<AnimalBehavior>().Role == "warp_guard")
		{
			return null;
		}
		Node node = TameableHelper.Instance().RequiredForTaming(entityTypeId);
		if (node == null)
		{
			return T._("포획이 불가능한 동물입니다.");
		}
		return T._("<em>{0}</em> 스킬이 필요합니다.", node.Name);
	}

	private void OnTameActionPress(BattleActionButton btn, bool press)
	{
		if (press)
		{
			if (btn.IsClickable)
			{
				if (TamingActionUsed != null)
				{
					TamingActionUsed();
				}
			}
			else
			{
				string tamingFailReason = GetTamingFailReason(_target);
				if (!string.IsNullOrEmpty(tamingFailReason))
				{
					UIManager.SystemMsg("BattleAction", tamingFailReason);
				}
			}
			ActionTooltipBase actionTooltipBase = UIManager.Popup.Tooltip<ActionTooltipBase>();
			actionTooltipBase.SetTamingHelp();
			ShowTooltip(btn, actionTooltipBase);
		}
		else
		{
			UIManager.Popup.FindTooltip<ActionTooltipBase>().Hide();
		}
	}

	private void ShowTooltip(BattleActionButton button, ActionTooltipBase tooltip)
	{
		Vector2 offset;
		if (UIManager.IsPortraitScreen)
		{
			tooltip.Direction = TooltipBase.TooltipDirection.Vertical;
			offset = new Vector2(0f, 10f);
		}
		else
		{
			tooltip.Direction = TooltipBase.TooltipDirection.Horizontal;
			offset = new Vector2(10f, 0f);
		}
		tooltip.Show(button.gameObject, offset);
	}

	private void RefreshPlayerActions()
	{
		BattleAction[] actionSlots = GameSystem<CombatSystem>.Instance().ActionSlots;
		int playerActionSlotCount = _actionButtons.PlayerActionSlotCount;
		string[] array = ((CombatLoadout.Enabled && !CommunityRanked.InMatch) ? CombatLoadout.Current() : null);
		for (int i = 0; i < playerActionSlotCount; i++)
		{
			if (array != null && i < array.Length)
			{
				BattleActionButton actionButton = _actionButtons.GetActionButton(i);
				string binding = array[i];
				if (CombatLoadout.IsItem(binding))
				{
					actionButton.ShowLoadoutItem(binding, CombatLoadout.FindCached(binding));
					continue;
				}
				actionButton.HideLoadoutItem();
				int num = CombatLoadout.ActionSlot(binding);
				BattleAction action = ((num >= 0 && actionSlots != null && num < actionSlots.Length) ? actionSlots[num] : null);
				_actionButtons.SetAction(i, action);
				continue;
			}
			if (CombatLoadout.Enabled)
			{
				_actionButtons.GetActionButton(i).HideLoadoutItem();
			}
			BattleAction battleAction = null;
			if (actionSlots != null && i < actionSlots.Length)
			{
				battleAction = actionSlots[i];
			}
			if (battleAction == null)
			{
				_actionButtons.SetPlaceholderAction(i, GameSystem<CombatSystem>.Instance().GetPlaceholderAction(i));
			}
			else
			{
				_actionButtons.SetAction(i, battleAction);
			}
		}
	}

	private void RefreshPetAction()
	{
		_petActionDirtyAt = null;
		Messages.PetActiveSkill? petBattleActionSkills = GetPetBattleActionSkills();
		_actionButtons.SetPetAction(petBattleActionSkills);
		if (!petBattleActionSkills.HasValue)
		{
			return;
		}
		PetSkillStates.State state = Durango.Utils.Singleton<PetManager>.Instance().PlayerPetSkillStates.GetState(petBattleActionSkills.Value.SkillId);
		if (state != null)
		{
			double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
			if (state.ReactivatedAt.HasValue && state.ReactivatedAt.Value > predictedServerTime)
			{
				_petActionDirtyAt = Times.UnixTimeToUnityTime(state.ReactivatedAt.Value);
			}
		}
	}

	private Messages.PetActiveSkill? GetPetBattleActionSkills()
	{
		if (!GameSystem<CombatSystem>.Instance().CombatMode)
		{
			return null;
		}
		Messages.Pet? pet = Durango.Utils.Singleton<PetManager>.Instance().GetPet(Durango.Utils.Singleton<PetManager>.Instance().GetPlayerPetId());
		if (!pet.HasValue)
		{
			return null;
		}
		Messages.Pet value = pet.Value;
		if (KUtility.GetSize(value.Statistics.AvailableActiveSkill) == 0)
		{
			return null;
		}
		Messages.PetActiveSkill[] availableActiveSkill = value.Statistics.AvailableActiveSkill;
		for (int i = 0; i < availableActiveSkill.Length; i++)
		{
			Messages.PetActiveSkill value2 = availableActiveSkill[i];
			Yaml.PetActiveSkill petActiveSkill = PetActiveSkills.Get(value2.SkillId, value2.Rank);
			if (petActiveSkill != null && (petActiveSkill.Context & SkillContext.Battle) != SkillContext.None)
			{
				return value2;
			}
		}
		return null;
	}

	private void RefershTamingState()
	{
		_tamingDirtyAt = null;
		_tamingDirtyAt = _actionButtons.SetTameAction(_target);
	}
}

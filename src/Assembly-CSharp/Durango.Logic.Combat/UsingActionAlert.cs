using System.Collections.Generic;
using Durango.Network;
using Durango.Player.Animation;
using Durango.Terrain;
using Durango.UI.InGame;
using Durango.Utils;
using Messages;
using UnityEngine;
using Yaml;

namespace Durango.Logic.Combat;

public class UsingActionAlert
{
	private struct Item
	{
		public int? Id;

		public PlayerActionAttackInfo Info;

		public float InitialYaw;
	}

	private BattleAction _action;

	private readonly List<Item> _items = new List<Item>();

	private PlayerAnimationClipInfo _cachedClip;

	private AnimationState _cachedState;

	private int? _ghostIndex;

	public void Set(BattleAction action)
	{
		Clear();
		if (!CombatSystem.PlayerAttackRangeEnabled)
		{
			return;
		}
		_action = action;
		if (_action == null || _action.Data.AttackInfo == null)
		{
			return;
		}
		PlayerActionAttackInfo[] attackInfo = _action.Data.AttackInfo;
		foreach (PlayerActionAttackInfo playerActionAttackInfo in attackInfo)
		{
			_items.Add(new Item
			{
				Info = (playerActionAttackInfo.OffServerPreview ?? playerActionAttackInfo)
			});
		}
		if (Singleton<PlayerManager>.Instance().GetPlayer(GameManager.PlayerId) != null)
		{
			_ghostIndex = _items.Count;
			attackInfo = _action.Data.AttackInfo;
			foreach (PlayerActionAttackInfo playerActionAttackInfo2 in attackInfo)
			{
				_items.Add(new Item
				{
					Info = (playerActionAttackInfo2.OffServerPreview ?? playerActionAttackInfo2)
				});
			}
		}
	}

	private void Clear()
	{
		_action = null;
		_cachedClip = null;
		_cachedState = null;
		_ghostIndex = null;
		foreach (Item item in _items)
		{
			int? id = item.Id;
			if (id.HasValue)
			{
				AreaOfEffectVisualizer.Stop(item.Id.Value);
			}
		}
		_items.Clear();
	}

	public void Update()
	{
		if (_action == null)
		{
			return;
		}
		if (!CombatSystem.PlayerAttackRangeEnabled)
		{
			Clear();
			return;
		}
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (localPlayer == null || localPlayer.CurrentPlayerClipInfo == null)
		{
			Clear();
			return;
		}
		if (_cachedClip == null)
		{
			if (!(localPlayer.CurrentPlayerClipInfo.Clip == _action.Data.Meta.Motion))
			{
				return;
			}
			_cachedClip = localPlayer.CurrentPlayerClipInfo;
			_cachedState = localPlayer.Anim[localPlayer.MotionPrefix + _cachedClip.Clip];
		}
		if (localPlayer.CurrentPlayerClipInfo != _cachedClip)
		{
			Clear();
			return;
		}
		PlayerBehavior player = Singleton<PlayerManager>.Instance().GetPlayer(GameManager.PlayerId);
		Vector3 currentPosition = localPlayer.CurrentPosition;
		Vector3 vector = ((!(player == null)) ? player.CurrentPosition : currentPosition);
		PlayerRootMotionPath path = _cachedClip.GetPath(localPlayer.IsMale);
		for (int num = _items.Count - 1; num >= 0; num--)
		{
			Item value = _items[num];
			if (value.Info.AttackTime < _cachedState.time)
			{
				_items.RemoveAt(num);
			}
			else
			{
				bool flag = _ghostIndex.HasValue && num >= _ghostIndex.Value;
				Vector3 vector2 = ((!flag) ? currentPosition : vector);
				Vector3 vector3 = new Vector3(value.Info.Offset.Item1, 0f, value.Info.Offset.Item2);
				if (path != null)
				{
					vector3 += path.GetDelta(_cachedState.time, value.Info.AttackTime);
				}
				vector3 = localPlayer.MainTransform.localToWorldMatrix.MultiplyVector(vector3);
				vector2 += vector3;
				int? id = value.Id;
				if (!id.HasValue)
				{
					Vector3 vector4 = Util.ClientPositionToWorldPosition(vector2);
					float num2 = Mathf.Max(0.01f, Mathf.Abs(_cachedState.speed));
					double usedAt = Connections.Frontend.GetPredictedServerTime() - (double)(_cachedState.time / num2);
					AttackAlerted? attackAlerted = value.Info.MakeAlertedAtCenter(new Vector2(vector4.x, vector4.z), localPlayer.CurrentYaw, usedAt, num2);
					if (!attackAlerted.HasValue)
					{
						_items.RemoveAt(num);
					}
					else
					{
						int num3 = AreaOfEffectVisualizer.ShowAttackAlerted((!flag) ? AreaOfEffectVisualizer.Type.Player : AreaOfEffectVisualizer.Type.Alert, attackAlerted.Value);
						if (num3 == -1)
						{
							_items.RemoveAt(num);
						}
						else
						{
							value.Id = num3;
							value.InitialYaw = localPlayer.CurrentYaw;
							_items[num] = value;
						}
					}
				}
				else
				{
					AreaOfEffectVisualizer.Move(value.Id.Value, vector2);
					AreaOfEffectVisualizer.Rotate(value.Id.Value, localPlayer.CurrentYaw - value.InitialYaw);
				}
			}
		}
	}
}

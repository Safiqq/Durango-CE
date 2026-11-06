using System;
using System.Collections.Generic;
using Durango.Network;
using Durango.Utils;
using Messages;
using Shared.Battle;
using UnityEngine;

namespace Durango.UI.InGame;

public class AreaOfEffectVisualizer : Singleton<AreaOfEffectVisualizer>
{
	public enum Type
	{
		Alert,
		Player,
		Catapult
	}

	private static int _idSequence;

	private readonly Dictionary<string, (int Id, double Until, double EventAt, AttackRangeContext Context)> _rangeMasks = new Dictionary<string, (int, double, double, AttackRangeContext)>();

	private readonly AttackRangeTimeline _rangeTimeline = new AttackRangeTimeline();

	private readonly List<string> _rangeRemovals = new List<string>();

	private float _nextRangeCleanup;

	private readonly Dictionary<string, double> _rangeCancelledAt = new Dictionary<string, double>();

	[EnumList(typeof(Type), false, 0, -1)]
	[SerializeField]
	private AreaOfEffectAlert[] _areaOfEffects;

	public static float Now()
	{
		if (Application.isPlaying)
		{
			return Time.time;
		}
		return Time.realtimeSinceStartup;
	}

	public static int GetNextId()
	{
		return _idSequence++;
	}

	public static int ShowCircle(Type type, Vector3 position, float radius, float startAt, float finishAt, float showAt, float hideAt)
	{
		if (Application.isPlaying && !Singleton<AreaOfEffectVisualizer>.HasInstance())
		{
			return -1;
		}
		if (radius <= 0f)
		{
			return -1;
		}
		if (Mathf.Max(finishAt, hideAt) < Now())
		{
			return -1;
		}
		AreaOfEffectAlert[] alerts = GetAlerts();
		if (alerts == null)
		{
			return -1;
		}
		AreaOfEffectAlert areaOfEffectAlert = alerts[(int)type];
		if (areaOfEffectAlert == null)
		{
			return -1;
		}
		return areaOfEffectAlert.ShowCircle(position, radius, startAt, finishAt, showAt, hideAt);
	}

	public static int ShowArc(Type type, Vector3 position, float radius, float startAngle, float endAngle, float startAt, float finishAt, float showAt, float hideAt)
	{
		if (Application.isPlaying && !Singleton<AreaOfEffectVisualizer>.HasInstance())
		{
			return -1;
		}
		if (radius <= 0f)
		{
			return -1;
		}
		if (Mathf.Max(finishAt, hideAt) < Now())
		{
			return -1;
		}
		AreaOfEffectAlert[] alerts = GetAlerts();
		if (alerts == null)
		{
			return -1;
		}
		AreaOfEffectAlert areaOfEffectAlert = alerts[(int)type];
		if (areaOfEffectAlert == null)
		{
			return -1;
		}
		return areaOfEffectAlert.ShowArc(position, radius, startAngle, endAngle, startAt, finishAt, showAt, hideAt);
	}

	public static int ShowRect(Type type, Vector3 position, float width, float height, float angle, float startAt, float finishAt, float showAt, float hideAt)
	{
		if (Application.isPlaying && !Singleton<AreaOfEffectVisualizer>.HasInstance())
		{
			return -1;
		}
		if (width <= 0f || height <= 0f)
		{
			return -1;
		}
		if (Mathf.Max(finishAt, hideAt) < Now())
		{
			return -1;
		}
		AreaOfEffectAlert[] alerts = GetAlerts();
		if (alerts == null)
		{
			return -1;
		}
		AreaOfEffectAlert areaOfEffectAlert = alerts[(int)type];
		if (areaOfEffectAlert == null)
		{
			return -1;
		}
		return areaOfEffectAlert.ShowRect(position, width, height, angle, startAt, finishAt, showAt, hideAt);
	}

	public static void Stop(int id, float delay = 0f)
	{
		if ((!Application.isPlaying || Singleton<AreaOfEffectVisualizer>.HasInstance()) && id != -1)
		{
			AreaOfEffectAlert[] alerts = GetAlerts();
			int i = 0;
			for (int size = KUtility.GetSize(alerts); i < size; i++)
			{
				alerts[i].Stop(id, delay);
			}
		}
	}

	public static void Move(int id, Vector3 pos)
	{
		if ((!Application.isPlaying || Singleton<AreaOfEffectVisualizer>.HasInstance()) && id != -1)
		{
			AreaOfEffectAlert[] alerts = GetAlerts();
			int i = 0;
			for (int size = KUtility.GetSize(alerts); i < size; i++)
			{
				alerts[i].Move(id, pos);
			}
		}
	}

	public static void Rotate(int id, float angle)
	{
		if (id == -1 || (Application.isPlaying && !Singleton<AreaOfEffectVisualizer>.HasInstance()))
		{
			return;
		}
		AreaOfEffectAlert[] alerts = GetAlerts();
		if (alerts == null)
		{
			return;
		}
		AreaOfEffectAlert[] array = alerts;
		foreach (AreaOfEffectAlert areaOfEffectAlert in array)
		{
			if (areaOfEffectAlert != null)
			{
				areaOfEffectAlert.Rotate(id, angle);
			}
		}
	}

	private static AreaOfEffectAlert[] GetAlerts()
	{
		if (Singleton<AreaOfEffectVisualizer>.HasInstance())
		{
			return Singleton<AreaOfEffectVisualizer>.Instance()._areaOfEffects;
		}
		if (!Application.isPlaying)
		{
			InGameUIRoot inGameUIRoot = UnityEngine.Object.FindAnyObjectByType<InGameUIRoot>();
			if (inGameUIRoot != null)
			{
				AreaOfEffectVisualizer areaOfEffectVisualizer = inGameUIRoot.MakeTempPrefabObject<AreaOfEffectVisualizer>();
				Singleton<AreaOfEffectVisualizer>.SetInstance(areaOfEffectVisualizer);
				return areaOfEffectVisualizer._areaOfEffects;
			}
		}
		return null;
	}

	private void Start()
	{
		GameSystem<CombatSystem>.Instance().AttackAlerted += OnAttackAlerted;
		GameSystem<CombatSystem>.Instance().VehicleProjectileFired += OnVehicleProjectileFired;
		Singleton<AnimalManager>.Instance().AnimalDisappeared += OnAnimalDisappeared;
		Singleton<GameManager>.Instance().PreReconnect += ClearRanges;
	}

	private void OnAttackAlerted(AttackAlerted alerted)
	{
		if (CombatSystem.MonsterAttackRangeEnabled || (alerted.RangeContext != null && alerted.RangeContext.Cancel))
		{
			ShowAttackAlerted(alerted);
		}
	}

	private void OnVehicleProjectileFired(VehicleProjectileFired fired)
	{
		ShowVehicleProjectileFired(fired);
	}

	private static string RangeOwner(string key)
	{
		int num = key.IndexOf("#range:", StringComparison.Ordinal);
		if (num >= 0)
		{
			return key.Substring(0, num);
		}
		return key;
	}

	private void OnAnimalDisappeared(AnimalBehavior animal)
	{
		if (animal == null)
		{
			return;
		}
		_rangeRemovals.Clear();
		foreach (KeyValuePair<string, (int, double, double, AttackRangeContext)> rangeMask in _rangeMasks)
		{
			if (RangeOwner(rangeMask.Key) == animal.EntityId)
			{
				_rangeRemovals.Add(rangeMask.Key);
			}
		}
		RemoveRanges();
	}

	private void RemoveRanges()
	{
		foreach (string rangeRemoval in _rangeRemovals)
		{
			Stop(_rangeMasks[rangeRemoval].Id);
			_rangeMasks.Remove(rangeRemoval);
		}
		_rangeRemovals.Clear();
	}

	private void ClearRanges()
	{
		foreach (KeyValuePair<string, (int, double, double, AttackRangeContext)> rangeMask in _rangeMasks)
		{
			Stop(rangeMask.Value.Item1);
		}
		_rangeMasks.Clear();
		_rangeCancelledAt.Clear();
		_rangeTimeline.Clear();
	}

	private void Update()
	{
		if (Time.unscaledTime < _nextRangeCleanup)
		{
			return;
		}
		_nextRangeCleanup = Time.unscaledTime + 0.1f;
		double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
		_rangeTimeline.Prune(predictedServerTime);
		_rangeRemovals.Clear();
		foreach (KeyValuePair<string, (int, double, double, AttackRangeContext)> rangeMask in _rangeMasks)
		{
			AnimalBehavior animalBehavior = (Singleton<AnimalManager>.HasInstance() ? Singleton<AnimalManager>.Instance().GetAnimal(RangeOwner(rangeMask.Key)) : null);
			if (!CombatSystem.MonsterAttackRangeEnabled || rangeMask.Value.Item2 + 0.3 < predictedServerTime || (animalBehavior != null && !animalBehavior.IsAlive))
			{
				_rangeRemovals.Add(rangeMask.Key);
			}
		}
		RemoveRanges();
		_rangeRemovals.Clear();
		foreach (KeyValuePair<string, double> item in _rangeCancelledAt)
		{
			if (item.Value + 30.0 < predictedServerTime)
			{
				_rangeRemovals.Add(item.Key);
			}
		}
		foreach (string rangeRemoval in _rangeRemovals)
		{
			_rangeCancelledAt.Remove(rangeRemoval);
		}
		_rangeRemovals.Clear();
	}

	private void OnDisable()
	{
		ClearRanges();
	}

	protected override void OnDestroyed()
	{
		if (GameSystem<CombatSystem>.HasInstance())
		{
			GameSystem<CombatSystem>.Instance().AttackAlerted -= OnAttackAlerted;
			GameSystem<CombatSystem>.Instance().VehicleProjectileFired -= OnVehicleProjectileFired;
		}
		if (Singleton<AnimalManager>.HasInstance())
		{
			Singleton<AnimalManager>.Instance().AnimalDisappeared -= OnAnimalDisappeared;
		}
		if (Singleton<GameManager>.HasInstance())
		{
			Singleton<GameManager>.Instance().PreReconnect -= ClearRanges;
		}
	}

	public static int ShowVehicleProjectileFired(VehicleProjectileFired fired)
	{
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		bool num = localPlayer != null && localPlayer.Driver.Vehicle != null && localPlayer.Driver.Vehicle.EntityId == fired.EntityId;
		Vector3 position = fired.TargetPosition.ToClientPosition();
		double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
		float time = Time.time;
		float showAt = ((!num) ? (time - (float)(predictedServerTime - fired.WarnSince)) : time);
		float finishAt = time - (float)(predictedServerTime - fired.DmgAt);
		float hideAt = time - (float)(predictedServerTime - fired.WarnUntil);
		return ShowCircle(Type.Catapult, position, fired.DmgRadius, time, finishAt, showAt, hideAt);
	}

	public static int ShowAttackAlerted(AttackAlerted alert)
	{
		return ShowAttackAlerted(Type.Alert, alert);
	}

	public static int ShowAttackAlerted(Type type, AttackAlerted alert)
	{
		if (alert.RangeContext != null && alert.RangeContext.Cancel)
		{
			if (!Singleton<AreaOfEffectVisualizer>.HasInstance())
			{
				return -1;
			}
			AreaOfEffectVisualizer areaOfEffectVisualizer = Singleton<AreaOfEffectVisualizer>.Instance();
			AttackRangeContext rangeContext = alert.RangeContext;
			double predictedServerTime = Connections.Frontend.GetPredictedServerTime();
			if (!areaOfEffectVisualizer._rangeTimeline.Accept(rangeContext, predictedServerTime, predictedServerTime, out var _))
			{
				return -1;
			}
			areaOfEffectVisualizer._rangeRemovals.Clear();
			foreach (KeyValuePair<string, (int, double, double, AttackRangeContext)> rangeMask in areaOfEffectVisualizer._rangeMasks)
			{
				if (rangeMask.Value.Item4 != null && rangeMask.Value.Item4.OwnerId == rangeContext.OwnerId && rangeMask.Value.Item4.AttackStartAt <= rangeContext.AttackStartAt)
				{
					areaOfEffectVisualizer._rangeRemovals.Add(rangeMask.Key);
				}
			}
			areaOfEffectVisualizer.RemoveRanges();
			return -1;
		}
		if (alert.DamageType == DamageType.Invalid && !alert.Center.HasValue && !string.IsNullOrEmpty(alert.EntityId))
		{
			if (!Singleton<AreaOfEffectVisualizer>.HasInstance() || double.IsNaN(alert.EventAt) || double.IsInfinity(alert.EventAt))
			{
				return -1;
			}
			AreaOfEffectVisualizer areaOfEffectVisualizer2 = Singleton<AreaOfEffectVisualizer>.Instance();
			string text = RangeOwner(alert.EntityId);
			if (areaOfEffectVisualizer2._rangeCancelledAt.TryGetValue(text, out var value) && alert.EventAt <= value)
			{
				return -1;
			}
			areaOfEffectVisualizer2._rangeCancelledAt[text] = alert.EventAt;
			areaOfEffectVisualizer2._rangeRemovals.Clear();
			foreach (KeyValuePair<string, (int, double, double, AttackRangeContext)> rangeMask2 in areaOfEffectVisualizer2._rangeMasks)
			{
				if (RangeOwner(rangeMask2.Key) == text && rangeMask2.Value.Item3 <= alert.EventAt)
				{
					areaOfEffectVisualizer2._rangeRemovals.Add(rangeMask2.Key);
				}
			}
			areaOfEffectVisualizer2.RemoveRanges();
			return -1;
		}
		WorldPosition? center = alert.Center;
		if (!center.HasValue)
		{
			return -1;
		}
		Vector3 vector = alert.Center.Value.ToClientPosition();
		if (Application.isPlaying && (PlayerBehavior.LocalPlayer == null || (vector - PlayerBehavior.LocalPlayer.CurrentPosition).magnitude > 2000f))
		{
			return -1;
		}
		double predictedServerTime2 = Connections.Frontend.GetPredictedServerTime();
		float num = Now();
		float num2 = num - (float)(predictedServerTime2 - alert.EventAt);
		float num3 = num - (float)(predictedServerTime2 - alert.AttackTime);
		if (alert.GroundMask != null)
		{
			if (!alert.GroundMask.IsValid() || double.IsNaN(alert.EventAt) || double.IsInfinity(alert.EventAt) || double.IsNaN(alert.AttackTime) || double.IsInfinity(alert.AttackTime))
			{
				return -1;
			}
			AreaOfEffectAlert[] alerts = GetAlerts();
			if (alerts == null || alerts[(int)type] == null)
			{
				return -1;
			}
			float num4 = num - (float)(predictedServerTime2 - alert.GroundMask.ActiveUntil);
			if (num4 < num)
			{
				return -1;
			}
			AreaOfEffectVisualizer areaOfEffectVisualizer3 = Singleton<AreaOfEffectVisualizer>.Instance();
			if (alert.RangeContext != null)
			{
				if (!areaOfEffectVisualizer3._rangeTimeline.Accept(alert.RangeContext, predictedServerTime2, alert.GroundMask.ActiveUntil, out var newAttack2))
				{
					if (string.IsNullOrEmpty(alert.EntityId) || !areaOfEffectVisualizer3._rangeMasks.TryGetValue(alert.EntityId, out (int, double, double, AttackRangeContext) value2) || value2.Item4 == null || value2.Item4.AttackStartAt != alert.RangeContext.AttackStartAt)
					{
						return -1;
					}
					return value2.Item1;
				}
				if (newAttack2)
				{
					areaOfEffectVisualizer3._rangeRemovals.Clear();
					foreach (KeyValuePair<string, (int, double, double, AttackRangeContext)> rangeMask3 in areaOfEffectVisualizer3._rangeMasks)
					{
						if (rangeMask3.Value.Item4 != null && rangeMask3.Value.Item4.OwnerId == alert.RangeContext.OwnerId && rangeMask3.Value.Item4.AttackStartAt < alert.RangeContext.AttackStartAt)
						{
							areaOfEffectVisualizer3._rangeRemovals.Add(rangeMask3.Key);
						}
					}
					areaOfEffectVisualizer3.RemoveRanges();
				}
			}
			if (!string.IsNullOrEmpty(alert.EntityId) && areaOfEffectVisualizer3._rangeCancelledAt.TryGetValue(RangeOwner(alert.EntityId), out var value3) && alert.EventAt <= value3)
			{
				return -1;
			}
			if (!string.IsNullOrEmpty(alert.EntityId) && areaOfEffectVisualizer3._rangeMasks.TryGetValue(alert.EntityId, out (int, double, double, AttackRangeContext) value4))
			{
				if (alert.RangeContext == null && alert.EventAt <= value4.Item3)
				{
					return value4.Item1;
				}
				Stop(value4.Item1);
			}
			int num5 = alerts[(int)type].ShowMask(alert.GroundMask, num2, num3, num4);
			if (num5 != -1 && !string.IsNullOrEmpty(alert.EntityId))
			{
				areaOfEffectVisualizer3._rangeMasks[alert.EntityId] = (num5, alert.GroundMask.ActiveUntil, alert.EventAt, alert.RangeContext);
			}
			return num5;
		}
		bool num6 = alert.DamageType == DamageType.Melee || alert.DamageType == DamageType.CircularArea || alert.DamageType == DamageType.Ranged;
		bool flag = alert.DamageType == DamageType.RectangularArea;
		bool flag2 = alert.DamageType == DamageType.Melee || alert.DamageType == DamageType.CircularArea;
		float num7 = ((!alert.Yaw.HasValue) ? 0f : alert.Yaw.Value);
		if (num6 && alert.Radius.HasValue && alert.Radius.Value > 0f)
		{
			float startAngle;
			float endAngle;
			if (flag2 && alert.Angles.HasValue)
			{
				startAngle = num7 + alert.Angles.Value.x;
				endAngle = num7 + alert.Angles.Value.y;
			}
			else
			{
				startAngle = 0f;
				endAngle = 360f;
			}
			return ShowArc(type, vector, alert.Radius.Value, startAngle, endAngle, num2, num3, num2, num3);
		}
		if (flag && alert.RectSizeHalves.HasValue)
		{
			Vector2 vector2 = alert.RectSizeHalves.Value * 2f;
			if (vector2.x > 0f && vector2.y > 0f)
			{
				return ShowRect(type, vector, vector2.x, vector2.y, num7, num2, num3, num2, num3);
			}
		}
		return -1;
	}

	[ExposedInEditor(null)]
	public void TestShowRect(Type type, Vector3 position, float width, float height, float angle, float duration)
	{
		duration = ((!(duration > 0f)) ? 3f : duration);
		float num = ((!Application.isPlaying) ? Time.realtimeSinceStartup : Time.time);
		ShowRect(type, position, width, height, angle, num, num + duration, num, num + duration);
	}

	[ExposedInEditor(null)]
	public void TestShowArc(Type type, Vector3 position, float radius, float startAngle, float endAngle, float duration)
	{
		duration = ((!(duration > 0f)) ? 3f : duration);
		float num = ((!Application.isPlaying) ? Time.realtimeSinceStartup : Time.time);
		if (endAngle <= startAngle)
		{
			ShowCircle(type, position, radius, num, num + duration, num, num + duration);
		}
		else
		{
			ShowArc(type, position, radius, startAngle, endAngle, num, num + duration, num, num + duration);
		}
	}
}

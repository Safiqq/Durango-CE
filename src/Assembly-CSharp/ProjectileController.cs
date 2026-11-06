using System;
using System.Collections.Generic;
using Durango.Render.Effect;
using Durango.Render.Particle;
using Durango.Utils;
using Durango.Utils.Extensions;
using JetBrains.Annotations;
using Messages;
using Shared.Battle;
using UnityEngine;

public class ProjectileController
{
	private GameObject _arrowObjTemplate;

	private GameObject _arrowObj;

	private GameObject _arrowObjTrail;

	private readonly List<Projectile> _projectileList = new List<Projectile>();

	private readonly Transform _aimBasis;

	private readonly Transform _yawTransform;

	private readonly bool _fireUntargetProjectile;

	[CanBeNull]
	private DamageEffectManager.ProjectileSet _currentProjectileSet;

	private Projectile _lastProjectile;

	private bool _missed = true;

	public Func<string, Transform> WeaponBoneResolver;

	public BodyPart TargetPart { get; private set; }

	public Vector3? TargetPos { get; private set; }

	public DamageableEntity Target { get; private set; }

	public ProjectileController(Transform aimBasis, Transform yawTransform)
	{
		_yawTransform = yawTransform;
		_aimBasis = aimBasis;
		_fireUntargetProjectile = true;
	}

	public ProjectileController(Transform aimBasis)
	{
		_yawTransform = null;
		_aimBasis = aimBasis;
		_fireUntargetProjectile = false;
	}

	private bool IsOverridableTarget(Projectile projectile)
	{
		if (projectile == null)
		{
			return false;
		}
		if (projectile.HasTarget())
		{
			return false;
		}
		if (projectile.ShootedAt.HasValue && projectile.ShootedAt.Value + 1.5f < Time.time)
		{
			return false;
		}
		return true;
	}

	public void SetTarget(DamageableEntity target, BodyPart part, bool missed)
	{
		if (IsOverridableTarget(_lastProjectile))
		{
			_lastProjectile.Missed = missed;
			_lastProjectile.SetTarget(target, part);
			return;
		}
		Target = target;
		TargetPart = part;
		TargetPos = null;
		_missed = missed;
	}

	public void SetTarget(Vector3 target, bool missed)
	{
		if (IsOverridableTarget(_lastProjectile))
		{
			_lastProjectile.Missed = missed;
			_lastProjectile.SetTarget(target);
			return;
		}
		TargetPos = target;
		Target = null;
		TargetPart = BodyPart.Invalid;
		_missed = missed;
	}

	public void SetWeaponData(WeaponDisplayInfo weaponDisplayInfo)
	{
		string text = ((_currentProjectileSet != null) ? _currentProjectileSet.ProjectilePrefab.Path : null);
		_currentProjectileSet = null;
		if (string.IsNullOrEmpty(weaponDisplayInfo.Projectile))
		{
			return;
		}
		string text2 = weaponDisplayInfo.Projectile;
		string text3 = null;
		int num = text2.IndexOf('#');
		if (num >= 0)
		{
			text3 = text2.Substring(num + 1);
			text2 = text2.Substring(0, num);
		}
		DamageEffectManager.ProjectileType projectileType = text2.ToEnum(DamageEffectManager.ProjectileType.Arrow);
		_currentProjectileSet = Singleton<DamageEffectManager>.Instance().ProjectileSets[(int)projectileType].Copy();
		if (_currentProjectileSet == null)
		{
			return;
		}
		if (!string.IsNullOrEmpty(text3))
		{
			ApplyWeaponFx(_currentProjectileSet, text3);
		}
		if (_currentProjectileSet.ProjectilePrefab.Path != text)
		{
			_arrowObjTemplate = null;
			ForceRemoveUnfiredArrow();
		}
		if (weaponDisplayInfo.ProjectileSpeed.HasValue)
		{
			_currentProjectileSet.Speed = weaponDisplayInfo.ProjectileSpeed.Value;
		}
		if (weaponDisplayInfo.DetonateDelay.HasValue)
		{
			_currentProjectileSet.ExplosionDelay = weaponDisplayInfo.DetonateDelay.Value;
		}
		string prefabPath = _currentProjectileSet.ProjectilePrefab.Path;
		Singleton<AssetBundleManager>.Instance().RequestAsset(prefabPath, typeof(GameObject), delegate(UnityEngine.Object asset)
		{
			if (this != null && _currentProjectileSet != null && _currentProjectileSet.ProjectilePrefab.Path == prefabPath)
			{
				_arrowObjTemplate = (GameObject)asset;
			}
		});
		SoundManager.PrepareEvent(_currentProjectileSet.ProjectileGlidingSound);
		SoundManager.PrepareEvent(_currentProjectileSet.ExplosionSound);
		if (_currentProjectileSet.CustomFx)
		{
			ParticleManager.Cache(_currentProjectileSet.MuzzleParticle);
			ParticleManager.Cache(_currentProjectileSet.ExplosionParticle.Path);
		}
	}

	private static void ApplyWeaponFx(DamageEffectManager.ProjectileSet set, string fx)
	{
		string[] array = fx.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string text = array[i].Substring(0, num);
				string text2 = array[i].Substring(num + 1);
				switch (text)
				{
				case "prefab":
					set.ProjectilePrefab = new ParticleType
					{
						Path = text2
					};
					break;
				case "trail":
					set.ProjectileTrailName = text2;
					break;
				case "muzzle":
					set.MuzzleParticle = text2;
					break;
				case "bone":
					set.MuzzleBone = text2;
					break;
				case "hit":
					set.ExplosionParticle = new ParticleType
					{
						Path = text2
					};
					break;
				case "sound":
					set.ProjectileGlidingSound = new SoundEventType
					{
						Path = text2
					};
					break;
				case "hit_sound":
					set.ExplosionSound = new SoundEventType
					{
						Path = text2
					};
					break;
				case "stick":
					set.StickToTarget = text2 == "1";
					break;
				}
			}
		}
		set.CustomFx = true;
	}

	public void ModifyProjectileSpeed(float speed)
	{
		if (_currentProjectileSet != null)
		{
			_currentProjectileSet.Speed = speed;
		}
	}

	public float EstimateLaunchingTime(float distance)
	{
		if (_currentProjectileSet == null || _currentProjectileSet.Speed <= 0f)
		{
			return 0f;
		}
		return distance / _currentProjectileSet.Speed;
	}

	public void ChargedProjectile()
	{
		if (_currentProjectileSet == null)
		{
			return;
		}
		if (_arrowObj == null)
		{
			PrepareArrow();
			if (_arrowObj == null)
			{
				return;
			}
		}
		Projectile projectile = new Projectile(_currentProjectileSet);
		projectile.Arrow = _arrowObj.transform;
		_lastProjectile = projectile;
	}

	private void PrepareArrow()
	{
		DamageEffectManager.ProjectileSet currentProjectileSet = _currentProjectileSet;
		if (currentProjectileSet == null)
		{
			return;
		}
		ForceRemoveUnfiredArrow();
		_arrowObj = MakeArrow();
		if (!(_arrowObj == null))
		{
			Transform transform = _arrowObj.transform.Find(currentProjectileSet.ProjectileTrailName);
			if (transform != null)
			{
				_arrowObjTrail = transform.gameObject;
				_arrowObjTrail.SetActive(value: false);
			}
		}
	}

	private GameObject MakeArrow()
	{
		if (_currentProjectileSet == null)
		{
			return null;
		}
		if (_arrowObjTemplate == null)
		{
			return null;
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(_arrowObjTemplate, _aimBasis);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		return gameObject;
	}

	public void OnRemoved()
	{
		ForceRemoveUnfiredArrow();
		for (int num = _projectileList.Count - 1; num >= 0; num--)
		{
			Projectile projectile = _projectileList[num];
			if (projectile.NeedToDestroy())
			{
				if (projectile.Arrow != null)
				{
					UnityEngine.Object.Destroy(projectile.Arrow.gameObject);
				}
				_projectileList.RemoveAt(num);
			}
		}
	}

	public void ForceRemoveUnfiredArrow()
	{
		if (_arrowObj != null)
		{
			UnityEngine.Object.Destroy(_arrowObj);
			_arrowObj = null;
		}
	}

	public void ShootProjectile()
	{
		DamageEffectManager.ProjectileSet currentProjectileSet = _currentProjectileSet;
		if (currentProjectileSet == null)
		{
			return;
		}
		if (!_fireUntargetProjectile && !TargetPos.HasValue && Target == null)
		{
			ForceRemoveUnfiredArrow();
			return;
		}
		if (_arrowObj == null)
		{
			ChargedProjectile();
			if (_arrowObj == null)
			{
				return;
			}
		}
		if (_lastProjectile == null)
		{
			return;
		}
		_lastProjectile.BeginTime = Time.time;
		Transform transform = null;
		if (currentProjectileSet.CustomFx && !string.IsNullOrEmpty(currentProjectileSet.MuzzleBone) && WeaponBoneResolver != null)
		{
			transform = WeaponBoneResolver(currentProjectileSet.MuzzleBone);
		}
		if (transform != null && _lastProjectile.Arrow != null)
		{
			_lastProjectile.Arrow.transform.position = transform.position;
		}
		if (currentProjectileSet.CustomFx && !string.IsNullOrEmpty(currentProjectileSet.MuzzleParticle))
		{
			Vector3 pos = ((transform != null) ? transform.position : ((_lastProjectile.Arrow != null) ? _lastProjectile.Arrow.position : _aimBasis.position));
			Vector3 forward = ((transform != null) ? transform.forward : ((_yawTransform != null) ? _yawTransform.forward : Vector3.forward));
			ParticleManager.Emit(currentProjectileSet.MuzzleParticle, pos, Quaternion.LookRotation(forward));
		}
		if (_lastProjectile.Arrow != null)
		{
			_lastProjectile.Missed = _missed;
			_lastProjectile.BeginPos = _lastProjectile.Arrow.transform.position;
			if (TargetPos.HasValue)
			{
				_lastProjectile.SetTarget(TargetPos.Value);
			}
			else if (Target != null)
			{
				_lastProjectile.SetTarget(Target, TargetPart);
			}
			SoundManager.PlayEvent(currentProjectileSet.ProjectileGlidingSound, SoundPosition.Fix(_lastProjectile.BeginPos));
		}
		_lastProjectile.MissedTargetPos = _lastProjectile.BeginPos + _yawTransform.forward * 2000f;
		_lastProjectile.Shoot();
		_arrowObj.transform.parent = null;
		_arrowObj = null;
		if (_arrowObjTrail != null)
		{
			_arrowObjTrail.SetActive(value: true);
			_arrowObjTrail = null;
		}
		_projectileList.Add(_lastProjectile);
		TargetPos = null;
		Target = null;
		_missed = true;
	}

	public void UpdateProjectiles()
	{
		for (int num = _projectileList.Count - 1; num >= 0; num--)
		{
			Projectile projectile = _projectileList[num];
			if (projectile == null)
			{
				_projectileList.RemoveAt(num);
			}
			else
			{
				if (projectile.TimeToDetonate())
				{
					projectile.Detonate();
				}
				if (projectile.NeedToDestroy())
				{
					if (projectile.Arrow != null)
					{
						UnityEngine.Object.Destroy(projectile.Arrow.gameObject);
					}
					_projectileList.RemoveAt(num);
				}
				if (!(projectile.HitTime > 0f) && projectile.Process())
				{
					projectile.HitTime = Time.time;
					projectile.Hit();
				}
			}
		}
	}
}
